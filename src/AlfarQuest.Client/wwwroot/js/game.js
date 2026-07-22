// =====================================================================
//  Alfar Quest — presentation layer
//
//  Orchestration only. This file owns the frame loop and the lifetime of a
//  session; input, atlases, terrain, entities and HUD each live in their own
//  module. It calls the C# engine (AlfarQuest.Client.GameEngine.Tick) once per
//  frame and paints what comes back.
//
//  Static geometry — map, props, spawn/exit — is NOT in the per-frame payload.
//  It is fetched via Snapshot() whenever the world's revision changes. Shipping
//  it every frame cost 125 KB and ~160 ms of C# serialisation per tick on the
//  80x80 outdoor map, which pinned the game at 6 fps.
// =====================================================================
import { initGfx, sizeToParent, canvas, ctx, lightCanvas, lctx, hexA } from "./gfx.js";
import { attachInput, detachInput, readInput, resetLatch } from "./input.js";
import { ATLAS, loadAtlases } from "./atlas.js";
import { buildFloorCanvas, drawFloor } from "./world/floor.js";
import { buildScenery } from "./world/scenery.js";
import { drawEntity, drawDecal } from "./render/entities.js";
import { drawHud, drawFloaters } from "./render/hud.js";
import { createMusic } from "./music.js";
import { initSfx, playSounds, setSfxMuted, stopSfx, sfxPlayed } from "./sfx.js";

const ASM = "AlfarQuest.Client";

let raf = 0, running = false, last = 0;
// Gameplay hold. While set the engine is not ticked at all, so enemies, AI,
// projectiles, cooldowns and animations freeze together and resume exactly
// where they stopped — there is no partial-pause state to get wrong.
let paused = false;
let activeHeroKey = "";      // whoever the player is currently steering
let lastState = null;       // last frame, so menus can read pools without ticking
// Sound on or off for the whole session. The music and effect modules each hold
// their own gain, but the synthesised level-up chime is made here and needs to
// know too — kept in step by toggleMute.
let muted = false;
// Music is per-session: created by startGame, dropped by stopGame. The stage
// each track belongs to is remembered so the frame loop can ask for the right
// one every frame without knowing which is already playing.
let music = null, tracks = null;
let onResize = null;

let scenery = null;      // cave scenery, scattered client-side
let floorCanvas = null;  // the whole chamber floor, pre-painted
let builtRev = -1;       // which world revision the caches belong to
let snap = null, snapRev = -1;

