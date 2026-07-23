#!/usr/bin/env python3
"""REGION 1 — ASHWOLD, the last village before the mine.

    python3 tools/build-r1-ashwold.py   ->  Maps/Regions/R1_Ashwold.tmx

This is not a generator. It is a level, written down. Every building, every
road and every barrel below is placed by hand with a reason, because the thing
that made the old Stage 1 read as procedural was not the tileset — it was that
nothing in it was anybody's decision.

WHY ASHWOLD IS HERE
    A river comes down out of the northern mountains; a stream comes west off
    the foothills; they meet, and a village sits in the fork, because that is
    where villages sit. The coast road from Seoshe crosses the river here on the
    only bridge for a day's walk, and the track to the mine leaves north from the
    same crossing. Ashwold lives off the delvers who pass through on their way up
    to the Cave Beyond Time: it feeds them, shoes their mules, sharpens their
    picks, and buries the ones who come back down.

HOW TO READ IT
    The village is laid out along the road, not around a point, because it grew
    along the road. Doors face the street. Gardens are behind the houses, away
    from the traffic. The smithy is the first building past the bridge — fire
    kept downwind of the thatch, and first claim on passing trade. The inn is on
    the square where the road forks, because that is where a traveller has to
    choose. The mill is upstream of the bridge so its leat runs clean. The fields
    are on the flat ground east and south, never in the wood. And the shrine and
    the graveyard are on the north road out of the village — the last thing you
    pass on the way up to the cave, and the first thing you pass coming back.

THE PLAYER'S LINE
    You arrive from the west, through the Seoshe gate, on the road. The bridge
    and the mill are the first thing you see; the village lies beyond them. The
    street carries you east to the square, where it forks: north to the wood and
    the mine, east to Kae Ychel. The graves on the north road tell you which way
    is dangerous before a single line of dialogue does.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import region_kit as K
import xml.sax.saxutils as sx

OUT = os.path.join(K.MAPS, "Regions")
os.makedirs(OUT, exist_ok=True)

COLS, ROWS = 72, 56                       # engine cells
MW, MH = COLS * K.SUB, ROWS * K.SUB       # map tiles: 144 x 112

REGION_ID = "r1_ashwold"
DISPLAY = "Ashwold"

# The terrain grid, at ENGINE resolution. One character per cell:
#   '.' grass   ',' dirt road   't' trodden track   'c' cobble
#   '~' water   '=' bridge   '#' rock   'g' worked ground
#   'B' building (solid; its facade is the wall you see)
G = [["." for _ in range(ROWS)] for _ in range(COLS)]


def put(x, y, ch):
    if 0 <= x < COLS and 0 <= y < ROWS:
        G[x][y] = ch


def at(x, y):
    if x < 0 or y < 0 or x >= COLS or y >= ROWS: return "#"
    return G[x][y]


def rect(x0, y0, x1, y1, ch):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            put(x, y, ch)


def path(points, width, ch, over=None):
    """A route through waypoints, `width` cells across. Roads and rivers are
    drawn this way rather than as rectangles so they bend, and so a bend reads as
    a reason (the road goes round the rock; the river found the low ground)."""
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
#  1. THE LAND
#
#  Water first, because water decides everything else: the crossing decides
#  where the road goes, and the road decides where the village is.
# =====================================================================

# The Ashwold — the river the village is named for. Out of the mountains at the
# north, down the west of the valley, away south toward the sea and Seoshe. It
# bends east around the harder ground under the mill and then runs straight.
RIVER = [(21, -2), (24, 7), (26, 14), (24, 20), (20, 27), (16, 34), (14, 42), (15, 58)]
path(RIVER, 3, "~")

# The Ashbrook — a stream off the eastern foothills, joining the river at the
# fork. It is why the north road has a ford, and why the water meadow is wet.
BROOK = [(74, 9), (64, 11), (56, 13), (48, 15), (40, 16), (32, 17), (26, 18), (23, 19)]
path(BROOK, 2, "~")

# The foothills close the north-east: the ground rises here, the brook comes out
# of it, and there is no way through — which is what makes the north road the
# way north instead of one option among many.
for x in range(46, COLS):
    h = 11 - int((x - 46) * 0.12)
    for y in range(0, max(0, h)):
        put(x, y, "#")
# A spur of the same rock closes the south-east and gives the Kae Ychel road a
# corridor to run in, so leaving the village east feels like a departure.
for x in range(62, COLS):
    for y in range(38, 52 - (x - 62) // 2):
        put(x, y, "#")

# =====================================================================
#  2. THE ROADS
#
#  Three, and each one goes somewhere: the coast road in from Seoshe, the mine
#  road north, the Kae Ychel road east. They meet at the square, which is the
#  only place in the region where a decision has to be made.
# =====================================================================
SQUARE = (35, 28, 41, 33)

# The Coast Road — in from the west edge, over the bridge, through the village.
path([(-2, 30), (10, 30), (17, 30)], 3, ",", over=".#")
path([(21, 30), (28, 30), (35, 30)], 2, ",", over=".#")
# The bridge. The only crossing: a stone one, because the coast road is a trade
# road and the tolls paid for it.
# The deck is five cells across so the parapet can stand ON it. Built one cell
# narrower, the rails had nowhere to go but the river.
for _bx in range(15, 24):
    for _by in range(28, 33):
        if at(_bx, _by) in "~.,": put(_bx, _by, "=")
BRIDGE_SPAN = [x for x in range(15, 24) if at(x, 30) == "="]

# The square, cobbled — the one paved thing in the village, laid by whoever
# could afford it, which is the reason the inn faces it.
rect(*SQUARE, ch="c")

# The Mine Road — north out of the square, over the ford, past the graves.
path([(38, 32), (38, 27), (37, 22), (37, 17), (36, 12), (36, 4), (36, -2)], 2, ",", over=".#")
# The ford. Stepping stones, not a bridge: nobody has ever paid for this one,
# which says plainly which road matters and which road is the one the delvers
# take.
rect(35, 15, 39, 18, "=")

# The Kae Ychel Road — east out of the square, through the gap in the rock.
path([(41, 31), (48, 31)], 2, ",", over=".#")
path([(48, 31), (56, 32), (64, 32), (74, 32)], 3, ",", over=".#")

# The mill track — up the east bank from the road to the mill. Short, and worn.
path([(26, 29), (27, 26), (28, 23), (28, 22)], 2, "t", over=".")
# The back lane — the second row of houses is reached from here, not from the
# high street, which is how a village thickens without widening.
path([(30, 32), (30, 38), (44, 38)], 2, "t", over=".")
# The field track — off the back lane out to the barn and the strips.
path([(44, 38), (52, 40), (58, 42)], 2, "t", over=".")
# The path down to the water — to the fishing steps below the bridge.
path([(19, 32), (19, 35), (19, 37)], 1, "t", over=".")

# =====================================================================
#  3. THE BUILDINGS
#
#  Eleven of them, and every one is somebody's. They are anchored by the
#  bottom-left of the FACADE in engine cells; the facade is what blocks, and the
#  roof above it is picture only, so the eaves overhang the way eaves do.
# =====================================================================
BUILDINGS = []   # (ex, ey, material, roof, bays, storeys, name, door_dx)


def building(ex, ey, material, roof, name, bays=1, storeys=1, door=1):
    """A building whose facade's bottom-left sits at engine cell (ex, ey)."""
    BUILDINGS.append((ex, ey, material, roof, bays, storeys, name, door))
    for x in range(ex, ex + 3 * bays):
        for y in range(ey - 1, ey + 1):
            put(x, y, "B")


