namespace AlfarQuest.Api.Data;

public class HeroEntity
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string HeroClass { get; set; } = "";
    public string Description { get; set; } = "";
    public int BaseHp { get; set; }
    public bool UnlockedByDefault { get; set; }
}

public class PlayerSave
{
    public int Id { get; set; }

    /// <summary>Who the save belongs to. Every query filters on it, which is what
    /// turns the sequential Id from something anyone could walk into something
    /// that is only reachable by its owner.</summary>
    public Guid PlayerAccountId { get; set; }

    public string PlayerName { get; set; } = "";
    public string ActiveHeroKey { get; set; } = "mage";
    public string Region { get; set; } = "cave_beyond_time";
    // Where in the region the party stood — the resume point.
    public float PosX { get; set; }
    public float PosY { get; set; }
    public long PlaytimeSeconds { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<SaveHero> Party { get; set; } = new();
    public List<SaveClaim> Claims { get; set; } = [];
    public List<SaveTally> Belongings { get; set; } = [];
    public List<SaveContainer> Containers { get; set; } = [];
}

/// <summary>One container this save has opened. The remainder is a child table
/// rather than a blob, for the same reason the belongings are: "what is still
/// sitting unclaimed in the world" is a question worth being able to ask.</summary>
public class SaveContainer
{
    public int Id { get; set; }
    public int PlayerSaveId { get; set; }
    public string ContainerKey { get; set; } = "";
    public DateTime OpenedAt { get; set; }
    public int Coin { get; set; }
    public List<SaveContainerItem> Remaining { get; set; } = [];
}

public class SaveContainerItem
{
    public int Id { get; set; }
    public int SaveContainerId { get; set; }
    public string Kind { get; set; } = "";
    public string TallyKey { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>Coin, materials and pack contents. One table with a Kind rather than
/// three identical ones — and columns rather than a blob, so "what is the economy
/// actually doing" stays a question this database can answer.</summary>
public class SaveTally
{
    public int Id { get; set; }
    public int PlayerSaveId { get; set; }
    public string Kind { get; set; } = "";
    public string TallyKey { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>One reward this save has already taken. A row per claim rather than a
/// delimited string: the names are authored content and a comma in one would
/// quietly corrupt the list.</summary>
public class SaveClaim
{
    public int Id { get; set; }
    public int PlayerSaveId { get; set; }
    public string RewardKey { get; set; } = "";
}

public class SaveHero
{
    public int Id { get; set; }
    public int PlayerSaveId { get; set; }
    public string HeroKey { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public bool Recruited { get; set; }

    public int AttributePoints { get; set; }
    public int SkillPoints { get; set; }

    // Points the player committed, per attribute. Columns rather than a blob so
    // a future balance pass can query them — "how is everyone actually building
    // the Cleric" is a question a JSON column cannot answer.
    public int SpentStrength { get; set; }
    public int SpentDexterity { get; set; }
    public int SpentAgility { get; set; }
    public int SpentVitality { get; set; }
    public int SpentIntelligence { get; set; }
    public int SpentWisdom { get; set; }
    public int SpentDefense { get; set; }
    public int SpentLuck { get; set; }

    // This hero's record, per statistic. Columns rather than a blob for the same
    // reason as the spent points above — "how far has anyone got the Thief" is a
    // question a JSON column cannot answer.
    public long StatEnemiesDefeated { get; set; }
    public long StatBossesDefeated { get; set; }
    public long StatDeaths { get; set; }
    public long StatDamageDealt { get; set; }
    public long StatDamageTaken { get; set; }
    public long StatTreasuresOpened { get; set; }
    public long StatItemsCollected { get; set; }
    public long StatGoldEarned { get; set; }
    public long StatDistanceWalked { get; set; }
    public long StatPlaySeconds { get; set; }

    public List<SaveSkill> Skills { get; set; } = [];
    public List<SaveEquipment> Equipped { get; set; } = [];

    /// <summary>This hero's own pack and purse, as keyed counts — the same shape as
    /// the party's shared <see cref="SaveTally"/>, but hung off the hero because
    /// inventory and gold are individual now.</summary>
    public List<SaveHeroTally> Belongings { get; set; } = [];
}

/// <summary>A hero's own pack item or coin, keyed and counted. Mirrors
/// <see cref="SaveTally"/> but scoped to a hero rather than the whole save.</summary>
public class SaveHeroTally
{
    public int Id { get; set; }
    public int SaveHeroId { get; set; }
    public string Kind { get; set; } = "";
    public string TallyKey { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>One item a hero is wearing. Only the id: the item's stats live in the
/// game's data, so a rebalance reaches saved gear instead of being frozen out
/// of it.</summary>
public class SaveEquipment
{
    public int Id { get; set; }
    public int SaveHeroId { get; set; }
    public string ItemId { get; set; } = "";
}

/// <summary>One learned skill and how far it was taken. Its own table rather than
/// a packed string on the row above: the set is open-ended, and an encoding like
/// "id:rank;id:rank" is a parser waiting to be written badly.</summary>
public class SaveSkill
{
    public int Id { get; set; }
    public int SaveHeroId { get; set; }
    public string SkillId { get; set; } = "";
    public int Rank { get; set; }
}
