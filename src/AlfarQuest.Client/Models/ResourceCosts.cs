namespace AlfarQuest.Client.Models;

/// <summary>What actions cost, and how fast the pools that pay for them refill.
///
/// Separate from StatCalculator on purpose: that file turns attributes into
/// numbers, whereas these are design decisions about pacing. Keeping them apart
/// means retuning how often an ultimate can be cast never risks touching the
/// formula for how much damage it does.
///
/// The intent of the numbers below: a full pool is worth two or three casts, and
/// after that regeneration — not the cooldown — decides how often the ultimate
/// comes back. So a fight opens with a burst and then rations, and Wisdom is what
/// shortens the rationing.
///
/// The first pass got this wrong in a way only measurement showed: regeneration
/// over one cooldown nearly paid for the next cast, so a full pool lasted eleven
/// casts and mana never once bound in a real fight.</summary>
public static class ResourceCosts
{
    /// <summary>Mana per use of a class ultimate. The Mage's is the loud one —
    /// twice the radius and nearly twice the damage — so it costs more.</summary>
    public static int Ability(string heroClass) => heroClass switch
    {
        "Mage" => 40,
        "Cleric" => 28,
        "Thief" => 26,
        _ => 30,
    };

    /// <summary>Stamina per dash. Enough that a panicked triple-dash empties the
    /// pool, not so much that one dodge has to be rationed.</summary>
    public const float Dash = 25f;

    /// <summary>What a hero has when the character sheet has not published a pool
    /// yet — in a test, or before the party loads. The simulation must stay
    /// playable without the UI, and a max of zero would silently disable every
    /// ability in the game.</summary>
    public const float FallbackMaxMana = 80f;
    public const float FallbackMaxStamina = 70f;
    public const float FallbackManaRegen = 4f;
    public const float FallbackStaminaRegen = 12f;
}
