using AlfarQuest.Api.Auth;
using AlfarQuest.Api.Common;
using AlfarQuest.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AlfarQuest.Api.Accounts;

/// <summary>Resolves an external identity to a local account, creating one on
/// first sight. Written now so the provider that arrives later has nothing to
/// decide about account matching — the subtle part of the feature.</summary>
public sealed class ExternalAccountLinker(GameDbContext db, TimeProvider clock)
{
    public async Task<Result<PlayerAccount>> ResolveAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        var existing = await db.Accounts.FirstOrDefaultAsync(
            a => a.ExternalProvider == identity.Provider && a.ExternalSubjectId == identity.SubjectId, ct);
        if (existing is not null) return Result<PlayerAccount>.Success(existing);

        var email = EmailAddress.Normalise(identity.Email);
        var byEmail = await db.Accounts.FirstOrDefaultAsync(a => a.Email == email, ct);

        if (byEmail is not null)
        {
            // Only link to an existing local account when the provider vouches for
            // the address. Without that, anyone who can make a provider account
            // claiming someone else's email would inherit their game.
            if (!identity.EmailVerified)
                return Result<PlayerAccount>.Fail(
                    "That email already has an account. Sign in with your password to link it.");

            byEmail.ExternalProvider = identity.Provider;
            byEmail.ExternalSubjectId = identity.SubjectId;
            await db.SaveChangesAsync(ct);
            return Result<PlayerAccount>.Success(byEmail);
        }

        var account = new PlayerAccount
        {
            Email = email,
            FullName = identity.FullName,
            // No local password: sign-in for this account goes through the
            // provider. A hash of unusable random bytes rather than an empty
            // string, so a bug that skipped the provider check cannot match "".
            PasswordHash = new Pbkdf2PasswordHasher().Hash(SecureTokens.Mint()),
            EmailConfirmed = identity.EmailVerified,
            ExternalProvider = identity.Provider,
            ExternalSubjectId = identity.SubjectId,
            RegisteredAt = clock.GetUtcNow().UtcDateTime,
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return Result<PlayerAccount>.Success(account);
    }
}
