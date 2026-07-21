namespace AlfarQuest.Client.Services.Auth;

/// <summary>Restores a session from storage on start-up, and tears one down when
/// the server stops honouring it.
///
/// Nothing is rendered until <see cref="RestoreAsync"/> has finished, so the app
/// never briefly shows a signed-out shell to someone who is signed in — and, more
/// importantly, never briefly shows game screens to someone who is not.</summary>
public sealed class SessionBootstrapper(
    TokenStore tokens,
    AuthenticationApi api,
    UserSessionService session,
    AlfarQuestAuthenticationStateProvider state,
    AuthTokenHandler pipeline)
{
    public bool Ready { get; private set; }

    /// <summary>Raised when the server rejects the session mid-use — expired,
    /// revoked, or signed out elsewhere. The router listens and shows sign-in.</summary>
    public event Action? SessionEnded;

    public async Task RestoreAsync()
    {
        // Registered before the first request goes out, so a session that dies at
        // any point is handled the same way it is handled here.
        pipeline.SessionRejected += HandleRejectionAsync;

        if (await tokens.ReadAsync() is { } token)
        {
            // Adopt the token only for the length of this check. It has to be on
            // the session for the handler to attach it, and it is dropped again
            // the moment the server declines to recognise it.
            session.Restore(token, Placeholder);

            if (await api.MeAsync() is { } player) session.Restore(token, player);
            else await session.EndAsync();
        }

        state.NotifyChanged();
        Ready = true;
    }

    private async Task HandleRejectionAsync()
    {
        if (!session.IsAuthenticated) return;
        await session.EndAsync();
        state.NotifyChanged();
        SessionEnded?.Invoke();
    }

    /// <summary>Stands in for the real profile for the one call that fetches it.
    /// Never reaches a screen: rendering is still blocked at this point.</summary>
    private static Shared.Profiles.PlayerProfileDto Placeholder { get; } = new();
}
