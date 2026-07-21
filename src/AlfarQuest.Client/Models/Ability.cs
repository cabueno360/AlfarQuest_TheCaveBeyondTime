namespace AlfarQuest.Client.Models;

/// <summary>A class's signature ability — the thing K fires.
///
/// Extracted from the combat code, which read `heroClass == "Mage" ? 240 : 170`
/// inline. Two reasons: the Skills tab has to show a cooldown and a cost, and
/// numbers it could only get by copying them would drift the first time either
/// was tuned. Now the engine and the interface read the same row.</summary>
public sealed record Ability
{
    public required string HeroClass { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string Icon { get; init; } = "✦";

    /// <summary>Seconds before it can be used again.</summary>
    public float Cooldown { get; init; } = 6f;

    /// <summary>Base damage inside the radius, before skills scale it.</summary>
    public int Damage { get; init; }

    /// <summary>Reach in world pixels, before skills widen it.</summary>
    public float Radius { get; init; }

    /// <summary>Health restored to every living party member. Zero for the
    /// abilities that only hurt.</summary>
    public int PartyHeal { get; init; }

    public string Colour { get; init; } = "#d8b45a";

    public int ManaCost => ResourceCosts.Ability(HeroClass);

    public static readonly IReadOnlyList<Ability> All =
    [
        new()
        {
            HeroClass = "Mage", Name = "Caged Fire", Icon = "🔥",
            Description = "Lets the demon out for a moment. A ring of hellfire, wider and louder than anything else the party can do — and the reason his mana runs out first.",
            Damage = 60, Radius = 240, Colour = "#ff8a4c",
        },
        new()
        {
            HeroClass = "Cleric", Name = "Nova of the Fractured", Icon = "☀",
            Description = "A burst of light that harms what stands in it and knits the whole party back together. Faith he no longer has, spent on people he still does.",
            Damage = 34, Radius = 170, PartyHeal = 25, Colour = "#f0d99a",
        },
        new()
        {
            HeroClass = "Thief", Name = "Shardburst", Icon = "💠",
            Description = "The crystal in his arm answers, badly. Blades of it leave him in every direction at once.",
            Damage = 34, Radius = 170, Colour = "#9fe4ff",
        },
    ];

    /// <summary>The ability for a class. Falls back to the first rather than
    /// throwing: a class added without one should be underpowered, not fatal.</summary>
    public static Ability For(string heroClass) =>
        All.FirstOrDefault(a => a.HeroClass == heroClass) ?? All[0];
}
