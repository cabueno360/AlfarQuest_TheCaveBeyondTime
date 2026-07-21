using AlfarQuest.Api.Auth;
using AlfarQuest.Api.Profiles;
using AlfarQuest.Shared.Auth;
using AlfarQuest.Shared.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlfarQuest.Api.Controllers;

[ApiController]
[Route("api/avatars")]
public sealed class AvatarsController(IAvatarService avatars, ICurrentUser caller) : ControllerBase
{
    /// <summary>Uploads a new portrait for the caller. The id is never taken from
    /// the request, so this cannot be pointed at anyone else's account.</summary>
    [HttpPost]
    [Authorize]
    [RequestSizeLimit(AvatarPolicy.MaxUploadBytes + 8192)]   // +headroom for the multipart envelope
    public async Task<ActionResult<PlayerProfileDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (caller.AccountId is not { } id) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new AuthProblem("Choose an image to upload."));

        // Checked before reading, so an oversized body is refused rather than
        // buffered into memory first.
        if (file.Length > AvatarPolicy.MaxUploadBytes)
            return BadRequest(new AuthProblem("Images must be 5 MB or smaller."));

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        var saved = await avatars.ReplaceAsync(id, buffer.ToArray(), ct);
        return saved.Ok ? Ok(saved.Value) : BadRequest(new AuthProblem(saved.Error!));
    }

    [HttpDelete]
    [Authorize]
    public async Task<ActionResult<PlayerProfileDto>> Remove(CancellationToken ct)
    {
        if (caller.AccountId is not { } id) return Unauthorized();

        var removed = await avatars.RemoveAsync(id, ct);
        return removed.Ok ? Ok(removed.Value) : BadRequest(new AuthProblem(removed.Error!));
    }

    /// <summary>Serves a portrait. Anonymous by design — an avatar is shown beside
    /// a name wherever players see each other, and gating it would break the
    /// friends and leaderboard screens before they are written. Nothing private is
    /// exposed: the URL is an opaque id, and only an image ever comes back.</summary>
    [HttpGet("{playerId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid playerId, CancellationToken ct)
    {
        var avatar = await avatars.GetAsync(playerId, ct);
        if (avatar is not { } stored) return NotFound();

        // The content type is the one the server encoded, never one the uploader
        // supplied; nosniff stops a browser from second-guessing it.
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, max-age=300";
        return File(stored.Content, stored.ContentType, lastModified: null, entityTag: new(stored.ETag));
    }
}
