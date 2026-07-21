using AlfarQuest.Api.Common;
using AlfarQuest.Shared.Profiles;

namespace AlfarQuest.Api.Profiles;

/// <summary>Reading and editing a profile. Every method takes the account id from
/// the caller's own token — there is no overload that lets one player name
/// another, so "can I edit this profile" has no way to be answered wrongly.</summary>
public interface IProfileService
{
    Task<PlayerProfileDto?> GetAsync(Guid accountId, CancellationToken ct = default);

    Task<Result<PlayerProfileDto>> UpdateAsync(Guid accountId, UpdateProfileRequest request, CancellationToken ct = default);

    /// <summary>Records what the last session achieved. Values only ever move
    /// forward, so a stale or replayed report cannot erase real progress.</summary>
    Task<Result<PlayerProfileDto>> RecordStatsAsync(Guid accountId, PlayerStatsDto stats, CancellationToken ct = default);
}
