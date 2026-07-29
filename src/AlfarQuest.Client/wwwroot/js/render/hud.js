// =====================================================================
//  HUD, drawn in screen space. One element per corner: the canvas and the
//  DOM overlays cannot see each other, so sharing a corner draws one
//  through the other.
// =====================================================================
import { ctx, canvas } from "../gfx.js";
import { drawHotbar } from "./hotbar.js";
import { t, tf } from "../i18n.js";

// ---------------------------------------------------------------------
//  Where you are.
//
//  The corner panel names the place at all times, but a line of 12px italic in
//  a corner is not an arrival. When the region changes, the name is announced:
//  it rises, holds, and goes, the way an area title does in the games this is
//  trying to be. It is also the only thing that tells a player who has just
//  walked over a seam that they are somewhere new.
// ---------------------------------------------------------------------
const title = { name: "", t: 0, last: null };
const TITLE_IN = 0.6, TITLE_HOLD = 2.2, TITLE_OUT = 1.0;
const TITLE_LIFE = TITLE_IN + TITLE_HOLD + TITLE_OUT;

/// Announce a place by name. Called with every frame's region; it only reacts
/// when the name actually changes, so re-entering the same place is silent.
function noteRegion(name, dt) {
    if (name && name !== title.last) {
        // Including the first one. Being told where you have woken up is the
        // whole point; a player who starts the game with no idea what the place
        // is called has been told nothing.
        // Track the English name but announce the translated one, so switching
        // language cannot make the same place read as a new one.
        title.last = name;
        title.name = t(name);
        title.t = TITLE_LIFE;
    }
    if (title.t > 0) title.t = Math.max(0, title.t - dt);
}

function drawRegionTitle(W, H) {
    if (title.t <= 0 || !title.name) return;
    const age = TITLE_LIFE - title.t;
    const a = age < TITLE_IN ? age / TITLE_IN
            : title.t < TITLE_OUT ? title.t / TITLE_OUT : 1;
    const rise = (1 - Math.min(1, age / TITLE_IN)) * 10;      // settles as it fades in

    ctx.save();
    ctx.globalAlpha = a;
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    const y = H * 0.22 + rise;

    ctx.font = "30px 'EB Garamond', serif";
    const w = ctx.measureText(title.name).width;
    // A band behind it, faded at both ends, so the name reads over grass or rock
    // without a hard box around it.
    const g = ctx.createLinearGradient(W / 2 - w, 0, W / 2 + w, 0);
    g.addColorStop(0, "rgba(8,6,18,0)");
    g.addColorStop(0.5, "rgba(8,6,18,0.62)");
    g.addColorStop(1, "rgba(8,6,18,0)");
    ctx.fillStyle = g;
    ctx.fillRect(W / 2 - w, y - 30, w * 2, 60);

    ctx.fillStyle = "rgba(0,0,0,0.75)";
    ctx.fillText(title.name, W / 2 + 2, y + 2);
    ctx.fillStyle = "#f0d99a";
    ctx.fillText(title.name, W / 2, y);

    // A rule under the name, drawn out from the middle as it appears.
    const rw = Math.min(1, age / TITLE_IN) * (w * 0.6);
    const rg = ctx.createLinearGradient(W / 2 - rw, 0, W / 2 + rw, 0);
    rg.addColorStop(0, "rgba(240,217,154,0)");
    rg.addColorStop(0.5, "rgba(240,217,154,0.85)");
    rg.addColorStop(1, "rgba(240,217,154,0)");
    ctx.fillStyle = rg;
    ctx.fillRect(W / 2 - rw, y + 22, rw * 2, 1);
    ctx.restore();
}

/// Test seam: what the title card is showing, and how much life it has left.
export function regionTitle() {
    return { name: title.name, t: +title.t.toFixed(2), showing: title.t > 0 };
}

// ---------------------------------------------------------------------
//  HUD (drawn in screen space)
// ---------------------------------------------------------------------
/// Loot text rising from a kill. Drawn in world space inside the camera
/// transform, so it stays over the corpse it came from.
export function drawFloaters(floats, cam) {
    if (!floats || !floats.length) return;
    ctx.save();
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    ctx.font = "bold 13px 'EB Garamond', serif";
    for (const f of floats) {
        ctx.globalAlpha = Math.min(1, f.life / 0.6);
        ctx.fillStyle = "rgba(0,0,0,0.65)";
        ctx.fillText(f.text, f.x + 1, f.y - 22 + 1);
        ctx.fillStyle = f.c;
        ctx.fillText(f.text, f.x, f.y - 22);
    }
    ctx.restore();
}

