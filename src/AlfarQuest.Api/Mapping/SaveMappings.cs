using AlfarQuest.Api.Data;
using AlfarQuest.Shared;

namespace AlfarQuest.Api.Mapping;

/// <summary>Entity to wire-contract projections, kept out of both the controller
/// and the service so neither has to know the other's shape.</summary>
public static class SaveMappings
{
    public static SaveGameDto ToDto(this PlayerSave s) => new()
    {
        Id = s.Id,
        PlayerName = s.PlayerName,
        ActiveHeroKey = s.ActiveHeroKey,
        Region = s.Region,
        PosX = s.PosX,
        PosY = s.PosY,
        PlaytimeSeconds = s.PlaytimeSeconds,
        UpdatedAt = s.UpdatedAt,
        Party = [.. s.Party.Select(ToDto)],
        ClaimedRewards = [.. s.Claims.Select(c => c.RewardKey)],
        Belongings = [.. s.Belongings.Select(t => new SaveTallyDto
        {
            Kind = t.Kind, Key = t.TallyKey, Count = t.Count,
        })],
        Containers = [.. s.Containers.Select(c => new SavedContainerDto
        {
            Key = c.ContainerKey,
            OpenedAt = c.OpenedAt,
            Coin = c.Coin,
            Remaining = [.. c.Remaining.Select(i => new SaveTallyDto
            {
                Kind = i.Kind, Key = i.TallyKey, Count = i.Count,
            })],
        })],
    };

    public static SaveHeroDto ToDto(this SaveHero p) => new()
    {
        HeroKey = p.HeroKey, Level = p.Level, Xp = p.Xp, Recruited = p.Recruited,
        AttributePoints = p.AttributePoints, SkillPoints = p.SkillPoints,
        Spent = new SpentAttributesDto
        {
            Strength = p.SpentStrength,
            Dexterity = p.SpentDexterity,
            Agility = p.SpentAgility,
            Vitality = p.SpentVitality,
            Intelligence = p.SpentIntelligence,
            Wisdom = p.SpentWisdom,
            Defense = p.SpentDefense,
            Luck = p.SpentLuck,
        },
        Skills = [.. p.Skills.Select(s => new SavedSkillDto { SkillId = s.SkillId, Rank = s.Rank })],
        Equipped = [.. p.Equipped.Select(e => e.ItemId)],
        // The hero's own pack and purse are one child table split by kind on the
        // way out — pack items and coin, sharing the tally shape.
        Inventory = [.. p.Belongings.Where(t => t.Kind == TallyKind.PackItem)
            .Select(t => new SaveTallyDto { Kind = t.Kind, Key = t.TallyKey, Count = t.Count })],
        Purse = [.. p.Belongings.Where(t => t.Kind == TallyKind.Currency)
            .Select(t => new SaveTallyDto { Kind = t.Kind, Key = t.TallyKey, Count = t.Count })],
        Statistics = new HeroStatsDto
        {
            EnemiesDefeated = p.StatEnemiesDefeated,
            BossesDefeated = p.StatBossesDefeated,
            Deaths = p.StatDeaths,
            DamageDealt = p.StatDamageDealt,
            DamageTaken = p.StatDamageTaken,
            TreasuresOpened = p.StatTreasuresOpened,
            ItemsCollected = p.StatItemsCollected,
            GoldEarned = p.StatGoldEarned,
            DistanceWalked = p.StatDistanceWalked,
            PlaySeconds = p.StatPlaySeconds,
        },
    };

    public static SaveHero ToEntity(this SaveHeroDto p) => new()
    {
        HeroKey = p.HeroKey, Level = p.Level, Xp = p.Xp, Recruited = p.Recruited,
        AttributePoints = p.AttributePoints, SkillPoints = p.SkillPoints,
        SpentStrength = p.Spent.Strength,
        SpentDexterity = p.Spent.Dexterity,
        SpentAgility = p.Spent.Agility,
        SpentVitality = p.Spent.Vitality,
        SpentIntelligence = p.Spent.Intelligence,
        SpentWisdom = p.Spent.Wisdom,
        SpentDefense = p.Spent.Defense,
        SpentLuck = p.Spent.Luck,
        StatEnemiesDefeated = p.Statistics.EnemiesDefeated,
        StatBossesDefeated = p.Statistics.BossesDefeated,
        StatDeaths = p.Statistics.Deaths,
        StatDamageDealt = p.Statistics.DamageDealt,
        StatDamageTaken = p.Statistics.DamageTaken,
        StatTreasuresOpened = p.Statistics.TreasuresOpened,
        StatItemsCollected = p.Statistics.ItemsCollected,
        StatGoldEarned = p.Statistics.GoldEarned,
        StatDistanceWalked = p.Statistics.DistanceWalked,
        StatPlaySeconds = p.Statistics.PlaySeconds,
        Skills = [.. p.Skills.Select(s => new SaveSkill { SkillId = s.SkillId, Rank = s.Rank })],
        Equipped = [.. p.Equipped.Select(id => new SaveEquipment { ItemId = id })],
        // Pack and purse folded back into one child table, tagged by kind.
        Belongings =
        [
            .. p.Inventory.Select(t => new SaveHeroTally { Kind = TallyKind.PackItem, TallyKey = t.Key, Count = t.Count }),
            .. p.Purse.Select(t => new SaveHeroTally { Kind = TallyKind.Currency, TallyKey = t.Key, Count = t.Count }),
        ],
    };

    public static HeroDto ToDto(this HeroEntity h) => new()
    {
        Key = h.Key, Name = h.Name, Title = h.Title, HeroClass = h.HeroClass,
        Description = h.Description, BaseHp = h.BaseHp, UnlockedByDefault = h.UnlockedByDefault,
    };
}
