using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 1 — where the experience is.
//
//  Every position here sits on a prop placed in Overworld.Detail.cs or
//  Overworld.Zones.cs, so the reward is attached to something the player
//  can already see. Rewards floating in empty grass would be a scavenger
//  hunt for invisible pixels.
// =====================================================================
public partial class World
{
    void PlaceOverworldRewards()
    {
        Discoveries.Clear();
        Interactables.Clear();

        // ---- the seven zones ----------------------------------------
        // Walking into one names it and pays. The radius is generous: this
        // should fire while crossing, not require standing on a spot.
        foreach (var (name, tx, ty, tiles) in new[]
        {
            ("Ashwold Camp",        13f, 70f, 7f),
            ("The Whispering Wood", 22f, 48f, 9f),
            ("The Grey Ford",       38f, 40f, 7f),
            ("The Crossroad",       44f, 33f, 6f),
            ("Deepdelve Camp",      50f, 20f, 8f),
            ("The Riven Pass",      62f, 12f, 8f),
            ("The Mine Mouth",      64f,  8f, 6f),
        })
            Discoveries.Add(new Discovery(name, TileCentre((int)tx, (int)ty), tiles * TILE,
                                          XpSource.RegionDiscovered));

        // ---- the two secrets ----------------------------------------
        // Worth more than a zone: nothing points at these.
        Discoveries.Add(new Discovery("Behind the Waterfall", TileCentre(8, 18), 3.2f * TILE, XpSource.SecretArea));
        Discoveries.Add(new Discovery("The Forest Hollow", TileCentre(18, 34), 3.2f * TILE, XpSource.SecretArea));

        // ---- things to open, mine and read --------------------------
        // Each pairs with a prop already standing at that spot.
        Add("Waterfall Cache",  9.5f, 19.5f, XpSource.TreasureChest, "Small Crystal", 3);
        Add("Sunken Crystal",   8.0f, 19.0f, XpSource.RareCrystal,   "Small Crystal", 2);
        Add("Hollow Crystal",  20.0f, 35.5f, XpSource.RareCrystal,   "Small Crystal", 2);
        Add("Shrine Relic",     6.5f, 44.0f, XpSource.Relic,         "Coins", 75);
        Add("Weathered Stone", 17.0f, 32.5f, XpSource.AncientTablet);
        Add("Deepdelve Seam",  51.4f, 18.2f, XpSource.OreVein,       "Stone", 4);
        Add("Miner's Strongbox", 48.6f, 18.0f, XpSource.TreasureChest, "Coins", 40);
        Add("Wagon Strongbox", 34.0f, 27.0f, XpSource.TreasureChest, "Coins", 30);
        Add("Crossroad Marker", 44.0f, 33.0f, XpSource.AncientTablet);

        // Last: whatever this player already found stays found.
        ApplyClaims();
    }

    /// <summary>Props sit at fractional tile positions, so the world position is
    /// computed the same way AddOw computes theirs. Rounding to a whole tile here
    /// would leave the reward up to half a tile off the thing it belongs to.</summary>
    void Add(string name, float tx, float ty, XpSource source, string? loot = null, int count = 1) =>
        Interactables.Add(new Interactable(
            name,
            new Vec(tx * TILE + TILE * 0.5f, ty * TILE + TILE * 0.5f),
            source, loot, count));
}
