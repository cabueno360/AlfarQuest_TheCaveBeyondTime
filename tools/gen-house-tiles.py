#!/usr/bin/env python3
"""Generate a modular pixel-art house tileset for the Cleric's house, in the
game's warm-and-cool palette (floors and a reference wall/roof set).

Drawn at a chunky logical scale (each "pixel" is a 2x2 block after upscaling) so
it reads as pixel art rather than smooth painting. Outputs:
  assets/Outside/house/*.png   — the reusable 32px tiles (the modular library)
"""
from PIL import Image, ImageDraw
import os, math

OUT = "src/AlfarQuest.Client/wwwroot/assets/Outside"
HOUSE = os.path.join(OUT, "house")
os.makedirs(HOUSE, exist_ok=True)

S = 2  # upscale: logical pixels -> screen pixels

# ---- palette (kept close to the game's void/gold/stone tones) ----
STONE   = (0x6b, 0x6f, 0x78); STONE_L = (0x8a, 0x8f, 0x99); STONE_D = (0x47, 0x4b, 0x53)
MORTAR  = (0x35, 0x38, 0x3f)
WOOD    = (0x7a, 0x56, 0x36); WOOD_L  = (0x97, 0x6b, 0x41); WOOD_D  = (0x55, 0x3b, 0x24)
WOODLN  = (0x3c, 0x29, 0x18)
SLATE   = (0x46, 0x51, 0x63); SLATE_L = (0x60, 0x6d, 0x82); SLATE_D = (0x30, 0x39, 0x48)
GLASS   = (0x86, 0xb0, 0xcf); GLASS_L = (0xb6, 0xd6, 0xe8); FRAME = (0x3a, 0x28, 0x18)
GOLD    = (0xd8, 0xb4, 0x5a)
OUT_D   = (0x22, 0x1c, 0x18)  # near-black outline

def canvas(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))

def save(img, name):
    big = img.resize((img.width * S, img.height * S), Image.NEAREST)
    big.save(os.path.join(HOUSE, name + ".png"))
    return img

# ---- 16x16 logical tiles (become 32px) ----
def t_stone():
    # Coursed blocks for a house wall. Same lesson as the floors: under lamplight
    # low contrast turns to mud, so the mortar is hard-dark, every block gets a lit
    # top edge, and the tones vary block to block. Courses stagger by half a block.
    im = canvas(16, 16); d = ImageDraw.Draw(im)
    # Kept DARK on purpose. A mid-grey wall sat at the same value as the lit
    # floorboards and the rooms stopped reading as rooms — walls have to be the
    # dark mass the eye reads as "not floor". The catch-light pass then lifts the
    # faces that turn toward the player, which is where the depth comes from.
    MORT = (0x14, 0x15, 0x19); EDGE = (0x5e, 0x62, 0x6b)
    tones = [(0x3a, 0x3d, 0x44), (0x33, 0x36, 0x3c), (0x41, 0x44, 0x4c),
             (0x2e, 0x31, 0x37), (0x37, 0x3a, 0x41), (0x3e, 0x41, 0x49)]
    d.rectangle([0, 0, 15, 15], fill=MORT)
    k = 0
    for row, y in enumerate(range(0, 16, 4)):
        off = 0 if row % 2 == 0 else 4
        for x in range(-off, 16, 8):
            x0, x1 = max(x, 0), min(x + 7, 16)
            if x1 - x0 < 2:
                continue
            d.rectangle([x0, y + 1, x1 - 1, y + 3], fill=tones[k % len(tones)]); k += 1
            d.line([x0, y + 1, x1 - 1, y + 1], fill=EDGE)
    return im

def t_wood():
    im = canvas(16, 16); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 15, 15], fill=WOOD)
    for y in range(0, 16, 4):            # horizontal planks
        d.line([0, y, 15, y], fill=WOODLN)
        d.line([0, y + 1, 15, y + 1], fill=WOOD_L)
        d.point([3, y + 2], fill=WOOD_D); d.point([11, y + 2], fill=WOOD_D)
    return im

def t_window():
    im = t_wood().copy(); d = ImageDraw.Draw(im)
    d.rectangle([2, 2, 13, 13], fill=FRAME)
    d.rectangle([3, 3, 12, 12], fill=GLASS)
    d.line([3, 3, 12, 3], fill=GLASS_L)             # top glint
    d.line([3, 3, 3, 12], fill=GLASS_L)
    d.line([8, 3, 8, 12], fill=FRAME)               # mullions
    d.line([3, 8, 12, 8], fill=FRAME)
    return im

