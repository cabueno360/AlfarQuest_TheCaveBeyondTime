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
    # Furniture carries the plank deck and picket railing a wooden footbridge is
    # dressed with — the same tiles Ashwold's hand-built bridge uses. Registered
    # last so it never shifts the firstgid of a set a committed map already refers
    # to by index.
    "Furniture":    "Environment/Props/Static/Furniture.png",
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

# Our own houses, packed into one atlas by tools/import-buildings.py and addressed
# as a plain 16px grid. Registered here with a stable firstgid so a region can
# stamp a whole house by name (stamp_house) and carry the one tileset ref. It sits
# in Tilesets/Ours/, so its ref path differs from the Pixel Crawler sets above.
_BLD_ATLAS = os.path.join(ROOT, "assets", "atlas_buildings.png")
_BLD_ALPHA = None
if os.path.exists(_BLD_ATLAS):
    _bi = Image.open(_BLD_ATLAS).convert("RGBA")
    _bcols, _brows = _bi.width // T, _bi.height // T
    GID["OurBuildings"] = (_firstgid, _bcols)
    TSREFS.append(f' <tileset firstgid="{_firstgid}" source="../Tilesets/Ours/OurBuildings.tsx"/>')
    _firstgid += _bcols * _brows
    _BLD_ALPHA = _bi.split()[3].load()


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


# --------------------------------------------------------- the wooden footbridge
# Ashwold's bridge is a plank deck with a picket rail, and every crossing is meant
# to read the same way. The deck tile goes on the BRIDGES layer itself — collision
# is read from which layer is painted, never from which tile, so a plank there is
# as walkable as the cobble it replaces, and it renders under the rail instead of
# over it. The rail is a single picket segment dropped on BUILDINGS along whichever
# deck edge looks out over open water — since a bridge now outranks Buildings, the
# rail dresses the edge without ever walling the deck.
DECK_PLANK = [(c, r) for r in (35, 36) for c in (1, 2, 3)]   # Furniture, interior planks
RAIL_PICKET = (2, 31)                                        # Furniture, a picket run


def deck_plank(x, y):
    c, r = vary(DECK_PLANK, x, y)
    return gid("Furniture", c, r)


def bridge_rail():
    return gid("Furniture", RAIL_PICKET[0], RAIL_PICKET[1])


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

# A scatter of small flowers, for the ground between houses. Denser than a
# planted flower BUSH but still an object, so it is placed near dwellings rather
# than sprinkled everywhere.
FLOWER_SPRIGS = [(12, 10), (13, 10), (12, 11), (13, 11), (14, 11)]

# Gravel and pebbles, for bare stone the way tufts are for grass: the small brown
# stones of Rocks.png rows 4-6. Bare rock at this zoom is as flat and telling as
# flat grass, and this is what breaks it up. ORE_ROCKS are the same sheet's
# copper-flecked stones (rows 7-8) — the vein the pit was following, so they go
# by hand near the workings, not scattered.
GRAVEL = [(c, r) for r in (4, 5, 6) for c in range(5)]
ORE_ROCKS = [(6, 7), (7, 7), (8, 7), (9, 7), (6, 8), (7, 8)]


def dress_stone(at, paint, cols, rows, density=0.30):
    """Scatter gravel across bare stone ('q'), the way dress_ground scatters
    tufts across grass. Texture only — nothing solid, nothing the engine reads."""
    laid = 0
    for tx in range(cols):
        for ty in range(rows):
            if at(tx, ty) != "q": continue
            if _h(tx, ty, 41) > density: continue
            mx = tx * SUB + (1 if _h(tx, ty, 42) > 0.5 else 0)
            my = ty * SUB + (1 if _h(tx, ty, 43) > 0.5 else 0)
            col, row = vary(GRAVEL, tx, ty)
            paint("Objects", mx, my, gid("Rocks", col, row))
            laid += 1
    return laid

# CROPS — mature vegetables from Farm.png, one tile each. Each ROW of the sheet
# is a crop and its columns are growth stages; these are the ripe ones. A garden
# bed grows ONE of these, in rows, the way a real bed does — which is the single
# most reference-matching thing that can go on the worked ground.
CROPS = {
    "carrot":      ("Farm", [(5, 1), (6, 1)]),
    "beet":        ("Farm", [(5, 3), (6, 3)]),
    "cabbage":     ("Farm", [(5, 5), (6, 5)]),
    "lettuce":     ("Farm", [(6, 7), (7, 7)]),
    "cauliflower": ("Farm", [(5, 9), (6, 9)]),
    "broccoli":    ("Farm", [(5, 11), (6, 11)]),
}
CROP_KINDS = list(CROPS)


