namespace AlfarQuest.Api.Data;

/// <summary>One signed-in session. A row per sign-in rather than a flag on the
/// account, so a player can be signed in on two machines and sign out of one.</summary>
public class PlayerSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerAccountId { get; set; }

    /// <summary>SHA-256 of the bearer token. The token itself is never stored.</summary>
    public string TokenHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set by sign-out. A revoked row is kept rather than deleted so the
    /// history stays auditable; expiry sweeps clean up eventually.</summary>
    public DateTime? RevokedAt { get; set; }

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>Recorded for the player's own "where am I signed in" list later.
    /// Truncated, because a User-Agent header is attacker-controlled input.</summary>
    public string? UserAgent { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
