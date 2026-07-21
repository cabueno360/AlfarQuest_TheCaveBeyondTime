using AlfarQuest.Api.Accounts;
using AlfarQuest.Api.Auth;
using AlfarQuest.Api.Common;
using AlfarQuest.Api.Data;
using AlfarQuest.Api.Profiles;
using AlfarQuest.Shared.Auth;
using AlfarQuest.Shared.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlfarQuest.Api.Controllers;

/// <summary>Registering, signing in and signing out. Thin on purpose: it turns
/// HTTP into a service call and a Result back into a status code, and holds no
/// rules of its own.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAccountService accounts,
    ISessionService sessions,
    ILogger<AuthController> log) : ControllerBase
{
    /// <summary>Throttled, like every endpoint below that takes a credential.
    ///
    /// The limit belongs on these three and not on the controller: /me runs on
    /// every page load of every signed-in player, and putting a guessing budget
    /// in front of it would log people out for browsing.</summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<ActionResult<AuthSessionDto>> SignUp(SignUpRequest request, CancellationToken ct)
    {
        var created = await accounts.RegisterAsync(request, ct);
        if (!created.Ok) return Problem(created);

        // Registration signs the player straight in: making someone type the
        // password they just chose, twice, to reach the game teaches them nothing.
        return await StartSession(created.Value!, rememberMe: false, ct);
    }

    [HttpPost("signin")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<ActionResult<AuthSessionDto>> SignIn(SignInRequest request, CancellationToken ct)
    {
        var authenticated = await accounts.AuthenticateAsync(request, ct);
        if (!authenticated.Ok)
        {
            log.LogInformation("Failed sign-in for {Email}", EmailAddress.Normalise(request.Email));
            return Unauthorized(new AuthProblem(authenticated.Error!, authenticated.Field));
        }

        return await StartSession(authenticated.Value!, request.RememberMe, ct);
    }

    /// <summary>Revokes the session in hand. Idempotent — signing out twice, or
    /// with a token already expired, is a success, because from the player's point
    /// of view the desired state is reached either way.</summary>
    [HttpPost("signout")]
    [Authorize]
    public async Task<IActionResult> SignOutSession([FromServices] ICurrentUser caller, CancellationToken ct)
    {
        if (caller.BearerToken is { } token) await sessions.RevokeAsync(token, ct);
        return NoContent();
    }

    /// <summary>Who the caller is. The client calls this on start-up to decide
    /// whether a stored token is still good, which is the only trustworthy way to
    /// answer that — the token's own contents cannot be believed.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<PlayerProfileDto>> Me(
        [FromServices] ICurrentUser caller, [FromServices] IProfileService profiles, CancellationToken ct) =>
        caller.AccountId is { } id && await profiles.GetAsync(id, ct) is { } profile
            ? Ok(profile)
            : Unauthorized();

    /// <summary>Always answers the same, whether or not the address is registered.
    /// Anything else turns this into a way to find out who plays here.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public IActionResult ForgotPassword(ForgotPasswordRequest request)
    {
        log.LogInformation("Password reset requested for {Email}", EmailAddress.Normalise(request.Email));
        // No mail is sent yet. The endpoint exists so the client's flow is real and
        // so delivery becomes one service to implement behind this shape.
        return Accepted();
    }

    private async Task<ActionResult<AuthSessionDto>> StartSession(
        PlayerAccount account, bool rememberMe, CancellationToken ct)
    {
        var issued = await sessions.IssueAsync(account, rememberMe, Request.Headers.UserAgent, ct);
        return Ok(new AuthSessionDto
        {
            Token = issued.Token,
            ExpiresAt = issued.ExpiresAt,
            Player = account.ToProfile(),
        });
    }

    private ActionResult Problem<T>(Result<T> result) =>
        BadRequest(new AuthProblem(result.Error!, result.Field));
}
