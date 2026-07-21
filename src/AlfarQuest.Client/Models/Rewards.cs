namespace AlfarQuest.Client.Models;

/// <summary>Everything that can pay experience.
///
/// A closed enum rather than loose strings: adding a way to earn XP should make
/// the compiler point at the table below, so no source can be introduced without
/// someone deciding what it is worth.</summary>
public enum XpSource
{
    Kill,
    TreasureChest,
    SecretArea,
    RareCrystal,
    OreVein,
    Relic,
    AncientTablet,
    RegionDiscovered,
    Puzzle,
    MiniBoss,
    Boss,
    Quest,
}

/// <summary>What each source is worth, and what floats up when it pays.
///
/// One table so the numbers can be balanced together — scattered across the code
/// that awards them, nobody would ever see the shape of the curve. Creature kills
/// are the exception: they carry their own value on the species, because a bat
/// and a mini-boss are both kills.</summary>
public sealed record XpAward(XpSource Source, int Xp, string Label, string Colour)
{
    public static readonly IReadOnlyList<XpAward> All =
    [
        new(XpSource.Kill,             0, "",                  "#cfe8ff"),   // per species
        new(XpSource.TreasureChest,   25, "Treasure!",         "#f0d99a"),
        new(XpSource.SecretArea,      30, "Secret found!",     "#e7ccff"),
        new(XpSource.RareCrystal,     40, "Rare crystal!",     "#9fe4ff"),
        new(XpSource.OreVein,         12, "Ore",               "#cfd6ff"),
        new(XpSource.Relic,           60, "Ancient relic!",    "#ffd7a8"),
        new(XpSource.AncientTablet,   35, "You read on…",      "#b9c7ff"),
        new(XpSource.RegionDiscovered,20, "New ground",        "#a8f0de"),
        new(XpSource.Puzzle,          45, "Solved!",           "#e8c48a"),
        new(XpSource.MiniBoss,       200, "Mini-boss slain!",  "#ffb0b0"),
        new(XpSource.Boss,           600, "BOSS SLAIN!",       "#ff9090"),
        new(XpSource.Quest,           80, "Quest complete",    "#f0d99a"),
    ];

    private static readonly Dictionary<XpSource, XpAward> ByKind =
        All.ToDictionary(a => a.Source);

    public static XpAward For(XpSource source) => ByKind[source];

    public static int Value(XpSource source) => ByKind[source].Xp;
}
