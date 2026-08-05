using AlfarQuest.Client.Game;
using AlfarQuest.Client.Models;
using AlfarQuest.Client.Services.Character;
using AlfarQuest.Shared;

namespace AlfarQuest.Client.Services.Profile;

/// <summary>Carries the party's progression to the server and back.
///
/// Progression is the thing a player would most resent losing, so this is
/// deliberately dull: it writes on every exit, reads once on entry, and does
/// nothing clever in between. WHICH save is played is the select screen's
/// decision, handed over in GameSession.SaveId — zero meaning a new game.</summary>
public sealed class CampaignSaveService(GameApiClient api, PartyState party, PlayTimeTracker playTime)
{
    /// <summary>The play time the save already carried when it was loaded — this
    /// session's stopwatch is added on top at write time, so the slot's total
    /// grows across sittings instead of being forever zero.</summary>
    private long _basePlaytime;
    /// <summary>The save being written to, once known. Null means "not loaded
    /// yet", which is why <see cref="SaveAsync"/> refuses to run before
    /// <see cref="LoadAsync"/> has: saving first would create a second save and
    /// orphan the real one.</summary>
    private int? _saveId;
    private bool _loaded;

    /// <summary>Reads the slot the select screen chose (GameSession.SaveId) and
    /// applies it to the party. A zero id is a NEW game: nothing is read, and the
    /// first exit creates a fresh save. Safe when the slot has since vanished —
    /// the player simply keeps their starting sheet.</summary>
    public async Task LoadAsync()
    {
        _loaded = true;
        _saveId = null;
        _basePlaytime = 0;

        // A zero id means a fresh delve ONLY when the select screen said so.
        // A cold arrival at /play — a refresh mid-game, a bookmark — has no
        // choice on record, and must resume the NEWEST save rather than fork a
        // brand-new campaign over the player's real one.
        if (GameSession.SaveId <= 0 && GameSession.SlotChosen) return;

        var saves = await api.MySavesAsync();
        var save = GameSession.SaveId > 0
            ? saves.FirstOrDefault(s => s.Id == GameSession.SaveId) ?? saves.FirstOrDefault()
            : saves.FirstOrDefault();           // cold boot: the newest
        if (save is null) return;               // truly a first run
        _saveId = save.Id;
        GameSession.SaveId = save.Id;
        _basePlaytime = save.PlaytimeSeconds;

        // Where to stand back up. The World constructor reads this hand-off when
        // the engine builds — region plus the very spot inside it.
        GameSession.ResumeRegion = string.IsNullOrWhiteSpace(save.Region) ? null : save.Region;
        GameSession.ResumeX = save.PosX;
        GameSession.ResumeY = save.PosY;
        if (string.IsNullOrWhiteSpace(GameSession.SaveName)) GameSession.SaveName = save.PlayerName;

        // Before the heroes: the world is built from this, and a chest that came
        // back because the claims arrived late is exactly the bug this prevents.
        party.ClaimedRewards.Clear();
        foreach (var key in save.ClaimedRewards) party.ClaimedRewards.Add(key);

        party.Containers.Clear();
        foreach (var c in save.Containers)
            party.Containers[c.Key] = new ContainerSave(
                c.Key, c.OpenedAt, c.Coin,
                [.. c.Remaining.Where(r => r.Kind == TallyKind.Material)
                    .Select(r => new MaterialStack(r.Key, r.Count))],
                [.. c.Remaining.Where(r => r.Kind == TallyKind.Item)
                    .SelectMany(r => Enumerable.Repeat(r.Key, r.Count))]);

        RestoreBelongings(save.Belongings);

        foreach (var hero in save.Party)
        {
            // A saved hero missing from the roster is recruited, not skipped. The
            // roster comes from GameSession.PartyKeys, which is a static default
            // after a refresh or a deep link straight to /play — skipping here
            // silently dropped the recruited Cleric, and the next auto-save wrote
            // the two-hero party over the real one. PartyKeys is kept in step so
            // the simulation (built after this load) fields him too.
            if (party.Find(hero.HeroKey) is not { } c)
            {
                if (Lore.Heroes.All(h => h.Key != hero.HeroKey)) continue;   // a retired key costs that hero, not the save
                party.Recruit(hero.HeroKey);
                c = party.Find(hero.HeroKey)!;
                if (!GameSession.PartyKeys.Contains(hero.HeroKey))
                    GameSession.PartyKeys = [.. GameSession.PartyKeys, hero.HeroKey];
            }

            c.RestoreProgress(
                hero.Level, hero.Xp, hero.AttributePoints, hero.SkillPoints,
                ToAttributes(hero.Spent),
                hero.Skills.Select(s => (s.SkillId, s.Rank)));

            // This hero's own pack, purse and record.
            c.RestoreInventory(hero.Inventory
                .Where(t => t.Kind == TallyKind.PackItem)
                .SelectMany(t => ItemCatalog.Find(t.Key) is { } item
                    ? Enumerable.Repeat(item, t.Count) : []));
            c.RestoreGold(hero.Purse.Select(t => (t.Key, (long)t.Count)));
            c.RestoreStats(FromStatsDto(hero.Statistics));

            // Worn gear last, so it lands on a sheet whose attributes are already
            // the saved ones — the item's bonuses fold into totals either way, but
            // restoring in this order keeps a half-applied sheet from ever existing.
            c.Gear.Clear();
            foreach (var id in hero.Equipped)
                if (ItemCatalog.Find(id) is { } item) c.Gear.Equip(item);
        }

        party.Notify();
    }

