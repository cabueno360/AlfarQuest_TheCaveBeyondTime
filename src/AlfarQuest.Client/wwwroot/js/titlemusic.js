// =====================================================================
//  Title screen music.
//
//  Two tracks, in order: the intro plays once and hands over to the theme,
//  which loops until the player leaves. Handing over on "ended" rather than
//  on a timer means the two never overlap, and the seam does not move when
//  the intro is re-cut.
//
//  Autoplay permission and fading live in music.js — the game screen needs
//  exactly the same two things.
// =====================================================================
import { createMusic } from "./music.js";

let music = null;

/// <param name="introUrl">Plays once, from the top.</param>
/// <param name="themeUrl">Takes over when the intro finishes, and loops.</param>
export function playIntro(introUrl, themeUrl, volume) {
    stopIntro();
    music = createMusic(volume ?? 0.5);
    music.play(introUrl, {
        loop: false,
        // The theme eases in over the usual crossfade. There is nothing left to
        // fade out of by then, so what it amounts to is a soft entry after the
        // intro's last note rather than the theme starting at full volume on top
        // of it.
        onEnded: () => music?.play(themeUrl),
    });
}

export function stopIntro() {
    music?.stop();
    music = null;
}

/// What is playing right now, and the element playing it. A seam for the probe.
export function nowPlaying() { return music?.url ?? null; }
export function nowPlayingElement() { return music?.element ?? null; }

export function toggleIntroMute() {
    return music ? music.toggleMuted() : true;
}
