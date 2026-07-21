namespace AlfarQuest.Client.Game;

public enum Biome { Forest, River, Mountain, NearCave, Cave }

/// <summary>What a creature is. Static data shared by every instance, so a new
/// species is one entry in <see cref="CreatureCatalog"/> and nothing else.</summary>
public sealed record CreatureType
{
    public required string Id { get; init; }
    public required string Kind { get; init; }      // sprite key in atlas_chars.json
    public required string Name { get; init; }
    public required Biome Biome { get; init; }

    public float MaxHp { get; init; } = 42;
    public int Damage { get; init; } = 8;
    public float Speed { get; init; } = 54;
    public float Radius { get; init; } = 15;
    public float Scale { get; init; } = 1f;

    /// <summary>How far it wanders from home, and how far it will notice you.
    /// Both in tiles; the spec asks for 3-8 and 5-7 respectively.</summary>
    public float PatrolTiles { get; init; } = 5;
    public float AggroTiles { get; init; } = 6;

    /// <summary>What it leaves behind, as (item, chance).</summary>
    public IReadOnlyList<(string Item, float Chance)> Loot { get; init; } = [];

    /// <summary>Experience for killing one. Lives on the species rather than in
    /// the XP table because every kill is the same *source* — what separates a
    /// bat from a mini-boss is which creature it was.</summary>
    public int Xp { get; init; } = 10;

    /// <summary>Marks the rare, dangerous ones. Their kill pays the mini-boss
    /// rate and announces itself.</summary>
    public bool MiniBoss { get; init; }

    /// <summary>What it is made of. Decides what flies off when it is struck —
    /// a blade landing on crystal should not look like the same blade landing on
    /// a body, and the effect id is derived from this rather than switched on the
    /// species.</summary>
    public string Material { get; init; } = "flesh";

    /// <summary>Whether its blows are magical. Magic is turned aside by Wisdom
    /// rather than by armour, so a party built entirely for physical defence has
    /// something it is genuinely weak to.</summary>
    public bool Magical { get; init; }
}

public static class CreatureCatalog
{
    public static readonly IReadOnlyList<CreatureType> All =
    [
        // --- forest ---
        new() { Id = "slime", Kind = "mobSlime", Name = "Crystal Slime", Biome = Biome.Forest,
                MaxHp = 30, Damage = 6, Speed = 42, Scale = 0.8f, PatrolTiles = 3, AggroTiles = 5,
                Xp = 15,
                Material = "crystal",
                Loot = [("Small Crystal", 0.35f), ("Coins", 0.5f)] },
        new() { Id = "beast", Kind = "mobBeast", Name = "Crystal Beast", Biome = Biome.Forest,
                MaxHp = 58, Damage = 11, Speed = 68, Scale = 0.9f, PatrolTiles = 8, AggroTiles = 7,
                Xp = 25,
                Loot = [("Hide", 0.6f), ("Coins", 0.4f)] },

        // --- river ---
        new() { Id = "swarm", Kind = "mobSwarm", Name = "Crystal Swarm", Biome = Biome.River,
                MaxHp = 22, Damage = 5, Speed = 76, Radius = 12, Scale = 0.7f,
                PatrolTiles = 6, AggroTiles = 6,
                Xp = 8,
                Magical = true,
                Material = "crystal",
                Loot = [("Spider Silk", 0.45f), ("Herbs", 0.3f)] },

        // --- mountain ---
        new() { Id = "bat", Kind = "mobBat", Name = "Crystal Bat", Biome = Biome.Mountain,
                MaxHp = 26, Damage = 7, Speed = 88, Radius = 12, Scale = 0.75f,
                PatrolTiles = 8, AggroTiles = 7,
                Xp = 10,
                Loot = [("Bat Wing", 0.55f), ("Coins", 0.3f)] },
        new() { Id = "walker", Kind = "mobWalker", Name = "Crystal Walker", Biome = Biome.Mountain,
                MaxHp = 74, Damage = 13, Speed = 46, Scale = 0.95f, PatrolTiles = 4, AggroTiles = 6,
                Xp = 30,
                Material = "stone",
                Loot = [("Stone", 0.7f), ("Small Crystal", 0.25f)] },

        // --- the last stretch before the mine ---
        new() { Id = "spider", Kind = "mobSpider", Name = "Crystal Spider", Biome = Biome.NearCave,
                MaxHp = 44, Damage = 12, Speed = 72, Scale = 0.85f, PatrolTiles = 5, AggroTiles = 7,
                Xp = 18,
                Loot = [("Spider Silk", 0.7f), ("Small Crystal", 0.3f)] },
        new() { Id = "worm", Kind = "mobWorm", Name = "Crystal Worm", Biome = Biome.NearCave,
                MaxHp = 96, Damage = 15, Speed = 38, Radius = 18, Scale = 1f,
                PatrolTiles = 3, AggroTiles = 5,
                Xp = 40,
                Material = "stone",
                Loot = [("Stone", 0.5f), ("Small Crystal", 0.5f)] },

        // --- the cave keeps its husks ---
        new() { Id = "husk", Kind = "husk", Name = "Gem-riddled Husk", Biome = Biome.Cave,
                MaxHp = 42, Damage = 8, Speed = 54, PatrolTiles = 0, AggroTiles = 999,
                Xp = 12,
                Magical = true,
                Material = "crystal",
                Loot = [("Small Crystal", 0.4f)] },
    ];

    public static CreatureType Of(string id) => All.First(c => c.Id == id);
    public static IEnumerable<CreatureType> In(Biome b) => All.Where(c => c.Biome == b);
}
