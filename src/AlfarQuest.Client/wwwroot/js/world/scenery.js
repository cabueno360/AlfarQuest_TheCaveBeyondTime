// =====================================================================
//  Cave scenery. Placement is seeded and rejects anything that would
//  crowd a crystal, another prop, or the party's spawn.
// =====================================================================
import { ATLAS } from "../atlas.js";
import { mulberry32 } from "../rng.js";

// Scenery for the cistern, as [col,row] cells of Miner_Decorations.png plus the
// scale to draw them at (the sheet is 128px; heroes are ~63px tall, so props
// need bringing down to a believable size). Chosen to read as a flooded mining
// gallery the delvers are picking through: crystal shrines, ruined pillars,
// spoil heaps and the leavings of whoever came before.
// `n` is how many of each to scatter: landmarks stay unique, mining debris
// repeats so the gallery feels worked-over rather than decorated.
const PROPS = [
    { cell: [3, 8],  s: 0.70, n: 1 },   // crystal-studded stone arch
    { cell: [8, 11], s: 0.58, n: 1 },   // crystal pedestal, blue flame
    { cell: [9, 11], s: 0.58, n: 1 },   // green crystal altar
    { cell: [9, 12], s: 0.56, n: 1 },   // teal crystal shrine
    { cell: [8, 9],  s: 0.54, n: 1 },   // basin of blue orbs
    { cell: [9, 0],  s: 0.54, n: 1 },   // gargoyle well
    { cell: [6, 10], s: 0.58, n: 1 },   // miner statue
    { cell: [5, 0],  s: 0.52, n: 2 },   // green-flame brazier
    { cell: [6, 4],  s: 0.62, n: 2 },   // chained obelisk
    { cell: [3, 10], s: 0.62, n: 2 },   // rune monument
    { cell: [2, 8],  s: 0.54, n: 2 },   // rune tablet
    { cell: [8, 4],  s: 0.62, n: 3 },   // toppled pillars
    { cell: [8, 13], s: 0.52, n: 4 },   // boulders
    { cell: [9, 13], s: 0.52, n: 3 },   // rock pile and pickaxe
    { cell: [8, 3],  s: 0.46, n: 3 },   // spoil heap
    { cell: [9, 3],  s: 0.44, n: 2 },   // heaped chains
    { cell: [5, 7],  s: 0.46, n: 2 },   // barrel and pick
    { cell: [5, 9],  s: 0.46, n: 2 },   // barrels
    { cell: [6, 2],  s: 0.44, n: 2 },   // chest
    { cell: [2, 3],  s: 0.44, n: 3 },   // flat slab
];

// Flat floor decals, painted under everything else.
const DECALS = [
    { cell: [4, 7], s: 0.85 },    // warding pentagram
    { cell: [0, 9], s: 0.70 },    // black spiral
];

export function buildScenery(s) {
    const W = s.chamberW, H = s.chamberH;
    if (!W || !H) return null;
    const rnd = mulberry32(0xA1FA4 + (s.level || 1) * 5171);   // "Alfar", near enough
    const crystals = s.ents.filter(e => e.t === "crystal");
    const spawn = s.spawn || { x: W * 0.5, y: H * 0.75 };
    const T = s.tile || 32;
    const map = s.map || [];
    const solid = (px, py) => {
        const x = Math.floor(px / T), y = Math.floor(py / T);
        return x < 0 || y < 0 || y >= map.length || x >= (map[0] || "").length || map[y][x] === "1";
    };
    const placed = [];

    const fits = (x, y, clear) => {
        if (Math.hypot(x - spawn.x, y - spawn.y) < 240) return false;
        // Props must stand on open floor, with their footprint clear of rock.
        if (solid(x, y) || solid(x - 26, y) || solid(x + 26, y) || solid(x, y - 26) || solid(x, y + 10)) return false;
        if (s.exit && Math.hypot(x - s.exit.x, y - s.exit.y) < 150) return false;
        for (const c of crystals) if (Math.hypot(x - c.x, y - c.y) < c.r + 70) return false;
        for (const p of placed) if (Math.hypot(x - p.x, y - p.y) < clear) return false;
        return true;
    };
    const scatter = (list, clear, out) => {
        let wanted = 0;
        for (const item of list) {
            for (let k = 0; k < (item.n ?? 1); k++) {
                wanted++;
                for (let tries = 0; tries < 80; tries++) {
                    const x = 150 + rnd() * (W - 300);
                    const y = 150 + rnd() * (H - 300);
                    if (!fits(x, y, clear)) continue;
                    const e = { t: "prop", x, y, cell: item.cell, s: item.s, flip: rnd() < 0.5 };
                    placed.push(e); out.push(e);
                    break;
                }
            }
        }
        return wanted;
    };

    const props = [], decals = [];
    const wantProps = scatter(PROPS, 118, props);
    scatter(DECALS, 240, decals);
    // Say so rather than silently thinning out: a crowded chamber drops props.
    if (props.length < wantProps)
        console.warn(`scenery: placed ${props.length}/${wantProps} props (chamber too crowded)`);
    return { props, decals };
}
