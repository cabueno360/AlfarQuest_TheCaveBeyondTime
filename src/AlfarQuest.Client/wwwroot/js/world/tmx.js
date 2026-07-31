// =====================================================================
//  Reading a Tiled .tmx for DRAWING.
//
//  The engine already parses the same file in C# for collision and for the
//  objects it spawns; this is the other half — the picture. Both read the one
//  map, so what Tiled shows, the game plays and draws.
//
//  Only what our maps use is handled: orthogonal, finite, external tilesets,
//  and layer data in base64 + zlib (Tiled's default) or CSV.
// =====================================================================

/// Decodes one layer's tile words. `zlib` is what Tiled writes; the browser can
/// inflate it natively, so there is no library here.
///
/// The words keep Tiled's flip flags in their top bits — a tile the author
/// mirrored must draw mirrored, so the bits are split from the gid where the
/// tile is drawn, not thrown away here. (They used to be masked off, which made
/// every flipped cliff edge and roof piece silently face the wrong way.)
async function decodeLayer(dataEl, count) {
    const encoding = dataEl.getAttribute("encoding");
    if (encoding === "csv")
        return Uint32Array.from(dataEl.textContent.split(",").slice(0, count), s => parseInt(s, 10) >>> 0);
    if (encoding !== "base64") throw new Error(`tmx: unsupported encoding ${encoding}`);

    const bin = atob(dataEl.textContent.trim());
    let bytes = Uint8Array.from(bin, c => c.charCodeAt(0));

    const compression = dataEl.getAttribute("compression");
    if (compression) {
        // "zlib" is a zlib wrapper, which is what DecompressionStream("deflate")
        // expects; "gzip" is its own format.
        const fmt = compression === "gzip" ? "gzip" : "deflate";
        const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream(fmt));
        bytes = new Uint8Array(await new Response(stream).arrayBuffer());
    }
    // Read as one typed view rather than a DataView call per tile: these layers
    // are 102,400 tiles each and the per-call overhead dominated the load.
    const n = Math.min(count, bytes.byteLength >> 2);
    const words = new Uint32Array(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + n * 4));
    const ids = new Uint32Array(count);
    ids.set(words.subarray(0, n));
    return ids;
}

// Tiled packs these into each tile word's top bits (TMX format, "Tile flipping").
const FLIP_H = 0x80000000, FLIP_V = 0x40000000, FLIP_D = 0x20000000, GID_MASK = 0x0fffffff;

const loadImage = (src) => new Promise((res, rej) => {
    const im = new Image();
    im.onload = () => res(im);
    im.onerror = () => rej(new Error(`tmx: image failed ${src}`));
    im.src = src;
});

/// Resolves a path that a .tmx or .tsx gave relative to itself.
const resolve = (base, rel) => new URL(rel, new URL(base, location.href)).href;

