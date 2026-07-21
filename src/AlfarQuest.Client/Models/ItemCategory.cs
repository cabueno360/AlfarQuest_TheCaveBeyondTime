namespace AlfarQuest.Client.Models;

/// <summary>How the pack is filtered.
///
/// The brief lists six categories. Four of them describe things the game does not
/// have yet — there are no consumables, quest items or treasures to sort. They
/// appear in the filter anyway, shown as empty rather than hidden: a filter that
/// silently omitted a category would make the pack look complete when it is not,
/// and the player learns what to expect to find.</summary>
public enum ItemCategory { All, Equipment, Consumables, QuestItems, Materials, Treasures, Misc }

public sealed record ItemCategoryInfo(ItemCategory Category, string Name, string Icon)
{
    public static readonly IReadOnlyList<ItemCategoryInfo> All =
    [
        new(ItemCategory.All,         "All",        "▦"),
        new(ItemCategory.Equipment,   "Equipment",  "🛡"),
        new(ItemCategory.Materials,   "Materials",  "◆"),
        new(ItemCategory.Consumables, "Consumables", "🧪"),
        new(ItemCategory.QuestItems,  "Quest",      "📜"),
        new(ItemCategory.Treasures,   "Treasures",  "💎"),
        new(ItemCategory.Misc,        "Misc",       "◇"),
    ];
}

/// <summary>Sorting and filtering for the pack, as plain functions over a
/// sequence.
///
/// Kept out of the component so what the toolbar promises and what the grid shows
/// cannot drift: both go through here, and the ordering is testable without a
/// browser.</summary>
public static class ItemFilter
{
    /// <summary>Everything in the pack is equipment today, because every Item
    /// carries a Slot. When a consumable exists it will need its own category on
    /// the item; this is the one place that will have to learn about it.</summary>
    public static ItemCategory CategoryOf(Item _) => ItemCategory.Equipment;

    public static IEnumerable<Item> Apply(
        IEnumerable<Item> items, ItemCategory category, string? search, InventorySortOrder sort)
    {
        var filtered = items.Where(i => Matches(i, category, search));

        return sort switch
        {
            // Rarest first: the reason to sort a pack is usually to find the good
            // thing in it.
            InventorySortOrder.Rarity => filtered.OrderByDescending(i => i.Rarity).ThenBy(i => i.Name),
            InventorySortOrder.Name => filtered.OrderBy(i => i.Name),
            InventorySortOrder.Slot => filtered.OrderBy(i => i.Slot).ThenByDescending(i => i.Rarity),
            _ => filtered,
        };
    }

    private static bool Matches(Item item, ItemCategory category, string? search)
    {
        if (category is not ItemCategory.All && CategoryOf(item) != category) return false;
        if (string.IsNullOrWhiteSpace(search)) return true;

        // Name and slot both, so "ring" finds the Copper Band and "hand" finds
        // what goes in a hand.
        var needle = search.Trim();
        return item.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || SlotInfo.Of(item.Slot).Name.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Mirrors the window state's sort setting. Declared beside the filter
/// so the two are read together.</summary>
public enum InventorySortOrder { Rarity, Name, Slot }
