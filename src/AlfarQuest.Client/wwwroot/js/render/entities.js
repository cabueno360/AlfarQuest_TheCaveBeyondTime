// =====================================================================
//  One draw function per entity kind, dispatched by drawEntity().
// =====================================================================
import { ATLAS, animFrame, drawSprite, getOutsideFrames, getCharFrames } from "../atlas.js";
import { CRYSTAL_CELLS } from "../world/tiles.js";
import { ctx, hexA } from "../gfx.js";

/// Draw actors from the outlined copy of their atlas. The rim itself is baked in
/// at load time — see makeOutlined — so this costs nothing per frame.
const OUTLINE = true;

export function drawEntity(e) {
    switch (e.t) {
        case "crystal": drawCrystal(e); break;
        case "husk": drawHusk(e); break;
        case "prop": drawProp(e); break;
        case "npc": drawNpc(e); break;
        case "hero": drawHero(e); break;
        case "proj": drawProjectile(e); break;
        case "slash": drawSlash(e); break;
        case "nova": drawNova(e); break;
        case "particle": drawParticle(e); break;
        case "exit": drawExit(e); break;
    }
}

function drawCrystal(e) {
    const { x, y, r } = e;
    const a = ATLAS.ores;
    if (!a.ready) return;
    // Stable per-crystal variant: keyed off its own position so the gallery
    // varies between spires but never flickers between frames.
    const idx = Math.abs(Math.floor(x * 0.11 + y * 0.07)) % CRYSTAL_CELLS.length;
    const [col, row] = CRYSTAL_CELLS[idx];
    const scale = (r * 3.6) / a.ch;
    drawSprite(a, col, row, x, y + r * 0.55, scale, (idx % 2) === 1, 1, 0);
}

function drawProp(e) {
    if (e.kind) return drawOutProp(e);
    const a = ATLAS.deco;
    if (!a.ready) return;
    // Grounding shadow, scaled to the prop so big ruins read as heavier.
    ctx.fillStyle = "rgba(0,0,0,0.38)";
    ctx.beginPath();
    ctx.ellipse(e.x, e.y - 3, a.cw * e.s * 0.32, a.cw * e.s * 0.11, 0, 0, 7);
    ctx.fill();
    drawSprite(a, e.cell[0], e.cell[1], e.x, e.y, e.s, e.flip, 1, 0);
}

// Stage 1 props come from the packed outdoor atlas, addressed by name. Frames
// vary in size, so each is drawn from its own box, bottom-anchored on the tile
// it stands on.
function drawOutProp(e) {
    const a = ATLAS.outside;
    const frames = getOutsideFrames();
    if (!a.ready || !frames) return;
    const list = frames[e.kind];
    if (!list || !list.length) return;
    const f = list[e.v % list.length];
    const dw = f.w * e.s, dh = f.h * e.s;

    ctx.fillStyle = "rgba(0,0,0,0.28)";
    ctx.beginPath();
    ctx.ellipse(e.x, e.y - 2, dw * 0.30, dw * 0.11, 0, 0, 7);
    ctx.fill();

    ctx.save();
    ctx.imageSmoothingEnabled = false;
    ctx.translate(e.x, 0);
    if (e.flip) ctx.scale(-1, 1);
    ctx.drawImage(a.img, f.x, f.y, f.w, f.h, -dw / 2, e.y - dh, dw, dh);
    ctx.restore();
}

// A villager. Two poses in the atlas; the second is used when facing left, so
// they appear to turn toward whoever is speaking to them.
function drawNpc(e) {
    const a = ATLAS.chars;
    const frames = getCharFrames();
    if (!a.ready || !frames) return;
    const list = frames[e.kind];
    if (!list || !list.length) return;

    const facingLeft = Math.cos(e.f) < 0;
    const f = list[facingLeft && list.length > 1 ? 1 : 0];
    const s = 1.15;
    const dw = f.w * s, dh = f.h * s;

    groundShadow(e.x, e.y - 2, dw * 0.40, dw * 0.15);
    drawFramed(a, f, e.x, e.y, dw, dh, facingLeft);

    // A quiet marker so a villager reads as approachable from a distance.
    if (e.inReach) {
        ctx.fillStyle = "rgba(240,217,154,0.9)";
        ctx.font = "bold 13px 'EB Garamond', serif";
        ctx.textAlign = "center"; ctx.textBaseline = "bottom";
        ctx.fillText("!", e.x, e.y - dh - 6);
    }
}