#  --- the high street, north side. Doors face the road; the gardens are behind.
building(24, 27, "board", "shingle", "the smithy")           # first past the bridge
building(29, 27, "log", "shingle", "Hadda's cottage")
building(37, 26, "plaster", "tile", "The Delvers' Rest", storeys=2)   # on the square
building(44, 27, "plank", "shingle", "the provisioner")
building(49, 28, "log", "shingle", "Orrin's cottage")

#  --- the back lane, south of the street. Poorer, older, tighter together.
building(31, 37, "log", "shingle", "the widow's cottage")
building(36, 37, "log", "shingle", "a cottage")
building(41, 37, "board", "shingle", "the fisher's cottage")

#  --- the mill, upstream of the bridge where the leat runs clean, and the
#      miller's own house beside it because a mill is never left alone.
building(25, 21, "board", "shingle", "the mill", bays=2)
building(31, 24, "plank", "shingle", "the miller's house")

#  --- the barn, out at the fields, big and plain.
building(52, 43, "board", "shingle", "the barn", bays=2)

# --- every household's own ground. A cottage without a plot behind it is a
#     model of a cottage; the plot is what says somebody lives in it. The path
#     from the door to the street is worn because it is walked every day.
def plot(ex, ey, w, h):
    """A worked garden. Only the BEDS are dug — two rows of them — and the rest
    stays grass, because a garden read as a slab of bare earth behind every
    house is what made the village look burnt rather than lived in."""
    for x in range(ex, ex + w):
        for y in range(ey, ey + h):
            if at(x, y) == "." and (y - ey) % 2 == 0 and 0 < (x - ex) < w - 1:
                put(x, y, "g")


def doorpath(ex, ey, to_y):
    """The trodden line from a doorstep to the road it faces. One cell wide and
    worn grass, not metalled road: seven of these at road width turned the
    village into a brown web with houses in the gaps."""
    step = 1 if to_y > ey else -1
    for y in range(ey, to_y + step, step):
        if at(ex, y) == ".": put(ex, y, "t")


for px_, py_, pw, ph in ((24, 22, 8, 4), (29, 22, 5, 4), (44, 22, 5, 4),
                         (49, 23, 5, 4), (31, 39, 4, 4), (36, 39, 4, 4),
                         (41, 39, 4, 4), (30, 19, 5, 3)):
    plot(px_, py_, pw, ph)
for dx_, dy_, ty_ in ((25, 28, 30), (30, 28, 30), (45, 28, 30), (50, 29, 31),
                      (32, 38, 39), (37, 38, 39), (42, 38, 39)):
    doorpath(dx_, dy_, ty_)

# --- the fields. Ploughed strips running with the slope, hedged apart, because
#     a field is a shape on the ground and not a fence with grass inside it.
for fy in range(45, 54, 3):
    for x in range(45, 63):
        for y in range(fy, fy + 2):
            if at(x, y) == ".": put(x, y, "g")

