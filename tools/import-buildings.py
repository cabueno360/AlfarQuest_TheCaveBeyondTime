#!/usr/bin/env python3
"""Cut complete house sprites out of screenshots and pack them into a buildings
atlas the maps can stamp.

    python3 tools/import-buildings.py

The five houses came as SCREENSHOTS — each house sitting on grass, with the dirt
path and the odd mushroom and even a stray hero in frame. So each is extracted
the way the hero sheets are: flood the grass-and-dirt background in from the
border, keep the single largest island (the house — the mushrooms, the tree line
and the hero are all smaller, separate islands and fall away), and trim.

Then each is downscaled to the game's scale — a cottage is four or five engine
cells, not the eighteen the screenshot's zoom would give — snapped to a whole
16px grid so it stamps cleanly, and packed into one atlas. atlas_buildings.png
plus OurBuildings.tsx, a grid tileset the maps and Tiled both read.
"""
from PIL import Image
from collections import deque
import os

ASSETS = "src/AlfarQuest.Client/wwwroot/assets"
TSDIR = "src/AlfarQuest.Client/wwwroot/Maps/Tilesets/Ours"
DESK = os.path.expanduser("~/Desktop")
T = 16

# The five screenshots, newest batch, with the name and target WIDTH in engine
# cells each should stand at in game. Height follows from the aspect.
# A whole-catalogue size dial, on top of the per-house cell widths below. The
# houses first went in sized to the old assembled kit (4 cells); at 1.25 they
# stand a quarter taller, to read in scale with the full-size heroes and
# villagers rather than under them.
HOUSE_SCALE = 1.25

# Target WIDTH in engine cells. Sized to the old assembled houses (4 cells) so a
# sprite drops into a terrace laid out for one without crowding its neighbour — a
# touch bigger for the hall and the chapel, which are meant to stand out.
SOURCES = [
    ("Screenshot 2026-07-24 at 15.42.32.png", "log_gable", 4),   # log house, gabled, balcony
    ("Screenshot 2026-07-24 at 15.42.42.png", "timber_hall", 5), # two-storey timber frame on posts
    ("Screenshot 2026-07-24 at 15.43.03.png", "cottage", 4),     # timber-frame cottage
    ("Screenshot 2026-07-24 at 15.43.23.png", "log_cabin", 4),   # log cabin with dormer
    ("Screenshot 2026-07-24 at 15.43.36.png", "chapel", 6),      # teal-roofed chapel, L-shaped
]


def isbg(p):
    r, g, b = p[0], p[1], p[2]
    grass = g > r + 8 and g > b + 8 and 40 < g < 190
    dirt = r > 85 and g > 55 and b < g - 8 and r >= g - 10 and b < 120 and r < 190
    return grass or dirt


def extract(path):
    """The house alone: grass and dirt flooded from the border, then the largest
    remaining island kept and cropped."""
    im = Image.open(path).convert("RGBA")
    W, H = im.size
    px = im.load()
    bg = bytearray(W * H)
    q = deque()
    for x in range(W):
        for y in (0, H - 1):
            if isbg(px[x, y]) and not bg[y * W + x]:
                bg[y * W + x] = 1; q.append((x, y))
    for y in range(H):
        for x in (0, W - 1):
            if isbg(px[x, y]) and not bg[y * W + x]:
                bg[y * W + x] = 1; q.append((x, y))
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < W and 0 <= ny < H and not bg[ny * W + nx] and isbg(px[nx, ny]):
                bg[ny * W + nx] = 1; q.append((nx, ny))

    seen = bytearray(W * H); best = None; bestn = 0
    for y in range(H):
        for x in range(W):
            if bg[y * W + x] or seen[y * W + x]: continue
            Q = deque([(x, y)]); seen[y * W + x] = 1; cells = []
            while Q:
                cx, cy = Q.popleft(); cells.append((cx, cy))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < W and 0 <= ny < H and not bg[ny * W + nx] and not seen[ny * W + nx]:
                        seen[ny * W + nx] = 1; Q.append((nx, ny))
            if len(cells) > bestn: bestn = len(cells); best = cells

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    op = out.load()
    for (x, y) in best:
        op[x, y] = px[x, y]
    drop_floaters(out)
    trim_spikes(out)
    thin_top(out)
    return out.crop(out.getbbox())


