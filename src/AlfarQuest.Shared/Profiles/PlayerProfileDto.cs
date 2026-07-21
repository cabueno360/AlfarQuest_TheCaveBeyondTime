namespace AlfarQuest.Shared.Profiles;

/// <summary>Everything the profile page renders. Deliberately one flat record
/// rather than a graph: it is read far more often than it is written, and the
/// page needs all of it at once.
///
/// Adding a field here is the intended way to grow the profile — nothing
/// downstream switches on its shape.</summary>
public sealed record PlayerProfileDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string? Country { get; init; }
    public string? PhoneNumber { get; init; }

    /// <summary>Null when the player has never uploaded one; the UI then draws the
    /// fantasy default rather than a broken image.</summary>
    public string? AvatarUrl { get; init; }

    public DateTime RegisteredAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    public PlayerStatsDto Stats { get; init; } = new();

    /// <summary>Empty today. The page already renders the section, so earning the
    /// first one is a server change alone.</summary>
    public IReadOnlyList<AchievementDto> Achievements { get; init; } = [];
}

/// <summary>The numbers the game reports back after a session.</summary>
public sealed record PlayerStatsDto
{
    public string? CurrentCharacter { get; init; }
    public int HighestLevel { get; init; }
    public long TotalGold { get; init; }
    public long TotalPlayTimeSeconds { get; init; }

    public string PlayTimeDisplay => TotalPlayTimeSeconds switch
    {
        < 60 => $"{TotalPlayTimeSeconds}s",
        < 3600 => $"{TotalPlayTimeSeconds / 60}m",
        _ => $"{TotalPlayTimeSeconds / 3600}h {TotalPlayTimeSeconds % 3600 / 60}m",
    };
}

public sealed record AchievementDto
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTime EarnedAt { get; init; }
}
