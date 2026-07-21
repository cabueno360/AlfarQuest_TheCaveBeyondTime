// Extract the sprites we need out of the labelled contact sheet into a clean,
// uniform, transparent-background atlas.
//
// The sheet has NO alpha (background is opaque rgb~17,19,16), so the panel
// backdrop is removed with a flood fill seeded from the border of each frame
// box. Flood fill (rather than a global colour threshold) matters: it keeps
// dark pixels *inside* the sprite, which a threshold would eat away.
import fs from 'fs';
import { PNG } from 'pngjs';

// Run from the repo root:  node tools/extract-sprites.mjs
//   (needs `npm i pngjs` — the game itself has no JS dependencies)
import path from 'path';
import { fileURLToPath } from 'url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const ASSETS = path.join(ROOT, 'src/AlfarQuest.Client/wwwroot/assets');
const SRC = path.join(ASSETS, 'Characters_.png');
const OUT = process.argv[2] || path.join(ASSETS, 'atlas_party.png');
const sheet = PNG.sync.read(fs.readFileSync(SRC));

const BG = [17, 19, 16];
const TOL = 16;

const SPECS = [
  // Columns 0-5 idle/walk, 6-7 attack, 8 the ability channel.
  // Boxes are trimmed so a neighbouring frame's staff/mace doesn't bleed in.
  { name: 'mage',   y0: 33,  y1: 98,  frames: [[49,78],[86,113],[121,151],[158,187],[196,225],[232,261],
                                               [407,448],[450,478],
                                               [518,560]] },
  // cleric 6/7 are the windup and the mace thrust — NOT x486+, which is the
  // detached golden nova effect rather than a pose.
  { name: 'cleric', y0: 103, y1: 168, frames: [[99,127],[141,177],[184,218],[223,261],[265,296],[306,338],
                                               [400,438],[444,482],
                                               [519,557]] },
  { name: 'ranger', y0: 184, y1: 241, frames: [[101,136],[149,175],[189,224],[231,266],[273,307],[316,354],
                                               [406,445],[447,493],
                                               [502,545]] },
  { name: 'husk',   y0: 572, y1: 630, frames: [[14,46],[54,86],[96,127]] },
];

function isBg(r, g, b) {
  return Math.abs(r - BG[0]) <= TOL && Math.abs(g - BG[1]) <= TOL && Math.abs(b - BG[2]) <= TOL;
}

// Returns {w,h,data(RGBA),bbox} for one frame with background made transparent.
function cutFrame(x0, y0, x1, y1) {
  const w = x1 - x0 + 1, h = y1 - y0 + 1;
  const buf = Buffer.alloc(w * h * 4);
  for (let j = 0; j < h; j++) {
    for (let i = 0; i < w; i++) {
      const si = ((y0 + j) * sheet.width + (x0 + i)) * 4;
      const di = (j * w + i) * 4;
      buf[di] = sheet.data[si]; buf[di+1] = sheet.data[si+1];
      buf[di+2] = sheet.data[si+2]; buf[di+3] = 255;
    }
  }
  // flood fill background from every border pixel
  const stack = [];
  const push = (i, j) => { if (i >= 0 && j >= 0 && i < w && j < h) stack.push(i, j); };
  for (let i = 0; i < w; i++) { push(i, 0); push(i, h - 1); }
  for (let j = 0; j < h; j++) { push(0, j); push(w - 1, j); }
  const seen = new Uint8Array(w * h);
  while (stack.length) {
    const j = stack.pop(), i = stack.pop();
    if (i < 0 || j < 0 || i >= w || j >= h) continue;
    const k = j * w + i;
    if (seen[k]) continue;
    const d = k * 4;
    if (buf[d + 3] === 0) { seen[k] = 1; continue; }
    if (!isBg(buf[d], buf[d+1], buf[d+2])) continue;
    seen[k] = 1;
    buf[d + 3] = 0;
    push(i + 1, j); push(i - 1, j); push(i, j + 1); push(i, j - 1);
  }
  // tight bbox of what survived
  let minX = w, minY = h, maxX = -1, maxY = -1;
  for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) {
    if (buf[(j * w + i) * 4 + 3] > 8) {
      if (i < minX) minX = i; if (i > maxX) maxX = i;
      if (j < minY) minY = j; if (j > maxY) maxY = j;
    }
  }
  return { w, h, data: buf, bbox: { minX, minY, maxX, maxY } };
}

