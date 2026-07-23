namespace AlfarQuest.Client.Game;

/// <summary>How a merchant reaches the interface.
///
/// The same seam as <see cref="ContainerBridge"/>: the engine notices the player
/// pressed [E] on a shopkeeper and offers them; whoever runs the shop window opens
/// it and says so. Unset — a test, or before the UI loads — the offer is refused,
/// so pressing [E] on a merchant simply does nothing rather than crashing.</summary>
public static class MerchantBridge
{
    /// <summary>Engine → UI: open the shop for this NPC, on behalf of the hero
    /// being steered. Returns true if a shop was listening and opened.</summary>
    public static Func<string, string, bool>? OnOpen;

    /// <summary>UI → engine: the shop closed, so release the hero it was holding.</summary>
    public static Action? OnClose;

    /// <summary>How many times a shop has been opened — a counter so a test can
    /// tell "the offer was made and refused" from "no offer happened".</summary>
    public static int Opened { get; private set; }

    public static bool Offer(string npcId, string buyerKey)
    {
        if (OnOpen is null) return false;
        var opened = OnOpen(npcId, buyerKey);
        if (opened) Opened++;
        return opened;
    }

    public static void Close() => OnClose?.Invoke();
}
