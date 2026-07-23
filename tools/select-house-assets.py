#!/usr/bin/env python3
"""Pick the wanted sprites from the auto-segmented set by index, give them game
names, and (a) montage them big for a final visual check, (b) copy them to the
game asset folder ready to load."""
from PIL import Image, ImageDraw
import os, shutil

SEG = "tools/refs/seg"
DST = "src/AlfarQuest.Client/wwwroot/assets/Outside/house/obj"
os.makedirs(DST, exist_ok=True)

# game name -> component index, read off /tmp/seg_A.png (0-47) and /tmp/seg_B.png (48-95)
SELECT = {
    # furniture
    "dining_table": 8, "chair": 9, "chair_b": 41, "bench": 43, "bench_long": 92,
    "bookshelf": 24, "cabinet": 44, "cupboard": 64, "chest": 59, "nightstand": 63,
    "low_table": 62, "desk": 93, "side_table": 10, "armchair": 47,
    # beds / bedroom
    "bed": 17, "bedside": 18, "curtains": 19, "screen": 20,
    # decor
    "candelabra": 12, "sconce": 14, "candle": 80, "icon": 15, "frame": 16,
    "font": 68, "globe": 81, "rug": 57, "rug_stone": 78,
    # plants
    "plant": 11, "flowers": 53, "flowers_yellow": 83,
    # medical
    "herbs": 70, "apothecary": 87, "medicine_stand": 72, "washstand": 67,
    # story props
    "journal": 21, "book_red": 22, "book_green": 23, "letter": 26, "records": 77,
    "bottle": 27, "panacea": 28, "wedding": 50, "chalice": 88, "cross": 89,
    # exterior / yard — the small authored details around the cottage. Trees are
    # deliberately NOT taken: the surrounding forest is the RPGW pack and painterly
    # pines beside it would read as two different woods.
    "fence": 30, "cart": 31, "bush": 32, "rock": 33, "flowerbush": 53,
    "wildflowers": 54, "stone": 56, "signpost": 76, "bucket": 95,
}

items = []
for name, idx in SELECT.items():
    p = os.path.join(SEG, f"c{idx:02d}.png")
    if not os.path.exists(p):
        print("MISSING", name, idx); continue
    items.append((name, Image.open(p).convert("RGBA")))

# copy to game assets
for name, im in items:
    im.save(os.path.join(DST, name + ".png"))

# montage at 2x for a visual check
COLS = 7; cell = 190
rows = (len(items)+COLS-1)//COLS
sheet = Image.new("RGBA", (COLS*cell, rows*cell), (52,52,60,255))
d = ImageDraw.Draw(sheet)
for i, (name, im) in enumerate(items):
    gx, gy = (i%COLS)*cell, (i//COLS)*cell
    for yy in range(0, cell, 18):
        for xx in range(0, cell, 18):
            if (xx//18+yy//18) % 2 == 0:
                d.rectangle([gx+xx, gy+yy, gx+xx+17, gy+yy+17], fill=(70,70,78,255))
    t = im.copy(); t = t.resize((t.width*2, t.height*2), Image.NEAREST)
    if t.width > cell-14 or t.height > cell-26:
        t.thumbnail((cell-14, cell-26))
    sheet.alpha_composite(t, (gx+(cell-t.width)//2, gy+4))
    d.text((gx+4, gy+cell-14), name, fill=(255,240,150,255))
sheet.save("/tmp/selected_house.png")
print(f"selected {len(items)} sprites -> {DST}")
print("montage -> /tmp/selected_house.png")
