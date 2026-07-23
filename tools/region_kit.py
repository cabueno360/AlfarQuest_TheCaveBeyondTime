#!/usr/bin/env python3
"""The shared painting kit for hand-authored overworld regions.

Not a generator — a box of tools the region builders use. It holds the three
things every region needs and none of the design:

  * the Pixel Crawler tilesets, registered once with stable firstgids;
  * the autotilers (grass/road/water/cliff), lifted from tools/make-tmx.py so a
    road here and a road on Stage 1 are cut the same way;
  * the ARCHITECTURE KIT — real houses, assembled from the pack's facade and
    roof sheets, because a village without dwellings is not a village and
    scattering rocks where houses should be is the reason the old Stage 1 read
    as procedural.

A house is a roof (Roofs.png, a whole gable seen at three-quarters) over a
facade (Walls.png, which is a kit: corner post, wall run, corner post, four
courses tall). The facade is painted on the `Walls` layer, which the engine
reads as solid — so the wall you can see is the wall you cannot walk through —
and the roof goes on `Buildings`, which blocks nothing, so the eaves overhang
the way eaves do. The doorway is simply the facade tile left unpainted.
"""
from PIL import Image
import os, zlib, base64, struct, xml.sax.saxutils as sx

ROOT = "src/AlfarQuest.Client/wwwroot"
MAPS = os.path.join(ROOT, "Maps")
TSDIR = os.path.join(MAPS, "Tilesets")
PC = os.path.join(MAPS, "Pixel Crawler")

T = 16              # the map's tile
ENGINE_TILE = 32    # the engine's cell
SUB = ENGINE_TILE // T

SETS = {
    "Floors":     "Environment/Tilesets/Floors_Tiles.png",
    "Water":      "Environment/Tilesets/Water_tiles.png",
    "Walls":      "Environment/Tilesets/Wall_Tiles.png",
    "Vegetation": "Environment/Props/Static/Vegetation.png",
    "Rocks":      "Environment/Props/Static/Rocks.png",
    "Farm":       "Environment/Props/Static/Farm.png",
    "BuildProps": "Environment/Structures/Buildings/Props.png",
    "BuildWalls": "Environment/Structures/Buildings/Walls.png",
    "BuildRoofs": "Environment/Structures/Buildings/Roofs.png",
    "TreeOakBig":   "Environment/Props/Static/Trees/Model_01/Size_04.png",
    "TreeOakSmall": "Environment/Props/Static/Trees/Model_01/Size_02.png",
    "TreePineBig":  "Environment/Props/Static/Trees/Model_02/Size_04.png",
    "TreePineMid":  "Environment/Props/Static/Trees/Model_02/Size_03.png",
    "TreePineLow":  "Environment/Props/Static/Trees/Model_02/Size_02.png",
    "TreeTall":     "Environment/Props/Static/Trees/Model_03/Size_03.png",
}

