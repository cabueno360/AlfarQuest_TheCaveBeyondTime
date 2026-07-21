using AlfarQuest.Client.Game;

namespace AlfarQuest.Client.Models;

/// <summary>Something the party can make. Data-driven: a new recipe is one entry
/// in <see cref="RecipeBook"/>, and the panel renders whatever is there.</summary>
public sealed record Recipe
{
    public required string Id { get; init; }
    public required Item Output { get; init; }

    /// <summary>Materials consumed, as (material id, count).</summary>
    public required IReadOnlyList<(string Material, int Count)> Cost { get; init; }

    public int CoinCost { get; init; }

    /// <summary>Which craftsman must be at hand. None means anywhere — a repair
    /// you can do at a campfire.</summary>
    public NpcServices Station { get; init; } = NpcServices.Crafting;

    public string StationName { get; init; } = "any craftsman";

    public bool Affordable(Satchel pouch, Wallet purse) =>
        Cost.All(c => pouch[c.Material] >= c.Count) && purse["gold"] >= CoinCost;
}

public static class RecipeBook
{
    public static readonly IReadOnlyList<Recipe> All =
    [
        new() { Id = "silk_gloves", StationName = "Dagna the blacksmith",
                Cost = [("Spider Silk", 2), ("Hide", 1)], CoinCost = 20,
                Output = new() { Id = "gloves_silk", Name = "Silkbound Gloves", Slot = Slot.Gloves,
                                 Rarity = Rarity.Uncommon, Icon = "🧤", Armour = 4,
                                 Attributes = new() { Dexterity = 2 },
                                 Flavour = "Light enough to forget you are wearing them." } },

        new() { Id = "stone_helm", StationName = "Dagna the blacksmith",
                Cost = [("Stone", 4), ("Hide", 1)], CoinCost = 35,
                Output = new() { Id = "helm_stone", Name = "Quarried Helm", Slot = Slot.Helmet,
                                 Rarity = Rarity.Uncommon, Icon = "⛑", Armour = 8,
                                 Attributes = new() { Vitality = 1 } } },

        new() { Id = "crystal_ring", StationName = "Perrin the alchemist",
                Cost = [("Small Crystal", 3), ("Bat Wing", 1)], CoinCost = 50,
                Output = new() { Id = "ring_crystal", Name = "Humming Band", Slot = Slot.Ring2,
                                 Rarity = Rarity.Rare, Icon = "○", CritChance = 0.05f,
                                 Attributes = new() { Luck = 2, Intelligence = 2 },
                                 Flavour = "It answers the crystal below. Faintly." } },

        new() { Id = "herb_amulet", StationName = "Perrin the alchemist",
                Cost = [("Herbs", 3), ("Spider Silk", 1)], CoinCost = 25,
                Output = new() { Id = "amulet_herb", Name = "Green Charm", Slot = Slot.Necklace,
                                 Rarity = Rarity.Uncommon, Icon = "◈",
                                 Attributes = new() { Vitality = 3 },
                                 Flavour = "Mother Sena's recipe. She would want it back." } },
    ];
}
