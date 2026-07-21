using AlfarQuest.Api.Data;
using AlfarQuest.Api.Mapping;
using AlfarQuest.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlfarQuest.Api.Controllers;

/// <summary>The roster the hero-select screen reads. Authenticated along with the
/// rest of the game data: the screen behind it is, so leaving its data open would
/// be a door beside a locked one.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class HeroesController(GameDbContext db) : ControllerBase
{
    /// <summary>The roster, seeded from the novel.</summary>
    // Materialised before mapping: EF cannot translate the ToDto extension into
    // SQL. HeroEntity has exactly the columns the DTO carries, so this reads the
    // same data the hand-written projection did.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HeroDto>>> Get(CancellationToken ct) =>
        Ok(await db.Heroes.AsNoTracking().ToListAsync(ct) is { } rows
            ? rows.Select(SaveMappings.ToDto).ToList()
            : []);
}
