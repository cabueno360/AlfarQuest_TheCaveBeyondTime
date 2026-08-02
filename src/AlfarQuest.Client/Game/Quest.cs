namespace AlfarQuest.Client.Game;

/// <summary>Main quests carry the story; side quests are optional. The player may
/// ignore the main line and do side work, or none at all.</summary>
public enum QuestKind { Main, Side }

/// <summary>Where a quest stands for this player.</summary>
public enum QuestStatus { NotStarted, Active, Complete }

/// <summary>How one step reads in the journal.</summary>
public enum StepState { Done, Current, Upcoming }

/// <summary>One step of a quest: the objective line shown while it is the current
/// step, and the flag whose presence marks it done. Steps are ordered — a later
/// flag being set implies the earlier steps are behind you (so entering the Cave
/// completes "find the mine" even if you never read the intervening step).</summary>
public sealed record QuestStep(string Objective, string DoneFlag);

/// <summary>What finishing a quest pays out. Items are catalogue IDs (resolved by
/// the party when it grants them, so this stays free of the item model) — the rare,
/// story-bound ones a quest gives, kept separate from a chest's common drops. Shown
/// at the offer, so the player accepts knowing what is on the table, and granted
/// again — for real — the moment the quest completes.</summary>
public sealed record QuestReward(int Xp = 0, int Gold = 0, IReadOnlyList<string>? Items = null)
{
    public static readonly QuestReward None = new();
    public IReadOnlyList<string> ItemIds => Items ?? [];
    public bool IsEmpty => Xp == 0 && Gold == 0 && ItemIds.Count == 0;
}

/// <summary>A quest, derived entirely from the persisted flag set (the same
/// ClaimedRewards store one-shot rewards ride, so quest progress is saved for
/// free). Nothing here holds mutable state: give it the flags and it tells you
/// where the player stands.
///
/// A side quest only counts as begun once its <see cref="StartFlag"/> is set — the
/// moment an NPC gives it — so it never appears in the journal before it is
/// offered. The main line is always underway.</summary>
public sealed record QuestDef(string Id, string Title, QuestKind Kind, string Summary,
                              IReadOnlyList<QuestStep> Steps, string StartFlag = "")
{
    /// <summary>The rare, story-bound payout for finishing the quest — shown at the
    /// offer and granted on completion. None unless a quest sets it.</summary>
    public QuestReward Reward { get; init; } = QuestReward.None;

    /// <summary>Index of the step the player is on: the first for which neither it
    /// nor any LATER step is done. Equals Steps.Count when all are done.</summary>
    public int CurrentIndex(IReadOnlyCollection<string> flags)
    {
        for (int i = 0; i < Steps.Count; i++)
            if (!Steps.Skip(i).Any(s => flags.Contains(s.DoneFlag)))
                return i;
        return Steps.Count;
    }

    public QuestStep? Current(IReadOnlyCollection<string> flags)
    {
        int i = CurrentIndex(flags);
        return i < Steps.Count ? Steps[i] : null;
    }

    public bool IsComplete(IReadOnlyCollection<string> flags) => CurrentIndex(flags) >= Steps.Count;

    /// <summary>Begun? The main line always; a side quest once its start flag (or,
    /// lacking one, any step flag) is set.</summary>
    public bool IsStarted(IReadOnlyCollection<string> flags) =>
        Kind == QuestKind.Main
        || (StartFlag.Length > 0 ? flags.Contains(StartFlag)
                                 : Steps.Any(s => flags.Contains(s.DoneFlag)));

    public QuestStatus Status(IReadOnlyCollection<string> flags) =>
        !IsStarted(flags) ? QuestStatus.NotStarted
        : IsComplete(flags) ? QuestStatus.Complete
        : QuestStatus.Active;

    /// <summary>Every step tagged done / current / upcoming, for the journal.</summary>
    public IReadOnlyList<(QuestStep Step, StepState State)> Progress(IReadOnlyCollection<string> flags)
    {
        int ci = CurrentIndex(flags);
        return Steps.Select((s, i) =>
            (s, i < ci ? StepState.Done : i == ci ? StepState.Current : StepState.Upcoming)).ToList();
    }
}

/// <summary>The campaign's quests. Story follows the book "Story for Music": the
/// Cleric of the northern vale traded his sanity to Cerno for a phial of panacea
/// for his dying wife Mirka, and swore to delve the Cave Beyond Time in Cerno's
/// stead. The party's road is to reach that Cave.</summary>
public static class QuestCatalog
{
    /// <summary>The main line. Flags: <c>quest_cave_learned</c> is set by talking to
    /// the Cleric's kin or Cerno about the Cave; <c>cave_entered</c> is set by
    /// World.EnterCave on the first descent (also when the Cleric joins). Keep these
    /// flag strings in step with those call sites.</summary>
    public static readonly QuestDef DelversPact = new(
        Id: "delvers_pact",
        Title: "The Delver's Pact",
        Kind: QuestKind.Main,
        Summary: "The Cleric of the vale traded his own sanity to Cerno for a phial of " +
                 "panacea, to save his dying wife Mirka — and swore to delve the Cave " +
                 "Beyond Time in Cerno's stead. Find the old mine that leads down to it.",
        Steps: new QuestStep[]
        {
            new("Ask in the vale after the Cave Beyond Time — the Cleric's kin will know", "quest_cave_learned"),
            new("Find the old mine and delve the Cave Beyond Time", "cave_entered"),
            // Below ground now. "cave_heart" is set by World.Rewards when the party
            // reaches the boss chamber (a persisted milestone, unlike the cave's
            // ordinary claims which are forgotten each descent); "cave_deep" is set by
            // Descend() on reaching the deepest named depth, the Cave Beyond Time
            // itself. Keep both strings in step with World.Rewards / World.Progress.
            new("Search the dark below for the Crystal Heart", "cave_heart"),
            new("Descend to the deepest reach — the Cave Beyond Time itself", "cave_deep"),
        })
    { Reward = new(Xp: 800, Gold: 300, Items: ["phial_panacea", "crystal_heart_shard"]) };