// ---------------------------------------------------------------------
//  Rendering
// ---------------------------------------------------------------------
function render(s) {
    const W = canvas.width, H = canvas.height;
    const cam = s.cam || { x: 0, y: 0 };
    const shakeMag = (s.hud && s.hud.shake ? s.hud.shake : 0) * 10;
    const sx = (Math.random() - 0.5) * shakeMag;
    const sy = (Math.random() - 0.5) * shakeMag;

    ctx.fillStyle = "#04030a";
    ctx.fillRect(0, 0, W, H);

    ctx.save();
    ctx.translate(W / 2 - cam.x + sx, H / 2 - cam.y + sy);

    // Descending rebuilds the chamber, so the cached floor and scenery — which
    // are keyed to the old crystal layout — have to be thrown away.
    if (s.rev !== snapRev) {
        snapRev = s.rev;
        try { snap = JSON.parse(DotNet.invokeMethod(ASM, "Snapshot")); }
        catch (err) { console.error("world snapshot failed", err); }
        floorCanvas = null; scenery = null; builtRev = s.rev;
    }
    if (!snap) return;
    // Merge the static geometry back in so the rest of the frame reads as before.
    s.map = snap.map; s.props = snap.props; s.arches = snap.arches; s.npcs = snap.npcs;
    s.spawn = snap.spawn; s.exit = snap.exit;
    s.chamberW = snap.chamberW; s.chamberH = snap.chamberH;

    // World-space rectangle the camera can actually see, padded so tall sprites
    // anchored just off-screen still draw their tops.
    const PAD = 160;
    const view = {
        x0: cam.x - W / 2 - PAD, y0: cam.y - H / 2 - PAD,
        x1: cam.x + W / 2 + PAD, y1: cam.y + H / 2 + PAD,
    };
    const inView = (e) => e.x >= view.x0 && e.x <= view.x1 && e.y >= view.y0 && e.y <= view.y1;

    if (!floorCanvas && (ATLAS.caves.ready || ATLAS.outTiles.ready)) floorCanvas = buildFloorCanvas(s);
    drawFloor(s, view, floorCanvas);

    // Props are authored by the engine now (it owns their collision), so the
    // client only draws what it is told.
    const props = (s.props || []).map(p => ({ t: "prop", x: p.x, y: p.y, cell: [p.cx, p.cy], s: p.s, flip: p.flip, kind: p.k, v: p.v || 0 }));
    const npcs = (s.npcs || []).map(n => ({ t: "npc", x: n.x, y: n.y, f: n.f, kind: n.kind,
                                            name: n.name, inReach: n.name === s.hud?.promptName }));

    // sort: floor decals & scenery first, then dynamic. Props share the crystals'
    // tier so the cavern's static furniture behaves as one layer behind the actors.
    const order = { exit: 0, crystal: 3, prop: 3, npc: 3, particle: 2, husk: 3, proj: 4, slash: 5, nova: 5, hero: 3 };
    // Culling before the sort matters twice over: fewer draw calls, and a much
    // shorter list to sort every frame. Stage 1 carries ~1400 props and only a
    // few dozen are ever on screen.
    // Sort on the GROUND LINE, not the raw y. A hero's sprite stands with its
    // feet at y + r*0.95 while a prop's y already is its base, so sorting both
    // by y alone let a tree whose trunk sat within that offset draw over a hero
    // standing in front of it.
    const ground = (e) => e.t === "hero" || e.t === "husk" ? e.y + (e.r || 16) * 0.95 : e.y;
    const ents = [...s.ents, ...props, ...npcs].filter(inView)
        .sort((a, b) => (order[a.t] ?? 9) - (order[b.t] ?? 9) || ground(a) - ground(b));
    for (let i = 0; i < ents.length; i++) { ents[i]._i = i; drawEntity(ents[i]); }

    drawFloaters(s.floats, cam);

    // additive colored glow for each light source (cave only — see below)
    if ((s.stage || 2) !== 1) {
    ctx.globalCompositeOperation = "lighter";
    for (const L of (s.lights || [])) {
        const g = ctx.createRadialGradient(L.x, L.y, 0, L.x, L.y, L.rad);
        g.addColorStop(0, hexA(L.c, 0.22));
        g.addColorStop(1, hexA(L.c, 0));
        ctx.fillStyle = g;
        ctx.beginPath(); ctx.arc(L.x, L.y, L.rad, 0, 7); ctx.fill();
    }
    ctx.globalCompositeOperation = "source-over";
    }
    ctx.restore();

    // Daylight outdoors: the torch-and-darkness model belongs to the cave, and
    // running it on Stage 1 would hide the map the player is meant to read.
    if ((s.stage || 2) === 1) { drawHud(s.hud); return; }

    // darkness overlay with light holes
    lctx.globalCompositeOperation = "source-over";
    // 0.87, not 0.93: the wall autotiles carry most of the cave's character and
    // at full darkness none of it reads. Torchlight still dominates — this only
    // lifts the rock enough to see its shape.
    lctx.fillStyle = "rgba(3,2,12,0.87)";
    lctx.fillRect(0, 0, W, H);
    lctx.globalCompositeOperation = "destination-out";
    for (const L of (s.lights || [])) {
        const lx = L.x - cam.x + W / 2 + sx;
        const ly = L.y - cam.y + H / 2 + sy;
        const g = lctx.createRadialGradient(lx, ly, 0, lx, ly, L.rad);
        g.addColorStop(0, "rgba(0,0,0,1)");
        g.addColorStop(0.55, "rgba(0,0,0,0.6)");
        g.addColorStop(1, "rgba(0,0,0,0)");
        lctx.fillStyle = g;
        lctx.beginPath(); lctx.arc(lx, ly, L.rad, 0, 7); lctx.fill();
    }
    lctx.globalCompositeOperation = "source-over";
    ctx.drawImage(lightCanvas, 0, 0);

    drawHud(s.hud);
}

