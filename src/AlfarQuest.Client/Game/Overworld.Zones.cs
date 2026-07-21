namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 1 zones A-G. Each sits at fixed coordinates from the
//  written spec; only the fine detail is scattered.
// =====================================================================
public partial class World
{
    // ---- ZONE B: forest, x 0-32, y 30-79 ----------------------------
    void BuildForest()
    {
        // Trees are props, not tiles: they block via their own radius, which
        // keeps the 2-4 tile walking gaps the spec asks for from being eaten by
        // square tile blocking.
        for (int n = 0; n < 2600; n++)
        {
            float x = (float)_rng.NextDouble() * 34f;
            float y = 26f + (float)_rng.NextDouble() * 53f;
            if (!OpenGround(x, y)) continue;
            if (NearRoad(x, y, 2.2f)) continue;                // keep the road clear
            if (InRect(x, y, 5, 59, 22, 78)) continue;         // spawn camp keeps its clearing

            if (_rng.NextDouble() > Density(x, y)) continue;   // thickets and clearings

            double r = _rng.NextDouble();
            if (r < 0.40) AddOw(x, y, "pine", 0.58f + (float)_rng.NextDouble() * 0.16f, true, 15f);
            else if (r < 0.58) AddOw(x, y, "broadleaf", 0.58f + (float)_rng.NextDouble() * 0.16f, true, 16f);
            else if (r < 0.78) AddOw(x, y, "bush", 0.5f + (float)_rng.NextDouble() * 0.14f, false, 0f);
            else if (r < 0.90) AddOw(x, y, "flowers", 0.46f + (float)_rng.NextDouble() * 0.12f, false, 0f);
            else if (r < 0.96) AddOw(x, y, "rock", 0.55f, true, 12f);
            else AddOw(x, y, "log", 0.55f, false, 0f);
        }

        // The rest of the map is not bare: sparse scrub everywhere else, thinning
        // toward the mountain so the approach visibly dries out.
        for (int n = 0; n < 2200; n++)
        {
            float x = 30f + (float)_rng.NextDouble() * 49f;
            float y = 12f + (float)_rng.NextDouble() * 67f;
            if (!OpenGround(x, y) || NearRoad(x, y, 2.0f)) continue;
            if (InRect(x, y, 46, 18, 64, 36)) continue;        // mining camp clearing
            if (InRect(x, y, 32, 4, 50, 18)) continue;         // the cleft stays barren

            // Vegetation falls away as you climb north; rock takes over.
            float lush = Math.Clamp((y - 10f) / 40f, 0f, 1f);
            if (_rng.NextDouble() > Density(x, y) * (0.35f + 0.65f * lush)) continue;

            double r = _rng.NextDouble();
            if (r < 0.20 * lush) AddOw(x, y, "pine", 0.58f, true, 15f);
            else if (r < 0.34 * lush) AddOw(x, y, "broadleaf", 0.6f, true, 16f);
            else if (r < 0.55) AddOw(x, y, "bush", 0.5f, false, 0f);
            else if (r < 0.74) AddOw(x, y, "flowers", 0.46f, false, 0f);
            else if (r < 0.92) AddOw(x, y, "rock", 0.5f + (1f - lush) * 0.3f, true, 12f);
            else AddOw(x, y, "deadTree", 0.6f, true, 13f);
        }
    }

    // ---- ZONE A: spawn camp, x 6-20 y 60-76 -------------------------
    void BuildSpawnCamp()
    {
        AddOw(10, 66, "tent", 0.75f, true, 22f);
        AddOw(14, 65, "campfire", 0.6f, false, 0f);
        AddOw(17, 67, "workbench", 0.62f, true, 18f);
        AddOw(8, 69, "crate", 0.6f, true, 12f);
        AddOw(9, 71, "crate", 0.6f, true, 12f);
        AddOw(18, 71, "barrel", 0.6f, true, 11f);
        AddOw(16, 62, "bench", 0.6f, false, 0f);
        AddOw(19, 63, "well", 0.65f, true, 16f);
        // Felled logs mark the edge of somewhere lived-in. The sheet has no
        // standalone wooden fence — what looked like one is a walled graveyard,
        // now placed as a landmark instead.
        for (int i = 0; i < 5; i++) AddOw(6.5f + i * 1.7f, 75.5f, "log", 0.62f, true, 13f);
        AddOw(12, 74, "signpost", 0.55f, false, 0f);
    }

    // ---- ZONE D: crossroad, centre (33,42) --------------------------
    void BuildCrossroad()
    {
        AddOw(33, 41.2f, "signpost", 0.7f, false, 0f);
        AddOw(31, 43, "lantern", 0.6f, false, 0f);
        AddOw(35.5f, 43.5f, "ruin", 0.6f, true, 16f);
        AddOw(30.5f, 39.5f, "rock", 0.6f, true, 13f);
        AddOw(36, 39, "bush", 0.6f, false, 0f);
    }

    // ---- ZONE E: mining camp, x 48-62 y 20-34 -----------------------
    void BuildMiningCamp()
    {
        AddOw(52, 27, "tent", 0.8f, true, 24f);
        AddOw(55.5f, 28.5f, "campfire", 0.65f, false, 0f);
        AddOw(58, 26, "workbench", 0.7f, true, 20f);
        AddOw(60, 28, "toolRack", 0.7f, true, 16f);
        AddOw(50, 30, "mineCart", 0.7f, true, 18f);
        AddOw(57, 31, "orePile", 0.65f, false, 0f);
        AddOw(59, 32, "orePile", 0.6f, false, 0f);
        AddOw(49, 25, "barrel", 0.65f, true, 11f);
        AddOw(50.5f, 24, "barrel", 0.65f, true, 11f);
        AddOw(54, 23, "crate", 0.65f, true, 12f);
        AddOw(61, 24, "wagon", 0.7f, true, 22f);
        AddOw(47, 28, "lantern", 0.6f, false, 0f);
        AddOw(62, 30, "stall", 0.65f, true, 18f);
    }

    // ---- ZONE G: the cave mouth, centre (40,8) ----------------------
    void BuildCaveEntrance()
    {
        // Clear a forecourt so the approach never dead-ends against rock.
        for (int x = 35; x <= 45; x++)
            for (int y = 6; y <= 16; y++)
                if (x >= 0 && y >= 0 && x < COLS && y < ROWS && Tiles[x, y] == ROCK)
                    Tiles[x, y] = PATH;

        CaveMouth = TileCentre(40, 8);
        AddOw(40, 8, "caveEntrance", 1.0f, false, 0f);   // the trigger, never a blocker

        AddOw(36, 11, "mineCart", 0.75f, true, 18f);
        AddOw(44, 10, "support", 0.8f, true, 16f);
        AddOw(35.5f, 13.5f, "support", 0.75f, true, 15f);
        AddOw(43, 13, "barrel", 0.7f, true, 11f);
        AddOw(44.5f, 14.5f, "crate", 0.7f, true, 12f);
        AddOw(37, 14, "orePile", 0.7f, false, 0f);
        AddOw(42, 15.5f, "orePile", 0.65f, false, 0f);
        AddOw(34, 10, "rock", 0.8f, true, 16f);
        AddOw(46, 12, "rock", 0.8f, true, 16f);
        AddOw(38, 12.5f, "lantern", 0.65f, false, 0f);
        AddOw(41.5f, 11.5f, "signpost", 0.6f, false, 0f);
        AddOw(45.5f, 16, "ladder", 0.65f, false, 0f);
    }
}
