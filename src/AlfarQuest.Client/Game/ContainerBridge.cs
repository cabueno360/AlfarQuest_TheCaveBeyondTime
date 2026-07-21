using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

/// <summary>How an opened container reaches the interface.
///
/// The same seam as LootBridge and RewardBridge, and for the same reason: the
/// engine knows a chest was opened and what fell out of it, and knows nothing
/// about windows. Unset — in a test, or before the party loads — the contents
/// are handed straight to the party instead, so the simulation still works with
/// no interface attached.</summary>
public static class ContainerBridge
{
    /// <summary>Offers an opened container's contents. Returns true if something
    /// took charge of them; false means nobody is listening and the caller should
    /// deliver them itself.</summary>
    public static Func<OpenedContainer, bool>? OnOpened;

    /// <summary>Whether the party is carrying a key. Asked before a locked
    /// container will open, and asked of the party rather than answered here,
    /// because the pouch is not the engine's to read.</summary>
    public static Func<string, bool>? HasKey;

    /// <summary>Spends the key. Separate from the check so a container that
    /// cannot be opened for some other reason does not eat one.</summary>
    public static Action<string>? ConsumeKey;

    /// <summary>Counters, in the same spirit as the drop counters: they separate
    /// "nothing was in it" from "something was and nobody was listening".</summary>
    public static int Opened { get; private set; }
    public static int Delivered { get; private set; }

    public static bool Offer(OpenedContainer container)
    {
        Opened++;
        if (OnOpened is null || !OnOpened(container)) return false;

        Delivered++;
        return true;
    }

    public static bool CarryingKey(string key) => HasKey?.Invoke(key) ?? false;

    public static void SpendKey(string key) => ConsumeKey?.Invoke(key);

    public static void ResetCounters() => (Opened, Delivered) = (0, 0);
}

/// <param name="Key">The container's unique name, so taking from it can be
/// written back against the right one.</param>
public sealed record OpenedContainer(string Key, ContainerKind Kind, LootStack Contents);
