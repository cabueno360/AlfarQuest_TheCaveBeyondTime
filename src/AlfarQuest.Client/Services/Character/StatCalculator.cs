using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Services.Character;

/// <summary>Turns attributes into the numbers combat actually uses.
///
/// This is the piece that keeps the attribute panel honest: the same function
/// feeds the character window AND the engine, so raising Strength genuinely
/// changes the damage the party deals. A sheet whose "+" button only moved a
/// label would be a mock-up, not a feature.</summary>
public static class StatCalculator
{
    public static IReadOnlyList<DerivedStat> For(Models.Character c)
    {
        var a = c.Total;                 // already includes worn attributes
        var gear = c.Gear.Total();       // flat bonuses that are not attributes
        var sk = c.SkillEffects();       // learned ranks
        var def = c.Def;

        // Reads the same weapon profile the engine rolls from, so the sheet
        // promises the exact range the blade deals — including the weapon's crit
        // and the pace it swings at.
        var weapon = Weapon(c);
        var wname = c.Gear.Weapon?.WeaponInfo?.Name ?? "Bare hands";
        var dtype = DamageTypeInfo.Of(weapon.Damage);
        var attackSpeed = AttackSpeed(def.AttackCooldown, a) * (1f + sk.AttackSpeed) / MathF.Max(0.1f, weapon.SpeedFactor);
        var block = Math.Min(0.9f, Block(a) + sk.BlockChance);
        var regen = HealthRegen(a) + sk.HealthRegen;
        var armour = PhysicalDefense(a) + gear.Armour;
        return
        [
            new("Weapon Damage",    $"{weapon.Min:0}–{weapon.Max:0} {dtype.Name}", $"{wname}, your attributes and skills."),
            new("Magic Damage",     $"{MagicDamage(def.Damage, a) * (1f + sk.AbilityDamage):0}", "Intelligence and skills."),
            new("Physical Defense", $"{armour:0}",                      "Defense and worn armour. Blunts every blow."),
            new("Damage Reduction", $"{DamageReduction(armour) * 100:0.0}%", "What that armour actually removes."),
            new("Magic Resistance", $"{MagicResistance(a) * 100:0.0}%",  "Wisdom turns aside magic."),
            new("Critical Chance",  $"{(CritChance(a) + gear.CritChance + weapon.CritBonus) * 100:0.0}%", "Dexterity, Luck and your weapon."),
            new("Critical Damage",  $"{CritDamage(a) * 100:0}%",         "How much a critical adds."),
            new("Attack Speed",     $"{attackSpeed:0.00}/s",             "Dexterity and skills shorten the cooldown."),
            new("Movement Speed",   $"{MoveSpeed(def.Speed, a):0}",      "Agility carries you further."),
            new("Accuracy",         $"{Accuracy(a):0}",                  "Dexterity steadies the hand."),
            new("Dodge Chance",     $"{Dodge(a) * 100:0.0}%",            "Agility slips the blow."),
            new("Block Chance",     $"{block * 100:0.0}%",               "Defense and Steady Guard."),
            new("Health Regen",     $"{regen:0.0}/s",                    "Vitality and Vigour."),
            new("Mana Regen",       $"{ManaRegen(a):0.0}/s",             "Wisdom refills the well."),
            new("Ability Cost",     $"{ResourceCosts.Ability(def.HeroClass)} mana", "What your ultimate spends."),
            new("Stamina Regen",    $"{StaminaRegen(a):0.0}/s",           "Agility gets your wind back."),
            new("Find Rarity",      $"+{(LootChance(a) + sk.LootChance) * 100:0}%", "Luck, and what the dark gives up."),
        ];
    }

    /// <summary>The equipped weapon's damage range and feel, with the wearer's
    /// attributes and skills already folded in — the engine rolls between Min and
    /// Max and applies crit, so the whole "no fixed damage" rule lives here.
    ///
    /// A bare-handed hero falls back to a small range off their class base, so
    /// losing your weapon weakens you rather than disarming the game.</summary>
    public static WeaponProfile Weapon(Models.Character c)
    {
        var a = c.Total;
        var gear = c.Gear.Total();
        var sk = c.SkillEffects();
        var w = c.Gear.Weapon;

        float baseMin, baseMax, speed, reach, stun, critBonus;
        DamageType type;
        if (w is not null)
        {
            baseMin = w.DamageMin; baseMax = w.DamageMax; type = w.EffectiveDamage;
            var info = w.WeaponInfo!;
            speed = info.SpeedFactor; reach = info.Reach; stun = info.StunChance; critBonus = info.CritBonus;
        }
        else
        {
            baseMin = c.Def.Damage * 0.6f; baseMax = c.Def.Damage * 1.0f;
            type = DamageType.Blunt; speed = 1f; reach = 0f; stun = 0f; critBonus = 0f;
        }

        // Magic weapons scale with Intelligence, everything else with Strength —
        // and both take the flat damage off rings. Then skills multiply the lot,
        // exactly as the sheet's Physical/Magic Damage rows do.
        bool magic = DamageTypeInfo.Of(type).IsMagic;
        float attrBonus = magic ? a.Intelligence * 2.1f : a.Strength * 1.6f;
        float mult = 1f + sk.Damage;
        return new WeaponProfile(
            MathF.Max(1f, (baseMin + attrBonus + gear.Damage) * mult),
            MathF.Max(1f, (baseMax + attrBonus + gear.Damage) * mult),
            type, speed, reach, stun, critBonus);
    }

