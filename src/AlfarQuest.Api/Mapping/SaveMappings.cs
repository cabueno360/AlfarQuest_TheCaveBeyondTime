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
        PlaytimeSeconds = s.PlaytimeSeconds,
        UpdatedAt = s.UpdatedAt,
        Party = [.. s.Party.Select(ToDto)],
        ClaimedRewards = [.. s.Claims.Select(c => c.RewardKey)],
        Belongings = [.. s.Belongings.Select(t => new SaveTallyDto
        {
            Kind = t.Kind, Key = t.TallyKey, Count = t.Count,
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
        Skills = [.. p.Skills.Select(s => new SaveSkill { SkillId = s.SkillId, Rank = s.Rank })],
        Equipped = [.. p.Equipped.Select(id => new SaveEquipment { ItemId = id })],
    };

    public static HeroDto ToDto(this HeroEntity h) => new()
    {
        Key = h.Key, Name = h.Name, Title = h.Title, HeroClass = h.HeroClass,
        Description = h.Description, BaseHp = h.BaseHp, UnlockedByDefault = h.UnlockedByDefault,
    };
}
