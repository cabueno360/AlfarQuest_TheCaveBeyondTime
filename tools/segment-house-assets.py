#!/usr/bin/env python3
"""Auto-segment the concept's asset grid into individual sprites — no hand-boxing.
Flood-fill the parchment from the band border (protecting sprites' light interiors),
dilate the remaining foreground so a sprite's detached parts stay one blob, label
the connected components, drop header text / divider lines, and dump each component
as a transparent PNG plus an indexed contact sheet to choose names from.
"""
from PIL import Image, ImageDraw
from collections import deque
import os

SRC = "tools/refs/cleric-house-concept.png"
OUT = "tools/refs/seg"
os.makedirs(OUT, exist_ok=True)

BAND_Y0, BAND_Y1 = 748, 1014
im = Image.open(SRC).convert("RGBA")
band = im.crop((0, BAND_Y0, im.width, BAND_Y1))
W, H = band.size
px = band.load()

def lum(p):  return (p[0]*299 + p[1]*587 + p[2]*114) // 1000
def sat(p):  mx, mn = max(p[:3]), min(p[:3]); return 0 if mx == 0 else (mx-mn)*100//mx

# Parchment reference = mode of the border ring.
hist = {}
for x in range(W):
    for y in (0, 1, H-2, H-1):
        p = px[x, y]; k = (p[0]//8*8, p[1]//8*8, p[2]//8*8); hist[k] = hist.get(k, 0)+1
PARCH = max(hist, key=hist.get)
def near_parch(p, tol=56):
    return (p[0]-PARCH[0])**2 + (p[1]-PARCH[1])**2 + (p[2]-PARCH[2])**2 <= tol*tol

# bg = looks like parchment (near colour) OR low-saturation & bright (paper).
is_bg = [[False]*W for _ in range(H)]
for y in range(H):
    for x in range(W):
        p = px[x, y]
        is_bg[y][x] = near_parch(p) or (sat(p) < 26 and lum(p) > 170)

# Clear only parchment CONNECTED to the border, so interior whites (sheets, pages,
# curtains) survive. fg = everything not cleared.
fg = [[True]*W for _ in range(H)]
q = deque()
for x in range(W):
    for y in (0, H-1):
        q.append((x, y))
for y in range(H):
    for x in (0, W-1):
        q.append((x, y))
while q:
    x, y = q.popleft()
    if x < 0 or y < 0 or x >= W or y >= H or not fg[y][x] or not is_bg[y][x]:
        continue
    fg[y][x] = False
    q.extend([(x+1, y), (x-1, y), (x, y+1), (x, y-1)])

# Dilate fg by 2px so a sprite's near-touching parts label as one blob.
R = 2
dil = [[False]*W for _ in range(H)]
for y in range(H):
    for x in range(W):
        if not fg[y][x]:
            continue
        for dy in range(-R, R+1):
            yy = y+dy
            if 0 <= yy < H:
                row = dil[yy]
                for dx in range(-R, R+1):
                    xx = x+dx
                    if 0 <= xx < W:
                        row[xx] = True

# Label connected components on the dilated mask.
comp = [[0]*W for _ in range(H)]
comps = []
cid = 0
for y0 in range(H):
    for x0 in range(W):
        if not dil[y0][x0] or comp[y0][x0]:
            continue
        cid += 1
        minx = maxx = x0; miny = maxy = y0; area = 0
        st = deque([(x0, y0)])
        comp[y0][x0] = cid
        while st:
            x, y = st.pop()
            area += 1
            if x < minx: minx = x
            if x > maxx: maxx = x
            if y < miny: miny = y
            if y > maxy: maxy = y
            for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
                xx, yy = x+dx, y+dy
                if 0 <= xx < W and 0 <= yy < H and dil[yy][xx] and not comp[yy][xx]:
                    comp[yy][xx] = cid; st.append((xx, yy))
        w, h = maxx-minx+1, maxy-miny+1
        comps.append({"id": cid, "box": (minx, miny, maxx, maxy), "w": w, "h": h, "area": area})

# Filter: drop tiny noise, thin divider lines, and wide-short header text near the top.
kept = []
for c in comps:
    w, h, a = c["w"], c["h"], c["area"]
    if a < 600 or w < 8 or h < 8:            continue
    if w > 55 and h < 20:                    continue   # header word
    if c["box"][1] < 18 and h < 24:          continue   # top-row text
    if a < w*h*0.10:                         continue   # sparse (stray text run)
    kept.append(c)
kept.sort(key=lambda c: (c["box"][1]//40, c["box"][0]))   # reading order

# Save each kept component (foreground pixels only) + build a contact sheet.
def crop_component(c):
    minx, miny, maxx, maxy = c["box"]
    out = Image.new("RGBA", (maxx-minx+1, maxy-miny+1), (0,0,0,0))
    op = out.load()
    for y in range(miny, maxy+1):
        for x in range(minx, maxx+1):
            if fg[y][x]:                       # original (non-dilated) pixels only
                op[x-minx, y-miny] = px[x, y]
    return out

sprites = []
for i, c in enumerate(kept):
    s = crop_component(c)
    s.save(os.path.join(OUT, f"c{i:02d}.png"))
    sprites.append((i, c, s))

COLS = 8; cw = ch = 128
rows = (len(sprites)+COLS-1)//COLS
sheet = Image.new("RGBA", (COLS*cw, rows*ch), (54,54,62,255))
d = ImageDraw.Draw(sheet)
for i, c, s in sprites:
    gx, gy = (i%COLS)*cw, (i//COLS)*ch
    for yy in range(0, ch, 16):
        for xx in range(0, cw, 16):
            if (xx//16+yy//16) % 2 == 0:
                d.rectangle([gx+xx, gy+yy, gx+xx+15, gy+yy+15], fill=(72,72,80,255))
    t = s.copy(); t.thumbnail((cw-20, ch-26))
    sheet.alpha_composite(t, (gx+(cw-t.width)//2, gy+2))
    bx = c["box"]
    d.text((gx+3, gy+ch-13), f"{i}:{bx[0]},{bx[1]+BAND_Y0} {c['w']}x{c['h']}", fill=(255,240,150,255))
sheet.save("/tmp/seg_contact.png")
print(f"parchment={PARCH}  components kept={len(kept)}")
print("contact -> /tmp/seg_contact.png,  sprites -> tools/refs/seg/")