    /// <summary>Writes the party's progression — including WHERE they stood, read
    /// straight from the running engine, so continuing resumes the walk. Silent
    /// on failure: this runs as the player leaves, and an error box on the way
    /// out helps nobody — the next exit will try again.</summary>
    public async Task SaveAsync()
    {
        if (!_loaded || party.Members.Count == 0) return;

        var (region, x, y) = Game.GameEngine.ResumePoint ?? (Game.World.StartRegion, 0f, 0f);
        var save = await api.SaveAsync(new SaveGameDto
        {
            Id = _saveId ?? 0,
            PlayerName = GameSession.SaveName is { Length: > 0 } name ? name : (party.Selected?.Name ?? ""),
            ActiveHeroKey = party.Selected?.Key ?? "mage",
            Region = region,
            PosX = x,
            PosY = y,
            // The slot's lifetime play time: what it already carried, plus this
            // sitting's stopwatch. Written every exit, so it survives crashes
            // up to the last save.
            PlaytimeSeconds = _basePlaytime + (long)playTime.Elapsed.TotalSeconds,
            Party = [.. party.Members.Select(ToDto)],
            ClaimedRewards = [.. party.ClaimedRewards],
            Containers = [.. party.Containers.Values.Select(ToDto)],
            Belongings = [.. Belongings()],
        });

        // Remember the id the server assigned, so the next exit updates this save
        // instead of creating another — and so the slot picker highlights it.
        if (save is not null) { _saveId = save.Id; GameSession.SaveId = save.Id; }
    }

    /// <summary>Replaces what the party is carrying.
    ///
    /// Replaces rather than adds: PartyState hands a new party its starting kit
    /// and purse before this runs, so adding would leave a returning player with
    /// two sets of boots and an extra 120 gold every time they came back.</summary>
    private void RestoreBelongings(IReadOnlyList<SaveTallyDto> tallies)
    {
        // Only the shared things live at the party level now: the material pouch,
        // the shared stash, and — if that mode is on — the shared gold pool. Each
        // hero's own purse and pack are restored per hero, above.
        party.Pouch.Clear();
        party.Stash.Clear();
        party.SharedPurse.Clear();

        foreach (var t in tallies)
        {
            switch (t.Kind)
            {
                case TallyKind.Currency:
                    party.SharedPurse.Set(t.Key, t.Count);
                    break;

                case TallyKind.Material:
                    party.Pouch.Add(t.Key, t.Count);
                    break;

                case TallyKind.PackItem:
                    // The shared stash. An id the game no longer defines is skipped,
                    // not fatal: a retired item should cost that item, not the save.
                    if (ItemCatalog.Find(t.Key) is { } item)
                        for (var i = 0; i < t.Count; i++) party.Stash.Add(item);
                    break;
            }
        }
    }

