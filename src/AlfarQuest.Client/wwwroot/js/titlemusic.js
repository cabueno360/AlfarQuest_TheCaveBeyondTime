// =====================================================================
//  Title screen music.
//
//  Browsers refuse to start audio until the user has interacted with the
//  page, so play() is attempted immediately and, if it is rejected, we arm
//  one-shot listeners and start on the first click/key/touch instead. That
//  is the difference between "music sometimes works" and "music always
//  works as soon as it legally can".
// =====================================================================
let audio = null;
let unlock = null;

export function playIntro(url, volume) {
    stopIntro();
    audio = new Audio(url);
    audio.loop = true;
    audio.volume = volume ?? 0.5;
    audio.preload = "auto";

    const tryPlay = () => audio && audio.play().catch(() => { });

    tryPlay();
    // Armed regardless: if the first attempt was allowed these never fire a
    // second play, because play() on an already-playing element is a no-op.
    unlock = () => { tryPlay(); teardown(); };
    for (const ev of ["pointerdown", "keydown", "touchstart"])
        window.addEventListener(ev, unlock, { once: true, passive: true });
}

function teardown() {
    if (!unlock) return;
    for (const ev of ["pointerdown", "keydown", "touchstart"])
        window.removeEventListener(ev, unlock);
    unlock = null;
}

export function stopIntro() {
    teardown();
    if (!audio) return;
    // Fade out rather than cutting, so leaving the title screen isn't a jolt.
    const a = audio;
    audio = null;
    const step = a.volume / 12;
    const timer = setInterval(() => {
        a.volume = Math.max(0, a.volume - step);
        if (a.volume <= 0.001) { clearInterval(timer); a.pause(); a.src = ""; }
    }, 40);
}

export function toggleIntroMute() {
    if (!audio) return true;
    audio.muted = !audio.muted;
    return audio.muted;
}
