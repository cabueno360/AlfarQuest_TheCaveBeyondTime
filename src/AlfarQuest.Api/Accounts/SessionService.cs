using AlfarQuest.Api.Auth;
using AlfarQuest.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlfarQuest.Api.Accounts;

public sealed class SessionService(GameDbContext db, IOptions<AuthOptions> options, TimeProvider clock)
    : ISessionService
{
    private readonly AuthOptions _options = options.Value;

    public async Task<IssuedSession> IssueAsync(
        PlayerAccount account, bool rememberMe, string? userAgent, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var lifetime = rememberMe ? _options.RememberMeLifetime : _options.SessionLifetime;
        var token = SecureTokens.Mint();

        db.Sessions.Add(new PlayerSession
        {
            PlayerAccountId = account.Id,
            TokenHash = SecureTokens.Fingerprint(token),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now + lifetime,
            // Attacker-controlled: bounded before it is ever written.
            UserAgent = userAgent?.Length > 256 ? userAgent[..256] : userAgent,
        });

        account.LastLoginAt = now;
        await db.SaveChangesAsync(ct);

        return new IssuedSession(token, now + lifetime);
    }

    public async Task<PlayerAccount?> ResolveAsync(string token, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var fingerprint = SecureTokens.Fingerprint(token);

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == fingerprint, ct);
        if (session is null || !session.IsActive(now)) return null;

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == session.PlayerAccountId, ct);
        if (account is null) return null;

        // Touch at most once a minute. Writing on every request would turn each
        // authenticated read into a write, for a field nothing reads that often.
        if (now - session.LastSeenAt > TimeSpan.FromMinutes(1))
        {
            session.LastSeenAt = now;
            await db.SaveChangesAsync(ct);
        }

        return account;
    }

    public async Task RevokeAsync(string token, CancellationToken ct = default)
    {
        var fingerprint = SecureTokens.Fingerprint(token);
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == fingerprint, ct);
        if (session is null || session.RevokedAt is not null) return;

        session.RevokedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllAsync(Guid accountId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.Sessions
            .Where(s => s.PlayerAccountId == accountId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
    }
}
