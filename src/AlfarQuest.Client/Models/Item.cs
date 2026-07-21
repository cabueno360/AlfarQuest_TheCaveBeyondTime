namespace AlfarQuest.Client.Models;

public enum Slot { Helmet, Necklace, Chest, Gloves, Pants, Boots, Ring1, Ring2, MainHand, OffHand }

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>Presentation for a rarity, beside the enum so a new tier is one row
/// and no component changes.</summary>
public sealed record RarityInfo(Rarity Rarity, string Name, string Colour)
{
    public static readonly IReadOnlyList<RarityInfo> All =
    [
        new(Rarity.Common,    "Common",    "#b8b6c8"),
        new(Rarity.Uncommon,  "Uncommon",  "#7fd694"),
        new(Rarity.Rare,      "Rare",      "#7fb6ff"),
        new(Rarity.Epic,      "Epic",      "#c98fff"),
        new(Rarity.Legendary, "Legendary", "#f0d99a"),
    ];

    public static RarityInfo Of(Rarity r) => All.First(x => x.Rarity == r);
}

/// <summary>Where a slot appears and what it will accept. Data-driven so the
/// equipment panel renders itself from this list.</summary>
public sealed record SlotInfo(Slot Slot, string Name, string Icon)
{
    public static readonly IReadOnlyList<SlotInfo> All =
    [
        new(Slot.Helmet,   "Helmet",    "⛑"),
        new(Slot.Necklace, "Necklace",  "◈"),
        new(Slot.Chest,    "Chest",     "🛡"),
        new(Slot.Gloves,   "Gloves",    "🧤"),
        new(Slot.Pants,    "Legs",      "👖"),
        new(Slot.Boots,    "Boots",     "🥾"),
        new(Slot.Ring1,    "Ring",      "○"),
        new(Slot.Ring2,    "Ring",      "○"),
        new(Slot.MainHand, "Main Hand", "⚔"),
        new(Slot.OffHand,  "Off Hand",  "🗡"),
    ];

    public static SlotInfo Of(Slot s) => All.First(x => x.Slot == s);
}

/// <summary>A piece of equipment. Immutable: items are looked at far more often
/// than they change, and an equipped item is shared by the panel, the tooltip
/// and the stat calculation at once.</summary>
public sealed record Item
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Slot Slot { get; init; }
    public Rarity Rarity { get; init; } = Rarity.Common;
    public string Icon { get; init; } = "◆";
    public string Flavour { get; init; } = "";

    /// <summary>Attribute bonuses, folded into the wearer's totals.</summary>
    public Attributes Attributes { get; init; } = new();

    /// <summary>Flat combat bonuses that do not come from an attribute.</summary>
    public float Damage { get; init; }
    public float Armour { get; init; }
    public float CritChance { get; init; }

    /// <summary>The lines a tooltip shows. Built here so every surface that
    /// describes an item words it identically.</summary>
    public IEnumerable<string> Bonuses()
    {
        if (Damage != 0) yield return $"+{Damage:0} Physical Damage";
        if (Armour != 0) yield return $"+{Armour:0} Armour";
        if (CritChance != 0) yield return $"+{CritChance * 100:0.0}% Critical Chance";
        foreach (var a in AttributeInfo.All)
        {
            var v = Attributes[a.Kind];
            if (v != 0) yield return $"+{v} {a.Name}";
        }
    }
}