# --- the stock pen between the village and the fields: fenced, trodden bare at
#     the gate, which is where the animals stand waiting.
for x in range(49, 58):
    for y in range(35, 41):
        # bare at the gate and along the rail where the stock stands; grass in
        # the middle, because that is what they are in there to eat.
        if at(x, y) == "." and (y == 40 or x == 49 or x == 57):
            put(x, y, "g")

def frontage(ex, ey, w, gap):
    """A fence along a plot's street side, with a gap for the gate. The gap is
    where that household's own path meets the road, which is what makes the
    fence look built rather than drawn."""
    for x in range(ex, ex + w):
        if x == gap or x == gap + 1: continue
        if at(x, ey) in ".t":
            PROPS.append((x, ey, "h_fence", 0.85, True, 13))


def hedge(x0, y0, x1, y1):
    """A planted line. A hedge is a line; scatter is not a hedge."""
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            if at(x, y) == ".":
                COVER.append((x, y, "bush"))


# =====================================================================
#  4. THE PROPS
#
#  Grouped by the thing they belong to. Nothing here is scatter: if it cannot
#  be answered with "whose is it and why is it there", it is not in the list.
# =====================================================================
PROPS = []     # (ex, ey, kind, scale, solid, radius)


def prop(ex, ey, kind, scale=1.0, solid=False, radius=0.0):
    PROPS.append((ex, ey, kind, scale, solid, radius))


# ---- the Seoshe gate: the road out to the coast, and the region's west door.
prop(6, 28, "support", 0.9, True, 15); prop(6, 32, "support", 0.9, True, 15)
prop(6, 27, "banner", 0.7); prop(6, 33, "banner", 0.7)
prop(8, 28, "signpost", 0.7, True, 10)          # SEOSHE ← / ASHWOLD →

# ---- the bridge: a parapet, and a lantern for the night crossing.
prop(16, 28, "support", 0.7, True, 12); prop(22, 28, "support", 0.7, True, 12)
prop(16, 32, "support", 0.7, True, 12); prop(22, 32, "support", 0.7, True, 12)
prop(23, 28, "lantern", 0.6)

# ---- the mill: the wheel is in the river, the sacks are on the bank, and the
#      cart that carries the flour down to the road is standing where it is
#      loaded.
prop(24, 22, "waterfall", 0.8)                  # the wheel's race
prop(28, 22, "crate", 0.6, True, 12); prop(29, 22, "barrel", 0.6, True, 11)
prop(29, 23, "wagon", 0.8, True, 18)
prop(33, 24, "h_bucket", 0.7)
prop(31, 26, "h_fence", 0.8, True, 12); prop(33, 26, "h_fence", 0.8, True, 12)

# ---- the smithy: fire, water, iron, and the broken things waiting for it.
prop(23, 28, "campfire", 0.8)                   # the forge, banked
prop(22, 29, "orePile", 0.7, True, 12)          # charcoal, kept clear of the fire
prop(27, 28, "workbench", 0.8, True, 16)        # the anvil block
prop(28, 29, "toolRack", 0.7, True, 12)
prop(23, 30, "barrel", 0.6, True, 11)           # the slack tub
prop(26, 29, "mineCart", 0.7, True, 16)         # a delver's cart in for a wheel

# ---- The Delvers' Rest: the sign, the lantern that is lit all night, the
#      benches by the door, the stable yard behind, the empties by the wall.
prop(36, 27, "lantern", 0.7)
prop(41, 27, "lantern", 0.7)
prop(36, 28, "bench", 0.7, True, 12); prop(41, 28, "bench", 0.7, True, 12)
prop(43, 25, "wagon", 0.85, True, 18)           # the carrier's wagon, in the yard
prop(44, 24, "barrel", 0.6, True, 11); prop(45, 25, "barrel", 0.6, True, 11)
prop(43, 24, "crate", 0.6, True, 12)

# ---- the square: the well the whole village draws from, the notice post at the
#      fork, and one old tree with a bench under it.
prop(38, 31, "well", 0.85, True, 17)
prop(40, 30, "signpost", 0.75, True, 10)        # MINE ↑ / KAE YCHEL →
prop(35, 32, "broadleaf", 1.15)                 # the square's tree
prop(35, 33, "bench", 0.7, True, 12)
prop(37, 33, "stall", 0.7, True, 15)            # market day leaves its frames up
prop(39, 33, "stall", 0.7, True, 15)

# ---- the provisioner: what a delver buys, stacked outside where it is seen.
prop(47, 27, "crate", 0.65, True, 12); prop(47, 28, "barrel", 0.6, True, 11)
prop(48, 27, "crate", 0.6, True, 12)
prop(43, 28, "stall", 0.7, True, 15)

# ---- the cottages: gardens behind, not in front. Flowers by the doors, beds
#      and a water butt behind, washing between two poles.
for gx, gy in ((29, 24), (31, 23), (50, 25), (32, 40), (37, 40), (42, 40)):
    prop(gx, gy, "h_flowerbush", 0.7)
for gx, gy in ((30, 23), (51, 24), (33, 41), (38, 41)):
    prop(gx, gy, "h_bush", 0.7)