export function drawHud(hud, dt = 0.016) {
    if (!hud) return;
    const W = canvas.width, H = canvas.height;

    noteRegion(hud.region, dt);

    // The action hotbar sits bottom-right, its own corner clear of the party
    // panel (bottom-left) and the quit button (top-right).
    drawHotbar(hud);

    // party panel — bottom left
    // 52px a row, not 44: each hero now carries a mana bar under the health one.
    const ROW = 52;
    const bx = 18, by = H - 18;
    hud.party.forEach((p, i) => {
        const py = by - (hud.party.length - i) * ROW;
        ctx.fillStyle = p.active ? "rgba(216,180,90,0.16)" : "rgba(10,8,24,0.6)";
        ctx.fillRect(bx, py, 210, 46);
        ctx.strokeStyle = p.active ? "#d8b45a" : "rgba(150,150,190,0.25)";
        ctx.lineWidth = 1.5; ctx.strokeRect(bx, py, 210, 46);

        ctx.fillStyle = "#e8e6f2"; ctx.font = "12px 'EB Garamond', serif"; ctx.textAlign = "left"; ctx.textBaseline = "top";
        ctx.fillText(`${p.slot}· ${p.dead ? t("(fallen) ") : ""}${t(p.cls)}`, bx + 8, py + 5);

        ctx.fillStyle = "rgba(0,0,0,0.5)"; ctx.fillRect(bx + 8, py + 21, 160, 8);
        ctx.fillStyle = p.dead ? "#552233" : "#5fbf7a"; ctx.fillRect(bx + 8, py + 21, 160 * (p.mhp ? p.hp / p.mhp : 0), 8);

        // Mana below health, thinner: it matters, but not as much as being alive,
        // and matching thicknesses would make the two hard to tell apart at a glance.
        ctx.fillStyle = "rgba(0,0,0,0.5)"; ctx.fillRect(bx + 8, py + 32, 160, 5);
        ctx.fillStyle = p.dead ? "#2a2a44" : "#6f8ce0";
        ctx.fillRect(bx + 8, py + 32, 160 * (p.mmana ? p.mana / p.mmana : 0), 5);

        if (!p.dead) {
            ctx.textAlign = "right";
            if (p.abilityReady) {
                ctx.fillStyle = "#f0d99a";
                ctx.fillText(`✦ ${t("ult")}`, bx + 202, py + 5);
            } else if (!p.abilityAffordable) {
                // Off cooldown but unpayable. Saying so is the difference between
                // "not yet" and a key that appears to do nothing.
                ctx.fillStyle = "#6f8ce0";
                ctx.fillText(t("mana"), bx + 202, py + 5);
            }
        }
    });

    drawExperience(hud, bx, by - hud.party.length * 44 - 12);

    // Enemies remaining — top LEFT. The top-right corner belongs to the DOM
    // "Abandon Delve" button (.aq-quit); this used to be drawn underneath it.
    // Top LEFT: the top-right corner belongs to the DOM "Abandon Delve" button.
    // Only the cave has husks to count and depths to number; the overworld and a
    // building interior carry their objective and their name instead — a husk
    // counter reading 0 in someone's kitchen would be noise.
    const inCave = hud.stage === 2;
    // Region names and objectives are written in English by the engine, so they
    // go through the same table as the labels around them.
    const headline = inCave
        ? (hud.phase === "cleared" ? t("Chamber cleared") : tf("Husks: {0}", hud.enemies))
        : (t(hud.objective || "") || t(hud.region || ""));
    const subline = inCave
        ? tf("{0}  ·  depth {1}", t(hud.region || ""), hud.level || 1)
        : (hud.objective ? t(hud.region || "") : "");
    // Below ground the husk count is the headline and the depth its subline, so the
    // Pact's current order hangs on a third line beneath them. On the surface the
    // objective is already the headline, so there is nothing more to add.
    const orders = inCave ? t(hud.objective || "") : "";

    ctx.font = "13px 'EB Garamond', serif";
    const panelW = orders ? Math.max(230, ctx.measureText(`↳ ${orders}`).width + 36) : 210;
    ctx.fillStyle = "rgba(10,8,24,0.55)"; ctx.fillRect(18, 14, panelW, orders ? 66 : 46);
    ctx.fillStyle = "#b9c7ff"; ctx.font = "14px 'EB Garamond', serif"; ctx.textAlign = "left"; ctx.textBaseline = "middle";
    ctx.fillText(headline, 28, 28);
    ctx.fillStyle = "#f0d99a"; ctx.font = "italic 12px 'EB Garamond', serif";
    ctx.fillText(subline, 28, 48);
    if (orders) {
        ctx.fillStyle = "#d7e0ff"; ctx.font = "13px 'EB Garamond', serif";
        ctx.fillText(`↳ ${orders}`, 28, 68);
    }

    // The chamber's boss: a broad health bar across the top, under the music pill,
    // so a Guardian fight reads as a Guardian fight and you can watch it fall.
    if (hud.boss) {
        const b = hud.boss;
        const frac = Math.max(0, Math.min(1, b.mhp > 0 ? b.hp / b.mhp : 0));
        const bw = Math.min(520, W * 0.46), bh = 15;
        const bx = (W - bw) / 2, by = 72;
        ctx.textAlign = "center"; ctx.textBaseline = "alphabetic";
        ctx.font = "16px 'Cinzel', serif";
        ctx.fillStyle = b.enraged ? "#ff9a6b" : "#e6d6a8";
        ctx.fillText(b.name.toUpperCase() + (b.enraged ? "  —  ENRAGED" : ""), W / 2, by - 7);
        ctx.fillStyle = "rgba(8,6,18,0.82)"; ctx.fillRect(bx - 3, by - 3, bw + 6, bh + 6);
        ctx.fillStyle = "rgba(46,22,32,0.9)"; ctx.fillRect(bx, by, bw, bh);
        const grad = ctx.createLinearGradient(bx, 0, bx + bw, 0);
        if (b.enraged) { grad.addColorStop(0, "#ff5a3a"); grad.addColorStop(1, "#ffb591"); }
        else { grad.addColorStop(0, "#b93348"); grad.addColorStop(1, "#e2687a"); }
        ctx.fillStyle = grad; ctx.fillRect(bx, by, bw * frac, bh);
        ctx.strokeStyle = b.enraged ? "rgba(255,150,100,0.85)" : "rgba(216,180,90,0.55)";
        ctx.lineWidth = 1; ctx.strokeRect(bx - 2.5, by - 2.5, bw + 5, bh + 5);
        ctx.textAlign = "left"; ctx.textBaseline = "middle";
    }

    // Over the corner panels but clear of the bottom prompt, at a fifth of the
    // way down, which is where the eye already is.
    drawRegionTitle(W, H);

    // --- talking to a villager ---
    // Prompt and balloon are both screen-space: a balloon anchored in the world
    // would drift off the edge when the villager stands near the view border.
    if (hud.promptName) {
        // The verb comes from the engine, which resolved what [E] would act on.
        // Composing it here from guesswork is how a prompt ends up offering to
        // "Talk to" a chest.
        const verb = t(hud.promptVerb || "Use");
        const label = `[E]  ${verb} ${t(hud.promptName)}`;
        ctx.font = "14px 'EB Garamond', serif"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
        const w = ctx.measureText(label).width + 26;
        ctx.fillStyle = "rgba(10,8,24,0.78)";
        ctx.fillRect(W / 2 - w / 2, H - 96, w, 30);
        ctx.strokeStyle = "rgba(216,180,90,0.5)"; ctx.lineWidth = 1;
        ctx.strokeRect(W / 2 - w / 2, H - 96, w, 30);
        ctx.fillStyle = "#f0d99a";
        ctx.fillText(label, W / 2, H - 81);
    }

    if (hud.talkLine) {
        const boxW = Math.min(620, W - 80), boxH = 96;
        const x = W / 2 - boxW / 2, y = H - 168;
        ctx.fillStyle = "rgba(10,8,24,0.92)";
        ctx.fillRect(x, y, boxW, boxH);
        ctx.strokeStyle = "rgba(216,180,90,0.55)"; ctx.lineWidth = 1.5;
        ctx.strokeRect(x, y, boxW, boxH);

        ctx.textAlign = "left"; ctx.textBaseline = "top";
        ctx.fillStyle = "#f0d99a"; ctx.font = "16px Cinzel, serif";
        ctx.fillText(t(hud.talkName), x + 18, y + 14);
        ctx.fillStyle = "var(--aq-muted)"; ctx.fillStyle = "#9a95b6";
        ctx.font = "italic 12px 'EB Garamond', serif";
        ctx.fillText(t(hud.talkRole), x + 18 + ctx.measureText(t(hud.talkName)).width + 60, y + 18);

        ctx.fillStyle = "#e8e6f2"; ctx.font = "15px 'EB Garamond', serif";
        ctx.textAlign = "center";
        wrapText(t(hud.talkLine), W / 2, y + 48, boxW - 44, 20);

        ctx.fillStyle = "#9a95b6"; ctx.font = "italic 11px 'EB Garamond', serif";
        ctx.textAlign = "right";
        ctx.fillText(t("[E] continue"), x + boxW - 16, y + boxH - 18);
    }

    // Cleared banner — holds, then fades out. It used to sit across the middle
    // of the screen forever, because the phase is one-way and nothing dismissed
    // it: the reward for clearing the chamber was a permanent obstruction. The
    // "Chamber cleared" readout above is the lasting indicator, so the banner
    // only needs to announce the moment.
    if (hud.phase === "cleared" && hud.message) {
        const HOLD = 4.5, FADE = 1.5;
        // `held`, not `t`: t() is the translator now, and a local of that name
        // would shadow it exactly where the banner's words need translating.
        const held = hud.clearedFor || 0;
        const alpha = held < HOLD ? 1 : Math.max(0, 1 - (held - HOLD) / FADE);
        if (alpha > 0) {
            ctx.save();
            ctx.globalAlpha = alpha;
            ctx.fillStyle = "rgba(4,3,10,0.55)"; ctx.fillRect(0, H / 2 - 60, W, 120);
            ctx.fillStyle = "#f0d99a"; ctx.font = "26px Cinzel, serif"; ctx.textAlign = "center"; ctx.textBaseline = "middle";
            ctx.fillText(t("The Cistern falls silent"), W / 2, H / 2 - 20);
            ctx.fillStyle = "#cfd6ff"; ctx.font = "italic 16px 'EB Garamond', serif";
            wrapText(t(hud.message), W / 2, H / 2 + 14, Math.min(640, W - 80), 22);
            ctx.restore();
        }
    }
}

