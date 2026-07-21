using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace AlfarQuest.Client.Services.Auth;

/// <summary>Translates our session into the identity Blazor's routing understands,
/// so &lt;AuthorizeView&gt; and [Authorize] work off the same truth the HTTP layer does.
///
/// Nothing here decides anything. The server decided when it issued the token;
/// this only reports what was decided. That distinction matters: a WebAssembly
/// client is fully rewritable by whoever is running it, so this state controls
/// what is *shown*, never what is *permitted*. Every route it guards is backed by
/// an endpoint that checks the token again.</summary>
public sealed class AlfarQuestAuthenticationStateProvider(UserSessionService session)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (session.Player is not { } player) return Task.FromResult(Anonymous);

        // The authentication type being non-null is what makes IsAuthenticated
        // true; an identity built without one is anonymous however many claims it
        // carries.
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new Claim(ClaimTypes.Name, player.FullName),
            new Claim(ClaimTypes.Email, player.Email),
        ], authenticationType: "AlfarSession");

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
