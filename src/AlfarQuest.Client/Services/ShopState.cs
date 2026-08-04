using AlfarQuest.Client.Game;
using AlfarQuest.Client.Models;
using AlfarQuest.Client.Services.Character;

namespace AlfarQuest.Client.Services;

/// <summary>The shop the player currently has open, and buying and selling across
/// its counter.
///
/// It is the merchant's side of <see cref="LootState"/>: the engine offers a
/// shopkeeper, this opens, and every coin that changes hands goes through the
/// active hero's own purse and pack — never a shared one, so who is trading
/// decides whose gold is spent and whose bag the sword lands in.
///
/// A merchant's shelves are held here for the session, drawn down as they are
/// bought and topped back up as things are sold, so a shelf you cleared stays
/// cleared until you walk away and the world is rebuilt. Restock and the wider
/// economy are the architecture the brief asks for and are switched off — see
/// <see cref="ShopEconomy"/>.</summary>
public sealed class ShopState(PartyState party)
{
    /// <summary>The dialogue leads into the shop, not straight to shelves: the
    /// merchant greets you, and Buy or Sell is a choice you make. Ask and Goodbye
    /// are the other two doors out of that first screen.</summary>
    public enum Stage { Greeting, Buying, Selling }

    /// <summary>One occupied shelf: an item and how many of it are left. Mutable
    /// because a sale changes the count without changing the item.</summary>
    public sealed class Slot(Item item, int quantity)
    {
        public Item Item { get; } = item;
        public int Quantity { get; set; } = quantity;
    }

    /// <summary>The open shop, resolved from the two catalogues so the window has
    /// everything it needs to draw a header without reaching back for it.</summary>
    public sealed record OpenShop(NpcDefinition Npc, Merchant Merchant)
    {
        public string Name => Npc.Name;
        public string Role => Npc.Role;
        public string Kind => Npc.Kind;      // the sprite, for the portrait
        public string Greeting => Merchant.Greeting;
        public bool Special => Merchant.Special;
    }

    private readonly Dictionary<string, List<Slot>> _shelves = [];

    public OpenShop? Open { get; private set; }
    public Stage Screen { get; private set; } = Stage.Greeting;

    /// <summary>The hero doing the trading — the one steered when the shop opened,
    /// and thereafter whoever the window's own hero tabs pick. Purchases are theirs
    /// and theirs alone.</summary>
    public Models.Character? Buyer { get; private set; }

    public IReadOnlyList<Models.Character> Party => party.Members;

    /// <summary>The item shown in the details pane, and which side it came from —
    /// a shelf item can be bought, a pack item can be sold.</summary>
    public Item? Detail { get; private set; }
    public bool DetailFromShop { get; private set; }

    /// <summary>The category tab the buy or sell list is filtered to, or null for
    /// all. One filter shared by both lists — a player looking for potions wants
    /// them narrowed on whichever side they are looking.</summary>
    public ItemClass? Filter { get; private set; }

    /// <summary>A one-line result of the last action, shown under the counter —
    /// "Bought Iron Helm", "Not enough gold". Cleared when the next action starts.</summary>
    public string? Notice { get; private set; }

    /// <summary>Ticks up on every completed trade, so the window has something to
    /// key its coin-and-sparkle animation to without diffing inventories itself.</summary>
    public int TradePulse { get; private set; }

    /// <summary>Whether this visit's one haggle has been tried, and what the
    /// counter charges because of it. Won: buying softens and selling sweetens.
    /// Lost badly: the keeper bristles and everything tilts the other way.</summary>
    public bool Haggled { get; private set; }
    private float _buyFactor = 1f, _sellFactor = 1f;

    /// <summary>One try at the keeper's prices per visit: d20 + the party's best
    /// Wisdom. 16 or better wins the counter over; 4 or under offends it; the
    /// middle spends the attempt on a shrug. Returns the throw for the dice
    /// overlay, or null when this visit already had its word.</summary>
    public object? TryHaggle()
    {
        if (Open is null || Haggled) return null;
        Haggled = true;

        var mod = party.Members.Count == 0 ? 0
            : party.Members.Max(m => Math.Clamp((m.Total.Wisdom - 10) / 2, 0, 5));
        var roll = Random.Shared.Next(1, 21);
        var total = roll + mod;

        string outcome;
        if (total >= 16)
        {
            _buyFactor = 0.85f; _sellFactor = 1.15f;
            Notice = "The keeper grumbles — prices soften.";
            outcome = "good";
        }
        else if (total <= 4)
        {
            _buyFactor = 1.10f; _sellFactor = 0.90f;
            Notice = "The keeper bristles — prices harden.";
            outcome = "bad";
        }
        else
        {
            Notice = "The keeper shrugs. The prices stand.";
            outcome = "plain";
        }
        Changed?.Invoke();
        // The same shape the engine's fate rolls travel in, for dice.js.
        return new { sides = 20, value = roll, mod, total, kind = "haggle", c = "#e0c66b", outcome };
    }

    public event Action? Changed;