function loop(now) {
    if (!running) return;
    if (paused) {
        // Keep the last frame on screen and keep the clock honest, so unpausing
        // does not hand the engine one enormous dt.
        last = now;
        raf = requestAnimationFrame(loop);
        return;
    }
    const dt = now - last; last = now;

    let state;
    try {
        const json = DotNet.invokeMethod(ASM, "Tick", dt, JSON.stringify(readInput(canvas.width, canvas.height)));
        state = JSON.parse(json);
    } catch (err) {
        console.error("engine tick failed", err);
        running = false; return;
    }
    lastState = state;
    activeHeroKey = state.hud?.party?.find(p => p.active)?.key || activeHeroKey;
    // Asked every frame and answered once: play() with the track already on is
    // deliberately nothing, so this costs a string comparison.
    const track = tracks?.[state.hud?.stage];
    if (track) music?.play(track);
    // The frame's sound events — swings, hits, footfalls, a chest opening. The
    // engine has already faded each for distance; this just turns them into
    // voices. Empty on a quiet frame, which is most of them.
    playSounds(state.sounds);
    // Accumulated for the probe: a sound is in the payload for a single frame, so
    // polling snapshots misses sparse events. This rolling set does not.
    if (state.sounds) for (const s of state.sounds) _familiesSeen.add(s.f);
    render(state);
    raf = requestAnimationFrame(loop);
}

export function startGame(heroKeysCsv, approachUrl, cavernUrl, host) {
    const el = document.getElementById("gameCanvas");
    if (!el) return;
    initGfx(el);

    // Menu keys are bound here rather than on a Blazor element: the canvas holds
    // focus during play, so a DOM handler on the page would never see them.
    attachInput(el, (action) => {
        // The action is passed on rather than discarded. It used to be ignored
        // and every menu key toggled the character sheet, which meant Escape —
        // the key for "close this" — opened one instead.
        if (host) host.invokeMethodAsync("MenuKey", action, activeHeroKey);
    });
    onResize = () => sizeToParent();
    window.addEventListener("resize", onResize);

    loadAtlases(() => { floorCanvas = null; });

    // Above ground and below it are different places and get different music.
    // Which one is playing is decided by the world's own stage every frame, not
    // by a transition handler, so loading a save straight into the mine starts
    // on the right track rather than switching a moment after arriving.
    tracks = { 1: approachUrl, 2: cavernUrl };
    music = createMusic(0.55);
    // Starts decoding the effect library now, so the first swing has its sound
    // ready rather than a beat late.
    initSfx();

    DotNet.invokeMethod(ASM, "Init", heroKeysCsv, el.width, el.height);
    running = true;
    last = performance.now();
    raf = requestAnimationFrame(loop);
}

export function stopGame() {
    running = false;
    cancelAnimationFrame(raf);
    resetLatch();                       // don't carry a press into the next delve
    music?.stop(); music = null; tracks = null;
    stopSfx();
    detachInput();
    window.removeEventListener("resize", onResize);
}

/// Freeze or resume gameplay. Called by any menu that needs the world to stop.
/// The hero the player is steering right now, so a menu opens on the right one.
export function activeHero() {
    return activeHeroKey;
}

/// Live pools for every party member. Read when a menu opens rather than every
/// frame — the world is paused by then, so one read is enough.
/// Whether a craftsman is within talking range, read when a menu opens.
export function atCraftsman() {
    return !!lastState?.hud?.atCraftsman;
}

