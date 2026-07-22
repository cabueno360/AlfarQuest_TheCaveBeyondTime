// =====================================================================
//  Input
//
//  The engine samples input once per frame. A tap whose keydown AND keyup
//  both land between two frames is therefore invisible to it — which is
//  why switching heroes with a quick 1/2/3 used to fail at random.
//  Discrete actions are latched on press and cleared only once the engine
//  has actually seen them; held state is OR-ed in on top, so holding a key
//  still re-triggers on cooldown exactly as before.
// =====================================================================
import { hotbarSlotAt } from "./render/hotbar.js";

const keys = {};
const mouse = { x: 0, y: 0 };
let mouseDown = false, rightDown = false;
const freshLatch = () => ({ switchTo: 0, cycle: 0, castSlot: 0, ability: false, dash: false, interact: false, potion: false });
let latch = freshLatch();
let handlers = null;

// Tab would move focus off the canvas, and the number row and Q must not scroll
// or type anywhere; all of them are ours while the game holds the keyboard.
const PREVENT = ["arrowup", "arrowdown", "arrowleft", "arrowright", " ", "tab"];

/// Whether the keystroke belongs to something the player is typing in.
///
/// This listener is on the document, so before the character window had a search
/// box every key in the game was safely ours. It is not any more: typing "c" in
/// that box closed the window, "e" talked to whoever was nearby, and space and
/// the arrow keys never reached the caret at all because they are preventDefault-ed
/// below. A game that captures the keyboard globally has to stop at the edge of a
/// text field.
function isTyping(target) {
    if (!target) return false;
    if (target.isContentEditable) return true;
    const tag = target.tagName;
    return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
}

function latchPress(k, shift) {
    // The number row casts the active hero's four skills now; switching heroes
    // moved to Tab (cycle) and the function keys (direct), so the fingers on the
    // skills are not the fingers on the party.
    if (k === "1" || k === "2" || k === "3" || k === "4") latch.castSlot = +k;
    else if (k === "f1" || k === "f2" || k === "f3") latch.switchTo = +k.slice(1);
    else if (k === "tab") latch.cycle = shift ? -1 : 1;
    else if (k === "k") latch.castSlot = 4;            // legacy: the ultimate
    else if (k === "q") latch.potion = true;
    else if (k === "shift" || k === " ") latch.dash = true;
    else if (k === "e") latch.interact = true;
}

// Keys that open menus rather than drive the hero. Held separately so the
// gameplay latch stays purely about actions, and so rebinding is one map.
const MENU_KEYS = { c: "character", escape: "close" };

export function attachInput(canvas, onMenuKey) {
    const onKeyDown = (e) => {
        if (isTyping(e.target)) return;
        const k = e.key.toLowerCase();
        if (!keys[k]) {
            latchPress(k, e.shiftKey);         // guard: held keys repeat keydown
            if (MENU_KEYS[k]) onMenuKey?.(MENU_KEYS[k]);
        }
        keys[k] = true;
        // Tab and the function keys have no "held" meaning and repeat, so they are
        // not tracked in `keys`; everything else is.
        if (PREVENT.includes(k) || k.startsWith("f")) e.preventDefault();
    };
    // Not guarded by isTyping: a key pressed before focus entered a field must
    // still be released, or the hero walks into a wall for as long as the box has
    // focus.
    const onKeyUp = (e) => { keys[e.key.toLowerCase()] = false; };
    const onMove = (e) => {
        const r = canvas.getBoundingClientRect();
        mouse.x = e.clientX - r.left; mouse.y = e.clientY - r.top;
    };
    const onDown = (e) => {
        if (e.button === 0) {
            // A left-click on a hotbar slot works the slot rather than swinging,
            // so the bar is a set of buttons and not just a readout.
            const slot = hotbarSlotAt(mouse.x, mouse.y);
            if (slot === "potion") latch.potion = true;
            else if (slot > 0) latch.castSlot = slot;
            else mouseDown = true;
        }
        if (e.button === 2) { rightDown = true; latch.ability = true; }
    };
    const onUp = (e) => {
        if (e.button === 0) mouseDown = false;
        if (e.button === 2) rightDown = false;
    };
    const onCtx = (e) => e.preventDefault();

    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    window.addEventListener("mouseup", onUp);
    canvas.addEventListener("mousemove", onMove);
    canvas.addEventListener("mousedown", onDown);
    canvas.addEventListener("contextmenu", onCtx);
    handlers = { canvas, onKeyDown, onKeyUp, onMove, onDown, onUp, onCtx };
}

export function detachInput() {
    if (!handlers) return;
    const { canvas, onKeyDown, onKeyUp, onMove, onDown, onUp, onCtx } = handlers;
    window.removeEventListener("keydown", onKeyDown);
    window.removeEventListener("keyup", onKeyUp);
    window.removeEventListener("mouseup", onUp);
    canvas.removeEventListener("mousemove", onMove);
    canvas.removeEventListener("mousedown", onDown);
    canvas.removeEventListener("contextmenu", onCtx);
    handlers = null;
    resetLatch();
}

export const resetLatch = () => { latch = freshLatch(); };

/// The cursor in canvas space, for the hotbar's hover — read by the game loop
/// and handed to the renderer, so hotbar.js need not reach back into input.
export const mousePos = () => mouse;

// Reads the frame's input and consumes the latch in one go, so a press can
// never be delivered twice.
export function readInput(viewW, viewH) {
    const state = {
        up: !!(keys["w"] || keys["arrowup"]),
        down: !!(keys["s"] || keys["arrowdown"]),
        left: !!(keys["a"] || keys["arrowleft"]),
        right: !!(keys["d"] || keys["arrowright"]),
        attack: !!(keys["j"] || mouseDown),      // level-triggered: hold to keep firing
        interact: latch.interact,                // edge-triggered: one talk per press
        ability: !!(rightDown || latch.ability),
        dash: !!(keys["shift"] || keys[" "] || latch.dash),
        potion: latch.potion,
        switchTo: latch.switchTo,                // F1-F3, edge-triggered
        cycle: latch.cycle,                      // Tab / Shift+Tab
        // A skill cast is edge-triggered — one press, one cast — held so a fast
        // tap between frames is never lost, then the number keys as a fallback for
        // a key held down.
        castSlot: latch.castSlot || (keys["1"] ? 1 : keys["2"] ? 2 : keys["3"] ? 3 : keys["4"] ? 4 : 0),
        mouseX: mouse.x, mouseY: mouse.y,
        viewW, viewH,
    };
    latch.switchTo = 0; latch.cycle = 0; latch.castSlot = 0;
    latch.ability = false; latch.dash = false; latch.interact = false; latch.potion = false;
    return state;
}
