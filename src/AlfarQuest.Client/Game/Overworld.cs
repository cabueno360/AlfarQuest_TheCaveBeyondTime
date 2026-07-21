namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 1 — "The Approach". Orchestration and the shared
//  placement helpers; each concern is a partial alongside.
// =====================================================================
public partial class World
{
    public const int OW_COLS = 80, OW_ROWS = 80;
    public const string OverworldName = "The Approach";

    // Where the mine mouth sits, and how close you must get to be taken in.
    public Vec CaveMouth;

    const float MouthRadius = 46f;

    void BuildOverworld()
    {
        Rev++;
        Stage = 1;
        COLS = OW_COLS; ROWS = OW_ROWS;
        Tiles = new byte[COLS, ROWS];
        Props.Clear(); Arches.Clear(); Crystals.Clear(); Husks.Clear();

        // Everything starts as open grass; the blocking is carved in after.
        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
                Tiles[x, y] = FLOOR;

        BuildMountain();      // ZONE F — northern barrier
        BuildRiver();         // ZONE C — river + the one bridge
        BuildRoads();         // the dirt route linking every zone
        BuildForest();        // ZONE B
        BuildSpawnCamp();     // ZONE A
        BuildCrossroad();     // ZONE D
        BuildMiningCamp();    // ZONE E
        BuildCaveEntrance();  // ZONE G
        BuildSecrets();       // waterfall cache + forest hollow
        BuildOptional();      // pond, crystal field, abandoned wagon
        BuildLandmarks();     // one memorable thing per area
        BuildStories();       // what happened here, told in objects
        PlaceNpcs();          // the village, and the last post before the graves
        SpawnWildlife(64);    // ZONE-appropriate creatures, never inside a safe zone
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

    void AddOw(float tx, float ty, string kind, float scale, bool solid, float radius)
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
