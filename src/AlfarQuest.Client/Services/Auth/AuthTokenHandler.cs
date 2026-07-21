using System.Net;
using System.Net.Http.Headers;

namespace AlfarQuest.Client.Services.Auth;

/// <summary>Attaches the bearer token to every outgoing request, and reacts when
/// the server says it is no longer good.
///
/// Doing it here rather than at each call site means no future service can forget
/// to authenticate — and no future service can forget to handle a session that
/// expired mid-play. There is one implementation of both, and it sits on the pipe.</summary>
public sealed class AuthTokenHandler(UserSessionService session) : DelegatingHandler
{
    /// <summary>Raised when a request comes back 401 with a token attached: the
    /// session ended somewhere else — expiry, a sign-out on another tab, a
    /// password change. The app listens and returns the player to sign-in.</summary>
    public event Func<Task>? SessionRejected;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var token = session.Token;
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);

        // Only when a token was actually presented. A 401 on an anonymous call is
        // the endpoint declining, not a session dying, and tearing the session
        // down for it would sign people out for visiting the wrong page.
        if (response.StatusCode == HttpStatusCode.Unauthorized && token is not null && SessionRejected is not null)
            await SessionRejected.Invoke();

        return response;
    }
}
