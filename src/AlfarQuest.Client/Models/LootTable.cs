namespace AlfarQuest.Client.Models;

/// <summary>What a single roll can produce.
///
/// Three kinds because the game stores three kinds: coin in the purse, materials
/// counted in the pouch, and equipment as individual items in the pack. A single
/// "reward" type would have to be nullable in two of three fields on every row.</summary>
public enum LootKind { Coin, Material, Item }

/// <summary>One line of a loot table.</summary>
/// <param name="Chance">0..1. Ignored when <paramref name="Guaranteed"/> is set.</param>
/// <param name="Guaranteed">Always drops. Every container that is worth opening
/// should have at least one, or a player can open six in a row and get nothing
/// — which teaches them to stop opening them.</param>
public sealed record LootEntry(
    LootKind Kind, string Key, int Min, int Max, float Chance = 1f, bool Guaranteed = false)
{
    public static LootEntry Coin(int min, int max, float chance = 1f, bool guaranteed = false) =>
        new(LootKind.Coin, "gold", min, max, chance, guaranteed);

    public static LootEntry Material(string id, int min, int max, float chance = 1f, bool guaranteed = false) =>
        new(LootKind.Material, id, min, max, chance, guaranteed);

    /// <summary>An item by catalogue id. Quantity is always one — equipment does
    /// not stack, and a "x3 Gilded Mace" would be three rows in the pack.</summary>
    public static LootEntry Gear(string itemId, float chance) =>
        new(LootKind.Item, itemId, 1, 1, chance);
}

/// <summary>What is inside a container, before it is rolled.
///
/// Declared as data so a new container is a table, not a branch. Everything the
/// brief asks for — drop chances, minimum and maximum quantity, guaranteed
/// items, rare and ultra-rare drops — is expressed by the rows rather than by
/// code that knows about "rare".</summary>
public sealed record LootTable(params LootEntry[] Entries)
{
    public static readonly LootTable Empty = new();

    /// <summary>Rolls the table.
    ///
    /// <paramref name="luck"/> is the party's find-rarity bonus — the same figure
    /// the creature loot roll uses, so a Luck build pays off everywhere rather
    /// than only on kills. It lifts the chance of the optional rows; a guaranteed
    /// row is already certain and a quantity is not a chance, so neither moves.</summary>
    public LootStack Roll(Func<double> rnd, float luck = 0f)
    {
        var coin = 0;
        var materials = new List<(string Id, int Count)>();
        var items = new List<string>();

        foreach (var e in Entries)
        {
            if (!e.Guaranteed && rnd() > e.Chance * (1f + luck)) continue;

            var count = e.Min >= e.Max ? e.Min : e.Min + (int)(rnd() * (e.Max - e.Min + 1));
            if (count <= 0) continue;

            switch (e.Kind)
            {
                case LootKind.Coin: coin += count; break;
                case LootKind.Material: materials.Add((e.Key, count)); break;
                case LootKind.Item: for (var i = 0; i < count; i++) items.Add(e.Key); break;
            }
        }

        return new LootStack(coin, materials, items);
    }
}

/// <summary>A rolled result: what is actually in this container, now.
///
/// Mutable because the player takes from it one line at a time and whatever is
/// left has to survive being saved. A record of immutable lists would mean
/// rebuilding the whole thing on every click.</summary>
public sealed class LootStack(int coin, List<(string Id, int Count)> materials, List<string> items)
{
    public int Coin { get; private set; } = coin;
    public List<(string Id, int Count)> Materials { get; } = materials;
    public List<string> Items { get; } = items;

    public bool IsEmpty => Coin <= 0 && Materials.Count == 0 && Items.Count == 0;

    /// <summary>Doubles the coin and every material stack — the harvest die's
    /// jackpot: the vein that cracks wide, the rare bloom among the leaves.
    /// Items are left alone; a second sword does not grow out of a lucky swing.</summary>
    public void Bounty()
    {
        Coin *= 2;
        for (var i = 0; i < Materials.Count; i++)
            Materials[i] = (Materials[i].Id, Materials[i].Count * 2);
    }

    public int LineCount => (Coin > 0 ? 1 : 0) + Materials.Count + Items.Count;

    public void TakeCoin() => Coin = 0;

    public void TakeMaterial(int index)
    {
        if (index >= 0 && index < Materials.Count) Materials.RemoveAt(index);
    }

    public void TakeItem(int index)
    {
        if (index >= 0 && index < Items.Count) Items.RemoveAt(index);
    }

    public void Clear()
    {
        Coin = 0;
        Materials.Clear();
        Items.Clear();
    }
}
