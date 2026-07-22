namespace AlfarQuest.Client.Models;

/// <summary>Every status effect the combat system is built to carry.
///
/// The brief asks for the architecture, not all of the effects — so the whole
/// list is named here, and the engine implements the handful that matter now
/// (stun, and the damage-over-time trio) while the rest are ready for a row of
/// wiring later. Naming them all keeps the shape honest: a system that "supports
/// poison" but has no name for it does not.</summary>
public enum StatusEffectKind
{
    Burning, Poison, Bleeding,      // damage over time
    Freeze, Slow, Stun, Silence,    // control
    Curse, Weakness,                // debuffs
    Shield, Regen, ManaRegen,       // boons
}

/// <summary>How an effect behaves: what it looks like, whether it ticks damage,
/// and whether it stops the thing it is on from acting. Data beside the enum, so
/// the engine's tick loop stays a switch on <see cref="Behaviour"/> rather than a
/// switch on the kind.</summary>
public sealed record StatusEffectInfo(
    StatusEffectKind Kind,
    string Name,
    string Icon,
    string Colour,
    StatusBehaviour Behaviour)
{
    public static readonly IReadOnlyList<StatusEffectInfo> All =
    [
        new(StatusEffectKind.Burning,   "Burning",  "🔥", "#ff8a3c", StatusBehaviour.DamageOverTime),
        new(StatusEffectKind.Poison,    "Poison",   "☠",  "#8fd24a", StatusBehaviour.DamageOverTime),
        new(StatusEffectKind.Bleeding,  "Bleeding", "🩸", "#c0392b", StatusBehaviour.DamageOverTime),
        new(StatusEffectKind.Freeze,    "Freeze",   "❄",  "#bfe9ff", StatusBehaviour.Immobilise),
        new(StatusEffectKind.Slow,      "Slow",     "🐌", "#9fb0d8", StatusBehaviour.SlowMovement),
        new(StatusEffectKind.Stun,      "Stun",     "💫", "#ffe66b", StatusBehaviour.Immobilise),
        new(StatusEffectKind.Silence,   "Silence",  "🤐", "#b9c7ff", StatusBehaviour.None),
        new(StatusEffectKind.Curse,     "Curse",    "🕳", "#c98fff", StatusBehaviour.None),
        new(StatusEffectKind.Weakness,  "Weakness", "💔", "#d98a8a", StatusBehaviour.None),
        new(StatusEffectKind.Shield,    "Shield",   "🛡", "#cfd6ff", StatusBehaviour.None),
        new(StatusEffectKind.Regen,     "Regen",    "✚",  "#7fd694", StatusBehaviour.HealOverTime),
        new(StatusEffectKind.ManaRegen, "Focus",    "◈",  "#9fe4ff", StatusBehaviour.None),
    ];

    private static readonly Dictionary<StatusEffectKind, StatusEffectInfo> ById = All.ToDictionary(e => e.Kind);

    public static StatusEffectInfo Of(StatusEffectKind k) => ById[k];
}

/// <summary>What an effect does each tick, decoupled from which effect it is — so
/// burning, poison and bleeding share one implementation and differ only in
/// colour and rate.</summary>
public enum StatusBehaviour { None, DamageOverTime, HealOverTime, Immobilise, SlowMovement }

/// <summary>One effect currently on a creature or a hero. A mutable runtime thing,
/// not catalogue data: it counts down, and it remembers who applied it so a
/// kill-by-poison still pays the right hero.</summary>
public sealed class ActiveEffect(StatusEffectKind kind, float seconds, float magnitude, string? source = null)
{
    public StatusEffectKind Kind { get; } = kind;
    public float Remaining { get; set; } = seconds;
    /// <summary>Damage or heal per second for the over-time behaviours; the slow
    /// fraction for Slow; unused otherwise.</summary>
    public float Magnitude { get; } = magnitude;
    /// <summary>The hero key that applied it, so a lingering effect's kill is
    /// still individual. Null for an effect the world itself applied.</summary>
    public string? Source { get; } = source;

    /// <summary>Accumulates fractional ticks, so a two-per-second poison lands
    /// whole points rather than being lost to rounding each frame.</summary>
    public float TickPool;

    public StatusBehaviour Behaviour => StatusEffectInfo.Of(Kind).Behaviour;
    public bool Immobilises => Behaviour == StatusBehaviour.Immobilise;
}
