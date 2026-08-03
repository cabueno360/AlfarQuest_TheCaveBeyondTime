// Make the outdoor props VISIBLE in Tiled.
//
//     node tools/gen-props-tsx.mjs
//
// The props' art lives in assets/Outside/atlas_outside.png, addressed by kind
// through atlas_outside.json — which Tiled cannot read, so every prop in the
// editor was a nameless point. This exports each frame as its own small PNG
// under Maps/Tilesets/props/, writes AtlasProps.tsx (an image-collection
// tileset over them), and converts the region maps' Props point-objects into
// TILE objects (gid) so they draw in the editor exactly where they stand in
// the game.
//
// The engine keeps reading the Kind/Variant/Scale properties as before — the
// gid is for Tiled's eyes. TmxMap normalises a tile object's bottom-left
// anchor to the bottom-centre point the engine has always used.
//
// Idempotent: maps already referencing AtlasProps.tsx are left alone, so it
// can be re-run after new art lands in the atlas.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { PNG } from 'pngjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const WWW = path.join(ROOT, 'src/AlfarQuest.Client/wwwroot');
const TILESETS = path.join(WWW, 'Maps/Tilesets');
const PROPS_DIR = path.join(TILESETS, 'props');
const FIRSTGID = 60000;                    // far above every real tileset; flip bits unaffected

const frames = JSON.parse(fs.readFileSync(path.join(WWW, 'assets/Outside/atlas_outside.json'), 'utf8'));
const atlas = PNG.sync.read(fs.readFileSync(path.join(WWW, 'assets/Outside/atlas_outside.png')));

// ---- 1. one PNG per frame, one tile per PNG --------------------------------
fs.mkdirSync(PROPS_DIR, { recursive: true });
const tileOf = {};                         // kind -> [tileId per variant]
const tiles = [];                          // { id, file, w, h }
let nextId = 0, maxW = 0, maxH = 0;

for (const [kind, list] of Object.entries(frames)) {
    tileOf[kind] = [];
    list.forEach((f, i) => {
        const out = new PNG({ width: f.w, height: f.h });
        PNG.bitblt(atlas, out, f.x, f.y, f.w, f.h, 0, 0);
        const file = `${kind}_${i}.png`;
        fs.writeFileSync(path.join(PROPS_DIR, file), PNG.sync.write(out));
        tiles.push({ id: nextId, file, w: f.w, h: f.h });
        tileOf[kind].push(nextId);
        maxW = Math.max(maxW, f.w); maxH = Math.max(maxH, f.h);
        nextId++;
    });
}

const tsx = [
    `<?xml version="1.0" encoding="UTF-8"?>`,
    `<tileset version="1.10" tiledversion="1.10.2" name="AtlasProps" tilewidth="${maxW}" tileheight="${maxH}" tilecount="${tiles.length}" columns="0">`,
    ` <grid orientation="orthogonal" width="1" height="1"/>`,
    ...tiles.map(t => ` <tile id="${t.id}">\n  <image source="props/${t.file}" width="${t.w}" height="${t.h}"/>\n </tile>`),
    `</tileset>`,
    ``,
].join('\n');
fs.writeFileSync(path.join(TILESETS, 'AtlasProps.tsx'), tsx);
console.log(`AtlasProps.tsx: ${tiles.length} tiles from ${Object.keys(frames).length} kinds`);

// ---- 2. point props -> tile objects in the region maps ---------------------
const MAPS = [
    'Maps/Regions/R1_Ashwold.tmx',
    'Maps/Regions/R2_WhisperingWood.tmx',
    'Maps/Regions/R3_Deepdelve.tmx',
    'Maps/Regions/R4_KaeYchelRoad.tmx',
];

for (const rel of MAPS) {
    const p = path.join(WWW, rel);
    let xml = fs.readFileSync(p, 'utf8');
    if (xml.includes('AtlasProps.tsx')) { console.log(`${rel}: already converted`); continue; }

    // Reference the tileset after the last existing one.
    const lastTs = xml.lastIndexOf('<tileset ');
    const lineEnd = xml.indexOf('\n', xml.indexOf('/>', lastTs));
    xml = xml.slice(0, lineEnd + 1)
        + ` <tileset firstgid="${FIRSTGID}" source="../Tilesets/AtlasProps.tsx"/>\n`
        + xml.slice(lineEnd + 1);

    // Convert the Props objects that have art. h_* house pieces and anything
    // the atlas does not know stay as points.
    const gStart = xml.search(/<objectgroup [^>]*name="Props"[^>]*>/);
    if (gStart < 0) { fs.writeFileSync(p, xml); console.log(`${rel}: tileset added (no Props layer)`); continue; }
    const gEnd = xml.indexOf('</objectgroup>', gStart);
    let converted = 0;
    const group = xml.slice(gStart, gEnd).replace(/  <object ([^>]*)>\r?\n([\s\S]*?)  <\/object>\r?\n/g,
        (block, attrs, body) => {
            const kind = (body.match(/name="Kind" value="([A-Za-z_]+)"/) || [])[1];
            if (!kind || !tileOf[kind] || !tileOf[kind].length) return block;
            const variant = parseInt((body.match(/name="Variant" value="(-?\d+)"/) || [])[1] ?? '0', 10) || 0;
            const scale = parseFloat((body.match(/name="Scale" value="([0-9.]+)"/) || [])[1] ?? '1') || 1;
            const f = frames[kind][((variant % frames[kind].length) + frames[kind].length) % frames[kind].length];
            const gid = FIRSTGID + tileOf[kind][((variant % tileOf[kind].length) + tileOf[kind].length) % tileOf[kind].length];
            // Engine pixels are 2x map pixels; the editor footprint is half the
            // drawn size. Anchor: point (bottom-centre) -> tile bottom-left.
            const w = +(f.w * scale / 2).toFixed(2), h = +(f.h * scale / 2).toFixed(2);
            const x = parseFloat((attrs.match(/x="(-?[0-9.]+)"/) || [])[1] ?? '0');
            const y = parseFloat((attrs.match(/y="(-?[0-9.]+)"/) || [])[1] ?? '0');
            const rest = attrs.replace(/ ?x="-?[0-9.]+"/, '').replace(/ ?y="-?[0-9.]+"/, '');
            converted++;
            const flip = /name="Flip" value="true"/.test(body) ? 0x80000000 : 0;
            return `  <object ${rest} gid="${(gid | flip) >>> 0}" x="${+(x - w / 2).toFixed(2)}" y="${y}" width="${w}" height="${h}">\n`
                + body.replace(/   <point\/>\r?\n/, '')
                + `  </object>\n`;
        });
    xml = xml.slice(0, gStart) + group + xml.slice(gEnd);
    fs.writeFileSync(p, xml);
    console.log(`${rel}: ${converted} props now visible in Tiled`);
}