// pass 1: cut everything, measure
const cuts = [];
for (const spec of SPECS) {
  for (let f = 0; f < spec.frames.length; f++) {
    const [fx0, fx1] = spec.frames[f];
    const c = cutFrame(fx0, spec.y0, fx1, spec.y1);
    const bw = c.bbox.maxX - c.bbox.minX + 1, bh = c.bbox.maxY - c.bbox.minY + 1;
    // Distance from this frame's lowest pixel to the bottom of the row band.
    // Every frame in a row is cut from the same band, so this preserves the
    // artist's shared ground line — see the alignment note below.
    const gap = (spec.y1 - spec.y0) - c.bbox.maxY;
    cuts.push({ spec: spec.name, f, c, bw, bh, gap });
  }
}
// Anchor every frame to its row's IDLE pose (frame 0), which is a plain
// character with nothing hanging below the boots — so its lowest pixel is the
// feet by definition. `rel` is then how far a frame's lowest pixel sits below
// (negative) or above (positive) that ground line.
const g0 = new Map();
for (const c of cuts) if (c.f === 0) g0.set(c.spec, c.gap);
for (const c of cuts) c.rel = c.gap - g0.get(c.spec);

const CW = Math.max(...cuts.map(c => c.bw)) + 4;
// Room for the tallest frame *plus* whatever hangs below the ground line.
const CH = Math.max(...cuts.map(c => c.bh + c.rel)) + 4;
const GROUND = CH - 2;          // cell row that must land on the hero's feet
console.log(`cell = ${CW}x${CH}, ground = ${GROUND}`);

// pass 2: compose atlas — one row per character.
//
// Frames are aligned on the ROW'S SHARED GROUND LINE, not on each frame's own
// bounding box. Bottom-aligning each bbox looks right until a frame contains an
// effect that hangs below the character's feet (the mage's magic circle, the
// cleric's swirl): the effect then rests on the cell floor and the hero appears
// to levitate. Preserving each frame's distance to the band bottom keeps every
// pose standing on the same ground and lets the effect spill below it.
const rowsByName = [...new Set(cuts.map(c => c.spec))];
const cols = Math.max(...rowsByName.map(n => cuts.filter(c => c.spec === n).length));
const atlas = new PNG({ width: CW * cols, height: CH * rowsByName.length });
atlas.data.fill(0);

for (const cut of cuts) {
  const row = rowsByName.indexOf(cut.spec);
  const ox = cut.f * CW + Math.floor((CW - cut.bw) / 2);
  const oy = row * CH + (CH - 2 - cut.rel - cut.bh);
  const { c } = cut;
  for (let j = c.bbox.minY; j <= c.bbox.maxY; j++) {
    for (let i = c.bbox.minX; i <= c.bbox.maxX; i++) {
      const si = (j * c.w + i) * 4;
      if (c.data[si + 3] === 0) continue;
      const dx = ox + (i - c.bbox.minX), dy = oy + (j - c.bbox.minY);
      const di = (dy * atlas.width + dx) * 4;
      atlas.data[di] = c.data[si]; atlas.data[di+1] = c.data[si+1];
      atlas.data[di+2] = c.data[si+2]; atlas.data[di+3] = c.data[si+3];
    }
  }
  console.log(`  ${cut.spec}[${cut.f}] bbox ${cut.bw}x${cut.bh} -> row ${row}`);
}

fs.writeFileSync(OUT, PNG.sync.write(atlas));
console.log('wrote', OUT);
// These numbers must match ATLAS.party in wwwroot/js/game.js.
console.log(JSON.stringify({ cell: { w: CW, h: CH }, ground: GROUND, rows: rowsByName,
  counts: rowsByName.map(n => cuts.filter(c => c.spec === n).length) }, null, 2));
