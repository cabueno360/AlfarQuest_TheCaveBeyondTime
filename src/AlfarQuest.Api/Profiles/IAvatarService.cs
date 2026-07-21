using AlfarQuest.Api.Common;
using AlfarQuest.Shared.Profiles;

namespace AlfarQuest.Api.Profiles;

public interface IAvatarService
{
    Task<Result<PlayerProfileDto>> ReplaceAsync(Guid accountId, byte[] upload, CancellationToken ct = default);

    Task<StoredAvatar?> GetAsync(Guid accountId, CancellationToken ct = default);

    Task<Result<PlayerProfileDto>> RemoveAsync(Guid accountId, CancellationToken ct = default);
}

public readonly record struct StoredAvatar(byte[] Content, string ContentType, string ETag);
