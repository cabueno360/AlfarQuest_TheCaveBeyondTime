namespace AlfarQuest.Client.Game;

/// <summary>How drops leave the simulation.
///
/// Same seam as <see cref="CharacterStats"/>, in the other direction: the engine
/// announces what fell, and whoever owns the party's belongings decides where it
/// goes. Unset — in a test, or before the party loads — kills still work and the
/// loot is simply discarded, so the simulation never depends on the UI.</summary>
public static class LootBridge
{
    /// <summary>item id, count, and the hero the drop belongs to — whoever was
    /// being steered when it fell. Loot is individual now, so the owner travels
    /// with every drop rather than the receiver guessing.</summary>
    public static Action<string, int, string>? OnDrop;

    /// <summary>Counters so a test can tell "nothing dropped" apart from "the
    /// drop happened and nobody was listening" — two very different bugs that
    /// look identical from outside.</summary>
    public static int Attempted { get; private set; }
    public static int Delivered { get; private set; }

    public static void Drop(string item, int count, string ownerKey)
    {
        Attempted++;
        if (OnDrop is null) return;
        OnDrop(item, count, ownerKey);
        Delivered++;
    }
}
