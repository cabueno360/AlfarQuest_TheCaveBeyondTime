using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

public enum Biome { Forest, River, Mountain, NearCave, Cave }

/// <summary>How a creature behaves when it has not seen you.
///
/// The idle half of its character — what it does with an empty stretch of map.
/// The reactive half (does it flee, does it defend its neighbours) is separate,
/// because a sleeper and a wanderer can both be skittish.</summary>
public enum Demeanor
{
    /// <summary>Loiters near home, drifting to new spots. The default.</summary>
    Wanderer,
    /// <summary>Sits still until something comes near, then wakes and gives
    /// chase. Worms half-buried, things that were never really gone.</summary>
    Sleeper,
    /// <summary>Holds its ground. Barely wanders, chases on a short leash and
    /// hurries back — it is guarding the spot, not patrolling.</summary>
    Sentry,
    /// <summary>Still and easy to miss until you are almost on it, then it
    /// pounces. Spiders in the dark, a bat that was part of the ceiling.</summary>
    Ambusher,
}

/// <summary>A creature's one simple trick beyond walking up and biting. Bosses
/// will get real kits later; these are readable, single-purpose, and enough to
/// stop every fight being the same fight.</summary>
public enum MonsterAbility
{
    None,
    /// <summary>A slow venom glob lobbed at range. The spider.</summary>
    PoisonSpit,
    /// <summary>A sudden lunge that closes the gap. The bat.</summary>
    Dash,
    /// <summary>A shard of crystal flung straight. The slime.</summary>
    CrystalBolt,
    /// <summary>A telegraphed ground-pound that hits everything close. The stone
    /// things — walker and worm.</summary>
    Smash,
    /// <summary>A weak, wandering bolt of raw magic. The swarm.</summary>
    MagicMissile,

    // --- boss kit: bigger, and used in rotation, not one trick ---
    /// <summary>A fanned spray of crystal shards. The Guardian's opener.</summary>
    CrystalVolley,
    /// <summary>A burst of crystal light that fills the chamber's heart — a wide
    /// ring, heavy, and it shoves. Get clear of it.</summary>
    ShardNova,
    /// <summary>Tears fresh husks out of the walls to swarm the party.</summary>
    SummonHusks,

