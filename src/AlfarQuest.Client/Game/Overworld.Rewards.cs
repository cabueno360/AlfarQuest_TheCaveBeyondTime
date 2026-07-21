using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 1 — where the experience is, and what there is to open.
//
//  A container and the art that stands for it are placed by the same
//  call. Registering an interactable at a spot and hoping a prop is
//  already there is how a chest ends up invisible, and it happened
//  before: the rewards were pinned to props placed elsewhere in the
//  build, and moving one silently orphaned the other.
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

        // ---- Zone A, Ashwold Camp: the tutorial's worth of things -----
        // Deliberately ordinary and close together. The first minute should
        // teach that objects are worth pressing E on, and cheap containers
        // that are sometimes empty teach it better than a guaranteed prize.
        Put("Camp Barrel",        11.5f, 68f,   "barrel");
        Put("Camp Crate",         15.5f, 69f,   "crate");
        Put("Ashwold Stores",     12.5f, 72.5f, "crate");
        Put("Cook's Barrel",      14.5f, 66.5f, "barrel");
        Put("Camp Herbs",         10f,   71f,   "herbs");
        Put("Wayfarer's Stall",   16.5f, 71.5f, "stall");
        Put("Ashwold Well",       17.5f, 67f,   "well");

        // ---- Zone B, the wood: what the trees hide -------------------
        Put("Forest Herbs",       20f,   52f,   "herbs");
        Put("Hollow Log Cache",   25.5f, 50f,   "crate");
        Put("Poacher's Barrel",   19f,   45f,   "barrel");
        Put("Lost Delver",        27f,   55f,   "corpse");
        Put("Wood Herbs",         24f,   58f,   "herbs");
        Put("Shrine of the Wood", 6.5f,  44f,   "altar");

        // ---- the secrets: the reward for pushing through --------------
        Put("Waterfall Cache",     9.5f, 19.5f, "chest_gold");
        Put("Sunken Crystal",      8f,   19f,   "crystal");
        Put("Hollow Crystal",     20f,   35.5f, "crystal");
        Put("Hollow Strongbox",   18.5f, 36f,   "chest_iron");
        Put("Weathered Stone",    17f,   32.5f, "runestone");

        // ---- Zone C and D, the ford and the crossroad ----------------
        Put("Ford Barrel",        36f,   41f,   "barrel");
        Put("Drowned Delver",     39.5f, 38f,   "corpse");
        Put("Wagon Strongbox",    34f,   27f,   "chest_wood");
        Put("Crossroad Marker",   44f,   33f,   "runestone");
        Put("Crossroad Statue",   45.5f, 34.5f, "statue");
        Put("Toppled Crate",      42f,   31f,   "crate");

        // ---- Zone E, the mining camp: the working world --------------
        Put("Deepdelve Seam",     51.4f, 18.2f, "ore");
        Put("Second Seam",        53f,   21f,   "ore");
        Put("Miner's Strongbox",  48.6f, 18f,   "chest_iron");
        Put("Loaded Cart",        50f,   22.5f, "cart");
        Put("Spoil Barrel",       47f,   21f,   "barrel");
        Put("Camp Stall",         52.5f, 24f,   "stall");
        Put("Deepdelve Well",     49f,   24.5f, "well");

        // ---- Zone F and G, the pass and the mine mouth ---------------
        // The last stretch is where the good and the guarded things are.
        Put("Pass Bones",         60f,   14f,   "corpse_old");
        Put("Cairn Bones",        58f,   11f,   "corpse_old");
        Put("Pass Seam",          61.5f, 16f,   "ore");
        Put("The Warden's Box",   63f,   10.5f, "chest_locked");
        Put("Mouth Altar",        65f,    9.5f, "altar");
        Put("Watcher of the Mouth", 66f,  11f,  "statue");
        Put("Old Cart",           62f,   13f,   "cart");
        Put("Deep Chest",         64.5f,  7f,   "chest_ancient");

        // Last: whatever this player already opened stays opened, and whatever
        // was left inside is still inside.
        ApplyClaims();
    }

    /// <summary>Places a container and the art that stands for it, together.
    ///
    /// The prop comes from the kind, so the thing on screen and the thing you can
    /// press E on can never disagree about what they are. Solid and radius are
    /// left at zero: these are meant to be walked up to, and a chest you cannot
    /// stand next to is a chest you cannot open.</summary>
    void Put(string name, float tx, float ty, string kindId)
    {
        var kind = ContainerKind.Find(kindId);
        if (kind is null) return;          // an unknown id places nothing rather than throwing

        var pos = new Vec(tx * TILE + TILE * 0.5f, ty * TILE + TILE * 0.5f);
        AddOw(tx, ty, kind.Prop, 0.75f, false, 0f);
        Interactables.Add(new Interactable(name, pos, kind));
    }
}
