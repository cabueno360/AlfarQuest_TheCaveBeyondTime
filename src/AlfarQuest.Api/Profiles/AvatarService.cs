using AlfarQuest.Api.Common;
using AlfarQuest.Api.Data;
using AlfarQuest.Shared.Profiles;
using Microsoft.EntityFrameworkCore;

namespace AlfarQuest.Api.Profiles;

public sealed class AvatarService(GameDbContext db, IImageProcessor images, TimeProvider clock) : IAvatarService
{
    public async Task<Result<PlayerProfileDto>> ReplaceAsync(
        Guid accountId, byte[] upload, CancellationToken ct = default)
    {
        if (upload.Length == 0)
            return Result<PlayerProfileDto>.Fail("No file was uploaded.");
        if (upload.Length > AvatarPolicy.MaxUploadBytes)
            return Result<PlayerProfileDto>.Fail("Images must be 5 MB or smaller.");

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null) return Result<PlayerProfileDto>.Fail("That account no longer exists.");

        var processed = images.ToSquareAvatar(upload, AvatarPolicy.StoredSize);
        if (!processed.Ok) return Result<PlayerProfileDto>.Fail(processed.Error!);

        var image = processed.Value;
        var now = clock.GetUtcNow().UtcDateTime;

        var existing = await db.Avatars.FirstOrDefaultAsync(a => a.PlayerAccountId == accountId, ct);
        if (existing is null)
        {
            db.Avatars.Add(new PlayerAvatar
            {
                PlayerAccountId = accountId,
                Content = image.Content,
                ContentType = image.ContentType,
                Width = image.Width,
                Height = image.Height,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.Content = image.Content;
            existing.ContentType = image.ContentType;
            existing.Width = image.Width;
            existing.Height = image.Height;
            existing.UpdatedAt = now;
        }

        // A fresh version means a fresh URL, which is what makes the new portrait
        // appear everywhere at once instead of after a hard refresh.
        account.AvatarVersion = now.Ticks.ToString("x");

        await db.SaveChangesAsync(ct);
        return Result<PlayerProfileDto>.Success(account.ToProfile());
    }

    public async Task<StoredAvatar?> GetAsync(Guid accountId, CancellationToken ct = default)
    {
        var avatar = await db.Avatars.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PlayerAccountId == accountId, ct);

        return avatar is null
            ? null
            : new StoredAvatar(avatar.Content, avatar.ContentType, $"\"{avatar.UpdatedAt.Ticks:x}\"");
    }

    public async Task<Result<PlayerProfileDto>> RemoveAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null) return Result<PlayerProfileDto>.Fail("That account no longer exists.");

        var avatar = await db.Avatars.FirstOrDefaultAsync(a => a.PlayerAccountId == accountId, ct);
        if (avatar is not null) db.Avatars.Remove(avatar);

        account.AvatarVersion = null;      // back to the default portrait
        await db.SaveChangesAsync(ct);
        return Result<PlayerProfileDto>.Success(account.ToProfile());
    }
}
