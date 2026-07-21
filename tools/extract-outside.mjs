// Extract the outdoor props out of the contact sheet into a clean atlas.
//
// Run from the repo root:  node tools/extract-outside.mjs            (detect)
//                          node tools/extract-outside.mjs --pack     (build atlas)
//   needs `npm i pngjs` in tools/
//
// The sheet has NO alpha: its background is opaque near-white and makes up
// about half the image. Two things follow from that.
//
//   * The backdrop is removed by flood filling from the image border, not by a
//     colour threshold. A threshold would also eat the near-white *inside* the
//     art — the canvas tent, the pale cliff stone, the waterfall foam.
//   * Because the backdrop is one connected region, whatever survives it is a
//     set of separate islands: one per prop. So the props are found by
//     connected-component labelling rather than by hand-typed boxes, which is
//     both faster and far less error-prone.
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { PNG } from 'pngjs';

// Alpha for one pixel of a prop. Sprite edges are anti-aliased against the white
// backdrop and land around 225-243 — too dark for the background flood to take,
// so they survive as a pale halo. Fading those boundary pixels removes the halo
// without touching genuinely white art (tent canvas, white flowers), because
// only pixels that sit ON the sprite's outline are considered.
function edgeAlpha(data, i, onEdge) {
    if (!onEdge) return 255;
    const lum = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
    if (lum < 214) return 255;
    if (lum > 243) return 0;
    return Math.round(255 * (243 - lum) / 29);
}

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const ASSETS = path.join(ROOT, 'src/AlfarQuest.Client/wwwroot/assets');
const SRC = path.join(ASSETS, 'Outside/outside_tiles.png');

const sheet = PNG.sync.read(fs.readFileSync(SRC));
const W = sheet.width, H = sheet.height;
const A = (x, y) => (y * W + x) * 4;

// Background is 254,255,254. Kept tight on purpose: the tent and the cliff
// highlights sit only a little below pure white.
const isBg = (i) => sheet.data[i] > 243 && sheet.data[i + 1] > 243 && sheet.data[i + 2] > 243;

// ---- 1. flood the backdrop from the border ----
const bg = new Uint8Array(W * H);
{
    const st = [];
    for (let x = 0; x < W; x++) { st.push(x, 0); st.push(x, H - 1); }
    for (let y = 0; y < H; y++) { st.push(0, y); st.push(W - 1, y); }
    while (st.length) {
        const y = st.pop(), x = st.pop();
        if (x < 0 || y < 0 || x >= W || y >= H) continue;
        const k = y * W + x;
        if (bg[k]) continue;
        if (!isBg(A(x, y))) continue;
        bg[k] = 1;
        st.push(x + 1, y, x - 1, y, x, y + 1, x, y - 1);
    }
}

// ---- 2. label what survived ----
const lab = new Int32Array(W * H).fill(-1);
const comps = [];
for (let sy = 0; sy < H; sy++) {
    for (let sx = 0; sx < W; sx++) {
        const k0 = sy * W + sx;
        if (bg[k0] || lab[k0] !== -1) continue;
        const id = comps.length;
        let minX = sx, maxX = sx, minY = sy, maxY = sy, area = 0;
        const st = [sx, sy];
        lab[k0] = id;
        while (st.length) {
            const y = st.pop(), x = st.pop();
            area++;
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
            for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]]) {
                const nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                const k = ny * W + nx;
                if (bg[k] || lab[k] !== -1) continue;
                lab[k] = id; st.push(nx, ny);
            }
        }
        comps.push({ id, x: minX, y: minY, w: maxX - minX + 1, h: maxY - minY + 1, area });
    }
}

// ---- 3. keep the things that are actually props ----
// Specks are noise; enormous islands are the terrain sample patches, which are
// tiled from directly (see tools/outside-atlas.json) rather than used as props.
const props = comps.filter(c =>
    c.area >= 220 && c.w >= 12 && c.h >= 12 && c.w <= 420 && c.h <= 420 &&
    !(c.w > 150 && c.h > 150 && c.area / (c.w * c.h) > 0.85));

