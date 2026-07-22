namespace AlfarQuest.Client.Models;

/// <summary>How an active skill plays out. Three shapes cover everything the
/// first classes need: a thing that flies, a burst around the caster, and a mend.
/// The engine switches on this rather than on a skill id, so a new skill is a row
/// in the catalogue and never a branch in combat.</summary>
public enum SkillShape { Projectile, Nova, Heal }

/// <summary>One active skill — the things on the hotbar, cast by 1–4. Unlike the
/// passive <see cref="Skill"/> book, these are used, cost mana, and run a
/// cooldown. Each class has its own set; higher slots unlock as the hero levels,
/// which is what makes the hotbar fill in as they grow.</summary>
public sealed record ActiveSkill
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string HeroClass { get; init; }

    /// <summary>Hotbar position, 1–4, and the key that casts it.</summary>
    public required int Slot { get; init; }

    public string Icon { get; init; } = "✦";
    public string Description { get; init; } = "";

    public SkillShape Shape { get; init; } = SkillShape.Projectile;
    public float Cooldown { get; init; } = 3f;
    public int ManaCost { get; init; } = 10;

    /// <summary>Hero level this becomes usable at. Below it the slot shows on the
    /// hotbar locked — a promise of what is coming, not an empty square.</summary>
    public int UnlockLevel { get; init; } = 1;

    /// <summary>Base damage before the hero's attributes and ability power scale
    /// it. Zero for a pure heal.</summary>
    public int Damage { get; init; }
    public DamageType DamageType { get; init; } = DamageType.Fire;
    public string Colour { get; init; } = "#d8b45a";

    // --- projectile ---
    public float Speed { get; init; } = 560f;
    /// <summary>How many bolts leave at once — one for an aimed shot, several in a
    /// spread for a multi-shot.</summary>
    public int Count { get; init; } = 1;
    public float Spread { get; init; } = 0.28f;
    /// <summary>Whether a single aimed bolt can roll a critical. Off for spreads
    /// and novas, which are already the big moment.</summary>
    public bool CanCrit { get; init; } = true;

    // --- nova ---
    public float Radius { get; init; } = 170f;

    // --- heal (self and party), and over-time / control riders ---
    public int Heal { get; init; }
    /// <summary>An effect the skill leaves — venom on a poison arrow, a hold on a
    /// shield bash. Applied to whatever a bolt hits, or to everything a nova
    /// catches.</summary>
    public StatusEffectKind? OnHit { get; init; }

    public int ManaCostFor => ManaCost;

    public static readonly IReadOnlyList<ActiveSkill> All =
    [
        // ---- Mage: reach and elements -------------------------------------
        new() { Id = "mage_firebolt", Name = "Firebolt", HeroClass = "Mage", Slot = 1, Icon = "🔥",
                Description = "A bolt of the caged fire, flung the length of the hall.",
                Shape = SkillShape.Projectile, DamageType = DamageType.Fire, Colour = "#ff8a3c",
                Cooldown = 2.2f, ManaCost = 9, Damage = 18, Speed = 580 },
        new() { Id = "mage_frost", Name = "Frost Shard", HeroClass = "Mage", Slot = 2, Icon = "❄",
                Description = "A splinter of ice that slows what it strikes.",
                Shape = SkillShape.Projectile, DamageType = DamageType.Ice, Colour = "#bfe9ff",
                Cooldown = 3f, ManaCost = 12, Damage = 14, Speed = 520, OnHit = StatusEffectKind.Slow },
        new() { Id = "mage_missiles", Name = "Arcane Missiles", HeroClass = "Mage", Slot = 3, Icon = "✦",
                Description = "Three motes of raw magic, loosed in a fan.",
                Shape = SkillShape.Projectile, DamageType = DamageType.Lightning, Colour = "#c98fff",
                Cooldown = 5f, ManaCost = 18, Damage = 9, Speed = 620, Count = 3, CanCrit = false,
                UnlockLevel = 2 },
        new() { Id = "mage_cagedfire", Name = "Caged Fire", HeroClass = "Mage", Slot = 4, Icon = "☄",
                Description = "Lets the demon out for a moment — a ring of hellfire.",
                Shape = SkillShape.Nova, DamageType = DamageType.Fire, Colour = "#ff8a4c",
                Cooldown = 9f, ManaCost = 40, Damage = 60, Radius = 240, CanCrit = false,
                UnlockLevel = 3 },

        // ---- Cleric: light and the mace -----------------------------------
        new() { Id = "cleric_smite", Name = "Smite", HeroClass = "Cleric", Slot = 1, Icon = "☀",
                Description = "A lance of light, thrown straight.",
                Shape = SkillShape.Projectile, DamageType = DamageType.Holy, Colour = "#f0d99a",
                Cooldown = 2.4f, ManaCost = 8, Damage = 16, Speed = 540 },
        new() { Id = "cleric_mend", Name = "Mend", HeroClass = "Cleric", Slot = 2, Icon = "✚",
                Description = "Knits the whole party back together.",
                Shape = SkillShape.Heal, Colour = "#7fd694",
                Cooldown = 8f, ManaCost = 22, Heal = 30 },
        new() { Id = "cleric_bash", Name = "Shield Bash", HeroClass = "Cleric", Slot = 3, Icon = "🛡",
                Description = "A short, heavy blow that leaves what it hits reeling.",
                Shape = SkillShape.Nova, DamageType = DamageType.Blunt, Colour = "#cfd6ff",
                Cooldown = 6f, ManaCost = 14, Damage = 14, Radius = 92, CanCrit = false,
                OnHit = StatusEffectKind.Stun, UnlockLevel = 2 },
        new() { Id = "cleric_nova", Name = "Nova of the Fractured", HeroClass = "Cleric", Slot = 4, Icon = "✷",
                Description = "A burst that harms what stands in it and heals the party.",
                Shape = SkillShape.Nova, DamageType = DamageType.Holy, Colour = "#f0d99a",
                Cooldown = 9f, ManaCost = 28, Damage = 34, Radius = 170, Heal = 25, CanCrit = false,
                UnlockLevel = 3 },

        // ---- Thief: the crossbow and the crystal --------------------------
        new() { Id = "thief_power", Name = "Power Shot", HeroClass = "Thief", Slot = 1, Icon = "🎯",
                Description = "One bolt, put through the middle of something.",
                Shape = SkillShape.Projectile, DamageType = DamageType.Piercing, Colour = "#9fe4ff",
                Cooldown = 3f, ManaCost = 10, Damage = 22, Speed = 700 },
        new() { Id = "thief_multi", Name = "Multi Shot", HeroClass = "Thief", Slot = 2, Icon = "🔱",
                Description = "Three bolts in a spread — for a crowd.",
                Shape = SkillShape.Projectile, DamageType = DamageType.Piercing, Colour = "#9fe4ff",
                Cooldown = 4f, ManaCost = 14, Damage = 10, Speed = 640, Count = 3, CanCrit = false },
        new() { Id = "thief_poison", Name = "Poison Arrow", HeroClass = "Thief", Slot = 3, Icon = "☠",
                Description = "A bolt slick with venom that gnaws long after it lands.",
                Shape = SkillShape.Projectile, DamageType = DamageType.Piercing, Colour = "#8fd24a",
                Cooldown = 5f, ManaCost = 12, Damage = 12, Speed = 640, OnHit = StatusEffectKind.Poison,
                UnlockLevel = 2 },
        new() { Id = "thief_shard", Name = "Shardburst", HeroClass = "Thief", Slot = 4, Icon = "💠",
                Description = "The crystal in his arm answers, badly — blades in every direction.",
                Shape = SkillShape.Nova, DamageType = DamageType.Ice, Colour = "#9fe4ff",
                Cooldown = 9f, ManaCost = 26, Damage = 34, Radius = 170, CanCrit = false,
                UnlockLevel = 3 },
    ];

    /// <summary>A class's active skills, in slot order. Falls back to the mage's
    /// so a class added without a set is underpowered, not broken.</summary>
    public static IReadOnlyList<ActiveSkill> For(string heroClass)
    {
        var set = All.Where(s => s.HeroClass == heroClass).OrderBy(s => s.Slot).ToList();
        return set.Count > 0 ? set : [.. All.Where(s => s.HeroClass == "Mage").OrderBy(s => s.Slot)];
    }

    /// <summary>The skill in a class's given hotbar slot, or null.</summary>
    public static ActiveSkill? At(string heroClass, int slot) =>
        For(heroClass).FirstOrDefault(s => s.Slot == slot);
}