GID, TSREFS = {}, []
_firstgid = 1
for _name, _rel in SETS.items():
    _im = Image.open(os.path.join(PC, _rel))
    _cols, _rows = _im.width // T, _im.height // T
    GID[_name] = (_firstgid, _cols)
    _src = os.path.relpath(os.path.join(PC, _rel), TSDIR).replace(os.sep, "/")
    _path = os.path.join(TSDIR, f"{_name}.tsx")
    if not os.path.exists(_path):        # never clobber a hand-edited tileset
        with open(_path, "w") as _f:
            _f.write(f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- Pixel Crawler, referenced in place: the pack is already a native 16x16 grid,
     so nothing is copied, sliced or rescaled. -->
<tileset version="1.10" tiledversion="1.10.2" name="{_name}" tilewidth="{T}" tileheight="{T}" tilecount="{_cols*_rows}" columns="{_cols}">
 <image source="{sx.escape(_src)}" width="{_cols*T}" height="{_rows*T}"/>
</tileset>
''')
    TSREFS.append(f' <tileset firstgid="{_firstgid}" source="../Tilesets/{_name}.tsx"/>')
    _firstgid += _cols * _rows


def gid(setname, col, row):
    base, cols = GID[setname]
    return base + row * cols + col


def vary(opts, x, y):
    """A stable pick from a list of variants. Position-hashed, never random, so
    the same map generates identically every time and a diff means a change."""
    return opts[((x * 73856093) ^ (y * 19349663)) % len(opts)]


# ----------------------------------------------------------------- autotiling
FLOOR_BLOCK = {"grass": 0, "cobble": 5, "dirt": 10}
RING = {
    "S":  [(1, 0), (2, 0), (3, 0)],
    "N":  [(1, 4), (2, 4), (3, 4)],
    "E":  [(0, 1), (0, 2), (0, 3)],
    "W":  [(4, 1), (4, 2), (4, 3)],
    "SE": [(1, 1)], "SW": [(3, 1)], "NE": [(1, 3)], "NW": [(3, 3)],
}
SOLID = [(1, 10), (2, 10), (3, 10)]


def floor_tile(mat, x, y, n, e, s, w):
    """A Floors tile for a cell of `mat` whose n/e/s/w say which neighbours are
    the same material. A missing side takes the ring tile whose bite faces it."""
    bx = FLOOR_BLOCK[mat]
    miss = ("" if n else "N") + ("" if s else "S") + ("" if e else "E") + ("" if w else "W")
    if miss not in RING:
        dx, dy = vary(SOLID, x, y)
        return gid("Floors", bx + dx, dy)
    dx, dy = vary(RING[miss], x, y)
    return gid("Floors", bx + dx, dy)


WATER_BLOCK = [0, 6]
WSOLID = [(c, r) for r in (6, 7, 8) for c in range(10)]


def water_solid(x, y):
    c, r = vary(WSOLID, x, y)
    return gid("Water", c, r)


def shore_tile(x, y, wn, we, ws, ww):
    """The shoreline, which is an ISLAND ring and therefore belongs on the LAND
    cell. It goes on the `Shore` layer, never on `Water` — Water is what the
    engine reads for "you may not walk here", and putting the beach there floods
    the beach."""
    bx = WATER_BLOCK[((x + y) // 7) % 2]
    key = ("N" if wn else "") + ("S" if ws else "") + ("E" if we else "") + ("W" if ww else "")
    at = {"N": (2, 0), "S": (2, 4), "E": (4, 2), "W": (0, 2),
          "NW": (1, 1), "NE": (3, 1), "SW": (1, 3), "SE": (3, 3)}.get(key)
    return gid("Water", bx + at[0], at[1]) if at else 0


CLIFF = 0        # the brown block of Wall_Tiles: earth and stone, not dungeon


def cliff_top(mx, my, n, e, s_, w_):
    if not n and not w_: return gid("Walls", CLIFF + 0, 0)
    if not n and not e:  return gid("Walls", CLIFF + 5, 0)
    if not s_ and not w_: return gid("Walls", CLIFF + 0, 5)
    if not s_ and not e:  return gid("Walls", CLIFF + 5, 5)
    if not n:  return gid("Walls", CLIFF + vary([1, 2, 3, 4], mx, my), 0)
    if not s_: return gid("Walls", CLIFF + vary([1, 2, 3, 4], mx, my), 5)
    if not w_: return gid("Walls", CLIFF + 0, vary([1, 2, 3, 4], mx, my))
    if not e:  return gid("Walls", CLIFF + 5, vary([1, 2, 3, 4], mx, my))
    return gid("Walls", CLIFF + vary([1, 2, 3, 4], mx, my), vary([1, 2, 3, 4], mx, my))


def cliff_face(mx, my, row):
    return gid("Walls", CLIFF + vary([1, 2, 3, 4], mx, my), row)


# ------------------------------------------------------------------ the trees
# Boxes are pixel rects inside each sheet, found by connected-component detection
# and snapped out to whole tiles. A tree is stamped bottom-centred on its cell.
TREES = {
    "broadleaf": [("TreeOakBig", (3, 2, 73, 126)), ("TreeOakBig", (83, 2, 73, 126)),
                  ("TreeOakBig", (166, 2, 69, 126)), ("TreeOakBig", (246, 2, 69, 126))],
    "cherry":    [("TreeOakSmall", (32, 2, 32, 62)), ("TreeOakSmall", (96, 2, 32, 62))],
    "pine":      [("TreePineMid", (4, 4, 37, 76)), ("TreePineMid", (52, 4, 37, 76)),
                  ("TreePineMid", (99, 6, 40, 74)), ("TreePineBig", (4, 9, 54, 103)),
                  ("TreePineBig", (68, 9, 54, 103)), ("TreeTall", (0, 1, 63, 143))],
    "deadTree":  [("Vegetation", (12 * 16, 4 * 16, 3 * 16, 5 * 16)),
                  ("Vegetation", (13 * 16, 4 * 16, 2 * 16, 5 * 16))],
}

# One-tile undergrowth, by kind.
GROUND_COVER = {
    "bush":    ("Vegetation", [(4, 10), (5, 10), (6, 10), (4, 11), (5, 11), (6, 11)]),
    "flowers": ("Vegetation", [(12, 11), (13, 11), (14, 11), (12, 10), (13, 10)]),
    "log":     ("Vegetation", [(15, 8)]),
    "stump":   ("Vegetation", [(15, 8)]),
    "rock":    ("Rocks",      [(0, 2), (1, 2), (2, 2), (3, 2), (0, 4), (1, 4)]),
    "orePile": ("Rocks",      [(0, 8), (1, 8), (2, 8)]),
}

# The small stuff that stops grass reading as a painted field: leaf sprigs,
# fern clumps, reeds. Kept apart from GROUND_COVER because these are ground
# TEXTURE — they go under everything and carry no collision — while a bush is a
# thing you walk around.
TUFTS = [(1, 9), (2, 9), (3, 9), (1, 10), (2, 10), (3, 10),
         (1, 12), (2, 12), (3, 12), (1, 13), (2, 13), (3, 13)]
CLUMPS = [(7, 11), (7, 14), (8, 11), (9, 11), (10, 11), (11, 11)]

_ALPHA = {}


def alpha_of(setname):
    if setname not in _ALPHA:
        im = Image.open(os.path.join(PC, SETS[setname])).convert("RGBA")
        _ALPHA[setname] = im.split()[3].load(), im.width // T, im.height // T
    return _ALPHA[setname]


# The tree canopies need MORE THAN ONE LAYER.
#
# A tile layer holds one tile per cell, so two trees whose boxes overlap cannot
# both be on it — the second one stamped wins every contested cell and slices the
# first in half. That is where the canopies with no trunk and the trunks with no
# canopy come from, and no amount of thinning the wood fixes it: any wood dense
# enough to look like a wood has overlapping crowns.
#
# So trees are dealt onto several layers, back to front. Sorted by their foot,
# each tree takes the FIRST layer none of its tiles are taken on; a tree that
# conflicts is pushed to a higher layer, which is drawn later and therefore in
# front — which is also exactly where a nearer tree belongs.
# Six, because four still left a third of a dense wood fighting over the last
# one. The count is the depth of overlap the wood actually has, and the last
# layer is the only one where a tree can still be cut.
TREE_LAYERS = ["Trees", "Trees2", "Trees3", "Trees4", "Trees5", "Trees6"]


def tree_box(kind, ex, ey, pick):
    """The tile rectangle a tree occupies, bottom-centred on its cell.
    Returns (tileset, sheet_col, sheet_row, map_x, map_y, w, h)."""
    opts = TREES[kind]
    setname, (bx, by, bw, bh) = opts[pick % len(opts)]
    tx0, ty0 = bx // T, by // T
    tw = -(-(bx + bw) // T) - tx0
    th = -(-(by + bh) // T) - ty0
    mx, my = ex * SUB + 1, ey * SUB + 1
    return setname, tx0, ty0, mx - tw // 2, my - th + 1, tw, th


def plant(trees, paint, pick):
    """Stamp a list of (ex, ey, kind) across the tree layers. `paint` is the
    caller's (layer, mx, my, gid) setter; `pick` chooses a variant per tree.
    Returns how many landed on each layer, which is the honest measure of how
    crowded a wood is."""
    taken = [set() for _ in TREE_LAYERS]
    used = [0] * len(TREE_LAYERS)
    for (ex, ey, kind) in sorted(trees, key=lambda t: (t[1], t[0])):
        if kind not in TREES: continue
        setname, tx0, ty0, ox, oy, tw, th = tree_box(kind, ex, ey, pick(ex, ey))
        cells = [(ox + dx, oy + dy) for dy in range(th) for dx in range(tw)
                 if opaque(setname, tx0 + dx, ty0 + dy)]
        li = next((i for i in range(len(TREE_LAYERS))
                   if not any(c in taken[i] for c in cells)), len(TREE_LAYERS) - 1)
        taken[li].update(cells)
        used[li] += 1
        for dy in range(th):
            for dx in range(tw):
                if not opaque(setname, tx0 + dx, ty0 + dy): continue
                paint(TREE_LAYERS[li], ox + dx, oy + dy, gid(setname, tx0 + dx, ty0 + dy))
    return used


def opaque(setname, col, row):
    """Whether a source tile has anything in it. Skipping the blank ones is what
    stops one tree erasing the canopy of the tree behind it."""
    alpha, cols, rows = alpha_of(setname)
    if col >= cols or row >= rows or col < 0 or row < 0: return False
    return any(alpha[col * T + px, row * T + py] > 8
               for py in range(0, T, 2) for px in range(0, T, 2))


# =====================================================================
#  THE ARCHITECTURE KIT
#
#  Walls.png is a facade kit, not a picture of a house: reading across, the
#  materials are log (cols 0-5), dark plank (6-11), vertical board (12-18) and
#  plaster (19-25); reading down, rows 1-4 are one storey — a lintel course, two
#  wall courses and a sill. Each material is [left post][wall run][right post].
#
#  Roofs.png holds whole gables at three-quarters: a wide shallow one (8x6) in
#  shingle and in green tile, and a narrower steeper one.
#
#  A house is therefore roof + facade, and its width is set by how many wall-run
#  tiles you repeat. Everything below is in MAP tiles (16px).
# =====================================================================
#  MEASURED, not guessed. Walls.png repeats its four materials every 6 columns,
#  in two bands: rows 0-5 is a wall whose corner posts run on past the sill (it
#  reads as a raised timber frame, legs and all — wrong for a cottage), and rows
#  7-10 is a wall that sits on the ground. Houses use the second.
#
#  Roofs.png holds whole gables that CANNOT be stretched: the ridge is a single
#  pair of columns and each slope column carries its own height, so repeating a
#  middle column gives a building two ridges. They are used at their native 8x6.
#  A building wider than one gable is therefore built the way a real one is —
#  as a range of bays sharing a wall.
MAT_BASE = {"log": 0, "plank": 6, "board": 12, "plaster": 18}
FACADE_ROWS = (7, 8, 9, 10)          # lintel, two courses, sill
ROOFS = {"shingle": 0, "tile": 8}    # brown shingle; green glazed tile
ROOF_ROWS = (0, 1, 2, 3, 4, 5)

HOUSE_W, FACADE_H, ROOF_H = 8, 4, 6      # map tiles
#  The roof is dropped onto the wall rather than stacked above it: its skirt
#  overlaps the top three courses so the eaves land ON the wall, which is where
#  eaves are. Stacked flush it floats, with a hand's width of grass showing
#  between roof and house.
ROOF_DROP = 3
HOUSE_H = FACADE_H + ROOF_H - ROOF_DROP  # 7 map tiles; the wall alone is solid


def house_tiles(material, roof, bays=1, storeys=1):
    """One building. Returns (wall_stamps, roof_stamps, (w, h)) in map tiles,
    origin at the top-left, as lists of (dx, dy, tileset, col, row).

    `bays` puts gables side by side for a longer building — an inn range, a
    barn — because the roof art is a fixed gable and a village of identical
    single-gable boxes is exactly the repetition to avoid. `storeys` stacks the
    wall courses, which is how the inn ends up taller than the cottages around
    it without being wider.
    """
    b = MAT_BASE[material]
    width = HOUSE_W * bays
    wall_h = FACADE_H * storeys

    walls = []
    for bay in range(bays):
        ox = bay * HOUSE_W
        for s in range(storeys):
            for dy, row in enumerate(FACADE_ROWS):
                y = s * FACADE_H + dy
                # post, six tiles of wall, post — the facade is one tile
                # narrower than its roof at each side, so the eaves overhang.
                walls.append((ox + 1, y, "BuildWalls", b + 0, row))
                for dx in range(2, HOUSE_W - 2):
                    walls.append((ox + dx, y, "BuildWalls", b + 1 + ((dx + row) % 4), row))
                walls.append((ox + HOUSE_W - 2, y, "BuildWalls", b + 5, row))

    rc = ROOFS[roof]
    roofs = []
    for bay in range(bays):
        ox = bay * HOUSE_W
        for dy, row in enumerate(ROOF_ROWS):
            for dx in range(HOUSE_W):
                # Row 5 columns 3-4 are not this roof: they are the tip of the
                # steeper gable packed underneath it on the sheet, and stamped
                # they put a small pale tent on the ridge of every house.
                if row == 5 and dx in (3, 4): continue
                roofs.append((ox + dx, dy - ROOF_DROP, "BuildRoofs", rc + dx, row))

    return walls, roofs, (width, wall_h + ROOF_H - ROOF_DROP)


# --------------------------------------------------------------------- output
def encode(data):
    return base64.b64encode(zlib.compress(struct.pack(f"<{len(data)}I", *data), 9)).decode()


def props_xml(pairs, indent="   "):
    if not pairs: return ""
    body = "".join(f'{indent} <property name="{k}" value="{sx.escape(str(v), {chr(34): "&quot;"})}"/>\n'
                   for k, v in pairs)
    return f"{indent}<properties>\n{body}{indent}</properties>\n"
