namespace AlfarQuest.Client.Game;

// =====================================================================
//  Carving the cave: rooms as wobbling ellipses, corridors as
//  discs along a bowed path, then the lake and its bridge.
// =====================================================================
public partial class World
{
    // Ellipse with a wobbling radius — organic edge, deliberate placement.
    void CarveRoom(Region r)
    {
        for (int x = r.Cx - r.Rx - 3; x <= r.Cx + r.Rx + 3; x++)
            for (int y = r.Cy - r.Ry - 3; y <= r.Cy + r.Ry + 3; y++)
            {
                if (x < 2 || y < 2 || x >= COLS - 2 || y >= ROWS - 2) continue;
                float dx = (x - r.Cx) / (float)r.Rx, dy = (y - r.Cy) / (float)r.Ry;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < 0.0001f) { Tiles[x, y] = FLOOR; continue; }
                float ang = MathF.Atan2(dy, dx);
                float wob = 1f + 0.20f * MathF.Sin(ang * 3f + r.Seed)
                               + 0.13f * MathF.Sin(ang * 5f - r.Seed * 2f)
                               + 0.08f * MathF.Sin(ang * 9f + r.Seed * 3f);
                if (d <= wob) Tiles[x, y] = FLOOR;
            }
    }

    void CarveCorridor(int ax, int ay, int bx, int by, int width, float seed)
    {
        int steps = (int)(MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)) * 2f) + 1;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            // Bow out sideways, returning to zero at both ends so the corridor
            // still lands inside the rooms it is meant to join.
            float nx = -(by - ay), ny = bx - ax;
            float len = MathF.Sqrt(nx * nx + ny * ny); if (len < 0.001f) len = 1;
            float bow = MathF.Sin(t * MathF.PI) * MathF.Sin(seed) * 5.5f;
            float cx = ax + (bx - ax) * t + nx / len * bow;
            float cy = ay + (by - ay) * t + ny / len * bow;
            float w = width * (0.8f + 0.35f * MathF.Sin(t * 7f + seed));
            CarveDiscTiles(cx, cy, w * 0.5f + 0.6f);
        }
    }

    void CarveDiscTiles(float cx, float cy, float r)
    {
        for (int x = (int)(cx - r) - 1; x <= cx + r + 1; x++)
            for (int y = (int)(cy - r) - 1; y <= cy + r + 1; y++)
            {
                if (x < 2 || y < 2 || x >= COLS - 2 || y >= ROWS - 2) continue;
                float dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r * r) Tiles[x, y] = FLOOR;
            }
    }

    // The lake gets real water with a plank bridge across it. Water blocks, the
    // bridge does not — that is the whole point of having tile types.
    void FloodWater()
    {
        var lake = Reg("lake");
        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
            {
                if (Tiles[x, y] != FLOOR) continue;
                float dx = (x - lake.Cx) / (float)(lake.Rx - 3), dy = (y - lake.Cy) / (float)(lake.Ry - 2);
                float d = MathF.Sqrt(dx * dx + dy * dy);
                float ang = MathF.Atan2(dy, dx);
                float wob = 1f + 0.18f * MathF.Sin(ang * 4f + 1.2f) + 0.1f * MathF.Sin(ang * 7f);
                if (d <= wob) Tiles[x, y] = WATER;
            }
        // Plank crossing, north to south through the middle of the hollow.
        for (int y = lake.Cy - lake.Ry - 1; y <= lake.Cy + lake.Ry + 1; y++)
            for (int x = lake.Cx - 1; x <= lake.Cx + 1; x++)
            {
                if (x < 2 || y < 2 || x >= COLS - 2 || y >= ROWS - 2) continue;
                if (Tiles[x, y] == WATER) Tiles[x, y] = BRIDGE;
            }
    }

    void SealBorder()
    {
        for (int x = 0; x < COLS; x++) { Tiles[x, 0] = Tiles[x, 1] = ROCK; Tiles[x, ROWS - 1] = Tiles[x, ROWS - 2] = ROCK; }
        for (int y = 0; y < ROWS; y++) { Tiles[0, y] = Tiles[1, y] = ROCK; Tiles[COLS - 1, y] = Tiles[COLS - 2, y] = ROCK; }
    }
}
