using AlfarQuest.Shared.Profiles;

namespace AlfarQuest.Shared.Auth;

/// <summary>What a successful sign-in or sign-up hands back: the bearer token and
/// the profile, so the client needs no second round trip to render the shell.</summary>
public sealed record AuthSessionDto
{
    public string Token { get; init; } = "";
    public DateTime ExpiresAt { get; init; }
    public PlayerProfileDto Player { get; init; } = new();
}

/// <summary>A failure the player is allowed to see. Field is set when the message
/// belongs beside one input; null when it belongs above the whole form.</summary>
public sealed record AuthProblem(string Message, string? Field = null);
