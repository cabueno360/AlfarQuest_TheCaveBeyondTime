// Shared drawing context.
//
// Exported with `let`, so importers see the live binding: modules can keep
// writing `ctx.fillRect(...)` exactly as they did when everything lived in one
// file, and still pick up the canvas once startGame() creates it. Passing the
// context through every draw call would have meant touching several hundred
// call sites for no gain.
export let canvas = null, ctx = null, lightCanvas = null, lctx = null;

export function initGfx(el) {
    canvas = el;
    ctx = el.getContext("2d");
    lightCanvas = document.createElement("canvas");
    lctx = lightCanvas.getContext("2d");
    sizeToParent();
}

export function sizeToParent() {
    if (!canvas) return;
    const w = canvas.clientWidth || window.innerWidth;
    const h = canvas.clientHeight || window.innerHeight;
    canvas.width = w; canvas.height = h;
    lightCanvas.width = w; lightCanvas.height = h;
}

// #rrggbb + alpha -> rgba(), used by the light and particle passes.
export function hexA(hex, a) {
    if (!hex) return `rgba(255,255,255,${a})`;
    const h = hex.replace("#", "");
    const r = parseInt(h.substring(0, 2), 16), g = parseInt(h.substring(2, 4), 16), b = parseInt(h.substring(4, 6), 16);
    return `rgba(${r},${g},${b},${a})`;
}
