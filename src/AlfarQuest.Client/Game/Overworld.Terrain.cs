namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 1 terrain: the mountain wall, the river and the road
//  network that ties every zone together.
// =====================================================================
public partial class World
{
    // ---- ZONE F: the mountain, y 0-20 -------------------------------
    // Irregular on purpose: a straight southern face would read as a wall
    // rather than as a mountain, and the spec calls for no straight lines.
    void BuildMountain()
    {
        for (int x = 0; x < COLS; x++)
        {
            float edge = 15f
                + 4.5f * MathF.Sin(x * 0.17f)
                + 2.5f * MathF.Sin(x * 0.41f + 1.3f)
                + 1.5f * MathF.Sin(x * 0.83f + 2.7f);
            // A notch around the cave mouth so the entrance sits in a cleft
            // instead of flush against the range.
            if (MathF.Abs(x - 40) < 7) edge -= 5.5f - MathF.Abs(x - 40) * 0.6f;
            for (int y = 0; y < (int)edge && y < ROWS; y++) Tiles[x, y] = ROCK;
        }
    }

    // ---- ZONE C: river from the mountains down to the southern lowlands ----
    void BuildRiver()
    {
        var pts = new List<(float x, float y)>();
        // The original lazy S, from the northwest cliffs down to the crossroad ford.
        for (int i = 0; i <= 60; i++)
        {
            float t = i / 60f;
            float x = 0 + (42 - 0) * t + 9f * MathF.Sin(t * 3.1f) - 4f * MathF.Sin(t * 6.4f);
            float y = 20 + (79 - 20) * t + 3f * MathF.Sin(t * 4.2f);
            pts.Add((x, y));
        }
        // The new southern reach: it swings back southwest through the lowlands and
        // runs off the map toward the coast and Seoshe, widening as it goes.
        for (int i = 1; i <= 46; i++)
        {
            float t = i / 46f;
            float x = 42 + (12 - 42) * t + 5f * MathF.Sin(t * 3.4f);
            float y = 79 + (126 - 79) * t;
            pts.Add((x, y));
        }
        for (int k = 0; k < pts.Count; k++)
        {
            var (px, py) = pts[k];
            float w = 2.4f + 0.8f * MathF.Sin(py * 0.21f) + 0.9f * Math.Clamp((py - 80f) / 40f, 0f, 1f); // wider downstream
            for (int x = (int)(px - w) - 1; x <= px + w + 1; x++)
                for (int y = (int)(py - w) - 1; y <= py + w + 1; y++)
                {
                    if (x < 0 || y < 0 || x >= COLS || y >= ROWS) continue;
                    if (Tiles[x, y] == ROCK) continue;          // river runs past the cliffs
                    float dx = x - px, dy = y - py;
                    if (dx * dx + dy * dy <= w * w) Tiles[x, y] = WATER;
                }
        }
        // The crossroad ford — decked wide enough that the player cannot miss it.
        for (int x = 24; x <= 32; x++)
            for (int y = 46; y <= 50; y++)
                if (Tiles[x, y] == WATER) Tiles[x, y] = BRIDGE;
        // The southern crossing, where the coast road steps over the river.
        for (int x = 24; x <= 33; x++)
            for (int y = 94; y <= 100; y++)
                if (Tiles[x, y] == WATER) Tiles[x, y] = BRIDGE;
    }

    // ---- the route: spawn -> bridge -> crossroad -> camp -> mouth ----
    void BuildRoads()
    {
        Road(13, 70, 20, 58, 2.0f);      // out of the spawn camp
        Road(20, 58, 28, 48, 1.8f);      // down to the bridge
        Road(28, 48, 33, 42, 2.0f);      // over and up to the crossroad
        Road(33, 42, 44, 36, 2.2f);      // crossroad -> mountain trail
        Road(44, 36, 55, 28, 2.0f);      // trail -> mining camp
        Road(55, 28, 44, 18, 1.7f);      // camp -> the cleft
        Road(44, 18, 40, 11, 1.9f);      // cleft -> the mouth
        Road(33, 42, 22, 36, 1.4f);      // a spur west into the forest
        Road(22, 36, 20, 25, 1.4f);      // up into the foothills, to the Cleric's house

        // The east road — out of the crossroad and off the map toward Kae Ychel,
        // through the drying scrub, past the caravan and the old colonnade.
        Road(33, 42, 72, 48, 2.2f);
        Road(72, 48, 115, 56, 2.2f);     // through the caravan camp
        Road(115, 56, 155, 61, 2.0f);
        Road(155, 61, 197, 65, 2.0f);    // to the eastern edge
        Road(150, 60, 158, 37, 1.5f);    // a spur up to the sunken colonnade
        Road(155, 61, 184, 84, 1.4f);    // a spur to the broken watchtower

        // The coast road — south from the crossroad, over the river, and off the
        // southwest edge toward Seoshe on its crescent shore.
        Road(33, 42, 32, 70, 2.0f);
        Road(32, 70, 28, 97, 2.0f);      // over the southern crossing
        Road(28, 97, 14, 123, 1.9f);     // to the southwest edge
        Road(32, 70, 50, 106, 1.6f);     // a spur down to the fishing steps
        Road(50, 106, 74, 112, 1.4f);    // on to the standing stones
    }

    void Road(float ax, float ay, float bx, float by, float w)
    {
        int steps = (int)(MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)) * 3) + 1;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float nx = -(by - ay), ny = bx - ax;
            float len = MathF.Sqrt(nx * nx + ny * ny); if (len < 0.001f) len = 1;
            float bow = MathF.Sin(t * MathF.PI) * 2.2f * MathF.Sin(ax + ay);
            float cx = ax + (bx - ax) * t + nx / len * bow;
            float cy = ay + (by - ay) * t + ny / len * bow;
            for (int x = (int)(cx - w) - 1; x <= cx + w + 1; x++)
                for (int y = (int)(cy - w) - 1; y <= cy + w + 1; y++)
                {
                    if (x < 0 || y < 0 || x >= COLS || y >= ROWS) continue;
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > w * w) continue;
                    // A road crossing water becomes a bridge deck, never a ford.
                    if (Tiles[x, y] == WATER || Tiles[x, y] == BRIDGE) { Tiles[x, y] = BRIDGE; continue; }
                    if (Tiles[x, y] == ROCK) continue;          // roads do not eat the mountain
                    Tiles[x, y] = PATH;
                }
        }
    }

    // A standing-in-for-noise density field. Uniform scatter gives a hedge of
    // even trees; this gives thickets you push through and clearings you break
    // into, which is what makes a forest read as a place rather than a texture.
    float Density(float x, float y) =>
        0.50f
        + 0.26f * MathF.Sin(x * 0.21f) * MathF.Cos(y * 0.17f)
        + 0.16f * MathF.Sin(x * 0.09f + y * 0.13f)
        + 0.10f * MathF.Cos(x * 0.37f - y * 0.29f);
}
