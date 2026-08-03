using AlfarQuest.Api.Data;
using AlfarQuest.Api.Mapping;
using AlfarQuest.Shared;
using Microsoft.EntityFrameworkCore;

namespace AlfarQuest.Api.Services;

/// <summary>Persistence for player saves. Lives outside the controller so the
/// upsert rules can be read — and tested — without an HTTP context.
///
/// Every method takes the owning account and filters on it. Ownership is a
/// parameter rather than something checked afterwards, so a query that forgot to
/// scope itself would not compile.</summary>
public sealed class SaveService(GameDbContext db)
{
    public async Task<SaveGameDto?> FindAsync(int id, Guid owner, CancellationToken ct = default) =>
        await db.Saves
            .Include(s => s.Party).ThenInclude(h => h.Skills)
            .Include(s => s.Party).ThenInclude(h => h.Equipped)
            .Include(s => s.Party).ThenInclude(h => h.Belongings)
            .Include(s => s.Claims)
            .Include(s => s.Belongings)
            .Include(s => s.Containers).ThenInclude(c => c.Remaining)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.PlayerAccountId == owner, ct) is { } save
            ? save.ToDto()
            : null;

    public async Task<IReadOnlyList<SaveGameDto>> ForOwnerAsync(Guid owner, CancellationToken ct = default) =>
        [.. (await db.Saves
                .Include(s => s.Party).ThenInclude(h => h.Skills)
            .Include(s => s.Party).ThenInclude(h => h.Equipped)
            .Include(s => s.Party).ThenInclude(h => h.Belongings)
            .Include(s => s.Claims)
            .Include(s => s.Belongings)
            .Include(s => s.Containers).ThenInclude(c => c.Remaining)
                .AsNoTracking()
                .Where(s => s.PlayerAccountId == owner)
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync(ct))
            .Select(SaveMappings.ToDto)];

    /// <summary>Creates a save, or replaces the contents of an existing one when
    /// <paramref name="dto"/> carries an id. Returns null if that id is unknown
    /// <em>or belongs to someone else</em> — the caller turns both into a 404, so
    /// the two are indistinguishable from outside.</summary>
    public async Task<SaveGameDto?> UpsertAsync(SaveGameDto dto, Guid owner, CancellationToken ct = default)
    {
        var save = dto.Id > 0 ? await LoadForUpdateAsync(dto.Id, owner, ct) : NewSave(owner);
        if (save is null) return null;

        save.PlayerName = dto.PlayerName;
        save.ActiveHeroKey = dto.ActiveHeroKey;
        save.Region = dto.Region;
        save.PosX = dto.PosX;
        save.PosY = dto.PosY;
        save.PlaytimeSeconds = dto.PlaytimeSeconds;
        save.UpdatedAt = DateTime.UtcNow;
        // The party is replaced wholesale rather than diffed: a save is a
        // snapshot, so reconciling row by row would add cost and no meaning.
        save.Party = [.. dto.Party.Select(SaveMappings.ToEntity)];
        // Distinct: the client sends a set, but a malformed request must not be
        // able to grow this table without bound.
        save.Claims = [.. dto.ClaimedRewards.Distinct().Take(2000)
            .Select(k => new SaveClaim { RewardKey = k })];

        // Bounded for the same reason, and non-negative: these arrive from a
        // browser, so they are a claim about the party's belongings rather than a
        // fact about them.
        // Bounded and sanitised for the same reason as everything else here: it
        // arrives from a browser, so it is a claim about the world rather than a
        // fact about it.
        save.Containers = [.. dto.Containers
            .Where(c => c.Key.Length is > 0 and <= 64)
            .Take(500)
            .Select(c => new SaveContainer
            {
                ContainerKey = c.Key,
                OpenedAt = c.OpenedAt,
                Coin = Math.Max(0, c.Coin),
                Remaining = [.. c.Remaining
                    .Where(i => i.Count > 0 && i.Key.Length is > 0 and <= 40)
                    .Take(40)
                    .Select(i => new SaveContainerItem { Kind = i.Kind, TallyKey = i.Key, Count = i.Count })],
            })];

        save.Belongings = [.. dto.Belongings
            .Where(t => t.Count > 0 && t.Key.Length is > 0 and <= 40)
            .Take(500)
            .Select(t => new SaveTally { Kind = t.Kind, TallyKey = t.Key, Count = t.Count })];

        await db.SaveChangesAsync(ct);
        return save.ToDto();
    }

    private async Task<PlayerSave?> LoadForUpdateAsync(int id, Guid owner, CancellationToken ct)
    {
        var save = await db.Saves.Include(s => s.Party).ThenInclude(h => h.Skills)
            .Include(s => s.Party).ThenInclude(h => h.Equipped)
            .Include(s => s.Party).ThenInclude(h => h.Belongings)
            .Include(s => s.Claims)
            .Include(s => s.Belongings)
            .Include(s => s.Containers).ThenInclude(c => c.Remaining)
            .FirstOrDefaultAsync(s => s.Id == id && s.PlayerAccountId == owner, ct);
        if (save is null) return null;

        // Skills first: they hang off the hero rows about to be removed, and an
        // orphaned child row is a foreign-key error at SaveChanges.
        // Children before parents: an orphaned child row is a foreign-key error
        // at SaveChanges, and the grandchildren of the hero rows go first of all.
        db.SaveSkills.RemoveRange(save.Party.SelectMany(h => h.Skills));
        db.SaveEquipment.RemoveRange(save.Party.SelectMany(h => h.Equipped));
        db.SaveHeroTallies.RemoveRange(save.Party.SelectMany(h => h.Belongings));
        db.SaveHeroes.RemoveRange(save.Party);
        db.SaveClaims.RemoveRange(save.Claims);
        db.SaveTallies.RemoveRange(save.Belongings);
        db.SaveContainerItems.RemoveRange(save.Containers.SelectMany(c => c.Remaining));
        db.SaveContainers.RemoveRange(save.Containers);
        save.Party.Clear();
        save.Claims.Clear();
        save.Belongings.Clear();
        save.Containers.Clear();
        return save;
    }

    /// <summary>Removes a save and everything hanging off it. True when it was
    /// the caller's and is gone; false for unknown ids and other people's — the
    /// controller turns both into the same 404.</summary>
    public async Task<bool> DeleteAsync(int id, Guid owner, CancellationToken ct = default)
    {
        // LoadForUpdate already strips the children in FK-safe order.
        var save = await LoadForUpdateAsync(id, owner, ct);
        if (save is null) return false;
        db.Saves.Remove(save);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private PlayerSave NewSave(Guid owner)
    {
        var save = new PlayerSave { PlayerAccountId = owner };
        db.Saves.Add(save);
        return save;
    }
}
