namespace AlfarQuest.Client.Models;

/// <summary>One hero's tally of what they have done — kept per hero, never shared,
/// so a veteran's record and a fresh recruit's are their own.
///
/// A flat bag of counters rather than a class per statistic: they are all "a
/// number that only goes up", the engine feeds them by name through
/// <see cref="Game.StatBridge"/>, and the save stores them as a block. Adding a
/// statistic is a field here and a case in <see cref="Add"/>.</summary>
public sealed class HeroStats
{
    public long EnemiesDefeated { get; set; }
    public long BossesDefeated { get; set; }
    public long Deaths { get; set; }
    public long DamageDealt { get; set; }
    public long DamageTaken { get; set; }
    public long TreasuresOpened { get; set; }
    public long ItemsCollected { get; set; }
    public long GoldEarned { get; set; }
    public long DistanceWalked { get; set; }
    public long PlaySeconds { get; set; }

    /// <summary>Names used by the engine's <see cref="Game.StatBridge"/>. Kept as
    /// constants so a typo is a compile error at the one call site, not a silently
    /// dropped statistic.</summary>
    public static class Kind
    {
        public const string EnemiesDefeated = "kills";
        public const string BossesDefeated = "bosses";
        public const string Deaths = "deaths";
        public const string DamageDealt = "dealt";
        public const string DamageTaken = "taken";
        public const string TreasuresOpened = "treasures";
        public const string ItemsCollected = "items";
        public const string GoldEarned = "gold";
        public const string DistanceWalked = "distance";
    }

    /// <summary>Adds to a statistic by name. An unknown name is ignored rather
    /// than thrown — a statistic the engine reports that this version does not
    /// track yet should cost nothing.</summary>
    public void Add(string kind, long amount)
    {
        switch (kind)
        {
            case Kind.EnemiesDefeated: EnemiesDefeated += amount; break;
            case Kind.BossesDefeated: BossesDefeated += amount; break;
            case Kind.Deaths: Deaths += amount; break;
            case Kind.DamageDealt: DamageDealt += amount; break;
            case Kind.DamageTaken: DamageTaken += amount; break;
            case Kind.TreasuresOpened: TreasuresOpened += amount; break;
            case Kind.ItemsCollected: ItemsCollected += amount; break;
            case Kind.GoldEarned: GoldEarned += amount; break;
            case Kind.DistanceWalked: DistanceWalked += amount; break;
        }
    }

    /// <summary>Replaces every counter from another block. Used when a save is
    /// applied — restoring a record has to overwrite, not add to, what a fresh
    /// sheet started at zero with.</summary>
    public void CopyFrom(HeroStats o)
    {
        EnemiesDefeated = o.EnemiesDefeated;
        BossesDefeated = o.BossesDefeated;
        Deaths = o.Deaths;
        DamageDealt = o.DamageDealt;
        DamageTaken = o.DamageTaken;
        TreasuresOpened = o.TreasuresOpened;
        ItemsCollected = o.ItemsCollected;
        GoldEarned = o.GoldEarned;
        DistanceWalked = o.DistanceWalked;
        PlaySeconds = o.PlaySeconds;
    }

    /// <summary>The lines the character sheet shows, in order — label and value,
    /// so the panel stays a loop rather than ten hand-written rows.</summary>
    public IEnumerable<(string Label, long Value)> Lines()
    {
        yield return ("Enemies defeated", EnemiesDefeated);
        yield return ("Bosses defeated", BossesDefeated);
        yield return ("Deaths", Deaths);
        yield return ("Damage dealt", DamageDealt);
        yield return ("Damage taken", DamageTaken);
        yield return ("Treasures opened", TreasuresOpened);
        yield return ("Items collected", ItemsCollected);
        yield return ("Gold earned", GoldEarned);
    }
}