def t_roof():
    im = canvas(16, 16); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 15, 15], fill=SLATE)
    for y in range(0, 16, 4):                        # overlapping slate courses
        d.line([0, y, 15, y], fill=SLATE_D)
        d.line([0, y + 1, 15, y + 1], fill=SLATE_L)
        for x in range(0, 16, 5):
            d.line([x, y, x, y + 3], fill=SLATE_D)
    return im

def t_floor_wood():
    # Long horizontal floorboards. The interior renders in near-dark with warm
    # lamplight pools, which crushes low-contrast detail to a flat brown — so the
    # boards are drawn with a HARD dark seam and a bright lit edge, and each board
    # carries a slightly different tone, so the plank pattern survives the dimming.
    # End-joints are staggered so the 16px tile does not line up into a grid.
    im = canvas(16, 16); d = ImageDraw.Draw(im)
    SEAM = (0x22, 0x15, 0x0a); EDGE = (0xa0, 0x75, 0x48)
    tones = [(0x7e, 0x58, 0x37), (0x6c, 0x4a, 0x2d), (0x86, 0x5e, 0x3a), (0x68, 0x47, 0x2b)]
    for i, y0 in enumerate(range(0, 16, 4)):
        d.rectangle([0, y0, 15, y0 + 3], fill=tones[i])
        d.line([0, y0, 15, y0], fill=SEAM)           # hard shadow seam between boards
        d.line([0, y0 + 1, 15, y0 + 1], fill=EDGE)   # bright lit edge just under it
    for y0, ex in [(0, 11), (4, 5), (8, 13), (12, 7)]:   # staggered board ends
        d.line([ex, y0, ex, y0 + 3], fill=SEAM)
        d.point([ex + 1, y0 + 1], fill=EDGE)
    return im

def t_floor_stone():
    # Flagstones — the kitchen floor. Same readability problem as the boards: the
    # interior is dim, so the joints are hard-dark and every stone gets a lit top
    # edge and its own tone, or the whole floor flattens into grey mud.
    im = canvas(16, 16); d = ImageDraw.Draw(im)
    # Kept dim: at full value the kitchen read as a bright slab dropped into a
    # lamplit house. These sit just above the boards in brightness, not far above.
    JOINT = (0x1c, 0x1e, 0x22); EDGE = (0x78, 0x7e, 0x88)
    tones = [(0x54, 0x58, 0x60), (0x4a, 0x4e, 0x55), (0x5c, 0x61, 0x69), (0x44, 0x48, 0x4f)]
    d.rectangle([0, 0, 15, 15], fill=JOINT)
    k = 0
    for y0 in (0, 8):
        for x0 in (0, 8):
            d.rectangle([x0 + 1, y0 + 1, x0 + 6, y0 + 6], fill=tones[k % 4]); k += 1
            d.line([x0 + 1, y0 + 1, x0 + 6, y0 + 1], fill=EDGE)   # lit top edge
    return im

def t_door():
    im = canvas(16, 16); d = ImageDraw.Draw(im)
    d.rectangle([2, 1, 13, 15], fill=WOOD_D)
    d.rectangle([3, 3, 12, 15], fill=WOOD)
    d.pieslice([3, 1, 12, 9], 180, 360, fill=WOOD)   # arched top
    d.line([8, 3, 8, 15], fill=WOODLN)               # plank seam
    for y in (5, 10):                                # iron bands
        d.line([3, y, 12, y], fill=STONE_D)
    d.ellipse([10, 8, 11, 9], fill=GOLD)             # handle
    return im

tiles = {
    "wall_stone": t_stone(), "wall_wood": t_wood(), "window": t_window(),
    "roof_slate": t_roof(), "floor_wood": t_floor_wood(), "floor_stone": t_floor_stone(),
    "door": t_door(),
}
for name, im in tiles.items():
    save(im, name)

# The exterior cottage is NO LONGER built here — it is assembled from the concept
# art's own pieces by tools/compose-cottage.py, which owns clericHouse.png. This
# script only generates the tiling surfaces (floors) and the modular wall/roof set
# kept for reference; re-running it must not clobber the cottage.
print("tiles written to", HOUSE)
