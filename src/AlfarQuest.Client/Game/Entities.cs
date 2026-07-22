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

    /// <summary>Each hotbar skill's own cooldown, indexed by slot 1–4 (0 unused).
    /// The single ultimate became four skills, so one timer became four.</summary>
    public readonly float[] SkillCool = new float[5];

    /// <summary>The healing draught's cooldown. Its own thing, not a skill —
    /// every class has it, and it costs no mana.</summary>
    public float PotionCool;

    // ---- companion AI (unused while this hero is the one being steered) ----
    /// <summary>Seconds before this companion will act on a threat — a human beat
    /// between a monster appearing and the swing. Re-rolled after every attack and
    /// whenever there is nothing to fight, so reactions never become instant.</summary>
    public float ReactCool;
    /// <summary>A loose wobble added to the formation slot, re-rolled every few
    /// seconds, so the party drifts rather than locking into a rigid triangle.</summary>
    public Vec SlotDrift;
    public float SlotDriftCool;

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

    /// <summary>Whether this hero's death has already been tallied, so a hero that
    /// stays down does not count as dying on every frame.</summary>
    public bool DeathCounted;

    /// <summary>Status effects on this hero — venom from a spider, and whatever
    /// else the architecture grows to carry. Ticked by the engine.</summary>
    public readonly List<Models.ActiveEffect> Effects = new();

    /// <summary>Held fast by a stun or a freeze. Nothing does this to a hero yet,
    /// but the check is here so the moment something can, it works.</summary>
    public bool Immobilised
    {
        get { foreach (var e in Effects) if (e.Immobilises && e.Remaining > 0) return true; return false; }
    }
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

    /// <summary>The equipped weapon's rolled damage — a fresh number every swing,
    /// between the weapon's min and max. This is the brief's first rule made real:
    /// no attack deals a fixed amount.</summary>
    public int RollDamage(Func<double> rnd)
    {
        var m = CharacterStats.For(Def.Key);
        var min = m.WeaponMin; var max = MathF.Max(min, m.WeaponMax);
        return (int)MathF.Round(min + (float)rnd() * (max - min));
    }

    /// <summary>The middle of the weapon's range, for anything that needs one
    /// number rather than a roll — a display, or a fallback.</summary>
    public int Damage
    {
        get { var m = CharacterStats.For(Def.Key); return (int)MathF.Round((m.WeaponMin + m.WeaponMax) * 0.5f); }
    }

    /// <summary>The weapon's damage type, reach, stun chance and the wearer's
    /// accuracy and dodge — read by combat so the equipped weapon and the
    /// defensive attributes finally reach the fight.</summary>
    public Models.DamageType DamageType => CharacterStats.For(Def.Key).WeaponDamage;
    public float StunChance => CharacterStats.For(Def.Key).WeaponStunChance;
    public float Accuracy => CharacterStats.For(Def.Key).Accuracy;
    public float DodgeChance => CharacterStats.For(Def.Key).DodgeChance;

    /// <summary>Melee reach. The weapon's, when it has one — a spear outreaches a
    /// dagger — otherwise the class default.</summary>
    public float Range
    {
        get { var r = CharacterStats.For(Def.Key).AttackReach; return r > 0 ? r : Def.Range; }
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
            // The weapon's pace on top of the attribute and skill speed: a hammer
            // (1.6) swings well over half again as slow as a dagger (0.62).
            return MathF.Max(0.08f,
                (Def.AttackCooldown - m.CooldownReduction) / (1f + m.AttackSpeedMultiplier) * m.WeaponSpeedFactor);
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

    /// <summary>Its current AI state — see <see cref="AiState"/>. Set from the
    /// species' demeanor at birth and stepped by StepAi.</summary>
    public AiState State = AiState.Patrol;
    public float Think;                 // seconds until the next patrol decision

    /// <summary>Counts down while it is committed by a neighbour's cry — a struck
    /// creature rouses those around it, and for a few seconds they will chase
    /// further than they normally would. This is what keeps group aggro local: it
    /// expires, so it cannot chain across the map.</summary>
    public float Alerted;

    /// <summary>Seconds until its one trick is ready again. Nothing to do with the
    /// bite, which is gated separately by <see cref="HitCool"/>.</summary>
    public float AbilityCool;

    /// <summary>Who struck last, so the kill pays the hero who landed the killing
    /// blow rather than the whole party. Null until something hits it — a death
    /// with no striker (a future trap) falls back to the steered hero.</summary>
    public string? LastHitBy;

    /// <summary>Status effects currently on it — a stun from a hammer, venom from
    /// a spider. The engine ticks these; the list is the runtime half of the
    /// architecture the catalogue describes.</summary>
    public readonly List<Models.ActiveEffect> Effects = new();

    /// <summary>Frozen in place by a stun or a freeze, so it can neither move nor
    /// act this frame.</summary>
    public bool Immobilised
    {
        get { foreach (var e in Effects) if (e.Immobilises && e.Remaining > 0) return true; return false; }
    }

    /// <summary>How much a slow is dragging on it, 0–1 — the strongest one wins.
    /// A frost shard leaves the creature crawling.</summary>
    public float Slow
    {
        get
        {
            float s = 0;
            foreach (var e in Effects)
                if (e.Behaviour == Models.StatusBehaviour.SlowMovement && e.Remaining > 0)
                    s = MathF.Max(s, e.Magnitude);
            return MathF.Min(0.9f, s);
        }
    }

    /// <summary>Never moved by anything — knockback included. Only the test
    /// training dummy sets this, so a hero can keep swinging at a target that does
    /// not fly out of reach on the first hit.</summary>
    public bool Rooted;

    public Husk(Vec p) : this(p, CreatureCatalog.Of("husk")) { }

    public Husk(Vec p, CreatureType def)
    {
        Def = def;
        Pos = Home = PatrolTarget = p;
        MaxHp = Hp = def.MaxHp;
        Speed = def.Speed;
        R = def.Radius;
        // Sleepers and ambushers begin still and unnoticed; everything else is
        // already up and about.
        State = def.Demeanor is Demeanor.Sleeper or Demeanor.Ambusher
            ? AiState.Sleep : AiState.Patrol;
    }
}

