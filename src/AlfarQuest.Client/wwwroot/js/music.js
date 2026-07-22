// =====================================================================
//  Music.
//
//  One player, told what should be playing. It decides how to get there —
//  crossfading out of whatever was on, and waiting for permission if the
//  browser has not given it yet.
//
//  Two things make this worth a module rather than an Audio element at each
//  call site. The first is autoplay: browsers refuse to start sound until the
//  page has been interacted with, so play() is attempted immediately and, when
//  refused, one-shot listeners start it on the first click or key instead. That
//  is the difference between music that sometimes works and music that starts
//  the moment it legally can.
//
//  The second is that a track change is a crossfade, not a cut. Walking into
//  the mine should not sound like a track skipping.
// =====================================================================

const FADE_MS = 900;
const STEP_MS = 40;

/// A music player. Each screen owns one; they do not share state, so leaving the
/// title screen cannot silence the game and vice versa.
export function createMusic(defaultVolume = 0.55) {
    let current = null;          // { audio, url, volume }
    let muted = false;
    let armed = null;            // gesture listeners, while waiting for permission

    /// Rides a volume to a target and then runs `done`. Held on the element so a
    /// second fade over the same track replaces the first rather than fighting
    /// it — which is what turned a quick there-and-back through a doorway into
    /// two tracks at half volume.
    function ride(audio, to, done) {
        clearInterval(audio._fade);
        const from = audio.volume;
        const steps = Math.max(1, Math.round(FADE_MS / STEP_MS));
        let i = 0;
        audio._fade = setInterval(() => {
            i++;
            audio.volume = Math.min(1, Math.max(0, from + (to - from) * (i / steps)));
            if (i >= steps) { clearInterval(audio._fade); audio._fade = 0; done?.(); }
        }, STEP_MS);
    }

    function disarm() {
        if (!armed) return;
        for (const ev of ["pointerdown", "keydown", "touchstart"])
            window.removeEventListener(ev, armed);
        armed = null;
    }

    /// Waits for the first gesture, then tries again. Armed only when a play()
    /// was actually refused, so a page that was allowed to start never installs
    /// listeners it will not use.
    function arm() {
        disarm();
        armed = () => { disarm(); current?.audio.play().catch(() => { }); };
        for (const ev of ["pointerdown", "keydown", "touchstart"])
            window.addEventListener(ev, armed, { once: true, passive: true });
    }

    function fadeOutAndDrop(audio) {
        ride(audio, 0, () => { audio.pause(); audio.src = ""; });
    }

    return {
        /// What should be playing now. Calling it again with the track already on
        /// is deliberately nothing at all — the game asks every frame.
        play(url, { loop = true, volume = defaultVolume, onEnded = null } = {}) {
            if (current?.url === url) return;

            const previous = current;
            const audio = new Audio(url);
            audio.loop = loop;
            audio.preload = "auto";
            audio.muted = muted;
            // From silence only when there is something to fade out of. Starting
            // the title track at zero with nothing playing would just be a
            // second of silence on arrival.
            audio.volume = previous ? 0 : volume;
            if (onEnded) audio.addEventListener("ended", onEnded, { once: true });

            current = { audio, url, volume };
            audio.play().catch(arm);

            if (previous) { fadeOutAndDrop(previous.audio); ride(audio, volume); }
        },

        /// Silences without stopping, so unmuting resumes where the track is
        /// rather than where it was left.
        toggleMuted() {
            muted = !muted;
            if (current) current.audio.muted = muted;
            return muted;
        },

        get muted() { return muted; },

        /// What is playing, and the element playing it.
        ///
        /// The url is how the probe checks that the right track is on. The
        /// element is how it tests the title hand-over without sitting through
        /// the intro: it winds the intro to its last moment and waits for the
        /// real "ended" to fire, rather than simulating the thing under test.
        get url() { return current?.url ?? null; },
        get element() { return current?.audio ?? null; },

        /// Fades out and forgets. Leaving a screen, not pausing it.
        stop() {
            disarm();
            if (!current) return;
            const { audio } = current;
            current = null;
            fadeOutAndDrop(audio);
        },
    };
}
