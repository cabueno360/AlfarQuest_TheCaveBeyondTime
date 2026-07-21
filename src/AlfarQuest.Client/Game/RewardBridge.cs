using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

/// <summary>How experience leaves the simulation.
///
/// Same shape as <see cref="LootBridge"/>: the engine knows what happened, the
/// party sheet knows what a level is. The engine announces and gets told how many
/// heroes levelled, which is all it needs to fire the celebration in the right
/// place — it never learns what a level *is*.
///
/// Unset (in a test, or before the party loads) awards are counted and dropped,
/// so the simulation runs without the UI existing.</summary>
public static class RewardBridge
{
    /// <summary>Awards XP to the party. Takes the hero the player is steering —
    /// the sheet needs it to know whose portrait belongs in the level-up window,
    /// and the engine is the only thing that knows who it is.
    /// Returns how many heroes levelled up.</summary>
    public static Func<int, XpSource, string, int>? OnXp;

    /// <summary>Counters so a test can tell "nothing was awarded" apart from "the
    /// award happened and nobody was listening" — two very different bugs that
    /// look identical from outside. The same distinction caught a phantom loot
    /// bug once already.</summary>
    public static int Awarded { get; private set; }
    public static int Delivered { get; private set; }
    public static int TotalXp { get; private set; }

    /// <returns>Heroes that levelled, or 0 when nobody is listening.</returns>
    public static int Grant(int xp, XpSource source, string leadHeroKey)
    {
        if (xp <= 0) return 0;

        Awarded++;
        TotalXp += xp;
        if (OnXp is null) return 0;

        Delivered++;
        return OnXp(xp, source, leadHeroKey);
    }

    /// <summary>Rewards this player has already taken, consulted when the
    /// overworld is built.
    ///
    /// Without it, leaving and re-entering rebuilds the world with every chest
    /// refilled and every region unvisited — which makes the door an infinite XP
    /// source. Only Stage 1 consults it: the cave is generated fresh each descent,
    /// so its rewards are meant to come back.</summary>
    public static Func<IReadOnlyCollection<string>>? ClaimedRewards;

    /// <summary>Announces that a one-shot reward has been taken, so it can be
    /// written to the save.</summary>
    public static Action<string>? OnClaimed;

    public static IReadOnlyCollection<string> Claimed() => ClaimedRewards?.Invoke() ?? [];

    public static void Claim(string key) => OnClaimed?.Invoke(key);

    /// <summary>What has happened to each container, and what is still in it.
    ///
    /// Separate from the claim list because a claim is a fact — this was found —
    /// while a container carries a remainder that changes every time the player
    /// takes something out of it.</summary>
    public static Func<IReadOnlyDictionary<string, ContainerSave>>? ContainerStates;

    /// <summary>Records a container's current state. Called by the engine when
    /// one is opened and by the interface after every take, because taking is the
    /// interface's to do and the remainder is what has to be written down.</summary>
    public static Action<ContainerSave>? OnContainerSaved;

    public static IReadOnlyDictionary<string, ContainerSave> Containers() =>
        ContainerStates?.Invoke() ?? new Dictionary<string, ContainerSave>();

    public static void SaveContainer(ContainerSave state) => OnContainerSaved?.Invoke(state);

    /// <summary>For tests that need a clean slate between runs.</summary>
    public static void ResetCounters() => (Awarded, Delivered, TotalXp) = (0, 0, 0);
}
