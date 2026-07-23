namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 1 — "The Approach". Orchestration and the shared
//  placement helpers; each concern is a partial alongside.
// =====================================================================
public partial class World
{
    // Four times the original 80×80, and wider than it is tall on purpose — the
    // world is meant to open out east toward Kae Ychel and south toward Seoshe,
    // not just run deeper north into the mountains. The original camp, crossroad,
    // mine and cave keep their coordinates; everything past x72 / y80 is new.
    public const int OW_COLS = 200, OW_ROWS = 128;
    public const string OverworldName = "The Approach";

    // Where the mine mouth sits, and how close you must get to be taken in.
    public Vec CaveMouth;

    const float MouthRadius = 46f;

    void BuildOverworld()
    {
        // Stage 1 is authored in Tiled. If its map is registered, it is the source
        // of truth and everything below is skipped; the generator underneath stays
        // as the fallback, so a missing or broken map still yields a playable world
        // and the cave and interiors are untouched by the migration.
        // See Overworld.Tmx.cs and docs/mapping-standard.md.
        if (Tiled.MapCatalog.Find(Tiled.MapCatalog.Stage01) is { } authored)
        {
            BuildOverworldFromTmx(authored);
            return;
        }

        Rev++;
        Stage = 1;
        COLS = OW_COLS; ROWS = OW_ROWS;
        Tiles = new byte[COLS, ROWS];
        Props.Clear(); Arches.Clear(); Crystals.Clear(); Husks.Clear(); Portals.Clear(); Examinables.Clear(); Reading = null;

        // Everything starts as open grass; the blocking is carved in after.
        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
                Tiles[x, y] = FLOOR;

        BuildMountain();      // ZONE F — northern barrier, now the full width
        BuildRiver();         // ZONE C — river down to the southern lowlands
        BuildRoads();         // the dirt route + the east and coast roads
        BuildForest();        // ZONE B — the original wood
        BuildEastVegetation();   // the drying scrub of the Kae Ychel road
        BuildSouthVegetation();  // the wet lowland forest toward the coast
        BuildSpawnCamp();     // ZONE A
        BuildCrossroad();     // ZONE D
        BuildMiningCamp();    // ZONE E
        BuildCaveEntrance();  // ZONE G
        BuildSecrets();       // waterfall cache + forest hollow
        BuildOptional();      // pond, crystal field, abandoned wagon
        BuildLandmarks();     // one memorable thing per area
        BuildClericHamlet();  // the first enterable building — opens onto its own map
        BuildEastReach();     // the road to Kae Ychel: caravan, ruins, watchtower
        BuildSouthReach();    // the road to Seoshe: fishing steps, stones, smugglers
        BuildMageSchool();    // the Academy outpost courtyard on the east road
        BuildSeosheGate();    // the gate into Seoshe, on the coast road
        BuildStories();       // what happened here, told in objects
        PlaceNpcs();          // the village, and the last post before the graves
        SpawnWildlife(180);   // ZONE-appropriate creatures, never inside a safe zone
        PlaceOverworldRewards();  // zones to find, chests to open, seams to mine

        Spawn = TileCentre(13, 70);
        Exit = CaveMouth;
        Camera = Spawn;
    }

    // ---- helpers ----------------------------------------------------
    bool InRect(float x, float y, float x0, float y0, float x1, float y1) =>
        x >= x0 && x <= x1 && y >= y0 && y <= y1;

    bool OpenGround(float tx, float ty)
    {
        int x = (int)tx, y = (int)ty;
        if (x < 1 || y < 1 || x >= COLS - 1 || y >= ROWS - 1) return false;
        return Tiles[x, y] == FLOOR;      // never on road, water, bridge or rock
    }

    bool NearRoad(float tx, float ty, float rad)
    {
        for (int x = (int)(tx - rad); x <= tx + rad; x++)
            for (int y = (int)(ty - rad); y <= ty + rad; y++)
            {
                if (x < 0 || y < 0 || x >= COLS || y >= ROWS) continue;
                var t = Tiles[x, y];
                if (t == PATH || t == BRIDGE) return true;
            }
        return false;
    }

    public void AddOw(float tx, float ty, string kind, float scale, bool solid, float radius)
    {
        Props.Add(new Prop
        {
            X = tx * TILE + TILE * 0.5f,
            Y = ty * TILE + TILE * 0.5f,
            Kind = kind,
            Variant = _rng.Next(64),        // client wraps this into the kind's list
            S = scale,
            Solid = solid,
            R = radius,
            Flip = _rng.Next(2) == 0,
        });
    }
}
