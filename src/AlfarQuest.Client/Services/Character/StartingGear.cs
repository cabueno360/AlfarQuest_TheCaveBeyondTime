using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Services.Character;

/// <summary>What each delver carries at the mouth of the cave. Data, so a new
/// class or a loot table later reuses the same Item shape.</summary>
public static class StartingGear
{
    private static readonly Dictionary<string, Item[]> ByClass = new()
    {
        ["Mage"] =
        [
            new() { Id = "staff_ash", Name = "Ashen Staff", Slot = Slot.MainHand, Rarity = Rarity.Uncommon,
                    Icon = "🪄", Damage = 6, Attributes = new() { Intelligence = 3 },
                    Flavour = "Scorched at the grip, where the demon pushes back." },
            new() { Id = "robe_exile", Name = "Exile's Robe", Slot = Slot.Chest, Rarity = Rarity.Common,
                    Icon = "🧥", Armour = 4, Attributes = new() { Intelligence = 1 } },
        ],
        ["Cleric"] =
        [
            new() { Id = "mace_gilded", Name = "Gilded Mace", Slot = Slot.MainHand, Rarity = Rarity.Rare,
                    Icon = "🔨", Damage = 11, Attributes = new() { Strength = 2 },
                    Flavour = "Blessed twice. He no longer remembers by whom." },
            new() { Id = "plate_blessed", Name = "Blessed Plate", Slot = Slot.Chest, Rarity = Rarity.Uncommon,
                    Icon = "🛡", Armour = 9, Attributes = new() { Vitality = 2 } },
            new() { Id = "torch", Name = "Pitch Torch", Slot = Slot.OffHand, Rarity = Rarity.Common,
                    Icon = "🔥", Flavour = "The only honest light down here." },
        ],
        ["Thief"] =
        [
            new() { Id = "crossbow", Name = "Seoshe Crossbow", Slot = Slot.MainHand, Rarity = Rarity.Uncommon,
                    Icon = "🏹", Damage = 8, CritChance = 0.04f, Attributes = new() { Dexterity = 2 } },
            new() { Id = "shard_arm", Name = "Fused Shard", Slot = Slot.Ring1, Rarity = Rarity.Epic,
                    Icon = "💎", CritChance = 0.06f, Attributes = new() { Luck = 3, Agility = 1 },
                    Flavour = "It took the crew. It left this." },
        ],
    };

    /// <summary>Loose gear the party sets out with, so the pack is not empty and
    /// there is something to compare against.</summary>
    public static readonly IReadOnlyList<Item> Spares =
    [
        new() { Id = "helm_dented", Name = "Dented Helm", Slot = Slot.Helmet, Rarity = Rarity.Common,
                Icon = "⛑", Armour = 3 },
        new() { Id = "boots_worn", Name = "Worn Boots", Slot = Slot.Boots, Rarity = Rarity.Common,
                Icon = "🥾", Armour = 2, Attributes = new() { Agility = 1 } },
        new() { Id = "ring_copper", Name = "Copper Band", Slot = Slot.Ring2, Rarity = Rarity.Uncommon,
                Icon = "○", Attributes = new() { Luck = 2 } },
        new() { Id = "amulet_quiet", Name = "Quiet Amulet", Slot = Slot.Necklace, Rarity = Rarity.Rare,
                Icon = "◈", Attributes = new() { Intelligence = 3, Vitality = 1 },
                Flavour = "It stops humming when something is near." },
        new() { Id = "blade_old", Name = "Old Shortblade", Slot = Slot.MainHand, Rarity = Rarity.Common,
                Icon = "🗡", Damage = 4 },
        new() { Id = "gloves_hide", Name = "Hide Gloves", Slot = Slot.Gloves, Rarity = Rarity.Common,
                Icon = "🧤", Armour = 2, Attributes = new() { Dexterity = 1 } },
    ];

    public static IReadOnlyList<Item> For(string heroClass) =>
        ByClass.TryGetValue(heroClass, out var kit) ? kit : [];

    /// <summary>Every item defined here, for the catalogue a save resolves ids
    /// against. Derived from the tables above rather than listed again, so an item
    /// cannot be added to the game and forgotten by the save.</summary>
    public static IEnumerable<Item> Everything => ByClass.Values.SelectMany(k => k).Concat(Spares);
}
