#!/usr/bin/env python3
"""Turn the exported Stage 1 world into a Tiled project, painted with Pixel Crawler.

    node tools/export-stage01.mjs      # capture the generated world (once)
    python3 tools/make-tmx.py          # write the .tmx and .tsx

Produces, under the client's wwwroot so Tiled edits ARE what the game loads:

    Maps/Tilesets/*.tsx                external tilesets
    Maps/Outside/Stage01_Outside.tmx   the official Stage 1 map

The Pixel Crawler pack is already a native 16x16 grid, so every .tsx REFERENCES
its PNG in place — no image is copied, sliced or rescaled.

Terrain is autotiled. Each Floors material is a 5x5 ring of "material with a bite
taken out of one side" around a hole, plus solid fills; picking the tile whose bite
faces the neighbouring material is what gives the organic edges. Water uses its
shoreline ring (an island in water) on the LAND side, so beaches appear by
themselves. Cliffs get a plateau top plus a vertical face drawn on the cell below,
on its own layer so it never blocks.
"""
from PIL import Image
import json, os, zlib, base64, struct, xml.sax.saxutils as sx

ROOT = "src/AlfarQuest.Client/wwwroot"
MAPS = os.path.join(ROOT, "Maps")
TSDIR = os.path.join(MAPS, "Tilesets")
MAPDIR = os.path.join(MAPS, "Outside")
PC = os.path.join(MAPS, "Pixel Crawler")
WORLD = "tools/refs/stage01-world.json"

T, ENGINE_TILE = 16, 32
SUB = ENGINE_TILE // T

os.makedirs(TSDIR, exist_ok=True)
os.makedirs(MAPDIR, exist_ok=True)

# --------------------------------------------------------------- tilesets ----
# name -> (png path relative to the .tsx, columns, rows)
def dims(p):
    im = Image.open(os.path.join(PC, p))
    return im.width // T, im.height // T

SETS = {
    "Floors":     "Environment/Tilesets/Floors_Tiles.png",
    "Water":      "Environment/Tilesets/Water_tiles.png",
    "Walls":      "Environment/Tilesets/Wall_Tiles.png",
    "Vegetation": "Environment/Props/Static/Vegetation.png",
    "Rocks":      "Environment/Props/Static/Rocks.png",
    # Tree sheets, each referenced at 16px so a tree can be stamped tile by tile.
    "TreeOakBig":   "Environment/Props/Static/Trees/Model_01/Size_04.png",
    "TreeOakSmall": "Environment/Props/Static/Trees/Model_01/Size_02.png",
    "TreePineBig":  "Environment/Props/Static/Trees/Model_02/Size_04.png",
    "TreePineMid":  "Environment/Props/Static/Trees/Model_02/Size_03.png",
    "TreePineLow":  "Environment/Props/Static/Trees/Model_02/Size_02.png",
    "TreeTall":     "Environment/Props/Static/Trees/Model_03/Size_03.png",
}

