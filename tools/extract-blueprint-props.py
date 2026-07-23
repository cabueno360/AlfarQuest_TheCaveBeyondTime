#!/usr/bin/env python3
"""Cut props out of the concept art's BLUEPRINT panels — things drawn only in the
floor plans, not in the tileset grid.

Mirka in her sickbed (second floor):

The design doc is explicit that she stays in the bed shown, and the engine draws
NPCs standing upright — so she is not an NPC sprite at all here. The bed with her
in it becomes one house-object prop, and the Mirka NPC standing behind it is
marked Bedridden so the renderer skips her sprite while she stays talkable.

The blueprint bed sits on a patterned rug, which no colour key can separate, so
the alpha is a rounded rectangle matching the bed's own silhouette — tight enough
that only the corners (rug and floorboards) are cut away.
"""
from PIL import Image, ImageDraw
import os

SRC = "tools/refs/cleric-house-concept.png"
DST = "src/AlfarQuest.Client/wwwroot/assets/Outside/house/obj/bed_mirka.png"

OBJ = "src/AlfarQuest.Client/wwwroot/assets/Outside/house/obj"
src = Image.open(SRC).convert("RGBA")


def cut(box, name, radius=0):
    """Crop a blueprint region and round its corners. The plans sit on patterned
    rugs and flagstones that no colour key can separate, so the alpha is the
    prop's own silhouette — cropped tight enough that only corners are cut."""
    im = src.crop(box)
    w, h = im.size
    if radius:
        mask = Image.new("L", (w, h), 0)
        ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, h - 1], radius=radius, fill=255)
        im.putalpha(mask)
    os.makedirs(OBJ, exist_ok=True)
    im.save(os.path.join(OBJ, name + ".png"))
    print(f"wrote {name}.png {im.size} (~{w/32:.1f} x {h/32:.1f} tiles)")


# Mirka asleep in her bed — MIRKA'S ROOM, second-floor plan.
cut((1017, 80, 1082, 197), "bed_mirka", radius=8)
# The kitchen hearth — a stone chimney breast with the fire lit, ground-floor plan.
# Used for both this house's fireplaces, in the kitchen and the living room.
cut((617, 67, 676, 149), "fireplace", radius=3)
