#!/usr/bin/env python3
"""Turn the three remaining C#-built places into Tiled maps, painted with Pixel
Crawler.

    node tools/export-interiors.mjs     # capture what the builders produce (once)
    python3 tools/make-places-tmx.py    # write the .tmx files

    Maps/Interiors/MageSchool.tmx        the Academy Outpost
    Maps/Interiors/Seoshe.tmx            the city
    Maps/Interiors/ThievesWarehouse.tmx  the burnt warehouse

Same conventions as everything else (docs/mapping-standard.md): a 16px grid
against the engine's 32px cell, terrain by layer precedence, everything else an
object with custom properties. NPCs and heroes stay on our own atlases.

Two of the three are rooms and read like the Cleric's house. Seoshe is not: it is
a city under open sky, so it is painted like Stage 1 instead — cobbled streets,
dock water with a drawn shoreline, and stone walls with a rim so the blocks read
as buildings rather than one grey slab.

Each prop carries a Painted property saying whether this map drew its art. That is
per prop and not per kind on purpose: a crate is painted into the warehouse floor
here and still drawn from our own atlas out on the Stage 1 road.
"""
from PIL import Image
import json, os, zlib, base64, struct, xml.sax.saxutils as sx

ROOT = "src/AlfarQuest.Client/wwwroot"
MAPS = os.path.join(ROOT, "Maps")
TSDIR = os.path.join(MAPS, "Tilesets")
OUTDIR = os.path.join(MAPS, "Interiors")
PC = os.path.join(MAPS, "Pixel Crawler")
SRC = "tools/refs/cleric-house.json"

T, ENGINE_TILE = 16, 32
SUB = ENGINE_TILE // T
os.makedirs(OUTDIR, exist_ok=True)

SETS = {
    # outdoors — the Stage 1 set, so a street here and a road out there match
    "Floors":     "Environment/Tilesets/Floors_Tiles.png",
    "Water":      "Environment/Tilesets/Water_tiles.png",
    "Walls":      "Environment/Tilesets/Wall_Tiles.png",
    "Rocks":      "Environment/Props/Static/Rocks.png",
    "Vegetation": "Environment/Props/Static/Vegetation.png",
    # indoors — the same two sheets the Cleric's house is built from
    "IntWalls":   "Environment/Structures/Buildings/Interior/Interior_Walls_01.png",
    "IntProps":   "Environment/Structures/Buildings/Interior/Interior_Props_01.png",
    # the furnishings these three places need and the house did not
    "Dungeon":    "Environment/Props/Static/Dungeon_Props.png",
    "Farm":       "Environment/Props/Static/Farm.png",
    "BuildProps": "Environment/Structures/Buildings/Props.png",
    "Esoteric":   "Environment/Props/Static/Esoteric.png",
}

firstgid, GID, tsrefs = 1, {}, []
for name, rel in SETS.items():
    im = Image.open(os.path.join(PC, rel))
    cols, rows = im.width // T, im.height // T
    GID[name] = (firstgid, cols)
    src = os.path.relpath(os.path.join(PC, rel), TSDIR).replace(os.sep, "/")
    path = os.path.join(TSDIR, f"{name}.tsx")
    # Never rewrite a tileset that is already there. Several generators share
    # these files and one of them may have been opened and saved in Tiled;
    # rewriting would throw that away for the sake of a comment.
    if not os.path.exists(path):
        with open(path, "w") as f:
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


def vary(opts, x, y):
    return opts[((x * 73856093) ^ (y * 19349663)) % len(opts)]


# ------------------------------------------------------------- outdoor autotile
# Straight from make-tmx.py, so Seoshe's streets and shoreline are cut the same
# way Stage 1's are. A Floors material is a 5x5 ring around a hole (rows 0-4) plus
# solid fills (rows 10-11); the ring cell to use is the one whose bite faces the
# side that is NOT the same material.
FLOOR_BLOCK = {"grass": 0, "cobble": 5, "dirt": 10}
RING = {
    "S":  [(1, 0), (2, 0), (3, 0)],
    "N":  [(1, 4), (2, 4), (3, 4)],
    "E":  [(0, 1), (0, 2), (0, 3)],
    "W":  [(4, 1), (4, 2), (4, 3)],
    "SE": [(1, 1)], "SW": [(3, 1)], "NE": [(1, 3)], "NW": [(3, 3)],
}
SOLID = [(1, 10), (2, 10), (3, 10)]


