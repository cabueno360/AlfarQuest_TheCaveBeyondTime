#!/usr/bin/env python3
"""Import OUR OWN art as Tiled tilesets, alongside the Pixel Crawler library.

    python3 tools/import-ours.py

Writes into Maps/Tilesets/Ours/. Two kinds, because our art comes in two kinds:

  * GRID sheets — the heroes and the husk (atlas_party), the cave tileset, the
    ore and decoration sheets. These are regular grids, so the .tsx references
    the PNG in place and nothing is copied.

  * PACKED atlases — atlas_chars (villagers and creatures) and atlas_outside
    (scenery). These are packed tight with a JSON frame table: 84 and 197 frames
    at 78 and 188 DIFFERENT sizes, on no grid at all. No grid tileset can address
    them, so each frame is cut to its own PNG and collected into an image
    tileset. The pixels are untouched — it is the same art, made addressable.

House objects are already one PNG each, so those are collected in place too.
"""
from PIL import Image
import json, os, xml.sax.saxutils as sx

ROOT = "src/AlfarQuest.Client/wwwroot"
ASSETS = os.path.join(ROOT, "assets")
OUT = os.path.join(ROOT, "Maps/Tilesets/Ours")
SPRITES = os.path.join(OUT, "sprites")
os.makedirs(SPRITES, exist_ok=True)

index = []

def rel_to_out(path):
    return os.path.relpath(path, OUT).replace(os.sep, "/")

# ---------------------------------------------------------------- grid sheets
# name -> (path under assets, tile width, tile height, what it is)
GRIDS = {
    "OurHeroes":       ("atlas_party.png", 51, 63,
                        "the three heroes and the husk — rows Mage/Cleric/Thief/husk, "
                        "columns idle+walk 0-5, attack 6-7, ability 8"),
    "OurCaveTiles":    ("Caves/MainLev2.0.png", 32, 32, "the cave's rock and floor"),
    "OurCaveDeco":     ("Caves/decorative.png", 32, 32, "cave decoration"),
    "OurOres":         ("Miner_Ores.png", 128, 128, "crystal spires and ore"),
    "OurDecorations":  ("Miner_Decorations.png", 128, 128, "the cave's dressing kits"),
    "OurOutsideTiles": ("Outside/outside_tiles.png", 32, 32,
                        "the Stage 1 terrain sheet. NOTE: the engine samples this at "
                        "arbitrary pixel offsets (hand-picked seamless windows), so a "
                        "32px grid will not line up with what the game draws"),
}

for name, (rel, tw, th, what) in GRIDS.items():
    path = os.path.join(ASSETS, rel)
    if not os.path.exists(path):
        print("missing:", rel); continue
    im = Image.open(path)
    cols, rows = im.width // tw, im.height // th
    with open(os.path.join(OUT, name + ".tsx"), "w") as f:
        f.write(f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- {sx.escape(what)}
     Our own art, referenced in place — nothing copied or rescaled. -->
<tileset version="1.10" tiledversion="1.10.2" name="{name}" tilewidth="{tw}" tileheight="{th}" tilecount="{cols*rows}" columns="{cols}">
 <image source="{sx.escape(rel_to_out(path))}" width="{im.width}" height="{im.height}"/>
</tileset>
''')
    index.append((name + ".tsx", rel, f"{im.width}x{im.height}", f"{tw}x{th}", cols * rows, "grid, in place"))

# ------------------------------------------------------------ packed atlases
def collection(name, tsx_tiles, tw, th, note):
    body = "".join(tsx_tiles)
    with open(os.path.join(OUT, name + ".tsx"), "w") as f:
        f.write(f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- {sx.escape(note)} -->
<tileset version="1.10" tiledversion="1.10.2" name="{name}" tilewidth="{tw}" tileheight="{th}" tilecount="{len(tsx_tiles)}" columns="0">
 <grid orientation="orthogonal" width="1" height="1"/>
{body}</tileset>
''')

PACKED = {
    "OurNpcs":  ("Outside/atlas_chars.png", "Outside/atlas_chars.json",
                 "Our villagers and creatures. The atlas is packed with a JSON frame "
                 "table at many different sizes, so each frame is cut to its own PNG "
                 "and collected here. Same pixels, made addressable."),
    "OurProps": ("Outside/atlas_outside.png", "Outside/atlas_outside.json",
                 "Our Stage 1 scenery, cut from the packed atlas the same way."),
}

for name, (png, js, note) in PACKED.items():
    sheet = Image.open(os.path.join(ASSETS, png)).convert("RGBA")
    frames = json.load(open(os.path.join(ASSETS, js)))
    tiles, tid, mw, mh = [], 0, 0, 0
    for key, lst in frames.items():
        for i, fr in enumerate(lst):
            sub = sheet.crop((fr["x"], fr["y"], fr["x"] + fr["w"], fr["y"] + fr["h"]))
            fname = f"{name}_{key}_{i}.png".replace(" ", "_").replace("'", "")
            sub.save(os.path.join(SPRITES, fname))
            mw, mh = max(mw, fr["w"]), max(mh, fr["h"])
            tiles.append(
                f'  <tile id="{tid}">\n'
                f'   <properties><property name="Name" value="{sx.escape(key)}"/>'
                f'<property name="Frame" type="int" value="{i}"/></properties>\n'
                f'   <image source="sprites/{sx.escape(fname)}" width="{fr["w"]}" height="{fr["h"]}"/>\n'
                f'  </tile>\n')
            tid += 1
    collection(name, tiles, mw, mh, note)
    index.append((name + ".tsx", png, f"{sheet.width}x{sheet.height}", "collection",
                  len(tiles), f"{len(frames)} names, cut to sprites/"))

# --------------------------------------------- house objects (already single)
HOUSE = os.path.join(ASSETS, "Outside/house/obj")
if os.path.isdir(HOUSE):
    tiles, tid, mw, mh = [], 0, 0, 0
    for fn in sorted(os.listdir(HOUSE)):
        if not fn.endswith(".png"): continue
        im = Image.open(os.path.join(HOUSE, fn))
        mw, mh = max(mw, im.width), max(mh, im.height)
        tiles.append(
            f'  <tile id="{tid}">\n'
            f'   <properties><property name="Name" value="{sx.escape(fn[:-4])}"/></properties>\n'
            f'   <image source="{sx.escape(rel_to_out(os.path.join(HOUSE, fn)))}" '
            f'width="{im.width}" height="{im.height}"/>\n  </tile>\n')
        tid += 1
    collection("OurHouseObjects", tiles, mw, mh,
               "The Cleric's-house furniture and story props, cut from the concept art. "
               "Already one PNG each, so referenced in place.")
    index.append(("OurHouseObjects.tsx", "assets/Outside/house/obj/", "-", "collection",
                  len(tiles), "in place"))

with open(os.path.join(OUT, "INDEX.md"), "w") as f:
    f.write("# Our own tileset library\n\nGenerated by `tools/import-ours.py`. "
            "Open any of these in Tiled with **Add External Tileset**.\n\n"
            "| tileset | source | image | tile | tiles | note |\n|---|---|---|---|---|---|\n")
    for row in index:
        f.write("| `" + row[0] + "` | " + " | ".join(str(c) for c in row[1:]) + " |\n")

print(f"wrote {len(index)} tilesets to {OUT}")
for row in index:
    print(f"   {row[0]:24} {row[4]:>5} tiles   {row[5]}")
