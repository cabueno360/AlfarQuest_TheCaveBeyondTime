// =====================================================================
//  Tile tables. Every entry was chosen by measuring the sheet, not by eye —
//  see tools/outside-atlas.json and the notes in each block.
// =====================================================================
// Cave floor, as [col,row] tiles of the caves sheet with a relative weight.
// All of these are "centre fill" tiles that repeat without a seam; the sheet's
// other tiles are autotile edges and corners, which would show hard triangles
// if used as fill.
export const FLOOR_TILES = [
    { cell: [9, 45],  w: 26 },   // plain dark rock
    { cell: [10, 45], w: 26 },   // plain dark rock, variant
    { cell: [9, 42],  w: 8 },    // scattered pebbles
    { cell: [11, 42], w: 8 },    // more pebbles
    { cell: [5, 42],  w: 4 },    // pale lichen
    { cell: [14, 42], w: 3 },    // blue lichen
];

export const PATH_TILE = [31, 37];      // worn brown dirt

// Cave mouth from the caves sheet: a 3x4 tile opening that reads as a doorway
// cut into the rock. Placed by the engine at region entrances.
export const ARCH = { c: 7, r: 9, w: 3, h: 4 };

// Ore deposits from Miner_Ores.png — gold veins, geodes, coal, gem-shot stone.
// These belong ON the rock face, not free-standing on the floor, which is the
// difference between "a cave with ore in it" and "rocks left lying about".
export const ORE_CELLS = [
    [1, 5], [2, 5], [1, 6], [2, 6],     // gold veins
    [5, 4], [3, 5], [5, 3],             // blue geode, crystals, coal
    [6, 4], [6, 5],                     // green gem-shot stone
    [0, 3], [2, 3],                     // lava-veined rock
    [3, 3], [4, 3],                     // crystal-studded grey stone
];

export const BRIDGE_TILE = [45, 32];    // wooden planks

// Crystal formations picked out of the ore sheet, as [col,row] cells.
export const CRYSTAL_CELLS = [[7, 0], [8, 1], [9, 2], [9, 0], [6, 0], [8, 2]];

// Paint the entire chamber floor once into an offscreen canvas. Tiling it live
// would cost ~2000 drawImage calls per frame; this way the frame budget is a
// single blit.
// Wall tiles picked out of the caves sheet's rock autotiles. The sheet draws a
// wall mass as a black top with a lit rocky rim; we stamp the rim on whichever
// sides face open floor, which is what gives the 3/4 cave look.
// Wall autotile, read off the sheet's ring-shaped rock block (cols 14-26,
// rows 0-9). Rather than four tiles used everywhere, each rock cell now picks
// the piece that matches which of its sides face open floor — so a corner
// actually draws a corner. The sheet's rim is two tiles thick, which is why the
// brief insists walls never be one tile wide.
// Picked by measuring each cell's mean luminance across the block, not by eye:
// the ring is symmetric about cols 19-20, its interior sits at ~10 and its lit
// bottom ledge at ~46-59. Two of the corners chosen by eye earlier ([23,0] and
// [15,8]) measured ~10 — they were interior cells, which is exactly why those
// walls rendered as nothing.
export const WALL = {
    fill: [19, 3],            // interior mass, black by design
    n:    [19, 0],            // top rim — genuinely dim, it faces away from us
    s:    [19, 7],            // the lit ledge turned toward the camera
    w:    [16, 3],            // left rim
    e:    [23, 3],            // right rim
    nw:   [16, 1], ne: [22, 1],
    sw:   [16, 7], se: [22, 7],
};

// Terrain sampled from outside_tiles.png. Each was verified by tiling it 4x4 and
// checking for a seam — most of that sheet is hand-painted patches, so an
// arbitrary 32x32 window usually lands half on the white backdrop.
export const OUT = {
    // Six samples, not one: a single tile has a directional grain, and repeating
    // it across 80x80 produced visible vertical banding. Flowered variants are
    // in the set but drawn less often — see the weights in buildOutdoorCanvas.
    grass:    [[40, 250], [48, 262], [44, 206], [34, 276], [30, 230], [16, 210]],
    path:     [[62, 215], [66, 60], [70, 110]],
    water:    [[236, 60], [230, 30]],
    bridge:   [[214, 240]],
    mountain: [[432, 20], [680, 20], [755, 130]],   // cliff tops, seen from above
    cliffFace:[[440, 40], [432, 95], [820, 20]],    // blocky stone for the south edge
};
