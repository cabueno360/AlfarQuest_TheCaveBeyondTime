namespace AlfarQuest.Client.Models;

public enum SkillCategory { Combat, Magic, Passive, Crafting }

/// <summary>A skill definition. Static data; what a character has invested lives
/// in <see cref="SkillBook"/>, so the catalogue can be shared by every hero.</summary>
public sealed record Skill
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required SkillCategory Category { get; init; }
    public string Icon { get; init; } = "✦";
    public string Description { get; init; } = "";
    public int MaxLevel { get; init; } = 5;

    /// <summary>What the next rank adds, phrased for the card.</summary>
    public required Func<int, string> NextBonus { get; init; }

    /// <summary>Points to reach <paramref name="level"/>. Later ranks cost more,
    /// so a maxed skill is a real choice rather than an inevitability.</summary>
    public int CostFor(int level) => 1 + level / 2;

    /// <summary>What a rank of this skill does to the simulation. Declared on the
    /// skill rather than switched on an id inside the engine, so a new skill is
    /// still one entry in the catalogue and the engine never learns its name.
    /// Null means the skill has no mechanical effect yet.</summary>
    public Func<int, SkillEffect>? PerRank { get; init; }

    /// <summary>Which classes may learn it. Empty means anyone.</summary>
    public IReadOnlyList<string> Classes { get; init; } = [];

    public bool AvailableTo(string heroClass) =>
        Classes.Count == 0 || Classes.Contains(heroClass);
}

/// <summary>What ranks of a skill contribute. Multipliers are additive fractions
/// (0.1 = +10%) so several skills can stack without one silently dominating.</summary>
public readonly record struct SkillEffect(
    float Damage = 0,
    float AbilityDamage = 0,
    float AbilityRadius = 0,
    float DashCooldown = 0,
    float AttackSpeed = 0,
    float HealthRegen = 0,
    float BlockChance = 0,
    float LootChance = 0,
    float MaterialYield = 0)
{
    public static SkillEffect operator +(SkillEffect a, SkillEffect b) => new(
        a.Damage + b.Damage,
        a.AbilityDamage + b.AbilityDamage,
        a.AbilityRadius + b.AbilityRadius,
        a.DashCooldown + b.DashCooldown,
        a.AttackSpeed + b.AttackSpeed,
        a.HealthRegen + b.HealthRegen,
        a.BlockChance + b.BlockChance,
        a.LootChance + b.LootChance,
        a.MaterialYield + b.MaterialYield);
}

/// <summary>Ranks a character has bought.</summary>
public sealed class SkillBook
{
    private readonly Dictionary<string, int> _ranks = [];

    public int RankOf(string skillId) => _ranks.GetValueOrDefault(skillId);
    public void Set(string skillId, int rank) => _ranks[skillId] = rank;

    /// <summary>Everything learned, for saving. Ranks of zero are left out — an
    /// unlearned skill is the absence of a row, not a row saying nothing.</summary>
    public IEnumerable<(string SkillId, int Rank)> Learned() =>
        _ranks.Where(kv => kv.Value > 0).Select(kv => (kv.Key, kv.Value));

    public void Clear() => _ranks.Clear();
}
