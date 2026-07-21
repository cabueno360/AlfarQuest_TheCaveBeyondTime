namespace AlfarQuest.Client.Game;

// =====================================================================
//  Creature AI and spawning.
//
//  Three states, written out rather than inferred from distance checks
//  scattered through the update: a creature that has lost you should walk
//  home, not stand where it gave up, and that is only clear if Return is
//  a state you can name.
// =====================================================================
public partial class World
{
    /// <summary>Places the party never has to fight: the village, the trail head
    /// and the mining camp. Given as tile rectangles because that is how the
    /// zones were authored.</summary>
    static readonly (int X0, int Y0, int X1, int Y1)[] SafeZones =
    [
        (4, 56, 24, 79),    // ZONE A — spawn camp and the village around it
        (28, 38, 40, 48),   // ZONE D — the crossroad where the wagoner waits
        (44, 18, 66, 36),   // ZONE E — the mining camp
        (33, 4, 48, 18),    // ZONE G — the forecourt of the mine
    ];

    public static bool InSafeZone(float wx, float wy)
    {
        int tx = (int)(wx / TILE), ty = (int)(wy / TILE);
        foreach (var (x0, y0, x1, y1) in SafeZones)
            if (tx >= x0 && tx <= x1 && ty >= y0 && ty <= y1) return true;
        return false;
    }

    /// <summary>Which biome a point belongs to, from the authored zone layout.
    /// Read top-down: the mountain wins over everything, then the approach, then
    /// the water, and the forest is what is left.</summary>
    Biome BiomeAt(float wx, float wy)
    {
        int tx = (int)(wx / TILE), ty = (int)(wy / TILE);
        // The bands reach well below the rock face on purpose. Keyed to y<=20 the
        // mountain species had nowhere to stand — that strip is nearly all cliff,
        // and the cleft below it is a safe zone — so walkers, spiders and worms
        // never spawned at all. The trail approaching the mine is their ground.
        if (ty <= 26 && tx is >= 30 and <= 54) return Biome.NearCave;
        if (ty <= 34) return Biome.Mountain;
        if (NearWater(wx, wy)) return Biome.River;
        return Biome.Forest;
    }

    bool NearWater(float wx, float wy)
    {
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
                if (TileAtWorld(wx + dx * TILE, wy + dy * TILE) == WATER) return true;
        return false;
    }

    void SpawnWildlife(int count)
    {
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < 60; tries++)
            {
                var p = new Vec(TILE * 3 + (float)_rng.NextDouble() * (ChamberW - TILE * 6),
                                TILE * 3 + (float)_rng.NextDouble() * (ChamberH - TILE * 6));
                if (Blocked(p, 18f)) continue;
                if (InSafeZone(p.X, p.Y)) continue;                 // no fights where people live

                // Keep a buffer around the party, but a smaller one than before:
                // at 340 the nearest creature sat two screens away and the first
                // ten minutes of the game had nothing in them. The safe zone
                // already guarantees the village itself is quiet.
                if ((p - Spawn).Len() < 190) continue;

                var options = CreatureCatalog.In(BiomeAt(p.X, p.Y)).ToList();
                if (options.Count == 0) continue;
                Husks.Add(new Husk(p, options[_rng.Next(options.Count)]));
                break;
            }
        }
    }

    /// <summary>One creature's turn. Returns the direction it wants to move, or
    /// zero to stand still.</summary>
    Vec StepAi(Husk k, Hero? target, float dt)
    {
        k.Think -= dt;
        float aggro = k.Def.AggroTiles * TILE;
        float patrol = k.Def.PatrolTiles * TILE;
        float toTarget = target is null ? float.MaxValue : (target.Pos - k.Pos).Len();

        // Creatures never pursue into a safe zone; that is what makes it safe.
        bool huntable = target is not null && !InSafeZone(target.Pos.X, target.Pos.Y);

        k.State = k.State switch
        {
            AiState.Chase when !huntable || toTarget > aggro * 1.8f => AiState.Return,
            AiState.Patrol when huntable && toTarget < aggro => AiState.Chase,
            AiState.Return when huntable && toTarget < aggro * 0.7f => AiState.Chase,
            AiState.Return when (k.Home - k.Pos).Len() < TILE => AiState.Patrol,
            _ => k.State,
        };

        switch (k.State)
        {
            case AiState.Chase:
                return target is null ? new Vec(0, 0) : (target.Pos - k.Pos).Norm();

            case AiState.Return:
                return (k.Home - k.Pos).Norm() * 0.7f;

            default:
                // Pick a new loiter point now and then, never further from home
                // than the species wanders.
                if (k.Think <= 0 || (k.PatrolTarget - k.Pos).Len() < TILE * 0.6f)
                {
                    k.Think = 1.6f + (float)_rng.NextDouble() * 2.6f;
                    float a = (float)_rng.NextDouble() * MathF.Tau;
                    float r = (float)_rng.NextDouble() * patrol;
                    k.PatrolTarget = k.Home + new Vec(MathF.Cos(a) * r, MathF.Sin(a) * r);
                }
                var step = k.PatrolTarget - k.Pos;
                return step.Len() < 4f ? new Vec(0, 0) : step.Norm() * 0.45f;
        }
    }
}
