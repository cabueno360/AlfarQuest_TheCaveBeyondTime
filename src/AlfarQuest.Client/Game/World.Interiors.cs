namespace AlfarQuest.Client.Game;

// =====================================================================
//  Building interiors and the doorways between them.
//
//  A door is a Portal: a point on one map that, when used, swaps the whole
//  world for another and sets the party down at the far side. It reuses the
//  same machinery as EnterCave — change the tiles, bump Rev, reposition the
//  party — so the renderer transitions for free, and the interior is just a
//  small map like any other. Leaving rebuilds the overworld from its seed
//  (deterministic, so the village is exactly as it was) and drops the party
//  back on the doorstep.
// =====================================================================
public partial class World
{
    /// <summary>A doorway. On the overworld it names the interior to load; inside
    /// an interior its target is <see cref="Overworld"/>, which sends you back
    /// out to <see cref="Return"/> — the spot just outside the door you came in by.</summary>
    public sealed class Portal
    {
        public Vec Pos;
        public float R = 40f;
        public required string Target;   // an interior id, or Portal.Overworld
        public string Label = "the door";
        public string Verb = "Enter";    // Enter a place, Go up/down stairs, Step outside
        public Vec Return;               // where a door from the world drops you back
        public Vec? Arrive;              // where a stair or inner door sets you down

        public const string Overworld = "__overworld__";
    }

    public List<Portal> Portals { get; } = [];

    /// <summary>The interior currently loaded, or null when out in the world or the
    /// cave. Interiors are Stage 3 — the renderer already lights and stone-floors
    /// anything that is not Stage 1.</summary>
    public string? CurrentInterior { get; private set; }
    public bool IsInterior => CurrentInterior is not null;

    /// <summary>Where leaving the current interior puts the party — the doorstep it
    /// was entered from, so stepping in and back out never moves you.</summary>
    Vec _interiorReturn;

    /// <summary>The current interior's display name, from its map's DisplayName
    /// property — what the HUD's corner panel and the title card announce.</summary>
    string _interiorName = "Indoors";

    /// <summary>Whether the current interior is open-air (a city's streets) and
    /// should be lit as daylight rather than as a dungeon.</summary>
    bool _interiorOutdoor;

    /// <summary>The floor material the current interior asks for — "wood" for a home,
    /// empty for the default stone. Read by the renderer when it paints the floor.</summary>
    string _interiorFloor = "";

    /// <summary>Whether the world should be drawn with the outdoor look — always
    /// on the overworld, and in interiors that declare themselves open-air.</summary>
    public bool OutdoorLook => IsInterior ? _interiorOutdoor : Stage == 1;

    /// <summary>The floor style the renderer should lay down — only interiors set it;
    /// the overworld and the cave use their own ground.</summary>
    public string FloorStyle => IsInterior ? _interiorFloor : "";

    /// <summary>Which authored map is loaded, or empty for a place that has none
    /// (the cave). The renderer uses it to draw the same .tmx the engine built
    /// from, so picture and geometry can never drift apart.</summary>
    public string MapId => IsInterior ? CurrentInterior ?? ""
                         : Stage == 1 ? CurrentRegion ?? StartRegion
                         : Stage == 2 ? Tiled.MapCatalog.Cave : "";

    /// <summary>The doorway within reach of the steered hero, or null. Recomputed
    /// each frame beside the NPC check so the prompt and the key never disagree.</summary>
    public Portal? PortalInReach { get; private set; }

    void UpdatePortals()
    {
        PortalInReach = null;
        if (Party.Count == 0 || Busy) return;
        var hero = Party[Active];
        var best = float.MaxValue;
        foreach (var p in Portals)
        {
            var d = (p.Pos - hero.Pos).Len();
            if (d < p.R && d < best) { best = d; PortalInReach = p; }
        }
    }

    /// <summary>Steps through a doorway — a door from the world into a building, a
    /// stair between its floors, or the way back out. Called from Interact when a
    /// portal is the thing in reach.</summary>
    public void UsePortal(Portal p)
    {
        if (p.Target == Portal.Overworld)
        {
            if (Stage == 2) LeaveCave();     // out of the DELVE — back to the region, not a doorstep
            else ExitInterior();             // out of a building — back to the doorstep
        }
        else if (p.Target == Tiled.MapCatalog.Cave) TryDescend();  // the mouth — the light first, then down
        else if (IsInterior) SwitchInterior(p.Target, p.Arrive);   // a stair between floors
        else
        {
            // A door from the world. The doorstep to come back OUT to is the
            // door's own DestinationSpawn when the map set one — and where the
            // hero is STANDING when it did not. Every region door ships with
            // "0.0,0.0" today, which used to be taken literally: leaving any
            // building dropped the party at the map's top-left corner.
            var back = p.Return.X != 0 || p.Return.Y != 0
                ? p.Return
                : Party.Count > 0 ? Party[Active].Pos : p.Pos;
            EnterInterior(p.Target, back);
        }
    }

