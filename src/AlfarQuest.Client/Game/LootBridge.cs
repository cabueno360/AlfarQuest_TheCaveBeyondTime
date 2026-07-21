namespace AlfarQuest.Client.Game;

/// <summary>How drops leave the simulation.
///
/// Same seam as <see cref="CharacterStats"/>, in the other direction: the engine
/// announces what fell, and whoever owns the party's belongings decides where it
/// goes. Unset — in a test, or before the party loads — kills still work and the
/// loot is simply discarded, so the simulation never depends on the UI.</summary>
public static class LootBridge
{
    public static Action<string, int>? OnDrop;

    /// <summary>Counters so a test can tell "nothing dropped" apart from "the
    /// drop happened and nobody was listening" — two very different bugs that
    /// look identical from outside.</summary>
    public static int Attempted { get; private set; }
    public static int Delivered { get; private set; }

    public static void Drop(string item, int count = 1)
    {
        Attempted++;
        if (OnDrop is null) return;
        OnDrop(item, count);
        Delivered++;
    }
}
