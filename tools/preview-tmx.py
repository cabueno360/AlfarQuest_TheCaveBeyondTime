#!/usr/bin/env python3
"""Render a .tmx to a PNG, so the map can be judged without running the game.

    python3 tools/preview-tmx.py                 # whole map, downscaled
    python3 tools/preview-tmx.py 60 100 40 26    # 1:1 crop at tile x y w h

Reads the tile layers in order and composites them with the real tileset images —
the same thing Tiled shows.
"""
from PIL import Image
import base64, zlib, struct, os, sys, xml.etree.ElementTree as ET

MAPS = "src/AlfarQuest.Client/wwwroot/Maps"
import sys as _s
TMX = os.environ.get("TMX") or os.path.join(MAPS, "Outside/Stage01_Outside.tmx")
TSDIR = os.path.join(MAPS, "Tilesets")

root = ET.parse(TMX).getroot()
MW, MH = int(root.get("width")), int(root.get("height"))
T = int(root.get("tilewidth"))

# tilesets: firstgid -> (image, columns). Paths are resolved relative to the file
# that gave them — the .tsx relative to the .tmx, its image relative to the .tsx —
# so a tileset in Tilesets/Ours/ resolves the same as one in Tilesets/.
sets = []
for ts in root.findall("tileset"):
    first = int(ts.get("firstgid"))
    tsx_path = os.path.normpath(os.path.join(os.path.dirname(TMX), ts.get("source")))
    tsx = ET.parse(tsx_path).getroot()
    img_rel = tsx.find("image").get("source")
    img = Image.open(os.path.normpath(os.path.join(os.path.dirname(tsx_path), img_rel))).convert("RGBA")
    sets.append((first, img, int(tsx.get("columns"))))
sets.sort(key=lambda s: s[0])

def tile(gid):
    for first, img, cols in reversed(sets):
        if gid >= first:
            i = gid - first
            c, r = i % cols, i // cols
            return img.crop((c * T, r * T, c * T + T, r * T + T))
    return None

def layer_data(el):
    d = el.find("data")
    raw = zlib.decompress(base64.b64decode(d.text.strip()))
    n = MW * MH
    return struct.unpack(f"<{n}I", raw[:n * 4])

if len(sys.argv) == 5:
    X0, Y0, W, H = (int(v) for v in sys.argv[1:5])
    scale, out = 1, "/tmp/tmx_crop.png"
else:
    X0, Y0, W, H = 0, 0, MW, MH
    scale, out = 0.25, "/tmp/tmx_full.png"

canvas = Image.new("RGBA", (W * T, H * T), (26, 30, 24, 255))
for el in root.findall("layer"):
    name = el.get("name")
    if name == "AbovePlayer":      # drawn over actors; still fine in a flat preview
        pass
    data = layer_data(el)
    for y in range(Y0, min(Y0 + H, MH)):
        row = y * MW
        for x in range(X0, min(X0 + W, MW)):
            g = data[row + x] & 0x1FFFFFFF
            if not g: continue
            t = tile(g)
            if t: canvas.alpha_composite(t, ((x - X0) * T, (y - Y0) * T))

if scale != 1:
    canvas = canvas.resize((int(canvas.width * scale), int(canvas.height * scale)), Image.LANCZOS)
canvas.convert("RGB").save(out)
print(f"wrote {out}  {canvas.width}x{canvas.height}")