    /// <summary>Enters a building from the overworld, remembering the doorstep to
    /// return to. The world is not kept in memory — leaving rebuilds it from seed.</summary>
    public void EnterInterior(string id, Vec returnTo)
    {
        _interiorReturn = returnTo;
        LoadInterior(id, null);
    }

    /// <summary>Moves between floors of the same building — up the stairs, down
    /// again — without disturbing where the front door will let you back out.</summary>
    public void SwitchInterior(string id, Vec? arriveAt) => LoadInterior(id, arriveAt);

    /// <summary>Throws away the current map and lays down an interior, standing the
    /// party at <paramref name="arriveAt"/> or, if none is given, the map's own
    /// entry point.
    ///
    /// The map IS the interior now. The C# room builders are retired: a missing or
    /// unparsed map leaves the door doing nothing — and says why in the console —
    /// rather than silently standing up geometry that no longer matches the
    /// picture. That visibility is the point of authoring in Tiled: a broken map
    /// should be fixed in Tiled, not papered over by code.</summary>
    void LoadInterior(string id, Vec? arriveAt)
    {
        if (Tiled.MapCatalog.Find(id) is not { } map)
        {
            Console.Error.WriteLine($"interior '{id}' has no registered map — the door does nothing. Is it in Maps/manifest.json?");
            return;
        }

        var from = CurrentInterior;      // for the reciprocal-door arrival below
        CurrentInterior = id;
        Stage = 3;
        Rev++;
        Phase = "playing";
        Talking = null; TradingWith = null;

        Props.Clear(); Npcs.Clear(); Portals.Clear(); Examinables.Clear(); Reading = null;
        // The overworld's chests and region markers must not leak into a building
        // whose tiles happen to sit where they stood. Leaving rebuilds the overworld
        // (and its rewards) from seed.
        Interactables.Clear(); Discoveries.Clear();
        Crystals.Clear(); Husks.Clear(); Shots.Clear(); Bolts.Clear(); Slashes.Clear(); Aoe.Clear(); Fx.Clear();

        // The place describes itself: its name, whether it is open-air and what
        // its floor is made of are map properties, not rows in a C# table.
        _interiorName = map.Property("DisplayName", id);
        _interiorOutdoor = map.Property("Outdoor") == "true";
        _interiorFloor = map.Property("FloorTile");

        BuildInteriorFromTmx(map);

        // The save says what this player has already taken from this room. Only
        // the overworld build did this, so stepping out and back in rebuilt every
        // interior chest full — a loot and XP farm the width of a doorway.
        ApplyClaims();

        // Where to stand. An explicit arrival wins; otherwise a floor reached
        // from another floor sets you down at ITS door back to where you came
        // from — the foot of the stair you just took, not the front door the
        // map's PlayerSpawn marks. Tiled warps never carry an Arrive, so
        // without this every stair in the Cleric's house (and every door back
        // into Seoshe's streets) dropped the party at the building's entrance.
        var at = arriveAt
            ?? (from is not null && Portals.FirstOrDefault(q => q.Target == from) is { } back
                ? back.Pos + new Vec(0, TILE * 0.75f)
                : Spawn);
        PlaceParty(at);
        Camera = at;
    }

    /// <summary>Leaves the current interior, rebuilding the overworld and dropping
    /// the party back on the doorstep. Rewards and opened containers persist through
    /// the RewardBridge, so the world is as it was left, not freshly stocked.</summary>
    public void ExitInterior()
    {
        var back = _interiorReturn;
        var region = CurrentRegion;
        CurrentInterior = null;
        // Come back out into the world you went in from. BuildOverworld only ever
        // knows Stage 1, so a door entered from a region used to put the party
        // down on the OLD map at the region's coordinates — the picture and the
        // ground disagreeing, with nothing to say so.
        if (region is null || !StandRegion(region))
            BuildOverworld();  // deterministic; also bumps Rev and clears entities
        // A doorstep that was never set (a debug entry, an old save) must not
        // strand the party at the map's corner — the region's spawn will do.
        if (back.X == 0 && back.Y == 0) back = Spawn;
        PlaceParty(back);
        Camera = back;
    }

