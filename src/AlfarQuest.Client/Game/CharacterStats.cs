namespace AlfarQuest.Client.Game;

/// <summary>The seam between the character sheet and the simulation.
///
/// The engine must not depend on Blazor services, and the UI must not reach into
/// the engine's entities — so the sheet publishes a lookup here and the engine
/// consults it. Unset (in a test, or before the party loads) every hero simply
/// uses its class baseline, so the simulation never depends on the UI existing.
/// </summary>
public static class CharacterStats
{
    /// <summary>Attribute-derived modifiers for a hero key.</summary>
    public static Func<string, HeroModifiers>? Lookup;

    public static HeroModifiers For(string key) => Lookup?.Invoke(key) ?? HeroModifiers.None;

    /// <summary>The party's best looter decides what the dark gives up — the
    /// bonus is a party-wide effect, not something only the hero who landed the
    /// killing blow enjoys.</summary>
    public static Func<(float Chance, float Yield)>? PartyFortune;

    /// <summary>Level and experience, for the canvas HUD to draw.
    ///
    /// Read-only and separate from <see cref="Lookup"/>: the engine displays
    /// progression but must never be able to change it, and a lookup that
    /// returned combat modifiers alongside XP would invite exactly that.</summary>
    public static Func<string, HeroProgress>? Progress;

    public static HeroProgress ProgressOf(string key) => Progress?.Invoke(key) ?? HeroProgress.None;
}

/// <param name="XpNext">XP needed for the level in hand, not the total earned —
/// the HUD draws a bar for the current level, not for the whole career.</param>
public readonly record struct HeroProgress(int Level, int Xp, int XpNext)
{
    public static readonly HeroProgress None = new(1, 0, 100);

    public float Fraction => XpNext <= 0 ? 0 : Math.Clamp(Xp / (float)XpNext, 0f, 1f);
}

/// <summary>What attributes add on top of a hero's class baseline. Additive and
/// multiplicative parts are kept apart so the intent of each is readable.</summary>
public readonly record struct HeroModifiers(
    float BonusDamage,
    float BonusMaxHp,
    float BonusSpeed,
    float CooldownReduction,
    // Skill-derived. Multipliers are additive fractions: 0.1 means +10%.
    float DamageMultiplier = 0,
    float AbilityDamageMultiplier = 0,
    float AbilityRadiusBonus = 0,
    float DashCooldownMultiplier = 0,
    float AttackSpeedMultiplier = 0,
    float HealthRegen = 0,
    float BlockChance = 0,
    float LootChance = 0,
    float MaterialYield = 0,
    // Attribute-derived, as fractions of incoming damage removed.
    float DamageReduction = 0,
    float MagicResistance = 0,
    // The mana and stamina pools. Absolute, not bonuses like BonusMaxHp: unlike
    // health there is no class baseline in HeroDef for these to add to.
    float MaxMana = 0,
    float MaxStamina = 0,
    float ManaRegen = 0,
    float StaminaRegen = 0)
{
    public static readonly HeroModifiers None = new(0, 0, 0, 0);
}
