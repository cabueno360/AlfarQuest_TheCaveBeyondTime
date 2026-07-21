namespace AlfarQuest.Client.Models;

/// <summary>The primary attributes. A record so a sheet can be recalculated by
/// producing a new value rather than mutating a shared one — these are read far
/// more often than they change.
///
/// Adding one is three edits that the compiler walks you through: a property
/// here, a case in the indexer and in <see cref="With"/>, and a row in
/// <see cref="AttributeInfo.All"/>. Every panel is driven off that list, so no
/// UI file changes. What the compiler cannot check is the part that matters —
/// an attribute nothing derives from is decorative, so a new one needs a term in
/// StatCalculator too.</summary>
public record Attributes
{
    public int Strength { get; init; }
    public int Dexterity { get; init; }
    public int Agility { get; init; }
    public int Vitality { get; init; }
    public int Intelligence { get; init; }
    public int Wisdom { get; init; }
    public int Defense { get; init; }
    public int Luck { get; init; }

    public int this[AttributeKind k] => k switch
    {
        AttributeKind.Strength => Strength,
        AttributeKind.Dexterity => Dexterity,
        AttributeKind.Agility => Agility,
        AttributeKind.Vitality => Vitality,
        AttributeKind.Intelligence => Intelligence,
        AttributeKind.Wisdom => Wisdom,
        AttributeKind.Defense => Defense,
        AttributeKind.Luck => Luck,
        _ => 0,
    };

    public Attributes With(AttributeKind k, int delta) => k switch
    {
        AttributeKind.Strength => this with { Strength = Strength + delta },
        AttributeKind.Dexterity => this with { Dexterity = Dexterity + delta },
        AttributeKind.Agility => this with { Agility = Agility + delta },
        AttributeKind.Vitality => this with { Vitality = Vitality + delta },
        AttributeKind.Intelligence => this with { Intelligence = Intelligence + delta },
        AttributeKind.Wisdom => this with { Wisdom = Wisdom + delta },
        AttributeKind.Defense => this with { Defense = Defense + delta },
        AttributeKind.Luck => this with { Luck = Luck + delta },
        _ => this,
    };

    public static Attributes operator +(Attributes a, Attributes b) => new()
    {
        Strength = a.Strength + b.Strength,
        Dexterity = a.Dexterity + b.Dexterity,
        Agility = a.Agility + b.Agility,
        Vitality = a.Vitality + b.Vitality,
        Intelligence = a.Intelligence + b.Intelligence,
        Wisdom = a.Wisdom + b.Wisdom,
        Defense = a.Defense + b.Defense,
        Luck = a.Luck + b.Luck,
    };
}

public enum AttributeKind
{
    Strength, Dexterity, Agility, Vitality, Intelligence, Wisdom, Defense, Luck,
}

/// <summary>Presentation metadata, kept beside the enum so adding an attribute
/// is one entry here and one case above — no UI file needs editing.</summary>
public sealed record AttributeInfo(AttributeKind Kind, string Name, string Abbr, string Description)
{
    public static readonly IReadOnlyList<AttributeInfo> All =
    [
        new(AttributeKind.Strength,     "Strength",     "STR", "Raises physical damage and what you can swing."),
        new(AttributeKind.Dexterity,    "Dexterity",    "DEX", "Sharpens accuracy, critical strikes and how fast you strike."),
        new(AttributeKind.Agility,      "Agility",      "AGI", "Improves movement and the chance to slip a blow."),
        new(AttributeKind.Vitality,     "Vitality",     "VIT", "Deepens health and how quickly it returns."),
        new(AttributeKind.Intelligence, "Intelligence", "INT", "Feeds magic damage, spell power and the mana to spend."),
        new(AttributeKind.Wisdom,       "Wisdom",       "WIS", "Refills mana faster and turns aside magic."),
        new(AttributeKind.Defense,      "Defense",      "DEF", "Blunts physical blows and improves your guard."),
        new(AttributeKind.Luck,         "Luck",         "LCK", "Nudges what the dark gives up, and how rare it is."),
    ];
}
