#!/usr/bin/env python3
"""Turn the exported Cleric's-house interiors into Tiled maps, painted with the
Pixel Crawler interior set.

    node tools/export-interiors.mjs     # capture both floors (once)
    python3 tools/make-house-tmx.py     # write the .tmx files

Writes Maps/Interiors/ClericHouse_Ground.tmx and ClericHouse_Upper.tmx, plus the
interior .tsx files. Same conventions as Stage 1 (docs/mapping-standard.md): a
16px grid against the engine's 32px cell, terrain by layer precedence, everything
else an object with custom properties.

Only the WORLD is Pixel Crawler — NPCs and heroes stay on our own atlases, so
Mirka and her father are object-layer spawns here, exactly as before.
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
    "IntWalls": "Environment/Structures/Buildings/Interior/Interior_Walls_01.png",
    "IntProps": "Environment/Structures/Buildings/Interior/Interior_Props_01.png",
    "Furniture": "Environment/Props/Static/Furniture.png",
}

firstgid, GID, tsrefs = 1, {}, []
for name, rel in SETS.items():
    im = Image.open(os.path.join(PC, rel))
    cols, rows = im.width // T, im.height // T
    GID[name] = (firstgid, cols)
    src = os.path.relpath(os.path.join(PC, rel), TSDIR).replace(os.sep, "/")
    with open(os.path.join(TSDIR, f"{name}.tsx"), "w") as f:
        f.write(f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- Pixel Crawler, referenced in place: native 16x16, nothing copied or rescaled. -->
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

# ---- surfaces, from the Interior_Walls sheet -------------------------------
# Rows 20-24 are floors: cols 0-4 wood boards, 5-9 grey flags.
WOOD  = [(c, r) for r in (21, 22, 23) for c in (0, 1, 2, 3)]
STONE = [(c, r) for r in (21, 22, 23) for c in (5, 6, 7, 8)]
# Rows 6-8 of the stone block are the wall face — a solid course, which is what a
# room's wall should read as from above.
WALL  = [(c, r) for r in (6, 7, 8) for c in (7, 8, 9, 10)]

# ---- our house objects -> a Pixel Crawler sprite ---------------------------
# Each entry is (tileset, col, row, tilesW, tilesH) — the box is stamped
# bottom-centred on the object's cell, like the trees on Stage 1.
FURN = {
    "bed":            ("IntProps", 0, 19, 3, 3),
    # bed_mirka is deliberately absent: it is the only piece with a PERSON in
    # it (Mirka asleep, cut from the concept's own plan). The map leaves her
    # cell empty and the sprite still draws, or the heart of the house would
    # be an empty bed.
    "bedside":        ("IntProps", 4, 1, 1, 2),
    "bookshelf":      ("IntProps", 0, 0, 2, 4),
    "cabinet":        ("IntProps", 2, 0, 2, 4),
    "cupboard":       ("IntProps", 2, 0, 2, 4),
    "chest":          ("Furniture", 5, 24, 2, 2),
    "dining_table":   ("IntProps", 17, 0, 3, 3),
    "low_table":      ("IntProps", 0, 6, 3, 3),
    "desk":           ("IntProps", 0, 6, 3, 3),
    "side_table":     ("IntProps", 4, 3, 1, 2),
    "chair":          ("IntProps", 4, 1, 1, 2),
    "chair_b":        ("IntProps", 4, 3, 1, 2),
    "armchair":       ("IntProps", 4, 5, 1, 2),
    "bench":          ("Furniture", 5, 27, 4, 2),
    "fireplace":      ("IntProps", 24, 3, 3, 7),
    "apothecary":     ("IntProps", 11, 14, 4, 2),
    "medicine_stand": ("IntProps", 11, 6, 2, 2),
    "washstand":      ("IntProps", 11, 6, 2, 2),
    "rug":            ("IntProps", 27, 19, 5, 5),
    "rug_stone":      ("IntProps", 21, 21, 6, 3),
    "curtains":       ("IntProps", 5, 11, 2, 3),
    "screen":         ("Furniture", 2, 9, 2, 4),
    "plant":          ("IntProps", 1, 22, 1, 2),
    "flowers":        ("IntProps", 0, 22, 1, 2),
    "flowers_yellow": ("IntProps", 3, 22, 1, 2),
    "herbs":          ("IntProps", 33, 13, 1, 2),
    "candelabra":     ("IntProps", 16, 6, 1, 3),
    "candle":         ("IntProps", 16, 8, 1, 1),
    "sconce":         ("IntProps", 16, 6, 1, 2),
    "font":           ("IntProps", 11, 6, 2, 2),
    "icon":           ("IntProps", 34, 0, 1, 1),
    "frame":          ("IntProps", 36, 0, 1, 1),
    "wedding":        ("IntProps", 5, 20, 1, 1),
    "globe":          ("IntProps", 14, 4, 1, 2),
    "journal":        ("IntProps", 32, 13, 1, 1),
    "records":        ("IntProps", 32, 13, 1, 1),
    "letter":         ("IntProps", 32, 13, 1, 1),
    "book_red":       ("IntProps", 35, 18, 1, 1),
    "book_green":     ("IntProps", 36, 18, 1, 1),
    "bottle":         ("IntProps", 33, 19, 1, 1),
    "panacea":        ("IntProps", 34, 19, 1, 1),
    "chalice":        ("IntProps", 33, 17, 1, 1),
    "cross":          ("IntProps", 35, 0, 1, 1),
}

ALPHA = {}
def alpha_of(setname):
    if setname not in ALPHA:
        im = Image.open(os.path.join(PC, SETS[setname])).convert("RGBA")
        ALPHA[setname] = im.split()[3].load(), im.width // T, im.height // T
    return ALPHA[setname]

LNAMES = ["Ground", "GroundDetails", "Walls", "Buildings", "Objects",
          "Furniture", "AbovePlayer", "Shadows"]
OBJ_ORDER = ["PlayerSpawn", "NPCSpawn", "Warp", "Interaction", "Props",
             "TreasureSpawn", "LightingMarker", "MusicZone"]

def encode(d):
    return base64.b64encode(zlib.compress(struct.pack(f"<{len(d)}I", *d), 9)).decode()

def build(key, w, out_name, display):
    COLS, ROWS = w["cols"], w["rows"]
    MW, MH = COLS * SUB, ROWS * SUB
    rows = w["tiles"]
    def at(x, y):
        if x < 0 or y < 0 or x >= COLS or y >= ROWS: return "#"
        return rows[y][x]

    layers = {n: [0] * (MW * MH) for n in LNAMES}
    def put(layer, mx, my, g):
        if g and 0 <= mx < MW and 0 <= my < MH:
            layers[layer][my * MW + mx] = g

    # Terrain on the upsampled 16px grid, as on Stage 1.
    for my in range(MH):
        for mx in range(MW):
            c = at(mx // SUB, my // SUB)
            if c == "#":
                col, row = vary(WALL, mx, my)
                put("Walls", mx, my, gid("IntWalls", col, row))
            else:
                surface = STONE if c == "," else WOOD      # ',' is the flagged kitchen
                col, row = vary(surface, mx, my)
                put("Ground", mx, my, gid("IntWalls", col, row))

    # Furniture, stamped bottom-centred like the trees outside. Rugs go down on
    # GroundDetails so anything standing on them still draws on top.
    stamped = 0
    for p in w["props"]:
        kind = p["kind"][2:] if p["kind"].startswith("h_") else p["kind"]
        spec = FURN.get(kind)
        if not spec: continue
        setname, c0, r0, tw, th = spec
        alpha, scols, srows = alpha_of(setname)
        mx, my = int(p["x"]) // T, int(p["y"]) // T
        layer = "GroundDetails" if kind.startswith("rug") else "Furniture"
        ox, oy = mx - tw // 2, my - th + 1
        for dy in range(th):
            for dx in range(tw):
                sx_, sy_ = c0 + dx, r0 + dy
                if sx_ >= scols or sy_ >= srows: continue
                if not any(alpha[sx_ * T + px, sy_ * T + py] > 8
                           for py in range(0, T, 2) for px in range(0, T, 2)):
                    continue
                put(layer, ox + dx, oy + dy, gid(setname, sx_, sy_))
        stamped += 1

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
                        ("Radius", round(p["r"], 2))])
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

    tmx = f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- {display}, painted with the Pixel Crawler interior set.
     16x16 grid; one engine cell is a 2x2 block, so the map is {MW}x{MH}.
     Walls are the Walls layer (blocking); the kitchen's flagstones are the same
     Ground layer with a stone surface. NPCs and heroes are NOT Pixel Crawler —
     they stay on our own atlases and appear here as NPCSpawn objects. -->
<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down"
     width="{MW}" height="{MH}" tilewidth="{T}" tileheight="{T}" infinite="0"
     nextlayerid="{lid+1}" nextobjectid="{oid[0]}">
 <properties>
  <property name="Interior" value="{key}"/>
  <property name="DisplayName" value="{sx.escape(display)}"/>
  <property name="EngineTile" type="int" value="{ENGINE_TILE}"/>
 </properties>
{chr(10).join(tsrefs)}
{"".join(parts)}</map>
'''
    path = os.path.join(OUTDIR, out_name)
    open(path, "w").write(tmx)
    print(f"wrote {path}  ({MW}x{MH}, {os.path.getsize(path)/1024:.0f} KB, "
          f"{stamped}/{len(w['props'])} props painted)")

data = json.load(open(SRC))
build("cleric_house", data["cleric_house"], "ClericHouse_Ground.tmx", "The Cleric's House")
build("cleric_house_upper", data["cleric_house_upper"], "ClericHouse_Upper.tmx", "The Cleric's House — Upstairs")
