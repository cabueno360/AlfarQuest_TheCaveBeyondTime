#!/usr/bin/env python3
"""REGION 2 — THE WHISPERING WOOD, between the village and the mine.

    python3 tools/build-r2-whispering-wood.py  ->  Maps/Regions/R2_WhisperingWood.tmx

WHY IT IS HERE
    Ashwold sits in the fork of two waters; the mine is up in the rock. Between
    them is the wood, and the wood is the point: it is the last easy walking
    before the ground turns against you. The road climbs through it, crosses the
    river at the Grey Ford, and runs east along the foot of the mountains toward
    the pass. Two kinds of people live off it — the colliers, who burn the
    charcoal Ashwold's forge eats, and the hamlet in the vale across the water,
    which is where the Cleric's house is.

HOW IT IS BUILT
    The south edge is not a design decision. It is a CONTRACT with Ashwold, read
    from tools/refs/regions/r1_ashwold.json: the road leaves that map at columns
    35-36, the river at 20-23, and the foothills fill 46-71. Those three land
    here at exactly the same columns, which is why walking over the seam costs
    the player nothing.

THE SHAPE OF IT
    Mountains wall the north; the foothills come up out of Ashwold's corner and
    fill the south-east, so the road has one way through and it is the gap
    between them. The river is born at the north wall and runs down the west of
    the map, so the road must cross it — and where a road crosses water people
    stop, which is why the ford is the busiest thing in the region and why the
    wayside cross is on the bank.

WHAT THE WOOD IS DOING
    Density is the story. It is thin along the road, thick away from it, and
    thickest in the north-west where nobody has cut for a generation. Three
    clearings are the only breaks: the vale, the colliers' burn, and the hollow
    in the south-west that nothing marks and nothing points to.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import region_kit as K
import xml.sax.saxutils as sx

OUT = os.path.join(K.MAPS, "Regions")
os.makedirs(OUT, exist_ok=True)

COLS, ROWS = 72, 56
MW, MH = COLS * K.SUB, ROWS * K.SUB

REGION_ID = "r2_whispering_wood"
DISPLAY = "The Whispering Wood"
SOUTH_NEIGHBOUR = "r1_ashwold"

#   '.' grass  ',' road  't' track  'c' cobble  '~' water  '=' bridge/ford
#   '#' rock   'g' worked ground    'B' building
G = [["." for _ in range(ROWS)] for _ in range(COLS)]


def put(x, y, ch):
    if 0 <= x < COLS and 0 <= y < ROWS: G[x][y] = ch


def at(x, y):
    if x < 0 or y < 0 or x >= COLS or y >= ROWS: return "#"
    return G[x][y]


def rect(x0, y0, x1, y1, ch):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            put(x, y, ch)


def path(points, width, ch, over=None):
    for i in range(len(points) - 1):
        (x0, y0), (x1, y1) = points[i], points[i + 1]
        steps = max(abs(x1 - x0), abs(y1 - y0)) * 3 + 1
        for s in range(steps + 1):
            t = s / steps
            cx, cy = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for dx in range(-(width // 2), width - width // 2):
                for dy in range(-(width // 2), width - width // 2):
                    x, y = int(round(cx)) + dx, int(round(cy)) + dy
                    if over is not None and at(x, y) not in over: continue
                    put(x, y, ch)


# =====================================================================
#  0. THE CONTRACT WITH ASHWOLD
# =====================================================================
_south = K.load_edges(SOUTH_NEIGHBOUR)
if _south is None:
    raise SystemExit("build r1 first: this region's south edge is read from its north edge")
SEAM = _south["north"]
ROAD_IN = K.runs(SEAM, ",t=")[0]        # (35, 36)
WATER_IN = K.runs(SEAM, "~")[0]         # (20, 23)
ROCK_IN = K.runs(SEAM, "#")[0]          # (46, 71)
print(f"seam with {SOUTH_NEIGHBOUR}: road {ROAD_IN}  water {WATER_IN}  rock {ROCK_IN}")

# path() lays an even width as offsets -(w//2) .. (w - w//2 - 1), so a width-2
# line centred at c covers [c-1, c] and a width-4 line covers [c-2, c+1]. The
# centre is therefore NOT the middle of the run, and taking it as the middle put
# both the road and the river one column left of where Ashwold leaves them —
# which the seam check caught, which is what the seam check is for.
ROAD_X = ROAD_IN[1]                 # width 2  -> [ROAD_IN[0], ROAD_IN[1]]
RIVER_X = WATER_IN[0] + 2           # width 4  -> [WATER_IN[0], WATER_IN[1]]

# =====================================================================
#  1. THE LAND
# =====================================================================

# The mountain wall along the north. Not a straight band: it comes down in
# spurs, so the road below it has a shape to follow and the skyline has corners.
for x in range(COLS):
    h = 6 + int(2.5 * (1 + __import__("math").sin(x * 0.31)) )
    if 30 <= x <= 44: h -= 2          # the shoulder the river comes out of
    for y in range(0, h):
        put(x, y, "#")

# The foothills, come up out of Ashwold's north-east corner. Their southern
# limit IS the seam: rock at columns 46-71 on the bottom row, exactly as the
# village map leaves it, thinning as it climbs north-east.
for x in range(ROCK_IN[0], COLS):
    top = int(55 - 1.4 * (x - ROCK_IN[0]))
    for y in range(max(0, top), ROWS):
        put(x, y, "#")

# The river. Born at the shoulder in the north wall, down the west of the map,
# out through the seam at exactly the columns Ashwold expects it.
RIVER = [(38, 4), (36, 12), (33, 20), (29, 28), (25, 38), (RIVER_X, 48), (RIVER_X, 58)]
path(RIVER, 4, "~")

# =====================================================================
#  2. THE ROAD
#
#  One road, because there is one way through: up out of the village, over the
#  ford, and east along the foot of the mountains toward the pass. Everything
#  else in the region is a spur off it, which is what makes leaving it a choice.
# =====================================================================
path([(ROAD_X, 58), (ROAD_X, 46), (36, 38), (35, 30), (35, 22), (38, 16),
      (46, 14), (56, 13), (66, 13), (74, 13)], 2, ",", over=".#")

def crossing(reach=2):
    """Turn water into deck wherever a way already runs over it. Boxing the ford
    by hand paved ten cells of river and swallowed the road with it; taking the
    shape from the road means the crossing is as wide as the road and bends with
    it."""
    laid = []
    for x in range(COLS):
        for y in range(ROWS):
            if at(x, y) != "~": continue
            if any(at(x + dx, y + dy) in ",t"
                   for dx in range(-reach, reach + 1) for dy in (-1, 0, 1)):
                laid.append((x, y))
    for x, y in laid: put(x, y, "=")
    return len(laid)


# The Grey Ford, and the vale's plank footbridge, are cut once every way has been
# laid — see the crossing() call below the tracks.

# The spur down to the vale: west off the road, over the water on a plank
# footbridge, into the hamlet. Narrow, because only the hamlet uses it.
path([(35, 34), (31, 33), (26, 32), (22, 31)], 2, "t", over=".#")

# The colliers' track: east off the road to the burn, kept clear so a cart can
# reach the kilns.
path([(37, 40), (43, 41), (49, 42)], 2, "t", over=".#")

_deck = crossing()

# =====================================================================
#  3. THE CLEARINGS
#
#  A wood with no holes in it is a wall. Three, and each is a hole somebody made
#  for a reason.
# =====================================================================
VALE = (12, 24, 27, 38)          # the chapel hamlet, across the water
BURN = (42, 34, 56, 48)          # the colliers' ground
HOLLOW = (6, 42, 16, 52)         # the one nobody made

BUILDINGS = []


def building(ex, ey, material, roof, name, bays=1, storeys=1):
    BUILDINGS.append((ex, ey, material, roof, bays, storeys, name))
    for x in range(ex, ex + 3 * bays):
        for y in range(ey - 1, ey + 1):
            put(x, y, "B")


# ---- the hamlet in the vale. Six roofs and a bell, on ground that floods, which
#      is why it never grew and why the village up the road did.
rect(12, 26, 28, 39, ".")                       # the vale floor, clear of wood
# One lane along the vale floor with the doors off it, which is what a hamlet on
# a terrace is; the spur comes in at its east end.
path([(22, 31), (21, 34), (17, 35), (14, 37)], 2, "t", over=".")
path([(21, 34), (21, 30), (24, 30)], 1, "t", over=".")
path([(19, 34), (19, 33)], 1, "t", over=".")

building(19, 32, "plaster", "tile", "the Cleric's house", bays=2)   # the door out of this map
building(14, 29, "log", "shingle", "a vale cottage")
building(24, 29, "log", "shingle", "Goodwife Marrow's")
building(24, 37, "board", "shingle", "the byre")
building(14, 36, "plaster", "tile", "the chapel")

# ---- the colliers' burn. Cleared ground, blackened, with the kilns on it.
rect(*BURN, ch=".")
for _x in range(44, 55):
    for _y in range(38, 45):
        if at(_x, _y) == "." and (_x + _y) % 3: put(_x, _y, "g")   # trodden, burnt
building(50, 39, "board", "shingle", "the colliers' hut")

# ---- the hollow. No clearing was made here; the trees simply do not grow on it,
#      and there is a reason for that under the moss.
rect(*HOLLOW, ch=".")

# =====================================================================
#  4. THE PROPS
# =====================================================================
PROPS = []


def prop(ex, ey, kind, scale=1.0, solid=False, radius=0.0):
    PROPS.append((ex, ey, kind, scale, solid, radius))


# ---- the seam itself: a marker stone where the village's ground ends, so
#      crossing back and forth has a landmark on both sides.
prop(ROAD_X + 2, 53, "signpost", 0.75, True, 10)          # ASHWOLD ↓ / THE FORD ↑
prop(ROAD_X - 2, 52, "rock", 0.8, True, 13)

# ---- the Grey Ford: the crossing, and everything a crossing collects.
prop(30, 17, "support", 0.7, True, 12); prop(41, 17, "support", 0.7, True, 12)
prop(30, 19, "support", 0.7, True, 12); prop(41, 19, "support", 0.7, True, 12)
prop(29, 15, "rock", 0.95, True, 15); prop(42, 20, "rock", 0.95, True, 15)
prop(43, 16, "signpost", 0.7, True, 10)
prop(29, 21, "lantern", 0.6)

# ---- the wayside cross on the near bank. The last mark of the Light before the
#      rock, and it is covered in offerings because everybody leaves one.
prop(37, 22, "statue", 1.0, True, 17)
prop(36, 23, "h_flowers", 0.8); prop(38, 23, "h_flowers", 0.8)
prop(37, 24, "bench", 0.7, True, 12)
prop(35, 24, "lantern", 0.65)

# ---- the colliers' burn: the kilns, the cordwood, the water they keep to hand
#      because a kiln that gets away takes the wood with it.
for _kx, _ky in ((45, 41), (48, 43), (52, 41)):
    prop(_kx, _ky, "campfire", 1.1)                   # the earth kilns, smoking
    prop(_kx - 1, _ky + 1, "orePile", 0.8, True, 13)  # the charcoal drawn off
for _lx, _ly in ((43, 38), (44, 38), (43, 39), (46, 37), (47, 37), (54, 43), (55, 43)):
    prop(_lx, _ly, "log", 0.9)
prop(50, 43, "barrel", 0.65, True, 11)                # water, kept to hand
prop(51, 42, "h_bucket", 0.7)
prop(53, 39, "toolRack", 0.7, True, 12)
prop(49, 44, "h_cart", 0.8, True, 15)
prop(46, 45, "stump", 0.8, True, 12); prop(52, 46, "stump", 0.8, True, 12)

# ---- the vale: the bell, the graves, the gardens. Small, tended, and older
#      than Ashwold — the chapel was here before the road mattered.
prop(17, 33, "well", 0.8, True, 16)
prop(12, 33, "graveyard", 0.85)
for _gx, _gy in ((11, 31), (13, 32), (12, 33), (14, 31)):
    prop(_gx, _gy, "gravestone", 0.7)
prop(16, 32, "statue", 0.85, True, 15)                # the chapel's mark
prop(18, 34, "lantern", 0.65); prop(22, 33, "lantern", 0.65)
prop(24, 33, "h_flowerbush", 0.75); prop(22, 35, "h_flowerbush", 0.75)
prop(25, 34, "h_fence", 0.85, True, 13); prop(26, 34, "h_fence", 0.85, True, 13)
prop(24, 29, "h_bush", 0.75); prop(16, 29, "h_bush", 0.75)
prop(26, 37, "h_cart", 0.8, True, 15)
prop(20, 36, "bench", 0.7, True, 12)
prop(21, 35, "h_flowers", 0.8)

# ---- the footbridge over the vale water.
for _bx in (26, 31):
    prop(_bx, 30, "support", 0.6, True, 11); prop(_bx, 35, "support", 0.6, True, 11)

# ---- the hollow: a ruin the wood grew back over. Nothing points at it.
prop(11, 47, "ruin", 1.1, True, 22)
prop(9, 46, "rock", 0.9, True, 14); prop(13, 49, "rock", 0.9, True, 14)
prop(10, 49, "gravestone", 0.7); prop(12, 45, "gravestone", 0.7)
prop(11, 50, "campfire", 0.7)                         # somebody camps here still

# ---- a deer trail marker deep in the north wood, and the forester's high seat.
prop(20, 14, "scaffold", 0.85, True, 15)
prop(19, 16, "log", 0.85)
prop(52, 20, "tent", 0.8, True, 15)                   # a delver's camp under the rock
prop(53, 21, "campfire", 0.8)
prop(51, 21, "crate", 0.6, True, 12)

# =====================================================================
#  5. THE WOOD
#
#  Thin by the road, thick away from it, thickest where nobody cuts. The wood is
#  the region: everything else is a hole in it.
# =====================================================================
STANDS = [
    (0, 6, 30, 30, 0.40, (6, 3, 1, 0)),      # the deep north-west: old, unlogged
    (0, 26, 22, 56, 0.34, (3, 5, 2, 0)),     # the west wood, down to the seam
    (26, 22, 46, 56, 0.28, (5, 4, 1, 0)),    # either side of the road
    (40, 8, 60, 26, 0.20, (7, 1, 2, 0)),     # the hanger under the mountains
    (44, 44, 62, 56, 0.24, (4, 4, 2, 0)),    # the wood against the foothills
]
CLEARINGS = [VALE, BURN, HOLLOW, (30, 12, 44, 26)]     # the ford stands in the open

UNDER = [
    (0, 6, 32, 34, 0.20, ("bush", "bush", "flowers", "rock")),
    (0, 26, 24, 56, 0.18, ("bush", "flowers", "log", "rock")),
    (26, 22, 48, 56, 0.14, ("bush", "flowers", "flowers", "log")),
    (40, 8, 62, 28, 0.12, ("rock", "rock", "bush", "orePile")),
    (42, 42, 64, 56, 0.14, ("bush", "log", "rock", "flowers")),
]

TREES, COVER = [], []


def _hash(x, y, salt=0):
    h = (x * 374761393 + y * 668265263 + salt * 2246822519) & 0xFFFFFFFF
    h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def clear_of_works(x, y, pad=1):
    for dx in range(-pad, pad + 1):
        for dy in range(-pad, pad + 1):
            if at(x + dx, y + dy) in ",tc=~B#g": return False
    return True


def in_clearing(x, y):
    return any(x0 <= x <= x1 and y0 <= y <= y1 for x0, y0, x1, y1 in CLEARINGS)


def edge_falloff(x, y, x0, y0, x1, y1):
    d = min(x - x0, x1 - x, y - y0, y1 - y)
    return max(0.0, min(1.0, d / 3.5))


for (x0, y0, x1, y1, dens, mix) in STANDS:
    kinds = (["pine"] * mix[0] + ["broadleaf"] * mix[1]
             + ["deadTree"] * mix[2] + ["cherry"] * mix[3])
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            if not clear_of_works(x, y) or in_clearing(x, y): continue
            p = dens * (0.35 + 0.65 * edge_falloff(x, y, x0, y0, x1, y1))
            if _hash(x, y, 1) < p:
                TREES.append((x, y, kinds[int(_hash(x, y, 2) * len(kinds)) % len(kinds)]))

planted = {(x, y) for x, y, _ in TREES}
for (x0, y0, x1, y1, dens, kinds) in UNDER:
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            if (x, y) in planted or not clear_of_works(x, y, 0): continue
            if in_clearing(x, y) and _hash(x, y, 5) > 0.3: continue
            if _hash(x, y, 3) < dens:
                COVER.append((x, y, kinds[int(_hash(x, y, 4) * len(kinds)) % len(kinds)]))

# =====================================================================
#  6. WHO IS HERE
# =====================================================================
SPAWN = (ROAD_X, 52)              # on the road, a stride in from the village

NPCS = [
    ("mirkafather", 23, 35),      # beside the door, on the vale lane — NOT on the
                                  #   door portal, which would shadow him
    ("chapel_keeper", 16, 37),    # at the chapel
    ("vale_widow", 24, 31),       # on her own doorstep
    ("collier", 47, 42),          # at the kilns
    ("forester", 33, 22),         # at the ford, where the road needs watching
]

SAFE = [
    (12, 26, 28, 40),             # the vale
    (28, 14, 44, 26),             # the ford and the cross
    (42, 36, 57, 48),             # the colliers' ground
    (ROAD_X - 4, 46, ROAD_X + 4, 56),   # the road in from the village
]

DISCOVERIES = [
    ("The Whispering Wood", ROAD_X, 44, 4.0, "RegionDiscovered"),
    ("The Grey Ford", 36, 18, 3.4, "RegionDiscovered"),
    ("The Wayside Cross", 37, 22, 3.0, "RegionDiscovered"),
    ("The Cleric's Vale", 20, 34, 3.6, "RegionDiscovered"),
    ("The Colliers' Burn", 48, 42, 3.4, "RegionDiscovered"),
    ("The Hollow", 11, 47, 3.2, "SecretArea"),
    ("The Deer Trail", 20, 15, 3.0, "SecretArea"),
]

CONTAINERS = [
    ("A collier's crate", 51, 42, "crate"),
    ("The kiln-master's barrel", 50, 43, "barrel"),
    ("A cache under the cross", 35, 24, "chest_wood"),
    ("The chapel's poor-box", 16, 37, "chest_wood"),
    ("A byre corner", 25, 38, "crate"),
    ("Something in the ruin", 11, 47, "chest_iron"),
    ("A delver's abandoned pack", 51, 21, "crate"),
    ("Under the high seat", 20, 14, "chest_wood"),
    ("Washed against the ford", 42, 20, "barrel"),
]

EXAMINABLES = [
    ("the ford stone", 29, 15, "Read", "plaque",
     "A slab set on end at the water's edge, the letters cut deep and filled with "
     "moss. \"THE GREY FORD. LAID BY THE HOUSE OF ASHWOLD THAT MEN MIGHT PASS DRY. \" "
     "Underneath, cut later and by a worse hand: \"AND COME BACK THE SAME WAY.\""),
    ("the wayside cross", 37, 22, "Examine", "note",
     "The Light's mark on a shaft of grey stone, and the shaft is barely visible "
     "for what is tied to it. Ribbons. A child's shoe. A pick-head with the haft "
     "snapped off at the socket. Somebody has knotted a lock of hair around the "
     "arm of it and the knot is not old."),
    ("the kilns", 47, 42, "Examine", "note",
     "Three earth kilns, banked and smoking, and a fourth pulled apart and cooling. "
     "The trick, the colliers will tell you if you stand still long enough, is that "
     "the fire must never see the air. Everything they make goes down the road to "
     "Ashwold's forge, and the forge sharpens what goes up to the mine."),
    ("the chapel", 15, 37, "Read", "plaque",
     "A low chapel of grey stone with a bell in a wooden frame beside it, because "
     "the tower fell in the year of the great water and was never rebuilt. The "
     "board by the door keeps the count: one stroke for a party going up, two for "
     "one coming down. The going-up column has three times the marks."),
    ("the Cleric's door", 20, 34, "Examine", "note",
     "A good house for this vale, and shut. The step is swept and the sill has been "
     "kept oiled by somebody who still does it every week. There is a bowl of water "
     "set out beside the door, the way they do here for a house with sickness in it, "
     "and it is fresh."),
    ("the graves of the vale", 12, 34, "Examine", "note",
     "Older stones than Ashwold's, and fewer. This vale buries its own and has been "
     "doing it a long time. Three at the end are new, and all three of those say the "
     "same thing under the name: TAKEN UP THE ROAD."),
    ("the high seat", 20, 14, "Examine", "note",
     "A forester's platform lashed into a pine, ten feet up, with the bark worn "
     "smooth where hands have gone. From here you can see the road for half a mile "
     "in both directions. Someone has cut marks into the rail — a tally, and the "
     "tally is of things that went north and did not come south."),
    ("the ruin in the hollow", 11, 47, "Read", "plaque",
     "Four courses of dressed stone in a square, and the moss has been scraped off "
     "one of them recently. The letters are the old kind, the same as the boundary "
     "stone in Ashwold's fields — Ychellen. Whatever stood here was standing before "
     "anyone in the vale had a name for the mountain."),
    ("the mountain road", 58, 13, "Examine", "note",
     "The road stops climbing here and runs along the foot of the rock, east, where "
     "the wall of the mountains breaks. The wheel-ruts are deep and old and they all "
     "go the same way. Nothing has driven back down them in a long while."),
]

# =====================================================================
#  6b. VALIDATE
# =====================================================================
WALKABLE = ".,tc=g"
_moved = []


def ground(x, y):
    if at(x, y) in WALKABLE: return x, y
    for r in range(1, 8):
        for dx in range(-r, r + 1):
            for dy in range(-r, r + 1):
                if max(abs(dx), abs(dy)) != r: continue
                if at(x + dx, y + dy) in WALKABLE: return x + dx, y + dy
    return x, y


def validated(items, label, xi=0, yi=1):
    out = []
    for it in items:
        it = list(it)
        nx, ny = ground(it[xi], it[yi])
        if (nx, ny) != (it[xi], it[yi]):
            _moved.append(f"{label} {it[xi], it[yi]} -> {(nx, ny)} (was {at(it[xi], it[yi])!r})")
            it[xi], it[yi] = nx, ny
        out.append(tuple(it))
    return out


PROPS[:] = validated(PROPS, "prop")
NPCS[:] = validated(NPCS, "npc", 1, 2)
EXAMINABLES[:] = validated(EXAMINABLES, "examine", 1, 2)
CONTAINERS[:] = validated(CONTAINERS, "container", 1, 2)
DISCOVERIES[:] = validated(DISCOVERIES, "discovery", 1, 2)
if at(*SPAWN) not in WALKABLE:
    SPAWN = ground(*SPAWN); _moved.append(f"SPAWN -> {SPAWN}")

# =====================================================================
#  7. PAINT IT
# =====================================================================
LNAMES = (["Ground", "GroundDetails", "Roads", "Bridges", "Water", "Shore",
           "Cliffs", "CliffFace", "Buildings", "Walls", "Objects"]
          + K.TREE_LAYERS + ["AbovePlayer", "Shadows"])
OBJ_ORDER = ["PlayerSpawn", "NPCSpawn", "EnemySpawn", "Warp", "Interaction",
             "Props", "TreasureSpawn", "Discovery", "SafeZone", "MusicZone"]

layers = {n: [0] * (MW * MH) for n in LNAMES}


def paint(layer, mx, my, g):
    if g and 0 <= mx < MW and 0 <= my < MH:
        layers[layer][my * MW + mx] = g


M = [[at(mx // K.SUB, my // K.SUB) for my in range(MH)] for mx in range(MW)]


def m(mx, my):
    if mx < 0 or my < 0 or mx >= MW or my >= MH: return "#"
    return M[mx][my]


for my in range(MH):
    for mx in range(MW):
        c = m(mx, my)
        dx, dy = K.vary(K.SOLID, mx, my)
        paint("Ground", mx, my, K.gid("Floors", K.FLOOR_BLOCK["grass"] + dx, dy))

        if c == "t":
            k = lambda xx, yy: m(xx, yy) in "t,=c"
            paint("GroundDetails", mx, my, K.floor_tile("dirt", mx, my,
                  k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))
        elif c == ",":
            k = lambda xx, yy: m(xx, yy) in ",=ct"
            paint("Roads", mx, my, K.floor_tile("dirt", mx, my,
                  k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))
        elif c == "c":
            k = lambda xx, yy: m(xx, yy) in "c,=t"
            paint("Roads", mx, my, K.floor_tile("cobble", mx, my,
                  k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))
        elif c == "=":
            k = lambda xx, yy: m(xx, yy) in "=,ct"
            paint("Bridges", mx, my, K.floor_tile("cobble", mx, my,
                  k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))
        elif c == "~":
            paint("Water", mx, my, K.water_solid(mx, my))
        elif c == "#":
            n, s_ = m(mx, my - 1) == "#", m(mx, my + 1) == "#"
            e_, w_ = m(mx + 1, my) == "#", m(mx - 1, my) == "#"
            paint("Cliffs", mx, my, K.cliff_top(mx, my, n, e_, s_, w_))
            if not s_:
                paint("CliffFace", mx, my + 1, K.cliff_face(mx, my, 6))
                paint("CliffFace", mx, my + 2, K.cliff_face(mx, my, 8))
        elif c == "B":
            dx, dy = K.vary(K.SOLID, mx, my)
            paint("GroundDetails", mx, my, K.gid("Floors", K.FLOOR_BLOCK["dirt"] + dx, dy))
        elif c == "g":
            k = lambda xx, yy: m(xx, yy) in "gB,ct"
            paint("GroundDetails", mx, my, K.floor_tile("dirt", mx, my,
                  k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))

for my in range(MH):
    for mx in range(MW):
        if m(mx, my) in "~#": continue
        wn, ws = m(mx, my - 1) == "~", m(mx, my + 1) == "~"
        we, ww = m(mx + 1, my) == "~", m(mx - 1, my) == "~"
        if wn or ws or we or ww:
            paint("Shore", mx, my, K.shore_tile(mx, my, wn, we, ws, ww))

for (ex, ey, mat, roof, bays, storeys, name) in BUILDINGS:
    walls, roofs, (bw, bh) = K.house_tiles(mat, roof, bays, storeys)
    ox = ex * K.SUB - 1
    oy = (ey + 1) * K.SUB - bh
    for dx, dy, s_, c_, r_ in roofs:
        if K.opaque(s_, c_, r_): paint("Buildings", ox + dx, oy + dy, K.gid(s_, c_, r_))
    for dx, dy, s_, c_, r_ in walls:
        if K.opaque(s_, c_, r_): paint("Walls", ox + dx, oy + dy, K.gid(s_, c_, r_))
    # A door in the middle of the visible facade, two tiles of it, drawn over the
    # wall rather than under the roof — the old stamp was keyed to a four-course
    # facade and now landed halfway up the roof.
    dxm = ox + bw // 2 - 1
    dym = oy + K.HOUSE_H + (storeys - 1) * len(K.FACADE_ROWS) - 2
    for i, (c_, r_) in enumerate(((6, 2), (7, 2), (6, 3), (7, 3))):
        paint("Buildings", dxm + (i % 2), dym + (i // 2), K.gid("BuildProps", c_, r_))

# The ground itself: sprigs everywhere, reed and fern where it is wet. A wood
# floor of flat green is the one thing that gives a hand-made map away.
# The density pass — crops on the worked ground, flower sprigs by the houses,
# and leaf/reed texture on the open grass. See region_kit.dress_ground.
_crops, _sprigs, _tufts = K.dress_ground(at, paint, COLS, ROWS)

_per_layer = K.plant(TREES, paint, lambda ex, ey: int(_hash(ex, ey, 7) * 997))

for (ex, ey, kind) in COVER:
    setname, opts = K.GROUND_COVER[kind]
    c_, r_ = K.vary(opts, ex, ey)
    paint("Objects", ex * K.SUB + (1 if _hash(ex, ey, 8) > 0.5 else 0),
          ey * K.SUB + 1, K.gid(setname, c_, r_))

# =====================================================================
#  8. WRITE IT
# =====================================================================
_oid = [1]


def nid():
    _oid[0] += 1
    return _oid[0] - 1


def obj(name, ex, ey, pairs, w=0, h=0):
    a = f'id="{nid()}" name="{sx.escape(name)}" x="{ex*K.T:.2f}" y="{ey*K.T:.2f}"'
    if w or h: a += f' width="{w*K.T:.2f}" height="{h*K.T:.2f}"'
    inner = K.props_xml(pairs)
    if not (w or h): inner += "   <point/>\n"
    return f"  <object {a}>\n{inner}  </object>\n"


og = {n: [] for n in OBJ_ORDER}
og["PlayerSpawn"] = [obj("PlayerSpawn", SPAWN[0] + 0.5, SPAWN[1] + 0.5, [("Type", "PlayerSpawn")])]
og["NPCSpawn"] = [obj(i, x + 0.5, y + 0.5, [("NpcId", i)]) for i, x, y in NPCS]
og["Props"] = [obj(k, x + 0.5, y + 0.5,
                   [("Kind", k), ("Variant", int(_hash(x, y, 9) * 64)), ("Scale", round(s, 3)),
                    ("Flip", "true" if _hash(x, y, 10) > 0.5 else "false"),
                    ("Solid", str(sol).lower()), ("Radius", round(rad, 2)),
                    ("Painted", "false")])
               for x, y, k, s, sol, rad in PROPS]
og["Props"] += [obj(k, x + 0.5, y + 0.5,
                    [("Kind", k), ("Variant", int(_hash(x, y, 11) * 64)), ("Scale", 1.0),
                     ("Flip", "false"), ("Solid", "true"),
                     ("Radius", 13 if k in ("pine", "broadleaf") else 10),
                     ("Painted", "true")])
                for x, y, k in TREES]
og["Props"] += [obj(k, x + 0.5, y + 0.5,
                    [("Kind", k), ("Variant", int(_hash(x, y, 12) * 64)), ("Scale", 0.8),
                     ("Flip", "false"), ("Solid", "false"), ("Radius", 0),
                     ("Painted", "true")])
                for x, y, k in COVER]
# The Cleric's house opens onto its own map, as it always has.
og["Warp"] = [obj("the Cleric's house", 20.5, 34.5,
                  [("DestinationMap", "cleric_house"), ("DestinationSpawn", "0.0,0.0"),
                   ("Label", "the Cleric's house"), ("Verb", "Enter"), ("Radius", 40)])]
og["Interaction"] = [obj(t, x + 0.5, y + 0.5,
                         [("InteractionType", "Examine"), ("Verb", v), ("ReadKind", k),
                          ("Radius", 46), ("Pages", p)])
                     for t, x, y, v, k, p in EXAMINABLES]
og["TreasureSpawn"] = [obj(n, x + 0.5, y + 0.5, [("ContainerKind", k)])
                       for n, x, y, k in CONTAINERS]
og["Discovery"] = [obj(n, x + 0.5, y + 0.5,
                       [("Radius", round(r * K.ENGINE_TILE, 1)), ("XpSource", s)])
                   for n, x, y, r, s in DISCOVERIES]
og["SafeZone"] = [obj(f"safe{i}", x0, y0, [], x1 - x0, y1 - y0)
                  for i, (x0, y0, x1, y1) in enumerate(SAFE)]

lid, parts = 400, []
for n in LNAMES:
    lid += 1
    parts.append(f''' <layer id="{lid}" name="{n}" width="{MW}" height="{MH}">
  <data encoding="base64" compression="zlib">{K.encode(layers[n])}</data>
 </layer>
''')
for n in OBJ_ORDER:
    lid += 1
    parts.append(f''' <objectgroup id="{lid}" name="{n}">
{"".join(og[n])} </objectgroup>
''')

tmx = f'''<?xml version="1.0" encoding="UTF-8"?>
<!-- REGION 2 — THE WHISPERING WOOD. Hand-authored; see
     tools/build-r2-whispering-wood.py for why each thing is where it is.
     Its SOUTH edge is a contract with Ashwold, read from that map's own north
     edge: road at columns {ROAD_IN[0]}-{ROAD_IN[1]}, water at {WATER_IN[0]}-{WATER_IN[1]}, rock from {ROCK_IN[0]}.
     16x16 grid; one engine cell is a 2x2 block, so the map is {MW}x{MH}. -->
<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down"
     width="{MW}" height="{MH}" tilewidth="{K.T}" tileheight="{K.T}" infinite="0"
     nextlayerid="{lid+1}" nextobjectid="{_oid[0]}">
 <properties>
  <property name="Region" value="{REGION_ID}"/>
  <property name="DisplayName" value="{sx.escape(DISPLAY)}"/>
  <property name="Stage" type="int" value="1"/>
  <property name="EngineTile" type="int" value="{K.ENGINE_TILE}"/>
  <property name="Wildlife" type="int" value="40"/>
  <property name="SouthMap" value="{SOUTH_NEIGHBOUR}"/>
  <property name="EastMap" value="r3_deepdelve"/>
 </properties>
{chr(10).join(K.TSREFS)}
{"".join(parts)}</map>
'''
path_out = os.path.join(OUT, "R2_WhisperingWood.tmx")
open(path_out, "w").write(tmx)

_edges = K.save_edges(REGION_ID, G, COLS, ROWS)
print(f"wrote {path_out}  ({MW}x{MH} tiles = {COLS}x{ROWS} cells, "
      f"{os.path.getsize(path_out)/1024:.0f} KB)")
print(f"  buildings {len(BUILDINGS)}   npcs {len(NPCS)}   placed props {len(PROPS)}")
print(f"  trees {len(TREES)} across {len(K.TREE_LAYERS)} layers {_per_layer}   "
      f"undergrowth {len(COVER)}   ground: {_crops} crops, {_sprigs} flower sprigs, {_tufts} tufts")
print(f"  examinables {len(EXAMINABLES)}   containers {len(CONTAINERS)}   "
      f"discoveries {len(DISCOVERIES)}")
if _moved:
    print(f"  MOVED {len(_moved)} placements off unwalkable ground:")
    for line in _moved[:10]: print("    " + line)

# ---- the seam, checked rather than asserted ----
mine, theirs = _edges["south"], SEAM
bad = [i for i in range(COLS)
       if (mine[i] in ",t=") != (theirs[i] in ",t=")
       or (mine[i] == "~") != (theirs[i] == "~")
       or (mine[i] == "#") != (theirs[i] == "#")]
print(f"  seam south vs {SOUTH_NEIGHBOUR}: "
      + ("MATCHES" if not bad else f"MISMATCH at columns {bad}"))
print(f"    ours   {mine}")
print(f"    theirs {theirs}")