    /// <summary>The shared belongings, flattened to keyed counts: the material
    /// pouch, the shared stash, and the shared gold pool when that mode is on.
    /// Each hero's own purse and pack are written per hero, not here.</summary>
    private IEnumerable<SaveTallyDto> Belongings()
    {
        foreach (var (material, count) in party.Pouch.Held())
            yield return new SaveTallyDto { Kind = TallyKind.Material, Key = material.Id, Count = count };

        foreach (var group in party.Stash.Items.GroupBy(i => i.Id))
            yield return new SaveTallyDto { Kind = TallyKind.PackItem, Key = group.Key, Count = group.Count() };

        if (party.SharedGold)
            foreach (var (currency, amount) in party.SharedPurse.Held())
                yield return new SaveTallyDto { Kind = TallyKind.Currency, Key = currency.Key, Count = (int)amount };
    }

    /// <summary>A container's state, with its remainder flattened to keyed
    /// counts. Items are grouped rather than listed one per row: a chest with
    /// three of the same ring is one row saying three.</summary>
    private static SavedContainerDto ToDto(ContainerSave c) => new()
    {
        Key = c.Key,
        OpenedAt = c.OpenedAt,
        Coin = c.Coin,
        Remaining =
        [
            .. c.Materials.Select(m => new SaveTallyDto
            {
                Kind = TallyKind.Material, Key = m.Id, Count = m.Count,
            }),
            .. c.Items.GroupBy(id => id).Select(g => new SaveTallyDto
            {
                Kind = TallyKind.Item, Key = g.Key, Count = g.Count(),
            }),
        ],
    };

    private static SaveHeroDto ToDto(Models.Character c) => new()
    {
        HeroKey = c.Key,
        Level = c.Level,
        Xp = c.Xp,
        Recruited = true,
        AttributePoints = c.AttributePoints,
        SkillPoints = c.SkillPoints,
        Spent = new SpentAttributesDto
        {
            Strength = c.Spent.Strength,
            Dexterity = c.Spent.Dexterity,
            Agility = c.Spent.Agility,
            Vitality = c.Spent.Vitality,
            Intelligence = c.Spent.Intelligence,
            Wisdom = c.Spent.Wisdom,
            Defense = c.Spent.Defense,
            Luck = c.Spent.Luck,
        },
        Skills = [.. c.Skills.Learned().Select(s => new SavedSkillDto { SkillId = s.SkillId, Rank = s.Rank })],
        Equipped = [.. c.Gear.Worn.Select(i => i.Id)],
        // The hero's own pack, purse and record — the individual half of the save.
        Inventory =
        [
            .. c.Bag.Items.GroupBy(i => i.Id).Select(g => new SaveTallyDto
            {
                Kind = TallyKind.PackItem, Key = g.Key, Count = g.Count(),
            }),
        ],
        Purse =
        [
            .. c.Purse.Held().Select(h => new SaveTallyDto
            {
                Kind = TallyKind.Currency, Key = h.Currency.Key, Count = (int)h.Amount,
            }),
        ],
        Statistics = ToStatsDto(c.Stats),
    };

    private static HeroStatsDto ToStatsDto(HeroStats s) => new()
    {
        EnemiesDefeated = s.EnemiesDefeated,
        BossesDefeated = s.BossesDefeated,
        Deaths = s.Deaths,
        DamageDealt = s.DamageDealt,
        DamageTaken = s.DamageTaken,
        TreasuresOpened = s.TreasuresOpened,
        ItemsCollected = s.ItemsCollected,
        GoldEarned = s.GoldEarned,
        GoldSpent = s.GoldSpent,
        DistanceWalked = s.DistanceWalked,
        PlaySeconds = s.PlaySeconds,
    };

    private static HeroStats FromStatsDto(HeroStatsDto d) => new()
    {
        EnemiesDefeated = d.EnemiesDefeated,
        BossesDefeated = d.BossesDefeated,
        Deaths = d.Deaths,
        DamageDealt = d.DamageDealt,
        DamageTaken = d.DamageTaken,
        TreasuresOpened = d.TreasuresOpened,
        ItemsCollected = d.ItemsCollected,
        GoldEarned = d.GoldEarned,
        GoldSpent = d.GoldSpent,
        DistanceWalked = d.DistanceWalked,
        PlaySeconds = d.PlaySeconds,
    };

    private static Attributes ToAttributes(SpentAttributesDto s) => new()
    {
        Strength = s.Strength,
        Dexterity = s.Dexterity,
        Agility = s.Agility,
        Vitality = s.Vitality,
        Intelligence = s.Intelligence,
        Wisdom = s.Wisdom,
        Defense = s.Defense,
        Luck = s.Luck,
    };
}
