namespace AlfarQuest.Client.Game;

// =====================================================================
//  The wider Stage 1 — the land the 4× map opened up.
//
//  East of the crossroad the country dries out along the road to Kae Ychel:
//  a traders' caravan, an old Ychellen colonnade, a broken watchtower, and
//  the crystal corruption breaking the surface. South, the river runs down
//  to the coast road toward Seoshe, past a fishing hamlet, a ring of standing
//  stones, and a burnt smugglers' warehouse nobody points to.
//
//  All of it is dressed from the props the Outside atlas has — ruins, tents,
//  statues, supports for columns — so it reads as place without waiting on art
//  that does not exist. The named buildings (the school, the city, the thieves'
//  interior) come in their own phases; this is the ground between them.
// =====================================================================
public partial class World
{
    // ---- vegetation for the new ground ------------------------------

    /// <summary>The drying scrub of the east: trees give way to dead wood and rock
    /// the further you go toward the desert and Kae Ychel.</summary>
    void BuildEastVegetation()
    {
        for (int n = 0; n < 3400; n++)
        {
            float x = 74f + (float)_rng.NextDouble() * 124f;
            float y = 22f + (float)_rng.NextDouble() * 103f;
            if (!OpenGround(x, y) || NearRoad(x, y, 2.0f)) continue;
            if (InRect(x, y, 104, 48, 130, 68)) continue;   // caravan camp
            if (InRect(x, y, 148, 28, 170, 42)) continue;   // the colonnade

            float dry = Math.Clamp((x - 74f) / 120f, 0f, 1f);   // 0 near the wood .. 1 far east
            if (_rng.NextDouble() > Density(x, y) * (0.72f - 0.30f * dry)) continue;

            double r = _rng.NextDouble();
            if (r < 0.18 * (1 - dry)) AddOw(x, y, "pine", 0.55f, true, 14f);
            else if (r < 0.30 * (1 - dry)) AddOw(x, y, "broadleaf", 0.58f, true, 15f);
            else if (r < 0.50) AddOw(x, y, "bush", 0.5f, false, 0f);
            else if (r < 0.63) AddOw(x, y, "deadTree", 0.6f, true, 13f);
            else if (r < 0.86) AddOw(x, y, "rock", 0.5f + dry * 0.35f, true, 12f);
            else AddOw(x, y, "flowers", 0.44f, false, 0f);
        }
    }

    /// <summary>The wet lowland forest of the south — thicker and greener than the
    /// approach, with a couple of ponds where the ground lies low.</summary>
    void BuildSouthVegetation()
    {
        Pond(62, 90, 5, 4);
        Pond(20, 112, 6, 4);

        for (int n = 0; n < 2800; n++)
        {
            float x = 2f + (float)_rng.NextDouble() * 76f;
            float y = 78f + (float)_rng.NextDouble() * 47f;
            if (!OpenGround(x, y) || NearRoad(x, y, 2.0f)) continue;
            if (InRect(x, y, 42, 100, 60, 118)) continue;   // fishing steps
            if (InRect(x, y, 70, 106, 86, 120)) continue;   // the standing stones

            if (_rng.NextDouble() > Density(x, y) * 0.95f) continue;

            double r = _rng.NextDouble();
            if (r < 0.34) AddOw(x, y, "pine", 0.6f, true, 15f);
            else if (r < 0.54) AddOw(x, y, "broadleaf", 0.62f, true, 16f);
            else if (r < 0.70) AddOw(x, y, "bush", 0.52f, false, 0f);
            else if (r < 0.86) AddOw(x, y, "flowers", 0.48f, false, 0f);
            else if (r < 0.94) AddOw(x, y, "rock", 0.5f, true, 12f);
            else AddOw(x, y, "log", 0.55f, false, 0f);
        }
    }