/// <summary>Sleep sits still until roused; Patrol wanders near home; Chase
/// follows the party; Return walks back once they are out of reach; Flee runs
/// from them when badly hurt. Kept explicit rather than inferred from distances
/// so the transitions are readable and testable.</summary>
public enum AiState { Patrol, Chase, Return, Sleep, Flee }

public class Projectile
{
    public Vec Pos, Vel; public int Damage; public string Color; public float Life;
    /// <summary>The hero who loosed it, carried so a kill by a bolt pays its
    /// owner and not whoever happens to be steering when it lands. Empty for a
    /// monster's bolt, which flies the other way.</summary>
    public string Owner = "";

    /// <summary>Turned aside by Wisdom rather than armour. Set on the magical
    /// monster bolts, so a party built only for physical defence still fears
    /// them.</summary>
    public bool Magical;

    /// <summary>An effect the bolt leaves on whatever it hits — the spider's spit
    /// carries venom, a poison arrow the same. Null for a plain bolt.</summary>
    public Models.StatusEffectKind? OnHit;

    /// <summary>A skill bolt's own damage type, overriding the weapon's — a
    /// Firebolt deals Fire whatever the mage holds. Null on an ordinary shot,
    /// which takes the shooter's weapon type.</summary>
    public Models.DamageType? SkillType;

    /// <summary>Whether this bolt can roll a critical. Off for spreads, which are
    /// already several hits.</summary>
    public bool CanCrit = true;

    public Projectile(Vec p, Vec v, int dmg, string c, float life, string owner = "", bool magical = false,
                      Models.StatusEffectKind? onHit = null)
    { Pos = p; Vel = v; Damage = dmg; Color = c; Life = life; Owner = owner; Magical = magical; OnHit = onHit; }
}

public class Slash
{
    public Vec Pos; public float Angle, Life, Radius = 60; public string Color; public bool Nova;
    public Slash(Vec p, float a, string c, float life) { Pos = p; Angle = a; Color = c; Life = life; }
}

/// <summary>One mote of an effect.
///
/// Carries its own physics rather than deriving it from a shared constant: a
/// rock fragment falls hard, an ember drifts upward and a mote of forest light
/// barely moves at all, and one gravity for all three would make every effect
/// the same effect wearing a different colour.</summary>
public class Particle
{
    public Vec Pos, Vel;
    public string Color;
    public float Life;

    /// <summary>What it started with, so the renderer can fade and shrink it
    /// against its own span instead of a fixed one.</summary>
    public float MaxLife;

    public float Size;

    /// <summary>Pixels per second squared. Negative rises — embers and souls.</summary>
    public float Gravity;

    /// <summary>Velocity kept each frame. 1 is frictionless; 0.88 settles fast.</summary>
    public float Drag;

    /// <summary>Drawn with lighter compositing, so overlapping motes build into
    /// light rather than into mud. Right for sparks and magic, wrong for dust.</summary>
    public bool Additive;

    public Particle(Vec p, Vec v, string c, float life,
                    float size = 3f, float gravity = 0f, float drag = 1f, bool additive = false)
    {
        Pos = p; Vel = v; Color = c; Life = MaxLife = life;
        Size = size; Gravity = gravity; Drag = drag; Additive = additive;
    }
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