    /// <summary>The buyer's spendable gold, through the same purse the rest of the
    /// game uses — so shared-gold mode, if it is ever turned on, needs no change here.</summary>
    public long BuyerGold => Buyer is { } b ? party.PurseFor(b)[Currency.GoldKey] : 0;

    /// <summary>Registers with the engine. The offer carries which NPC and which
    /// hero, so the shop opens on the right shelves for the right purse.</summary>
    public void Attach()
    {
        MerchantBridge.OnOpen = (npcId, buyerKey) =>
        {
            if (NpcCatalog.Find(npcId) is not { } npc || MerchantCatalog.For(npcId) is not { } merchant)
                return false;   // not a merchant the game knows — decline, do not open blank
            Open = new OpenShop(npc, merchant);
            Screen = Stage.Greeting;
            Buyer = party.Find(buyerKey) ?? party.Selected ?? party.Members.FirstOrDefault();
            Detail = null; DetailFromShop = false; Filter = null; Notice = null;
            // A fresh visit, a fresh chance to haggle — and yesterday's bad
            // blood (or good word) does not follow the party to the counter.
            Haggled = false; _buyFactor = 1f; _sellFactor = 1f;
            EnsureShelf(npcId);
            Changed?.Invoke();
            return true;
        };
    }

    /// <summary>Builds a merchant's live shelves the first time they are opened,
    /// from their catalogue stock. Kept thereafter, so the state of their shop
    /// persists across visits within the session.</summary>
    private void EnsureShelf(string npcId)
    {
        if (_shelves.ContainsKey(npcId)) return;
        if (MerchantCatalog.For(npcId) is not { } m) { _shelves[npcId] = []; return; }
        _shelves[npcId] =
        [
            .. m.Stock
                .Select(line => (item: ItemCatalog.Find(line.ItemId), line.Quantity))
                .Where(x => x.item is not null)
                .Select(x => new Slot(x.item!, x.Quantity)),
        ];
    }

    /// <summary>The open merchant's shelves, filtered to the chosen category. Empty
    /// when nothing is open.</summary>
    public IReadOnlyList<Slot> Shelf =>
        Open is { } s && _shelves.TryGetValue(s.Npc.Id, out var list)
            ? [.. list.Where(sl => sl.Quantity > 0 && (Filter is null || sl.Item.Kind == Filter))]
            : [];

    /// <summary>The buyer's pack, filtered the same way — what they could sell.</summary>
    public IReadOnlyList<Item> Pack =>
        Buyer is { } b
            ? [.. b.Bag.Items.Where(i => Filter is null || i.Kind == Filter)]
            : [];

    /// <summary>Categories that actually appear on either side right now, so the
    /// filter row shows only tabs that would find something.</summary>
    public IReadOnlyList<ItemClass> Categories
    {
        get
        {
            if (Open is not { } s) return [];
            var shelf = _shelves.TryGetValue(s.Npc.Id, out var list) ? list.Where(sl => sl.Quantity > 0).Select(sl => sl.Item.Kind) : [];
            var pack = Buyer?.Bag.Items.Select(i => i.Kind) ?? [];
            return [.. shelf.Concat(pack).Distinct().OrderBy(c => (int)c)];
        }
    }

    public void ShowBuy() { Screen = Stage.Buying; ClearDetail(); Changed?.Invoke(); }
    public void ShowSell() { Screen = Stage.Selling; ClearDetail(); Changed?.Invoke(); }
    public void BackToGreeting() { Screen = Stage.Greeting; ClearDetail(); Changed?.Invoke(); }

    private void ClearDetail() { Detail = null; DetailFromShop = false; Filter = null; Notice = null; }

    public void SetFilter(ItemClass? c) { Filter = c; Changed?.Invoke(); }

    public void SelectDetail(Item item, bool fromShop)
    {
        Detail = item; DetailFromShop = fromShop; Notice = null;
        Changed?.Invoke();
    }

    /// <summary>Switches which hero is trading. The window's hero tabs call this;
    /// the shelves do not change, but the pack, the purse and everything the
    /// details pane compares against become that hero's. The brief's rule that the
    /// shop follows the active hero, honoured inside the window.</summary>
    public void SetBuyer(Models.Character c)
    {
        if (!party.Members.Contains(c)) return;
        Buyer = c;
        // A detail drawn from the old hero's pack no longer belongs to anyone.
        if (DetailFromShop == false) ClearDetail();
        Notice = null;
        Changed?.Invoke();
    }

    // ---- buying ---------------------------------------------------------

    /// <summary>What the buyer would pay for this shelf item and be paid for a pack
    /// item — routed through <see cref="ShopEconomy"/> so a future discount lands
    /// here. With the economy levers off these equal the item's list prices.</summary>
    public int PriceToBuy(Item item) => (int)Math.Max(1, Math.Round(
        (Open is { } s ? ShopEconomy.BuyPrice(item, Buyer, s.Merchant) : item.BuyPrice) * _buyFactor));
    public int PriceToSell(Item item) => (int)Math.Max(1, Math.Round(
        (Open is { } s2 ? ShopEconomy.SellPrice(item, Buyer, s2.Merchant) : item.SellPrice) * _sellFactor));