def floor_tile(mat, x, y, n, e, s, w):
    bx = FLOOR_BLOCK[mat]
    miss = ("" if n else "N") + ("" if s else "S") + ("" if e else "E") + ("" if w else "W")
    key = miss if miss in RING else None
    if key is None:
        dx, dy = vary(SOLID, x, y)
        return gid("Floors", bx + dx, dy)
    dx, dy = vary(RING[key], x, y)
    return gid("Floors", bx + dx, dy)


WATER_BLOCK = [0, 6]
WSOLID = [(c, r) for r in (6, 7, 8) for c in range(10)]


def water_solid(x, y):
    c, r = vary(WSOLID, x, y)
    return gid("Water", c, r)


def shore_tile(x, y, wn, we, ws, ww):
    """The island ring, placed on the LAND cell so the beach draws itself."""
    bx = WATER_BLOCK[((x + y) // 7) % 2]
    key = ("N" if wn else "") + ("S" if ws else "") + ("E" if we else "") + ("W" if ww else "")
    at = {"N": (2, 0), "S": (2, 4), "E": (4, 2), "W": (0, 2),
          "NW": (1, 1), "NE": (3, 1), "SW": (1, 3), "SE": (3, 3)}.get(key)
    return gid("Water", bx + at[0], at[1]) if at else 0


# Seoshe's walls use the GREY block of Wall_Tiles (cols 6-11) rather than the
# brown one Stage 1's cliffs use: the same rim, quarried stone instead of earth,
# which is what a walled port city should read as from above.
CITY = 6


def wall_top(mx, my, n, e, s_, w_):
    """A wall-top tile picked from the rim that matches which sides fall away.

    Only the OUTLINE of a block comes from this sheet. Seoshe's solid cells are
    buildings, not a hillside, so a block filled edge to edge with quarried rock
    read as a canyon with streets cut through it. The rim is kept — it is what
    makes each block stand up off the street — and the mass behind it is laid in
    coursed masonry instead, which is a wall seen from above."""
    if not n and not w_: return gid("Walls", CITY + 0, 0)
    if not n and not e:  return gid("Walls", CITY + 5, 0)
    if not s_ and not w_: return gid("Walls", CITY + 0, 5)
    if not s_ and not e:  return gid("Walls", CITY + 5, 5)
    if not n:  return gid("Walls", CITY + vary([1, 2, 3, 4], mx, my), 0)
    if not s_: return gid("Walls", CITY + vary([1, 2, 3, 4], mx, my), 5)
    if not w_: return gid("Walls", CITY + 0, vary([1, 2, 3, 4], mx, my))
    if not e:  return gid("Walls", CITY + 5, vary([1, 2, 3, 4], mx, my))
    col, row = vary(ROOM_WALL, mx, my)
    return gid("IntWalls", col, row)


def wall_face(mx, my, row):
    """The courses under a southern edge. Row 6 is the top of the face and row 7
    its continuation; row 8 is the variant footed in grass, which is right under
    open sky and wrong under a roof."""
    return gid("Walls", CITY + vary([1, 2, 3, 4], mx, my), row)


# -------------------------------------------------------------- indoor surfaces
# Rows 20-24 of Interior_Walls are floors: cols 0-4 wood boards, 5-9 grey flags.
# Rows 6-8 of the stone block are the wall face — a solid course, which is what a
# room's wall reads as from above.
WOOD = [(c, r) for r in (21, 22, 23) for c in (0, 1, 2, 3)]
STONE = [(c, r) for r in (21, 22, 23) for c in (5, 6, 7, 8)]
ROOM_WALL = [(c, r) for r in (6, 7, 8) for c in (7, 8, 9, 10)]

# ------------------------------------------------------------------- furniture
# kind -> variants of (tileset, col, row, tilesW, tilesH). The box is stamped
# bottom-centred on the prop's cell, like the trees on Stage 1, and blank source
# tiles are skipped so one piece never erases what is behind it.
#
# Anything absent from here keeps its own sprite: caveEntrance is the doorway
# marker the player aims at, and the statues, crystals and ruins are ours and
# have no equal in the pack.
FURN = {
    "crate":     [("Farm", 18, 0, 1, 2), ("Farm", 19, 0, 1, 2), ("Farm", 10, 0, 1, 2)],
    "barrel":    [("Farm", 16, 0, 1, 1), ("Farm", 17, 0, 1, 1),
                  ("Farm", 16, 1, 1, 1), ("Farm", 17, 1, 1, 1)],
    "banner":    [("Dungeon", 4, 4, 1, 2), ("Dungeon", 5, 4, 1, 2), ("Dungeon", 6, 4, 1, 2)],
    "toolRack":  [("Dungeon", c, 2, 1, 2) for c in range(8)],
    "workbench": [("Dungeon", 1, 5, 3, 1)],
    "ladder":    [("Dungeon", 8, 0, 1, 2)],
    "stall":     [("BuildProps", 5, 10, 4, 2), ("Farm", 20, 0, 3, 2)],
    "support":   [("BuildProps", 0, 8, 1, 3)],
    "scaffold":  [("BuildProps", 1, 11, 4, 5)],
    "chair":     [("IntProps", 4, 1, 1, 2), ("IntProps", 4, 3, 1, 2)],
    "lantern":   [("IntProps", 15, 4, 2, 2), ("IntProps", 13, 4, 2, 2)],   # chandeliers
    "log":       [("Vegetation", 15, 8, 1, 1)],
    "rock":      [("Rocks", c, 2, 1, 1) for c in range(4)],
    "orePile":   [("Rocks", c, 8, 1, 1) for c in range(3)],
    "flowers":   [("Vegetation", 12, 11, 1, 1), ("Vegetation", 13, 11, 1, 1),
                  ("Vegetation", 14, 11, 1, 1)],
}

ALPHA = {}


def alpha_of(setname):
    if setname not in ALPHA:
        im = Image.open(os.path.join(PC, SETS[setname])).convert("RGBA")
        ALPHA[setname] = im.split()[3].load(), im.width // T, im.height // T
    return ALPHA[setname]


LNAMES = ["Ground", "GroundDetails", "Roads", "Water", "Shore", "Walls", "WallFace",
          "Buildings", "Objects", "Furniture", "AbovePlayer", "Shadows"]
OBJ_ORDER = ["PlayerSpawn", "NPCSpawn", "Warp", "Interaction", "Props",
             "TreasureSpawn", "LightingMarker", "MusicZone"]


def encode(d):
    return base64.b64encode(zlib.compress(struct.pack(f"<{len(d)}I", *d), 9)).decode()


def build(key, w, out_name, display, outdoor=False, surface="wood", furn=None):
    COLS, ROWS = w["cols"], w["rows"]
    MW, MH = COLS * SUB, ROWS * SUB
    rows = w["tiles"]
    furn = {**FURN, **(furn or {})}

    def at(x, y):
        if x < 0 or y < 0 or x >= COLS or y >= ROWS: return "#"
        return rows[y][x]

    layers = {n: [0] * (MW * MH) for n in LNAMES}

    def put(layer, mx, my, g):
        if g and 0 <= mx < MW and 0 <= my < MH:
            layers[layer][my * MW + mx] = g

    # Terrain is upsampled to the 16px grid and autotiled THERE. Filling an engine
    # cell's whole 2x2 block with one tile made every edge read as a doubled blob.
    M = [[at(mx // SUB, my // SUB) for my in range(MH)] for mx in range(MW)]

    def m(mx, my):
        if mx < 0 or my < 0 or mx >= MW or my >= MH: return "#"
        return M[mx][my]

    for my in range(MH):
        for mx in range(MW):
            c = m(mx, my)
            if outdoor:
                # Grass under everything, so erasing anything above reveals a
                # finished field rather than a hole.
                dx, dy = vary(SOLID, mx, my)
                put("Ground", mx, my, gid("Floors", FLOOR_BLOCK["grass"] + dx, dy))
                if c == ",":                                    # cobbled street
                    k = lambda xx, yy: m(xx, yy) == ","
                    put("Roads", mx, my, floor_tile("cobble", mx, my,
                        k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))
                elif c == "~":
                    put("Water", mx, my, water_solid(mx, my))
                elif c == "#":
                    n, s_ = m(mx, my - 1) == "#", m(mx, my + 1) == "#"
                    e_, w_ = m(mx + 1, my) == "#", m(mx - 1, my) == "#"
                    put("Walls", mx, my, wall_top(mx, my, n, e_, s_, w_))
                    if not s_:
                        # Two courses below the drop, the lower footed in grass, so
                        # a block of city reads as standing up off the street.
                        put("WallFace", mx, my + 1, wall_face(mx, my, 6))
                        put("WallFace", mx, my + 2, wall_face(mx, my, 8))
            else:
                if c == "#":
                    # Indoors gets the SAME treatment, and for the same reason.
                    # A room walled in flat coursed stone sits at the value of its
                    # own floor and stops reading as a room at all — that was tried
                    # twice for the Cleric's house and reverted twice. The rim is
                    # what draws the line between where you may walk and where you
                    # may not, so it is used under a roof as well.
                    n, s_ = m(mx, my - 1) == "#", m(mx, my + 1) == "#"
                    e_, w_ = m(mx + 1, my) == "#", m(mx - 1, my) == "#"
                    put("Walls", mx, my, wall_top(mx, my, n, e_, s_, w_))
                    if not s_:
                        put("WallFace", mx, my + 1, wall_face(mx, my, 6))
                        put("WallFace", mx, my + 2, wall_face(mx, my, 7))
                elif c == "~":
                    put("Water", mx, my, water_solid(mx, my))
                else:
                    col, row = vary(STONE if surface == "stone" else WOOD, mx, my)
                    put("Ground", mx, my, gid("IntWalls", col, row))

    if outdoor:
        # Beaches last, over the ground already laid — and on their OWN layer.
        # Water is what the engine reads to decide where you may not walk, and
        # this ring is drawn on the LAND cell, so painting it into Water turned
        # the quayside into deep water nobody could stand on.
        for my in range(MH):
            for mx in range(MW):
                if m(mx, my) in "~#": continue
                wn, ws = m(mx, my - 1) == "~", m(mx, my + 1) == "~"
                we, ww = m(mx + 1, my) == "~", m(mx - 1, my) == "~"
                if wn or ws or we or ww:
                    put("Shore", mx, my, shore_tile(mx, my, wn, we, ws, ww))

    painted = set()
    for p in w["props"]:
        opts = furn.get(p["kind"])
        if not opts: continue
        mx, my = int(p["x"]) // T, int(p["y"]) // T
        setname, c0, r0, tw, th = vary(opts, mx, my)
        alpha, scols, srows = alpha_of(setname)
        ox, oy = mx - tw // 2, my - th + 1              # bottom-centred on the cell
        for dy in range(th):
            for dx in range(tw):
                sx_, sy_ = c0 + dx, r0 + dy
                if sx_ >= scols or sy_ >= srows: continue
                if not any(alpha[sx_ * T + px, sy_ * T + py] > 8
                           for py in range(0, T, 2) for px in range(0, T, 2)):
                    continue
                put("Furniture", ox + dx, oy + dy, gid(setname, sx_, sy_))
        painted.add(id(p))

    # ---- objects: everything the engine spawns, unchanged ----
    oid = [1]

    def nid():
        oid[0] += 1; return oid[0] - 1

    def props_xml(pairs):
        if not pairs: return ""
        body = "".join(f'    <property name="{k}" value="{sx.escape(str(v), {chr(34): "&quot;"})}"/>\n'
                       for k, v in pairs)
        return f"   <properties>\n{body}   </properties>\n"

    def obj(name, x, y, pairs):
        return (f'  <object id="{nid()}" name="{sx.escape(name)}" x="{x/2:.2f}" y="{y/2:.2f}">\n'
                f'{props_xml(pairs)}   <point/>\n  </object>\n')

    og = {n: [] for n in OBJ_ORDER}
    og["PlayerSpawn"] = [obj("PlayerSpawn", w["spawn"]["x"], w["spawn"]["y"], [("Type", "PlayerSpawn")])]
    og["NPCSpawn"] = [obj(n["id"], n["x"], n["y"], [("NpcId", n["id"])]) for n in w["npcs"]]
    og["Warp"] = [obj(p["label"], p["x"], p["y"],
                      [("DestinationMap", p["target"]),
                       ("DestinationSpawn", f'{p["retX"]:.1f},{p["retY"]:.1f}'),
                       ("Label", p["label"]), ("Verb", p["verb"]), ("Radius", round(p["r"], 2))])
                  for p in w["portals"]]
    og["Interaction"] = [obj(e["title"], e["x"], e["y"],
                             [("InteractionType", "Examine"), ("Verb", e["verb"]),
                              ("ReadKind", e["kind"]), ("Radius", round(e["r"], 2)),
                              ("Pages", "␟".join(e["pages"]))])
                         for e in w["examinables"]]
    og["Props"] = [obj(p["kind"], p["x"], p["y"],
                       [("Kind", p["kind"]), ("Variant", p["v"]), ("Scale", round(p["s"], 3)),
                        ("Flip", str(p["flip"]).lower()), ("Solid", str(p["solid"]).lower()),
                        ("Radius", round(p["r"], 2)),
                        ("Painted", str(id(p) in painted).lower())])
                   for p in w["props"]]

    lid, parts = 200, []
    for n in LNAMES:
        lid += 1
        parts.append(f''' <layer id="{lid}" name="{n}" width="{MW}" height="{MH}">
  <data encoding="base64" compression="zlib">{encode(layers[n])}</data>
 </layer>
''')
    for n in OBJ_ORDER:
        lid += 1
        parts.append(f''' <objectgroup id="{lid}" name="{n}">
{"".join(og[n])} </objectgroup>
''')

    sky = ("Open sky: this one is a city, so it is painted like Stage 1 — cobbled "
           "streets, dock water with a drawn shoreline, quarried walls with a rim."
           if outdoor else
           f"A roofed place: Walls block, everything else is {surface} floor.")
    tmx = f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- {display}, painted with Pixel Crawler.
     16x16 grid; one engine cell is a 2x2 block, so the map is {MW}x{MH}.
     {sky}
     NPCs and heroes are NOT Pixel Crawler — they stay on our own atlases and
     appear here as NPCSpawn objects. Each prop says whether this map drew it. -->
<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down"
     width="{MW}" height="{MH}" tilewidth="{T}" tileheight="{T}" infinite="0"
     nextlayerid="{lid+1}" nextobjectid="{oid[0]}">
 <properties>
  <property name="Interior" value="{key}"/>
  <property name="DisplayName" value="{sx.escape(display)}"/>
  <property name="Outdoor" type="bool" value="{str(outdoor).lower()}"/>
  <property name="EngineTile" type="int" value="{ENGINE_TILE}"/>
 </properties>
{chr(10).join(tsrefs)}
{"".join(parts)}</map>
'''
    path = os.path.join(OUTDIR, out_name)
    open(path, "w").write(tmx)
    print(f"wrote {path}  ({MW}x{MH}, {os.path.getsize(path)/1024:.0f} KB, "
          f"{len(painted)}/{len(w['props'])} props painted)")


data = json.load(open(SRC))

# The Academy keeps stone underfoot, and its fittings are a school's, not a
# smithy's: the mason's bench becomes an alchemy rack and the weapon racks become
# the shelves of the arcane library the place is examined for.
build("mage_school", data["mage_school"], "MageSchool.tmx", "The Academy Outpost",
      surface="stone", furn={"workbench": [("Esoteric", 5, 1, 2, 3)],
                             "toolRack": [("IntProps", 0, 0, 2, 4), ("IntProps", 2, 0, 2, 4)]})
build("seoshe", data["seoshe"], "Seoshe.tmx", "Seoshe", outdoor=True)
build("thieves_warehouse", data["thieves_warehouse"], "ThievesWarehouse.tmx",
      "The Burnt Warehouse", surface="wood")
