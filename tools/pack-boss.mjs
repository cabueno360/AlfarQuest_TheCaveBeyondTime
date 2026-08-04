// Pack a Gemini boss sheet into atlas_chars: the first half of its poses as
// the idle kind, the second half as the Rage kind the renderer holds when the
// keeper enrages.
//
//     node tools/pack-boss.mjs <sheet.png> <kindBase> [targetH=64]
//
// e.g.  node tools/pack-boss.mjs %USERPROFILE%\Downloads\sheet.png bossVault
//
// Poses are found as column bands of non-magenta; a band holding two welded
// poses (a tail or a reaching arm bridging the gap) is split at its density
// valley. Each pose is trimmed, magenta turned transparent, box-filtered down
// to the target height, and appended to the atlas. The original sheet is
// archived under assets/sprites.
import fs from 'node:fs';
import path from 'node:path';
import { PNG } from 'pngjs';

const [sheetPath, kindBase, targetHArg] = process.argv.slice(2);
if (!sheetPath || !kindBase) { console.error('usage: pack-boss.mjs <sheet.png> <kindBase> [targetH]'); process.exit(1); }
const TARGET_H = +(targetHArg ?? 64);

const WWW = path.resolve(path.dirname(new URL(import.meta.url).pathname.slice(1)), '../src/AlfarQuest.Client/wwwroot');
const img = PNG.sync.read(fs.readFileSync(sheetPath));
const magenta = (x, y) => {
    const i = (y * img.width + x) * 4;
    return img.data[i] > 180 && img.data[i + 2] > 180 && img.data[i + 1] < 110;
};

const colHas = [];
for (let x = 0; x < img.width; x++) {
    let rows = 0;
    for (let y = 0; y < img.height; y += 2) if (!magenta(x, y)) rows++;
    colHas.push(rows >= 8);
}
const bands = [];
let start = -1;
for (let x = 0; x <= img.width; x++) {
    const has = x < img.width && colHas[x];
    if (has && start < 0) start = x;
    if (!has && start >= 0) { if (x - start > 40) bands.push([start, x]); start = -1; }
}
const density = x => { let n = 0; for (let y = 0; y < img.height; y += 2) if (!magenta(x, y)) n++; return n; };
for (let i = 0; i < bands.length; i++) {
    const [a, b] = bands[i];
    if (b - a < img.width * 0.32) continue;
    let best = -1, bestN = Infinity;
    for (let x = a + Math.floor((b - a) * 0.25); x < a + Math.floor((b - a) * 0.75); x++) {
        const n = density(x);
        if (n < bestN) { bestN = n; best = x; }
    }
    bands.splice(i, 1, [a, best], [best + 1, b]);
    i--;
}
console.log('bands:', JSON.stringify(bands));

function extract([bx0, bx1]) {
    let y0 = img.height, y1 = 0;
    for (let y = 0; y < img.height; y++)
        for (let x = bx0; x < bx1; x++)
            if (!magenta(x, y)) { if (y < y0) y0 = y; if (y > y1) y1 = y; break; }
    const w = bx1 - bx0, h = y1 - y0 + 1;
    const sp = new PNG({ width: w, height: h });
    for (let y = 0; y < h; y++)
        for (let x = 0; x < w; x++) {
            const si = ((y0 + y) * img.width + (bx0 + x)) * 4, di = (y * w + x) * 4;
            if (magenta(bx0 + x, y0 + y)) { sp.data[di + 3] = 0; continue; }
            sp.data[di] = img.data[si]; sp.data[di + 1] = img.data[si + 1];
            sp.data[di + 2] = img.data[si + 2]; sp.data[di + 3] = img.data[si + 3];
        }
    return sp;
}

function shrink(sp) {
    const scale = TARGET_H / sp.height;
    const ow = Math.round(sp.width * scale), oh = TARGET_H;
    const out = new PNG({ width: ow, height: oh });
    for (let y = 0; y < oh; y++)
        for (let x = 0; x < ow; x++) {
            const sx0 = Math.floor(x / scale), sx1 = Math.min(sp.width, Math.ceil((x + 1) / scale));
            const sy0 = Math.floor(y / scale), sy1 = Math.min(sp.height, Math.ceil((y + 1) / scale));
            let r = 0, g = 0, b = 0, a = 0, n = 0;
            for (let sy = sy0; sy < sy1; sy++)
                for (let sx = sx0; sx < sx1; sx++) {
                    const i = (sy * sp.width + sx) * 4, al = sp.data[i + 3];
                    r += sp.data[i] * al; g += sp.data[i + 1] * al; b += sp.data[i + 2] * al;
                    a += al; n++;
                }
            const o = (y * ow + x) * 4;
            if (a > 0) {
                out.data[o] = Math.round(r / a); out.data[o + 1] = Math.round(g / a); out.data[o + 2] = Math.round(b / a);
                const av = Math.round(a / n);
                out.data[o + 3] = av > 96 ? 255 : av > 40 ? av : 0;
            }
        }
    return out;
}

const sprites = bands.map(extract).map(shrink);
const half = Math.ceil(sprites.length / 2);
const groups = [[kindBase, sprites.slice(0, half)], [kindBase + 'Rage', sprites.slice(half)]];

const atlasPath = path.join(WWW, 'assets/Outside/atlas_chars.png');
const jsonPath = path.join(WWW, 'assets/Outside/atlas_chars.json');
const atlas = PNG.sync.read(fs.readFileSync(atlasPath));
const frames = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));

const rowH = TARGET_H + 4;
const totalW = sprites.reduce((s, p) => s + p.width + 2, 2);
const grown = new PNG({ width: Math.max(atlas.width, totalW), height: atlas.height + rowH });
PNG.bitblt(atlas, grown, 0, 0, atlas.width, atlas.height, 0, 0);

let x = 2;
for (const [kind, list] of groups) {
    frames[kind] = [];
    for (const sp of list) {
        PNG.bitblt(sp, grown, 0, 0, sp.width, sp.height, x, atlas.height + 2);
        frames[kind].push({ x, y: atlas.height + 2, w: sp.width, h: sp.height });
        x += sp.width + 2;
    }
    console.log(`${kind}: ${list.length} frames`);
}

fs.writeFileSync(atlasPath, PNG.sync.write(grown));
fs.writeFileSync(jsonPath, JSON.stringify(frames));
fs.mkdirSync(path.join(WWW, 'assets/sprites'), { recursive: true });
fs.copyFileSync(sheetPath, path.join(WWW, 'assets/sprites', kindBase + '-sheet.png'));
console.log(`packed at row y=${atlas.height + 2}; sheet archived as assets/sprites/${kindBase}-sheet.png`);
