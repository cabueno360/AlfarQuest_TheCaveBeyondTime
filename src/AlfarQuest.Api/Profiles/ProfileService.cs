using AlfarQuest.Api.Common;
using AlfarQuest.Api.Data;
using AlfarQuest.Shared.Profiles;
using Microsoft.EntityFrameworkCore;

namespace AlfarQuest.Api.Profiles;

public sealed class ProfileService(GameDbContext db) : IProfileService
{
    public async Task<PlayerProfileDto?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct) is { } account
            ? account.ToProfile()
            : null;

    public async Task<Result<PlayerProfileDto>> UpdateAsync(
        Guid accountId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null) return Result<PlayerProfileDto>.Fail("That account no longer exists.");

        var name = (request.FullName ?? "").Trim();
        if (name.Length < 2)
            return Result<PlayerProfileDto>.Fail("A name is at least 2 characters.", nameof(request.FullName));

        // Email is deliberately not assignable here. Until verification exists,
        // no code path may change it.
        account.FullName = name;
        account.Country = Blank(request.Country);
        account.PhoneNumber = Blank(request.PhoneNumber);

        await db.SaveChangesAsync(ct);
        return Result<PlayerProfileDto>.Success(account.ToProfile());
    }

    public async Task<Result<PlayerProfileDto>> RecordStatsAsync(
        Guid accountId, PlayerStatsDto stats, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null) return Result<PlayerProfileDto>.Fail("That account no longer exists.");

        // Monotonic on purpose. These arrive from a browser, so they are a claim,
        // not a fact — taking the max means the worst a forged low value can do is
        // nothing, and playtime accumulates rather than being overwritten by
        // whichever tab closed last.
        account.HighestLevel = Math.Max(account.HighestLevel, Math.Max(0, stats.HighestLevel));
        account.TotalGold = Math.Max(account.TotalGold, Math.Max(0, stats.TotalGold));
        account.TotalPlayTimeSeconds += Math.Clamp(stats.TotalPlayTimeSeconds, 0, 86_400);

        if (!string.IsNullOrWhiteSpace(stats.CurrentCharacter))
            account.CurrentCharacter = stats.CurrentCharacter.Trim()[..Math.Min(stats.CurrentCharacter.Trim().Length, 40)];

        await db.SaveChangesAsync(ct);
        return Result<PlayerProfileDto>.Success(account.ToProfile());
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
