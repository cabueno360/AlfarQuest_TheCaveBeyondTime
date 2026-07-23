using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Services.Character;

/// <summary>Goods that are bought and carried but never worn — potions, herbs,
/// rations, arrows — and the pieces of armour a smith keeps on the rack. Kept
/// apart from <see cref="WeaponRack"/> and <see cref="StartingGear"/> so "what a
/// shop stocks" is its own list, and so a consumable is not mistaken for a spare
/// the party set out with.
///
/// A consumable is a <see cref="Slot.None"/> item: it lives in the pack and can
/// be traded, but the paper doll and the equip button leave it alone. What it
/// does when used is a later system; for now it is a good with a price.</summary>
public static class MerchantGoods
{
    public static readonly IReadOnlyList<Item> All =
    [
        // ---- the alchemist's and herbalist's shelves ----------------------
        new() { Id = "potion_health", Name = "Health Draught", Slot = Slot.None, Value = 25,
                Category = ItemClass.Consumable, Rarity = Rarity.Common, Icon = "🧪",
                Flavour = "Tastes of iron and regret. Works all the same." },
        new() { Id = "potion_mana", Name = "Mana Draught", Slot = Slot.None, Value = 30,
                Category = ItemClass.Consumable, Rarity = Rarity.Common, Icon = "⚗",
                Flavour = "Cold going down, bright coming up." },
        new() { Id = "potion_antidote", Name = "Antidote", Slot = Slot.None, Value = 20,
                Category = ItemClass.Consumable, Rarity = Rarity.Common, Icon = "🝆",
                Flavour = "For what the spiders leave behind." },
        new() { Id = "potion_elixir", Name = "Greater Elixir", Slot = Slot.None, Value = 90,
                Category = ItemClass.Magic, Rarity = Rarity.Rare, Icon = "🫗",
                Flavour = "Whatever ails you, it disagrees with it more." },
        new() { Id = "herbs_medicinal", Name = "Medicinal Herbs", Slot = Slot.None, Value = 12,
                Category = ItemClass.Consumable, Rarity = Rarity.Common, Icon = "❦",
                Flavour = "Bitter, but it keeps the shakes off." },
        new() { Id = "seeds_sunleaf", Name = "Sunleaf Seeds", Slot = Slot.None, Value = 8,
                Category = ItemClass.Material, Rarity = Rarity.Common, Icon = "🌱",
                Flavour = "They grow toward whatever light there is. Even down here." },

        // ---- the hunter's and the road's stores ---------------------------
        new() { Id = "arrows_bundle", Name = "Bundle of Arrows", Slot = Slot.None, Value = 15,
                Category = ItemClass.Consumable, Rarity = Rarity.Common, Icon = "➶",
                Flavour = "Twenty, fletched grey. He counts them out slowly." },
        new() { Id = "ration_trail", Name = "Trail Ration", Slot = Slot.None, Value = 6,
                Category = ItemClass.Food, Rarity = Rarity.Common, Icon = "🍞",
                Flavour = "Hard as a shield and half as tasty. It travels." },
        new() { Id = "hide_cured", Name = "Cured Hide", Slot = Slot.None, Value = 18,
                Category = ItemClass.Material, Rarity = Rarity.Common, Icon = "▧",
                Flavour = "Good for a strap, a patch, or a night's warmth." },

        // ---- the smith's armour rack --------------------------------------
        new() { Id = "helm_iron", Name = "Iron Helm", Slot = Slot.Helmet, Value = 70,
                Rarity = Rarity.Uncommon, Icon = "⛑", Armour = 6 },
        new() { Id = "plate_iron", Name = "Iron Cuirass", Slot = Slot.Chest, Value = 140,
                Rarity = Rarity.Uncommon, Icon = "🛡", Armour = 11, Attributes = new() { Vitality = 1 } },
        new() { Id = "boots_iron", Name = "Ironshod Boots", Slot = Slot.Boots, Value = 55,
                Rarity = Rarity.Uncommon, Icon = "🥾", Armour = 5 },
        new() { Id = "shield_round", Name = "Round Shield", Slot = Slot.Shield, Value = 90,
                Rarity = Rarity.Uncommon, Icon = "🛆", Armour = 8, Attributes = new() { Defense = 1 } },

        // ---- the magic scholar's exclusive stock --------------------------
        new() { Id = "tome_embers", Name = "Tome of Embers", Slot = Slot.None, Value = 260,
                Category = ItemClass.Book, Rarity = Rarity.Rare, Icon = "📕",
                Flavour = "The margins are singed. Read it anyway." },
        new() { Id = "rune_ward", Name = "Warding Rune", Slot = Slot.None, Value = 180,
                Category = ItemClass.Magic, Rarity = Rarity.Rare, Icon = "🜛",
                Flavour = "It hums when something means you harm." },
        new() { Id = "crystal_mana", Name = "Mana Crystal", Slot = Slot.None, Value = 120,
                Category = ItemClass.Magic, Rarity = Rarity.Rare, Icon = "◈",
                Flavour = "A well of the deep light, corked in glass." },
        new() { Id = "ring_scholar", Name = "Scholar's Ring", Slot = Slot.Ring1, Value = 340,
                Category = ItemClass.Accessory, Rarity = Rarity.Epic, Icon = "◍",
                Attributes = new() { Intelligence = 4, Wisdom = 2 } },
    ];

    private static readonly Dictionary<string, Item> ById = All.ToDictionary(i => i.Id);
    public static Item? Find(string id) => ById.GetValueOrDefault(id);
}
