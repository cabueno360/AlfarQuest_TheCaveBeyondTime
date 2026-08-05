// =====================================================================
//  The floor, painted once per world build into an offscreen canvas.
//  Tiling it live would be thousands of drawImage calls per frame.
// =====================================================================
import { ATLAS } from "../atlas.js";
import { mulberry32, route, distToRoutes } from "../rng.js";
import { FLOOR_TILES, PATH_TILE, ARCH, ORE_CELLS, BRIDGE_TILE, WALL, OUT } from "./tiles.js";
import { ctx } from "../gfx.js";
import { drawTmxLayers } from "./tmx.js";

// Stage 1's authored map, once it has loaded. While this is null the stage draws
// itself the old way — from terrain samples keyed to the tile type — so the game
// is never waiting on a file to show a world.
// Keyed by map id — Stage 1 and each migrated interior. The engine sends which
// one is loaded (RenderState.mapId), so picture and geometry come from one file.
const maps = {};
// The painted maps, kept once drawn. Stage 1's ground alone is hundreds of
// thousands of tiles and none of them change, so repainting on every world
// rebuild — which happens each time a door is stepped through — was pure waste.
const painted = {};
// The above-player slice of each map, cached the same way — only the tall things
// (roofs, canopies) that must draw OVER an actor standing behind them.
const aboveMaps = {};
export function setStageMap(id, tmx) { maps[id] = tmx; delete painted[id]; delete aboveMaps[id]; }
export function mapFor(id) { return (id && maps[id]) || null; }
/// Whether the place currently loaded is drawn from a map rather than from code.
export function hasStageMap(id) { return mapFor(id) !== null; }

/// Layers drawn OVER the actors rather than under them. Only the explicit
/// `AbovePlayer` layer qualifies. Houses (the `Buildings` layer) do NOT: an actor
/// always draws in FRONT of a house — over its walls and roof alike — and never
/// vanishes behind it, because the house is SOLID and cannot be walked into. Roof
/// occlusion was tried and dropped: it hid actors standing right where you expect
/// to see them (in front of the wall, or behind a fence).
const ABOVE = ["AbovePlayer"];

/// The above-player canvas for the loaded map — JUST the ABOVE layers, transparent
/// everywhere else — cached like the floor. Drawn after the actors (drawStageAbove).
export function buildAboveCanvas(s) {
    const tmx = mapFor(s.mapId);
    if (!tmx) return null;
    if (aboveMaps[s.mapId]) return aboveMaps[s.mapId];
    const c = document.createElement("canvas");
    c.width = Math.ceil(s.chamberW / 32) * 32;
    c.height = Math.ceil(s.chamberH / 32) * 32;
    const g = c.getContext("2d");
    g.imageSmoothingEnabled = false;
    drawTmxLayers(g, tmx, [], ABOVE);   // only the above-player layers
    aboveMaps[s.mapId] = c;
    return c;
}

/// Blits the above-player canvas over whatever has been drawn — call it after the
/// actors so roofs and canopies hide those standing behind them. Same camera-slice
/// blit as drawFloor.
export function drawStageAbove(view, aboveCanvas) {
    if (!aboveCanvas) return;
    ctx.save();
    ctx.imageSmoothingEnabled = false;
    const vx = Math.max(0, view.x0), vy = Math.max(0, view.y0);
    const vw = Math.min(aboveCanvas.width - vx, view.x1 - vx);
    const vh = Math.min(aboveCanvas.height - vy, view.y1 - vy);
    if (vw > 0 && vh > 0) ctx.drawImage(aboveCanvas, vx, vy, vw, vh, vx, vy, vw, vh);
    ctx.restore();
}