prop(28, 28, "h_flowers", 0.7); prop(33, 28, "h_flowers", 0.7)
prop(48, 29, "h_flowers", 0.7); prop(34, 38, "h_flowers", 0.7)
prop(43, 39, "h_bucket", 0.7)
prop(31, 41, "h_fence", 0.8, True, 12); prop(33, 41, "h_fence", 0.8, True, 12)
prop(36, 41, "h_fence", 0.8, True, 12); prop(38, 41, "h_fence", 0.8, True, 12)
prop(30, 40, "h_cart", 0.75, True, 15)

# ---- the fisher's cottage, at the lane's end nearest the water, with the nets.
prop(40, 39, "h_bucket", 0.7)
prop(19, 36, "log", 0.8)                        # the landing stage
prop(20, 37, "crate", 0.6, True, 12)
prop(19, 37, "barrel", 0.55, True, 10)
prop(20, 34, "bench", 0.6, True, 11)

# ---- the fields: strips fenced off the pasture, a scarecrow, the barn's gear.
for fx in range(46, 62, 3):
    prop(fx, 47, "h_fence", 0.8, True, 12)
    prop(fx, 52, "h_fence", 0.8, True, 12)
prop(54, 49, "statue", 0.6)                     # the scarecrow, such as it is
prop(51, 45, "h_cart", 0.8, True, 15)
prop(55, 44, "crate", 0.6, True, 12); prop(56, 44, "barrel", 0.6, True, 11)
prop(58, 45, "log", 0.7)

# ---- the woodcutter's yard, where the road meets the wood: the felled stack,
#      the block, the axe-scarred stumps. He works the edge, not the deep wood.
prop(28, 13, "log", 0.9); prop(29, 13, "log", 0.9); prop(28, 14, "log", 0.9)
prop(30, 14, "stump", 0.8, True, 12)
prop(27, 12, "toolRack", 0.7, True, 12)
prop(30, 12, "tent", 0.8, True, 16)
prop(31, 15, "campfire", 0.7)

# ---- the shrine on the mine road, and the graves beyond it. This is the beat:
#      it is the last thing you pass going up, and it is full.
prop(33, 11, "statue", 1.0, True, 18)           # the Holy Light's mark
prop(32, 12, "lantern", 0.7); prop(34, 12, "lantern", 0.7)
prop(32, 10, "h_flowers", 0.8); prop(34, 10, "h_flowers", 0.8)
prop(33, 13, "bench", 0.7, True, 12)
prop(30, 9, "graveyard", 0.9)
for gx, gy in ((29, 7), (31, 7), (28, 8), (30, 8), (32, 8), (29, 10), (31, 10)):
    prop(gx, gy, "gravestone", 0.7)
prop(27, 9, "h_fence", 0.8, True, 12); prop(33, 8, "h_fence", 0.8, True, 12)

# ---- the ford: a marker stone on each bank, because in spate you need to know
#      where the stones are.
prop(34, 16, "rock", 0.9, True, 14); prop(40, 20, "rock", 0.9, True, 14)
prop(41, 17, "signpost", 0.7, True, 10)

# ---- the water meadow in the fork of the two waters: wet, unfenced, grazed.
for wx, wy in ((28, 20), (31, 21), (26, 21), (33, 20), (29, 22)):
    prop(wx, wy, "h_wildflowers", 0.75)

# ---- the broken cart on the Kae Ychel road, one wheel in the ditch. Somebody
#      is coming back for it; nobody has.
prop(56, 34, "wagon", 0.85, True, 18)
prop(57, 35, "crate", 0.6, True, 12)
prop(55, 35, "log", 0.7)

# ---- the old boundary stone where the fields end and the wood begins. It is
#      older than the village and nobody moves it.
prop(60, 47, "ruin", 0.9, True, 18)
prop(61, 48, "rock", 0.8, True, 13)

# ---- the plot boundaries. Each frontage takes its gate where that household's
#      own path comes out, so no fence is ever crossed to reach a door.
#      The street-side row fences the VERGE at y=28, not the road at y=29: a
#      fence laid on the carriageway is skipped by the terrain test and the
#      whole north side ended up with no boundary at all.
for _fx, _fy, _fw, _gap in ((24, 28, 5, 25), (29, 28, 5, 30), (44, 28, 5, 45),
                            (49, 29, 5, 50), (31, 39, 5, 32), (36, 39, 5, 37),
                            (41, 39, 5, 42)):
    frontage(_fx, _fy, _fw, _gap)

# ---- the bridge's parapet, down both sides of the span.
for _bx in BRIDGE_SPAN:
    prop(_bx, 28, "support", 0.55, True, 10)
    prop(_bx, 32, "support", 0.55, True, 10)

# ---- the ground between the village and the fields, which was open green with
#      nothing in it. An orchard row behind the inn, a stack of hay by the pen,
#      and the two big trees a village always has on its common.
prop(46, 22, "cherry", 1.0); prop(48, 23, "cherry", 1.0); prop(50, 22, "cherry", 1.0)
prop(46, 25, "cherry", 1.0); prop(49, 26, "cherry", 1.0)
prop(53, 33, "broadleaf", 1.25); prop(59, 29, "broadleaf", 1.2)
prop(56, 27, "log", 0.8); prop(57, 28, "log", 0.8)
prop(52, 36, "crate", 0.6, True, 12); prop(53, 37, "barrel", 0.6, True, 11)
prop(50, 34, "h_cart", 0.8, True, 15)
prop(58, 38, "tent", 0.75, True, 15)          # a drover's camp by the pen gate
prop(59, 39, "campfire", 0.7)

