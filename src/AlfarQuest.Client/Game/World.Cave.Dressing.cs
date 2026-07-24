namespace AlfarQuest.Client.Game;

// =====================================================================
//  Per-region dressing kits, crystal pillars and husk spawning.
//  Landmarks stay unique; mining debris repeats.
// =====================================================================
public partial class World
{
    // -----------------------------------------------------------------
    //  Dressing: every region gets its own kit, so no two rooms read alike.
    //  Fields are [decoSheetCol, decoSheetRow, scale, solid?] — the client
    //  looks these up in Miner_Decorations.png.
    // -----------------------------------------------------------------
    record Kit(int Cx, int Cy, float S, bool Solid, int Count);

    static readonly Dictionary<string, Kit[]> RegionKits = new()
    {
        ["entrance"] = new[] {
            new Kit(5, 7, 0.46f, false, 2),    // barrel and pick
            new Kit(6, 2, 0.44f, false, 1),    // chest
            new Kit(8, 3, 0.46f, false, 2),   // spoil heap
        },
        ["mine"] = new[] {
            new Kit(9, 13, 0.52f, false, 4),   // rock pile and pickaxe
            new Kit(8, 3,  0.46f, false, 5),  // spoil heaps
            new Kit(5, 9,  0.46f, false, 3),   // barrels
            new Kit(6, 10, 0.58f, true, 1),   // miner statue
            new Kit(9, 3,  0.44f, false, 3),  // heaped chains
            new Kit(6, 2,  0.44f, false, 2),   // chests
        },
        ["tunnels"] = new[] {
            new Kit(8, 13, 0.52f, false, 5),   // boulders
            new Kit(9, 3,  0.44f, false, 3),  // chains
            new Kit(2, 3,  0.44f, false, 4),  // slabs
        },
        ["lake"] = new[] {
            new Kit(8, 13, 0.52f, false, 3),   // boulders on the shore
            new Kit(2, 3,  0.44f, false, 3),  // wet slabs
            new Kit(9, 0,  0.54f, true, 1),   // gargoyle well
        },
        ["crystal"] = new[] {
            new Kit(8, 11, 0.58f, true, 2),   // crystal pedestal
            new Kit(9, 11, 0.58f, true, 1),   // green crystal altar
            new Kit(9, 12, 0.56f, true, 2),   // teal shrine
            new Kit(8, 9,  0.54f, true, 1),   // basin of orbs
        },
        ["ruins"] = new[] {
            new Kit(8, 4,  0.62f, true, 4),   // toppled pillars
            new Kit(3, 10, 0.62f, true, 2),   // rune monument
            new Kit(2, 8,  0.54f, false, 3),  // rune tablets
            new Kit(3, 8,  0.70f, true, 1),   // crystal arch
        },
        ["sanctuary"] = new[] {
            new Kit(6, 4,  0.62f, true, 2),   // chained obelisk
            new Kit(5, 0,  0.52f, true, 3),   // green-flame braziers
            new Kit(2, 8,  0.54f, false, 2),  // tablets
        },
        ["boss"] = new[] {
            new Kit(5, 0,  0.52f, true, 6),   // ring of braziers
            new Kit(8, 4,  0.62f, true, 3),   // fallen pillars
        },
    };

    void DressRegions()
    {
        foreach (var r in CaveRegions)
        {
            if (!RegionKits.TryGetValue(r.Key, out var kit)) continue;
            foreach (var k in kit)
                for (int n = 0; n < k.Count; n++)
                    PlaceProp(r, k);
        }
    }

    void PlaceProp(Region r, Kit k)
    {
        for (int tries = 0; tries < 120; tries++)
        {
            float a = (float)_rng.NextDouble() * MathF.Tau;
            float rad = MathF.Sqrt((float)_rng.NextDouble());       // even area coverage
            float x = (r.Cx + MathF.Cos(a) * rad * (r.Rx - 1.2f)) * TILE + TILE * 0.5f;
            float y = (r.Cy + MathF.Sin(a) * rad * (r.Ry - 1.2f)) * TILE + TILE * 0.5f;

            if (TileAtWorld(x, y) != FLOOR) continue;               // never on water, bridge or rock
            if (TileAtWorld(x - 22, y) != FLOOR || TileAtWorld(x + 22, y) != FLOOR) continue;
            if (TileAtWorld(x, y - 22) != FLOOR || TileAtWorld(x, y + 14) != FLOOR) continue;
            if ((new Vec(x, y) - Spawn).Len() < 130) continue;
            if ((new Vec(x, y) - Exit).Len() < 150) continue;

            // Anything that blocks needs room around it, checked in all eight
            // directions — otherwise it can plug a passage and the player is
            // stopped by something they cannot see is in the way.
            if (k.Solid)
            {
                bool tight = false;
                for (int a2 = 0; a2 < 8 && !tight; a2++)
                {
                    float ang2 = a2 * MathF.PI / 4f;
                    for (float dd = 24; dd <= 72; dd += 24)
                        if (TileAtWorld(x + MathF.Cos(ang2) * dd, y + MathF.Sin(ang2) * dd) != FLOOR) { tight = true; break; }
                }
                if (tight) continue;
            }

            bool clash = false;
            foreach (var p in Props) if ((new Vec(p.X, p.Y) - new Vec(x, y)).Len() < 74) { clash = true; break; }
            if (clash) continue;

            Props.Add(new Prop {
                X = x, Y = y, Cx = k.Cx, Cy = k.Cy, S = k.S,
                Solid = k.Solid, R = k.Solid ? 128 * k.S * 0.20f : 0,
                Flip = _rng.Next(2) == 0,
            });
            return;
        }
    }

    // Crystal pillars stand in the open, never inside rock and never blocking
    // the entrance or the way out.
    void BuildChamber()
    {
        int want = 12 + _rng.Next(6);
        int guard = 0;
        while (Crystals.Count < want && guard++ < 900)
        {
            float r = 38 + (float)_rng.NextDouble() * 30;
            var p = new Vec(TILE * 3 + (float)_rng.NextDouble() * (ChamberW - TILE * 6),
                            TILE * 3 + (float)_rng.NextDouble() * (ChamberH - TILE * 6));
            if (Blocked(p, r + 10)) continue;
            if ((p - Spawn).Len() < 200 || (p - Exit).Len() < 170) continue;
            bool clash = false;
            foreach (var c in Crystals) if ((c.Pos - p).Len() < c.R + r + 50) { clash = true; break; }
            if (!clash) Crystals.Add(new Crystal(p, r));
        }
    }

    void SpawnHusks(int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vec p = Spawn;
            for (int guard = 0; guard < 300; guard++)
            {
                var c = new Vec(TILE * 3 + (float)_rng.NextDouble() * (ChamberW - TILE * 6),
                                TILE * 3 + (float)_rng.NextDouble() * (ChamberH - TILE * 6));
                if (Blocked(c, 20f)) continue;                       // never inside rock
                if ((c - Spawn).Len() < 300) continue;               // not on top of the party
                if ((ResolveCrystalCollision(c) - c).Len() > 0.01f) continue;   // nor inside a pillar
                p = c; break;
            }
            Husks.Add(new Husk(p));
        }
    }
}
