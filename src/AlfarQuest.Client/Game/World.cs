namespace AlfarQuest.Client.Game;

// =====================================================================
//  The simulation. State and the per-frame Update() live here; every
//  other concern is a partial in World.*.cs.
// =====================================================================
public partial class World
{
    // The chamber is a tile grid now, not an open box. TILE matches the caves
    // tileset so walls line up with the art exactly.
    public const int TILE = 32;

    // Size varies by stage — the overworld is 80x80, the cave 88x56 — so these
    // are instance fields, not constants.
    public int COLS = 88, ROWS = 56;

    float ChamberW => COLS * TILE;

    float ChamberH => ROWS * TILE;

    // 0 blocks (rock outside, cliff/mountain in the open), 1 and 4 are walkable
    // ground, 2 is water, 3 a bridge deck.
    public const byte ROCK = 0, FLOOR = 1, WATER = 2, BRIDGE = 3, PATH = 4;

    public byte[,] Tiles = new byte[88, 56];

    public int Stage = 1;              // 1 = the approach, 2 = the cave
    public int Rev;                    // bumped on every world rebuild
    public List<Prop> Props = new();

    public List<Vec> Arches = new();   // cave mouths at region entrances
    public Vec Spawn, Exit;

    public float ViewW, ViewH;

    public Vec Camera;

    public List<Hero> Party = new();

    public int Active;

    public List<Husk> Husks = new();

    public List<Projectile> Shots = new();

    public List<Slash> Slashes = new();

    public List<Particle> Fx = new();

    public List<Crystal> Crystals = new();

    public string Phase = "playing";   // playing | cleared
    public float ClearedFor;           // seconds since the chamber fell silent
    public float TorchTime;

    public float Shake;

    public int Level = 1;

    private Random _rng = new(42);

    // Corridor widths vary on purpose: tight crawls, ordinary passages and a
    // couple of broad halls give the map a rhythm instead of one uniform grid.
    static readonly (string A, string B, int W)[] Links =
    {
        ("entrance", "mine",      3),
        ("entrance", "lake",      5),
        ("mine",     "tunnels",   2),   // tight crawl
        ("tunnels",  "crystal",   3),
        ("lake",     "crystal",   4),
        ("lake",     "ruins",     4),
        ("ruins",    "sanctuary", 3),
        ("sanctuary","boss",      4),
        ("lake",     "boss",      2),   // shortcut loop, easy to miss
        ("mine",     "lake",      2),   // back way into the workings
    };

    public World(string[] heroKeys, float viewW, float viewH)
    {
        ViewW = viewW; ViewH = viewH;
        BuildOverworld();                 // Stage 1: the approach
        int slot = 0;
        foreach (var key in heroKeys.Take(3))
            Party.Add(new Hero(Lore.ByKey(key), Spawn + new Vec((slot++ - 1) * 40f, 0)));
        Active = 0;
        Camera = Spawn;
    }