    void Pond(int cx, int cy, int rx, int ry)
    {
        for (int x = cx - rx; x <= cx + rx; x++)
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                if (x < 0 || y < 0 || x >= COLS || y >= ROWS) continue;
                float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                if (dx * dx + dy * dy <= 1f && Tiles[x, y] == FLOOR) Tiles[x, y] = WATER;
            }
    }

    // ---- the east reach: the road to Kae Ychel ----------------------
    void BuildEastReach()
    {
        // The caravan camp — the Neruum traders' rest on the road east.
        Clearing(115, 57, 9);
        AddOw(112, 55, "tent", 0.8f, true, 24f); AddOw(118, 55, "tent", 0.75f, true, 22f);
        AddOw(115, 57, "campfire", 0.65f, false, 0f);
        AddOw(110, 59, "wagon", 0.8f, true, 22f); AddOw(120, 59, "wagon", 0.75f, true, 20f);
        AddOw(113, 60, "stall", 0.7f, true, 18f); AddOw(117.5f, 60, "stall", 0.68f, true, 18f);
        AddOw(122, 56, "barrel", 0.6f, true, 11f); AddOw(108, 56, "crate", 0.6f, true, 12f);
        AddOw(115, 53, "banner", 0.7f, false, 0f); AddOw(109, 54.5f, "lantern", 0.6f, false, 0f);
        AddOw(105, 60, "signpost", 0.7f, false, 0f);        // To Kae Ychel (east)

        // The Sunken Colonnade — an old Kae Ychellen ruin up the northern spur.
        Clearing(158, 35, 9);
        for (int i = 0; i < 5; i++)
        {
            AddOw(152f + i * 3f, 32, "support", 0.85f, true, 15f);
            AddOw(152f + i * 3f, 38, "support", 0.8f, true, 15f);
        }
        AddOw(158, 35, "statue", 1.0f, true, 18f);
        AddOw(153, 35.5f, "ruin", 0.9f, true, 20f); AddOw(163, 35.5f, "ruin", 0.85f, true, 18f);
        AddOw(156, 30, "gravestone", 0.6f, true, 12f); AddOw(160, 30, "gravestone", 0.55f, true, 11f);

        // The Broken Watch — a collapsed watchtower on the far eastern flat.
        Clearing(184, 84, 7);
        AddOw(184, 84, "ruin", 1.1f, true, 24f);
        AddOw(181, 86, "support", 0.85f, true, 16f); AddOw(187, 86, "support", 0.8f, true, 15f);
        AddOw(184, 88, "statue", 0.8f, true, 16f); AddOw(180, 82, "signpost", 0.6f, false, 0f);

        // The Weeping Shards — the crystal corruption surfacing in the east hills.
        for (int n = 0; n < 22; n++)
        {
            float x = 146f + (float)_rng.NextDouble() * 12f, y = 22f + (float)_rng.NextDouble() * 8f;
            if (!OpenGround(x, y)) continue;
            AddOw(x, y, "crystal", 0.55f + (float)_rng.NextDouble() * 0.4f, _rng.Next(3) == 0, 12f);
        }
    }

    // ---- the south reach: the coast road to Seoshe ------------------
    void BuildSouthReach()
    {
        // The Fishing Steps — a hamlet where the coast road meets the river.
        Clearing(50, 108, 9);
        AddOw(50, 108, "well", 0.72f, true, 16f);
        AddOw(46, 106, "bench", 0.6f, false, 0f); AddOw(54, 106, "bench", 0.6f, false, 0f);
        AddOw(44, 109, "campfire", 0.6f, false, 0f); AddOw(43, 111, "stall", 0.65f, true, 18f);
        for (int i = 0; i < 3; i++) AddOw(56f + i * 1.6f, 111f, "log", 0.6f, false, 0f);   // drawn-up boats
        AddOw(58, 108, "signpost", 0.6f, false, 0f);        // To Seoshe (southwest)

        // The stone bridge over the southern crossing.
        AddOw(27, 94, "support", 0.7f, true, 14f); AddOw(30, 100, "support", 0.7f, true, 14f);
        AddOw(24.5f, 97, "lantern", 0.6f, false, 0f); AddOw(32, 97, "lantern", 0.6f, false, 0f);

        // The Standing Stones — a ring of old menhirs in a southern clearing.
        Clearing(78, 112, 8);
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8;
            AddOw(78 + MathF.Cos(a) * 5f, 112 + MathF.Sin(a) * 4f, "statue", 0.7f, true, 14f);
        }
        AddOw(78, 112, "ruin", 0.7f, true, 16f);

        // Smuggler's Hollow — a burnt warehouse hidden off the coast road, the
        // Seoshe crew's inland stash. Screened by bushes; no sign points to it.
        Clearing(14, 116, 6);
        AddOw(14, 116, "ruin", 1.1f, true, 24f);
        AddOw(11, 118, "crate", 0.62f, true, 12f); AddOw(17, 118, "barrel", 0.6f, true, 11f);
        foreach (var (bx, by) in new[] { (9f, 115f), (9f, 118f), (19f, 115f), (19f, 118f), (14f, 120.5f) })
            AddOw(bx, by, "bush", 0.72f, false, 0f);

        // Signposts back at the crossroad, pointing the two new ways.
        AddOw(30.5f, 45, "signpost", 0.62f, false, 0f);     // south: the coast road
        AddOw(36, 39.5f, "signpost", 0.62f, false, 0f);     // east: the Kae Ychel road
    }

    // ---- the Academy outpost: a college of Kae Ychel on the east road ----
    /// <summary>An arcane academy courtyard — the Mage School. No building sprite
    /// exists for it, so it is dressed as the book describes the Academy grounds
    /// themselves: colonnades, silk banners, statues of the masters and the Twin
    /// Sun King, glowing arcane crystals, and a grand dark doorway into the hall.
    /// Apprentices (placed by NpcCatalog) walk among it.</summary>
    void BuildMageSchool()
    {
        Clearing(96, 47, 11);

        // The grand doorway into the hall, flanked by columns, and the portal.
        AddPortal(96, 42, "mage_school", "the Academy outpost", TileCentre(96, 44));
        AddOw(93, 42, "support", 0.9f, true, 16f); AddOw(99, 42, "support", 0.9f, true, 16f);
        AddOw(94, 41, "banner", 0.7f, false, 0f); AddOw(98, 41, "banner", 0.7f, false, 0f);
        AddOw(94, 43, "lantern", 0.6f, false, 0f); AddOw(98, 43, "lantern", 0.6f, false, 0f);

        // A colonnade approach up to the door, statues of the masters to either side.
        foreach (var cy in new[] { 45f, 48f, 51f })
        {
            AddOw(92, cy, "support", 0.82f, true, 15f);
            AddOw(100, cy, "support", 0.82f, true, 15f);
        }
        AddOw(90, 46, "statue", 0.95f, true, 18f);        // a grandmaster
        AddOw(102, 46, "statue", 1.0f, true, 18f);        // the Twin Sun King
        AddOw(96, 49, "statue", 0.85f, true, 16f);        // the Divine Podium, central

        // Arcane crystals as braziers and wards, and banners along the walls.
        foreach (var (bx, by) in new[] { (91f, 44f), (101f, 44f), (91f, 49f), (101f, 49f) })
            AddOw(bx, by, "banner", 0.65f, false, 0f);
        foreach (var (cx, cy) in new[] { (94f, 47f), (98f, 47f), (93f, 51f), (99f, 51f) })
            AddOw(cx, cy, "crystal", 0.55f, false, 0f);

        // Gardens — the Academy is known for them — and a sign at the road.
        AddOw(87, 48, "cherry", 1.0f, true, 18f); AddOw(105, 48, "cherry", 0.95f, true, 18f);
        foreach (var (fx, fy) in new[] { (89f, 50f), (103f, 50f), (95f, 52f), (97f, 52f) })
            AddOw(fx, fy, "flowers", 0.5f, false, 0f);
        AddOw(94, 53, "signpost", 0.62f, false, 0f);
        Road(96, 53, 96, 50, 1.4f);       // a short spur off the east road into the yard
    }

    // ---- the gate of Seoshe: the way into the coastal city ----
    /// <summary>A fortified gate on the coast road — stone ramparts, an archway,
    /// banners and guards, and the portal into the Seoshe city map. The city itself
    /// lies beyond, off this map, its own place entirely.</summary>
    void BuildSeosheGate()
    {
        Clearing(31, 76, 6);
        // City wall to either side of the gate (drawn as stone rampart), with the
        // gate opening left clear for the road to pass through.
        for (int x = 25; x <= 37; x++)
            for (int y = 74; y <= 75; y++)
                if ((x < 30 || x > 32) && x < COLS && y < ROWS) Tiles[x, y] = ROCK;
        for (int x = 30; x <= 32; x++)
            for (int y = 73; y <= 79; y++)
                if (x < COLS && y < ROWS) Tiles[x, y] = PATH;

        AddPortal(31, 75, "seoshe", "the gate of Seoshe", TileCentre(31, 77));
        AddOw(29, 75, "support", 0.9f, true, 16f); AddOw(33, 75, "support", 0.9f, true, 16f);
        AddOw(29, 74, "banner", 0.7f, false, 0f); AddOw(33, 74, "banner", 0.7f, false, 0f);
        AddOw(26, 76, "statue", 0.85f, true, 16f); AddOw(36, 76, "statue", 0.85f, true, 16f);
        AddOw(27, 78, "signpost", 0.65f, false, 0f);
        AddOw(35, 78, "lantern", 0.6f, false, 0f); AddOw(27, 73, "lantern", 0.6f, false, 0f);
    }

    /// <summary>Sweeps loose vegetation and rock out of a circle so a camp or a
    /// ruin reads as a cleared, lived-in spot rather than a thicket with tents in
    /// it. Only clears scatter — never the structures placed on purpose.</summary>
    void Clearing(float cx, float cy, float rad)
    {
        var c = new Vec(cx * TILE + TILE * 0.5f, cy * TILE + TILE * 0.5f);
        float r2 = (rad * TILE) * (rad * TILE);
        Props.RemoveAll(p =>
            (p.X - c.X) * (p.X - c.X) + (p.Y - c.Y) * (p.Y - c.Y) < r2
            && p.Kind is "pine" or "broadleaf" or "bush" or "deadTree" or "rock" or "flowers" or "log");
    }
}
