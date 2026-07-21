using System.Security.Claims;
using System.Text.Encodings.Web;
using AlfarQuest.Api.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AlfarQuest.Api.Auth;

/// <summary>Turns an `Authorization: Bearer &lt;token&gt;` header into a principal.
///
/// This is the only place a request becomes "someone". Everything downstream —
/// [Authorize], ICurrentUser, every controller — reads what this produces, so
/// there is exactly one implementation of "is this caller who they say they are"
/// to get right.</summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionService sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header)) return AuthenticateResult.NoResult();

        var raw = header.ToString();
        if (!raw.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

        var token = raw[BearerPrefix.Length..].Trim();
        if (token.Length == 0) return AuthenticateResult.NoResult();

        // Resolve rather than merely validate: expiry and revocation are checked
        // against the row on every request, which is what makes sign-out real.
        var account = await sessions.ResolveAsync(token, Context.RequestAborted);
        if (account is null) return AuthenticateResult.Fail("The session is not valid.");

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Email, account.Email),
            new Claim(ClaimTypes.Name, account.FullName),
        ], AuthSchemes.Session);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), AuthSchemes.Session));
    }

    /// <summary>401 with a bare WWW-Authenticate. No redirect: this API serves a
    /// WebAssembly client, and a 302 to a login page would arrive at fetch() as a
    /// confusing success.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }
}