firstgid, GID = 1, {}
tsrefs = []
for name, rel in SETS.items():
    cols, rows = dims(rel)
    GID[name] = (firstgid, cols)
    src = os.path.relpath(os.path.join(PC, rel), TSDIR).replace(os.sep, "/")
    with open(os.path.join(TSDIR, f"{name}.tsx"), "w") as f:
        f.write(f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- Pixel Crawler, referenced in place: the pack is already a native 16x16 grid,
     so nothing is copied, sliced or rescaled. -->
<tileset version="1.10" tiledversion="1.10.2" name="{name}" tilewidth="{T}" tileheight="{T}" tilecount="{cols*rows}" columns="{cols}">
 <image source="{sx.escape(src)}" width="{cols*T}" height="{rows*T}"/>
</tileset>
''')
    tsrefs.append(f' <tileset firstgid="{firstgid}" source="../Tilesets/{name}.tsx"/>')
    firstgid += cols * rows

def gid(setname, col, row):
    base, cols = GID[setname]
    return base + row * cols + col

# ---------------------------------------------------------------- autotile ----
# A Floors material: block of 5 columns (ring at rows 0-4), solid fills rows 10-11.
FLOOR_BLOCK = {"grass": 0, "cobble": 5, "dirt": 10}
# Which ring cell carries the bite facing a missing neighbour. Values are the
# (dx, dy) inside the 5x5 ring; the three-long runs are variants.
RING = {
    "S":  [(1, 0), (2, 0), (3, 0)],      # material above, bite below
    "N":  [(1, 4), (2, 4), (3, 4)],
    "E":  [(0, 1), (0, 2), (0, 3)],
    "W":  [(4, 1), (4, 2), (4, 3)],
    "SE": [(1, 1)], "SW": [(3, 1)], "NE": [(1, 3)], "NW": [(3, 3)],
}
SOLID = [(1, 10), (2, 10), (3, 10)]          # one tone; row 11 is a shade off
SOLID_ALT = [(1, 11), (2, 11), (3, 11)]      # sprinkled sparingly for life

def vary(opts, x, y):
    return opts[((x * 73856093) ^ (y * 19349663)) % len(opts)]

def floor_tile(mat, x, y, n, e, s, w):
    """A Floors tile for a cell of `mat` whose neighbours (n/e/s/w True = same
    material). Missing sides pick the ring tile whose bite faces that way."""
    bx = FLOOR_BLOCK[mat]
    miss = ("" if n else "N") + ("" if s else "S") + ("" if e else "E") + ("" if w else "W")
    key = {"": None, "N": "N", "S": "S", "E": "E", "W": "W",
           "NE": "NE", "NW": "NW", "SE": "SE", "SW": "SW"}.get(miss)
    if key is None:                                  # surrounded, or a shape the
        dx, dy = vary(SOLID, x, y)                   # ring cannot express
        return gid("Floors", bx + dx, dy)
    dx, dy = vary(RING[key], x, y)
    return gid("Floors", bx + dx, dy)

# Water: shoreline ring is an ISLAND in water, so it is placed on the LAND cell.
WATER_BLOCK = [0, 6]
WSOLID = [(c, r) for r in (6, 7, 8) for c in range(10)]

def water_solid(x, y):
    c, r = vary(WSOLID, x, y)
    return gid("Water", c, r)

def shore_tile(x, y, wn, we, ws, ww):
    """A land cell touching water: the island ring tile whose water faces the same
    way. Returns 0 when the shape is not expressible (leave the land as it is)."""
    bx = WATER_BLOCK[((x + y) // 7) % 2]
    key = ("N" if wn else "") + ("S" if ws else "") + ("E" if we else "") + ("W" if ww else "")
    at = {"N": (2, 0), "S": (2, 4), "E": (4, 2), "W": (0, 2),
          "NW": (1, 1), "NE": (3, 1), "SW": (1, 3), "SE": (3, 3)}.get(key)
    return gid("Water", bx + at[0], at[1]) if at else 0

# Cliffs. The grey plateau block is cols 6-11: a full rim (top row 0, bottom row 5,
# left col 6, right col 11, corners at the four ends) around an interior at
# cols 7-10 rows 1-4, with the vertical face on rows 6-7 and its grass-footed
# variant on rows 8-9. Using the rim is what stops the range reading as a slab.
CX = 0                                  # brown block: the grey one is a flat dungeon wall
def cliff_top(mx, my, n, e, s_, w_):
    """A plateau tile picked from the rim that matches which sides drop away."""
    if not n and not w_: return gid("Walls", CX + 0, 0)
    if not n and not e:  return gid("Walls", CX + 5, 0)
    if not s_ and not w_: return gid("Walls", CX + 0, 5)
    if not s_ and not e:  return gid("Walls", CX + 5, 5)
    if not n:  return gid("Walls", CX + vary([1, 2, 3, 4], mx, my), 0)
    if not s_: return gid("Walls", CX + vary([1, 2, 3, 4], mx, my), 5)
    if not w_: return gid("Walls", CX + 0, vary([1, 2, 3, 4], mx, my))
    if not e:  return gid("Walls", CX + 5, vary([1, 2, 3, 4], mx, my))
    return gid("Walls", CX + vary([1, 2, 3, 4], mx, my), vary([1, 2, 3, 4], mx, my))

def cliff_face(mx, my, row):
    """The vertical wall under a southern edge: row 6 is its top course, row 8 the
    course that meets grass."""
    return gid("Walls", CX + vary([1, 2, 3, 4], mx, my), row)

# ------------------------------------------------------------------ world ----
w = json.load(open(WORLD))
COLS, ROWS = w["cols"], w["rows"]
MW, MH = COLS * SUB, ROWS * SUB
rows = w["tiles"]
def at(x, y):
    if x < 0 or y < 0 or x >= COLS or y >= ROWS: return "#"
    return rows[y][x]

blank = [0] * (MW * MH)
LNAMES = ["Ground", "GroundDetails", "Roads", "Bridges", "Water", "Shore", "Cliffs",
          "CliffFace", "Buildings", "Objects", "Trees", "AbovePlayer", "Shadows"]
layers = {n: list(blank) for n in LNAMES}

def put(layer, mx, my, g):
    """One MAP cell (16px). Terrain is painted at the art's own resolution — filling
    an engine cell's whole 2x2 block with one tile made every edge and every tuft
    read as a doubled 32px blob."""
    if not g or not (0 <= mx < MW and 0 <= my < MH): return
    layers[layer][my * MW + mx] = g

# The engine reasons at 32px but the art is 16px, so the terrain is upsampled to
# the map grid FIRST and autotiled there: edges are then computed against 16px
# neighbours, which is what makes them read as fringes instead of blocks.
M = [[at(mx // SUB, my // SUB) for my in range(MH)] for mx in range(MW)]
def m(mx, my):
    if mx < 0 or my < 0 or mx >= MW or my >= MH: return "#"
    return M[mx][my]

for my in range(MH):
    for mx in range(MW):
        c = m(mx, my)

        # Ground: grass under absolutely everything, so erasing anything above
        # reveals a finished field rather than a hole.
        dx, dy = vary(SOLID, mx, my)
        put("Ground", mx, my, gid("Floors", FLOOR_BLOCK["grass"] + dx, dy))

        if c == ",":                                  # dirt road, autotiled
            k = lambda xx, yy: m(xx, yy) == ","
            put("Roads", mx, my, floor_tile("dirt", mx, my,
                k(mx, my-1), k(mx+1, my), k(mx, my+1), k(mx-1, my)))
        elif c == "=":                                # bridge deck — cobble
            k = lambda xx, yy: m(xx, yy) in "=,"
            put("Bridges", mx, my, floor_tile("cobble", mx, my,
                k(mx, my-1), k(mx+1, my), k(mx, my+1), k(mx-1, my)))
        elif c == "~":
            put("Water", mx, my, water_solid(mx, my))
        elif c == "#":
            n, sth = m(mx, my-1) == "#", m(mx, my+1) == "#"
            e_, w_ = m(mx+1, my) == "#", m(mx-1, my) == "#"
            put("Cliffs", mx, my, cliff_top(mx, my, n, e_, sth, w_))
            if not sth:
                # Two courses of wall below the drop, the lower one footed in grass.
                put("CliffFace", mx, my + 1, cliff_face(mx, my, 6))
                put("CliffFace", mx, my + 2, cliff_face(mx, my, 8))

# Beaches: a land cell touching water gets the island ring, so the shoreline
# draws itself. Done after, so it sits over the ground already laid.
#
# On its OWN layer, not on Water. Water is what the engine reads to decide where
# you may not walk, and this ring is drawn on the LAND cell — putting it there
# turned the last strip of every beach into deep water you could not stand on.
for my in range(MH):
    for mx in range(MW):
        if m(mx, my) in "~#": continue
        wn, ws = m(mx, my-1) == "~", m(mx, my+1) == "~"
        we, ww = m(mx+1, my) == "~", m(mx-1, my) == "~"
        if wn or ws or we or ww:
            put("Shore", mx, my, shore_tile(mx, my, wn, we, ws, ww))

# ----- scatter Pixel Crawler vegetation where our props say greenery stands ----
# Our prop kind -> a single-tile Pixel Crawler sprite. Only the 1-tile plants from
# rows 9-14 of the Vegetation sheet are used here; the 3x3 bushes and the trees are
# multi-tile and are stamped separately.
VEG = {
    "bush":    ("Vegetation", [(4, 10), (5, 10), (6, 10), (4, 11), (5, 11), (6, 11)]),
    "flowers": ("Vegetation", [(12, 11), (13, 11), (14, 11), (12, 10), (13, 10)]),
    "log":     ("Vegetation", [(15, 8)]),
    "rock":    ("Rocks",      [(0, 2), (1, 2), (2, 2), (3, 2), (0, 4), (1, 4)]),
    "orePile": ("Rocks",      [(0, 8), (1, 8), (2, 8)]),
}
for p in w["props"]:
    veg = VEG.get(p["kind"])
    if not veg: continue
    mx, my = int(p["x"]) // T, int(p["y"]) // T
    setname, opts = veg
    c, r = vary(opts, mx, my)
    put("Objects", mx, my, gid(setname, c, r))


# ----------------------------------------------------------------- trees ----
# Each tree is a rectangle of tiles inside its sheet. Boxes came from detecting
# connected non-transparent regions (tools: see the session notes) and are snapped
# out to whole 16px tiles. A tree is stamped bottom-centred on the prop's cell,
# and fully transparent source tiles are skipped so one tree never erases the
# canopy of the one behind it.
SHEET_ALPHA = {}
def sheet_alpha(setname):
    if setname not in SHEET_ALPHA:
        im = Image.open(os.path.join(PC, SETS[setname])).convert("RGBA")
        SHEET_ALPHA[setname] = im.split()[3].load(), im.width // T, im.height // T
    return SHEET_ALPHA[setname]

def box_tiles(setname, x, y, w, h):
    """The sheet's tile rect enclosing a pixel box."""
    tx0, ty0 = x // T, y // T
    tx1, ty1 = -(-(x + w) // T), -(-(y + h) // T)
    return tx0, ty0, tx1 - tx0, ty1 - ty0

# kind -> [(tileset, px box), ...] variants
TREES = {
    "broadleaf": [("TreeOakBig", (3, 2, 73, 126)), ("TreeOakBig", (83, 2, 73, 126)),
                  ("TreeOakBig", (166, 2, 69, 126)), ("TreeOakBig", (246, 2, 69, 126))],
    "cherry":    [("TreeOakSmall", (32, 2, 32, 62)), ("TreeOakSmall", (96, 2, 32, 62))],
    "pine":      [("TreePineMid", (4, 4, 37, 76)), ("TreePineMid", (52, 4, 37, 76)),
                  ("TreePineMid", (99, 6, 40, 74)), ("TreePineBig", (4, 9, 54, 103)),
                  ("TreePineBig", (68, 9, 54, 103)), ("TreeTall", (0, 1, 63, 143))],
    "deadTree":  [("Vegetation", (12 * 16, 4 * 16, 3 * 16, 5 * 16)),
                  ("Vegetation", (13 * 16, 4 * 16, 2 * 16, 5 * 16))],
}

def stamp_tree(kind, mx, my):
    opts = TREES.get(kind)
    if not opts: return False
    setname, box = opts[((mx * 92837111) ^ (my * 689287499)) % len(opts)]
    tx0, ty0, tw, th = box_tiles(setname, *box)
    alpha, scols, srows = sheet_alpha(setname)
    ox, oy = mx - tw // 2, my - th + 1          # bottom-centred on the prop cell
    for dy in range(th):
        for dx in range(tw):
            sx, sy = tx0 + dx, ty0 + dy
            if sx >= scols or sy >= srows: continue
            if not any(alpha[sx * T + px, sy * T + py] > 8
                       for py in range(0, T, 2) for px in range(0, T, 2)):
                continue                        # blank source tile — leave the map alone
            put("Trees", ox + dx, oy + dy, gid(setname, sx, sy))
    return True

planted = 0
for p in w["props"]:
    mx, my = int(p["x"]) // T, int(p["y"]) // T
    if stamp_tree(p["kind"], mx, my): planted += 1

# ------------------------------------------------------------------- write ----
def encode(data):
    return base64.b64encode(zlib.compress(struct.pack(f"<{len(data)}I", *data), 9)).decode()

def props_xml(pairs, indent="   "):
    if not pairs: return ""
    body = "".join(f'{indent} <property name="{k}" value="{sx.escape(str(v), {chr(34): "&quot;"})}"/>\n'
                   for k, v in pairs)
    return f"{indent}<properties>\n{body}{indent}</properties>\n"

def obj(oid, name, x, y, pairs, wpx=0, hpx=0, point=True):
    a = f'id="{oid}" name="{sx.escape(name)}" x="{x/2:.2f}" y="{y/2:.2f}"'
    if wpx or hpx: a += f' width="{wpx/2:.2f}" height="{hpx/2:.2f}"'
    inner = props_xml(pairs)
    if point and not (wpx or hpx): inner += "   <point/>\n"
    return f'  <object {a}>\n{inner}  </object>\n'

oid = 1
def nid():
    global oid; oid += 1; return oid - 1

og = {}
og["PlayerSpawn"] = [obj(nid(), "PlayerSpawn", w["spawn"]["x"], w["spawn"]["y"], [("Type", "PlayerSpawn")])]
og["NPCSpawn"] = [obj(nid(), n["id"], n["x"], n["y"], [("NpcId", n["id"])]) for n in w["npcs"]]
og["EnemySpawn"] = [obj(nid(), c["id"], c["x"], c["y"], [("EnemyId", c["id"]), ("Respawn", "true")])
                    for c in w["creatures"]]
og["TreasureSpawn"] = [obj(nid(), t["name"], t["x"], t["y"],
                           [("ContainerKind", t["kind"]), ("InteractionType", "Container")])
                       for t in w["containers"]]
og["Discovery"] = [obj(nid(), d["name"], d["x"], d["y"],
                       [("XpSource", d["source"]), ("Radius", round(d["radius"], 2)), ("QuestId", "")])
                   for d in w["discoveries"]]
og["Warp"] = [obj(nid(), p["label"], p["x"], p["y"],
                  [("DestinationMap", p["target"]),
                   ("DestinationSpawn", f'{p["retX"]:.1f},{p["retY"]:.1f}'),
                   ("Label", p["label"]), ("Verb", p["verb"]), ("Radius", round(p["r"], 2))])
              for p in w["portals"]]
og["Interaction"] = [obj(nid(), e["title"], e["x"], e["y"],
                         [("InteractionType", "Examine"), ("Verb", e["verb"]),
                          ("ReadKind", e["kind"]), ("Radius", round(e["r"], 2)),
                          ("Pages", "␟".join(e["pages"]))])
                     for e in w["examinables"]]
og["Props"] = [obj(nid(), p["kind"], p["x"], p["y"],
                   [("Kind", p["kind"]), ("Variant", p["v"]), ("Scale", round(p["s"], 3)),
                    ("Flip", str(p["flip"]).lower()), ("Solid", str(p["solid"]).lower()),
                    ("Radius", round(p["r"], 2))])
               for p in w["props"]]
og["SafeZone"] = [obj(nid(), "SafeZone", z["x0"]*ENGINE_TILE, z["y0"]*ENGINE_TILE, [("Type", "SafeZone")],
                      wpx=(z["x1"]-z["x0"]+1)*ENGINE_TILE, hpx=(z["y1"]-z["y0"]+1)*ENGINE_TILE, point=False)
                  for z in w["safeZones"]]
og["CaveMouth"] = [obj(nid(), "CaveMouth", w["caveMouth"]["x"], w["caveMouth"]["y"],
                       [("InteractionType", "Descend"), ("DestinationMap", "cave")])]
for empty in ("MerchantSpawn", "QuestTrigger", "MusicZone", "CameraZone",
              "ParticleEmitter", "LightingMarker", "SavePoint"):
    og[empty] = []

OBJ_ORDER = ["PlayerSpawn", "NPCSpawn", "EnemySpawn", "MerchantSpawn", "TreasureSpawn",
             "Discovery", "Warp", "Interaction", "Props", "SafeZone", "CaveMouth",
             "QuestTrigger", "MusicZone", "CameraZone", "ParticleEmitter", "LightingMarker", "SavePoint"]

lid, parts = 100, []
for name in LNAMES:
    lid += 1
    parts.append(f''' <layer id="{lid}" name="{name}" width="{MW}" height="{MH}">
  <data encoding="base64" compression="zlib">{encode(layers[name])}</data>
 </layer>
''')
for name in OBJ_ORDER:
    lid += 1
    parts.append(f''' <objectgroup id="{lid}" name="{name}">
{"".join(og.get(name, []))} </objectgroup>
''')

tmx = f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- Stage 1 — "{w["name"]}", painted with Pixel Crawler. See docs/mapping-standard.md.
     16x16 grid; the engine's cell is 32px, so one engine cell is a 2x2 block here
     and the map is {MW}x{MH}. Terrain type is read by layer precedence
     (Cliffs > Water > Bridges > Roads > Ground). Shore and CliffFace are
     decoration only and are never read for collision — the shoreline ring is
     drawn on the LAND cell, so putting it on Water would flood every beach. -->
<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down"
     width="{MW}" height="{MH}" tilewidth="{T}" tileheight="{T}" infinite="0"
     nextlayerid="{lid+1}" nextobjectid="{oid}">
 <properties>
  <property name="Stage" type="int" value="1"/>
  <property name="DisplayName" value="{sx.escape(w["name"])}"/>
  <property name="Music" value="overworld"/>
  <property name="EngineTile" type="int" value="{ENGINE_TILE}"/>
 </properties>
{chr(10).join(tsrefs)}
{"".join(parts)}</map>
'''
path = os.path.join(MAPDIR, "Stage01_Outside.tmx")
open(path, "w").write(tmx)
print(f"wrote {path}  ({MW}x{MH} @{T}px, {os.path.getsize(path)/1024:.0f} KB)")
print("tilesets:", ", ".join(f"{n}(gid {GID[n][0]})" for n in SETS))
print(f"trees stamped: {planted}")
print("objects:", ", ".join(f"{k} {len(v)}" for k, v in og.items() if v))