props.sort((a, b) => (a.y - b.y) || (a.x - b.x));
console.log(`components: ${comps.length}  ->  props kept: ${props.length}`);
console.log(`largest kept: ${props.slice().sort((a, b) => b.area - a.area).slice(0, 5)
    .map(c => `${c.w}x${c.h}@${c.x},${c.y}`).join('  ')}`);

fs.writeFileSync(path.join(ROOT, 'tools/outside-props.json'),
    JSON.stringify(props.map(({ x, y, w, h, area }) => ({ x, y, w, h, area })), null, 1));

// ---- 4. contact sheet of every detected prop, for eyeballing / labelling ----
const SC = 1, PAD = 4;
const cw = Math.max(...props.map(p => p.w)) + PAD;
const ch = Math.max(...props.map(p => p.h)) + PAD;
const COLS = 14, ROWS = Math.ceil(props.length / COLS);
const sheetOut = new PNG({ width: COLS * cw * SC, height: ROWS * ch * SC });
for (let i = 0; i < sheetOut.data.length; i += 4) {
    // checkerboard so cut-out transparency is obvious
    const px = (i / 4) % sheetOut.width, py = Math.floor((i / 4) / sheetOut.width);
    const ck = ((Math.floor(px / 8) + Math.floor(py / 8)) % 2) ? 90 : 60;
    sheetOut.data[i] = ck * 0.5; sheetOut.data[i + 1] = ck * 0.2;
    sheetOut.data[i + 2] = ck; sheetOut.data[i + 3] = 255;
}
props.forEach((p, i) => {
    const ox = (i % COLS) * cw, oy = Math.floor(i / COLS) * ch;
    for (let y = 0; y < p.h; y++)
        for (let x = 0; x < p.w; x++) {
            const sxp = p.x + x, syp = p.y + y;
            if (lab[syp * W + sxp] !== p.id) continue;   // only this prop's own pixels
            const si = A(sxp, syp);
            const di = ((oy + y) * sheetOut.width + (ox + x)) * 4;
            sheetOut.data[di] = sheet.data[si];
            sheetOut.data[di + 1] = sheet.data[si + 1];
            sheetOut.data[di + 2] = sheet.data[si + 2];
            sheetOut.data[di + 3] = 255;
        }
});
fs.writeFileSync(path.join(ROOT, 'tools/outside-contact.png'), PNG.sync.write(sheetOut));
console.log(`contact sheet: tools/outside-contact.png  (${COLS} per row, cell ${cw}x${ch})`);

// ---- 5. compact, uniform contact sheet for identification ----
// Each prop scaled to fit one cell so all of them are legible at once; the
// index is implied by position (row * COLS2 + col), which is what the naming
// table below refers to.
const CELL = 104, COLS2 = 16, ROWS2 = Math.ceil(props.length / COLS2);
const cs = new PNG({ width: COLS2 * CELL, height: ROWS2 * CELL });
for (let i = 0; i < cs.data.length; i += 4) {
    const px = (i / 4) % cs.width, py = Math.floor((i / 4) / cs.width);
    const band = Math.floor(py / CELL) % 2, colb = Math.floor(px / CELL) % 2;
    const v = (band ^ colb) ? 42 : 30;
    cs.data[i] = v; cs.data[i + 1] = v * 0.8; cs.data[i + 2] = v * 1.6; cs.data[i + 3] = 255;
}
props.forEach((p, i) => {
    const ox = (i % COLS2) * CELL, oy = Math.floor(i / COLS2) * CELL;
    const sc = Math.min((CELL - 10) / p.w, (CELL - 10) / p.h, 1);
    const dw = Math.max(1, Math.round(p.w * sc)), dh = Math.max(1, Math.round(p.h * sc));
    const px0 = ox + Math.floor((CELL - dw) / 2), py0 = oy + Math.floor((CELL - dh) / 2);
    for (let y = 0; y < dh; y++)
        for (let x = 0; x < dw; x++) {
            const sxp = p.x + Math.floor(x / sc), syp = p.y + Math.floor(y / sc);
            if (sxp >= W || syp >= H) continue;
            if (lab[syp * W + sxp] !== p.id) continue;
            const si = A(sxp, syp), di = ((py0 + y) * cs.width + (px0 + x)) * 4;
            cs.data[di] = sheet.data[si]; cs.data[di + 1] = sheet.data[si + 1];
            cs.data[di + 2] = sheet.data[si + 2]; cs.data[di + 3] = 255;
        }
    // index ticks: a bar of `col` marks along the top, row marks down the left
    for (let t = 0; t <= (i % COLS2); t++)
        for (let d = 0; d < 3; d++) { const di = ((oy + 2 + d) * cs.width + ox + 3 + t * 3) * 4;
            cs.data[di] = 255; cs.data[di + 1] = 240; cs.data[di + 2] = 120; }
    for (let t = 0; t <= Math.floor(i / COLS2); t++)
        for (let d = 0; d < 3; d++) { const di = ((oy + 8 + t * 3) * cs.width + ox + 2 + d) * 4;
            cs.data[di] = 120; cs.data[di + 1] = 240; cs.data[di + 2] = 255; }
});
fs.writeFileSync(path.join(ROOT, 'tools/outside-index.png'), PNG.sync.write(cs));
console.log(`index sheet: tools/outside-index.png  (${COLS2} per row, ${ROWS2} rows)`);

