namespace AlfarQuest.Client.Game;

// =====================================================================
//  Tile queries and body collision. Everything that answers
//  "can something stand here" lives together.
// =====================================================================
public partial class World
{
    // -----------------------------------------------------------------
    //  Tile queries and collision
    // -----------------------------------------------------------------
    public byte TileAtWorld(float wx, float wy)
    {
        int x = (int)(wx / TILE), y = (int)(wy / TILE);
        if (x < 0 || y < 0 || x >= COLS || y >= ROWS) return ROCK;
        return Tiles[x, y];
    }

    // Rock and open water stop a body; a plank bridge does not.
    public bool IsWallAt(float wx, float wy)
    {
        var t = TileAtWorld(wx, wy);
        return t == ROCK || t == WATER;      // FLOOR, PATH and BRIDGE carry a body
    }

    bool Blocked(Vec p, float radius)
    {
        if (IsWallAt(p.X, p.Y)) return true;
        if (IsWallAt(p.X + radius, p.Y) || IsWallAt(p.X - radius, p.Y)) return true;
        if (IsWallAt(p.X, p.Y + radius) || IsWallAt(p.X, p.Y - radius)) return true;
        foreach (var pr in Props)
        {
            if (!pr.Solid) continue;
            float dx = p.X - pr.X, dy = (p.Y - pr.Y) * 1.35f;       // squat ellipse: props block at their base
            if (dx * dx + dy * dy < (pr.R + radius) * (pr.R + radius)) return true;
        }
        return false;
    }

    // Axis-separated so a body sliding along a wall keeps the component that is
    // still free, instead of stopping dead the moment either axis is blocked.
    public Vec MoveBlocked(Vec pos, Vec delta, float radius)
    {
        var p = pos;
        var tryX = new Vec(p.X + delta.X, p.Y);
        if (!Blocked(tryX, radius)) p = tryX;
        var tryY = new Vec(p.X, p.Y + delta.Y);
        if (!Blocked(tryY, radius)) p = tryY;
        return p;
    }

    static Vec TileCentre(int tx, int ty) => new(tx * TILE + TILE * 0.5f, ty * TILE + TILE * 0.5f);

    // One character per tile: '#' rock, '.' floor, '~' water, '=' bridge.
    public List<string> MapRows()
    {
        var rows = new List<string>(ROWS);
        var sb = new System.Text.StringBuilder(COLS);
        for (int y = 0; y < ROWS; y++)
        {
            sb.Clear();
            for (int x = 0; x < COLS; x++)
                sb.Append(Tiles[x, y] switch { FLOOR => '.', WATER => '~', BRIDGE => '=', PATH => ',', _ => '#' });
            rows.Add(sb.ToString());
        }
        return rows;
    }

    Vec ResolveCrystalCollision(Vec pos)
    {
        foreach (var c in Crystals)
        {
            var d = pos - c.Pos;
            float len = d.Len();
            float min = c.R + 18f;
            if (len < min && len > 0.01f) pos = c.Pos + d.Norm() * min;
        }
        return pos;
    }

    Vec Clamp(Vec p, float pad) =>
        new(Math.Clamp(p.X, pad, ChamberW - pad), Math.Clamp(p.Y, pad, ChamberH - pad));

    bool Outside(Vec p) => p.X < -20 || p.Y < -20 || p.X > ChamberW + 20 || p.Y > ChamberH + 20;
}