// Stage 1 floor: grass, dirt road, river, bridge deck and the mountain.
export function buildOutdoorCanvas(s) {
    const T = 32, W = s.chamberW, H = s.chamberH;
    if (!W || !H) return null;
    const cols = Math.ceil(W / T), rows = Math.ceil(H / T);
    const c = document.createElement("canvas");
    c.width = cols * T; c.height = rows * T;
    const g = c.getContext("2d");
    g.imageSmoothingEnabled = false;

    const authored = mapFor(s.mapId);
    if (authored) return paintMap(s.mapId, authored, c, g);
    const img = ATLAS.outTiles.img;
    const rnd = mulberry32(0x5EED1);
    const map = s.map || [];
    const at = (x, y) => (x < 0 || y < 0 || y >= map.length || x >= (map[0] || "").length) ? "#" : map[y][x];

    // Mirroring half the tiles breaks up the grain that survives even with six
    // samples — the eye reads a repeated directional texture as stripes long
    // before it notices the tile itself repeating.
    const put = (set, tx, ty, weightToFirst = 1) => {
        const pick = weightToFirst > 1
            ? Math.floor(Math.pow(rnd(), weightToFirst) * set.length)
            : Math.floor(rnd() * set.length);
        const [sx, sy] = set[Math.min(pick, set.length - 1)];
        if (rnd() < 0.5) {
            g.drawImage(img, sx, sy, T, T, tx * T, ty * T, T, T);
        } else {
            g.save();
            g.translate(tx * T + T, ty * T);
            g.scale(-1, 1);
            g.drawImage(img, sx, sy, T, T, 0, 0, T, T);
            g.restore();
        }
    };
    for (let ty = 0; ty < rows; ty++)
        for (let tx = 0; tx < cols; tx++) {
            switch (at(tx, ty)) {
                case "#": put(OUT.mountain, tx, ty); break;
                case ",": put(OUT.path, tx, ty); break;
                case "~": put(OUT.water, tx, ty); break;
                case "=": put(OUT.grass, tx, ty, 1.8); put(OUT.bridge, tx, ty); break;
                default:  put(OUT.grass, tx, ty, 1.8); break;
            }
        }
    // Mountain edges: a stone face where the range meets open ground, so the
    // barrier reads as a cliff rather than as a differently-coloured field.
    for (let ty = 0; ty < rows; ty++)
        for (let tx = 0; tx < cols; tx++) {
            if (at(tx, ty) !== "#") continue;
            const south = at(tx, ty + 1) !== "#";
            if (!south && at(tx - 1, ty) === "#" && at(tx + 1, ty) === "#" && at(tx, ty - 1) === "#") continue;
            put(OUT.cliffFace, tx, ty);
            if (south) {
                const grad = g.createLinearGradient(0, ty * T + T, 0, ty * T + T * 1.9);
                grad.addColorStop(0, "rgba(0,0,0,0.5)");
                grad.addColorStop(1, "rgba(0,0,0,0)");
                g.fillStyle = grad;
                g.fillRect(tx * T, ty * T + T, T, T * 0.9);
            }
        }

    // A wash of warm light over the finished ground.
    //
    // A flat blend toward one colour lifts the floor AND compresses its own
    // contrast by the same fraction — new = (1-a)·old + a·tint — which is what
    // was actually wanted. The tiles are a checkerboard of dirt and grass with
    // flowers scattered through them, and at full contrast a green-brown Thief
    // standing on green-brown ground disappeared into it.
    //
    // Kept low. Past about a quarter the ground stops reading as ground.
    g.fillStyle = "rgba(236, 226, 200, 0.20)";
    g.fillRect(0, 0, c.width, c.height);

    return c;
}

/// Paints a map into the canvas and keeps it. The map's own grid is 16px against
/// the engine's 32px cell, and its pixel size matches the canvas exactly, so a
/// cell lands where the author put it.
function paintMap(id, tmx, c, g) {
    if (painted[id]) return painted[id];
    drawTmxLayers(g, tmx, ABOVE);
    painted[id] = c;
    return c;
}

