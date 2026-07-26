namespace AlfarQuest.Client.Game;

/// <summary>How the engine asks the interface to play a fullscreen cutscene.
///
/// The same seam as <see cref="DialogueBridge"/>: the simulation reaches a moment
/// that wants a video — the first descent into the Cave Beyond Time — and whoever
/// owns the screen plays it and freezes the game until it ends. Unset (a test, or
/// before the UI loads) the request is refused and the game simply carries on, so
/// the simulation never depends on a screen being attached.</summary>
public static class VideoBridge
{
    /// <summary>Engine → UI: play this video fullscreen and hold the game behind it.
    /// Returns true if a screen was listening.</summary>
    public static Func<string, bool>? OnPlay;

    /// <summary>UI → engine: the cutscene finished or was skipped. Optional — the
    /// video is cosmetic, so nothing is required to react.</summary>
    public static Action? OnEnded;

    /// <summary>How many cutscenes have played — a counter so a test can tell "the
    /// request was made and refused" from "no request happened".</summary>
    public static int Played { get; private set; }

    public static bool Play(string src)
    {
        if (OnPlay is null) return false;
        var ok = OnPlay(src);
        if (ok) Played++;
        return ok;
    }

    public static void NotifyEnded() => OnEnded?.Invoke();
}