    /// <summary>Sets the party down around a point, tidily abreast and never inside
    /// a wall. Movement/cooldowns are left untouched — walking through a door is not
    /// a new life the way descending into the cave is.</summary>
    void PlaceParty(Vec at)
    {
        for (int i = 0; i < Party.Count; i++)
        {
            var p = at + new Vec((i - 1) * 34f, 0);
            if (Blocked(p, 14f)) p = NearestOpen(p);
            Party[i].Pos = p;
        }
    }

    // ---- helpers the interior builders use --------------------------

    /// <summary>Carves a floor rectangle out of the solid interior block — the room
    /// you stand in, with the surrounding rock left as its walls.</summary>
    public void Room(int x0, int y0, int x1, int y1)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                if (x >= 0 && y >= 0 && x < COLS && y < ROWS) Tiles[x, y] = FLOOR;
    }

    /// <summary>Stamps a rock partition back into a carved room — an interior wall
    /// between two spaces. Leave a gap in the run for a doorway.</summary>
    public void Wall(int x0, int y0, int x1, int y1)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                if (x >= 0 && y >= 0 && x < COLS && y < ROWS) Tiles[x, y] = ROCK;
    }

    /// <summary>Lays a paved street (rendered as the outdoor path) — for a city's
    /// open-air interior, where rock is a building and this is the way between.</summary>
    public void Street(int x0, int y0, int x1, int y1)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                if (x >= 0 && y >= 0 && x < COLS && y < ROWS) Tiles[x, y] = PATH;
    }

    /// <summary>Fills water — a harbour, a dock, the crescent shore.</summary>
    public void Water(int x0, int y0, int x1, int y1)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                if (x >= 0 && y >= 0 && x < COLS && y < ROWS) Tiles[x, y] = WATER;
    }

    /// <summary>Places a catalogued NPC at a tile — for people who live indoors, like
    /// Mirka in her sickroom, who never appear on the overworld placement table.</summary>
    public void AddNpc(string id, float tx, float ty)
    {
        if (NpcCatalog.Find(id) is { } def) Npcs.Add(new Npc(def, TileCentre(tx, ty)));
    }

    /// <summary>Places a Cleric's-house furniture/decor/story sprite — its own PNG,
    /// drawn whole by the renderer as kind "h_&lt;name&gt;" (see HOUSE_OBJ in atlas.js).
    /// Furniture passes solid=true with a collision radius; small decor passes false.
    /// No random flip or variant, so a bed or a portrait always sits as it was drawn.</summary>
    public void AddHouseObj(float tx, float ty, string name, float scale, bool solid, float radius, bool flip = false)
    {
        Props.Add(new Prop
        {
            X = tx * TILE + TILE * 0.5f,
            Y = ty * TILE + TILE * 0.5f,
            Kind = "h_" + name,
            S = scale, Solid = solid, R = radius, Flip = flip,
        });
    }

    /// <summary>A stair between floors of the same building: it carries you to
    /// <paramref name="arrive"/> on the target floor, drawn as a ladder.</summary>
    public void AddStair(float tx, float ty, string target, Vec arrive, string label)
    {
        Portals.Add(new Portal { Pos = TileCentre(tx, ty), Target = target, Label = label, Arrive = arrive, R = 42f, Verb = "Go" });
        AddOw(tx, ty, "ladder", 0.7f, false, 0f);
    }

    /// <summary>Adds a doorway between the world and a building: a door from the
    /// overworld reads "Enter …", the way back out reads "Step …".</summary>
    public void AddPortal(float tx, float ty, string target, string label, Vec ret, string? doorProp = "caveEntrance")
    {
        var pos = TileCentre(tx, ty);
        Portals.Add(new Portal { Pos = pos, Target = target, Label = label, Return = ret, R = 42f,
                                 Verb = target == Portal.Overworld ? "Step" : "Enter" });
        if (doorProp is not null) AddOw(tx, ty, doorProp, 0.8f, false, 0f);
    }

    /// <summary>A door between two interiors — a city and a building within it —
    /// carrying you to <paramref name="arrive"/> on the far map. Reads "Enter …".</summary>
    public void AddDoor(float tx, float ty, string target, Vec arrive, string label, string? doorProp = "caveEntrance")
    {
        Portals.Add(new Portal { Pos = TileCentre(tx, ty), Target = target, Label = label, Arrive = arrive, R = 42f, Verb = "Enter" });
        if (doorProp is not null) AddOw(tx, ty, doorProp, 0.8f, false, 0f);
    }
}
