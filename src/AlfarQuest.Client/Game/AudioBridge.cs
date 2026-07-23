namespace AlfarQuest.Client.Game;

/// <summary>How the interface asks for a sound.
///
/// Most sound is raised by the simulation, at a place in the world, and fades
/// with distance — see <see cref="World.PlaySound"/>. But a purchase happens in a
/// menu, not on the map: there is no world point for the coin, and it should play
/// at full strength because the player did it. This carries those.
///
/// The engine sets <see cref="Play"/> to its own queue; the shop calls it. Unset —
/// a test with no world — the call is a no-op, so a sale still goes through in
/// silence rather than failing.</summary>
public static class AudioBridge
{
    /// <summary>UI → engine: play this sound family on the next frame, centred and
    /// full-volume. Named, not a file — the browser's audio layer still owns which
    /// take to play, exactly as it does for a swing.</summary>
    public static Action<string>? Play;
}