/// Level and progress to the next, sitting directly above the party panel so
/// the two read as one block of "how the party is doing".
function drawExperience(hud, x, y) {
    const W = 210, H = 34;
    const top = y - H;

    ctx.fillStyle = "rgba(10,8,24,0.62)";
    ctx.fillRect(x, top, W, H);

    // The frame brightens for a moment after a level-up. It is the same signal
    // as the particles and the shake, in the one place the player is already
    // looking to see what changed.
    const glow = hud.levelUpGlow || 0;
    ctx.strokeStyle = glow > 0
        ? `rgba(240,217,154,${0.35 + 0.55 * Math.min(1, glow)})`
        : "rgba(150,150,190,0.25)";
    ctx.lineWidth = glow > 0 ? 2 : 1.5;
    ctx.strokeRect(x, top, W, H);

    ctx.textBaseline = "top";
    ctx.textAlign = "left";
    ctx.fillStyle = "#f0d99a";
    ctx.font = "13px Cinzel, serif";
    ctx.fillText(tf("Level {0}", hud.heroLevel || 1), x + 8, top + 5);

    ctx.textAlign = "right";
    ctx.fillStyle = "#9a95b6";
    ctx.font = "11px 'EB Garamond', serif";
    ctx.fillText(`${hud.xp || 0} / ${hud.xpNext || 0}`, x + W - 8, top + 7);

    const barX = x + 8, barY = top + 23, barW = W - 16, barH = 6;
    ctx.fillStyle = "rgba(0,0,0,0.5)";
    ctx.fillRect(barX, barY, barW, barH);

    const frac = Math.max(0, Math.min(1, hud.xpFrac || 0));
    if (frac > 0) {
        const fill = ctx.createLinearGradient(barX, 0, barX + barW, 0);
        fill.addColorStop(0, "#b9c7ff");
        fill.addColorStop(1, "#e7ccff");
        ctx.fillStyle = fill;
        ctx.fillRect(barX, barY, barW * frac, barH);
    }
}

function wrapText(text, cx, y, maxW, lh) {
    const words = text.split(" ");
    let line = "", lines = [];
    for (const w of words) {
        if (ctx.measureText(line + w).width > maxW && line) { lines.push(line); line = w + " "; }
        else line += w + " ";
    }
    lines.push(line);
    lines.forEach((l, i) => ctx.fillText(l.trim(), cx, y + i * lh));
}
