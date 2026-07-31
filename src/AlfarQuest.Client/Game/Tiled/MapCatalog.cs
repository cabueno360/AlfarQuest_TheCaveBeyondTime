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
    /// <summary>The cave's map id. One map serves every depth: the layout is the
    /// same each time and only the population changes.</summary>
    public const string Cave = "cave";

    // WHICH maps exist lives in wwwroot/Maps/manifest.json now — one list that
    // the engine (Play.razor.cs) and the renderer (game.js) both read, so adding
    // a map is a Tiled save plus one JSON line, never a code edit in two
    // languages that have to agree.

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
