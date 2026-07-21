using AlfarQuest.Api.Auth;
using AlfarQuest.Api.Common;
using AlfarQuest.Api.Data;
using AlfarQuest.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace AlfarQuest.Api.Accounts;

public sealed class AccountService(
    GameDbContext db,
    IPasswordHasher passwords,
    TimeProvider clock,
    ILogger<AccountService> log) : IAccountService
{
    /// <summary>A real hash of a password nobody has. Verifying against it when the
    /// address is unknown makes a miss cost the same as a hit — otherwise the
    /// response time alone tells an attacker which addresses are registered.</summary>
    private static readonly Lazy<string> DecoyHash =
        new(() => new Pbkdf2PasswordHasher().Hash("this account does not exist"));

    public async Task<Result<PlayerAccount>> RegisterAsync(SignUpRequest request, CancellationToken ct = default)
    {
        var email = EmailAddress.Normalise(request.Email);

        if (!PasswordRules.Acceptable(request.Password))
            return Result<PlayerAccount>.Fail(
                $"Passwords are at least {PasswordRules.MinLength} characters.", nameof(SignUpRequest.Password));

        if (request.Password != request.ConfirmPassword)
            return Result<PlayerAccount>.Fail("The two passwords do not match.", nameof(SignUpRequest.ConfirmPassword));

        if (await db.Accounts.AnyAsync(a => a.Email == email, ct))
            return Result<PlayerAccount>.Fail("That email already has an account.", nameof(SignUpRequest.Email));

        var account = new PlayerAccount
        {
            Email = email,
            PasswordHash = passwords.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Country = Blank(request.Country),
            PhoneNumber = Blank(request.PhoneNumber),
            RegisteredAt = clock.GetUtcNow().UtcDateTime,
        };

        db.Accounts.Add(account);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Two sign-ups on the same address at the same instant: the check
            // above passed for both and the unique index caught the loser. Report
            // it the way the check would have, but only once it is confirmed that
            // the address is what the insert tripped over — swallowing every
            // DbUpdateException here would hide real faults behind a friendly lie.
            db.Entry(account).State = EntityState.Detached;
            if (!await db.Accounts.AnyAsync(a => a.Email == email, ct))
            {
                log.LogError(ex, "Registration failed for reasons other than a duplicate address");
                throw;
            }

            return Result<PlayerAccount>.Fail("That email already has an account.", nameof(SignUpRequest.Email));
        }

        log.LogInformation("Registered account {AccountId}", account.Id);
        return Result<PlayerAccount>.Success(account);
    }

    public async Task<Result<PlayerAccount>> AuthenticateAsync(SignInRequest request, CancellationToken ct = default)
    {
        var email = EmailAddress.Normalise(request.Email);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == email, ct);

        var verification = passwords.Verify(account?.PasswordHash ?? DecoyHash.Value, request.Password);

        // One message for both failures. Distinguishing them would turn the sign-in
        // form into a way to enumerate who has an account here.
        if (account is null || !verification.Success)
            return Result<PlayerAccount>.Fail("That email and password do not match.");

        if (verification.NeedsRehash)
        {
            account.PasswordHash = passwords.Hash(request.Password);
            await db.SaveChangesAsync(ct);
        }

        return Result<PlayerAccount>.Success(account);
    }

    public async Task<Result<PlayerAccount>> ChangePasswordAsync(
        Guid accountId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null) return Result<PlayerAccount>.Fail("That account no longer exists.");

        // Proving current ownership is what stops a borrowed session from locking
        // the real owner out of their account.
        if (!passwords.Verify(account.PasswordHash, request.CurrentPassword).Success)
            return Result<PlayerAccount>.Fail(
                "That is not your current password.", nameof(ChangePasswordRequest.CurrentPassword));

        if (!PasswordRules.Acceptable(request.NewPassword))
            return Result<PlayerAccount>.Fail(
                $"Passwords are at least {PasswordRules.MinLength} characters.", nameof(ChangePasswordRequest.NewPassword));

        if (request.NewPassword != request.ConfirmPassword)
            return Result<PlayerAccount>.Fail(
                "The two passwords do not match.", nameof(ChangePasswordRequest.ConfirmPassword));

        account.PasswordHash = passwords.Hash(request.NewPassword);
        await db.SaveChangesAsync(ct);

        return Result<PlayerAccount>.Success(account);
    }

    public Task<PlayerAccount?> FindAsync(Guid accountId, CancellationToken ct = default) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
