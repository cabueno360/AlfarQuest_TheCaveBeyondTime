namespace AlfarQuest.Client.Models;

/// <summary>Whether a kind of damage is turned aside by armour or by will.
///
/// The split matters because the two are resisted by different stats: physical
/// damage is blunted by armour (Defense), magic by Wisdom. A creature or a hero
/// can be tough against one and soft against the other, which is what makes a
/// party of all-plate afraid of a mage.</summary>
public enum DamageCategory { Physical, Magic }

/// <summary>A kind of damage. The physical trio and the six schools of magic, so
/// a weapon or a spell can say what it deals and a creature can say what it
/// shrugs off. Kept as an enum with a data table beside it — a new school is one
/// row and no combat code changes.</summary>
public enum DamageType
{
    Slashing, Piercing, Blunt,          // physical
    Fire, Ice, Lightning, Nature, Holy, Dark,   // magic
}

/// <summary>Presentation and category for a damage type. Beside the enum so the
/// tooltip, the floating number and the resistance table all read the same
/// source.</summary>
public sealed record DamageTypeInfo(DamageType Type, string Name, DamageCategory Category, string Colour)
{
    public static readonly IReadOnlyList<DamageTypeInfo> All =
    [
        new(DamageType.Slashing,  "Slashing",  DamageCategory.Physical, "#e8e6f2"),
        new(DamageType.Piercing,  "Piercing",  DamageCategory.Physical, "#cfd6ff"),
        new(DamageType.Blunt,     "Blunt",     DamageCategory.Physical, "#d8c9a8"),
        new(DamageType.Fire,      "Fire",      DamageCategory.Magic,    "#ff8a3c"),
        new(DamageType.Ice,       "Ice",       DamageCategory.Magic,    "#bfe9ff"),
        new(DamageType.Lightning, "Lightning", DamageCategory.Magic,    "#ffe66b"),
        new(DamageType.Nature,    "Nature",    DamageCategory.Magic,    "#7fd694"),
        new(DamageType.Holy,      "Holy",      DamageCategory.Magic,    "#fff6d8"),
        new(DamageType.Dark,      "Dark",      DamageCategory.Magic,    "#c98fff"),
    ];

    private static readonly Dictionary<DamageType, DamageTypeInfo> ById = All.ToDictionary(d => d.Type);

    public static DamageTypeInfo Of(DamageType t) => ById[t];

    public bool IsMagic => Category == DamageCategory.Magic;
}