export function partyVitals() {
    return (lastState?.hud?.party || []).map(p => ({
        key: p.key, hp: p.hp, maxHp: p.mhp,
        mana: p.mana, maxMana: p.mmana,
        stamina: p.stam, maxStamina: p.mstam,
    }));
}

export function setPaused(value) {
    paused = !!value;
    return paused;
}

/// The level-up chime.
///
/// Synthesised rather than loaded from a file: it is three notes, and shipping
/// an audio asset for it would cost a download and a decode for something the
/// Web Audio API makes in a dozen lines. It also means the sound can never be
/// the one thing that failed to load.
///
/// Silent when muted, and it never throws — a browser that refuses to start an
/// AudioContext must not take the level-up with it.
export function playLevelUp() {
    if (muted) return;
    try {
        const AudioCtx = window.AudioContext || window.webkitAudioContext;
        if (!AudioCtx) return;
        const ac = new AudioCtx();

        // A rising major triad: the shape every game has used for "you grew"
        // since the eighties, because it works.
        [523.25, 659.25, 783.99].forEach((freq, i) => {
            const at = ac.currentTime + i * 0.09;
            const osc = ac.createOscillator();
            const gain = ac.createGain();
            osc.type = "triangle";
            osc.frequency.value = freq;
            // Ramp rather than switch: a gain that jumps to zero clicks.
            gain.gain.setValueAtTime(0.0001, at);
            gain.gain.exponentialRampToValueAtTime(0.22, at + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.0001, at + 0.55);
            osc.connect(gain).connect(ac.destination);
            osc.start(at);
            osc.stop(at + 0.6);
        });

        // Release the hardware once it has finished, or a long session leaks a
        // context per level.
        setTimeout(() => ac.close().catch(() => { }), 1200);
    } catch { /* no audio available; the window and particles still land */ }
}

/// Where the party is, for the save. Read on the way out, like the vitals.
export function currentRegion() {
    return lastState?.hud?.region || "the approach";
}

/// The last frame's HUD, unmodified.
///
/// A diagnostic, not a game feature: the canvas is pixels, so without this there
/// is no way for a test to ask whether the XP bar is showing what the sheet
/// thinks. Reads the frame that was already computed and holds no state, so it
/// cannot change what the game does.
export function hudSnapshot() {
    return lastState?.hud ?? null;
}

export function toggleMute() {
    // One button for the whole session's sound: the music and the effects mute
    // and unmute together. Keyed off the music's state so the two never drift
    // into one being on while the other is off.
    muted = music ? music.toggleMuted() : !muted;
    setSfxMuted(muted);
    return muted;
}

/// Which track the world's stage has called for. A seam for the probe.
export function nowPlaying() { return music?.url ?? null; }

/// How many effect voices have started this session. A seam for the probe to
/// prove a swing reached the speakers, not just the render payload.
export function sfxCount() { return sfxPlayed(); }

/// The last frame's sound events, before the audio layer touched them. Lets the
/// probe check the engine raised the right family, separately from whether it
/// played.
export function lastSounds() { return lastState?.sounds ?? []; }

/// The creature catalogue as data. A seam for the probe to assert the bestiary is
/// varied and lore-placed, without depending on what happened to spawn.
export function bestiary() {
    try { return JSON.parse(DotNet.invokeMethod(ASM, "Bestiary")); } catch { return []; }
}

/// Stages a controlled encounter — the party on open ground, creatures around it,
/// already hunting. A test seam so the AI can be watched on a deterministic stage
/// rather than wherever a headless walk happened to end up. Never used in play.
export function debugEncounter() {
    try { DotNet.invokeMethod(ASM, "DebugEncounter"); } catch { /* engine not up */ }
}

/// Every sound family raised since the last reset. A sparse event lives in the
/// payload for one frame, so a polling probe misses it; this accumulates.
const _familiesSeen = new Set();
export function soundFamiliesSeen() { return [..._familiesSeen]; }
export function resetSoundFamilies() { _familiesSeen.clear(); }