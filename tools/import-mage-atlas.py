#!/usr/bin/env python3
"""Cut the Mage's own sprite sheet into the strip the engine animates.

    python3 tools/import-mage-atlas.py [source.png]

The source is an 8x8 grid of 32x32 cells — SIXTY-FOUR VARIATIONS of the same
wizard, not an animation. Almost every cell faces the camera; there is no walk
cycle in it, no side view, and no four directions. So it cannot be dropped in as
a character sheet: something has to choose which of the sixty-four are the idle,
which are the swing and which is the channel, and that choice is here rather
than buried in the renderer.

What the engine wants, from ATLAS.party's contract, is one row of nine cells:

    0 1 2 3 4 5   the idle-and-walk loop
    6 7           the attack, played forward across the swing
    8             the ability channel, which outranks the attack

Chosen from the grid as follows. Row 2 is the calm rank — arms in, robe closed,
the narrowest silhouettes on the sheet — so the loop comes from there, taking the
six cells that differ most from each other so the shuffle reads as movement
rather than as a still frame. The swing comes from row 3, which is the same man
with both arms thrown wide. The channel is row 4: hooded, arms raised, the only
poses on the sheet that look like something is being called down.

The whole 8x8 sheet is ALSO kept and registered as a tileset, so the other
fifty-five poses are addressable in Tiled like every other piece of art in the
project — see tools/import-ours.py.
"""
from PIL import Image
import os, shutil, sys

SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser("~/Downloads/The Mage Atlas.png")
ASSETS = "src/AlfarQuest.Client/wwwroot/assets"
SHEET = os.path.join(ASSETS, "atlas_mage_sheet.png")   # the grid, kept whole
STRIP = os.path.join(ASSETS, "atlas_mage.png")         # what the engine draws
CELL = 32

#  (col, row) into the source grid, in the engine's column order.
#  Change these to re-cast the Mage; nothing else needs to move.
FRAMES = [
    # 0-5  the idle and walk loop — row 2, arms in, taken across the row so
    #      consecutive frames are not near-identical
    (0, 2), (3, 2), (1, 2), (5, 2), (2, 2), (6, 2),
    # 6-7  the attack — row 3, both arms thrown wide
    (0, 3), (2, 3),
    # 8    the ability channel — row 4, hooded, arms raised
    (4, 4),
]

im = Image.open(SRC).convert("RGBA")
if im.size != (256, 256):
    raise SystemExit(f"expected a 256x256 grid of 32x32 cells, got {im.size}")

os.makedirs(ASSETS, exist_ok=True)
shutil.copyfile(SRC, SHEET)

strip = Image.new("RGBA", (CELL * len(FRAMES), CELL), (0, 0, 0, 0))
for i, (c, r) in enumerate(FRAMES):
    strip.paste(im.crop((c * CELL, r * CELL, c * CELL + CELL, r * CELL + CELL)),
                (i * CELL, 0))
strip.save(STRIP)

# The foot line, measured rather than assumed: the renderer stands a hero on it,
# and a sprite whose feet are not where the atlas says they are floats or sinks.
px = strip.load()
foot = max((y for y in range(CELL) for x in range(strip.width) if px[x, y][3] > 8),
           default=CELL - 1)
print(f"wrote {SHEET}   (the whole 8x8 grid, for Tiled)")
print(f"wrote {STRIP}   {strip.width}x{strip.height} — 9 cells of {CELL}x{CELL}")
print(f"  frames: {FRAMES}")
print(f"  ground line at y={foot} — set ATLAS.mage.ground to this")
