namespace AlfarQuest.Client.Game;

// =====================================================================
//  Sound.
//
//  The simulation names an event — "swing", "hit", "chest" — and this
//  collects them for the frame. The audio layer in the browser owns
//  everything after that: which of a family's takes to play, how to pool
//  the voices, how not to run out of channels. The engine never touches
//  a file.
//
//  This sits beside the particle system on purpose. An effect that plays
//  motes usually wants a sound too, so VfxSpec can name one and Play()
//  raises it here — one row of the catalogue, both senses.
// =====================================================================
public partial class World
{
    readonly List<RSound> _sounds = new();

    /// <summary>Last time each family sounded, so a frame that strikes two husks
    /// at once does not stack the same clip on itself into a flam. Keyed by
    /// family; the window is a few frames.</summary>
    readonly Dictionary<string, float> _sfxLast = new();
    float _sfxClock;

    const float SfxDedupe = 0.05f;   // seconds; one clip per family per this window
    const int MaxSoundsPerFrame = 8; // a busy frame is not eight distinct noises

    /// <summary>Raises a sound at a point. Volume falls off past the edge of the
    /// view, so a companion's fight two screens away is heard faintly rather than
    /// at point-blank. The player's own actions happen under the camera and come
    /// through at full strength.</summary>
    public void PlaySound(string? family, Vec at, float gain = 1f)
    {
        if (string.IsNullOrEmpty(family)) return;
        if (_sounds.Count >= MaxSoundsPerFrame) return;

        if (_sfxLast.TryGetValue(family, out var t) && _sfxClock - t < SfxDedupe) return;
        _sfxLast[family] = _sfxClock;

        // Full inside the view, tapering to nothing about a screen beyond it.
        var d = (at - Camera).Len();
        var edge = MathF.Max(ViewW, ViewH) * 0.5f;
        var vol = gain * Math.Clamp(1f - (d - edge) / (edge * 1.6f), 0f, 1f);
        if (vol < 0.04f) return;     // too far to matter; don't spend a voice on it

        _sounds.Add(new RSound { f = family, v = MathF.Min(1f, vol) });
    }

    /// <summary>Clears the frame's sounds and advances the dedupe clock. Called at
    /// the top of Update so what accumulates during the tick is exactly what the
    /// render payload carries — no sound survives into the next frame to play twice.</summary>
    void BeginSfxFrame(float dt)
    {
        _sounds.Clear();
        _sfxClock += dt;
    }
}