    public void Update(float dt, InputState input)
    {
        TorchTime += dt;
        Shake = Math.Max(0, Shake - dt * 4f);

        // --- hero switching ---
        if (input.switchTo is >= 1 and <= 3 && input.switchTo <= Party.Count)
        {
            int idx = input.switchTo - 1;
            if (Party[idx].Alive) Active = idx;
        }
        if (!Party[Active].Alive)
        {
            var next = Party.FindIndex(h => h.Alive);
            if (next >= 0) Active = next;
        }

        var world = ScreenToWorld(new Vec(input.mouseX, input.mouseY), input);

        // --- heroes ---
        for (int i = 0; i < Party.Count; i++)
        {
            var h = Party[i];
            if (!h.Alive) continue;
            bool isActive = i == Active;
            h.Cool = Math.Max(0, h.Cool - dt);
            h.AbilityCool = Math.Max(0, h.AbilityCool - dt);
            h.DashCool = Math.Max(0, h.DashCool - dt);
            h.IFrames = Math.Max(0, h.IFrames - dt);
            h.AttackAnim = Math.Max(0, h.AttackAnim - dt);
            h.AbilityAnim = Math.Max(0, h.AbilityAnim - dt);
            // Vigour knits wounds while you walk.
            if (h.HealthRegen > 0 && h.Hp < h.MaxHp)
                h.Hp = Math.Min(h.MaxHp, h.Hp + h.HealthRegen * dt);

            // Mana and stamina refill on their own, always — they are the cost of
            // acting, not a reward for resting, and a pool that only refilled when
            // idle would make standing still the optimal play.
            h.Mana = Math.Min(h.MaxMana, h.Mana + h.ManaRegen * dt);
            h.Stamina = Math.Min(h.MaxStamina, h.Stamina + h.StaminaRegen * dt);

            if (isActive) UpdateActiveHero(h, dt, input, world);
            // Slot is derived from party index, not a running count, so a
            // companion doesn't hop to the other side when the third one dies.
            else UpdateCompanion(h, dt, i > Active ? i - 1 : i);

            h.Pos = ResolveCrystalCollision(h.Pos);
            h.Pos = Clamp(h.Pos, TILE);
        }

        SeparateParty();

        // --- husks ---
        foreach (var k in Husks)
        {
            k.HitCool = Math.Max(0, k.HitCool - dt);
            var target = NearestHero(k.Pos);
            var dir = StepAi(k, target, dt);
            k.Pos = MoveBlocked(k.Pos, dir * k.Speed * dt + k.Knock, 13f);
            k.Knock *= 0.86f;
            if (target is not null)
            {
                if ((target.Pos - k.Pos).Len() < 30 && k.HitCool <= 0 && target.IFrames <= 0)
                {
                    // Steady Guard turns some blows aside entirely. Written as a
                    // branch rather than `continue`: this sits inside the husk
                    // loop, and skipping the rest of the iteration would also skip
                    // that husk's collision resolve, clamp and flash decay.
                    if (target.BlockChance > 0 && _rng.NextDouble() < target.BlockChance)
                    {
                        Burst(target.Pos, "#cfd6ff", 4);
                    }
                    else
                    {
                        target.Hp -= target.Absorb(k.Def.Damage, k.Def.Magical);
                        target.Flash = 0.15f;
                        Shake = Math.Max(Shake, 0.35f);
                    }
                    k.HitCool = 0.8f;
                }
            }
            k.Pos = ResolveCrystalCollision(k.Pos);
            k.Pos = Clamp(k.Pos, TILE);
            k.Flash = Math.Max(0, k.Flash - dt);
        }
        // Roll before removing: the corpse still knows what species it was.
        foreach (var dead in Husks.Where(k => k.Hp <= 0)) { AwardKill(dead); RollLoot(dead); }
        Husks.RemoveAll(k => k.Hp <= 0);
        UpdateFloaters(dt);

        // --- projectiles ---
        foreach (var s in Shots)
        {
            s.Pos += s.Vel * dt;
            s.Life -= dt;
            if (IsWallAt(s.Pos.X, s.Pos.Y)) { Burst(s.Pos, s.Color, 4); s.Life = 0; }
            foreach (var k in Husks)
            {
                if ((k.Pos - s.Pos).Len() < 22 + k.R)
                {
                    k.Hp -= s.Damage; k.Flash = 0.15f;
                    k.Knock += s.Vel.Norm() * 46f;
                    Burst(s.Pos, s.Color, 6);
                    s.Life = 0; break;
                }
            }
        }
        Shots.RemoveAll(s => s.Life <= 0 || Outside(s.Pos));

        // --- slashes (melee arcs, damage applied on spawn; here just fade) ---
        foreach (var sl in Slashes) sl.Life -= dt;
        Slashes.RemoveAll(sl => sl.Life <= 0);

        // --- particles ---
        foreach (var p in Fx) { p.Pos += p.Vel * dt; p.Vel *= 0.9f; p.Life -= dt; }
        Fx.RemoveAll(p => p.Life <= 0);

        // --- camera follows active hero ---
        var focus = Party[Active].Alive ? Party[Active].Pos : Camera;
        Camera += (focus - Camera) * Math.Min(1f, dt * 6f);
        Camera = new Vec(
            Math.Clamp(Camera.X, ViewW / 2, ChamberW - ViewW / 2),
            Math.Clamp(Camera.Y, ViewH / 2, ChamberH - ViewH / 2));

        UpdateNpcs();
        UpdateRewards(dt);
        if (input.interact) Interact();

        // Stage 1 has no enemies: the objective is simply to reach the mine.
        if (Stage == 1)
        {
            if (Party[Active].Alive && (Party[Active].Pos - CaveMouth).Len() < 46f) EnterCave();
            return;
        }

        if (Husks.Count == 0 && Phase == "playing") Phase = "cleared";
        if (Phase == "cleared")
        {
            ClearedFor += dt;   // lets the banner time itself out

            // Short grace period so the descent can't trigger on the same frame
            // the last husk dies, with the player still holding a movement key.
            if (ClearedFor > 0.8f && Party[Active].Alive &&
                (Party[Active].Pos - ExitPos).Len() < 62f)
                Descend();
        }
    }

    static float AngleDiff(float a, float b)
    {
        float d = a - b;
        while (d > Math.PI) d -= (float)(2 * Math.PI);
        while (d < -Math.PI) d += (float)(2 * Math.PI);
        return d;
    }
}
