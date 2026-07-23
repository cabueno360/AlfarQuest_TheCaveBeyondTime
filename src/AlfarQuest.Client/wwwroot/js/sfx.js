// =====================================================================
//  Sound effects.
//
//  The engine hands each frame a list of sound events — {f: family, v:
//  volume} — and this turns them into noise. A family is a folder of
//  interchangeable takes; playing one picks at random so a swung sword
//  does not tick like a metronome.
//
//  Web Audio rather than <audio> elements. Each file is fetched and
//  decoded once into a buffer; playing it is a throwaway source node off
//  that buffer, which is cheap enough to overlap a dozen at once and
//  costs nothing to garbage-collect. One master gain sits in front of the
//  speakers so muting is a single number and not a walk over live voices.
//
//  This is the pooling and the ceiling the brief asks for: buffers are
//  the object pool, and a voice counter caps how many can sound together
//  so a berserk frame cannot turn into a wall of clipping.
// =====================================================================

const DIR = "audio/sfx/";

// family -> the takes it may play. The engine only ever names the family;
// which file and how many exist is entirely this layer's business.
const FAMILIES = {
    step_dirt:    ["step-dirt-1", "step-dirt-2", "step-dirt-3", "step-dirt-4", "step-dirt-5"],
    step_stone:   ["step-stone-1", "step-stone-2", "step-stone-3", "step-stone-4", "step-stone-5"],
    step_water:   ["step-water-1", "step-water-2", "step-water-3"],
    dash:         ["dash"],
    swing:        ["swing-1", "swing-2", "swing-3"],
    bow:          ["bow-1", "bow-2"],
    hit:          ["hit-1", "hit-2", "hit-3"],
    crit:         ["crit"],
    block:        ["block-1", "block-2", "block-3", "parry-1", "parry-2"],
    magic_fire:   ["fire-1", "fire-2", "fire-3"],
    magic_frost:  ["frost-1", "frost-2"],
    magic_holy:   ["holy-1", "holy-2"],
    spell_impact: ["spell-impact-1", "spell-impact-2", "spell-impact-3"],
    chest:        ["chest-1", "chest-2"],
    chest_rare:   ["chest-2", "unlock"],
    // A purchase or a sale. No coin recording of its own, so it borrows the
    // latch of a lock and the lid of a chest — a small, dry "transaction" click.
    coin:         ["unlock", "chest-1"],
    mine:         ["mine-1", "mine-2", "mine-3", "mine-4", "mine-5"],
    chop:         ["chop-1", "chop-2", "chop-3", "chop-4"],
};

// Some families are inherently louder than others in the source recordings;
// trim them here rather than editing the files, so the mix is one readable map.
const FAMILY_GAIN = {
    step_dirt: 0.5, step_stone: 0.5, step_water: 0.5,
    hit: 0.9, crit: 1.0, swing: 0.55, bow: 0.6, block: 0.7,
    magic_fire: 0.85, magic_frost: 0.85, magic_holy: 0.85, spell_impact: 0.8,
    chest: 0.9, chest_rare: 1.0, coin: 0.85, mine: 0.8, chop: 0.8, dash: 0.5,
};

const MASTER = 0.7;      // headroom under the music
const MAX_VOICES = 24;   // hard ceiling; past this a frame's extra sounds drop

let ctx = null;
let master = null;
let muted = false;
let voices = 0;
const buffers = new Map();   // "hit-2" -> AudioBuffer | "pending"
let armed = false;
let started = false;
let lastLog = { plays: 0 };  // a seam for the probe: how many voices have started

/// Brings the audio context up and starts decoding every take. Safe to call more
/// than once. The context may begin suspended — browsers hold it until a gesture
/// — so a one-shot listener resumes it on the first click or key.
export function initSfx() {
    if (started) return;
    started = true;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;                 // no Web Audio here; the game is silent but fine
    ctx = new AC();
    master = ctx.createGain();
    master.gain.value = muted ? 0 : MASTER;
    master.connect(ctx.destination);

    for (const takes of Object.values(FAMILIES))
        for (const name of takes) load(name);

    armResume();
}

function armResume() {
    if (armed) return;
    armed = true;
    const resume = () => {
        if (ctx && ctx.state === "suspended") ctx.resume().catch(() => { });
    };
    for (const ev of ["pointerdown", "keydown", "touchstart"])
        window.addEventListener(ev, resume, { passive: true });
}

async function load(name) {
    if (buffers.has(name)) return;
    buffers.set(name, "pending");
    try {
        const res = await fetch(DIR + name + ".ogg");
        const bytes = await res.arrayBuffer();
        const buf = await ctx.decodeAudioData(bytes);
        buffers.set(name, buf);
    } catch {
        buffers.delete(name);        // let a later frame try again rather than pin a failure
    }
}

/// Plays the frame's sounds. Each entry is {f, v}; v is already faded for
/// distance by the engine. A family whose buffers have not finished decoding is
/// skipped this frame rather than queued — a footstep half a second late is worse
/// than a footstep missed.
export function playSounds(list) {
    if (!ctx || !list || !list.length) return;
    if (ctx.state === "suspended") return;   // still waiting on the gesture; drop, don't stack
    for (const s of list) play(s.f, s.v);
}

function play(family, vol) {
    if (voices >= MAX_VOICES) return;
    const takes = FAMILIES[family];
    if (!takes) return;

    // A different take each time, chosen without Math.random so the module stays
    // deterministic under test — the voice count is what varies the pick.
    const name = takes[(lastLog.plays + voices) % takes.length];
    const buf = buffers.get(name);
    if (!buf || buf === "pending") return;

    const src = ctx.createBufferSource();
    src.buffer = buf;
    const g = ctx.createGain();
    g.gain.value = Math.max(0, Math.min(1, (vol ?? 1) * (FAMILY_GAIN[family] ?? 1)));
    src.connect(g).connect(master);

    voices++;
    lastLog.plays++;
    src.onended = () => { voices--; };
    src.start();
}

export function setSfxMuted(value) {
    muted = !!value;
    if (master) master.gain.value = muted ? 0 : MASTER;
    return muted;
}

/// Lets go of the context when the session ends, so a returned-to menu is not
/// still holding an audio device open.
export function stopSfx() {
    if (ctx) { try { ctx.close(); } catch { /* already closed */ } }
    ctx = null; master = null; started = false; armed = false; voices = 0;
    buffers.clear();
}

/// How many voices have been started since load. A seam for the probe to prove a
/// swing actually reached the speakers, not just the render payload.
export function sfxPlayed() { return lastLog.plays; }
