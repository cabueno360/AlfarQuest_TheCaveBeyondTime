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
        (16, 18, 30, 30),   // the Cleric's hamlet in the foothills
        (106, 50, 128, 66), // the caravan camp on the east road
        (42, 100, 60, 118), // the fishing steps on the coast road
        (86, 40, 106, 55),  // the Academy outpost courtyard
        (24, 71, 38, 80),   // the gate of Seoshe on the coast road
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

    /// <summary>Distance in tiles from the steered hero to the nearest creature,
    /// and the unit direction to it. A diagnostic seam only — it lets a test walk
    /// toward a fight instead of into a wall.</summary>
    float NearestCreatureTiles(out Vec dir, out Husk? nearest)
    {
        dir = new Vec(0, 0);
        nearest = null;
        if (Party.Count == 0 || Active >= Party.Count) return 999f;
        var from = Party[Active].Pos;
        float bestD = float.MaxValue;
        foreach (var k in Husks)
        {
            var d = (k.Pos - from).Len();
            if (d < bestD) { bestD = d; nearest = k; }
        }
        if (nearest is null) return 999f;
        dir = (nearest.Pos - from).Norm();
        return bestD / TILE;
    }

    /// <summary>Distance in tiles from the steered hero to the nearest creature
    /// that casts a ranged bolt, and the way to it. A seam so a test can provoke a
    /// caster specifically.</summary>
    float NearestCasterTiles(out Vec dir)
    {
        dir = new Vec(0, 0);
        if (Party.Count == 0 || Active >= Party.Count) return 999f;
        var from = Party[Active].Pos;
        Husk? best = null; float bestD = float.MaxValue;
        foreach (var k in Husks)
        {
            if (k.Def.Ability is not (MonsterAbility.PoisonSpit or MonsterAbility.CrystalBolt
                                      or MonsterAbility.MagicMissile)) continue;
            var d = (k.Pos - from).Len();
            if (d < bestD) { bestD = d; best = k; }
        }
        if (best is null) return 999f;
        dir = (best.Pos - from).Norm();
        return bestD / TILE;
    }

    /// <summary>One creature's turn. Returns the direction it wants to move, or
    /// zero to stand still.
    ///
    /// Five states, and which transitions are possible is decided by the species'
    /// demeanor and health — a sleeper wakes where a wanderer was already up, a
    /// coward breaks where a walker never does. The point of naming them is that a
    /// creature that lost you walks home rather than freezing, and one that is
    /// afraid runs rather than trading blows to the death.</summary>
    Vec StepAi(Husk k, Hero? target, float dt)
    {
        k.Think -= dt;
        k.Alerted = MathF.Max(0, k.Alerted - dt);

        // A neighbour's cry buys extra reach for a few seconds — an alerted
        // creature commits further than it normally would. It expires, which is
        // what keeps group aggro local instead of chaining across the map.
        float aggro = k.Def.AggroTiles * TILE * (k.Alerted > 0 ? 2f : 1f);
        float patrol = k.Def.PatrolTiles * TILE;
        float toTarget = target is null ? float.MaxValue : (target.Pos - k.Pos).Len();

        // Creatures never pursue into a safe zone; that is what makes it safe.
        bool huntable = target is not null && !InSafeZone(target.Pos.X, target.Pos.Y);

        // The cowardly break when badly hurt — but only with a threat close enough
        // to be worth running from.
        bool wantFlee = k.Def.FleeBelow > 0 && k.Hp < k.Def.MaxHp * k.Def.FleeBelow
                        && huntable && toTarget < aggro * 1.5f;

        // Ambushers keep still until you are almost on them; everyone else notices
        // at their full aggro range.
        float wake = k.Def.Demeanor == Demeanor.Ambusher ? aggro * 0.55f : aggro;

        var was = k.State;
        k.State = k.State switch
        {
            _ when wantFlee => AiState.Flee,
            AiState.Flee when !huntable || toTarget > aggro * 1.6f => AiState.Return,
            AiState.Sleep when huntable && toTarget < wake => AiState.Chase,
            AiState.Chase when !huntable || toTarget > aggro * 1.8f => AiState.Return,
            AiState.Patrol when huntable && toTarget < aggro => AiState.Chase,
            AiState.Return when huntable && toTarget < aggro * 0.7f => AiState.Chase,
            AiState.Return when (k.Home - k.Pos).Len() < TILE =>
                k.Def.Demeanor is Demeanor.Sleeper or Demeanor.Ambusher
                    ? AiState.Sleep : AiState.Patrol,
            _ => k.State,
        };

        // The instant it notices you — a sleeper stirring, an ambusher springing.
        // A beat of feedback so a thing that was scenery a moment ago does not
        // simply lurch into motion.
        if (was is AiState.Sleep && k.State is AiState.Chase)
            Floaters.Add(new FloatText(k.Pos + new Vec(0, -k.R - 10), "!", "#ffd77a"));

        switch (k.State)
        {
            case AiState.Sleep:
                return new Vec(0, 0);

            // Straight away from the threat. A cornered creature keeps trying —
            // there is nowhere better for it to go.
            case AiState.Flee:
                return target is null ? new Vec(0, 0) : (k.Pos - target.Pos).Norm();

            case AiState.Chase:
                return target is null ? new Vec(0, 0) : (target.Pos - k.Pos).Norm();

            case AiState.Return:
                return (k.Home - k.Pos).Norm() * 0.7f;

            default: // Patrol
                // Sentries barely leave their post; wanderers roam their full range.
                float loiter = k.Def.Demeanor == Demeanor.Sentry ? patrol * 0.35f : patrol;
                if (k.Think <= 0 || (k.PatrolTarget - k.Pos).Len() < TILE * 0.6f)
                {
                    k.Think = 1.6f + (float)_rng.NextDouble() * 2.6f;
                    float a = (float)_rng.NextDouble() * MathF.Tau;
                    float r = (float)_rng.NextDouble() * loiter;
                    k.PatrolTarget = k.Home + new Vec(MathF.Cos(a) * r, MathF.Sin(a) * r);
                }
                var step = k.PatrolTarget - k.Pos;
                return step.Len() < 4f ? new Vec(0, 0) : step.Norm() * 0.45f;
        }
    }

    /// <summary>A struck creature's cry, which rouses its neighbours.
    ///
    /// Bounded on purpose. The cry reaches only as far as the species is loud
    /// (<see cref="CreatureType.AlertTiles"/>), and what it grants — a few seconds
    /// of committed pursuit — expires. Protective creatures answer even mid-patrol
    /// and call a little louder; the rest only stir if they were idle. Together
    /// that is "a wolf helps a wolf, a bat colony turns as one" without the whole
    /// map emptying onto one hero.</summary>
    void Alert(Husk struck, Vec at)
    {
        float radius = (struck.Def.Protective ? struck.Def.AlertTiles + 2f : struck.Def.AlertTiles) * TILE;
        foreach (var k in Husks)
        {
            if (k == struck || k.Hp <= 0) continue;
            if ((k.Pos - at).Len() > radius) continue;

            k.Alerted = 4f;
            if (k.State is AiState.Sleep or AiState.Patrol || k.Def.Protective)
                k.State = AiState.Chase;
        }
        struck.Alerted = 4f;
    }
}