    // --- the deeper keepers' tricks: each depth's boss fights differently ---
    /// <summary>Three slow shards of ice in a fan; whatever they touch is left
    /// dragging. The Weeping Warden's answer to being kited.</summary>
    FrostVolley,
    /// <summary>A cold rain called down ON EACH HERO — a telegraphed ring under
    /// your own feet, so the answer is to keep moving, not to keep away.</summary>
    WeepingRain,
    /// <summary>Two shockwave rings, inner then outer, so backing off once is not
    /// enough — the Vault Keeper's quake reaches further than it looks.</summary>
    QuakeRings,
    /// <summary>Splits off shimmering copies of itself, a fraction as strong —
    /// pressure and confusion at once. The Mirrored One's whole argument.</summary>
    MirrorSplit,
    /// <summary>Thickens time around the whole party — every hero slowed at once.
    /// The true Guardian's power, in the one place time runs wrong.</summary>
    TimeSlow,
}

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

    /// <summary>Its idle character. Decides where it starts — a sleeper begins
    /// asleep — and how it moves when it has not seen the party.</summary>
    public Demeanor Demeanor { get; init; } = Demeanor.Wanderer;

    /// <summary>Below this fraction of its health it turns and runs. Zero means it
    /// never breaks — most things fight to the end; the small and the cowardly do
    /// not.</summary>
    public float FleeBelow { get; init; } = 0f;

    /// <summary>Rushes to help when a neighbour is struck, and rouses from further
    /// away. Beasts hunt in support of one another; a bat colony turns as one.</summary>
    public bool Protective { get; init; }

    /// <summary>How far its cry carries when it is hit — the radius over which it
    /// wakes nearby creatures. Kept small on purpose so aggro never chains across
    /// the whole map.</summary>
    public float AlertTiles { get; init; } = 3.5f;

    /// <summary>Its one trick, and the reach and pace of it. Range in tiles.</summary>
    public MonsterAbility Ability { get; init; } = MonsterAbility.None;
    public float AbilityRangeTiles { get; init; } = 4.5f;
    public float AbilityCooldown { get; init; } = 3f;

    /// <summary>What it leaves behind, as (item, chance).</summary>
    public IReadOnlyList<(string Item, float Chance)> Loot { get; init; } = [];

    /// <summary>Experience for killing one. Lives on the species rather than in
    /// the XP table because every kill is the same *source* — what separates a
    /// bat from a mini-boss is which creature it was.</summary>
    public int Xp { get; init; } = 10;

    /// <summary>Marks the rare, dangerous ones. Their kill pays the mini-boss
    /// rate and announces itself.</summary>
    public bool MiniBoss { get; init; }

    /// <summary>The chamber-holding kind: a health bar of its own, a KIT it rotates
    /// through instead of one trick, and an enrage. A step above MiniBoss — this is
    /// the fight a room is built around, not just a dangerous variant.</summary>
    public bool Boss { get; init; }

    /// <summary>A boss's kit — the abilities it cycles through, in order. Empty for
    /// everything else, which falls back to its single <see cref="Ability"/>.</summary>
    public IReadOnlyList<MonsterAbility> Kit { get; init; } = [];

    /// <summary>Below this fraction of its health it ENRAGES: faster, and its
    /// cooldowns shorten. Zero = it never does.</summary>
    public float EnrageBelow { get; init; } = 0f;

    /// <summary>Below this fraction of its health a boss TURNS: <see cref="Kit2"/>
    /// replaces <see cref="Kit"/>, so the second half of the fight is a different
    /// fight. Zero = one kit all the way down.</summary>
    public float PhaseBelow { get; init; } = 0f;
    public IReadOnlyList<MonsterAbility> Kit2 { get; init; } = [];

    /// <summary>What it is made of. Decides what flies off when it is struck —
    /// a blade landing on crystal should not look like the same blade landing on
    /// a body, and the effect id is derived from this rather than switched on the
    /// species.</summary>
    public string Material { get; init; } = "flesh";

    /// <summary>Whether its blows are magical. Magic is turned aside by Wisdom
    /// rather than by armour, so a party built entirely for physical defence has
    /// something it is genuinely weak to.</summary>
    public bool Magical { get; init; }

    /// <summary>How readily it slips a blow. The quick, small things — bats, the
    /// swarm — are hard to land on; the slow heavy ones are not. Read by the
    /// attacker's accuracy roll.</summary>
    public float Evasion { get; init; }

    /// <summary>What it shrugs off, as (damage type, fraction removed). A negative
    /// fraction is a weakness — it takes extra. Crystal turns a point aside but
    /// shatters under a hammer; stone the reverse. Absent types read as zero, so
    /// a new school of magic simply lands in full until someone tunes it.</summary>
    public IReadOnlyDictionary<DamageType, float> Resist { get; init; } =
        new Dictionary<DamageType, float>();

    public float Resistance(DamageType type) => Math.Clamp(Resist.GetValueOrDefault(type), -1f, 0.9f);
}

public static class CreatureCatalog
{
    // Thematic resistance tables, shared by the species made of the same stuff.
    // Crystal turns a point aside but rings apart under a blunt blow; stone is the
    // opposite, hard to cut or pierce but crushed all the same.
    static readonly IReadOnlyDictionary<DamageType, float> Crystal = new Dictionary<DamageType, float>
    {
        [DamageType.Piercing] = 0.35f, [DamageType.Ice] = 0.3f, [DamageType.Blunt] = -0.3f,
    };
    static readonly IReadOnlyDictionary<DamageType, float> Stone = new Dictionary<DamageType, float>
    {
        [DamageType.Slashing] = 0.35f, [DamageType.Piercing] = 0.25f, [DamageType.Blunt] = -0.35f,
    };
    // Water shrugs at ice and closes around a blade, but fire drinks it away.
    static readonly IReadOnlyDictionary<DamageType, float> Water = new Dictionary<DamageType, float>
    {
        [DamageType.Ice] = 0.5f, [DamageType.Slashing] = 0.15f, [DamageType.Fire] = -0.35f,
    };

