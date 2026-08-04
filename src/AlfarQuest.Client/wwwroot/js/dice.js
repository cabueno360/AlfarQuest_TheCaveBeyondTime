// The fate dice — 3D dice rolled across the whole screen at the moments the
// game consults the fates: an ultimate cast, a chest's fortune.
//
// Built on @3d-dice/dice-box (self-hosted under /lib/dice-box; no CDN). The
// engine has already decided the number — C# rolls, the gameplay effect is
// applied on the spot, and the die here is thrown to land on that same value
// (dice-box's "1d20@17" preset notation). Real-time combat never waits on a
// physics simulation, and the die never lies about what happened.
//
// The overlay sits above every window (the character sheet, the loot window,
// dialogs) and never takes the pointer.

let box = null;          // the DiceBox, once initialised
let initing = null;      // in-flight init, so two rolls don't build two worlds
let clearTimer = 0;

function ensureOverlay() {
    let el = document.getElementById("aq-dice");
    if (el) return el;
    el = document.createElement("div");
    el.id = "aq-dice";
    Object.assign(el.style, {
        position: "fixed", inset: "0", zIndex: "10000",
        pointerEvents: "none",
    });
    document.body.appendChild(el);
    return el;
}

async function init() {
    ensureOverlay();
    const { default: DiceBox } = await import("../lib/dice-box/dice-box.es.min.js");
    const b = new DiceBox({
        container: "#aq-dice",
        assetPath: "/lib/dice-box/assets/",
        theme: "default",
        scale: 11,              // fullscreen table — smaller than this reads as a crumb
        gravity: 1.4,
        mass: 1.2,
        friction: 0.9,
        settleTimeout: 4000,
        lightIntensity: 1,
        shadowTransparency: 0.7,
    });
    await b.init();
    box = b;
    return b;
}

/// One fate roll. `sides` and `value` name the die and where it must land;
/// `colour` tints the die to whoever (or whatever) is rolling.
export async function rollFate(sides, value, colour) {
    try {
        if (!box) await (initing ??= init());
        clearTimeout(clearTimer);
        box.updateConfig({ themeColor: colour || "#c9a227" });
        box.roll(`1d${sides}@${value}`);
        // Swept away once read — the table must be clear before the next throw,
        // and a die left on screen becomes furniture.
        clearTimer = setTimeout(() => box?.clear(), 3200);
    } catch (e) {
        // The dice are ceremony, not truth — the engine already applied the
        // result, so a WebGL hiccup must never take the game down with it.
        console.warn("fate dice unavailable:", e?.message ?? e);
    }
}

/// Stops and clears the table — called when the game page goes away.
export function clearDice() {
    clearTimeout(clearTimer);
    box?.clear();
}
