using AlfarQuest.Api.Data;
using AlfarQuest.Shared.Profiles;

namespace AlfarQuest.Api.Profiles;

/// <summary>Account entity to the profile the client renders.
///
/// This projection is the boundary that keeps PasswordHash, session rows and the
/// external-provider columns off the wire. Anything not named here cannot leak,
/// which is why the API never serialises the entity itself.</summary>
public static class ProfileMappings
{
    public static PlayerProfileDto ToProfile(this PlayerAccount a) => new()
    {
        Id = a.Id,
        FullName = a.FullName,
        Email = a.Email,
        Country = a.Country,
        PhoneNumber = a.PhoneNumber,
        AvatarUrl = AvatarUrl(a),
        RegisteredAt = a.RegisteredAt,
        LastLoginAt = a.LastLoginAt,
        Stats = new PlayerStatsDto
        {
            CurrentCharacter = a.CurrentCharacter,
            HighestLevel = a.HighestLevel,
            TotalGold = a.TotalGold,
            TotalPlayTimeSeconds = a.TotalPlayTimeSeconds,
        },
        Achievements = [],
    };

    /// <summary>Null until something has been uploaded, so the UI can choose the
    /// default portrait rather than request an image that is not there.
    /// The version suffix is what makes a new upload appear at once instead of
    /// being served from the browser's cache under the same URL.</summary>
    private static string? AvatarUrl(PlayerAccount a) =>
        a.AvatarVersion is null ? null : $"api/avatars/{a.Id}?v={a.AvatarVersion}";
}
