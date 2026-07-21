namespace AlfarQuest.Client.Models;

/// <summary>Every place an item can be worn.
///
/// Wider than the game currently has gear for. That is deliberate: the paper doll
/// is meant to show the shape of a whole character, and an empty Cape slot tells a
/// player something to look for, whereas a doll with six slots tells them the game
/// is small. Nothing here is persisted by name — a save stores item ids — so this
/// list can grow without touching anyone's save.</summary>
public enum Slot
{
    Helmet, Shoulders, Cape, Chest, Gloves, Belt,
    Necklace, Ring1, Ring2, Pants, Boots, Artifact,
    MainHand, OffHand, Shield,
    Pet, Mount,
}

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

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
        new(Rarity.Mythic,    "Mythic",    "#ff7a6b"),
    ];

    public static RarityInfo Of(Rarity r) => All.First(x => x.Rarity == r);
}

/// <summary>Which column of the paper doll a slot stands in.</summary>
public enum DollColumn { Left, Right, Hands, Future }

/// <summary>Where a slot appears on the paper doll and what it looks like empty.
///
/// The layout lives here rather than in CSS so adding a slot is one row and the
/// doll rearranges itself. Icon is the silhouette shown while the slot is empty:
/// an outline of what belongs there reads as "nothing yet", where a blank square
/// reads as a rendering fault.</summary>
public sealed record SlotInfo(Slot Slot, string Name, string Icon, DollColumn Column, bool Available = true)
{
    public static readonly IReadOnlyList<SlotInfo> All =
    [
        new(Slot.Helmet,    "Helmet",    "⛑", DollColumn.Left),
        new(Slot.Shoulders, "Shoulders", "⩕", DollColumn.Left),
        new(Slot.Cape,      "Cape",      "🜲", DollColumn.Left),
        new(Slot.Chest,     "Armor",     "🛡", DollColumn.Left),
        new(Slot.Gloves,    "Gloves",    "🧤", DollColumn.Left),
        new(Slot.Belt,      "Belt",      "⌒", DollColumn.Left),

        new(Slot.Necklace,  "Necklace",  "◈", DollColumn.Right),
        new(Slot.Ring1,     "Ring",      "○", DollColumn.Right),
        new(Slot.Ring2,     "Ring",      "○", DollColumn.Right),
        new(Slot.Pants,     "Legs",      "👖", DollColumn.Right),
        new(Slot.Boots,     "Boots",     "🥾", DollColumn.Right),
        new(Slot.Artifact,  "Artifact",  "✧", DollColumn.Right),

        new(Slot.MainHand,  "Main Hand", "⚔", DollColumn.Hands),
        new(Slot.OffHand,   "Off Hand",  "🗡", DollColumn.Hands),
        new(Slot.Shield,    "Shield",    "🛆", DollColumn.Hands),

        // Shown because the brief names them, disabled because neither system
        // exists. A slot that quietly accepted an item it could do nothing with
        // would be worse than one that says it is not ready.
        new(Slot.Pet,       "Pet",       "🐾", DollColumn.Future, Available: false),
        new(Slot.Mount,     "Mount",     "🐎", DollColumn.Future, Available: false),
    ];

    public static SlotInfo Of(Slot s) => All.First(x => x.Slot == s);

    public static IEnumerable<SlotInfo> In(DollColumn column) => All.Where(x => x.Column == column);
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
