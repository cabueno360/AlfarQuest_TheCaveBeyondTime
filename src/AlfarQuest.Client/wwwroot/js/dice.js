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

/// Every roll ever thrown on this table, engine- and UI-side alike — the
/// probes' record, since UI rolls (a haggle, a persuasion) never ride the
/// engine's snapshot channel that game.js's diceSeen() watches.
const _rolled = [];
export function rolledLog() { return _rolled; }

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
    const still = document.getElementById("aq-dice-still");
    if (still) { still.style.opacity = "0"; still.style.transform = "translate(-50%, -50%) scale(.7)"; }
}

/// Physics owns the tumble; presentation owns the result. The die lands
/// wherever momentum threw it — then it is LIFTED off the table: the settled
/// frame is read back, the die cropped out of it, and mounted dead centre at
/// twice the size, big enough to read from across the room.
///
/// Reading the WebGL canvas is also what forces this: dice-box stops
/// rendering when the dice settle, and a readback consumes the unpreserved
/// drawing buffer — so after reading, the live canvas is blank anyway. Taking
/// the still and hiding the canvas turns that into the feature.
const STILL_H = 300;                     // on-screen height of the lifted die

function liftDie() {
    try {
        const canvas = document.querySelector("#aq-dice canvas");
        if (!canvas || !canvas.width) return;

        const full = document.createElement("canvas");
        full.width = canvas.width; full.height = canvas.height;
        const fg = full.getContext("2d", { willReadFrequently: true });
        fg.drawImage(canvas, 0, 0);
        const d = fg.getImageData(0, 0, full.width, full.height).data;

        let x0 = full.width, y0 = full.height, x1 = -1, y1 = -1, n = 0;
        for (let y = 0; y < full.height; y++)
            for (let x = 0; x < full.width; x++)
                if (d[(y * full.width + x) * 4 + 3] > 40) {
                    n++;
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
        window.__diceGlide = { n, x0, y0, x1, y1 };
        if (n < 12) return;                  // nothing readable — leave the table alone

        const still = document.getElementById("aq-dice-still") ?? (() => {
            const c = document.createElement("canvas");
            c.id = "aq-dice-still";
            Object.assign(c.style, {
                position: "fixed", left: "50%", top: "46%",
                transform: "translate(-50%, -50%) scale(.7)",
                zIndex: "10001", pointerEvents: "none",
                opacity: "0", transition: "opacity .2s ease, transform .3s cubic-bezier(.2,.9,.3,1.2)",
                filter: "drop-shadow(0 10px 22px rgba(0,0,0,.6))",
            });
            document.body.appendChild(c);
            return c;
        })();

        const sw = x1 - x0 + 1, sh = y1 - y0 + 1;
        const scale = STILL_H / sh;
        still.width = Math.round(sw * scale); still.height = STILL_H;
        const sg = still.getContext("2d");
        sg.imageSmoothingEnabled = true;
        sg.clearRect(0, 0, still.width, still.height);
        sg.drawImage(full, x0, y0, sw, sh, 0, 0, still.width, still.height);

        // The canvas has given up its picture to the still; hide it so the
        // blank buffer does not read as a flicker.
        canvas.style.opacity = "0";
        requestAnimationFrame(() => {
            still.style.opacity = "1";
            still.style.transform = "translate(-50%, -50%) scale(1)";
        });
    } catch { /* readback refused — the die simply stays where it fell */ }
}

/// The die has landed: glide it to centre stage, read it out and let fate
/// sting — a bright sting for a success, a dull thud for a failure, a clean
/// metallic ding for the middle. Wired once, at init.
function settled() {
    if (!current) return;
    liftDie();
    showPlaque(current);
    if (current.outcome === "good") playSounds([{ f: "crit", v: 1.0 }]);
    else if (current.outcome === "bad") playSounds([{ f: "hit", v: 0.95 }]);
    else playSounds([{ f: "parry", v: 0.85 }]);
    current = null;
    // The reading time starts NOW, when there is a number to read — counted
    // from the throw, a slow-spinning d20 got one second of table before the
    // sweep. The roll-time timer stays armed as a fallback for a settle that
    // never fires.
    clearTimeout(clearTimer);
    clearTimer = setTimeout(hideAll, 3500);
}

async function init() {
    ensureTable();
    const { default: DiceBox } = await import("../lib/dice-box/dice-box.es.min.js");
    const b = new DiceBox({
        container: "#aq-dice",
        assetPath: "/lib/dice-box/assets/",
        theme: "default",
        scale: 22,              // a fate die is read across the room, not squinted at
        // Physics owns the tumble and lands wherever momentum says — heavy
        // gravity was tried and only slammed the die down at its spawn corner.
        // WHERE it rests does not matter: the glide in settled() carries the
        // landed die to the centre of the screen.
        gravity: 1.4,
        mass: 1.2,
        friction: 0.9,
        throwForce: 5,
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
    _rolled.push(d);
    try {
        if (!box) await (initing ??= init());
        clearTimeout(clearTimer);
        clearTimeout(knockTimer);
        const p = document.getElementById("aq-dice-plaque");
        if (p) p.style.opacity = "0";

        current = d;
        // A fresh throw needs the live table back: the last result's still is
        // swept and the canvas (blanked by that readback) shown again.
        hideAll();
        const canvas = document.querySelector("#aq-dice canvas");
        if (canvas) canvas.style.opacity = "1";
        box.updateConfig({ themeColor: d.c || "#c9a227" });
        box.roll(`1d${d.sides}@${d.value}`);

        // The tumble, heard: a run of wooden knocks fading as the die loses its
        // energy, the way a real die drums a table.
        playSounds([{ f: "block", v: 0.95 }]);
        knockTimer = setTimeout(() => {
            playSounds([{ f: "block", v: 0.75 }]);
            knockTimer = setTimeout(() => playSounds([{ f: "block", v: 0.55 }]), 240);
        }, 220);

        // A fallback sweep only — settled() re-arms the real one from the
        // moment the die lands, so the reading time never depends on how long
        // the tumble took.
        clearTimer = setTimeout(hideAll, 9000);
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