def thin_top(im):
    """Peel the thin masts that stand in a row by themselves — a pair of finials
    the spike-trim spares because each is the other's tall neighbour. From the top
    down, a row with only a few filled px is nothing but mast, so clear it; stop at
    the first row wide enough to be roof. Confined to the top eighth, so a gable
    coming to a point loses at most its very tip."""
    W, H = im.size
    px = im.load()

    def filled(x, y):
        return px[x, y][3] > 12

    limit = H // 8
    for y in range(min(limit, H)):
        if sum(1 for x in range(W) if filled(x, y)) > 9:
            break
        for x in range(W):
            px[x, y] = (0, 0, 0, 0)


def trim_spikes(im):
    """Shave the thin masts — a stray fence-post the crop kept, a chapel finial —
    that jut straight up out of the silhouette. A spike is a column that stands
    more than a few px ABOVE its neighbours on BOTH sides; a chimney, being a wide
    block, always has a same-height neighbour on one side and so is never a spike.
    Each spike is cut down to the taller of its two neighbouring surfaces."""
    W, H = im.size
    px = im.load()
    top = [next((y for y in range(H) if px[x, y][3] > 12), H) for x in range(W)]
    cuts = []
    for x in range(W):
        if top[x] >= H: continue
        left = min((top[k] for k in range(max(0, x - 5), x) if top[k] < H), default=H)
        right = min((top[k] for k in range(x + 1, min(W, x + 6)) if top[k] < H), default=H)
        if top[x] < left - 5 and top[x] < right - 5:
            cuts.append((x, min(left, right)))     # clear down to the taller side
    for x, base in cuts:
        for y in range(0, min(base, H)):
            px[x, y] = (0, 0, 0, 0)


def drop_floaters(im):
    """Clear the stray bits that ride above the roofline — a tree line the crop
    caught, a thin flag-pole. Per column, the house is the mass connected UP from
    the bottom: filled pixels, crossing only small gaps in the texture. The
    chimney is part of it, because it sits ON the roof with no gap. A tree line or
    a pole floats with clear air beneath it, so it is never reached from below and
    is wiped."""
    W, H = im.size
    px = im.load()

    def filled(x, y):
        return px[x, y][3] > 12

    for x in range(W):
        # find the bottom-most filled pixel, then walk up keeping the mass,
        # tolerating gaps of up to 3px (texture), stopping at real air.
        bottom = next((y for y in range(H - 1, -1, -1) if filled(x, y)), None)
        if bottom is None: continue
        top = bottom
        gap = 0
        y = bottom - 1
        while y >= 0:
            if filled(x, y):
                top = y; gap = 0
            else:
                gap += 1
                if gap > 3: break
            y -= 1
        for yy in range(0, top):
            px[x, yy] = (0, 0, 0, 0)


