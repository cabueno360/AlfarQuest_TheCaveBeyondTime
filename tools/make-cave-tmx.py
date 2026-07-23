#!/usr/bin/env python3
"""Turn the exported cave into a Tiled map, painted with the Pixel Crawler
dungeon set.

    node tools/export-cave.mjs      # capture the built cave (once)
    python3 tools/make-cave-tmx.py  # write the .tmx

The cave's layout is hand-authored in C# and identical at every depth — only its
population changes — so ONE map describes every level. Crystals, creatures and
the rewards stay in code: they are the delve, not the place.

Story: the eight regions are written into the map as named rectangles on a
`Region` layer, so "The Crystal Garden" and "The Crystal Heart" are things you can
see and move in Tiled rather than coordinates buried in C#.
"""
from PIL import Image
import json, os, zlib, base64, struct, xml.sax.saxutils as sx

ROOT = "src/AlfarQuest.Client/wwwroot"
MAPS = os.path.join(ROOT, "Maps")
TSDIR = os.path.join(MAPS, "Tilesets")
OUTDIR = os.path.join(MAPS, "Cave")
PC = os.path.join(MAPS, "Pixel Crawler")
SRC = "tools/refs/cave.json"

T, ENGINE_TILE = 16, 32
SUB = ENGINE_TILE // T
os.makedirs(OUTDIR, exist_ok=True)

SETS = {
    "Dungeon":   "Environment/Tilesets/Dungeon_Tiles.png",
    "Walls":     "Environment/Tilesets/Wall_Tiles.png",
    "Water":     "Environment/Tilesets/Water_tiles.png",
    "Floors":    "Environment/Tilesets/Floors_Tiles.png",
    "Rocks":     "Environment/Props/Static/Rocks.png",
    "DungeonProps": "Environment/Props/Static/Dungeon_Props.png",
    "Furniture": "Environment/Props/Static/Furniture.png",
}

