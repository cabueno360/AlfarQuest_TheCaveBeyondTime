namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 2 — the cave. The region graph and the links between
//  them; carving and dressing are partials alongside.
// =====================================================================
public partial class World
{
    // Region names are the descent's flavour text. Past the end of the list the
    // delve keeps going and the depth is just numbered.
    static readonly string[] RegionNames =
    {
        "The Crystal Cistern", "The Great Coral Tree", "The Weeping Gallery",
        "The Sunken Vault", "The Mirror Halls", "The Cave Beyond Time",
    };

    /// <summary>The other shore. The Diver's crossing from the Cistern lands
    /// here, exactly as Kazzat promised — the one living place in the dead sea.
    /// The depth reuses the delve's rooms but renames its geography, keeps its
    /// own bestiary and keeper, and trades the cold blue wash for the Tree's
    /// warmth.</summary>
    public const int CoralDepth = 2;
    bool OnCoralDepth => Stage == 2 && Level == CoralDepth;

    /// <summary>What the delve's rooms are called on the Tree's shore. Keyed by
    /// the same room keys every depth shares; discovery XP announces these
    /// instead of the mine-names that would make no sense under the sea.</summary>
    static readonly Dictionary<string, string> CoralRooms = new()
    {
        ["entrance"]  = "The Diver's Berth",
        ["mine"]      = "The Polyp Shoals",
        ["tunnels"]   = "The Bone Reefs",
        ["lake"]      = "The Still Lagoon",
        ["crystal"]   = "The Blooming Terraces",
        ["ruins"]     = "The Drowned Fleet",
        ["sanctuary"] = "The Roots of the Tree",
        ["boss"]      = "The Heartwood",
    };

    /// <summary>On the coral shore, every room wears its sea-name. Runs after the
    /// rooms are known (map-read or generator) and before rewards are placed, so
    /// the discoveries announce the renamed geography.</summary>
    void ApplyCoralNames()
    {
        if (!OnCoralDepth) return;
        _caveRegions = CaveRegions
            .Select(r => CoralRooms.TryGetValue(r.Key, out var name) ? r with { Name = name } : r)
            .ToList();
    }

    // Stage 1 is one named place; an interior is named by its own map's
    // DisplayName property; the cave numbers its depths.
    public string RegionName =>
        IsInterior ? _interiorName
        : Stage == 1 ? (CurrentRegion is null ? OverworldName : RegionTitle)
        : Level <= RegionNames.Length ? RegionNames[Level - 1] : $"The Deep — level {Level}";

    // =================================================================
    //  World building
    //
    //  Hand-authored, not generated. The regions below are placed at fixed
    //  coordinates in a deliberate order — Entrance, mine, tunnels, lake,
    //  crystal caverns, ruins, sanctuary, boss — and joined by named
    //  corridors, so the cavern reads as designed rather than as noise.
    //  Only the *edges* are irregular: each room is an ellipse whose radius
    //  is modulated by a couple of sine terms, which keeps rooms organic
    //  without making the layout random.
    // =================================================================
    public record Region(string Key, string Name, int Cx, int Cy, int Rx, int Ry, float Seed);

    public static readonly Region[] Regions =
    {
        new("entrance",  "The Descent",            44,  49,  8,  5, 0.7f),
        new("mine",      "The Abandoned Workings",  16,  45, 11,  7, 1.9f),
        new("tunnels",   "The Cut Tunnels",         15,  28,  8,  6, 3.1f),
        new("lake",      "The Drowned Hollow",      44,  33, 13,  8, 4.4f),
        new("crystal",   "The Crystal Garden",      22,  11, 11,  7, 5.6f),
        new("ruins",     "The Sunken Ruins",        58,  11, 11,  7, 6.8f),
        new("sanctuary", "The Forgotten Shrine",    72,  27,  9,  6, 8.2f),
        new("boss",      "The Crystal Heart",       71,  46, 12,  8, 9.5f),
    };

    /// <summary>The cave's rooms as read from its map, or null when it was built
    /// by the generator. When set it REPLACES the static array above — the static
    /// one is the generator's own copy and the fallback. Filled by
    /// BuildCaveFromTmx from the map's Region objects, exactly as the overworld's
    /// safe zones are read from theirs.</summary>
    List<Region>? _caveRegions;

    /// <summary>The rooms in play: the map's if it gave any, else the generator's.
    /// Everything downstream — dressing, rewards, the spawn and exit lookups —
    /// reads this, so a room moved in Tiled moves the game with it.</summary>
    IReadOnlyList<Region> CaveRegions => _caveRegions ?? (IReadOnlyList<Region>)Regions;

