namespace AlfarQuest.Client.Game;

/// <summary>How a hero's tally of deeds leaves the simulation.
///
/// The same seam as <see cref="LootBridge"/> and <see cref="RewardBridge"/>: the
/// engine announces that a hero did something countable — landed a blow, took
/// one, felled an enemy, opened a chest — and whoever keeps the party's sheets
/// files it against that hero. Unset (a test, or before the party loads) the
/// events are simply dropped, so the simulation never depends on the UI.
///
/// Every event names the hero it belongs to, because statistics are individual:
/// the kill goes to whoever landed it, the damage taken to whoever was hit.</summary>
public static class StatBridge
{
    public static Action<string, string, long>? OnStat;

    public static void Record(string heroKey, string kind, long amount = 1)
    {
        if (string.IsNullOrEmpty(heroKey) || amount == 0) return;
        OnStat?.Invoke(heroKey, kind, amount);
    }
}
