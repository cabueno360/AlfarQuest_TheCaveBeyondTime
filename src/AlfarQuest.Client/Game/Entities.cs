using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Runtime entities. Plain mutable classes on purpose: these are
//  stepped 60 times a second, and records would allocate a copy per
//  entity per frame.
// =====================================================================
public class Hero
{
    public Lore.HeroDef Def;
    public Vec Pos;
    public float Hp, Facing, Cool, AbilityCool, DashCool, IFrames, Flash, AttackAnim, AbilityAnim;

    /// <summary>Live pools, owned here like Hp. They used to be shown full on the
    /// character sheet because nothing spent them — the sheet was reporting a
    /// resource that did not exist.</summary>
    public float Mana, Stamina;

    /// <summary>Computed, not stored: Vitality spent mid-delve has to raise the
    /// pool immediately, and a value captured at construction would leave the
    /// attribute decorative until the next world rebuild.</summary>
    public float MaxHp => Def.BaseHp + CharacterStats.For(Def.Key).BonusMaxHp;
    public Vec DashVel;
    public bool Alive => Hp > 0;
    public Hero(Lore.HeroDef def, Vec pos)
    {
        Def = def; Pos = pos;
        Hp = MaxHp;
        Mana = MaxMana;
        Stamina = MaxStamina;
    }

    // Pools and their refill. Each falls back to a playable default when the
    // character sheet has not published one: the simulation must run without the
    // UI, and a maximum of zero would silently disable every ability in the game.
    public float MaxMana => Positive(CharacterStats.For(Def.Key).MaxMana, ResourceCosts.FallbackMaxMana);
    public float MaxStamina => Positive(CharacterStats.For(Def.Key).MaxStamina, ResourceCosts.FallbackMaxStamina);
    public float ManaRegen => Positive(CharacterStats.For(Def.Key).ManaRegen, ResourceCosts.FallbackManaRegen);
    public float StaminaRegen => Positive(CharacterStats.For(Def.Key).StaminaRegen, ResourceCosts.FallbackStaminaRegen);

    /// <summary>The mana one use of this hero's ultimate costs.</summary>
    public int AbilityCost => ResourceCosts.Ability(Def.HeroClass);

    /// <summary>Spends from a pool if it can be paid in full. Partial spending is
    /// deliberately impossible: half a cast is not a thing, and a caller that
    /// checked separately could drift from the caller that spends.</summary>
    public bool SpendMana(float amount)
    {
        if (Mana < amount) return false;
        Mana -= amount;
        return true;
    }

    public bool SpendStamina(float amount)
    {
        if (Stamina < amount) return false;
        Stamina -= amount;
        return true;
    }

    static float Positive(float value, float fallback) => value > 0 ? value : fallback;

    /// <summary>Damage this hero deals, including attribute bonuses.</summary>
    public int Damage
    {
        get
        {
            var m = CharacterStats.For(Def.Key);
            return (int)MathF.Round((Def.Damage + m.BonusDamage) * (1f + m.DamageMultiplier));
        }
    }

    /// <summary>Incoming damage after armour, never below 1.
    ///
    /// Computed here rather than at the call site so every future source of harm
    /// — traps, magic, falling — gets the same treatment by construction. The
    /// floor matters: without it enough Defense makes a hero untouchable, and a
    /// fight nobody can lose stops being one.</summary>
    public float Absorb(float incoming, bool magical = false)
    {
        var m = CharacterStats.For(Def.Key);
        var reduction = magical ? m.MagicResistance : m.DamageReduction;
        return MathF.Max(1f, incoming * (1f - reduction));
    }

    /// <summary>Skill-boosted ability damage and reach.</summary>
    public float AbilityDamageMultiplier => 1f + CharacterStats.For(Def.Key).AbilityDamageMultiplier;
    public float AbilityRadiusBonus => CharacterStats.For(Def.Key).AbilityRadiusBonus;
    public float BlockChance => CharacterStats.For(Def.Key).BlockChance;
    public float HealthRegen => CharacterStats.For(Def.Key).HealthRegen;

    /// <summary>Movement speed, including attribute bonuses.</summary>
    public float Speed => Def.Speed + CharacterStats.For(Def.Key).BonusSpeed;

    /// <summary>Seconds between attacks, floored so no build turns attacking
    /// into a continuous beam.</summary>
    public float AttackCooldown
    {
        get
        {
            var m = CharacterStats.For(Def.Key);
            return MathF.Max(0.08f, (Def.AttackCooldown - m.CooldownReduction) / (1f + m.AttackSpeedMultiplier));
        }
    }

    /// <summary>Seconds before the next dash. Footwork shortens it.</summary>
    public float DashCooldown =>
        MathF.Max(0.2f, 0.9f * (1f + CharacterStats.For(Def.Key).DashCooldownMultiplier));
}

/// <summary>A creature. One class for every species and both stages: the cave's
/// husks and the outdoor wildlife differ only in their <see cref="Def"/>, so all
/// the combat, damage, death and health-bar code already written applies to both
/// without a parallel type.</summary>
public class Husk
{
    public Vec Pos, Knock, Home, PatrolTarget;
    public float Hp, MaxHp, Speed, R, HitCool, Flash;
    public CreatureType Def;

    /// <summary>Patrol wanders near home; Chase follows the party; Return walks
    /// back once the party is out of reach. Kept explicit rather than inferred
    /// from distances so the transitions are readable and testable.</summary>
    public AiState State = AiState.Patrol;
    public float Think;                 // seconds until the next patrol decision

    public Husk(Vec p) : this(p, CreatureCatalog.Of("husk")) { }

    public Husk(Vec p, CreatureType def)
    {
        Def = def;
        Pos = Home = PatrolTarget = p;
        MaxHp = Hp = def.MaxHp;
        Speed = def.Speed;
        R = def.Radius;
    }
}

public enum AiState { Patrol, Chase, Return }

public class Projectile
{
    public Vec Pos, Vel; public int Damage; public string Color; public float Life;
    public Projectile(Vec p, Vec v, int dmg, string c, float life) { Pos = p; Vel = v; Damage = dmg; Color = c; Life = life; }
}

public class Slash
{
    public Vec Pos; public float Angle, Life, Radius = 60; public string Color; public bool Nova;
    public Slash(Vec p, float a, string c, float life) { Pos = p; Angle = a; Color = c; Life = life; }
}

public class Particle
{
    public Vec Pos, Vel; public string Color; public float Life;
    public Particle(Vec p, Vec v, string c, float life) { Pos = p; Vel = v; Color = c; Life = life; }
}

public class Prop
{
    public float X, Y, S, R;
    public int Cx, Cy;              // cave props: cell in Miner_Decorations
    public string Kind = "";        // outdoor props: name in atlas_outside.json
    public int Variant;
    public bool Solid, Flip;
}

public class Crystal
{
    public Vec Pos; public float R;
    public Crystal(Vec p, float r) { Pos = p; R = r; }
}

public struct Vec
{
    public float X, Y;
    public Vec(float x, float y) { X = x; Y = y; }
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, float s) => new(a.X * s, a.Y * s);
    public float Len() => MathF.Sqrt(X * X + Y * Y);
    public Vec Norm() { float l = Len(); return l < 1e-5f ? new Vec(0, 0) : new Vec(X / l, Y / l); }
}
