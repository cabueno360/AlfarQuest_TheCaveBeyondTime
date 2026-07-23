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
        "The Crystal Cistern", "The Weeping Gallery", "The Sunken Vault",
        "The Mirror Halls", "The Cave Beyond Time",
    };

    // Stage 1 is one named place; an interior is named for itself; the cave
    // numbers its depths.
    public string RegionName =>
        IsInterior ? (InteriorCatalog.Find(CurrentInterior!)?.Name ?? "Indoors")
        : Stage == 1 ? OverworldName
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

    public static Region Reg(string key) => Array.Find(Regions, r => r.Key == key)!;

    void BuildWorld()
    {
        Rev++;

        // Authored in Tiled? Then the rock, the water and the ways between are read
        // from the map. Only the PLACE comes from there: the crystals, the husks
        // and the rewards are still placed per descent below, because those are the
        // delve rather than the cave. See docs/mapping-standard.md.
        if (Tiled.MapCatalog.Find(Tiled.MapCatalog.Cave) is { } authored)
        {
            BuildCaveFromTmx(authored);
            Spawn = TileCentre(Reg("entrance").Cx, Reg("entrance").Cy + 2);
            Exit  = TileCentre(Reg("boss").Cx, Reg("boss").Cy);
            DressRegions();
            PlaceCaveRewards();
            return;
        }

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

        DressRegions();
        PlaceCaveRewards();
    }
}
