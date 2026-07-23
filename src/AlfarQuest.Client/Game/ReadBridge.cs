namespace AlfarQuest.Client.Game;

/// <summary>What a page of readable text carries to the interface — a journal, a
/// letter, an examined object's description. Pages are turned one at a time so a
/// long entry reads like a book, not a wall.</summary>
public sealed record OpenedReading(string Title, string Kind, IReadOnlyList<string> Pages);

/// <summary>How an examined thing reaches the reading panel.
///
/// The same seam as <see cref="MerchantBridge"/>: the engine notices the player
/// pressed [E] on the journal or an old letter and offers its text; whoever runs
/// the reading window opens it. Unset — in a test, or before the UI loads — the
/// offer is refused, so pressing [E] simply does nothing.</summary>
public static class ReadBridge
{
    /// <summary>Engine → UI: open this text. Returns true if a reader was listening.</summary>
    public static Func<OpenedReading, bool>? OnOpen;

    /// <summary>UI → engine: the reader closed, so release the hero it was holding.</summary>
    public static Action? OnClose;

    public static int Opened { get; private set; }

    public static bool Offer(OpenedReading r)
    {
        if (OnOpen is null) return false;
        var opened = OnOpen(r);
        if (opened) Opened++;
        return opened;
    }

    public static void Close() => OnClose?.Invoke();
}
