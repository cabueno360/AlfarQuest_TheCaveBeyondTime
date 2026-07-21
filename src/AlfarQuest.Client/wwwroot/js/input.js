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
const keys = {};
const mouse = { x: 0, y: 0 };
let mouseDown = false, rightDown = false;
let latch = { switchTo: 0, ability: false, dash: false, interact: false };
let handlers = null;

const PREVENT = ["arrowup", "arrowdown", "arrowleft", "arrowright", " "];

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

function latchPress(k) {
    if (k === "1" || k === "2" || k === "3") latch.switchTo = +k;
    else if (k === "k") latch.ability = true;
    else if (k === "shift" || k === " ") latch.dash = true;
    else if (k === "e") latch.interact = true;
}

// Keys that open menus rather than drive the hero. Held separately so the
// gameplay latch stays purely about actions, and so rebinding is one map.
const MENU_KEYS = { c: "character", escape: "close" };

export function attachInput(canvas, onFirstClick, onMenuKey) {
    const onKeyDown = (e) => {
        if (isTyping(e.target)) return;
        const k = e.key.toLowerCase();
        if (!keys[k]) {
            latchPress(k);                    // guard: held keys repeat keydown
            if (MENU_KEYS[k]) onMenuKey?.(MENU_KEYS[k]);
        }
        keys[k] = true;
        if (PREVENT.includes(k)) e.preventDefault();
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
        if (e.button === 0) mouseDown = true;
        if (e.button === 2) { rightDown = true; latch.ability = true; }
        onFirstClick?.();
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

export const resetLatch = () => { latch = { switchTo: 0, ability: false, dash: false, interact: false }; };

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
        ability: !!(keys["k"] || rightDown || latch.ability),
        dash: !!(keys["shift"] || keys[" "] || latch.dash),
        switchTo: latch.switchTo || (keys["1"] ? 1 : keys["2"] ? 2 : keys["3"] ? 3 : 0),
        mouseX: mouse.x, mouseY: mouse.y,
        viewW, viewH,
    };
    latch.switchTo = 0; latch.ability = false; latch.dash = false; latch.interact = false;
    return state;
}
