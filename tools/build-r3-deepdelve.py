#!/usr/bin/env python3
"""REGION 3 — DEEPDELVE AND THE RIVEN PASS. The end of the road.

    python3 tools/build-r3-deepdelve.py  ->  Maps/Regions/R3_Deepdelve.tmx

WHY IT IS HERE
    This is what the whole world has been pointing at. Ashwold feeds the delvers,
    the wood is what they walk through, and this is where they go in. It carries
    the CAVE MOUTH — the Cave Beyond Time — and until it existed the new Stage 1
    had nowhere to descend, which is the only reason the old map was still the
    one being played.

WHAT THE CONTRACT SAYS
    The wood leaves its east edge almost entirely walled: rock on rows 0-7, rock
    again from row 20 to the bottom, and a single band of open ground between,
    with the road on rows 12-13. So the way in is a cleft, and the region opens
    from it. That was not a design choice made here; it is what the neighbour
    already committed to, read from tools/refs/regions/r2_whispering_wood.json.

THE SHAPE OF IT
    Rock is the ground here and everything else is carved out of it. The road
    comes through the Riven Pass, drops into the bowl where the pit stands, and
    ends. Above the bowl, reached by a switchback that doubles back on itself so
    the last climb is slow, is the mouth. Meltwater collects in a tarn on the
    bowl's floor because water in a place like this has nowhere else to go, and
    the pit pumps from it.

WHAT THE PLACE SAYS
    Deepdelve is not abandoned and it is not working. The gear is greased and
    stopped. There is ore still on a cart that was loaded on the feast day. The
    captain keeps a board with two columns, and the second one has gone quiet.
    Nobody has taken the headgear down, because taking it down would be saying
    something out loud.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import region_kit as K
import xml.sax.saxutils as sx

OUT = os.path.join(K.MAPS, "Regions")
os.makedirs(OUT, exist_ok=True)

COLS, ROWS = 72, 56
MW, MH = COLS * K.SUB, ROWS * K.SUB

REGION_ID = "r3_deepdelve"
DISPLAY = "Deepdelve"
WEST_NEIGHBOUR = "r2_whispering_wood"

#   '.' grass  ',' road  't' track  'c' cobble  '~' water  '=' bridge
#   '#' rock   'g' worked ground    'B' building
G = [["#" for _ in range(ROWS)] for _ in range(COLS)]      # rock, until carved


def put(x, y, ch):
    if 0 <= x < COLS and 0 <= y < ROWS: G[x][y] = ch


def at(x, y):
    if x < 0 or y < 0 or x >= COLS or y >= ROWS: return "#"
    return G[x][y]


def rect(x0, y0, x1, y1, ch):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            put(x, y, ch)


def blob(cx, cy, rx, ry, ch, rough=0.0):
    """An opened space with an irregular rim, because nothing underground and
    nothing quarried has a straight edge."""
    for x in range(int(cx - rx - 2), int(cx + rx + 3)):
        for y in range(int(cy - ry - 2), int(cy + ry + 3)):
            d = ((x - cx) / max(1e-6, rx)) ** 2 + ((y - cy) / max(1e-6, ry)) ** 2
            wob = 1 + rough * math.sin(x * 0.7 + y * 0.4) * math.cos(x * 0.3 - y * 0.6)
            if d <= wob: put(x, y, ch)


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
#  0. THE CONTRACT WITH THE WOOD
# =====================================================================
_west = K.load_edges(WEST_NEIGHBOUR)
if _west is None:
    raise SystemExit("build r2 first: this region's west edge is read from its east edge")
SEAM = _west["east"]
ROAD_IN = K.runs(SEAM, ",t=")[0]          # rows 12-13
OPEN_IN = [r for r in K.runs(SEAM, ".") if r[1] - r[0] > 0]
print(f"seam with {WEST_NEIGHBOUR}: road rows {ROAD_IN}  open rows {OPEN_IN}")

ROAD_Y = ROAD_IN[1]                       # width 2 -> [ROAD_IN[0], ROAD_IN[1]]

# The west edge is laid down exactly as the neighbour leaves it, cell for cell.
# Nothing is inferred: the wood already decided what is rock here and what is not.
for y, ch in enumerate(SEAM):
    put(0, y, "." if ch == "." else ch)
for y, ch in enumerate(SEAM):
    if ch in ",t=": put(0, y, ",")

# =====================================================================
#  1. CARVING THE ROCK
#
#  Everything starts solid. The pass, the bowl and the shelf are the only places
#  anything opened up — two of them by water and time, one of them by men.
# =====================================================================

# The Riven Pass: a cleft running east off the seam, barely wider than a cart.
rect(0, ROAD_IN[0] - 1, 14, ROAD_IN[1] + 2, ".")
for x in range(0, 15):                        # it narrows in the middle and opens again
    squeeze = 2 if 5 <= x <= 9 else 0
    for y in range(ROAD_IN[0] - 1, ROAD_IN[1] + 3):
        if squeeze and not (ROAD_IN[0] <= y <= ROAD_IN[1]): put(x, y, "#")

# The bowl: where the pass lets out. Meltwater collects at its floor.
blob(34, 26, 20, 13, ".", rough=0.22)
blob(30, 40, 12, 8, ".", rough=0.25)          # the lower shelf, running south

# The shelf the mouth is on, above the bowl to the north-east.
blob(53, 12, 11, 6, ".", rough=0.2)

# The tarn. Meltwater with nowhere to go; the pit pumps off it, which is the only
# reason anyone ever dug here rather than a mile further along the range.
blob(24, 33, 7, 4, "~", rough=0.18)

# ---- RIDGES AND GULLIES ----------------------------------------------------
#  A mountain that is one unbroken fill of rock reads as a brown slab, because
#  the autotiler only ever puts a rim where the rock MEETS something else. Two
#  thirds of this region is rock, so it needs edges on the inside too.
#
#  Sealed HOLLOWS, not lines. The first attempt cut long straight clefts and the
#  result was worse than the slab it was fixing: a dozen parallel diagonal
#  scratches, which reads as machinery far more loudly than flat brown does.
#  Rock breaks in lumps, so these are lumps — irregular, of several sizes, at no
#  particular spacing, and none of them reachable. They exist so the autotiler
#  has an edge to draw inside the mass, and for nothing else.
_HOLLOWS = [(62, 5, 4, 3), (67, 24, 3, 5), (61, 38, 5, 4), (50, 48, 4, 3),
            (13, 47, 5, 4), (4, 30, 3, 4), (7, 4, 4, 2), (23, 5, 3, 2),
            (39, 4, 5, 3), (56, 50, 3, 3), (19, 51, 4, 2), (68, 49, 2, 3),
            (30, 6, 2, 2), (48, 3, 2, 2), (58, 15, 3, 2), (66, 33, 2, 4),
            (44, 50, 2, 2), (9, 22, 2, 3), (2, 46, 3, 2), (35, 50, 3, 2)]
for _hx, _hy, _hrx, _hry in _HOLLOWS:
    for _x in range(_hx - _hrx - 1, _hx + _hrx + 2):
        for _y in range(_hy - _hry - 1, _hy + _hry + 2):
            d = ((_x - _hx) / max(1e-6, _hrx)) ** 2 + ((_y - _hy) / max(1e-6, _hry)) ** 2
            wob = 1 + 0.4 * math.sin(_x * 1.3 + _y * 0.9) * math.cos(_x * 0.5 - _y * 1.1)
            # Never within two cells of a border. An edge is a CONTRACT with the
            # neighbour, and one hollow that reached column 0 rewrote three rows
            # of it — caught by the seam check at the bottom of this file, which
            # is the entire reason that check exists.
            if _x < 2 or _y < 2 or _x >= COLS - 2 or _y >= ROWS - 2: continue
            if d <= wob and at(_x, _y) == "#": put(_x, _y, "q")

# =====================================================================
#  2. THE WAYS
# =====================================================================
# The road: through the pass and down into the bowl, where it stops. There is no
# road past a pit head; there is only the climb.
path([(-2, ROAD_Y), (8, ROAD_Y), (16, 16), (22, 22), (30, 26), (38, 27)], 2, ",", over=".#")

# The switchback to the mouth. It doubles back twice, because the last hundred
# feet of that climb is the part nobody hurries.
path([(38, 27), (44, 26), (46, 22), (41, 20), (45, 16), (51, 14), (54, 13)], 2, "t", over=".#")

# The pit tracks: rails from the adit down to the sorting floor and the tips.
path([(40, 24), (36, 25), (32, 27)], 2, "t", over=".")
path([(32, 27), (28, 30), (26, 34)], 2, "t", over=".")     # down to the pump
path([(34, 28), (32, 36), (30, 42)], 2, "t", over=".")     # the tips, and the way south

# South out of the region, toward the Kae Ychel road. A track, not a road: this
# is the miners' way round, and R4 will pick it up at the same column.
path([(30, 42), (31, 50), (31, 58)], 2, "t", over=".#")

# The floor of a working pit is stone and spoil. Only the sheltered southern
# fall keeps its grass, which is why that is the only place a tree stands.
for _x in range(COLS):
    for _y in range(ROWS):
        if at(_x, _y) == "." and not (14 <= _x <= 40 and _y >= 34):
            put(_x, _y, "q")

# =====================================================================
#  3. THE PIT
# =====================================================================
BUILDINGS = []


def building(ex, ey, material, roof, name, bays=1, storeys=1):
    BUILDINGS.append((ex, ey, material, roof, bays, storeys, name))
    for x in range(ex, ex + 3 * bays):
        for y in range(ey - 1, ey + 1):
            put(x, y, "B")


# The sorting floor: trodden, black with spoil, and the only flat ground here
# that men made rather than found.
for _x in range(30, 42):
    for _y in range(24, 31):
        if at(_x, _y) == "." and (_x + _y) % 4: put(_x, _y, "g")

building(33, 31, "board", "shingle", "the pit office", bays=2)   # the captain's board
building(38, 31, "board", "shingle", "the winch house")
building(28, 24, "log", "shingle", "the miners' bunkhouse", bays=2)
building(26, 37, "board", "shingle", "the pump house")

# =====================================================================
#  4. THE PROPS
# =====================================================================
PROPS = []


def prop(ex, ey, kind, scale=1.0, solid=False, radius=0.0):
    PROPS.append((ex, ey, kind, scale, solid, radius))


# ---- the pass: a marker where the wood ends and the rock begins, and the
#      lamps somebody still lights along it.
prop(3, ROAD_Y - 2, "signpost", 0.75, True, 10)
prop(11, ROAD_Y + 2, "lantern", 0.7); prop(4, ROAD_Y + 2, "lantern", 0.7)
prop(13, ROAD_Y - 2, "rock", 1.0, True, 15); prop(6, ROAD_Y + 3, "rock", 0.9, True, 14)

# ---- the pit head. The gear is the landmark: you see it from the pass, and it
#      is not turning.
prop(40, 23, "scaffold", 1.3, True, 20)         # the headgear over the adit
prop(41, 25, "caveEntrance", 0.9, False, 0)     # the adit itself
prop(38, 24, "mineCart", 0.85, True, 17)        # still loaded, since the feast day
prop(36, 26, "mineCart", 0.8, True, 16)
prop(34, 24, "orePile", 0.95, True, 15); prop(32, 25, "orePile", 0.9, True, 14)
prop(30, 28, "orePile", 0.85, True, 14)
prop(37, 29, "toolRack", 0.75, True, 12)
prop(35, 29, "workbench", 0.8, True, 16)
prop(39, 28, "barrel", 0.65, True, 11); prop(40, 29, "crate", 0.65, True, 12)
prop(33, 23, "lantern", 0.7); prop(39, 22, "lantern", 0.7)
prop(31, 31, "campfire", 0.9)                   # the only fire still lit up here
prop(29, 32, "tent", 0.8, True, 15)
prop(42, 28, "crystal", 0.8)                    # what came up, and why they dug on

# ---- the tips: spoil thrown down the slope, year on year.
for _tx, _ty in ((33, 36), (34, 38), (31, 37), (35, 40), (32, 41)):
    prop(_tx, _ty, "orePile", 0.9, True, 14)
prop(30, 44, "h_cart", 0.8, True, 15)
prop(34, 43, "log", 0.8)

# ---- the tarn and the pump: a leat off the water, and the beam engine's house.
prop(27, 35, "waterfall", 0.75)                 # the leat, falling to the wheel
prop(25, 38, "barrel", 0.65, True, 11)
prop(28, 39, "h_bucket", 0.7)
prop(22, 30, "rock", 0.95, True, 15); prop(20, 36, "rock", 0.9, True, 14)

# ---- the switchback, and the offerings that accumulate on it. People leave
#      things at every turn, and the turns are where they stop to breathe.
prop(45, 25, "statue", 0.85, True, 15)
prop(44, 21, "gravestone", 0.75); prop(46, 17, "gravestone", 0.75)
prop(43, 19, "h_flowers", 0.8); prop(48, 15, "h_flowers", 0.8)
prop(47, 23, "lantern", 0.7); prop(49, 17, "lantern", 0.7)

# ---- THE MOUTH. Everything here is arranged around it, and it is the only
#      thing on the shelf.
prop(54, 12, "caveEntrance", 1.35, False, 0)
prop(51, 12, "support", 0.9, True, 14); prop(57, 12, "support", 0.9, True, 14)
prop(52, 10, "banner", 0.75); prop(56, 10, "banner", 0.75)
prop(53, 14, "campfire", 0.85)                  # the fire the last watch keeps
prop(56, 14, "crate", 0.65, True, 12); prop(57, 15, "barrel", 0.65, True, 11)
prop(50, 14, "tent", 0.85, True, 16)            # Yeska's, eleven days at the mouth
prop(58, 13, "gravestone", 0.75)
prop(55, 15, "h_flowers", 0.8)
prop(52, 15, "toolRack", 0.7, True, 12)

# ---- a few last stones on the bowl floor, where the roof of something fell in.
for _rx, _ry in ((19, 22), (24, 20), (44, 33), (40, 36), (17, 28), (46, 30)):
    prop(_rx, _ry, "rock", 0.9, True, 14)
for _cx, _cy in ((21, 26), (43, 31), (18, 32)):
    prop(_cx, _cy, "crystal", 0.7)

# =====================================================================
#  5. WHAT GROWS HERE
#
#  Almost nothing. A few pines have got a hold on the sheltered side of the bowl
#  and there is scrub on the tips; above the shelf there is nothing at all. The
#  absence is the point — you have walked out of the wood.
# =====================================================================
STANDS = [
    (14, 30, 30, 46, 0.16, (8, 1, 3, 0)),      # the sheltered south-west of the bowl
    (40, 32, 52, 46, 0.10, (7, 0, 4, 0)),      # thinner still, on the eastern fall
]
CLEARINGS = [(28, 22, 44, 32), (46, 8, 62, 18)]     # the pit floor and the shelf

UNDER = [
    (12, 20, 34, 48, 0.13, ("rock", "rock", "bush", "orePile")),
    (36, 28, 56, 48, 0.10, ("rock", "orePile", "rock", "bush")),
    (44, 6, 64, 20, 0.08, ("rock", "rock", "orePile", "rock")),
]

TREES, COVER = [], []


def _hash(x, y, salt=0):
    h = (x * 374761393 + y * 668265263 + salt * 2246822519) & 0xFFFFFFFF
    h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def clear_of_works(x, y, pad=1):
    for dx in range(-pad, pad + 1):
        for dy in range(-pad, pad + 1):
            if at(x + dx, y + dy) in ",tc=~B#gq": return False
    return True


def in_clearing(x, y):
    return any(x0 <= x <= x1 and y0 <= y <= y1 for x0, y0, x1, y1 in CLEARINGS)


def edge_falloff(x, y, x0, y0, x1, y1):
    d = min(x - x0, x1 - x, y - y0, y1 - y)
    return max(0.0, min(1.0, d / 3.5))


for (x0, y0, x1, y1, dens, mix) in STANDS:
    kinds = ["pine"] * mix[0] + ["broadleaf"] * mix[1] + ["deadTree"] * mix[2]
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
            if _hash(x, y, 3) < dens:
                COVER.append((x, y, kinds[int(_hash(x, y, 4) * len(kinds)) % len(kinds)]))

# =====================================================================
#  6. WHO IS HERE
# =====================================================================
SPAWN = (3, ROAD_Y)
CAVE_MOUTH = (54, 13)          # what the whole world has been pointing at

NPCS = [
    ("cerno", 52, 14),             # at the mouth, as the book puts him
    ("stranded_delver", 50, 15),   # eleven days at it, and has not gone in
    ("pit_captain", 34, 30),       # at his board
    ("winch_hand", 39, 30),        # at a drum with nothing to wind
]

SAFE = [
    (0, ROAD_IN[0] - 2, 16, ROAD_IN[1] + 3),    # the pass
    (26, 20, 46, 34),                            # the pit floor
    (44, 8, 62, 18),                             # the shelf and the mouth
]

DISCOVERIES = [
    ("The Riven Pass", 8, ROAD_Y, 3.4, "RegionDiscovered"),
    ("Deepdelve", 35, 27, 4.0, "RegionDiscovered"),
    ("The Pit Head", 40, 24, 3.2, "RegionDiscovered"),
    ("The Black Tarn", 24, 33, 3.4, "RegionDiscovered"),
    ("The Switchback", 45, 21, 3.0, "RegionDiscovered"),
    ("The Mouth", 54, 13, 4.0, "RegionDiscovered"),
    ("The Tips", 33, 39, 3.0, "SecretArea"),
]

CONTAINERS = [
    ("A loaded cart", 38, 24, "cart"),
    ("The winch house store", 40, 29, "crate"),
    ("A miner's locker", 39, 28, "barrel"),
    ("The captain's chest", 35, 29, "chest_iron"),
    ("Spoil worth sorting", 32, 25, "ore"),
    ("A seam in the tips", 34, 38, "ore"),
    ("Left at the mouth", 56, 14, "crate"),
    ("Yeska's stock", 57, 15, "barrel"),
    ("Under the pump floor", 25, 38, "chest_wood"),
    ("Wedged in the pass", 13, 15, "chest_wood"),
]

EXAMINABLES = [
    ("the pass marker", 3, ROAD_Y - 2, "Read", "plaque",
     "A stone at the mouth of the cleft, and the wood behind you is the last of it. "
     "\"THE RIVEN PASS. DEEPDELVE BEYOND. NO CARTS AFTER DARK.\" Somebody has "
     "scratched out the last four words and written nothing in their place."),
    ("the captain's board", 34, 30, "Read", "note",
     "A board under the office eave, ruled into two columns in a careful hand. On "
     "the left, every party that has gone up to the mouth, with the date and the "
     "number in it. On the right, the same parties coming back down. The left "
     "column runs to the bottom of the board and continues on a second sheet "
     "nailed under it. The right column stops two thirds of the way."),
    ("the headgear", 40, 23, "Examine", "note",
     "The winding gear stands over the adit, greased and sound and perfectly "
     "still. The drum is full of rope. Nobody has taken it down, and nobody will: "
     "taking it down would be saying out loud what everybody here already knows."),
    ("the loaded cart", 38, 24, "Examine", "note",
     "A cart at the sorting floor with ore still in it, and a chalk mark on the "
     "side giving the date it was filled. It is the date of the feast. Rain has "
     "been on the chalk and the numbers have run, but they are still the numbers."),
    ("the tarn", 24, 33, "Examine", "note",
     "Meltwater with nowhere to go, black because it is deep and because nothing "
     "lives in it. The pump takes off the near end and has done for years. Along "
     "the far shore, where nobody walks, the stones are laid in a line too "
     "straight to have fallen that way."),
    ("the offerings", 45, 25, "Examine", "note",
     "The turn of the switchback, and the place where anyone climbing it stops to "
     "get their breath. That is why the offerings are here and not at the top: a "
     "lamp, a folded coat, a child's shoe with the lace still tied. People leave "
     "things where they stop, not where they mean to."),
    ("the mouth", 54, 13, "Read", "plaque",
     "A cave amongst the cliffs, and the cold coming out of it is not the cold of "
     "the mountain. The timbering at the entrance is old mine work and it stops "
     "eight feet in, because eight feet in the passage stops being anything men "
     "made. A board is wedged in the rock beside it. \"THE ACCOUNT OF THIS PLACE "
     "IS KEPT BELOW. WE DO NOT KEEP IT.\""),
    ("the last watch", 53, 14, "Examine", "note",
     "A fire, banked and fed, on the shelf outside the mouth. Somebody has kept it "
     "going for a long while — the ash under it is a foot deep. It is not there to "
     "cook on and it is not there for warmth. It is there so that anything coming "
     "out has to come out past a light."),
    ("the bunkhouse", 29, 24, "Examine", "note",
     "Eighteen bunks, and the blankets on eleven of them are folded the way a man "
     "folds a blanket when he is coming back to it. Nobody has moved them. The "
     "cook still lays eighteen places, which the captain has told her twice to "
     "stop doing."),
]

# =====================================================================
#  6b. VALIDATE
# =====================================================================
WALKABLE = ".,tc=gq"
_moved = []


def ground(x, y):
    if at(x, y) in WALKABLE: return x, y
    for r in range(1, 10):
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
if at(*CAVE_MOUTH) not in WALKABLE:
    CAVE_MOUTH = ground(*CAVE_MOUTH); _moved.append(f"CAVE MOUTH -> {CAVE_MOUTH}")

# =====================================================================
#  7. PAINT IT
# =====================================================================
LNAMES = (["Ground", "GroundDetails", "Roads", "Bridges", "Water", "Shore",
           "Cliffs", "CliffFace", "Buildings", "Walls", "Objects"]
          + K.TREE_LAYERS + ["AbovePlayer", "Shadows"])
OBJ_ORDER = ["PlayerSpawn", "NPCSpawn", "EnemySpawn", "Warp", "Interaction",
             "Props", "TreasureSpawn", "Discovery", "SafeZone", "CaveMouth", "MusicZone"]

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
        # The bed under everything is STONE here, not turf. Laying grass under
        # the whole region put a green fringe around every hollow and every
        # cliff, because that is what shows through the bite in an autotile —
        # and there is no grass under the rock at this height.
        near_green = 14 <= mx // K.SUB <= 40 and my // K.SUB >= 32
        bed = "grass" if near_green else "dirt"
        paint("Ground", mx, my, K.gid("Floors", K.FLOOR_BLOCK[bed] + dx, dy))

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
                # Rows 6-7 are the bare face. Row 8 is the one with grass at its
                # foot, and there is no grass at four thousand feet.
                paint("CliffFace", mx, my + 1, K.cliff_face(mx, my, 6))
                paint("CliffFace", mx, my + 2, K.cliff_face(mx, my, 7))
        elif c == "B":
            dx, dy = K.vary(K.SOLID, mx, my)
            paint("GroundDetails", mx, my, K.gid("Floors", K.FLOOR_BLOCK["dirt"] + dx, dy))
        elif c == "g":
            k = lambda xx, yy: m(xx, yy) in "gB,ct"
            paint("GroundDetails", mx, my, K.floor_tile("dirt", mx, my,
                  k(mx, my - 1), k(mx + 1, my), k(mx, my + 1), k(mx - 1, my)))
        elif c == "q":
            # Bare stone: the quarry floor, the shelf, and the sealed clefts in
            # the range. Walkable where the party can reach it and simply relief
            # where it cannot.
            k = lambda xx, yy: m(xx, yy) not in "#"
            paint("GroundDetails", mx, my, K.floor_tile("cobble", mx, my,
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

# Ground cover: sparse, and stone rather than green. Above the shelf, nothing.
_tufts = 0
for _tx in range(COLS):
    for _ty in range(ROWS):
        if at(_tx, _ty) != ".": continue   # tufts only where grass survives
        h = _hash(_tx, _ty, 21)
        if h > (0.13 if _ty > 18 else 0.05): continue
        mx_ = _tx * K.SUB + (1 if _hash(_tx, _ty, 22) > 0.5 else 0)
        near_water = any(at(_tx + dx, _ty + dy) == "~"
                         for dx in range(-2, 3) for dy in range(-2, 3))
        pool = K.CLUMPS if (near_water and h < 0.07) else K.TUFTS
        c_, r_ = K.vary(pool, _tx, _ty)
        paint("GroundDetails", mx_, _ty * K.SUB + 1, K.gid("Vegetation", c_, r_))
        _tufts += 1

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
# The whole point of the region. The engine reads this and it becomes both the
# way down and the stage's exit.
og["CaveMouth"] = [obj("The Mouth", CAVE_MOUTH[0] + 0.5, CAVE_MOUTH[1] + 0.5,
                       [("InteractionType", "Descend"), ("DestinationMap", "cave")])]
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

lid, parts = 500, []
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
<!-- REGION 3 — DEEPDELVE AND THE RIVEN PASS. Hand-authored; see
     tools/build-r3-deepdelve.py for why each thing is where it is.
     Its WEST edge is laid down cell for cell from the Whispering Wood's east
     edge: rock except one band of open ground, with the road on rows {ROAD_IN[0]}-{ROAD_IN[1]}.
     This region carries the CAVE MOUTH, which is what the rest of the world has
     been pointing at.
     16x16 grid; one engine cell is a 2x2 block, so the map is {MW}x{MH}. -->
<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down"
     width="{MW}" height="{MH}" tilewidth="{K.T}" tileheight="{K.T}" infinite="0"
     nextlayerid="{lid+1}" nextobjectid="{_oid[0]}">
 <properties>
  <property name="Region" value="{REGION_ID}"/>
  <property name="DisplayName" value="{sx.escape(DISPLAY)}"/>
  <property name="Stage" type="int" value="1"/>
  <property name="EngineTile" type="int" value="{K.ENGINE_TILE}"/>
  <property name="Wildlife" type="int" value="22"/>
  <property name="WestMap" value="{WEST_NEIGHBOUR}"/>
  <property name="SouthMap" value="r4_kae_ychel_road"/>
 </properties>
{chr(10).join(K.TSREFS)}
{"".join(parts)}</map>
'''
path_out = os.path.join(OUT, "R3_Deepdelve.tmx")
open(path_out, "w").write(tmx)

_edges = K.save_edges(REGION_ID, G, COLS, ROWS)
print(f"wrote {path_out}  ({MW}x{MH} tiles = {COLS}x{ROWS} cells, "
      f"{os.path.getsize(path_out)/1024:.0f} KB)")
print(f"  buildings {len(BUILDINGS)}   npcs {len(NPCS)}   placed props {len(PROPS)}")
print(f"  trees {len(TREES)} across {len(K.TREE_LAYERS)} layers {_per_layer}   "
      f"undergrowth {len(COVER)}   ground tufts {_tufts}")
print(f"  examinables {len(EXAMINABLES)}   containers {len(CONTAINERS)}   "
      f"discoveries {len(DISCOVERIES)}")
print(f"  CAVE MOUTH at {CAVE_MOUTH}")
open_cells = sum(1 for x in range(COLS) for y in range(ROWS) if G[x][y] in ".,tc=gB")
print(f"  carved out of the rock: {open_cells} cells "
      f"({100*open_cells/(COLS*ROWS):.0f}% — the rest is mountain)")
if _moved:
    print(f"  MOVED {len(_moved)} placements off unwalkable ground:")
    for line in _moved[:10]: print("    " + line)

mine, theirs = _edges["west"], SEAM
bad = [i for i in range(ROWS)
       if (mine[i] in ",t=") != (theirs[i] in ",t=") or (mine[i] == "#") != (theirs[i] == "#")]
print(f"  seam west vs {WEST_NEIGHBOUR}: "
      + ("MATCHES" if not bad else f"MISMATCH at rows {bad}"))