    /// <summary>Instance now, not static: the room graph can come from the map.
    /// The Seed only matters to the generator's carving, which uses the static
    /// array, so a map-read room needing a Seed is never asked for one.</summary>
    Region Reg(string key) => CaveRegions.First(r => r.Key == key);

    /// <summary>What the cave is, when nothing tells it otherwise. The authored
    /// map overrides both; the generator underneath has never had a size of its
    /// own and used to take the overworld's.</summary>
    public const int CAVE_COLS = 88, CAVE_ROWS = 56;

    void BuildWorld()
    {
        Rev++;

        // Authored in Tiled? Then the rock, the water and the ways between are read
        // from the map — and depth 1 is the authored Cistern: its dressing, and any
        // chests, discoveries or enemies the map places, are read rather than
        // rolled. Deeper floors are the delve and stay procedural. Empty layers
        // fall through to the generator, so authoring can begin one layer at a
        // time. See docs/mapping-standard.md.
        if (Tiled.MapCatalog.Find(Tiled.MapCatalog.Cave) is { } authored)
        {
            BuildCaveFromTmx(authored);
            Spawn = authored.Objects("PlayerSpawn").FirstOrDefault() is { } sp
                ? FromMap(sp.X, sp.Y)
                : TileCentre(Reg("entrance").Cx, Reg("entrance").Cy + 2);
            // Where the next descent begins — the map's own Descend marker when it
            // placed one, the boss room's centre otherwise.
            Exit = authored.Objects("Descent").FirstOrDefault() is { } dn
                ? FromMap(dn.X, dn.Y)
                : TileCentre(Reg("boss").Cx, Reg("boss").Cy);
            AddCaveExit();

            ApplyCoralNames();
            if (Level == 1 && authored.Objects("Props").Any()) ReadCaveProps(authored);
            else DressRegions();

            PlaceCaveRewards();
            if (Level == 1)
            {
                if (authored.Objects("Discovery").Any()) { Discoveries.Clear(); ReadDiscoveries(authored); }
                if (authored.Objects("TreasureSpawn").Any()) { Interactables.Clear(); ReadContainers(authored); }
                ReadCreatures(authored);   // authored extras stand beside the rolled husks
            }
            return;
        }

        _caveRegions = null;                        // the generator uses its own rooms
        Tiles = new byte[COLS, ROWS];               // all rock to start
        Props.Clear();

        foreach (var r in Regions) CarveRoom(r);
        foreach (var (a, b, w) in Links)
        {
            var ra = Reg(a); var rb = Reg(b);
            CarveCorridor(ra.Cx, ra.Cy, rb.Cx, rb.Cy, w, ra.Seed + rb.Seed);
        }

        // Cave mouths where a corridor leaves a room. The sheet's arch art is a
        // south-facing opening, so links that run mostly sideways are skipped
        // rather than drawn rotated into something that reads wrong.
        Arches.Clear();
        foreach (var (a, b, _) in Links)
        {
            var ra = Reg(a); var rb = Reg(b);
            float dx = rb.Cx - ra.Cx, dy = rb.Cy - ra.Cy;
            if (Math.Abs(dy) <= Math.Abs(dx)) continue;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) continue;
            int ex = (int)(ra.Cx + dx / len * ra.Rx * 0.95f);
            int ey = (int)(ra.Cy + dy / len * ra.Ry * 0.95f);
            if (ex > 2 && ey > 2 && ex < COLS - 3 && ey < ROWS - 3) Arches.Add(TileCentre(ex, ey));
        }

        FloodWater();
        SealBorder();

        Spawn = TileCentre(Reg("entrance").Cx, Reg("entrance").Cy + 2);
        Exit  = TileCentre(Reg("boss").Cx, Reg("boss").Cy);
        AddCaveExit();

        ApplyCoralNames();
        DressRegions();
        PlaceCaveRewards();
    }

    /// <summary>The way back up. A "Leave the cave" step is placed at the entrance
    /// region — the mouth the party came in by — on every level, so the delve is not
    /// one-way. Stepping it runs <see cref="LeaveCave"/>. Cleared first so descending
    /// never stacks a second one.</summary>
    void AddCaveExit()
    {
        Portals.Clear();
        Portals.Add(new Portal
        {
            Pos = TileCentre(Reg("entrance").Cx, Reg("entrance").Cy),
            Target = Portal.Overworld, Verb = "Step", Label = "Leave the cave", R = 44f,
        });
    }
}
