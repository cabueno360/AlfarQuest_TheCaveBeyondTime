using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Services.Character;

/// <summary>Every item the game can produce, indexed by id.
///
/// This exists so a save can store an id instead of a copy of the item. Storing
/// the stats would freeze them: rebalance the Gilded Mace and every existing save
/// would keep the old one forever, which is the same reason spent attribute
/// points are saved rather than totals.
///
/// It is assembled from the two places items are actually defined, so there is no
/// third list to keep in step — an item that exists in the game is in here by
/// construction.</summary>
public static class ItemCatalog
{
    public static readonly IReadOnlyDictionary<string, Item> ById = Build();

    /// <summary>Null for an id that no longer exists. Callers skip it rather than
    /// failing the load: an item removed in a later version should cost the player
    /// that item, not their whole save.</summary>
    public static Item? Find(string? id) =>
        id is not null && ById.TryGetValue(id, out var item) ? item : null;

    private static Dictionary<string, Item> Build()
    {
        var all = new Dictionary<string, Item>();

        foreach (var item in StartingGear.Everything) all[item.Id] = item;
        foreach (var recipe in RecipeBook.All) all[recipe.Output.Id] = recipe.Output;

        return all;
    }
}