    public static readonly IReadOnlyList<CreatureType> All =
    [
        // --- forest: the gentle introduction ---
        // Slimes drift and are easily got; cornered, they pop a shard at you and
        // then bolt. The first thing most players kill.
        new() { Id = "slime", Kind = "mobSlime", Name = "Crystal Slime", Biome = Biome.Forest,
                MaxHp = 30, Damage = 6, Speed = 42, Scale = 0.8f, PatrolTiles = 3, AggroTiles = 5,
                Xp = 15,
                Demeanor = Demeanor.Wanderer, FleeBelow = 0.25f,
                Ability = MonsterAbility.CrystalBolt, AbilityRangeTiles = 4.5f, AbilityCooldown = 3.5f,
                Material = "crystal", Evasion = 0.06f,
                Resist = Crystal,
                Loot = [("Small Crystal", 0.35f), ("Coins", 0.5f)] },
        // Beasts run in support of one another — strike one and the pack turns.
        new() { Id = "beast", Kind = "mobBeast", Name = "Crystal Beast", Biome = Biome.Forest,
                MaxHp = 58, Damage = 11, Speed = 68, Scale = 0.9f, PatrolTiles = 8, AggroTiles = 7,
                Xp = 25,
                Demeanor = Demeanor.Wanderer, Protective = true, AlertTiles = 4.5f,
                Evasion = 0.08f,
                Loot = [("Hide", 0.6f), ("Coins", 0.4f)] },

        // --- river: quick and fragile ---
        // A colony that scatters when hurt and lobs raw magic while it does.
        new() { Id = "swarm", Kind = "mobSwarm", Name = "Crystal Swarm", Biome = Biome.River,
                MaxHp = 22, Damage = 5, Speed = 76, Radius = 12, Scale = 0.7f,
                PatrolTiles = 6, AggroTiles = 6,
                Xp = 8,
                Demeanor = Demeanor.Wanderer, FleeBelow = 0.4f, Protective = true, AlertTiles = 4f,
                Ability = MonsterAbility.MagicMissile, AbilityRangeTiles = 5f, AbilityCooldown = 2.8f,
                Magical = true,
                Material = "crystal", Evasion = 0.22f,
                Resist = Crystal,
                Loot = [("Spider Silk", 0.45f), ("Herbs", 0.3f)] },

        // --- mountain ---
        // Bats hang unnoticed until you are under them, then dive. One roused, the
        // roost roused.
        new() { Id = "bat", Kind = "mobBat", Name = "Crystal Bat", Biome = Biome.Mountain,
                MaxHp = 26, Damage = 7, Speed = 88, Radius = 12, Scale = 0.75f,
                PatrolTiles = 8, AggroTiles = 5,
                Xp = 10,
                Demeanor = Demeanor.Ambusher, Protective = true, AlertTiles = 5f,
                Ability = MonsterAbility.Dash, AbilityRangeTiles = 6f, AbilityCooldown = 2.5f,
                Evasion = 0.20f,
                Loot = [("Bat Wing", 0.55f), ("Coins", 0.3f)] },
        // Walkers stand over the crystal seams and do not stray — a sentry that
        // pounds the ground when you close.
        new() { Id = "walker", Kind = "mobWalker", Name = "Crystal Walker", Biome = Biome.Mountain,
                MaxHp = 74, Damage = 13, Speed = 46, Scale = 0.95f, PatrolTiles = 2, AggroTiles = 6,
                Xp = 30,
                Demeanor = Demeanor.Sentry, AlertTiles = 3f,
                Ability = MonsterAbility.Smash, AbilityRangeTiles = 2.4f, AbilityCooldown = 3.5f,
                Material = "stone", Resist = Stone,
                Loot = [("Stone", 0.7f), ("Small Crystal", 0.25f)] },

        // --- the last stretch before the mine ---
        // Spiders sit still in the dark and spit venom the moment you are in reach.
        new() { Id = "spider", Kind = "mobSpider", Name = "Crystal Spider", Biome = Biome.NearCave,
                MaxHp = 44, Damage = 12, Speed = 72, Scale = 0.85f, PatrolTiles = 5, AggroTiles = 6,
                Xp = 18,
                Demeanor = Demeanor.Ambusher, AlertTiles = 4f,
                Ability = MonsterAbility.PoisonSpit, AbilityRangeTiles = 5.5f, AbilityCooldown = 3f,
                Evasion = 0.12f,
                Loot = [("Spider Silk", 0.7f), ("Small Crystal", 0.3f)] },
        // Worms lie buried until the ground shakes above them, then heave up and
        // smash. Slow, heavy, and worth the most out here.
        new() { Id = "worm", Kind = "mobWorm", Name = "Crystal Worm", Biome = Biome.NearCave,
                MaxHp = 96, Damage = 15, Speed = 38, Radius = 18, Scale = 1f,
                PatrolTiles = 2, AggroTiles = 4,
                Xp = 40,
                Demeanor = Demeanor.Sleeper, AlertTiles = 3f,
                Ability = MonsterAbility.Smash, AbilityRangeTiles = 2.6f, AbilityCooldown = 3f,
                Material = "stone", Resist = Stone,
                Loot = [("Stone", 0.5f), ("Small Crystal", 0.5f)] },

        // --- the cave keeps its husks ---
        // Relentless and single-minded: they see the whole chamber and simply come.
        // No trick, no fear — the baseline the outdoor creatures are a relief from.
        new() { Id = "husk", Kind = "husk", Name = "Gem-riddled Husk", Biome = Biome.Cave,
                MaxHp = 42, Damage = 8, Speed = 54, PatrolTiles = 0, AggroTiles = 999,
                Xp = 12,
                Demeanor = Demeanor.Wanderer,
                Magical = true,
                Material = "crystal", Resist = Crystal,
                Loot = [("Small Crystal", 0.4f)] },

        // --- the five keepers of the descent: one boss per named depth, each with
        // its own kit and a second kit it turns to below PhaseBelow, so the fight
        // a room is built around CHANGES as the party goes deeper — deeper is
        // different, not merely longer. Stats are tuned to each depth's
        // recommended level (World.RecommendedLevel); SpawnBoss picks by depth.

        // Depth 1, the Crystal Cistern — the teaching boss. The kit the old
        // Guardian always had: volley, summon, nova, and a late enrage.
        new() { Id = "cistern_warden", Kind = "bossCistern", Name = "Warden of the Cistern",
                Biome = Biome.Cave,
                MaxHp = 640, Damage = 15, Speed = 40, Radius = 26, Scale = 1.55f,
                PatrolTiles = 0, AggroTiles = 999, AlertTiles = 0,
                Demeanor = Demeanor.Wanderer,
                Magical = true, Material = "crystal", Resist = Crystal,
                Boss = true,
                Kit = [MonsterAbility.CrystalVolley, MonsterAbility.SummonHusks, MonsterAbility.ShardNova],
                EnrageBelow = 0.3f, AbilityRangeTiles = 9f, AbilityCooldown = 2.8f,
                Xp = 240, Loot = [("Small Crystal", 1f), ("Coins", 0.9f)] },

        // Depth 2, the Weeping Gallery — water and cold. Punishes standing still:
        // the rain lands where you ARE, and the frost leaves you dragging.
        new() { Id = "weeping_warden", Kind = "bossWeeping", Name = "The Weeping Warden",
                Biome = Biome.Cave,
                MaxHp = 1050, Damage = 19, Speed = 44, Radius = 26, Scale = 1.7f,
                PatrolTiles = 0, AggroTiles = 999, AlertTiles = 0,
                Demeanor = Demeanor.Wanderer,
                Magical = true, Material = "crystal", Resist = Water,
                Boss = true,
                Kit  = [MonsterAbility.FrostVolley, MonsterAbility.WeepingRain, MonsterAbility.SummonHusks],
                PhaseBelow = 0.45f,
                Kit2 = [MonsterAbility.FrostVolley, MonsterAbility.WeepingRain, MonsterAbility.ShardNova],
                EnrageBelow = 0.25f, AbilityRangeTiles = 9f, AbilityCooldown = 2.6f,
                Xp = 360, Loot = [("Small Crystal", 1f), ("Coins", 1f)] },

        // Depth 3, the Sunken Vault — armour and earth. Slow, physical and heavy;
        // its quake reaches further than it looks, and blades barely mark it.
        new() { Id = "vault_keeper", Kind = "mobKnight", Name = "The Vault Keeper",
                Biome = Biome.Cave,
                MaxHp = 1500, Damage = 24, Speed = 42, Radius = 27, Scale = 2.2f,
                PatrolTiles = 0, AggroTiles = 999, AlertTiles = 0,
                Demeanor = Demeanor.Wanderer,
                Material = "stone", Resist = Stone,
                Boss = true,
                Kit  = [MonsterAbility.QuakeRings, MonsterAbility.Smash, MonsterAbility.SummonHusks],
                PhaseBelow = 0.5f,
                Kit2 = [MonsterAbility.QuakeRings, MonsterAbility.CrystalVolley, MonsterAbility.Smash],
                EnrageBelow = 0.25f, AbilityRangeTiles = 8f, AbilityCooldown = 3.0f,
                Xp = 500, Loot = [("Stone", 1f), ("Small Crystal", 1f), ("Coins", 1f)] },

        // Depth 4, the Mirror Halls — the fight where you cannot trust your eyes.
        // It splits into shimmering copies; kill the one that bleeds crystal.
        new() { Id = "mirrored_one", Kind = "mobMage", Name = "The Mirrored One",
                Biome = Biome.Cave,
                MaxHp = 1850, Damage = 26, Speed = 48, Radius = 25, Scale = 2.0f,
                PatrolTiles = 0, AggroTiles = 999, AlertTiles = 0,
                Demeanor = Demeanor.Wanderer,
                Magical = true, Material = "crystal", Resist = Crystal, Evasion = 0.1f,
                Boss = true,
                Kit  = [MonsterAbility.CrystalVolley, MonsterAbility.MirrorSplit, MonsterAbility.ShardNova],
                PhaseBelow = 0.5f,
                Kit2 = [MonsterAbility.MirrorSplit, MonsterAbility.FrostVolley, MonsterAbility.ShardNova],
                EnrageBelow = 0.3f, AbilityRangeTiles = 10f, AbilityCooldown = 2.4f,
                Xp = 650, Loot = [("Small Crystal", 1f), ("Coins", 1f)] },

        // Depth 5, the Cave Beyond Time — the true Guardian of the Crystal Heart,
        // in the one place time runs wrong. It thickens time around the whole
        // party, and below half health it shatters and reforms into a second kit.
        new() { Id = "guardian", Kind = "bossGiant", Name = "Guardian of the Crystal Heart",
                Biome = Biome.Cave,
                MaxHp = 2700, Damage = 30, Speed = 44, Radius = 30, Scale = 1.5f,
                PatrolTiles = 0, AggroTiles = 999, AlertTiles = 0,
                Demeanor = Demeanor.Wanderer,
                Magical = true, Material = "crystal", Resist = Crystal,
                Boss = true,
                Kit  = [MonsterAbility.CrystalVolley, MonsterAbility.SummonHusks, MonsterAbility.TimeSlow, MonsterAbility.ShardNova],
                PhaseBelow = 0.5f,
                Kit2 = [MonsterAbility.FrostVolley, MonsterAbility.MirrorSplit, MonsterAbility.TimeSlow, MonsterAbility.ShardNova],
                EnrageBelow = 0.3f, AbilityRangeTiles = 10f, AbilityCooldown = 2.3f,
                Xp = 900, Loot = [("Small Crystal", 1f), ("Coins", 1f)] },
    ];

    public static CreatureType Of(string id) => All.First(c => c.Id == id);
    public static IEnumerable<CreatureType> In(Biome b) => All.Where(c => c.Biome == b);
}
