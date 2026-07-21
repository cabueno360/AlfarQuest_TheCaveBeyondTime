using AlfarQuest.Api.Auth;
using AlfarQuest.Api.Services;
using AlfarQuest.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlfarQuest.Api.Controllers;

/// <summary>A player's campaign progress. Authenticated, and every call scoped to
/// the caller's own account — the save id is sequential, so without that scoping
/// reading someone else's game would be a matter of counting.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SavesController(SaveService saves, ICurrentUser caller) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SaveGameDto>> Get(int id, CancellationToken ct)
    {
        if (caller.AccountId is not { } owner) return Unauthorized();

        // Not-found rather than forbidden for someone else's save: telling an
        // enumerator which ids exist is most of what they wanted to learn.
        return await saves.FindAsync(id, owner, ct) is { } save ? Ok(save) : NotFound();
    }

    /// <summary>The caller's own saves, so the client never has to guess an id.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<SaveGameDto>>> Mine(CancellationToken ct) =>
        caller.AccountId is { } owner ? Ok(await saves.ForOwnerAsync(owner, ct)) : Unauthorized();

    /// <summary>Create a save, or update the existing one when an Id is supplied.</summary>
    [HttpPost]
    public async Task<ActionResult<SaveGameDto>> Upsert(SaveGameDto dto, CancellationToken ct)
    {
        if (caller.AccountId is not { } owner) return Unauthorized();
        return await saves.UpsertAsync(dto, owner, ct) is { } save ? Ok(save) : NotFound();
    }
}