def crop_for(bed_x, bed_y):
    """Which vegetable a bed grows — stable per bed, so a field is patches of
    different crops rather than one uniform green, and re-runs are identical."""
    return CROP_KINDS[((bed_x * 2654435761) ^ (bed_y * 40503)) % len(CROP_KINDS)]


def _h(x, y, salt=0):
    v = (x * 374761393 + y * 668265263 + salt * 2246822519) & 0xFFFFFFFF
    v = (v ^ (v >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((v ^ (v >> 16)) & 0xFFFF) / 65535.0


def dress_ground(at, paint, cols, rows, tuft_density=0.22):
    """The density pass, shared by every region: what turns flat green into a
    place that reads as lived-in. Three things, and the distinction between them
    is the whole point —

      * CROPS on worked ground ('g'). A bed grows one vegetable, in a row, so the
        fields are patches of carrot, cabbage, beet rather than one green wash.
        Objects, and INTENTIONAL — a real bed, not scatter.
      * FLOWER SPRIGS near dwellings ('B'). Objects too, and clustered where
        people would plant them, not sprinkled over the whole map.
      * TUFTS everywhere else. TEXTURE — leaf sprigs and, by water, reed — under
        everything and carrying no collision, denser than before because flat
        grass at this zoom is the loudest tell of a hand-made map.

    Returns (crops, sprigs, tufts) placed, for the builder's summary line."""
    crops = sprigs = tufts = 0
    for tx in range(cols):
        for ty in range(rows):
            c = at(tx, ty)
            mx = tx * SUB + (1 if _h(tx, ty, 22) > 0.5 else 0)
            my = ty * SUB + (1 if _h(tx, ty, 23) > 0.5 else 0)

            if c == "g":
                # A bed grows the crop of the bed it belongs to. Beds are found by
                # snapping to a 4-cell grid so a run of 'g' is one crop, not a
                # different vegetable every tile.
                kind = crop_for(tx // 4, ty // 4)
                setname, opts = CROPS[kind]
                col, row = vary(opts, tx, ty)
                paint("Objects", tx * SUB, ty * SUB, gid(setname, col, row))
                if _h(tx, ty, 31) > 0.5:                 # a second seedling, offset
                    col2, row2 = vary(opts, tx + 1, ty)
                    paint("Objects", tx * SUB + 1, ty * SUB + 1, gid(setname, col2, row2))
                crops += 1
                continue

            if c != ".":
                continue

            # Flower sprigs where a house is near — a dwelling's own ground.
            near_house = any(at(tx + dx, ty + dy) == "B"
                             for dx in range(-2, 3) for dy in range(-2, 3))
            if near_house and _h(tx, ty, 24) < 0.35:
                col, row = vary(FLOWER_SPRIGS, tx, ty)
                paint("Objects", mx, my, gid("Vegetation", col, row))
                sprigs += 1
                continue

            # Texture everywhere else.
            if _h(tx, ty, 21) > tuft_density:
                continue
            near_water = any(at(tx + dx, ty + dy) == "~"
                             for dx in range(-2, 3) for dy in range(-2, 3))
            pool = CLUMPS if (near_water and _h(tx, ty, 21) < 0.09) else TUFTS
            col, row = vary(pool, tx, ty)
            paint("GroundDetails", mx, ty * SUB + 1, gid("Vegetation", col, row))
            tufts += 1
    return crops, sprigs, tufts


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

#  MEASURED, not guessed, and then LOOKED AT — the first version shipped houses
#  the user rightly called open crates. What was wrong:
#
#    * The roof was treated as background and the wall as the mass. In the
#      reference art the ROOF IS THE BUILDING: a big gable fills the shape and
#      only a strip of facade shows beneath it.
#    * The gable has a NOTCH in its bottom middle. That notch is where the front
#      wall belongs, tucked under the ridge. Hanging a full-height wall below the
#      whole roof instead left the notch showing grass — which is exactly what
#      read as "you can see inside it".
#
#  Roofs.png rows 0-5 are whole gables, eight tiles wide: brown at cols 0-7,
#  green glazed tile at 8-15. They CANNOT be stretched — the ridge is one pair of
#  columns — so a longer building is a range of bays, as a real one is.
#
#  Walls.png repeats four materials every 6 columns. Rows 7-10 is the band that
#  sits on the ground (rows 0-5 is a raised frame with legs). Only the bottom
#  courses are used: a house shows a strip of wall, not a storey.
MAT_BASE = {"log": 0, "plank": 6, "board": 12, "plaster": 18}
ROOFS = {"shingle": 0, "tile": 8}

HOUSE_W, ROOF_H = 8, 6
FACADE_ROWS = (9, 10)           # two courses only: a house shows a STRIP of
                                #   wall under its roof, not a storey. Three
                                #   still read as a box with a lid on it.
FACADE_W = 6                    # one tile narrower than the roof at each side,
FACADE_X = 1                    #   so the eaves overhang
FACADE_Y = 5                    # tucked up into the gable's notch
HOUSE_H = FACADE_Y + len(FACADE_ROWS)      # 7 map tiles = 3.5 engine cells


def house_tiles(material, roof, bays=1, storeys=1):
    """One building. Returns (wall_stamps, roof_stamps, (w, h)) in map tiles,
    origin at the top-left, as lists of (dx, dy, tileset, col, row).

    The facade is the SOLID part — the engine reads the Walls layer for collision
    — and it spans the building's width so the whole house blocks. The roof goes
    on a layer that blocks nothing, so its eaves overhang the doorstep.

    `bays` sets whole gables side by side for a longer building; `storeys` adds
    courses of wall under the same roof, which is how the inn stands taller than
    the cottages without being wider."""
    b = MAT_BASE[material]
    rc = ROOFS[roof]
    width = HOUSE_W * bays
    extra = (storeys - 1) * len(FACADE_ROWS)

    walls = []
    for bay in range(bays):
        ox = bay * HOUSE_W
        for s in range(storeys):
            for dy, row in enumerate(FACADE_ROWS):
                y = FACADE_Y + s * len(FACADE_ROWS) + dy
                walls.append((ox + FACADE_X, y, "BuildWalls", b + 0, row))
                for dx in range(1, FACADE_W - 1):
                    walls.append((ox + FACADE_X + dx, y, "BuildWalls",
                                  b + 1 + ((dx + row) % 4), row))
                walls.append((ox + FACADE_X + FACADE_W - 1, y, "BuildWalls", b + 5, row))

    roofs = []
    for bay in range(bays):
        ox = bay * HOUSE_W
        for dy in range(ROOF_H):
            for dx in range(HOUSE_W):
                roofs.append((ox + dx, dy, "BuildRoofs", rc + dx, dy))

    return walls, roofs, (width, HOUSE_H + extra)


# =====================================================================
#  OUR OWN HOUSE SPRITES
#
#  The assembled kit above builds a house out of the pack's facade and roof
#  strips; these are whole houses, drawn by hand (well — generated, cleaned and
#  cut by tools/import-buildings.py) and packed into one atlas. A region stamps
#  one by name with stamp_house, which is the sprite equivalent of house_tiles:
#  the picture on `Buildings`, its ground-floor wall on `Walls` so it blocks, the
#  doorway left walkable.
#
#  HOUSES is read from the catalog import-buildings.py writes, so the sizes here
#  and the pixels there never drift: resize a house at import and it moves in the
#  game with no hand-edit.
# =====================================================================
import json as _json

_BLD_CAT = os.path.join("tools", "refs", "buildings.json")
HOUSES = _json.load(open(_BLD_CAT))["houses"] if os.path.exists(_BLD_CAT) else {}


def house_opaque(col, row):
    """Whether the buildings atlas has anything in tile (col, row) — the empty
    tiles around a house are skipped so they never blank what is under them."""
    if _BLD_ALPHA is None: return False
    return any(_BLD_ALPHA[col * T + px, row * T + py] > 12
               for py in range(0, T, 2) for px in range(0, T, 2))


def stamp_house(paint, name, ex, ey, wall_h=4, anchor="left"):
    """Stamp one of our house sprites, its base on the row below engine cell
    (ex, ey). `anchor` places its LEFT edge at ex (the convention the region
    layouts were drawn to — the doors fall off the lane the same as before) or
    CENTRES it on ex.

    Its opaque tiles go on `Buildings` (drawn, never blocking) EXCEPT the bottom
    `wall_h` courses, which go on `Walls` — drawn AND read as solid, so the
    ground-floor wall is what you cannot walk through while the eaves overhang the
    doorstep. The detected doorway is left off `Walls`, two tiles wide, so the
    threshold stays walkable and a warp there is reachable.

    Returns (w, h) in map tiles, so the builder can keep clear of the footprint."""
    hp = HOUSES[name]
    col, row, w, h, door = hp["col"], hp["row"], hp["w"], hp["h"], hp["door"]
    ox = ex * SUB if anchor == "left" else ex * SUB - w // 2
    oy = (ey + 1) * SUB - h
    for dy in range(h):
        for dx in range(w):
            if not house_opaque(col + dx, row + dy): continue
            g = gid("OurBuildings", col + dx, row + dy)
            solid = dy >= h - wall_h and not (door <= dx <= door + 1)
            paint("Walls" if solid else "Buildings", ox + dx, oy + dy, g)
    return w, h


def house_for(material="log", roof="shingle", name="", bays=1, storeys=1, key=0):
    """Pick one of our five sprites for a building the region already describes,
    keeping its intent: a green-tile roof was the region's shrine or school, a
    two-storey frame its hall or inn. `key` (e.g. a position hash) splits the two
    log houses so a hamlet is not all the same cabin."""
    n = name.lower()
    if any(w in n for w in ("chapel", "shrine", "temple", "church", "bell")):
        return "chapel"        # the teal-roofed, tower-and-porch sprite
    if bays >= 2 or storeys >= 2 or any(w in n for w in
                                        ("hall", "inn", "lodge", "manor", "school", "academy")):
        return "timber_hall"   # the tall two-storey timber frame on posts
    if material in ("plaster", "board"):
        return "cottage"       # the plaster-and-beam cottage
    if material == "log":
        return "log_cabin" if key % 2 else "log_gable"   # two log houses, split by place
    return "cottage"


# ----------------------------------------------------------------- the seams
#  A region's edge is a CONTRACT with its neighbour. Written down when a region
#  is built and read by the next one, so a road that leaves Ashwold at a given
#  column enters the wood at the same column — by construction, not by somebody
#  remembering. A comment saying "the road is at x=35" is a comment; this is the
#  actual row of ground, and if it stops matching the next build says so.
EDGES = os.path.join("tools", "refs", "regions")


def save_edges(region_id, G, cols, rows):
    """Write this region's four borders: the outermost line of ground on each."""
    import json
    os.makedirs(EDGES, exist_ok=True)
    data = {
        "cols": cols, "rows": rows,
        "north": "".join(G[x][0] for x in range(cols)),
        "south": "".join(G[x][rows - 1] for x in range(cols)),
        "west":  "".join(G[0][y] for y in range(rows)),
        "east":  "".join(G[cols - 1][y] for y in range(rows)),
    }
    with open(os.path.join(EDGES, region_id + ".json"), "w") as f:
        json.dump(data, f, indent=1)
    return data


def load_edges(region_id):
    import json
    path = os.path.join(EDGES, region_id + ".json")
    return json.load(open(path)) if os.path.exists(path) else None


def runs(profile, chars):
    """The spans of `chars` along an edge, as (start, end) inclusive. What the
    next region needs to know: where the road is, where the water is."""
    out, i = [], 0
    while i < len(profile):
        if profile[i] in chars:
            j = i
            while j + 1 < len(profile) and profile[j + 1] in chars: j += 1
            out.append((i, j)); i = j + 1
        else:
            i += 1
    return out


# --------------------------------------------------------------------- output
def encode(data):
    return base64.b64encode(zlib.compress(struct.pack(f"<{len(data)}I", *data), 9)).decode()


def props_xml(pairs, indent="   "):
    if not pairs: return ""
    body = "".join(f'{indent} <property name="{k}" value="{sx.escape(str(v), {chr(34): "&quot;"})}"/>\n'
                   for k, v in pairs)
    return f"{indent}<properties>\n{body}{indent}</properties>\n"