    public bool CanAfford(Item item) => Buyer is not null && BuyerGold >= PriceToBuy(item);

    /// <summary>Buys one of a shelf item: the buyer pays, the item moves into their
    /// pack, the shelf loses one. Refused — and nothing changes — if they are short
    /// of gold or out of pack room, because a purchase that took the coin and left
    /// the item on the counter is the worst outcome a shop can have.</summary>
    public bool Buy(Slot slot)
    {
        if (Open is null || Buyer is not { } b || slot.Quantity <= 0) return false;

        var price = PriceToBuy(slot.Item);
        if (BuyerGold < price) { Notice = "Not enough gold."; Changed?.Invoke(); return false; }
        if (b.Bag.IsFull) { Notice = "Your pack is full."; Changed?.Invoke(); return false; }

        party.PurseFor(b).Add(Currency.GoldKey, -price);
        b.Bag.Add(slot.Item);
        b.Stats.Add(HeroStats.Kind.GoldSpent, price);
        b.Stats.Add(HeroStats.Kind.ItemsCollected, 1);
        slot.Quantity--;

        AudioBridge.Play?.Invoke("coin");
        Notice = $"Bought {slot.Item.Name}.";
        TradePulse++;
        // A shelf emptied to nothing drops out of the details pane so the buttons
        // do not offer to buy what is no longer there.
        if (slot.Quantity <= 0 && ReferenceEquals(Detail, slot.Item)) ClearDetailKeepScreen();
        party.Notify();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Buys up to <paramref name="want"/> of a shelf item, stopping at
    /// whatever runs out first — stock, gold or pack room. The "Buy Quantity"
    /// button; it never half-fails, each unit is a whole purchase.</summary>
    public int BuyMany(Slot slot, int want)
    {
        var bought = 0;
        for (var i = 0; i < want && slot.Quantity > 0 && Buy(slot); i++) bought++;
        if (bought > 0) Notice = $"Bought {bought}× {slot.Item.Name}.";
        Changed?.Invoke();
        return bought;
    }

    // ---- selling --------------------------------------------------------

    /// <summary>Whether this merchant will take the item at all. A blacksmith buys
    /// blades and plate; an alchemist will not touch either. The roadside trader
    /// buys anything.</summary>
    public bool WillBuy(Item item) => Open is { } s && s.Merchant.BuysCategory(item.Kind);

    /// <summary>Sells one item from the buyer's pack: it leaves the pack, the buyer
    /// is paid the sell price, and it joins the merchant's shelves. Refused if the
    /// merchant does not deal in that kind of thing.</summary>
    public bool Sell(Item item)
    {
        if (Open is not { } s || Buyer is not { } b) return false;
        if (!WillBuy(item)) { Notice = $"{s.Name} won't buy that."; Changed?.Invoke(); return false; }
        if (!b.Bag.Remove(item)) return false;

        var price = PriceToSell(item);
        party.PurseFor(b).Add(Currency.GoldKey, price);
        b.Stats.Add(HeroStats.Kind.GoldEarned, price);
        Restock(s.Npc.Id, item, 1);   // what you sell goes onto their shelf

        AudioBridge.Play?.Invoke("coin");
        Notice = $"Sold {item.Name} for {price}.";
        TradePulse++;
        // If that was the last of this item in the pack, drop it from the details.
        if (!b.Bag.Items.Contains(item) && ReferenceEquals(Detail, item)) ClearDetailKeepScreen();
        party.Notify();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Sells up to <paramref name="want"/> copies of an item from the pack.
    /// The "Sell Quantity" button. Copies are matched by id, so five identical
    /// health draughts sell as five.</summary>
    public int SellMany(Item item, int want)
    {
        if (Buyer is not { } b) return 0;
        var sold = 0;
        for (var i = 0; i < want; i++)
        {
            var match = b.Bag.Items.FirstOrDefault(x => x.Id == item.Id);
            if (match is null || !Sell(match)) break;
            sold++;
        }
        if (sold > 0) Notice = $"Sold {sold}× {item.Name}.";
        Changed?.Invoke();
        return sold;
    }

    /// <summary>Puts stock onto a merchant's shelf — used when the player sells to
    /// them, and the hook a future restock loop would call. Stacks onto an existing
    /// shelf line of the same item rather than adding a second one.</summary>
    private void Restock(string npcId, Item item, int quantity)
    {
        EnsureShelf(npcId);
        var shelf = _shelves[npcId];
        if (shelf.FirstOrDefault(sl => sl.Item.Id == item.Id) is { } existing) existing.Quantity += quantity;
        else shelf.Add(new Slot(item, quantity));
    }

    private void ClearDetailKeepScreen() { Detail = null; DetailFromShop = false; }

    public void Close()
    {
        if (Open is null) return;
        Open = null;
        Detail = null; Notice = null;
        MerchantBridge.Close();      // release the hero the engine was holding
        Changed?.Invoke();
    }
}