# =====================================================================
#  5. THE WOOD
#
#  Stands, not scatter. Each stand has a centre it is thickest at and an edge it
#  thins to, so the wood has a border instead of a boundary, and no tree ever
#  stands in the road.
# =====================================================================
STANDS = [
    # (x0, y0, x1, y1, density, mix)  — mix is (pine, broadleaf, dead, cherry)
    # The Whispering Wood coming down over the north edge: conifer, and thick.
    (0, 0, 34, 14, 0.34, (7, 2, 1, 0)),
    # The west bank, across the river from the village — older, broadleaf.
    (0, 8, 15, 30, 0.30, (2, 6, 1, 0)),
    # The southern carr, wet ground below the village, alder and dead standing.
    (0, 36, 14, 56, 0.28, (2, 4, 3, 0)),
    # The south-east wood, up against the rock spur.
    (52, 46, 72, 56, 0.30, (5, 4, 1, 0)),
    # The hanger on the foothill slope, thinning as the ground rises.
    (44, 8, 62, 18, 0.18, (6, 2, 2, 0)),
    # Orchard behind the inn — planted, so it is sparse, regular and cherry.
    (44, 21, 50, 25, 0.30, (0, 0, 0, 8)),
]

# Clearings punched back out: the wood has to have holes or it is a wall.
CLEARINGS = [
    (26, 8, 36, 16),      # the shrine and the graves stand in the open
    (25, 10, 33, 16),     # the woodcutter's yard is a clearing he made
    (2, 20, 12, 26),      # a glade on the west bank — the Forest Hollow
]

# Undergrowth follows the trees but reaches further, so the wood has a fringe.
UNDER = [
    (0, 0, 36, 17, 0.16, ("bush", "bush", "flowers", "rock")),
    (0, 6, 17, 32, 0.14, ("bush", "flowers", "flowers", "log")),
    (0, 34, 16, 56, 0.15, ("bush", "bush", "log", "rock")),
    (50, 44, 72, 56, 0.14, ("bush", "rock", "flowers", "log")),
    (42, 6, 64, 20, 0.10, ("rock", "rock", "bush", "orePile")),
    # The meadow either side of the roads out — grass, thistle, nothing tall.
    (44, 34, 66, 44, 0.06, ("flowers", "bush", "flowers", "flowers")),
    (16, 44, 44, 56, 0.07, ("flowers", "bush", "flowers", "log")),
]

TREES = []      # (ex, ey, kind)
COVER = []      # (ex, ey, kind)


def _hash(x, y, salt=0):
    h = (x * 374761393 + y * 668265263 + salt * 2246822519) & 0xFFFFFFFF
    h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def clear_of_works(x, y, pad=1):
    """Nothing plants itself in a road, a river, a building or their verge. The
    verge is the point: a tree touching the road is the single loudest tell that
    a map was filled rather than laid out."""
    for dx in range(-pad, pad + 1):
        for dy in range(-pad, pad + 1):
            if at(x + dx, y + dy) in ",tc=~B#g": return False
    return True


def in_clearing(x, y):
    return any(x0 <= x <= x1 and y0 <= y <= y1 for x0, y0, x1, y1 in CLEARINGS)


def edge_falloff(x, y, x0, y0, x1, y1):
    """1 at the stand's heart, 0 at its border, so a wood thins out instead of
    stopping at a line."""
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
            if in_clearing(x, y) and _hash(x, y, 5) > 0.25: continue
            if _hash(x, y, 3) < dens:
                COVER.append((x, y, kinds[int(_hash(x, y, 4) * len(kinds)) % len(kinds)]))

# Hedges: the field boundary, the pen, and the lane behind the cottages.
hedge(63, 45, 63, 53)
hedge(48, 34, 57, 34)
hedge(30, 42, 45, 42)
# A hedgerow along the field boundary: planted in a line, because a hedge is.
for hy in range(45, 54):
    if at(63, hy) == ".": COVER.append((63, hy, "bush"))
for hx in range(46, 62, 2):
    if at(hx, 44) == ".": COVER.append((hx, 44, "bush"))

# =====================================================================
#  6. WHO IS HERE, AND WHAT THERE IS TO FIND
# =====================================================================
SPAWN = (11, 30)          # on the road, inside the gate, facing the bridge

NPCS = [
    ("guard", 8, 31),             # the gate, watching the coast road
    ("seoshe_guard", 5, 29), ("seoshe_guard", 5, 32),
    ("smith", 26, 28),            # at his anvil
    ("wagoner", 44, 25),          # loading in the inn yard
    ("elder", 38, 33),            # on the square, where the elder always is
    ("alchemist", 45, 28),        # the provisioner, outside her own door
    ("wife", 32, 28),             # on her doorstep, on the street
    ("boy", 36, 32), ("girl", 39, 32),      # children play in the square
    ("woodcutter", 29, 15),       # at the yard, on the wood's edge
    ("fisher", 19, 34),           # at the landing below the bridge
    ("farmwife", 50, 45),         # out at the strips
    ("hunter", 33, 14),           # come down the mine road, stopped at the shrine
    ("scholar", 41, 18),          # at the ford, copying the boundary marker
]

