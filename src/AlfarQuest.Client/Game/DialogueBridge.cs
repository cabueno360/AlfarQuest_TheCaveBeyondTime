namespace AlfarQuest.Client.Game;

/// <summary>How a talking NPC reaches the conversation window.
///
/// The same seam as <see cref="MerchantBridge"/>: the engine notices the player
/// pressed [E] on someone who answers a menu of questions and offers them; whoever
/// runs the dialogue window opens it and says so. Unset — a test, or before the UI
/// loads — the offer is refused, and the NPC falls back to cycling their one-line
/// balloon, so the world still behaves with no window attached.</summary>
public static class DialogueBridge
{
    /// <summary>Engine → UI: open the question menu for this NPC. Returns true if a
    /// window was listening and opened.</summary>
    public static Func<string, bool>? OnOpen;

    /// <summary>UI → engine: the conversation closed, so release the held hero.</summary>
    public static Action? OnClose;

    /// <summary>How many conversations have opened — a counter so a test can tell
    /// "the offer was made and refused" from "no offer happened".</summary>
    public static int Opened { get; private set; }

    public static bool Offer(string npcId)
    {
        if (OnOpen is null) return false;
        var opened = OnOpen(npcId);
        if (opened) Opened++;
        return opened;
    }

    public static void Close() => OnClose?.Invoke();
}
