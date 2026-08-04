// Slice the Gemini Kazzat sheet, scale the idle pose to game size, and pack it
// into atlas_chars as the npcKazzat kind. The full sheet is archived in the
// repo so the key and tongue poses are there the day something wants them.
import fs from 'node:fs';
import path from 'node:path';
import { PNG } from 'pngjs';

const SHEET = path.join(process.env.USERPROFILE, 'Downloads', '529d5b92-d30a-4c52-af72-bbc293fd0f11.png');
const WWW = 'c:/tests/AlfarQuest_TheCaveBeyondTime/src/AlfarQuest.Client/wwwroot';
const TARGET_H = 84;                 // villagers stand ~54px; the guardian looms

const img = PNG.sync.read(fs.readFileSync(SHEET));
const magenta = (x, y) => {
    const i = (y * img.width + x) * 4;
    const [r, g, b] = [img.data[i], img.data[i + 1], img.data[i + 2]];
    return r > 180 && b > 180 && g < 110;
};

// Column bands of non-magenta = the poses.
const colHas = [];
for (let x = 0; x < img.width; x++) {
    let has = false;
    for (let y = 0; y < img.height; y += 2) if (!magenta(x, y)) { has = true; break; }
    colHas.push(has);
}
const bands = [];
let start = -1;
for (let x = 0; x <= img.width; x++) {
    const has = x < img.width && colHas[x];
    if (has && start < 0) start = x;
    if (!has && start >= 0) { if (x - start > 40) bands.push([start, x]); start = -1; }
}
console.log('bands:', JSON.stringify(bands));
if (!bands.length) throw new Error('no sprite bands found');

// The idle-with-cane pose is the leftmost band. Tight bbox, then extract with
// magenta turned transparent.
const [bx0, bx1] = bands[0];
let y0 = img.height, y1 = 0;
for (let y = 0; y < img.height; y++)
    for (let x = bx0; x < bx1; x++)
        if (!magenta(x, y)) { if (y < y0) y0 = y; if (y > y1) y1 = y; break; }
const w = bx1 - bx0, h = y1 - y0 + 1;
console.log(`idle pose: ${w}x${h} at (${bx0},${y0})`);

const sprite = new PNG({ width: w, height: h });
for (let y = 0; y < h; y++)
    for (let x = 0; x < w; x++) {
        const si = ((y0 + y) * img.width + (bx0 + x)) * 4, di = (y * w + x) * 4;
        if (magenta(bx0 + x, y0 + y)) { sprite.data[di + 3] = 0; continue; }
        sprite.data.copy ? img.data.copy(sprite.data, di, si, si + 4) : null;
        sprite.data[di] = img.data[si]; sprite.data[di + 1] = img.data[si + 1];
        sprite.data[di + 2] = img.data[si + 2]; sprite.data[di + 3] = img.data[si + 3];
    }

// Box-filter downscale to the target height.
const scale = TARGET_H / h;
const ow = Math.round(w * scale), oh = TARGET_H;
const out = new PNG({ width: ow, height: oh });
for (let y = 0; y < oh; y++)
    for (let x = 0; x < ow; x++) {
        const sx0 = Math.floor(x / scale), sx1 = Math.min(w, Math.ceil((x + 1) / scale));
        const sy0 = Math.floor(y / scale), sy1 = Math.min(h, Math.ceil((y + 1) / scale));
        let r = 0, g = 0, b = 0, a = 0, n = 0;
        for (let sy = sy0; sy < sy1; sy++)
            for (let sx = sx0; sx < sx1; sx++) {
                const i = (sy * w + sx) * 4, al = sprite.data[i + 3];
                r += sprite.data[i] * al; g += sprite.data[i + 1] * al; b += sprite.data[i + 2] * al;
                a += al; n++;
            }
        const o = (y * ow + x) * 4;
        if (a > 0) {
            out.data[o] = Math.round(r / a); out.data[o + 1] = Math.round(g / a); out.data[o + 2] = Math.round(b / a);
            out.data[o + 3] = Math.round(a / n) > 96 ? 255 : Math.round(a / n) > 40 ? Math.round(a / n) : 0;
        }
    }
console.log(`scaled to ${ow}x${oh}`);

// Append to atlas_chars: widen/extend the canvas below and record the frame.
const atlasPath = path.join(WWW, 'assets/Outside/atlas_chars.png');
const jsonPath = path.join(WWW, 'assets/Outside/atlas_chars.json');
const atlas = PNG.sync.read(fs.readFileSync(atlasPath));
const frames = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));

const grown = new PNG({ width: Math.max(atlas.width, ow + 4), height: atlas.height + oh + 4 });
PNG.bitblt(atlas, grown, 0, 0, atlas.width, atlas.height, 0, 0);
PNG.bitblt(out, grown, 0, 0, ow, oh, 2, atlas.height + 2);
frames.npcKazzat = [{ x: 2, y: atlas.height + 2, w: ow, h: oh }];

fs.writeFileSync(atlasPath, PNG.sync.write(grown));
fs.writeFileSync(jsonPath, JSON.stringify(frames));
fs.mkdirSync(path.join(WWW, 'assets/sprites'), { recursive: true });
fs.copyFileSync(SHEET, path.join(WWW, 'assets/sprites/kazzat-sheet.png'));
console.log(`npcKazzat packed at (2, ${atlas.height + 2}) ${ow}x${oh}; sheet archived`);