// ---- 6. name the pieces we actually use, then pack ----
// Indices refer to tools/outside-index.png (16 per row). Anything not named
// here stays out of the atlas — the sheet holds 261 islands, most of them
// duplicates or filler we have no use for.
const NAMES = {
    caveEntrance: [13], caveMouth: [18],
    pine: [9, 21, 34, 46, 67], broadleaf: [11, 12, 29, 35, 49, 56, 62, 72], cherry: [60],
    deadTree: [87, 123], stump: [68], log: [45, 50, 57, 101],
    bush: [15, 24, 37, 73, 77, 78, 83, 86, 88, 94, 95, 96, 97, 98, 103, 111, 117, 124, 131, 136],
    flowers: [69, 79, 93, 100, 102, 107, 110, 113, 114, 115, 128, 134, 140, 149, 150, 154, 157, 160],
    rock: [14, 17, 53, 54, 55, 66, 82, 89, 104, 105, 106, 119, 125, 249, 250, 255, 256, 259],
    cliff: [3, 4, 5, 6, 7, 8, 19, 20, 25, 27, 28, 32, 33, 40, 48, 64, 71, 84, 85, 99, 109],
    waterfall: [30],
    barrel: [22, 51, 80, 91, 92, 142, 143, 159, 224, 235, 239],
    crate: [65, 90, 146, 151, 158, 176, 190, 232, 246],
    tent: [145], campfire: [152],
    workbench: [144, 148, 163, 179], toolRack: [189], bench: [153, 175, 203, 254], chair: [155, 166],
    mineCart: [23, 76, 167, 201], wagon: [216, 217],
    orePile: [233, 253, 260], crystal: [210, 211, 212, 213, 214, 215, 231, 234, 252, 257, 258],
    support: [16, 52, 74], ladder: [206, 208], scaffold: [240],
    signpost: [36, 108], lantern: [31, 191], banner: [204, 207],
    graveyard: [169], well: [47, 70, 187, 198, 247],
    statue: [141, 147, 168, 171, 173, 209, 221, 222, 223], ruin: [129, 130, 132, 133, 135, 186, 242],
    gravestone: [183, 184, 188, 192, 197, 200], stall: [220, 243, 244],
};

