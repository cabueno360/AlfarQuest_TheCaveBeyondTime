#!/usr/bin/env python3
"""REGION 4 — THE KAE YCHEL ROAD. The way east, and the way home.

    python3 tools/build-r4-kae-ychel-road.py  ->  Maps/Regions/R4_KaeYchelRoad.tmx

WHY IT IS HERE
    This closes the ring. The other three are the road TO the cave — village,
    wood, pit — and this is the country that runs the other way, east toward the
    sun-city of Kae Ychel. It is the optional wing: nothing here is on the
    critical path, so everything here is a reason to leave it. And because it
    adjoins Ashwold on the west, exploring it brings you back where you started
    rather than making you walk back.

    It is also the last place with no home. The Academy Outpost — a college of
    Kae Ychel, and the Mage's own tragedy — has been an interior with no world
    around it. It gets one here.

WHAT THE CONTRACTS SAY
    Two neighbours, so two edges are fixed, not chosen:
      * WEST is Ashwold's east edge: the coast-country road leaves the village at
        rows 31-33, the Ashbrook at rows 8-10, foothills at 0-7 and 38-47.
      * NORTH is Deepdelve's south edge: rock the whole way but for the miners'
        track down at columns 30-31.
    Both are read from tools/refs/regions/, laid down cell for cell, and CHECKED.

THE SHAPE OF IT
    The mountains that wall Deepdelve come down the north and north-west and
    peter out; the land opens south and east into dry, rolling country — the
    Sun King's own, hard and bright. The road crosses it west to east and runs
    off the far side toward Kae Ychel, nine days on. The Ashbrook comes in from
    the village and pools at an oasis, because the old road knew where to put
    water, and the caravan waters there. In the south the Sunken Colonnade — a
    Ychellen city the sun found before anyone here had a name for it — is the
    same old stone as the boundary marker in Ashwold's fields and the ruin in the
    wood's hollow. Three sightings of one dead people, for anyone joining them up.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import region_kit as K
import xml.sax.saxutils as sx

OUT = os.path.join(K.MAPS, "Regions")
os.makedirs(OUT, exist_ok=True)

COLS, ROWS = 72, 56
MW, MH = COLS * K.SUB, ROWS * K.SUB

REGION_ID = "r4_kae_ychel_road"
DISPLAY = "The Kae Ychel Road"
WEST_NEIGHBOUR = "r1_ashwold"
NORTH_NEIGHBOUR = "r3_deepdelve"

#   '.' grass  ',' road  't' track  'c' cobble  '~' water  '=' bridge
#   '#' rock   'g' worked ground  'q' bare stone  'B' building
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


def blob(cx, cy, rx, ry, ch, rough=0.0):
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
#  0. THE TWO CONTRACTS
# =====================================================================
_west = K.load_edges(WEST_NEIGHBOUR)
_north = K.load_edges(NORTH_NEIGHBOUR)
if _west is None or _north is None:
    raise SystemExit("build r1 and r3 first: this region's west and north edges are read from theirs")

WSEAM = _west["east"]           # Ashwold's east edge -> our west edge
NSEAM = _north["south"]         # Deepdelve's south edge -> our north edge
ROAD_IN = K.runs(WSEAM, ",t=")[0]       # rows 31-33
WATER_IN = K.runs(WSEAM, "~")[0]        # rows 8-10
TRACK_IN = K.runs(NSEAM, ",t=")[0]      # cols 30-31
print(f"west seam ({WEST_NEIGHBOUR}): road rows {ROAD_IN}  water rows {WATER_IN}")
print(f"north seam ({NORTH_NEIGHBOUR}): track cols {TRACK_IN}")

ROAD_Y = ROAD_IN[1]                     # width 2 line centred here -> [31,33]? see below
WATER_Y = WATER_IN[0] + 1               # width 3
TRACK_X = TRACK_IN[1]

# =====================================================================
#  1. THE LAND
# =====================================================================

# The mountains that wall Deepdelve come down our north and north-west and run
# out. The north edge is laid EXACTLY as Deepdelve leaves it — rock but for the
# miners' track — and the range thins as it comes south.
for x in range(COLS):
    if NSEAM[x] == "#":
        depth = 7 - int(0.08 * abs(x - 20))
        for y in range(0, max(1, depth)):
            put(x, y, "#")
# The north-west foothills, continuing Ashwold's corner (rows 0-7 rock on the
# west seam), falling away east.
for y in range(0, 8):
    if WSEAM[y] == "#":
        for x in range(0, 10 - y):
            put(x, y, "#")
# The rock spur that walled Ashwold's east road (its east edge is rock at rows
# 38-47) runs on into this map and thins as it goes east — the high ground the
# road skirts. It must reach the WEST edge exactly at those rows and nowhere
# else, because that edge is Ashwold's to define.
for y in range(38, 48):
    for x in range(0, 26 - abs(y - 43)):
        put(x, y, "#")
# A low bluff in the south, south of the ruins and clear of the west edge, so
# the Sunken Colonnade has a hillside above it without touching the seam.
for x in range(6, 40):
    top = 52 + int(1.5 * math.sin(x * 0.35))
    for y in range(top, ROWS):
        put(x, y, "#")

# The Ashbrook, in from the village at rows 8-10, running down to the oasis and
# then losing itself in the dry ground — a desert stream that does not reach the
# far side.
path([(-2, WATER_Y), (6, WATER_Y + 1), (12, 14), (16, 18), (20, 21)], 3, "~")
blob(22, 22, 6, 4, "~", rough=0.2)              # the oasis pool

# The west edge is Ashwold's to define. Lay it cell for cell, and lay a
# three-wide apron of matching ground just inside it, so whatever the road and
# the spur do further in, the seam itself is exactly the neighbour's edge.
for y, ch in enumerate(WSEAM):
    kind = {"#": "#", "~": "~"}.get(ch, "," if ch in ",t=" else ".")
    for x in range(0, 2):
        # only overwrite where we would otherwise contradict the contract
        if x == 0 or at(x, y) in ".#~":
            put(x, y, kind)

# =====================================================================
#  2. THE ROADS
# =====================================================================
# The Kae Ychel road: in from Ashwold at rows 31-33, east across the whole map
# and off the far side toward the sun-city. The spine of the region.
# The incoming road is laid to the WIDTH THE CONTRACT ASKS FOR, read from the
# neighbour's edge, rather than to a number written here — Ashwold's road has
# been narrowed once already, and a hard-wired 3 put a row of tarmac where the
# village leaves grass.
ROAD_W_IN = ROAD_IN[1] - ROAD_IN[0] + 1
ROAD_MID = ROAD_IN[0] + ROAD_W_IN // 2
path([(-2, ROAD_MID), (8, ROAD_MID)], ROAD_W_IN, ",", over=".#~")
path([(8, ROAD_MID), (22, 32), (34, 33), (48, 32), (60, 31), (74, 31)],
     2, ",", over=".#")

# The miners' track down from Deepdelve at columns 30-31, joining the road. This
# is the way round the mountains that R3 sends south.
path([(TRACK_X, -2), (TRACK_X, 8), (32, 18), (33, 26), (34, 33)], 2, "t", over=".#")

# The spur up to the Academy Outpost, north off the road onto its own terrace.
path([(50, 32), (52, 26), (54, 22)], 2, "t", over=".#")

# The caravan's loop off the road at the oasis, so wagons pull in and out
# without blocking the through road.
path([(26, 32), (24, 28), (22, 26)], 2, "t", over=".")
path([(22, 26), (28, 25), (30, 30)], 2, "t", over=".")

# The old processional way down into the Sunken Colonnade, half lost.
path([(20, 44), (16, 47), (12, 49)], 2, "t", over=".#")

# =====================================================================
#  3. THE ACADEMY OUTPOST, AND THE OTHER BUILDINGS
# =====================================================================
BUILDINGS = []


def building(ex, ey, material, roof, name, bays=1, storeys=1):
    BUILDINGS.append((ex, ey, material, roof, bays, storeys, name))
    for x in range(ex, ex + 3 * bays):
        for y in range(ey - 1, ey + 1):
            put(x, y, "B")


# The Academy Outpost: a college of Kae Ychel on its own terrace above the road.
# Plaster and tile, because it is the Academy's money out here, not a farmer's.
rect(48, 18, 60, 26, ".")
building(52, 22, "plaster", "tile", "the Academy Outpost", bays=2, storeys=2)  # the door in
building(48, 20, "plaster", "tile", "the apprentices' hall")
building(58, 21, "log", "shingle", "the outpost stable")

# The caravan rest at the oasis: a way-house and a store, where the road waters.
building(28, 30, "board", "shingle", "the way-house", bays=2)
building(33, 31, "log", "shingle", "the caravan store")

# =====================================================================
#  4. THE PROPS
# =====================================================================
PROPS = []


def prop(ex, ey, kind, scale=1.0, solid=False, radius=0.0):
    PROPS.append((ex, ey, kind, scale, solid, radius))


# ---- the road in from Ashwold: a marker where the coast-country ends and the
#      sun-road begins.
prop(4, ROAD_Y - 2, "signpost", 0.75, True, 10)          # ASHWOLD ← / KAE YCHEL →
prop(6, ROAD_Y + 2, "rock", 0.85, True, 13)

# ---- the oasis and the caravan. Water, and everything that stops for it.
prop(22, 25, "well", 0.85, True, 17)                     # the old road's own well
prop(24, 23, "cherry", 1.05); prop(20, 24, "cherry", 1.05); prop(25, 20, "cherry", 1.0)
prop(27, 28, "wagon", 0.9, True, 18); prop(31, 29, "wagon", 0.85, True, 17)
prop(29, 27, "tent", 0.85, True, 16); prop(25, 29, "tent", 0.8, True, 15)
prop(30, 28, "campfire", 0.9)
prop(26, 27, "barrel", 0.65, True, 11); prop(32, 30, "crate", 0.65, True, 12)
prop(28, 26, "stall", 0.7, True, 15); prop(24, 30, "crate", 0.6, True, 12)
prop(21, 27, "h_bucket", 0.7); prop(23, 28, "log", 0.8)
prop(34, 29, "banner", 0.7); prop(27, 25, "banner", 0.7)

# ---- the Academy Outpost's courtyard: colonnade, wards, the masters in stone.
prop(50, 20, "support", 0.9, True, 14); prop(58, 20, "support", 0.9, True, 14)
prop(50, 24, "support", 0.9, True, 14); prop(58, 24, "support", 0.9, True, 14)
prop(52, 19, "statue", 0.9, True, 16); prop(56, 19, "statue", 0.9, True, 16)
prop(54, 25, "crystal", 0.85); prop(51, 22, "crystal", 0.8); prop(57, 22, "crystal", 0.8)
prop(49, 19, "banner", 0.75); prop(59, 19, "banner", 0.75)
prop(53, 26, "lantern", 0.7); prop(55, 26, "lantern", 0.7)
prop(60, 23, "cherry", 1.0); prop(47, 22, "cherry", 1.0)          # the Academy's garden

# ---- the Sunken Colonnade: a Ychellen city the sun found. Fallen pillars, the
#      old stone, and the dry that took it.
for _cx, _cy in ((10, 48), (13, 49), (16, 50), (9, 51), (14, 52), (11, 53)):
    prop(_cx, _cy, "ruin", 1.0, True, 20)
prop(12, 50, "statue", 0.85, True, 15)
prop(15, 48, "support", 0.85, True, 14); prop(8, 49, "support", 0.85, True, 14)
prop(11, 47, "gravestone", 0.75); prop(17, 51, "gravestone", 0.75)
for _rx, _ry in ((7, 52), (18, 49), (13, 47), (10, 53)):
    prop(_rx, _ry, "rock", 0.9, True, 14)

# ---- the dry country between: scattered stone, a dead tree here and there, an
#      abandoned wagon that did not make Kae Ychel.
prop(44, 40, "wagon", 0.85, True, 18)                    # a hauler that stopped here
prop(45, 41, "crate", 0.6, True, 12); prop(43, 41, "log", 0.8)
prop(64, 40, "ruin", 0.9, True, 18)                      # one more Ychellen stone
prop(40, 14, "rock", 0.9, True, 14); prop(62, 44, "rock", 0.9, True, 14)
prop(56, 38, "orePile", 0.8, True, 13)                   # spoil off the miners' track
prop(38, 8, "signpost", 0.7, True, 10)                   # DEEPDELVE ↑ at the track head
prop(36, 12, "rock", 0.85, True, 13)

# ---- the east edge: the road off toward Kae Ychel, marked and not enterable.
prop(68, 30, "signpost", 0.8, True, 11)                  # KAE YCHEL — NINE DAYS
prop(66, 29, "support", 0.8, True, 13); prop(66, 33, "support", 0.8, True, 13)
prop(67, 28, "banner", 0.75); prop(67, 34, "banner", 0.75)

# =====================================================================
#  5. WHAT GROWS HERE
#
#  Dry country. Cherry and green cluster at the oasis and the Academy garden;
#  everywhere else it is dead trees, dry scrub and stone. The gradient from wet
#  to parched is the whole of the vegetation story.
# =====================================================================
STANDS = [
    (14, 12, 30, 30, 0.20, (1, 3, 4, 2)),      # around the oasis: greener, mixed
    (34, 6, 52, 20, 0.12, (2, 1, 6, 0)),       # the dry north: mostly dead
    (40, 36, 62, 52, 0.14, (2, 2, 5, 0)),      # the parched south-east
    (46, 44, 62, 54, 0.12, (1, 1, 6, 0)),
]
CLEARINGS = [(18, 18, 34, 34), (46, 16, 62, 28), (6, 44, 20, 54), (24, 28, 36, 34)]

UNDER = [
    (12, 10, 32, 32, 0.14, ("bush", "flowers", "rock", "log")),
    (34, 6, 54, 22, 0.10, ("rock", "log", "bush", "rock")),
    (38, 34, 64, 54, 0.12, ("rock", "bush", "log", "rock")),
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
SPAWN = (3, ROAD_Y)

NPCS = [
    ("caravan_master", 29, 29),    # at the oasis, with his wagons
    ("road_priest", 24, 27),       # blessing the well
    ("trader", 31, 30),            # Sella of the Road, working the caravan
    ("scholar", 55, 24),           # the Keeper, at the outpost
    ("apprentice_ward", 50, 22),   # in the courtyard
    ("apprentice_scry", 57, 24),
]

SAFE = [
    (18, 20, 36, 34),              # the oasis and the caravan rest
    (46, 16, 62, 28),              # the Academy terrace
    (0, ROAD_IN[0] - 2, 12, ROAD_IN[1] + 3),   # the road in from Ashwold
    (6, 44, 20, 54),               # the ruins
]

DISCOVERIES = [
    ("The Kae Ychel Road", 10, ROAD_Y, 3.6, "RegionDiscovered"),
    ("The Oasis", 22, 24, 3.6, "RegionDiscovered"),
    ("The Caravan Rest", 29, 29, 3.4, "RegionDiscovered"),
    ("The Academy Outpost", 54, 22, 3.8, "RegionDiscovered"),
    ("The Sunken Colonnade", 12, 50, 3.6, "SecretArea"),
    ("The Miners' Track", 32, 12, 3.0, "RegionDiscovered"),
    ("The Waymark East", 68, 30, 3.0, "SecretArea"),
]

CONTAINERS = [
    ("The way-house cellar", 30, 30, "chest_wood"),
    ("A caravan crate", 32, 30, "crate"),
    ("The store's barrels", 33, 30, "barrel"),
    ("A pilgrim's cache by the well", 21, 27, "chest_wood"),
    ("The outpost's ward-chest", 52, 25, "chest_iron"),
    ("Left in the apprentices' hall", 49, 20, "crate"),
    ("Sunk under a pillar", 12, 50, "chest_iron"),
    ("In the fallen colonnade", 15, 50, "chest_wood"),
    ("The stopped wagon's load", 44, 40, "cart"),
    ("Cached at the east waymark", 66, 31, "barrel"),
]

EXAMINABLES = [
    ("the sun-road marker", 4, ROAD_Y - 2, "Read", "plaque",
     "A pillar of pale stone where the road changes underfoot from the black earth "
     "of the coast country to the dust of the east. \"THE KAE YCHEL ROAD. WATER AT "
     "THE OASIS, AND NOT AGAIN FOR A DAY. THE SUN KING KEEPS THIS ROAD, AND THE SUN "
     "KING KEEPS NO SHADE.\""),
    ("the oasis well", 22, 24, "Examine", "note",
     "A well of the old make, the stone the same grey as the fallen city to the "
     "south, and the water in it sweet where everything around it is dust. Whoever "
     "cut this road cut the well first. The caravans have watered here so long that "
     "the coping is worn into scoops where the ropes go over."),
    ("the caravan board", 29, 29, "Read", "note",
     "A board under the way-house eave, chalked with the road's plain arithmetic. "
     "\"KAE YCHEL — 9 DAYS, WELLS HOLDING. SILK & GLASS EAST. ORE & HIDE WEST. "
     "NEXT CARAVAN AT THE DARK OF THE MOON.\" Someone has added, in a different "
     "hand: \"IF THE WELLS DO NOT HOLD, DO NOT SET OUT.\""),
    ("the Academy gate", 53, 24, "Read", "plaque",
     "The gate of the Academy's outpost, worked with the mark of the Twin Sun King "
     "— two discs, one rising, one setting. \"THE ACADEMY OF KAE YCHEL. THIS "
     "OUTPOST PREPARES THOSE WHO WOULD SIT THE MIDSUMMER TESTS.\" The wards on the "
     "gateposts hum under your hand, and they are not warm. They are cold."),
    ("the courtyard wards", 51, 22, "Examine", "note",
     "Crystal set into iron stands at the corners of the yard, and the air between "
     "them has a grain to it, like heat-shimmer that will not go away. This is the "
     "Academy's own art, the same that made the demon that emptied the memorial "
     "inside — the same that made the party's Mage, and unmade six of his fellows."),
    ("the fallen colonnade", 12, 50, "Read", "plaque",
     "A double row of pillars, half of them down, sunk to their capitals in dust "
     "that was a city floor. The stone is Ychellen — the same letters as the "
     "boundary marker in Ashwold's fields and the ruin in the wood's hollow. Three "
     "sightings of one people, and this is the only one that was a city. The sun "
     "found it. The road goes round it and does not go in."),
    ("a toppled statue", 12, 50, "Examine", "note",
     "A figure of the old stone, face down where it fell, and long enough ago that "
     "the dust has drifted over the back of it. Turned up, the face is worn to "
     "nothing but the shape of a crown — two discs, one rising, one setting. The "
     "Sun King was old here before Kae Ychel raised him a city of his own."),
    ("the east waymark", 68, 30, "Read", "plaque",
     "The last stone before the road runs off the edge of anywhere anyone here "
     "has been. \"KAE YCHEL — NINE DAYS. THE NERUUM FLOTILLA MAKES PORT AT THE "
     "DARK OF THE MOON. TRAVELLERS FOR THE MIDSUMMER TESTS, TAKE THE NORTH FORK AT "
     "THE THIRD WELL.\" East of here is off your map, and the road knows it is not."),
    ("the stopped wagon", 44, 40, "Examine", "note",
     "A hauler's wagon, an axle cracked, pulled off the road and left. The load is "
     "gone but the manifest is still nailed to the board: silk, lamp-glass, and a "
     "sealed crate marked only with the Academy's two-disc sun. Whatever that was, "
     "somebody carried it on by hand rather than wait for a wheelwright."),
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
        # This is the Sun King's dry country: the bed is dirt and the grass is
        # patchy on top of it, which is the opposite of the wood. Grass keeps a
        # hold only near the water and on the Academy's watered terrace.
        tx, ty = mx // K.SUB, my // K.SUB
        wet = (14 <= tx <= 32 and 12 <= ty <= 32) or (46 <= tx <= 62 and 16 <= ty <= 28)
        bed = "grass" if wet else "dirt"
        paint("Ground", mx, my, K.gid("Floors", K.FLOOR_BLOCK[bed] + dx, dy))
        # No "dry sward" of scattered grass tiles: the first attempt laid them on
        # a hashed grid and the result was a checkerboard, the most artificial
        # thing on any of the four maps. Dirt underfoot with the tufts below is
        # parched country enough.

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

# Ground texture: dry tufts everywhere, reed only at the oasis.
# The density pass — crops on the worked ground, flower sprigs by the houses,
# and leaf/reed texture on the open grass. See region_kit.dress_ground.
_crops, _sprigs, _tufts = K.dress_ground(at, paint, COLS, ROWS, tuft_density=0.14)

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
# The Academy Outpost opens onto its own interior map.
og["Warp"] = [obj("the Academy Outpost", 53.5, 22.5,
                  [("DestinationMap", "mage_school"), ("DestinationSpawn", "0.0,0.0"),
                   ("Label", "the Academy Outpost"), ("Verb", "Enter"), ("Radius", 40)])]
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

lid, parts = 600, []
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
<!-- REGION 4 — THE KAE YCHEL ROAD. Hand-authored; see
     tools/build-r4-kae-ychel-road.py for why each thing is where it is.
     It closes the ring: its WEST edge is Ashwold's east edge (road at rows
     {ROAD_IN[0]}-{ROAD_IN[1]}, the Ashbrook at {WATER_IN[0]}-{WATER_IN[1]}) and its NORTH edge is Deepdelve's
     south edge (the miners' track at columns {TRACK_IN[0]}-{TRACK_IN[1]}), both laid cell for cell.
     Carries the Academy Outpost, the Mage's own place. Dry country: the bed is
     dirt and grass keeps a hold only at the water and the Academy's terrace.
     16x16 grid; one engine cell is a 2x2 block, so the map is {MW}x{MH}. -->
<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down"
     width="{MW}" height="{MH}" tilewidth="{K.T}" tileheight="{K.T}" infinite="0"
     nextlayerid="{lid+1}" nextobjectid="{_oid[0]}">
 <properties>
  <property name="Region" value="{REGION_ID}"/>
  <property name="DisplayName" value="{sx.escape(DISPLAY)}"/>
  <property name="Stage" type="int" value="1"/>
  <property name="EngineTile" type="int" value="{K.ENGINE_TILE}"/>
  <property name="Wildlife" type="int" value="30"/>
  <property name="WestMap" value="{WEST_NEIGHBOUR}"/>
  <property name="NorthMap" value="{NORTH_NEIGHBOUR}"/>
 </properties>
{chr(10).join(K.TSREFS)}
{"".join(parts)}</map>
'''
path_out = os.path.join(OUT, "R4_KaeYchelRoad.tmx")
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

# ---- both seams, checked ----
def seam_ok(mine, theirs):
    return [i for i in range(len(mine))
            if (mine[i] in ",t=") != (theirs[i] in ",t=")
            or (mine[i] == "~") != (theirs[i] == "~")
            or (mine[i] == "#") != (theirs[i] == "#")]


bw = seam_ok(_edges["west"], WSEAM)
bn = seam_ok(_edges["north"], NSEAM)
print(f"  seam west vs {WEST_NEIGHBOUR}: " + ("MATCHES" if not bw else f"MISMATCH at rows {bw}"))
print(f"  seam north vs {NORTH_NEIGHBOUR}: " + ("MATCHES" if not bn else f"MISMATCH at cols {bn}"))