    /// <summary>Mirka's father begs the party to carry word to the Cleric below.
    /// Given by asking him "Can I help?"; done the moment the Cleric joins at the
    /// Cave (<c>cave_entered</c>).</summary>
    public static readonly QuestDef WordForTheCleric = new(
        Id: "word_for_cleric",
        Title: "Word for the Cleric",
        Kind: QuestKind.Side,
        Summary: "Mirka's father asked you to find the Cleric in the dark below and tell " +
                 "him his wife still breathes — help enough, he said, for an old man.",
        Steps: new QuestStep[]
        {
            new("Carry word to the Cleric below that Mirka still breathes", "cave_entered"),
        },
        StartFlag: "sq_word_started")
    { Reward = new(Xp: 220, Gold: 90, Items: ["mirka_locket"]) };

    /// <summary>Seek out Cerno, the one soul to come back from the Cave. Given by
    /// asking the vale's elders who Cerno is; done by finding him at the cave
    /// forecourt and hearing his warning.</summary>
    public static readonly QuestDef SeekCerno = new(
        Id: "seek_cerno",
        Title: "The Guide Who Returned",
        Kind: QuestKind.Side,
        Summary: "They speak of Cerno of Kaladash — the only man to walk out of the Cave " +
                 "Beyond Time, though he left half his mind in it. He keeps to the cave " +
                 "forecourt. Hear what he has to say before you descend.",
        Steps: new QuestStep[]
        {
            new("Find Cerno at the cave forecourt and hear his warning", "sq_cerno_heard"),
        },
        StartFlag: "sq_cerno_started")
    { Reward = new(Xp: 300, Gold: 120, Items: ["kaladash_signet"]) };

    /// <summary>The epilogue. The Pact pays out a phial whose flavour has promised
    /// all along that a single drop would wake Mirka — this carries it back up.
    /// Its start flag is <c>cave_deep</c> itself, so the journal turns toward home
    /// the moment the deepest depth is reached, without anyone having to say so;
    /// <c>mirka_woken</c> is set by World.TryWakeMirka at her bedside.</summary>
    public static readonly QuestDef PanaceaHome = new(
        Id: "panacea_home",
        Title: "The Way Back Up",
        Kind: QuestKind.Side,
        Summary: "The Cave gave up its panacea at the last. Carry it back up out of the " +
                 "dark, to the house on the hill where Mirka sleeps — and wake her.",
        Steps: new QuestStep[]
        {
            new("Bring the Panacea to Mirka's bedside", "mirka_woken"),
        },
        StartFlag: "cave_deep")
    { Reward = new(Xp: 500) };

    public static readonly IReadOnlyList<QuestDef> All = new[]
    {
        DelversPact, WordForTheCleric, SeekCerno, PanaceaHome,
    };

    public static QuestDef? Find(string id) => All.FirstOrDefault(q => q.Id == id);

    /// <summary>The quests begun so far, main line first — what the journal lists.</summary>
    public static IReadOnlyList<QuestDef> Started(IReadOnlyCollection<string> flags) =>
        All.Where(q => q.IsStarted(flags))
           .OrderBy(q => q.Kind == QuestKind.Main ? 0 : 1)
           .ToList();

    /// <summary>The line the HUD shows: the current step of the main quest — and
    /// once the Pact is done, the epilogue's, so the road home is on screen the
    /// same way the road down was. Empty only when the whole tale is told.</summary>
    public static string HudObjective(IReadOnlyCollection<string> flags) =>
        DelversPact.Current(flags)?.Objective
        ?? (PanaceaHome.Status(flags) == QuestStatus.Active ? PanaceaHome.Current(flags)?.Objective : null)
        ?? "";

    /// <summary>The quests that this one newly-set flag just carried over the finish
    /// line — complete now, and not complete an instant ago without it. The claim
    /// site calls this the moment a flag is added, so a quest is toasted exactly
    /// once, whichever flag (a descent, a discovery, a line of dialogue) closed it.</summary>
    public static IEnumerable<QuestDef> CompletedBy(string flag, IReadOnlyCollection<string> flags)
    {
        var before = flags.Where(f => f != flag).ToHashSet();
        // Started-before matters: a side quest's done-flag can be set by the world
        // (WordForTheCleric ends on `cave_entered`, which every delver trips) without
        // the quest ever having been TAKEN. Only toast one the player actually holds.
        return All.Where(q => q.IsComplete(flags) && !q.IsComplete(before) && q.IsStarted(before));
    }
}