export function buildFloorCanvas(s) {
    if (s.outdoor) return buildOutdoorCanvas(s);   // overworld, and open-air interiors (a city)

    // A building authored in Tiled draws from its map, exactly as Stage 1 does.
    // Its lamps and hearths still light it — the darkness pass is untouched, so a
    // house painted from a map is still a house lit from within.
    const authored = mapFor(s.mapId);
    if (authored) {
        const c0 = document.createElement("canvas");
        c0.width = Math.ceil(s.chamberW / 32) * 32; c0.height = Math.ceil(s.chamberH / 32) * 32;
        const g0 = c0.getContext("2d");
        g0.imageSmoothingEnabled = false;
        return paintMap(s.mapId, authored, c0, g0);
    }
    const T = ATLAS.caves.cw;
    const W = s.chamberW, H = s.chamberH;
    if (!W || !H) return null;
    const cols = Math.ceil(W / T), rows = Math.ceil(H / T);
    const map = s.map || [];
    // '#' rock, '.' floor, '~' water, '=' bridge
    const at = (x, y) =>
        x < 0 || y < 0 || y >= map.length || x >= (map[0] || "").length ? "#" : map[y][x];
    const isWall = (x, y) => at(x, y) === "#";
    const isWet  = (x, y) => at(x, y) === "~";
    const c = document.createElement("canvas");
    c.width = cols * T; c.height = rows * T;
    const g = c.getContext("2d");
    g.imageSmoothingEnabled = false;

    const img = ATLAS.caves.img;
    const rnd = mulberry32(0xCA7E5 + (s.level || 1) * 7919);

    // A home lays its own floorboards over the carved floor instead of cave stone;
    // its walls stay the same rock (a cottage's stone lower storey). If the tile is
    // not loaded yet, fall through to the cave floor rather than leaving holes.
    const houseWood = s.floorStyle === "wood" && ATLAS.houseWood.ready ? ATLAS.houseWood.img : null;
    // Within a boarded home, a cell the builder laid as PATH (',') is flagged
    // stone instead — the kitchen, per the blueprint. One interior, two surfaces.
    const houseStone = houseWood && ATLAS.houseStone.ready ? ATLAS.houseStone.img : null;

    const total = FLOOR_TILES.reduce((n, t) => n + t.w, 0);
    const pick = (r) => {
        let acc = r * total;
        for (const t of FLOOR_TILES) { acc -= t.w; if (acc <= 0) return t.cell; }
        return FLOOR_TILES[0].cell;
    };
    for (let ty = 0; ty < rows; ty++)
        for (let tx = 0; tx < cols; tx++) {
            if (isWall(tx, ty)) continue;                // rock gets painted later
            if (houseWood) {
                const surface = (at(tx, ty) === "," && houseStone) ? houseStone : houseWood;
                g.drawImage(surface, 0, 0, T, T, tx * T, ty * T, T, T);
                continue;
            }
            const [cx, cy] = pick(rnd());
            g.drawImage(img, cx * T, cy * T, T, T, tx * T, ty * T, T, T);
            const c = at(tx, ty);
            if (c === "~") {
                g.fillStyle = "rgba(24,64,74,0.82)";     // standing water
                g.fillRect(tx * T, ty * T, T, T);
            } else if (c === "=") {
                g.drawImage(img, BRIDGE_TILE[0] * T, BRIDGE_TILE[1] * T, T, T, tx * T, ty * T, T, T);
            }
        }

    // Routes: the spawn to the exit, plus spurs to where the crystals cluster —
    // the places a delving party would actually have reason to walk.
    const crystals = s.ents.filter(e => e.t === "crystal");
    const routes = [];
    if (s.spawn && s.exit) routes.push(route(s.spawn.x, s.spawn.y, s.exit.x, s.exit.y, rnd, 110));
    for (let i = 0; i < 3 && crystals.length && routes.length; i++) {
        const c1 = crystals[Math.floor(rnd() * crystals.length)];
        const from = routes[0][Math.floor(rnd() * routes[0].length)];
        routes.push(route(from.x, from.y, c1.x, c1.y + c1.r, rnd, 60));
    }

    // Feathered edge: full-strength core, fading band outside it, so the path
    // blends into the rock instead of ending on a hard tile boundary.
    const CORE = 34, FADE = 40;
    if (routes.length && !houseWood)
        for (let ty = 0; ty < rows; ty++)
            for (let tx = 0; tx < cols; tx++) {
                if (at(tx, ty) !== ".") continue;
                const d = distToRoutes(tx * T + T / 2, ty * T + T / 2, routes);
                if (d > CORE + FADE) continue;
                const a = d <= CORE ? 1 : 1 - (d - CORE) / FADE;
                g.globalAlpha = a * (0.75 + rnd() * 0.25);   // slight mottling
                g.drawImage(img, PATH_TILE[0] * T, PATH_TILE[1] * T, T, T, tx * T, ty * T, T, T);
            }
    g.globalAlpha = 1;

    // ---- rock ----
    const put = (cell, tx, ty) => g.drawImage(img, cell[0] * T, cell[1] * T, T, T, tx * T, ty * T, T, T);

    // Homes keep the cave's rock walls, deliberately. A flat coursed-block tile was
    // tried here and reverted: without the rim cells a wall sits at the same value
    // as the lit floorboards and the rooms stop reading as rooms. The rock's dark
    // mass plus its lit rims is what separates one room from the next.
    for (let ty = 0; ty < rows; ty++)
        for (let tx = 0; tx < cols; tx++) {
            if (!isWall(tx, ty)) continue;
            put(WALL.fill, tx, ty);

            const n = !isWall(tx, ty - 1), s2 = !isWall(tx, ty + 1);
            const w = !isWall(tx - 1, ty), e = !isWall(tx + 1, ty);

            // Two adjacent open sides make a corner, not two overlapping edges.
            if (n && w) put(WALL.nw, tx, ty);
            else if (n && e) put(WALL.ne, tx, ty);
            else if (s2 && w) put(WALL.sw, tx, ty);
            else if (s2 && e) put(WALL.se, tx, ty);
            else {
                if (n) put(WALL.n, tx, ty);
                if (s2) put(WALL.s, tx, ty);
                if (w) put(WALL.w, tx, ty);
                if (e) put(WALL.e, tx, ty);
            }

            // Diagonal-only opening: the rim has to turn back on itself, so
            // stamp the matching corner or the wall shows a square notch.
            if (!n && !w && !isWall(tx - 1, ty - 1)) put(WALL.nw, tx, ty);
            if (!n && !e && !isWall(tx + 1, ty - 1)) put(WALL.ne, tx, ty);
            if (!s2 && !w && !isWall(tx - 1, ty + 1)) put(WALL.sw, tx, ty);
            if (!s2 && !e && !isWall(tx + 1, ty + 1)) put(WALL.se, tx, ty);
        }

    // Catch-light on every rock face that borders open floor. The tileset's rock
    // is unlit — its brightest cell measures 59/255 — so under the cave's
    // darkness an exposed wall was indistinguishable from empty black, and the
    // player could not tell a wall from somewhere they simply had not lit yet.
    g.save();
    g.globalCompositeOperation = "lighter";
    for (let ty = 0; ty < rows; ty++)
        for (let tx = 0; tx < cols; tx++) {
            if (!isWall(tx, ty)) continue;
            const open = (!isWall(tx, ty - 1) ? 1 : 0) + (!isWall(tx, ty + 1) ? 1 : 0)
                       + (!isWall(tx - 1, ty) ? 1 : 0) + (!isWall(tx + 1, ty) ? 1 : 0);
            if (!open) continue;
            g.fillStyle = !isWall(tx, ty + 1)
                ? "rgba(96,74,44,0.42)"      // south face catches the most
                : "rgba(58,50,40,0.26)";
            g.fillRect(tx * T, ty * T, T, T);
        }
    g.restore();

    // Ore seams on the exposed rock faces. Only south-facing walls get them —
    // that is the face actually turned toward the player, so a seam anywhere
    // else would be hidden inside the rock mass.
    const ores = ATLAS.ores;
    if (ores.ready && !houseWood)                          // no ore veins in a home's stone walls
        for (let ty = 0; ty < rows; ty++)
            for (let tx = 0; tx < cols; tx++) {
                if (!isWall(tx, ty) || isWall(tx, ty + 1)) continue;
                if (rnd() > 0.16) continue;
                const [oc, orr] = ORE_CELLS[Math.floor(rnd() * ORE_CELLS.length)];
                const sc = 0.30 + rnd() * 0.10;
                const dw = ores.cw * sc, dh = ores.ch * sc;
                g.drawImage(ores.img, oc * ores.cw, orr * ores.ch, ores.cw, ores.ch,
                            tx * T + (T - dw) / 2, ty * T + T - dh * 0.86, dw, dh);
            }

    // Cave mouths at the region entrances, drawn last so the opening sits over
    // the wall rather than under it.
    for (const a of (s.arches || [])) {
        const ax = a.x - (ARCH.w * T) / 2;
        const ay = a.y - ARCH.h * T + T * 1.5;
        g.drawImage(img, ARCH.c * T, ARCH.r * T, ARCH.w * T, ARCH.h * T,
                    ax, ay, ARCH.w * T, ARCH.h * T);
    }

    // Soft drop shadow on the floor under every south-facing wall face.
    for (let ty = 0; ty < rows; ty++)
        for (let tx = 0; tx < cols; tx++) {
            if (isWall(tx, ty) || !isWall(tx, ty - 1)) continue;
            const grad = g.createLinearGradient(0, ty * T, 0, ty * T + T * 0.7);
            grad.addColorStop(0, "rgba(0,0,0,0.55)");
            grad.addColorStop(1, "rgba(0,0,0,0)");
            g.fillStyle = grad;
            g.fillRect(tx * T, ty * T, T, T * 0.7);
        }
    return c;
}