firstgid, GID, tsrefs = 1, {}, []
for name, rel in SETS.items():
    im = Image.open(os.path.join(PC, rel))
    cols, rows = im.width // T, im.height // T
    GID[name] = (firstgid, cols)
    src = os.path.relpath(os.path.join(PC, rel), TSDIR).replace(os.sep, "/")
    with open(os.path.join(TSDIR, f"Cave{name}.tsx"), "w") as f:
        f.write(f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- Pixel Crawler, referenced in place: native 16x16, nothing copied or rescaled. -->
<tileset version="1.10" tiledversion="1.10.2" name="Cave{name}" tilewidth="{T}" tileheight="{T}" tilecount="{cols*rows}" columns="{cols}">
 <image source="{sx.escape(src)}" width="{cols*T}" height="{rows*T}"/>
</tileset>
''')
    tsrefs.append(f' <tileset firstgid="{firstgid}" source="../Tilesets/Cave{name}.tsx"/>')
    firstgid += cols * rows

def gid(setname, col, row):
    base, cols = GID[setname]
    return base + row * cols + col

def vary(opts, x, y):
    return opts[((x * 73856093) ^ (y * 19349663)) % len(opts)]

# ---- surfaces ---------------------------------------------------------------
# Dungeon_Tiles cols 4-8 rows 0-2 are the cold stone floor; the grey Wall_Tiles
# block (cols 6-11) is a flat rock face — too flat for a mountain outdoors, which
# is exactly right underground.
FLOOR = [(c, r) for r in (0, 1, 2) for c in (4, 5, 6, 7)]
WATERS = [(c, r) for r in (6, 7, 8) for c in range(10)]
BRIDGE = [(c, r) for r in (10, 11) for c in (6, 7, 8)]        # Floors cobble
CX = 6                                                          # grey wall block

def wall_tile(mx, my, n, e, s_, w_):
    """Rock, picked from the block's rim so a room edge reads as a wall face."""
    if not n and not w_: return gid("Walls", CX + 0, 0)
    if not n and not e:  return gid("Walls", CX + 5, 0)
    if not s_ and not w_: return gid("Walls", CX + 0, 5)
    if not s_ and not e:  return gid("Walls", CX + 5, 5)
    if not n:  return gid("Walls", CX + vary([1, 2, 3, 4], mx, my), 0)
    if not s_: return gid("Walls", CX + vary([1, 2, 3, 4], mx, my), 5)
    if not w_: return gid("Walls", CX + 0, vary([1, 2, 3, 4], mx, my))
    if not e:  return gid("Walls", CX + 5, vary([1, 2, 3, 4], mx, my))
    return gid("Walls", CX + vary([1, 2, 3, 4], mx, my), vary([1, 2, 3, 4], mx, my))

# ---- the cave's own props, by their cell in Miner_Decorations ---------------
# The kits in World.Cave.Dressing name every one of these in a comment; the names
# below are those comments, so a change there is easy to follow here.
# cell -> (tileset, col, row, tilesW, tilesH)
PROPS = {
    (2, 3):  ("DungeonProps", 1, 5, 3, 1),    # slabs
    (8, 4):  ("DungeonProps", 1, 5, 3, 1),    # fallen pillars
    (2, 8):  ("Rocks", 0, 17, 2, 2),          # rune tablets
    (3, 10): ("DungeonProps", 6, 0, 1, 2),    # rune monument
    (6, 4):  ("DungeonProps", 7, 0, 1, 2),    # chained obelisk
    (9, 0):  ("DungeonProps", 6, 0, 1, 2),    # gargoyle well
    (8, 9):  ("DungeonProps", 6, 0, 1, 2),    # basin of orbs
    (6, 10): ("DungeonProps", 6, 0, 1, 2),    # miner statue
    (9, 3):  ("DungeonProps", 0, 3, 1, 2),    # chains
    (5, 0):  ("Dungeon", 6, 15, 1, 3),        # green-flame braziers
    (5, 7):  ("Furniture", 0, 26, 2, 2),      # barrel and pick
    (5, 9):  ("Furniture", 0, 26, 2, 2),      # barrels
    (6, 2):  ("Furniture", 5, 24, 2, 2),      # chests
    (8, 13): ("Rocks", 0, 2, 2, 2),           # boulders
    (8, 3):  ("Rocks", 2, 2, 2, 2),           # spoil heaps
    (9, 13): ("Rocks", 0, 4, 2, 2),           # rock pile and pickaxe
    # The crystal that runs through the whole story gets the pack's glowing gems.
    (3, 8):  ("Dungeon", 3, 19, 3, 3),        # crystal arch      — blue
    (8, 11): ("Dungeon", 3, 19, 3, 3),        # crystal pedestal  — blue
    (9, 12): ("Dungeon", 3, 19, 3, 3),        # teal shrine       — blue
    (9, 11): ("Dungeon", 6, 19, 3, 3),        # green crystal altar
}

# ---- the story: the cave's eight regions, from World.Cave.Regions -----------
REGIONS = [
    ("entrance",  "The Descent",             44, 49,  8, 5),
    ("mine",      "The Abandoned Workings",  16, 45, 11, 7),
    ("tunnels",   "The Cut Tunnels",         15, 28,  8, 6),
    ("lake",      "The Drowned Hollow",      44, 33, 13, 8),
    ("crystal",   "The Crystal Garden",      22, 11, 11, 7),
    ("ruins",     "The Sunken Ruins",        58, 11, 11, 7),
    ("sanctuary", "The Forgotten Shrine",    72, 27,  9, 6),
    ("boss",      "The Crystal Heart",       71, 46, 12, 8),
]
# The depths, named. One map serves them all; the name is the depth's flavour.
DEPTHS = ["The Crystal Cistern", "The Weeping Gallery", "The Sunken Vault",
          "The Mirror Halls", "The Cave Beyond Time"]

ALPHA = {}
def alpha_of(setname):
    if setname not in ALPHA:
        im = Image.open(os.path.join(PC, SETS[setname])).convert("RGBA")
        ALPHA[setname] = im.split()[3].load(), im.width // T, im.height // T
    return ALPHA[setname]

w = json.load(open(SRC))
# The cave inherits the overworld's dimensions at runtime (EnterCave never resets
# COLS/ROWS), so it is captured as 200x128 with everything past the regions sealed
# in rock. Cropped back to the size the cave declares for itself — every region,
# prop, arch and both ends of the delve sit well inside it, so nothing playable is
# lost and the map opens in Tiled as a cave rather than as a mostly-empty field.
CAVE_COLS, CAVE_ROWS = 88, 56
COLS, ROWS = min(w["cols"], CAVE_COLS), min(w["rows"], CAVE_ROWS)
MW, MH = COLS * SUB, ROWS * SUB
rows = w["tiles"]
def at(x, y):
    if x < 0 or y < 0 or x >= COLS or y >= ROWS: return "#"
    return rows[y][x]

LNAMES = ["Ground", "GroundDetails", "Water", "Bridges", "Walls",
          "Objects", "Props", "AbovePlayer", "Shadows"]
layers = {n: [0] * (MW * MH) for n in LNAMES}
def put(layer, mx, my, g):
    if g and 0 <= mx < MW and 0 <= my < MH:
        layers[layer][my * MW + mx] = g

M = [[at(mx // SUB, my // SUB) for my in range(MH)] for mx in range(MW)]
def m(mx, my):
    if mx < 0 or my < 0 or mx >= MW or my >= MH: return "#"
    return M[mx][my]

for my in range(MH):
    for mx in range(MW):
        c = m(mx, my)
        if c == "#":
            n, s_ = m(mx, my-1) == "#", m(mx, my+1) == "#"
            e, w_ = m(mx+1, my) == "#", m(mx-1, my) == "#"
            put("Walls", mx, my, wall_tile(mx, my, n, e, s_, w_))
            continue
        col, row = vary(FLOOR, mx, my)          # carved floor under everything
        put("Ground", mx, my, gid("Dungeon", col, row))
        if c == "~":
            cc, rr = vary(WATERS, mx, my); put("Water", mx, my, gid("Water", cc, rr))
        elif c == "=":
            cc, rr = vary(BRIDGE, mx, my); put("Bridges", mx, my, gid("Floors", cc, rr))

stamped = 0
for p in w["props"]:
    spec = PROPS.get((p.get("cx", -1), p.get("cy", -1)))
    if not spec: continue
    setname, c0, r0, tw, th = spec
    alpha, scols, srows = alpha_of(setname)
    mx, my = int(p["x"]) // T, int(p["y"]) // T
    ox, oy = mx - tw // 2, my - th + 1
    for dy in range(th):
        for dx in range(tw):
            sx_, sy_ = c0 + dx, r0 + dy
            if sx_ >= scols or sy_ >= srows: continue
            if not any(alpha[sx_ * T + px, sy_ * T + py] > 8
                       for py in range(0, T, 2) for px in range(0, T, 2)):
                continue
            put("Props", ox + dx, oy + dy, gid(setname, sx_, sy_))
    stamped += 1

# ---- objects ---------------------------------------------------------------
oid = [1]
def nid():
    oid[0] += 1; return oid[0] - 1
def props_xml(pairs):
    if not pairs: return ""
    body = "".join(f'    <property name="{k}" value="{sx.escape(str(v), {chr(34): "&quot;"})}"/>\n'
                   for k, v in pairs)
    return f"   <properties>\n{body}   </properties>\n"
def point(name, x, y, pairs):
    return (f'  <object id="{nid()}" name="{sx.escape(name)}" x="{x/2:.2f}" y="{y/2:.2f}">\n'
            f'{props_xml(pairs)}   <point/>\n  </object>\n')
def rect(name, tx, ty, tw, th, pairs):
    return (f'  <object id="{nid()}" name="{sx.escape(name)}" '
            f'x="{tx*ENGINE_TILE/2:.2f}" y="{ty*ENGINE_TILE/2:.2f}" '
            f'width="{tw*ENGINE_TILE/2:.2f}" height="{th*ENGINE_TILE/2:.2f}">\n'
            f'{props_xml(pairs)}  </object>\n')

og = {
    "PlayerSpawn": [point("PlayerSpawn", w["spawn"]["x"], w["spawn"]["y"], [("Type", "PlayerSpawn")])],
    "Descent": [point("Descend", w["exit"]["x"], w["exit"]["y"],
                      [("InteractionType", "Descend"), ("DestinationMap", "cave")])],
    "Arch": [point("Arch", a["x"], a["y"], [("Type", "CaveMouth")]) for a in w["arches"]],
    "Discovery": [point(d["name"], d["x"], d["y"],
                        [("XpSource", d["source"]), ("Radius", round(d["radius"], 2))])
                  for d in w["discoveries"]],
    "TreasureSpawn": [point(t["name"], t["x"], t["y"],
                            [("ContainerKind", t["kind"]), ("InteractionType", "Container")])
                      for t in w["containers"]],
    "Props": [point(f'deco {p.get("cx")},{p.get("cy")}', p["x"], p["y"],
                    [("Cx", p.get("cx", 0)), ("Cy", p.get("cy", 0)), ("Scale", round(p["s"], 3)),
                     ("Solid", str(p["solid"]).lower()), ("Radius", round(p["r"], 2)),
                     ("Flip", str(p["flip"]).lower())])
              for p in w["props"]],
    # The story, on the map: each region as a named rectangle you can see and move.
    "Region": [rect(name, cx - rx, cy - ry, rx * 2 + 1, ry * 2 + 1,
                    [("RegionKey", key), ("RegionName", name)])
               for key, name, cx, cy, rx, ry in REGIONS],
}
OBJ_ORDER = ["PlayerSpawn", "Descent", "Arch", "Region", "Discovery", "TreasureSpawn",
             "Props", "EnemySpawn", "LightingMarker", "MusicZone"]

def encode(d):
    return base64.b64encode(zlib.compress(struct.pack(f"<{len(d)}I", *d), 9)).decode()

lid, parts = 300, []
for n in LNAMES:
    lid += 1
    parts.append(f''' <layer id="{lid}" name="{n}" width="{MW}" height="{MH}">
  <data encoding="base64" compression="zlib">{encode(layers[n])}</data>
 </layer>
''')
for n in OBJ_ORDER:
    lid += 1
    parts.append(f''' <objectgroup id="{lid}" name="{n}">
{"".join(og.get(n, []))} </objectgroup>
''')

tmx = f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- The cave — Stage 2, painted with the Pixel Crawler dungeon set.
     ONE map serves every depth: the layout is the same each time and only the
     population changes, so crystals, creatures and rewards stay in code while the
     place itself lives here. The eight regions of the story are on the Region
     layer as named rectangles. Depths, in order: {", ".join(DEPTHS)}. -->
<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down"
     width="{MW}" height="{MH}" tilewidth="{T}" tileheight="{T}" infinite="0"
     nextlayerid="{lid+1}" nextobjectid="{oid[0]}">
 <properties>
  <property name="Stage" type="int" value="2"/>
  <property name="DisplayName" value="{sx.escape(DEPTHS[0])}"/>
  <property name="Depths" value="{sx.escape(' | '.join(DEPTHS))}"/>
  <property name="Music" value="cavern"/>
  <property name="EngineTile" type="int" value="{ENGINE_TILE}"/>
 </properties>
{chr(10).join(tsrefs)}
{"".join(parts)}</map>
'''
path = os.path.join(OUTDIR, "Cave_Descent.tmx")
open(path, "w").write(tmx)
print(f"wrote {path}  ({MW}x{MH}, {os.path.getsize(path)/1024:.0f} KB)")
print(f"props painted {stamped}/{len(w['props'])}, regions {len(REGIONS)}, arches {len(w['arches'])}")