# Where the creatures may not go. Not rectangles drawn around content, but the
# places people actually keep clear: the village, the gate, the fields, the yard.
SAFE = [
    (2, 26, 23, 35),      # the gate and the west approach
    (22, 20, 54, 43),     # the village, the mill, the lane
    (44, 42, 64, 54),     # the fields
    (25, 6, 40, 20),      # the shrine, the graves, the ford, the wood yard
]

DISCOVERIES = [
    ("Ashwold", 38, 31, 4.0, "RegionDiscovered"),
    ("The Gate of Seoshe", 7, 30, 3.0, "RegionDiscovered"),
    ("The Ashwold Bridge", 19, 30, 3.0, "RegionDiscovered"),
    ("The Mill", 26, 22, 3.0, "RegionDiscovered"),
    ("The Shrine of the Light", 33, 11, 3.2, "RegionDiscovered"),
    ("The Ford", 37, 18, 3.0, "RegionDiscovered"),
    ("The Forest Hollow", 7, 23, 3.0, "SecretArea"),
    ("The Boundary Stone", 60, 47, 3.0, "SecretArea"),
]

CONTAINERS = [
    # In the village, containers belong to somebody, so they are few and they
    # are where that person would keep them.
    ("The smith's stock", 27, 29, "crate"),
    ("A slack tub", 23, 30, "barrel"),
    ("The inn's cellar hatch", 44, 24, "chest_wood"),
    ("The carrier's load", 45, 25, "barrel"),
    ("The provisioner's crate", 48, 27, "crate"),
    ("The miller's store", 28, 22, "crate"),
    ("A fisher's creel", 20, 37, "crate"),
    ("The barn's corner", 55, 44, "crate"),
    # Out of the village, they belong to nobody, which is the whole difference.
    ("A hollow log", 6, 23, "chest_wood"),            # in the forest hollow
    ("Under the boundary stone", 61, 48, "chest_iron"),
    ("A washed-up crate", 15, 45, "barrel"),          # downstream of the ford
    ("The abandoned wagon", 56, 34, "cart"),
]

EXAMINABLES = [
    ("the gate of Ashwold", 8, 28, "Read", "plaque",
     "A board nailed to the gatepost, the paint gone grey. \"ASHWOLD. Bridge toll "
     "one copper the cart, free the foot. Delvers: the road up is the north road. "
     "The Rest has beds. Pay the smith before he starts, not after.\""),
    ("the notice post", 40, 30, "Read", "note",
     "Bills nailed one over another. The top one is fresh: \"WANTED, word of the "
     "party of four that went up on the feast day and has not come down. Their "
     "gear was carried on a grey mule. Any word to the Rest.\" Beneath it, older "
     "and softer with rain, is another notice in the same hand. And beneath that, "
     "another."),
    ("the well", 38, 31, "Examine", "note",
     "The village well, the rope worn into a groove on the stone lip. Somebody "
     "has scratched a tally into the coping — five short strokes, then five, then "
     "five, and one. Nobody in Ashwold will tell you what is being counted."),
    ("the shrine", 33, 11, "Read", "plaque",
     "A pillar of pale stone with the Holy Light's mark cut into it, and the cut "
     "kept clean of moss by hands that come often. Offerings at its foot: a "
     "child's carved bird, a pick head, three copper coins, and a woman's ring "
     "with the stone gone. \"GO UP IN THE LIGHT. COME DOWN IN IT.\""),
    ("the graves", 30, 9, "Examine", "note",
     "Seven stones, and only two of them old. The new ones are not weathered "
     "enough to read from a distance, so you have to stand close. Every one of "
     "them gives the same year. Three give the same month."),
    ("the ford", 37, 18, "Examine", "note",
     "Flat stones set into the brook, worn smooth in a line — and worn most in "
     "the middle, where the mules go. On the far bank the mud holds prints going "
     "north. You count them out of habit. Far more go up than come back."),
    ("the mill", 27, 23, "Examine", "note",
     "The wheel turns slow and the leat runs clean, which means somebody clears "
     "it. Sacks are stacked under the eave and tallied in chalk on the door: "
     "flour for the Rest, flour for the camp, flour for the mine. The mine's "
     "column has not been added to in three weeks."),
    ("the boundary stone", 60, 47, "Read", "plaque",
     "A stone older than the village, half sunk and furred with lichen. The "
     "letters on it are not the ones anyone here uses now, and the one word that "
     "can still be made out is a name — Ychellen. The fields stop at it. Nobody "
     "has ever ploughed past it, and nobody can say why not."),
    ("the broken wagon", 56, 34, "Examine", "note",
     "A carrier's wagon with a wheel off and the axle propped on a stone. The "
     "load has been taken, but not hurriedly: the ropes are coiled, not cut. "
     "Whoever it was walked on east, and meant to come back for it."),
]

# =====================================================================
#  6b. VALIDATE
#
#  Every placement is checked against the ground it stands on. A barrel in the
#  river and a villager inside a wall are the two mistakes that cost a map its
#  credibility instantly, and they are both trivially detectable — so they are
#  detected, moved to the nearest ground somebody could actually stand on, and
#  reported, rather than left for a screenshot to find.
# =====================================================================
WALKABLE = ".,tc=g"
_moved = []


