namespace AlfarQuest.Client.Models;

/// <summary>A kind of thing the world lets you interact with.
///
/// The catalogue is the system. Adding a wardrobe, a fishing spot or a boss
/// chest is one row here plus a placement — no branch anywhere reads an id and
/// decides what a chest does, which is what "modular and easy to expand" has to
/// mean in practice.
///
/// <paramref name="Prop"/> ties the object to the art that stands for it, so a
/// container can never be placed where the player sees nothing.</summary>
/// <param name="Verb">What the prompt offers: Open, Search, Mine, Read.</param>
/// <param name="RequiresKey">A material id that must be in the pouch. Consumed on
/// opening — a key that opened everything would not be a lock.</param>
/// <param name="RespawnHours">Null never comes back. The brief's own figures:
/// common barrels within the hour, common chests in a day, rare chests never.</param>
public sealed record ContainerKind(
    string Id,
    string Name,
    string Verb,
    string Prop,
    LootTable Loot,
    XpSource Xp,
    Rarity Tier = Rarity.Common,
    string? RequiresKey = null,
    float? RespawnHours = null,
    string? EmptyLine = null)
{
    /// <summary>What the object says when the roll came up with nothing. Silence
    /// on an empty barrel reads as a broken button.</summary>
    public string WhenEmpty => EmptyLine ?? "Nothing useful inside.";

    public static readonly IReadOnlyList<ContainerKind> All =
    [
        // ---- chests: the tiers the brief names, told apart by what is in them
        new("chest_wood", "Wooden Chest", "Open", "crate", new LootTable(
                LootEntry.Coin(12, 30, guaranteed: true),
                LootEntry.Material("Stone", 1, 3, 0.5f),
                LootEntry.Material("Hide", 1, 2, 0.35f),
                LootEntry.Gear("helm_dented", 0.10f)),
            XpSource.TreasureChest, Rarity.Common, RespawnHours: 24f),

        new("chest_iron", "Ironbound Chest", "Open", "crate", new LootTable(
                LootEntry.Coin(40, 80, guaranteed: true),
                LootEntry.Material("Small Crystal", 1, 3, 0.6f),
                LootEntry.Material("Spider Silk", 1, 2, 0.4f),
                LootEntry.Gear("gloves_hide", 0.22f),
                LootEntry.Gear("ring_copper", 0.12f)),
            XpSource.TreasureChest, Rarity.Uncommon),

        new("chest_gold", "Gilded Chest", "Open", "crate", new LootTable(
                LootEntry.Coin(90, 160, guaranteed: true),
                LootEntry.Material("Small Crystal", 2, 5, 0.8f),
                LootEntry.Gear("amulet_quiet", 0.30f),
                LootEntry.Gear("blade_old", 0.25f)),
            XpSource.TreasureChest, Rarity.Rare),

        new("chest_ancient", "Ancient Chest", "Open", "ruin", new LootTable(
                LootEntry.Coin(140, 240, guaranteed: true),
                LootEntry.Material("Small Crystal", 3, 6, 0.9f),
                LootEntry.Gear("shard_arm", 0.18f),          // ultra-rare
                LootEntry.Gear("amulet_quiet", 0.40f)),
            XpSource.Relic, Rarity.Epic),

        // A lock is only interesting if the key is findable. This one comes off
        // the skeletons — see corpse_old below.
        new("chest_locked", "Locked Strongbox", "Unlock", "crate", new LootTable(
                LootEntry.Coin(180, 300, guaranteed: true),
                LootEntry.Material("Small Crystal", 4, 8, guaranteed: true),
                LootEntry.Gear("shard_arm", 0.35f)),
            XpSource.TreasureChest, Rarity.Legendary, RequiresKey: "Rusted Key"),

        // ---- ordinary containers: often empty, and that is the point
        new("barrel", "Barrel", "Search", "barrel", new LootTable(
                LootEntry.Coin(3, 12, 0.55f),
                LootEntry.Material("Herbs", 1, 2, 0.35f),
                LootEntry.Material("Stone", 1, 2, 0.2f)),
            XpSource.OreVein, Rarity.Common, RespawnHours: 0.5f,
            EmptyLine: "The barrel holds nothing but old rainwater."),

        new("crate", "Crate", "Search", "crate", new LootTable(
                LootEntry.Coin(4, 16, 0.5f),
                LootEntry.Material("Stone", 1, 3, 0.45f),
                LootEntry.Material("Hide", 1, 1, 0.2f)),
            XpSource.OreVein, Rarity.Common, RespawnHours: 0.5f,
            EmptyLine: "Packing straw, and nothing under it."),

        new("cart", "Mine Cart", "Search", "mineCart", new LootTable(
                LootEntry.Material("Stone", 2, 5, guaranteed: true),
                LootEntry.Material("Small Crystal", 1, 2, 0.4f),
                LootEntry.Coin(8, 24, 0.5f)),
            XpSource.OreVein, Rarity.Common, RespawnHours: 1f),

        new("stall", "Trader's Stall", "Search", "stall", new LootTable(
                LootEntry.Coin(10, 26, 0.6f),
                LootEntry.Material("Herbs", 1, 3, 0.5f)),
            XpSource.OreVein, Rarity.Common, RespawnHours: 1f,
            EmptyLine: "Swept clean. Someone got here first."),

        // ---- the dead
        new("corpse", "Dead Delver", "Search", "gravestone", new LootTable(
                LootEntry.Coin(20, 60, guaranteed: true),
                LootEntry.Gear("blade_old", 0.35f),
                LootEntry.Gear("boots_worn", 0.30f),
                LootEntry.Material("Hide", 1, 2, 0.4f)),
            XpSource.Relic, Rarity.Uncommon,
            EmptyLine: "Picked over long before you got here."),

        new("corpse_old", "Old Bones", "Search", "graveyard", new LootTable(
                LootEntry.Material("Rusted Key", 1, 1, 0.45f),
                LootEntry.Coin(10, 30, 0.6f),
                LootEntry.Material("Bat Wing", 1, 2, 0.3f)),
            XpSource.AncientTablet, Rarity.Common,
            EmptyLine: "Bones, and the dust they are becoming."),

        // ---- gathering
        new("ore", "Ore Seam", "Mine", "orePile", new LootTable(
                LootEntry.Material("Stone", 2, 5, guaranteed: true),
                LootEntry.Material("Small Crystal", 1, 2, 0.35f)),
            XpSource.OreVein, Rarity.Common, RespawnHours: 1f),

        new("crystal", "Crystal Growth", "Prise", "crystal", new LootTable(
                LootEntry.Material("Small Crystal", 2, 4, guaranteed: true),
                LootEntry.Coin(10, 30, 0.4f)),
            XpSource.RareCrystal, Rarity.Rare, RespawnHours: 2f),

        new("herbs", "Herb Patch", "Gather", "bush", new LootTable(
                LootEntry.Material("Herbs", 1, 3, guaranteed: true),
                LootEntry.Material("Spider Silk", 1, 1, 0.2f)),
            XpSource.OreVein, Rarity.Common, RespawnHours: 0.5f),

        // ---- things to read and stand before
        new("altar", "Ancient Altar", "Touch", "ruin", new LootTable(
                LootEntry.Coin(60, 120, guaranteed: true),
                LootEntry.Material("Small Crystal", 2, 4, 0.7f)),
            XpSource.Relic, Rarity.Epic),

        new("statue", "Weathered Statue", "Examine", "statue", LootTable.Empty,
            XpSource.AncientTablet, Rarity.Common,
            EmptyLine: "A face worn past recognition. Whoever it was is not remembered."),

        new("runestone", "Rune Stone", "Read", "signpost", LootTable.Empty,
            XpSource.AncientTablet, Rarity.Uncommon,
            EmptyLine: "The carving is Àlfar, and older than the mine."),

        new("well", "Old Well", "Search", "well", new LootTable(
                LootEntry.Coin(15, 45, 0.7f),
                LootEntry.Material("Small Crystal", 1, 1, 0.15f)),
            XpSource.TreasureChest, Rarity.Common, RespawnHours: 2f,
            EmptyLine: "You hear the coin hit water a long way down. It was not yours."),
    ];

    private static readonly Dictionary<string, ContainerKind> ById = All.ToDictionary(k => k.Id);

    public static ContainerKind? Find(string id) => ById.GetValueOrDefault(id);
}
