namespace AlfarQuest.Client.Game.Tiled;

/// <summary>The maps that have been migrated to Tiled, parsed and held for the
/// engine to build from.
///
/// The world is built synchronously, but a .tmx has to be fetched over HTTP, so it
/// cannot be read at build time. Instead the page fetches and registers a map
/// before the world is created (see Play.razor.cs), and the builder asks here.
///
/// A map that is not registered is simply not migrated. The overworld is the ring
/// of regions below; the cave and the interiors are their own maps. A region that
/// fails to register drops the game onto a bare fallback world (World.BuildOverworld)
/// — a place to stand, not the procedural Stage 1 that used to live there, which
/// has been retired.</summary>
public static class MapCatalog
{
    /// <summary>The maps fetched and registered at startup, as (id, path under
    /// wwwroot). An interior's id is its <see cref="InteriorDef"/> id, so the
    /// builder can simply ask whether the interior it is about to build has a map.</summary>
    public static readonly (string Id, string Path)[] Migrated =
    [
        ("cleric_house", "Maps/Interiors/ClericHouse_Ground.tmx"),
        ("cleric_house_upper", "Maps/Interiors/ClericHouse_Upper.tmx"),
        ("mage_school", "Maps/Interiors/MageSchool.tmx"),
        ("seoshe", "Maps/Interiors/Seoshe.tmx"),
        ("thieves_warehouse", "Maps/Interiors/ThievesWarehouse.tmx"),
        (Cave, "Maps/Cave/Cave_Descent.tmx"),
    ];

    /// <summary>The cave's map id. One map serves every depth: the layout is the
    /// same each time and only the population changes.</summary>
    public const string Cave = "cave";

    /// <summary>The hand-authored overworld REGIONS that will replace Stage 1.
    ///
    /// Kept apart from <see cref="Migrated"/> on purpose: a region registers and
    /// can be walked and judged, but it does not become the overworld. Stage 1
    /// stays on its own map until the ring of regions is closed and the cave is
    /// reachable again, because half a world is not a world and the game has to
    /// stay finishable between iterations.</summary>
    public static readonly (string Id, string Path)[] Regions =
    [
        ("r1_ashwold", "Maps/Regions/R1_Ashwold.tmx"),
        ("r2_whispering_wood", "Maps/Regions/R2_WhisperingWood.tmx"),
        ("r3_deepdelve", "Maps/Regions/R3_Deepdelve.tmx"),
        ("r4_kae_ychel_road", "Maps/Regions/R4_KaeYchelRoad.tmx"),
    ];

    static readonly Dictionary<string, TmxMap> Maps = [];

    /// <summary>Parses and registers a map. Bad map data must never take the game
    /// down with it: a map that fails to parse is left unregistered, so the stage
    /// falls back (a region to the bare fallback world, the cave/interior to their
    /// own builders) and the player still gets a world.</summary>
    public static bool Register(string id, string xml)
    {
        try
        {
            Maps[id] = TmxMap.Parse(xml);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"map '{id}' failed to parse, falling back: {ex.Message}");
            Maps.Remove(id);
            return false;
        }
    }

    public static TmxMap? Find(string id) => Maps.GetValueOrDefault(id);

    public static bool Has(string id) => Maps.ContainsKey(id);

    /// <summary>Test seam: forget every registered map, so a probe can prove the
    /// fallback world still produces a playable stage.</summary>
    public static void Clear() => Maps.Clear();
}
