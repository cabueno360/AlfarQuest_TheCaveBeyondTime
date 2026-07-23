using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

/// <summary>When a merchant's shelves fill back up. Only <see cref="None"/> is
/// wired; the rest are the architecture the brief asks for — a restock loop can
/// switch on this without the shop knowing which policy it is.</summary>
public enum RestockPolicy { None, Daily, Weekly, OnStoryProgress, OnQuestComplete }

/// <summary>One line of a merchant's opening stock: an item and how many.</summary>
public sealed record StockLine(string ItemId, int Quantity);

/// <summary>What makes an NPC a shopkeeper — their greeting, their shelves, and
/// what they will take off your hands. Kept apart from <see cref="NpcDefinition"/>
/// so "who stands where and says what" and "who trades and in what" are two
/// lists: not every NPC is a merchant, and the ones who are earn it by role.</summary>
public sealed record Merchant
{
    public required string NpcId { get; init; }
    public required string Greeting { get; init; }
    public required IReadOnlyList<StockLine> Stock { get; init; }

    /// <summary>Which categories this merchant will buy from the player. Empty
    /// means anything — the roadside trader takes all comers. A blacksmith buys
    /// weapons and armour; an alchemist will not touch a sword.</summary>
    public IReadOnlyList<ItemClass> Buys { get; init; } = [];

    /// <summary>An uncommon merchant with exclusive stock — worth seeking out.</summary>
    public bool Special { get; init; }

    /// <summary>How the shelves refill. Off by default; the field is here so the
    /// systems the brief lists as future have somewhere to read from.</summary>
    public RestockPolicy Restock { get; init; } = RestockPolicy.None;

    public bool BuysCategory(ItemClass c) => Buys.Count == 0 || Buys.Contains(c);
}

/// <summary>Every merchant in the game, as data — who trades, in what, and on
/// what terms. The one list the shop and the engine both read, so "this NPC keeps
/// a shop" has a single answer.</summary>
public static class MerchantCatalog
{
    public static readonly IReadOnlyList<Merchant> All =
    [
        // Dagna, at the forge — weapons and the armour to survive what they meet.
        new()
        {
            NpcId = "smith", Greeting = "Your edge is dull and everything down there is not. What'll it be?",
            Buys = [ItemClass.Weapon, ItemClass.Armor],
            Stock =
            [
                new("sword_iron", 3), new("sword_steel", 1), new("axe_war", 1), new("hammer_iron", 1),
                new("helm_iron", 2), new("plate_iron", 1), new("boots_iron", 2), new("shield_round", 1),
            ],
        },
        // Perrin the alchemist — draughts and crystal-work. Will not buy a blade.
        new()
        {
            NpcId = "alchemist", Greeting = "Careful what you breathe in here. Now — potions, was it?",
            Buys = [ItemClass.Consumable, ItemClass.Material, ItemClass.Magic],
            Stock =
            [
                new("potion_health", 5), new("potion_mana", 4), new("potion_antidote", 3),
                new("potion_elixir", 1), new("crystal_mana", 2),
            ],
        },
        // Mother Sena, the herbalist by the water — the gentle shelf.
        new()
        {
            NpcId = "wife", Greeting = "Take the herbs by the water, love. Or take mine, they're cleaner.",
            Buys = [ItemClass.Consumable, ItemClass.Material, ItemClass.Food],
            Stock =
            [
                new("herbs_medicinal", 6), new("potion_health", 2), new("seeds_sunleaf", 4),
                new("potion_antidote", 2), new("ration_trail", 4),
            ],
        },
        // Sella, the road trader at the crossroad — a bit of everything, buys all.
        new()
        {
            NpcId = "trader", Greeting = "Come from the towns, going to the towns. Everything's for sale but the cart.",
            Stock =
            [
                new("potion_health", 2), new("arrows_bundle", 3), new("ration_trail", 5),
                new("hide_cured", 3), new("spear_long", 1), new("ring_copper", 1),
            ],
        },
        // The Neruum trader in Seoshe's market — off the flotilla, a bit of everything.
        new()
        {
            NpcId = "seoshe_trader", Greeting = "Off the flotilla, all of it. Foreign goods, foreign luck. Both spend the same.",
            Stock =
            [
                new("potion_health", 4), new("potion_mana", 3), new("herbs_medicinal", 5),
                new("ration_trail", 6), new("hide_cured", 4), new("arrows_bundle", 3),
                new("crystal_mana", 1), new("ring_copper", 1),
            ],
        },
        // The Keeper — a rare scholar-merchant with what the others cannot get.
        new()
        {
            NpcId = "scholar", Greeting = "Few find this door. Fewer leave with coin still in their purse.",
            Special = true,
            Buys = [ItemClass.Magic, ItemClass.Book, ItemClass.Accessory, ItemClass.Consumable],
            Stock =
            [
                new("tome_embers", 1), new("rune_ward", 2), new("crystal_mana", 2),
                new("ring_scholar", 1), new("potion_elixir", 2),
            ],
        },
    ];

    private static readonly Dictionary<string, Merchant> ById = All.ToDictionary(m => m.NpcId);
    public static Merchant? For(string npcId) => ById.GetValueOrDefault(npcId);
    public static bool Trades(string npcId) => ById.ContainsKey(npcId);
}
