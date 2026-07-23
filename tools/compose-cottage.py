#!/usr/bin/env python3
"""Assemble the Cleric's cottage from the concept art's own EXTERIOR pieces —
Part 1 / Step 7 of the design doc: build the exterior by assembling the modular
pieces rather than cropping the illustration.

Pieces (component indices from tools/segment-house-assets.py):
    7  long stone wall with windows  -> the walls, used once (no seam)
    1  slate roof section            -> the roof, edges trimmed then tiled
    2  arched door                   -> the entrance, centred at the base
    0  stone facade w/ arched window -> a dormer breaking the roofline

The roof piece carries a wooden eave along its bottom, so it is laid in ONE
course only — stacking it would print an eave through the middle of the roof —
and its baked-in left/right edges are trimmed before tiling or every join shows
as a dark seam.

Writes assets/Outside/clericHouse.png, whose door sits at the base centre where
the entrance portal is.
"""
from PIL import Image
import os

SEG = "tools/refs/seg"
OUT = "src/AlfarQuest.Client/wwwroot/assets/Outside/clericHouse.png"

def piece(i):
    """A component, trimmed to its own content — every one carries 2px of
    transparent padding, which would otherwise open a gap between courses."""
    im = Image.open(os.path.join(SEG, f"c{i:02d}.png")).convert("RGBA")
    return im.crop(im.getbbox())

roof   = piece(1)     # slate course, eave along its bottom
door   = piece(2)     # arched door
dormer = piece(0)     # stone facade with an arched window

# The wall component is really TWO fragments that were adjacent on the sheet and
# merged into one blob; the long run is everything left of the empty column at
# x=155. Take that, or the cottage shows a break near its right end.
wall_raw = Image.open(os.path.join(SEG, "c07.png")).convert("RGBA")
wall = wall_raw.crop((2, 2, 155, 66))

TRIM = 4                                            # shave the baked side edges
core = roof.crop((TRIM, 0, roof.width - TRIM, roof.height))

W = wall.width
DORMER_RISE = 22                                    # how far it breaks the roofline
dormer_s = dormer.resize((int(dormer.width * 0.72), int(dormer.height * 0.72)), Image.LANCZOS)

OVERLAP = 6          # tuck the wall under the eave so no daylight shows between
roof_top = DORMER_RISE
wall_top = roof_top + roof.height - OVERLAP
H = wall_top + wall.height

cot = Image.new("RGBA", (W, H), (0, 0, 0, 0))

# --- roof: one course, trimmed tiles laid left to right, last one clipped to fit
x = 0
while x < W:
    tile = core.crop((0, 0, min(core.width, W - x), core.height))
    cot.alpha_composite(tile, (x, roof_top))
    x += core.width

# --- the dormer, breaking the roofline above the door
dx = (W - dormer_s.width) // 2
cot.alpha_composite(dormer_s, (dx, roof_top + roof.height - dormer_s.height - 2))

# --- walls, then the door centred at the base
cot.alpha_composite(wall, (0, wall_top))
cot.alpha_composite(door, ((W - door.width) // 2, H - door.height))

cot.save(OUT)
print("wrote", OUT, cot.size, f"(~{cot.width/32:.1f} x {cot.height/32:.1f} tiles)")
