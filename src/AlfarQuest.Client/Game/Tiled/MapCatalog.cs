namespace AlfarQuest.Client.Game.Tiled;

/// <summary>The maps that have been migrated to Tiled, parsed and held for the
/// engine to build from.
///
/// The world is built synchronously, but a .tmx has to be fetched over HTTP, so it
/// cannot be read at build time. Instead the page fetches and registers a map
/// before the world is created (see Play.razor.cs), and the builder asks here.
///
/// A map that is not registered is simply not migrated: the engine falls back to
/// its old generator for that stage. That is what keeps the migration incremental —
/// Stage 1 loads from Tiled, the cave and the interiors keep working as they were,
/// and nothing needs a flag to say so.</summary>
public static class MapCatalog
{
    /// <summary>The id of Stage 1's map — the file under wwwroot/Maps/Outside.</summary>
    public const string Stage01 = "Stage01_Outside";

    /// <summary>The maps that have been migrated, as (id, path under wwwroot). An
    /// interior's id is its <see cref="InteriorDef"/> id, so the builder can simply
    /// ask whether the interior it is about to build has a map.</summary>
    public static readonly (string Id, string Path)[] Migrated =
    [
        (Stage01, "Maps/Outside/Stage01_Outside.tmx"),
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

    static readonly Dictionary<string, TmxMap> Maps = [];

    /// <summary>Parses and registers a map. Bad map data must never take the game
    /// down with it: a map that fails to parse is left unregistered, so the stage
    /// builds from the old generator and the player still gets a world.</summary>
    public static bool Register(string id, string xml)
    {
        try
        {
            Maps[id] = TmxMap.Parse(xml);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"map '{id}' failed to parse, falling back to the generator: {ex.Message}");
            Maps.Remove(id);
            return false;
        }
    }

    public static TmxMap? Find(string id) => Maps.GetValueOrDefault(id);

    public static bool Has(string id) => Maps.ContainsKey(id);

    /// <summary>Test seam: forget every registered map, so a probe can prove the
    /// generator fallback still produces a playable stage.</summary>
    public static void Clear() => Maps.Clear();
}
