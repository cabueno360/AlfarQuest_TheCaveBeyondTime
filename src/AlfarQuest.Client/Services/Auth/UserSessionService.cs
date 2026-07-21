using AlfarQuest.Shared.Profiles;

namespace AlfarQuest.Client.Services.Auth;

/// <summary>The signed-in player, as the running app knows them: the token every
/// request carries, and the profile every screen reads.
///
/// One object holds this so no component keeps its own copy — changing an avatar
/// or a display name updates the header, the dropdown and the profile page from
/// the single <see cref="Changed"/> that follows.</summary>
public sealed class UserSessionService(TokenStore tokens)
{
    public string? Token { get; private set; }
    public PlayerProfileDto? Player { get; private set; }

    public bool IsAuthenticated => Token is not null && Player is not null;

    public event Action? Changed;

    public async Task BeginAsync(string token, PlayerProfileDto player, bool persistent)
    {
        Token = token;
        Player = player;
        await tokens.WriteAsync(token, persistent);
        Changed?.Invoke();
    }

    /// <summary>Adopts a token already in storage once the server has confirmed
    /// whose it is. Does not rewrite storage: the choice the player made about
    /// persistence belongs to the sign-in that created it.</summary>
    public void Restore(string token, PlayerProfileDto player)
    {
        Token = token;
        Player = player;
        Changed?.Invoke();
    }

    /// <summary>A fresh profile for the same session — after an edit or an avatar
    /// upload. Ignored when signed out, so a response arriving after sign-out
    /// cannot resurrect a session.</summary>
    public void UpdatePlayer(PlayerProfileDto player)
    {
        if (Token is null) return;
        Player = player;
        Changed?.Invoke();
    }

    public async Task EndAsync()
    {
        Token = null;
        Player = null;
        await tokens.ClearAsync();
        Changed?.Invoke();
    }
}
