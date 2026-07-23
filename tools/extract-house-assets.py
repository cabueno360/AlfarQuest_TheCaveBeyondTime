#!/usr/bin/env python3
"""Extract the individual object sprites from the Cleric's House concept art's
"TILES AND ASSETS NEEDED" grid — each icon becomes its own transparent PNG prop,
exactly as Part 4 of the design doc asks.

Only DISCRETE OBJECTS are pulled (furniture, decor, medical, story props). Walls
and floors must tile seamlessly, which painterly swatches cannot, so those stay
as the generated house tiles.

Background removal: the sheet is on parchment. We flood-fill from the crop border
inward, clearing pixels near the local parchment colour — so a sprite's own light
interior (bed sheets, curtains, book pages), being walled off by darker edges, is
never reached and survives.
"""
from PIL import Image
from collections import deque
import os

SRC = "tools/refs/cleric-house-concept.png"
OUT = "tools/refs/extracted"
os.makedirs(OUT, exist_ok=True)
im = Image.open(SRC).convert("RGBA")

# name: (x0, y0, x1, y1) in original-image coords, chosen with a parchment margin.
ITEMS = {
    # --- structures used as props (hearth) + a rug decal ---
    "fireplace":      (810, 880, 940, 968),
    "rug":            (444, 884, 514, 928),
    # --- furniture ---
    "dining_table":   (770, 754, 856, 804),
    "chair":          (768, 815, 814, 874),
    "chair_b":        (816, 815, 860, 874),
    "side_table":     (902, 756, 952, 804),
    "bookshelf":      (860, 806, 942, 888),
    "nightstand":     (862, 888, 920, 930),
    "chest":          (768, 888, 832, 930),
    "coffee_table":   (768, 930, 858, 988),
    "bench":          (858, 930, 952, 988),
    # --- decor & objects ---
    "candelabra":     (963, 750, 1014, 810),
    "wall_candles":   (1015, 750, 1080, 808),
    "icon_holy":      (1082, 748, 1122, 810),
    "frame":          (1122, 748, 1156, 810),
    "picture":        (963, 810, 1014, 874),
    "letter":         (1015, 812, 1062, 860),
    "book_blue":      (963, 874, 1006, 914),
    "candle_jar":     (1015, 872, 1052, 926),
    "font":           (963, 928, 1014, 990),
    "pot_plant":      (1015, 928, 1060, 990),
    "pot_flowers":    (1062, 928, 1106, 990),
    # --- bedroom & medical ---
    "bed":            (1150, 750, 1236, 880),
    "nightstand_med": (1238, 750, 1290, 808),
    "curtains":       (1290, 748, 1334, 814),
    "screen":         (1334, 748, 1380, 814),
    "chair_red":      (1238, 812, 1290, 878),
    "chair_tan":      (1290, 812, 1340, 878),
    "herbs":          (1156, 886, 1200, 930),
    "censer":         (1238, 886, 1282, 930),
    "medicine_tray":  (1288, 878, 1342, 930),
    "basin_table":    (1152, 928, 1204, 990),
    "medicine_table": (1206, 928, 1316, 990),
    "urn":            (1316, 928, 1350, 990),
    # --- special / story ---
    "journal":        (1350, 750, 1422, 808),
    "book_red":       (1424, 750, 1472, 802),
    "book_green":     (1474, 754, 1524, 800),
    "wedding":        (1350, 810, 1450, 884),
    "bottle_empty":   (1452, 810, 1488, 884),
    "panacea":        (1490, 810, 1526, 884),
    "chalice":        (1356, 892, 1428, 990),
    "cross":          (1466, 892, 1518, 990),
}

def bg_color(cell):
    """Most common colour around the crop's border ring — that ring is nearly all
    parchment, so its mode is the parchment colour even when a sprite touches one
    corner (which broke the old median-of-corners estimate)."""
    w, h = cell.size
    px = cell.load()
    hist = {}
    ring = []
    for x in range(w):
        ring += [(x, 0), (x, 1), (x, h-1), (x, h-2)]
    for y in range(h):
        ring += [(0, y), (1, y), (w-1, y), (w-2, y)]
    for x, y in ring:
        r, g, b, a = px[x, y]
        key = (r // 8 * 8, g // 8 * 8, b // 8 * 8)   # quantise to merge texture noise
        hist[key] = hist.get(key, 0) + 1
    return max(hist, key=hist.get)

def dist2(a, b):
    return (a[0]-b[0])**2 + (a[1]-b[1])**2 + (a[2]-b[2])**2

def strip_bg(cell, tol=52):
    """Flood-fill parchment from the border to transparent."""
    w, h = cell.size
    px = cell.load()
    bg = bg_color(cell)
    t2 = tol*tol
    seen = [[False]*w for _ in range(h)]
    q = deque()
    for x in range(w):
        for y in (0, h-1):
            q.append((x, y))
    for y in range(h):
        for x in (0, w-1):
            q.append((x, y))
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y][x]:
            continue
        seen[y][x] = True
        r, g, b, a = px[x, y]
        if dist2((r, g, b), bg) <= t2:
            px[x, y] = (r, g, b, 0)
            q.extend([(x+1, y), (x-1, y), (x, y+1), (x, y-1)])
    return cell

def trim(cell):
    bbox = cell.getbbox()
    return cell.crop(bbox) if bbox else cell

results = {}
for name, box in ITEMS.items():
    cell = im.crop(box)
    cell = strip_bg(cell)
    cell = trim(cell)
    cell.save(os.path.join(OUT, name + ".png"))
    results[name] = cell

# contact sheet: each sprite on a mid-grey checker so transparency shows
COLS = 8
cw, ch = 130, 130
rows = (len(results) + COLS - 1) // COLS
sheet = Image.new("RGBA", (COLS*cw, rows*ch), (60, 60, 68, 255))
from PIL import ImageDraw
d = ImageDraw.Draw(sheet)
for i, (name, spr) in enumerate(results.items()):
    cx, cy = (i % COLS)*cw, (i // COLS)*ch
    # checker
    for yy in range(0, ch, 16):
        for xx in range(0, cw, 16):
            if (xx//16 + yy//16) % 2 == 0:
                d.rectangle([cx+xx, cy+yy, cx+xx+15, cy+yy+15], fill=(78,78,86,255))
    s = spr.copy()
    s.thumbnail((cw-24, ch-30))
    sheet.alpha_composite(s, (cx + (cw-s.width)//2, cy + 4))
    d.text((cx+4, cy+ch-14), name, fill=(255,255,180,255))
sheet.save("/tmp/extracted_contact.png")
print(f"extracted {len(results)} sprites -> {OUT}")
print("contact sheet -> /tmp/extracted_contact.png")
