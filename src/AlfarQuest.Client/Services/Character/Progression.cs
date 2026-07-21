namespace AlfarQuest.Client.Models;

/// <summary>The levelling curve, in one place so the window, the reward code and
/// any future balancing all read the same numbers.
///
/// Everything here is a constant rather than a hard-coded expression, so pacing
/// can be retuned without hunting through the game for arithmetic.</summary>
public static class Progression
{
    public const int AttributePointsPerLevel = 5;
    public const int SkillPointsPerLevel = 1;
    public const int MaxLevel = 60;

    /// <summary>The opening levels, written out rather than computed.
    ///
    /// The first hour is where pacing matters most and where a formula is least
    /// trustworthy, so these four are chosen by hand. Beyond them the curve takes
    /// over — a table to level 60 would be a wall of numbers nobody would keep
    /// balanced.</summary>
    private static readonly int[] EarlyLevels = [100, 180, 300, 500];

    /// <summary>How steeply the cost climbs once the hand-written levels run out.
    /// Below ~1.6 late levels arrive too easily; above ~2.1 the last stretch is a
    /// wall.</summary>
    private const float Steepness = 1.85f;

    /// <summary>XP required to go from <paramref name="level"/> to the next.</summary>
    public static int XpForNext(int level)
    {
        if (level < 1) return EarlyLevels[0];
        if (level <= EarlyLevels.Length) return EarlyLevels[level - 1];

        // Continues from the last hand-written value along a power curve, rounded
        // to a round number so the HUD never shows 1,247 XP to the next level.
        var last = EarlyLevels[^1];
        var scaled = last * MathF.Pow(level / (float)EarlyLevels.Length, Steepness);
        return (int)(MathF.Round(scaled / 10f) * 10);
    }

    public static float FractionToNext(int level, int xp) =>
        Math.Clamp(xp / (float)XpForNext(level), 0f, 1f);

    public static bool AtMaxLevel(int level) => level >= MaxLevel;
}
