using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

/// <summary>The levers of a deeper merchant economy — merchant purses, supply and
/// demand, reputation and faction discounts, seasonal and event pricing.
///
/// Every one of them is off. The brief asks for the architecture, not the system:
/// a shop today sells at list price and buys at a flat fraction, and a merchant's
/// coin is bottomless. What this class buys is a single, honest place for that to
/// change — the price the counter charges already runs through <see cref="BuyPrice"/>
/// and <see cref="SellPrice"/>, so turning a lever on is editing a multiplier here,
/// not chasing the arithmetic through the shop.</summary>
public static class ShopEconomy
{
    /// <summary>Merchants would run out of coin and could refuse a sale they cannot
    /// pay for. Off: a merchant's purse is treated as bottomless, so a sale never
    /// fails for want of their gold.</summary>
    public static bool LimitedMerchantGold { get; set; }

    /// <summary>Prices would drift with how much of a thing a merchant holds —
    /// cheaper when overstocked, dearer when scarce. Off: list price is list price.</summary>
    public static bool SupplyAndDemand { get; set; }

    /// <summary>Charisma, reputation and faction standing would cut the buy price
    /// and lift the sell price. Off: everyone pays and is paid the same.</summary>
    public static bool ReputationDiscounts { get; set; }

    /// <summary>Festival and event sales would apply a blanket markdown. Off.</summary>
    public static bool EventSales { get; set; }

    /// <summary>What a merchant is assumed to be able to pay, when
    /// <see cref="LimitedMerchantGold"/> is off. The wider system would track a real
    /// purse per merchant; until then the shop reads this and never blocks a sale.</summary>
    public const long BottomlessPurse = long.MaxValue;

    /// <summary>The price the buyer actually pays, after every discount that is
    /// switched on. With all levers off this is exactly the item's list price, so
    /// the numbers a player sees match the catalogue.</summary>
    public static int BuyPrice(Item item, Models.Character? buyer, Merchant merchant) =>
        Adjust(item.BuyPrice, BuyMultiplier(buyer, merchant));

    /// <summary>The price the merchant pays for what the player sells — the item's
    /// sell price, after any bonus that is switched on. All off: the flat fraction.</summary>
    public static int SellPrice(Item item, Models.Character? buyer, Merchant merchant) =>
        Adjust(item.SellPrice, SellMultiplier(buyer, merchant));

    /// <summary>The buy-side multiplier: below 1 is a discount. Always 1 while the
    /// levers are off; the one place a reputation or event system would hook in.</summary>
    private static float BuyMultiplier(Models.Character? buyer, Merchant merchant)
    {
        var m = 1f;
        // if (ReputationDiscounts) m -= StandingWith(buyer, merchant) * 0.2f;
        // if (EventSales)          m -= 0.15f;
        return m;
    }

    /// <summary>The sell-side multiplier: above 1 is a better payout.</summary>
    private static float SellMultiplier(Models.Character? buyer, Merchant merchant)
    {
        var m = 1f;
        // if (ReputationDiscounts) m += StandingWith(buyer, merchant) * 0.1f;
        return m;
    }

    private static int Adjust(int price, float multiplier) =>
        Math.Max(1, (int)MathF.Round(price * multiplier));
}