// A wildlife creature, drawn from the character atlas by species.
function drawCreature(e) {
    const a = ATLAS.chars;
    const frames = getCharFrames();
    if (!a.ready || !frames) return;
    const list = frames[e.name];
    if (!list || !list.length) return;

    // Cycle the species' frames by distance travelled, like the heroes.
    const f = list[animFrame("mob:" + e._i, e, list.length, 11)];
    const s = (e.s || 1) * 1.1;
    const dw = f.w * s, dh = f.h * s;
    const foot = e.y + (e.r || 15) * 0.9;

    groundShadow(e.x, foot - 2, dw * 0.42, dw * 0.15);
    drawFramed(a, f, e.x, foot, dw, dh, false);

    ctx.save();
    ctx.imageSmoothingEnabled = false;
    if (e.flash > 0) {
        ctx.globalAlpha = Math.min(0.8, e.flash * 6);
        ctx.fillStyle = "#ffffff";
        ctx.fillRect(e.x - dw / 2, foot - dh, dw, dh);
    }
    ctx.restore();

    if (e.hp < e.mhp) {
        const w = Math.max(24, dw * 0.7);
        ctx.fillStyle = "rgba(0,0,0,0.6)"; ctx.fillRect(e.x - w / 2, foot - dh - 8, w, 4);
        ctx.fillStyle = "#c85a8a"; ctx.fillRect(e.x - w / 2, foot - dh - 8, w * (e.hp / e.mhp), 4);
    }
}

// Flat on the floor, so no shadow and no depth sorting.
export function drawDecal(e) {
    const a = ATLAS.deco;
    if (!a.ready) return;
    const w = a.cw * e.s, h = a.ch * e.s;
    ctx.save();
    ctx.imageSmoothingEnabled = false;
    ctx.globalAlpha = 0.5;
    ctx.drawImage(a.img, e.cell[0] * a.cw, e.cell[1] * a.ch, a.cw, a.ch,
                  e.x - w / 2, e.y - h / 2, w, h);
    ctx.restore();
}

function drawHusk(e) {
    const { x, y, r } = e;
    const a = ATLAS.party;
    if (!a.ready) return;

    groundShadow(x, y + r * 0.95, r * 0.9, r * 0.32);

    // Keyed by list index, not position: husks move, so a position-derived key
    // would mint a new clock every frame (no animation, and an unbounded Map).
    const f = animFrame("husk:" + e._i, e, a.frames.husk, 9);
    drawSprite(a, f, a.rows.husk, x, y + r * 0.95, 1, false, 1, e.flash, OUTLINE);

    // the crystal that rides in them, glowing through the ribs
    ctx.save();
    ctx.globalCompositeOperation = "lighter";
    const g = ctx.createRadialGradient(x, y - r * 0.2, 0, x, y - r * 0.2, r * 0.9);
    g.addColorStop(0, "rgba(150,210,255,0.35)");
    g.addColorStop(1, "rgba(90,150,255,0)");
    ctx.fillStyle = g;
    ctx.beginPath(); ctx.arc(x, y - r * 0.2, r * 0.9, 0, 7); ctx.fill();
    ctx.restore();

    if (e.hp < e.mhp) {
        ctx.fillStyle = "rgba(0,0,0,0.6)"; ctx.fillRect(x - r, y - r - 34, r * 2, 4);
        ctx.fillStyle = "#c85a8a"; ctx.fillRect(x - r, y - r - 34, r * 2 * (e.hp / e.mhp), 4);
    }
}

function drawHero(e) {
    const { x, y, r } = e;
    const a = ATLAS.party;
    const row = a.rows[e.name];
    if (!a.ready || row === undefined) return;

    groundShadow(x, y + r * 0.95, r * 0.95, r * 0.34);

    // the active hero stands in a gilded rune circle
    if (e.active && !e.dead) {
        ctx.strokeStyle = "rgba(240,217,154,0.85)"; ctx.lineWidth = 2.5;
        ctx.beginPath(); ctx.ellipse(x, y + r * 0.95, r * 1.1, r * 0.38, 0, 0, 7); ctx.stroke();
        ctx.strokeStyle = "rgba(240,217,154,0.22)"; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.ellipse(x, y + r * 0.95, r * 1.45, r * 0.52, 0, 0, 7); ctx.stroke();
    }
    // dodge i-frames read as a crystalline shimmer
    if (e.iframe > 0) {
        ctx.strokeStyle = "rgba(190,225,255,0.8)"; ctx.lineWidth = 2;
        ctx.beginPath(); ctx.ellipse(x, y - r * 0.6, r * 1.05, r * 1.9, 0, 0, 7); ctx.stroke();
    }

    // The engine counts Hero.AttackAnim down from `duration` to 0, so the two
    // attack cells play forward across that window; otherwise walk normally.
    // animFrame still runs while attacking, so the stride stays in phase when
    // the pose ends mid-stride.
    const walk = animFrame("hero:" + e.name, e, a.frames[e.name] ?? 6);
    let f = walk;
    if (e.atk > 0) {
        const t = 1 - Math.min(1, e.atk / a.attack.duration);        // 0 -> 1
        f = a.attack.first + Math.min(a.attack.count - 1, Math.floor(t * a.attack.count));
    }
    // Ability outranks the basic attack: DoAbility can fire on the same frame
    // as an attack, and the channel pose is the more dramatic of the two.
    if (e.abl > 0) f = a.ability.col;
    drawSprite(a, f, row, x, y + r * 0.95, 1, Math.cos(e.f) < 0, e.dead ? 0.35 : 1, e.flash, OUTLINE);
}