/// Loads a map and everything it needs to be drawn. Returns null on any failure —
/// a map that will not load must never stop the game starting; the renderer falls
/// back to the art it used before.
export async function loadTmx(url) {
    try {
        const text = await (await fetch(encodeURI(url))).text();
        if (!text.trimStart().startsWith("<?xml")) return null;   // not a map (SPA fallback)
        const doc = new DOMParser().parseFromString(text, "application/xml");
        const map = doc.querySelector("map");
        if (!map) return null;

        const width = +map.getAttribute("width"), height = +map.getAttribute("height");
        const tileW = +map.getAttribute("tilewidth"), tileH = +map.getAttribute("tileheight");

        // Tilesets and layers are independent, so they are resolved together
        // rather than one after another — done serially this took over ten
        // seconds and the map arrived long after the player did.
        const tilesets = await Promise.all(
            [...map.querySelectorAll(":scope > tileset")].map(async (ts) => {
                // A tileset is either external — a `source` pointing at a .tsx — or
                // written into the map itself, which is what Tiled saves when the
                // author imports a sheet without exporting it. Both are read here:
                // an embedded one has no `source`, and fetching that null took the
                // whole map down, falling the region back to procedural art.
                const src = ts.getAttribute("source");
                let el = ts, base = url;
                if (src) {
                    base = resolve(url, src);
                    el = new DOMParser().parseFromString(
                        await (await fetch(encodeURI(base))).text(), "application/xml")
                        .querySelector("tileset");
                    if (!el) throw new Error(`tmx: tileset failed ${src}`);
                }
                // An embedded tileset gives its image relative to the map; an
                // external one relative to the .tsx. `base` is already whichever.
                return {
                    firstgid: +ts.getAttribute("firstgid"),
                    columns: +el.getAttribute("columns"),
                    tw: +el.getAttribute("tilewidth"), th: +el.getAttribute("tileheight"),
                    image: await loadImage(resolve(base, el.querySelector("image").getAttribute("source"))),
                };
            }));
        tilesets.sort((a, b) => a.firstgid - b.firstgid);

        const layers = await Promise.all(
            [...map.querySelectorAll(":scope > layer")]
                .filter((el) => el.querySelector("data"))
                .map(async (el) => ({
                    name: el.getAttribute("name"),
                    visible: el.getAttribute("visible") !== "0",
                    data: await decodeLayer(el.querySelector("data"), width * height),
                })));
        return { width, height, tileW, tileH, tilesets, layers };
    } catch (err) {
        console.warn("tmx: not drawing from the map —", err.message);
        return null;
    }
}

/// Paints the map's tile layers onto a context, in file order. `skip` names layers
/// to leave out — the ones drawn above the actors rather than under them. Pass
/// `only` instead to paint JUST those layers (the above-player canvas): a layer is
/// kept when it is in `only`, or — when `only` is null — when it is not in `skip`.
export function drawTmxLayers(g, tmx, skip = [], only = null) {
    const { width, height, tileW, tileH, tilesets, layers } = tmx;
    // Resolving a gid to its tileset is the hot path here — hundreds of thousands
    // of tiles — so the lookup walks a short sorted list backwards rather than
    // searching per tile.
    const setFor = (gid) => {
        for (let i = tilesets.length - 1; i >= 0; i--)
            if (gid >= tilesets[i].firstgid) return tilesets[i];
        return null;
    };
    for (const layer of layers) {
        if (!layer.visible) continue;
        if (only ? !only.includes(layer.name) : skip.includes(layer.name)) continue;
        const d = layer.data;
        for (let y = 0; y < height; y++) {
            const row = y * width;
            for (let x = 0; x < width; x++) {
                const word = d[row + x];
                if (!word) continue;
                const gid = word & GID_MASK;
                const ts = setFor(gid);
                if (!ts) continue;
                const i = gid - ts.firstgid;
                const sx = (i % ts.columns) * ts.tw, sy = ((i / ts.columns) | 0) * ts.th;
                // A tile taller than the grid (a whole tree in one tile) hangs up
                // and to the left of its cell, the way Tiled draws it.
                const dx = x * tileW, dy = y * tileH - (ts.th - tileH);
                if (!(word & (FLIP_H | FLIP_V | FLIP_D))) {
                    g.drawImage(ts.image, sx, sy, ts.tw, ts.th, dx, dy, ts.tw, ts.th);
                    continue;
                }
                // A flipped tile, drawn mirrored about its own centre. Tiled's
                // order is: diagonal (axis swap) first, then horizontal, then
                // vertical — canvas transforms compose so the last call applies
                // first, hence V, H, D here.
                g.save();
                g.translate(dx + ts.tw / 2, dy + ts.th / 2);
                if (word & FLIP_V) g.scale(1, -1);
                if (word & FLIP_H) g.scale(-1, 1);
                if (word & FLIP_D) g.transform(0, 1, 1, 0, 0, 0);
                g.drawImage(ts.image, sx, sy, ts.tw, ts.th, -ts.tw / 2, -ts.th / 2, ts.tw, ts.th);
                g.restore();
            }
        }
    }
}
