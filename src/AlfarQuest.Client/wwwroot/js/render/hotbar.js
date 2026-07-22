// =====================================================================
//  Action hotbar — bottom-right, in screen space.
//
//  The four skill slots of whoever is being steered, plus the potion,
//  each with its icon, keyboard shortcut, and a cooldown wipe. A locked
//  slot shows the level it wants; an unaffordable one reads "mana". Hover
//  raises a tooltip; a click casts, which is why the rects are kept after
//  each draw for input.js to hit-test against.
//
//  Drawn on the canvas rather than in the DOM so it costs nothing per
//  frame — a Blazor overlay re-diffing sixty times a second to move a
//  cooldown wipe would be the one expensive thing on screen.
// =====================================================================
import { ctx, canvas } from "../gfx.js";

const SLOT = 48, GAP = 7, PAD = 18;

// Last-drawn geometry, so a click can be tested without recomputing — and the
// mouse, pushed in by the game loop, so the draw can raise a tooltip.
let rects = [];        // { x, y, w, h, slot }  (slot: 1..4 or "potion")
let mouse = { x: -1, y: -1 };

export function setHotbarMouse(x, y) { mouse.x = x; mouse.y = y; }

/// The slot a screen point is over — 1..4 for a skill, "potion" for the draught,
/// or 0 for none. Read by input.js so a click on the bar casts rather than swings.
export function hotbarSlotAt(x, y) {
    for (const r of rects)
        if (x >= r.x && x <= r.x + r.w && y >= r.y && y <= r.y + r.h) return r.slot;
    return 0;
}

export function drawHotbar(hud) {
    const bar = hud?.hotbar;
    if (!bar) { rects = []; return; }

    const W = canvas.width, H = canvas.height;
    const count = bar.length + 1;                 // skills + the potion
    const totalW = count * SLOT + (count - 1) * GAP;
    let x = W - PAD - totalW;
    const y = H - PAD - SLOT;

    rects = [];
    let hovered = null;

    for (const s of bar) {
        drawSlot(x, y, s.icon, s.shortcut, s.cdFrac, {
            ready: s.ready, unlocked: s.unlocked, affordable: s.affordable, lockLevel: s.unlockLevel,
        });
        rects.push({ x, y, w: SLOT, h: SLOT, slot: s.slot });
        if (over(x, y)) hovered = { s, x };
        x += SLOT + GAP;
    }

    // The potion sits at the end of the row, its own colour so it does not read
    // as a fifth skill.
    drawSlot(x, y, "🧪", "Q", hud.potionCdFrac, { ready: hud.potionReady, unlocked: true, affordable: true, potion: true });
    rects.push({ x, y, w: SLOT, h: SLOT, slot: "potion" });
    if (over(x, y)) hovered = { potion: true, x };

    if (hovered) drawTooltip(hovered, y);
}

function over(x, y) {
    return mouse.x >= x && mouse.x <= x + SLOT && mouse.y >= y && mouse.y <= y + SLOT;
}

function drawSlot(x, y, icon, shortcut, cdFrac, st) {
    const usable = st.ready;
    // Frame: gold when it can be pressed, dim when it cannot.
    ctx.fillStyle = usable ? "rgba(40,34,20,0.82)" : "rgba(10,8,24,0.8)";
    ctx.fillRect(x, y, SLOT, SLOT);
    ctx.strokeStyle = usable ? "#d8b45a" : "rgba(150,150,190,0.28)";
    ctx.lineWidth = usable ? 2 : 1.2;
    ctx.strokeRect(x, y, SLOT, SLOT);

    // Icon, greyed while the slot cannot be used.
    ctx.globalAlpha = st.unlocked ? (usable ? 1 : 0.5) : 0.32;
    ctx.font = "22px 'EB Garamond', serif";
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    ctx.fillStyle = "#e8e6f2";
    ctx.fillText(icon, x + SLOT / 2, y + SLOT / 2 + 1);
    ctx.globalAlpha = 1;

    // Cooldown: a dark wipe rising from the bottom, covering the fraction left.
    if (cdFrac > 0.001) {
        const h = SLOT * Math.min(1, cdFrac);
        ctx.fillStyle = "rgba(6,5,14,0.66)";
        ctx.fillRect(x, y + SLOT - h, SLOT, h);
    }

    // The reason it is unusable, said plainly.
    if (!st.unlocked) {
        ctx.fillStyle = "#9a95b6"; ctx.font = "10px 'EB Garamond', serif";
        ctx.fillText("🔒", x + SLOT / 2, y + SLOT / 2 - 12);
        ctx.fillText(`Lv ${st.lockLevel}`, x + SLOT / 2, y + SLOT - 9);
    } else if (!st.affordable && cdFrac <= 0.001 && !st.potion) {
        ctx.fillStyle = "#6f8ce0"; ctx.font = "italic 10px 'EB Garamond', serif";
        ctx.fillText("mana", x + SLOT / 2, y + SLOT - 8);
    }

    // Keyboard shortcut, top-left.
    ctx.fillStyle = "#f0d99a"; ctx.font = "bold 11px 'EB Garamond', serif";
    ctx.textAlign = "left"; ctx.textBaseline = "top";
    ctx.fillText(shortcut, x + 4, y + 3);
}

function drawTooltip(h, barY) {
    const lines = h.potion
        ? ["Healing Draught", "Restores 40% of your health.", "Shortcut: Q   ·   No mana cost"]
        : [`${h.s.name}`,
           h.s.desc,
           `Shortcut: ${h.s.shortcut}   ·   ${h.s.manaCost} mana` +
             (h.s.unlocked ? "" : `   ·   unlocks at level ${h.s.unlockLevel}`)];

    ctx.font = "13px 'EB Garamond', serif";
    const w = Math.max(...lines.map(l => ctx.measureText(l).width)) + 24;
    const lh = 18, boxH = lines.length * lh + 12;
    let bx = h.x - w / 2 + SLOT / 2;
    bx = Math.max(8, Math.min(canvas.width - w - 8, bx));
    const by = barY - boxH - 10;

    ctx.fillStyle = "rgba(8,6,18,0.95)";
    ctx.fillRect(bx, by, w, boxH);
    ctx.strokeStyle = "rgba(216,180,90,0.5)"; ctx.lineWidth = 1;
    ctx.strokeRect(bx, by, w, boxH);

    ctx.textAlign = "left"; ctx.textBaseline = "top";
    lines.forEach((l, i) => {
        ctx.fillStyle = i === 0 ? "#f0d99a" : i === 1 ? "#cfd6ff" : "#9a95b6";
        ctx.font = i === 0 ? "bold 13px 'EB Garamond', serif"
                 : i === 1 ? "italic 12px 'EB Garamond', serif" : "12px 'EB Garamond', serif";
        ctx.fillText(l, bx + 12, by + 8 + i * lh);
    });
}