export function drawFloor(s, view, floorCanvas) {
    const W = (s && s.chamberW) || 1800, H = (s && s.chamberH) || 1100;
    if (floorCanvas) {
        ctx.save();
        ctx.imageSmoothingEnabled = false;
        // Only the slice under the camera. Blitting the whole map every frame
        // costs the same whether you can see it or not, and on the 80x80
        // outdoor map that alone is a 2560x2560 copy per frame.
        const vx = Math.max(0, view.x0), vy = Math.max(0, view.y0);
        const vw = Math.min(floorCanvas.width - vx, view.x1 - vx);
        const vh = Math.min(floorCanvas.height - vy, view.y1 - vy);
        if (vw > 0 && vh > 0) ctx.drawImage(floorCanvas, vx, vy, vw, vh, vx, vy, vw, vh);
        // Cool wash over the tiles so the cistern still reads as cold and wet
        // rather than as a lit dungeon floor — but a home's floorboards get a warm
        // hearthlight wash instead, so the room reads as lived-in, not a cellar.
        if (s && !s.outdoor) {
            // A depth can name its own wash (the coral shore's warm rose);
            // otherwise homes read warm and the cave reads cold and wet.
            ctx.fillStyle = s.caveTint
                ? s.caveTint
                : s.floorStyle === "wood"
                    ? "rgba(48,30,12,0.16)"            // warm, lived-in
                    : "rgba(10,14,40,0.26)";           // cold, wet cave tint
            ctx.fillRect(0, 0, floorCanvas.width, floorCanvas.height);
        }
        ctx.restore();
        return;
    }
    // Fallback until the tileset finishes loading.
    const g = ctx.createRadialGradient(W / 2, H / 2, 60, W / 2, H / 2, 1200);
    g.addColorStop(0, "#12112a");
    g.addColorStop(1, "#06050f");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, W, H);
}
