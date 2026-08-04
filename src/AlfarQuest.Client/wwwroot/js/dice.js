// The fate dice — 3D dice rolled at the centre of the screen at the moments
// the game consults the fates: a skill cast (d20), a chest's fortune (d6).
//
// Built on @3d-dice/dice-box (self-hosted under /lib/dice-box; no CDN). The
// engine has already decided the number — C# rolls, the gameplay effect is
// applied on the spot, and the die here is thrown to land on that same value
// (dice-box's "1d20@17" preset notation). Real-time combat never waits on a
// physics simulation, and the die never lies about what happened.
//
// The table is a square held at the centre of the screen, above every window,
// never taking the pointer. Dice knock as they tumble; when they settle a
// small plaque shows the sum, and a good or bad fate stings accordingly.

import { playSounds } from "./sfx.js";
import { t } from "./i18n.js";

let box = null;          // the DiceBox, once initialised
let initing = null;      // in-flight init, so two rolls don't build two worlds
let clearTimer = 0;
let knockTimer = 0;
let current = null;      // the roll being animated — read when the die settles

function ensureTable() {
    let el = document.getElementById("aq-dice");
    if (el) return el;
    // The WHOLE viewport. A smaller centred box put the physics walls in the
    // middle of the scene: a die resting against one was clipped at the canvas
    // edge and read as "behind the map". Full-screen, the only walls are the
    // screen's own edges — the die is above everything, everywhere; a gentler
    // throw (see throwForce) is what keeps it settling near the centre.
    el = document.createElement("div");
    el.id = "aq-dice";
    Object.assign(el.style, {
        position: "fixed", inset: "0",
        zIndex: "10000",
        pointerEvents: "none",
    });
    document.body.appendChild(el);
    return el;
}

/// The plaque under the table that reads the settled roll out. One element,
/// restyled and reshown per roll.
function plaque() {
    let el = document.getElementById("aq-dice-plaque");
    if (el) return el;
    el = document.createElement("div");
    el.id = "aq-dice-plaque";
    Object.assign(el.style, {
        position: "fixed",
        left: "50%", bottom: "15vh",
        transform: "translate(-50%, 0)",
        zIndex: "10001",
        pointerEvents: "none",
        padding: "10px 22px",
        borderRadius: "10px",
        border: "1px solid #b9a06a",
        background: "rgba(16, 12, 28, .92)",
        color: "#e8e2f2",
        font: "600 15px Georgia, serif",
        textAlign: "center",
        letterSpacing: ".04em",
        boxShadow: "0 6px 24px rgba(0,0,0,.5)",
        opacity: "0",
        transition: "opacity .18s ease",
    });
    document.body.appendChild(el);
    return el;
}

function showPlaque(d) {
    const el = plaque();
    const sum = d.mod > 0 ? `${d.value} + ${d.mod} = <b>${d.total}</b>` : `<b>${d.total}</b>`;
    const word = d.outcome === "good" ? `<div style="color:#f0d99a">${t("Success!")}</div>`
               : d.outcome === "bad" ? `<div style="color:#d98a8a">${t("Failure…")}</div>`
               : "";
    el.innerHTML = `<div style="font-size:12px;color:#9a91b0">d${d.sides}</div><div>${sum}</div>${word}`;
    el.style.borderColor = d.outcome === "good" ? "#f0d99a" : d.outcome === "bad" ? "#d98a8a" : "#b9a06a";
    el.style.opacity = "1";
}

function hideAll() {
    box?.clear();
    const el = document.getElementById("aq-dice-plaque");
    if (el) el.style.opacity = "0";
}

/// The die has landed: read it out and let fate sting. Wired once, at init.
function settled() {
    if (!current) return;
    showPlaque(current);
    if (current.outcome === "good") playSounds([{ f: "crit", v: 0.9 }]);
    else if (current.outcome === "bad") playSounds([{ f: "hit", v: 0.8 }]);
    else playSounds([{ f: "block", v: 0.5 }]);
    current = null;
}

async function init() {
    ensureTable();
    const { default: DiceBox } = await import("../lib/dice-box/dice-box.es.min.js");
    const b = new DiceBox({
        container: "#aq-dice",
        assetPath: "/lib/dice-box/assets/",
        theme: "default",
        scale: 16,              // a fate die is read across the room, not squinted at
        gravity: 1.6,
        mass: 1.2,
        friction: 0.95,
        // A soft throw with plenty of spin: the die loses its travel early and
        // tends to die out around the middle of the screen instead of streaking
        // to a corner the way the default force sends it.
        throwForce: 3.5,
        spinForce: 5,
        settleTimeout: 4000,
        lightIntensity: 1,
        shadowTransparency: 0.7,
        onRollComplete: settled,
    });
    await b.init();
    box = b;
    return b;
}

/// One fate roll — `d` is the engine's record of it (sides, value, mod, total,
/// outcome, colour). The die is thrown preset to land on d.value.
export async function rollFate(d) {
    try {
        if (!box) await (initing ??= init());
        clearTimeout(clearTimer);
        clearTimeout(knockTimer);
        const p = document.getElementById("aq-dice-plaque");
        if (p) p.style.opacity = "0";

        current = d;
        box.updateConfig({ themeColor: d.c || "#c9a227" });
        box.roll(`1d${d.sides}@${d.value}`);

        // The tumble, heard: wooden knocks while the die is still rolling.
        playSounds([{ f: "block", v: 0.7 }]);
        knockTimer = setTimeout(() => playSounds([{ f: "block", v: 0.55 }]), 260);

        // Swept away once read — the table must be clear before the next throw,
        // and a die left on screen becomes furniture.
        clearTimer = setTimeout(hideAll, 3600);
    } catch (e) {
        // The dice are ceremony, not truth — the engine already applied the
        // result, so a WebGL hiccup must never take the game down with it.
        // The plaque still reads the number out, so the fates are never mute.
        console.warn("fate dice unavailable:", e?.message ?? e);
        showPlaque(d);
        clearTimer = setTimeout(hideAll, 2600);
    }
}

/// Stops and clears the table — called when the game page goes away.
export function clearDice() {
    clearTimeout(clearTimer);
    clearTimeout(knockTimer);
    current = null;
    hideAll();
}