if (process.argv.includes('--pack')) {
    const picks = [];
    for (const [name, idxs] of Object.entries(NAMES))
        idxs.forEach((i, k) => {
            if (!props[i]) { console.warn(`  ! ${name}[${k}] index ${i} out of range`); return; }
            picks.push({ name, variant: k, p: props[i] });
        });

    // shelf-pack, tallest first
    picks.sort((a, b) => b.p.h - a.p.h);
    const PADP = 2, MAXW = 2048;
    let cx = PADP, cy = PADP, rowH = 0, atlasH = 0;
    for (const q of picks) {
        if (cx + q.p.w + PADP > MAXW) { cx = PADP; cy += rowH + PADP; rowH = 0; }
        q.ax = cx; q.ay = cy;
        cx += q.p.w + PADP; rowH = Math.max(rowH, q.p.h);
        atlasH = Math.max(atlasH, cy + q.p.h + PADP);
    }
    const atlas = new PNG({ width: MAXW, height: atlasH });
    atlas.data.fill(0);
    const mine = (id, x, y) => x >= 0 && y >= 0 && x < W && y < H && lab[y * W + x] === id;
    for (const q of picks)
        for (let y = 0; y < q.p.h; y++)
            for (let x = 0; x < q.p.w; x++) {
                const sxp = q.p.x + x, syp = q.p.y + y;
                if (lab[syp * W + sxp] !== q.p.id) continue;    // its own pixels only
                const onEdge =
                    !mine(q.p.id, sxp - 1, syp) || !mine(q.p.id, sxp + 1, syp) ||
                    !mine(q.p.id, sxp, syp - 1) || !mine(q.p.id, sxp, syp + 1);
                const si = A(sxp, syp), di = ((q.ay + y) * MAXW + (q.ax + x)) * 4;
                atlas.data[di] = sheet.data[si]; atlas.data[di + 1] = sheet.data[si + 1];
                atlas.data[di + 2] = sheet.data[si + 2];
                atlas.data[di + 3] = edgeAlpha(sheet.data, si, onEdge);
            }
    const outPng = path.join(ASSETS, 'Outside/atlas_outside.png');
    fs.writeFileSync(outPng, PNG.sync.write(atlas));

    const map = {};
    for (const q of picks) (map[q.name] ??= []).push({ x: q.ax, y: q.ay, w: q.p.w, h: q.p.h });
    fs.writeFileSync(path.join(ASSETS, 'Outside/atlas_outside.json'), JSON.stringify(map, null, 1));
    console.log(`\npacked ${picks.length} sprites in ${Object.keys(map).length} kinds`);
    console.log(`  ${outPng}  ${MAXW}x${atlasH}`);
    for (const [k, v] of Object.entries(map)) console.log(`  ${k.padEnd(14)} ${v.length}`);
}

// ---- 7. proof sheet: one example of every NAMED kind ----
// Guards against a silent mismatch: the NAMES table indexes into `props`, so any
// change to detection can shift every index and quietly pack the wrong art under
// the right name. This renders what each name actually resolved to.
if (process.argv.includes('--pack')) {
    const kinds = Object.keys(NAMES);
    const CE = 120, CC = 8, CR = Math.ceil(kinds.length / CC);
    const ps = new PNG({ width: CC * CE, height: CR * CE });
    for (let i = 0; i < ps.data.length; i += 4) { ps.data[i] = 30; ps.data[i+1] = 24; ps.data[i+2] = 46; ps.data[i+3] = 255; }
    kinds.forEach((name, i) => {
        const p = props[NAMES[name][0]];
        if (!p) return;
        const ox = (i % CC) * CE, oy = Math.floor(i / CC) * CE;
        const sc = Math.min((CE - 12) / p.w, (CE - 12) / p.h, 1);
        const dw = Math.round(p.w * sc), dh = Math.round(p.h * sc);
        const x0 = ox + Math.floor((CE - dw) / 2), y0 = oy + Math.floor((CE - dh) / 2);
        for (let y = 0; y < dh; y++)
            for (let x = 0; x < dw; x++) {
                const sxp = p.x + Math.floor(x / sc), syp = p.y + Math.floor(y / sc);
                if (sxp >= W || syp >= H || lab[syp * W + sxp] !== p.id) continue;
                const si = A(sxp, syp), di = ((y0 + y) * ps.width + (x0 + x)) * 4;
                ps.data[di] = sheet.data[si]; ps.data[di+1] = sheet.data[si+1]; ps.data[di+2] = sheet.data[si+2];
            }
    });
    fs.writeFileSync(path.join(ROOT, 'tools/outside-proof.png'), PNG.sync.write(ps));
    console.log('proof sheet (order below): tools/outside-proof.png');
    console.log(kinds.map((k, i) => `${i}:${k}`).join('  '));
}
