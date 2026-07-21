using AlfarQuest.Api.Data;

namespace AlfarQuest.Api.Accounts;

/// <summary>Issues, resolves and revokes the bearer tokens that stand for a
/// signed-in player.</summary>
public interface ISessionService
{
    Task<IssuedSession> IssueAsync(PlayerAccount account, bool rememberMe, string? userAgent, CancellationToken ct = default);

    /// <summary>The account behind a token, or null if the token is unknown,
    /// expired or revoked. Called on every authenticated request.</summary>
    Task<PlayerAccount?> ResolveAsync(string token, CancellationToken ct = default);

    /// <summary>Signs out of this one session, leaving other devices alone.</summary>
    Task RevokeAsync(string token, CancellationToken ct = default);

    /// <summary>Signs out everywhere. Used after a password change, because the
    /// most likely reason to change one is that it may be known.</summary>
    Task RevokeAllAsync(Guid accountId, CancellationToken ct = default);
}

/// <param name="Token">The only time the plaintext token exists outside the
/// client — it is hashed before it reaches the database.</param>
public readonly record struct IssuedSession(string Token, DateTime ExpiresAt);
