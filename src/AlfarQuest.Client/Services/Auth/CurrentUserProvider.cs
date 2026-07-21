using AlfarQuest.Shared.Profiles;

namespace AlfarQuest.Client.Services.Auth;

/// <summary>Read-only access to the signed-in player, for components that want to
/// show their name or portrait and have no business changing anything.
///
/// A separate, narrower face on <see cref="UserSessionService"/>: a component that
/// takes this cannot end a session by accident, and its dependencies say plainly
/// that it only reads.</summary>
public sealed class CurrentUserProvider(UserSessionService session)
{
    public PlayerProfileDto? Player => session.Player;

    public bool IsAuthenticated => session.IsAuthenticated;

    /// <summary>What to call someone before their profile has loaded, or when they
    /// signed up without giving a name.</summary>
    public string DisplayName => session.Player?.FullName is { Length: > 0 } name ? name : "Delver";

    /// <summary>The first letters of the name, for the portrait's fallback. Two
    /// words at most: initials past that stop being recognisable.</summary>
    public string Initials
    {
        get
        {
            var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant(),
            };
        }
    }

    public event Action? Changed
    {
        add => session.Changed += value;
        remove => session.Changed -= value;
    }
}