/// Stamps a packed frame with a dark rim behind it.
///
/// NPCs and wildlife are addressed by rectangle rather than by grid cell, so
/// they cannot go through drawSprite — but they stand on the same busy ground
/// and need the same separation, so the rim is written once here instead of
/// three times at the call sites.
function drawFramed(a, f, cx, footY, dw, dh, flip) {
    ctx.save();
    ctx.imageSmoothingEnabled = false;
    ctx.translate(cx, 0);
    if (flip) ctx.scale(-1, 1);

    ctx.drawImage(a.outlined ?? a.img, f.x, f.y, f.w, f.h, -dw / 2, footY - dh, dw, dh);
    ctx.restore();
}

/// A soft pool of shade under an actor.
///
/// A flat ellipse read as a sticker on ground this busy; a gradient reads as
/// shade. It is doing more work than it looks: the dark ring immediately around
/// the feet is most of what separates a figure from the tiles it stands on.
function groundShadow(x, y, rx, ry) {
    const g = ctx.createRadialGradient(x, y, 0, x, y, rx);
    g.addColorStop(0, "rgba(0,0,0,0.55)");
    g.addColorStop(0.6, "rgba(0,0,0,0.34)");
    g.addColorStop(1, "rgba(0,0,0,0)");
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(1, ry / rx);
    ctx.translate(-x, -y);
    ctx.fillStyle = g;
    ctx.beginPath(); ctx.arc(x, y, rx, 0, 7); ctx.fill();
    ctx.restore();
}

function drawProjectile(e) {
    ctx.fillStyle = e.c;
    ctx.shadowColor = e.c; ctx.shadowBlur = 12;
    ctx.beginPath(); ctx.arc(e.x, e.y, e.r, 0, 7); ctx.fill();
    ctx.shadowBlur = 0;
}

function drawSlash(e) {
    ctx.strokeStyle = hexA(e.c, Math.min(1, e.life * 5));
    ctx.lineWidth = 6;
    ctx.beginPath();
    ctx.arc(e.x, e.y, e.r || 60, e.f - 0.9, e.f + 0.9);
    ctx.stroke();
}

function drawNova(e) {
    const prog = 1 - e.life / 0.4;
    ctx.strokeStyle = hexA(e.c, 1 - prog);
    ctx.lineWidth = 10;
    ctx.beginPath(); ctx.arc(e.x, e.y, (e.r || 200) * prog, 0, 7); ctx.stroke();
}

function drawParticle(e) {
    // Fades and shrinks against its own lifetime, which the engine sends as
    // t01. A fixed fade made a mote meant to last three seconds vanish in the
    // first third of it and a spark meant to last a quarter never fade at all.
    const t = e.t01 ?? 1;
    const size = Math.max(1, (e.r || 3) * (0.35 + t * 0.65));
    const alpha = Math.min(1, t * 1.6);

    ctx.save();
    if (e.add) {
        // Additive: overlapping sparks build into light rather than into mud.
        // Wrong for dust, which is why the effect decides and not this function.
        //
        // No shadowBlur. A per-particle blur is a filter pass each, and there can
        // be hundreds on screen; the glow it bought is most of the way there from
        // the overlap itself plus one larger, fainter square underneath.
        ctx.globalCompositeOperation = "lighter";
        ctx.fillStyle = hexA(e.c, alpha * 0.25);
        const halo = size * 2.4;
        ctx.fillRect(e.x - halo / 2, e.y - halo / 2, halo, halo);
    }
    ctx.fillStyle = hexA(e.c, alpha);
    ctx.fillRect(e.x - size / 2, e.y - size / 2, size, size);
    ctx.restore();
}

function drawExit(e) {
    ctx.fillStyle = "#0a1a2a";
    ctx.beginPath();
    ctx.moveTo(e.x, e.y - e.r);
    ctx.lineTo(e.x + e.r * 0.9, e.y + e.r);
    ctx.lineTo(e.x - e.r * 0.9, e.y + e.r);
    ctx.closePath(); ctx.fill();
    ctx.fillStyle = "#8fd0ff"; ctx.font = "italic 16px 'EB Garamond', serif"; ctx.textAlign = "center";
    ctx.fillText("↓ deeper ↓", e.x, e.y + e.r + 24);
}
