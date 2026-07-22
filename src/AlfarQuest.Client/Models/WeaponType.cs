namespace AlfarQuest.Client.Models;

/// <summary>What a weapon is, in the way that changes how it fights.
///
/// The whole point of the advanced-combat brief: a hammer and a dagger should
/// feel nothing alike. So each type carries a profile — how fast it swings, how
/// far it reaches, what it does to a critical, whether it can stun — and the
/// engine reads that rather than treating every weapon as a number. A new weapon
/// type is one row in <see cref="WeaponClass.All"/>.</summary>
public enum WeaponType { Sword, Axe, Hammer, Spear, Dagger, Bow, Staff }

/// <summary>The profile of a weapon type. Multipliers are relative to a plain
/// sword, which sits at 1.0 across the board — the balanced middle everything
/// else is fast-or-slow, near-or-far against.</summary>
public sealed record WeaponClass(
    WeaponType Type,
    string Name,
    DamageType Damage,
    /// <summary>Multiplies the attack cooldown. Below 1 is faster (dagger), above
    /// is slower (hammer). The single biggest thing that makes a weapon feel
    /// different in the hand. A slow weapon earns it back in a higher damage range
    /// on the item itself, so there is no separate damage multiplier here to
    /// double-count.</summary>
    float SpeedFactor,
    /// <summary>Added to critical chance. The dagger's whole argument.</summary>
    float CritBonus,
    /// <summary>Reach in world pixels for a melee swing. Longer for a spear,
    /// shorter for a dagger. Ignored by the ranged types, which carry their reach
    /// in the projectile.</summary>
    float Reach,
    /// <summary>Chance to stun on hit. Only the hammer, for now — the first real
    /// status effect, and the reason to carry something so slow.</summary>
    float StunChance = 0f)
{
    public static readonly IReadOnlyList<WeaponClass> All =
    [
        //                                                    speed  crit   reach  stun
        new(WeaponType.Sword,  "Sword",  DamageType.Slashing, 1.00f, 0.00f, 74f),
        new(WeaponType.Axe,    "Axe",    DamageType.Slashing, 1.35f, 0.00f, 72f),
        new(WeaponType.Hammer, "Hammer", DamageType.Blunt,    1.60f, 0.00f, 70f, StunChance: 0.20f),
        new(WeaponType.Spear,  "Spear",  DamageType.Piercing, 1.10f, 0.02f, 118f),
        new(WeaponType.Dagger, "Dagger", DamageType.Piercing, 0.62f, 0.12f, 58f),
        new(WeaponType.Bow,    "Bow",    DamageType.Piercing, 0.95f, 0.04f, 0f),
        new(WeaponType.Staff,  "Staff",  DamageType.Fire,     1.15f, 0.00f, 0f),
    ];

    private static readonly Dictionary<WeaponType, WeaponClass> ById = All.ToDictionary(w => w.Type);

    public static WeaponClass Of(WeaponType t) => ById[t];

    /// <summary>Whether this is a thrown/fired/cast weapon rather than a swung
    /// one. The melee reach and stun are meaningless for these.</summary>
    public bool IsRanged => Type is WeaponType.Bow or WeaponType.Staff;
}
