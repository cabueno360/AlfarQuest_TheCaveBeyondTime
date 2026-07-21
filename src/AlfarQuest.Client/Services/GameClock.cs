namespace AlfarQuest.Client.Services;

/// <summary>Whether gameplay is running.
///
/// Deliberately independent of any one menu: the inventory, settings, map and
/// quest log will each want to freeze the world, and none of them should have to
/// know about the others. A menu asks for a hold and releases it; the world runs
/// only when nobody is holding it.
/// </summary>
public sealed class GameClock
{
    private readonly HashSet<string> _holds = [];

    public bool IsPaused => _holds.Count > 0;
    public event Action? Changed;

    /// <summary>Freezes gameplay on behalf of <paramref name="reason"/>. Repeating
    /// the same reason is a no-op, so a component may call it defensively.</summary>
    public void Hold(string reason)
    {
        if (_holds.Add(reason)) Changed?.Invoke();
    }

    public void Release(string reason)
    {
        if (_holds.Remove(reason)) Changed?.Invoke();
    }

    public void Toggle(string reason)
    {
        if (_holds.Contains(reason)) Release(reason); else Hold(reason);
    }
}
