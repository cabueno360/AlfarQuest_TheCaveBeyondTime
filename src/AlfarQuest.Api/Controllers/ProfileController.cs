using AlfarQuest.Api.Accounts;
using AlfarQuest.Api.Auth;
using AlfarQuest.Api.Profiles;
using AlfarQuest.Shared.Auth;
using AlfarQuest.Shared.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlfarQuest.Api.Controllers;

/// <summary>The signed-in player's own profile.
///
/// [Authorize] on the class, and every action working from
/// <see cref="ICurrentUser.AccountId"/> rather than a route parameter. There is
/// no /api/profile/{id}, so there is no id to tamper with.</summary>
[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController(
    IProfileService profiles,
    IAccountService accounts,
    ISessionService sessions,
    ICurrentUser caller) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlayerProfileDto>> Get(CancellationToken ct) =>
        caller.AccountId is { } id && await profiles.GetAsync(id, ct) is { } profile
            ? Ok(profile)
            : Unauthorized();

    [HttpPut]
    public async Task<ActionResult<PlayerProfileDto>> Update(UpdateProfileRequest request, CancellationToken ct)
    {
        if (caller.AccountId is not { } id) return Unauthorized();

        var updated = await profiles.UpdateAsync(id, request, ct);
        return updated.Ok
            ? Ok(updated.Value)
            : BadRequest(new AuthProblem(updated.Error!, updated.Field));
    }

    /// <summary>Progress reported by the client at the end of a session.</summary>
    [HttpPost("stats")]
    public async Task<ActionResult<PlayerProfileDto>> RecordStats(PlayerStatsDto stats, CancellationToken ct)
    {
        if (caller.AccountId is not { } id) return Unauthorized();

        var updated = await profiles.RecordStatsAsync(id, stats, ct);
        return updated.Ok ? Ok(updated.Value) : BadRequest(new AuthProblem(updated.Error!, updated.Field));
    }

    /// <summary>Changing a password signs every other device out. Someone changing
    /// theirs usually suspects it is known, and leaving those sessions alive would
    /// defeat the point of changing it.</summary>
    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        if (caller.AccountId is not { } id) return Unauthorized();

        var changed = await accounts.ChangePasswordAsync(id, request, ct);
        if (!changed.Ok) return BadRequest(new AuthProblem(changed.Error!, changed.Field));

        await sessions.RevokeAllAsync(id, ct);

        // ...including this one, so the client re-authenticates with the new
        // password rather than coasting on a token minted under the old one.
        return NoContent();
    }
}