def snap16(im):
    """Pad to a whole number of 16px tiles, bottom-anchored (the house's feet sit
    on the grid), centred horizontally."""
    w = -(-im.width // T) * T
    h = -(-im.height // T) * T
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    canvas.alpha_composite(im, ((w - im.width) // 2, h - im.height))
    return canvas


def door_col(tile):
    """Where the doorway is, as the LEFT local tile-column of a two-tile gap the
    house's collision leaves walkable. The door is the dark opening at the base, so
    the darkest two-tile window along the bottom courses is the door. Left open so
    a warp on the threshold is reachable and the eye's doorway is the game's."""
    W, H = tile.size
    px = tile.load()
    tw = W // T
    lo = max(0, H - 5 * T)                            # the bottom ~5 tiles: the wall

    def dark(col):                                   # mean luma of opaque px, or +inf
        vals = [(px[x, y][0] + px[x, y][1] + px[x, y][2]) // 3
                for x in range(col * T, min((col + 1) * T, W))
                for y in range(lo, H) if px[x, y][3] > 40]
        return sum(vals) / len(vals) if vals else 1e9

    d = [dark(c) for c in range(tw)]
    return min(range(max(1, tw - 1)), key=lambda c: d[c] + d[c + 1])


houses = []
for fn, name, cells in SOURCES:
    path = os.path.join(DESK, fn)
    if not os.path.exists(path):
        print("missing:", fn); continue
    raw = extract(path)
    target_w = round(cells * (T * 2) * HOUSE_SCALE)  # engine cell = 32px = 2 map tiles
    scale = target_w / raw.width
    small = raw.resize((target_w, max(1, round(raw.height * scale))), Image.LANCZOS)
    tile = snap16(small)
    houses.append((name, tile))
    print(f"{name:12} screenshot {raw.size} -> {tile.size}  "
          f"({tile.width // T}x{tile.height // T} tiles = {tile.width // 32}x{tile.height // 32} cells)")

# Pack side by side into one atlas, each on a 16px grid, columns aligned.
gap = T
atlas_w = sum(h[1].width + gap for h in houses) + gap
atlas_h = -(-max(h[1].height for h in houses) // T) * T + gap
atlas = Image.new("RGBA", (atlas_w, atlas_h), (0, 0, 0, 0))
placed = []                                          # (name, col, row, tw, th) in tiles
doors = {}                                           # name -> local door tile-col
x = gap
for name, tile in houses:
    atlas.alpha_composite(tile, (x, gap))
    placed.append((name, x // T, gap // T, tile.width // T, tile.height // T))
    doors[name] = door_col(tile)
    x += tile.width + gap

os.makedirs(ASSETS, exist_ok=True)
atlas.save(os.path.join(ASSETS, "atlas_buildings.png"))

cols = atlas.width // T
tsx_tiles = "".join(
    f'  <!-- {name}: tiles ({c},{r}) {tw}x{th} -->\n' for (name, c, r, tw, th) in placed)
with open(os.path.join(TSDIR, "OurBuildings.tsx"), "w") as f:
    f.write(f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- Our complete house sprites, extracted from screenshots and packed on a 16px
     grid. Stamp a house by its tile rectangle; the map builders read the same
     rects from region_kit.HOUSES. -->
<tileset version="1.10" tiledversion="1.10.2" name="OurBuildings" tilewidth="{T}" tileheight="{T}" tilecount="{cols * (atlas.height // T)}" columns="{cols}">
 <image source="../../../assets/atlas_buildings.png" width="{atlas.width}" height="{atlas.height}"/>
{tsx_tiles}</tileset>
''')

# The catalog the map builders read, so region_kit.HOUSES and this stay one
# source of truth — a house resized here moves in the game without a hand-edit.
import json
catalog = {
    "atlas": "atlas_buildings.png",
    "atlas_w": atlas.width, "atlas_h": atlas.height, "cols": cols,
    "houses": {name: {"col": c, "row": r, "w": tw, "h": th, "door": doors[name]}
               for (name, c, r, tw, th) in placed},
}
os.makedirs("tools/refs", exist_ok=True)
with open("tools/refs/buildings.json", "w") as f:
    json.dump(catalog, f, indent=1)

print(f"\nwrote {os.path.join(ASSETS, 'atlas_buildings.png')}  {atlas.size}")
print(f"wrote {os.path.join(TSDIR, 'OurBuildings.tsx')}")
print("wrote tools/refs/buildings.json")
print("\nHOUSES = {")
for (name, c, r, tw, th) in placed:
    print(f'    "{name}": ({c}, {r}, {tw}, {th}),  door@{doors[name]}')
print("}")
