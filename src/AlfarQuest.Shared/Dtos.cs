namespace AlfarQuest.Shared;

// Wire contracts between the client and the API. Records with init-only setters:
// these cross a serialisation boundary and are never mutated after they are
// built, which is exactly the case records exist for. Object-initialiser syntax
// still works, so every existing construction site is unaffected.

/// <summary>A hero from the Alfar Quest lore. Static reference data, seeded into
/// MySQL and served to the hero-select screen.</summary>
public record HeroDto
{
    public string Key { get; init; } = "";          // stable id, e.g. "mage"
    public string Name { get; init; } = "";         // display name (placeholder until named in canon)
    public string Title { get; init; } = "";        // e.g. "Bearer of the Caged Fire"
    public string HeroClass { get; init; } = "";    // Mage / Cleric / Thief
    public string Description { get; init; } = "";
    public int BaseHp { get; init; }
    public bool UnlockedByDefault { get; init; }
}

/// <summary>One player's persisted campaign progress.</summary>
public record SaveGameDto
{
    public int Id { get; init; }
    public string PlayerName { get; init; } = "";
    public string ActiveHeroKey { get; init; } = "mage";
    public string Region { get; init; } = "cave_beyond_time";
    public long PlaytimeSeconds { get; init; }
    public IReadOnlyList<SaveHeroDto> Party { get; init; } = [];

    /// <summary>One-shot rewards already taken: chests opened, seams mined,
    /// regions found. Without these a reload refills the world, and the front
    /// door becomes an infinite source of experience.</summary>
    public IReadOnlyList<string> ClaimedRewards { get; init; } = [];

    /// <summary>What the party is carrying: coin, gathered materials and the
    /// pack. One list rather than three, because they are the same shape — a key
    /// and a count — and <see cref="TallyKind"/> says which is which.</summary>
    public IReadOnlyList<SaveTallyDto> Belongings { get; init; } = [];

    public DateTime UpdatedAt { get; init; }
}

/// <summary>One hero's progression. Everything a player earned and could resent
/// losing: the level, the experience toward the next, the points not yet spent,
/// and — separately — the points already committed.
///
/// Spent points are stored rather than only the totals, because a total is the
/// baseline plus the spending, and baselines get rebalanced. Storing totals
/// would silently pocket a buff or steal a nerf from every existing save.</summary>
public record SaveHeroDto
{
    public string HeroKey { get; init; } = "";
    public int Level { get; init; } = 1;
    public int Xp { get; init; }
    public bool Recruited { get; init; }

    public int AttributePoints { get; init; }
    public int SkillPoints { get; init; }

    public SpentAttributesDto Spent { get; init; } = new();

    public IReadOnlyList<SavedSkillDto> Skills { get; init; } = [];

    /// <summary>Item ids this hero is wearing. Ids, not copies: an item's stats
    /// belong to the game's data, so a rebalance reaches saved gear instead of
    /// being pocketed by it.</summary>
    public IReadOnlyList<string> Equipped { get; init; } = [];
}

/// <summary>A keyed count belonging to a save.
///
/// Currencies, materials and pack items share this shape exactly. Three tables
/// that differed only in name would be three migrations for one idea, so they
/// share one and carry a kind.</summary>
public record SaveTallyDto
{
    public string Kind { get; init; } = "";
    public string Key { get; init; } = "";
    public int Count { get; init; }
}

/// <summary>The values <see cref="SaveTallyDto.Kind"/> may take. Constants rather
/// than an enum because they cross a wire and are stored as text — a renamed enum
/// member would silently orphan every existing row.</summary>
public static class TallyKind
{
    public const string Currency = "currency";
    public const string Material = "material";
    public const string PackItem = "pack";
}

/// <summary>Points the player chose to put somewhere. A flat record rather than a
/// dictionary so the wire format is checked by the compiler, and so a renamed
/// attribute is a build error instead of a silently dropped column.</summary>
public record SpentAttributesDto
{
    public int Strength { get; init; }
    public int Dexterity { get; init; }
    public int Agility { get; init; }
    public int Vitality { get; init; }
    public int Intelligence { get; init; }
    public int Wisdom { get; init; }
    public int Defense { get; init; }
    public int Luck { get; init; }
}

public record SavedSkillDto
{
    public string SkillId { get; init; } = "";
    public int Rank { get; init; }
}