    // --- the formulas, each small enough to read at a glance ---
    public static float PhysicalDamage(int baseDamage, Attributes a) => baseDamage + a.Strength * 1.6f;
    public static float MagicDamage(int baseDamage, Attributes a) => baseDamage * 0.6f + a.Intelligence * 2.1f;

    /// <summary>Armour rating. Defense carries it, with Vitality contributing a
    /// little — a tough body absorbs something regardless of training.</summary>
    public static float PhysicalDefense(Attributes a) => a.Defense * 2.2f + a.Vitality * 0.5f;

    /// <summary>Armour converted to a fraction of damage removed.
    ///
    /// A diminishing curve rather than flat subtraction: subtraction eventually
    /// makes a hero immune to weak enemies and then to everything, whereas this
    /// approaches — but never reaches — total immunity. Capped anyway, because a
    /// hero who cannot be hurt has no game left.</summary>
    public static float DamageReduction(float armour) =>
        Math.Min(0.75f, armour / (armour + 60f));

    public static float MagicResistance(Attributes a) => Math.Min(0.75f, a.Wisdom * 0.012f);

    public static float CritChance(Attributes a) =>
        Math.Min(0.60f, 0.02f + a.Dexterity * 0.005f + a.Luck * 0.003f);

    public static float CritDamage(Attributes a) => 1.5f + a.Luck * 0.012f;
    public static float Accuracy(Attributes a) => 60 + a.Dexterity * 2.4f;
    public static float Dodge(Attributes a) => Math.Min(0.45f, a.Agility * 0.007f);
    public static float Block(Attributes a) => Math.Min(0.50f, a.Defense * 0.010f);
    public static float HealthRegen(Attributes a) => a.Vitality * 0.09f;
    /// <summary>Mana per second.
    ///
    /// Tuned against the pool and the ability cost together: at baseline Wisdom a
    /// hero gets two or three casts before running dry, and then waits. The first
    /// numbers here were far too generous — regeneration over one cooldown very
    /// nearly paid for the next cast, so a full pool lasted eleven casts and mana
    /// was decoration in every fight that mattered.</summary>
    public static float ManaRegen(Attributes a) => 1.0f + a.Wisdom * 0.22f;

    /// <summary>Stamina per second. Agility both deepens the pool and refills it,
    /// which is what makes a nimble hero able to keep dodging.</summary>
    public static float StaminaRegen(Attributes a) => 8f + a.Agility * 0.6f;

    /// <summary>Luck's real effect: it multiplies drop chances in the engine's
    /// loot roll, alongside the Delver's Luck skill. Without this the attribute
    /// would describe a benefit it never delivered.</summary>
    public static float LootChance(Attributes a) => a.Luck * 0.011f;

    /// <summary>Attacks per second. Dexterity shortens the class cooldown, with a
    /// floor so no amount of investment turns attacking into a solid beam.</summary>
    public static float AttackSpeed(float baseCooldown, Attributes a) =>
        1f / Math.Max(0.08f, baseCooldown - a.Dexterity * 0.006f);

    public static float MoveSpeed(float baseSpeed, Attributes a) => baseSpeed + a.Agility * 1.8f;

    public static float MaxHp(int baseHp, Attributes a) => baseHp + a.Vitality * 6f;
    /// <summary>The mana pool. Deliberately only a few casts deep — a pool that
    /// holds ten is a pool nobody watches.</summary>
    public static float MaxMana(Attributes a) => 30 + a.Intelligence * 4f;
    public static float MaxStamina(Attributes a) => 40 + a.Vitality * 3f + a.Agility * 2f;
}

/// <summary>The equipped weapon's resolved damage, with the wearer folded in.
/// Min/Max are the final range the engine rolls between.</summary>
public readonly record struct WeaponProfile(
    float Min, float Max, DamageType Damage, float SpeedFactor, float Reach, float StunChance, float CritBonus);

/// <summary>One row of the derived-stats panel.</summary>
/// <param name="Pending">True for a stat the interface names but nothing in the
/// game feeds yet. Shown greyed with a dash — inventing a number for it would
/// tell the player a system exists when it does not.</param>
public sealed record DerivedStat(string Name, string Value, string Description, bool Pending = false);