def ground(x, y):
    """The nearest cell a person could stand on, searched outward."""
    if at(x, y) in WALKABLE: return x, y
    for r in range(1, 7):
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
            _moved.append(f"{label} {it}: {(it[xi], it[yi])} -> {(nx, ny)} (was {at(it[xi], it[yi])!r})")
            it[xi], it[yi] = nx, ny
        out.append(tuple(it))
    return out


PROPS[:] = validated(PROPS, "prop")
NPCS[:] = validated(NPCS, "npc", 1, 2)
EXAMINABLES[:] = validated(EXAMINABLES, "examine", 1, 2)
CONTAINERS[:] = validated(CONTAINERS, "container", 1, 2)
DISCOVERIES[:] = validated(DISCOVERIES, "discovery", 1, 2)
if at(*SPAWN) not in WALKABLE:
    SPAWN = ground(*SPAWN)
    _moved.append(f"SPAWN -> {SPAWN}")

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


# The terrain is upsampled to the 16px grid and autotiled THERE, so edges are
# computed against 16px neighbours and read as fringes rather than 32px blocks.
M = [[at(mx // K.SUB, my // K.SUB) for my in range(MH)] for mx in range(MW)]


def m(mx, my):
    if mx < 0 or my < 0 or mx >= MW or my >= MH: return "#"
    return M[mx][my]


for my in range(MH):
    for mx in range(MW):
        c = m(mx, my)
        # Grass under everything, so erasing anything above reveals a finished
        # field rather than a hole.
        dx, dy = K.vary(K.SOLID, mx, my)
        paint("Ground", mx, my, K.gid("Floors", K.FLOOR_BLOCK["grass"] + dx, dy))

        if c == "t":
            # A track is worn, not laid: the same dirt, but thin, and it keeps
            # the grass showing through at its edges.
            k = lambda xx, yy: m(xx, yy) in "t,=c"
            paint("GroundDetails", mx, my, K.floor_tile("dirt", mx, my,
                  k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))
        elif c == ",":
            k = lambda xx, yy: m(xx, yy) in ",=ct"
            paint("Roads", mx, my, K.floor_tile("dirt", mx, my,
                  k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))
        elif c == "c":
            # The paving bleeds into the streets that meet it, so the square has
            # an edge that was laid rather than one that was cut.
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
            # The ground a house stands on is trodden bare, not lawn.
            dx, dy = K.vary(K.SOLID, mx, my)
            paint("GroundDetails", mx, my, K.gid("Floors", K.FLOOR_BLOCK["dirt"] + dx, dy))
        elif c == "g":
            # Worked ground — a garden, a strip, a trodden pen. Walkable, but
            # dug, and autotiled so it has an edge where the grass takes over.
            k = lambda xx, yy: m(xx, yy) in "gB,c"
            paint("GroundDetails", mx, my, K.floor_tile("dirt", mx, my,
                  k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))

# The shoreline, on its own layer — it is drawn on the LAND cell, and Water is
# what the engine reads for "you may not walk here".
for my in range(MH):
    for mx in range(MW):
        if m(mx, my) in "~#": continue
        wn, ws = m(mx, my - 1) == "~", m(mx, my + 1) == "~"
        we, ww = m(mx + 1, my) == "~", m(mx - 1, my) == "~"
        if wn or ws or we or ww:
            paint("Shore", mx, my, K.shore_tile(mx, my, wn, we, ws, ww))

# ---- the buildings ----
for (ex, ey, mat, roof, bays, storeys, name, door) in BUILDINGS:
    walls, roofs, (bw, bh) = K.house_tiles(mat, roof, bays, storeys)
    # The facade's bottom sits on the cell's bottom edge; its top-left in map
    # tiles is therefore one facade-height up from there.
    ox = ex * K.SUB - 1
    oy = (ey + 1) * K.SUB - K.FACADE_H * storeys
    for dx, dy, s, c, r in roofs:
        if K.opaque(s, c, r): paint("Buildings", ox + dx, oy + dy, K.gid(s, c, r))
    for dx, dy, s, c, r in walls:
        if K.opaque(s, c, r): paint("Walls", ox + dx, oy + dy, K.gid(s, c, r))
    # A door in the middle of the ground floor, and the step in front of it.
    dxm = ox + bw // 2 - 1 + (door - 1) * 2
    dym = oy + K.FACADE_H * storeys - 2
    for i, (c, r) in enumerate(((6, 1), (7, 1), (6, 2), (7, 2), (6, 3), (7, 3))):
        paint("Buildings", dxm + (i % 2), dym + (i // 2) - 1, K.gid("BuildProps", c, r))

# ---- the ground itself. Flat green is the single loudest thing separating this
#      from the art it is trying to look like: real ground has sprigs, fern and
#      reed in it. Sparse, unplanned, and under everything, so it reads as
#      texture rather than as objects somebody placed.
_tufts = 0
for _tx in range(COLS):
    for _ty in range(ROWS):
        if at(_tx, _ty) != ".": continue
        h = _hash(_tx, _ty, 21)
        if h > 0.16: continue
        mx_, my_ = _tx * K.SUB + (1 if _hash(_tx, _ty, 22) > 0.5 else 0), _ty * K.SUB + 1
        # Reed and fern only where the ground is damp — beside the water, in the
        # carr — and leaf sprigs everywhere else.
        near_water = any(at(_tx + dx, _ty + dy) == "~"
                         for dx in range(-2, 3) for dy in range(-2, 3))
        pool = K.CLUMPS if (near_water and h < 0.09) else K.TUFTS
        c, r = K.vary(pool, _tx, _ty)
        paint("GroundDetails", mx_, my_, K.gid("Vegetation", c, r))
        _tufts += 1

# ---- the trees, dealt across several layers so that overlapping crowns keep
#      each other whole (see region_kit.plant) ----
_per_layer = K.plant(TREES, paint, lambda ex, ey: int(_hash(ex, ey, 7) * 997))

for (ex, ey, kind) in COVER:
    setname, opts = K.GROUND_COVER[kind]
    c, r = K.vary(opts, ex, ey)
    paint("Objects", ex * K.SUB + (1 if _hash(ex, ey, 8) > 0.5 else 0),
          ey * K.SUB + 1, K.gid(setname, c, r))

# =====================================================================
#  8. WRITE IT
# =====================================================================
_oid = [1]


def nid():
    _oid[0] += 1
    return _oid[0] - 1


def obj(name, ex, ey, pairs, w=0, h=0):
    """Objects are written in MAP pixels, which is engine cells x 16."""
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
# The trees and undergrowth the map itself draws are still props to the engine,
# because they are what carries the collision — but their sprite is suppressed.
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
og["Warp"] = [obj("the gate of Seoshe", 6.5, 30.5,
                  [("DestinationMap", "seoshe"), ("DestinationSpawn", "0.0,0.0"),
                   ("Label", "the gate of Seoshe"), ("Verb", "Enter"), ("Radius", 42)])]
og["Interaction"] = [obj(t, x + 0.5, y + 0.5,
                         [("InteractionType", "Examine"), ("Verb", v), ("ReadKind", k),
                          ("Radius", 46), ("Pages", p)])
                     for t, x, y, v, k, p in EXAMINABLES]
og["TreasureSpawn"] = [obj(n, x + 0.5, y + 0.5, [("ContainerKind", k)])
                       for n, x, y, k in CONTAINERS]
og["Discovery"] = [obj(n, x + 0.5, y + 0.5, [("Radius", round(r * K.ENGINE_TILE, 1)),
                                             ("XpSource", s)])
                   for n, x, y, r, s in DISCOVERIES]
og["SafeZone"] = [obj(f"safe{i}", x0, y0, [], x1 - x0, y1 - y0)
                  for i, (x0, y0, x1, y1) in enumerate(SAFE)]

lid, parts = 300, []
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
<!-- REGION 1 — ASHWOLD. Hand-authored; see tools/build-r1-ashwold.py for why
     each thing is where it is. 16x16 grid; one engine cell is a 2x2 block, so
     the map is {MW}x{MH} for a region of {COLS}x{ROWS} cells.
     Terrain by layer precedence: Cliffs > Walls > Water > Bridges > Roads >
     Ground. Shore and CliffFace are decoration and never block. Walls holds
     building FACADES — the wall you see is the wall you cannot pass — and
     Buildings holds the roofs above them, which block nothing so the eaves can
     overhang. NPCs and heroes stay on our own atlases as NPCSpawn objects. -->
<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down"
     width="{MW}" height="{MH}" tilewidth="{K.T}" tileheight="{K.T}" infinite="0"
     nextlayerid="{lid+1}" nextobjectid="{_oid[0]}">
 <properties>
  <property name="Region" value="{REGION_ID}"/>
  <property name="DisplayName" value="{sx.escape(DISPLAY)}"/>
  <property name="Stage" type="int" value="1"/>
  <property name="EngineTile" type="int" value="{K.ENGINE_TILE}"/>
  <property name="NorthMap" value="r2_whispering_wood"/>
  <property name="EastMap" value="r4_kae_ychel_road"/>
 </properties>
{chr(10).join(K.TSREFS)}
{"".join(parts)}</map>
'''
path_out = os.path.join(OUT, "R1_Ashwold.tmx")
open(path_out, "w").write(tmx)

solid = sum(1 for x in range(COLS) for y in range(ROWS) if G[x][y] in "#B")
road = sum(1 for x in range(COLS) for y in range(ROWS) if G[x][y] in ",tc=")
water = sum(1 for x in range(COLS) for y in range(ROWS) if G[x][y] == "~")
print(f"wrote {path_out}  ({MW}x{MH} tiles = {COLS}x{ROWS} cells, "
      f"{os.path.getsize(path_out)/1024:.0f} KB)")
print(f"  buildings {len(BUILDINGS)}   npcs {len(NPCS)}   placed props {len(PROPS)}")
print(f"  trees {len(TREES)} across {len(K.TREE_LAYERS)} layers {_per_layer}   "
      f"undergrowth {len(COVER)}   ground tufts {_tufts}")
print(f"  examinables {len(EXAMINABLES)}   containers {len(CONTAINERS)}   "
      f"discoveries {len(DISCOVERIES)}")
if _moved:
    print(f"  MOVED {len(_moved)} placements off unwalkable ground:")
    for line in _moved[:12]: print("    " + line)
print(f"  land: road {100*road/(COLS*ROWS):.1f}%  water {100*water/(COLS*ROWS):.1f}%  "
      f"solid {100*solid/(COLS*ROWS):.1f}%")
