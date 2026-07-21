namespace AlfarQuest.Client.Models;

/// <summary>The party's pack. Shared rather than per-hero: the delvers travel
/// together, and splitting the bag would mean a screen to move things between
/// them before any of it was useful.</summary>
public sealed class Inventory(int capacity = 24)
{
    private readonly List<Item> _items = [];

    public int Capacity { get; } = capacity;
    public IReadOnlyList<Item> Items => _items;
    public bool IsFull => _items.Count >= Capacity;

    public bool Add(Item? item)
    {
        if (item is null || IsFull) return false;
        _items.Add(item);
        return true;
    }

    public bool Remove(Item item) => _items.Remove(item);

    /// <summary>Empties the pack. Used when a save is applied: restoring has to
    /// replace what the party is carrying, not add to the starting kit they were
    /// just handed.</summary>
    public void Clear() => _items.Clear();

    /// <summary>What is worn in the slot this item would take, so the UI can show
    /// the player what they are giving up before they give it up.</summary>
    public static Item? Rival(Loadout gear, Item candidate) => gear[candidate.Slot];
}
