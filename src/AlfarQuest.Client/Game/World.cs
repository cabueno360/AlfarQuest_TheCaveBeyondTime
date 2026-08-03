using AlfarQuest.Client.Models;

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
    // TRUNK is a soft block — a tree's foot. Unlike ROCK it stops you only at its
    // narrow core (see World.Collision), so you can weave between trunks and brush
    // past them instead of snagging on a full 32px cell.
    public const byte ROCK = 0, FLOOR = 1, WATER = 2, BRIDGE = 3, PATH = 4, TRUNK = 5;

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

    /// <summary>Hostile projectiles — the monsters' spit, bolts and missiles.
    /// Kept apart from the heroes' <see cref="Shots"/> so each only ever harms the
    /// other side, without a friendly-fire flag on every projectile.</summary>
    public List<Projectile> Bolts = new();

    public List<Slash> Slashes = new();

    public List<Particle> Fx = new();

    public List<Crystal> Crystals = new();

    public string Phase = "playing";   // playing | cleared | fallen
    public float ClearedFor;           // seconds since the chamber fell silent
    public float FallenFor;            // seconds since the last hero fell
    const float FallenHold = 4.2f;     // how long the fall holds before the party is stood back up
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

    /// <summary>Where the game opens. Stage 1 is the ring of hand-authored
    /// regions now, and it begins in the village — Ashwold, at the gate in from
    /// Seoshe, facing the bridge.</summary>
    public const string StartRegion = "r1_ashwold";

    public World(string[] heroKeys, float viewW, float viewH)
    {
        ViewW = viewW; ViewH = viewH;

        // A continued save stands the party where it left off — region and spot,
        // handed over by the campaign loader. One-shot: cleared once read, so a
        // later fresh run starts at the gate as ever. A region the maps no
        // longer know (or an old save whose Region was display text) falls back
        // to the village, and a spot that lands in rock is nudged clear.
        var resumeRegion = GameSession.ResumeRegion;
        var resumeAt = new Vec(GameSession.ResumeX, GameSession.ResumeY);
        GameSession.ResumeRegion = null;
        GameSession.ResumeX = GameSession.ResumeY = 0;

        bool resumed = resumeRegion is not null && LoadRegion(resumeRegion);
        if (!resumed && !LoadRegion(StartRegion))
            BuildOverworld();

        var start = Spawn;
        if (resumed && (resumeAt.X != 0 || resumeAt.Y != 0))
            start = Blocked(resumeAt, 14f) ? NearestOpen(resumeAt) : resumeAt;

        int slot = 0;
        foreach (var key in heroKeys.Take(3))
            Party.Add(new Hero(Lore.ByKey(key), start + new Vec((slot++ - 1) * 40f, 0)));
        Active = 0;
        Camera = start;

        // The two seams the shop needs from the engine: a sound queue for its coin,
        // and a way to release the held hero when its window closes. Set here rather
        // than at start-up because a rebuilt world is the one that should answer —
        // the last World constructed is the one being played.
        AudioBridge.Play = QueueUiSound;
        MerchantBridge.OnClose = CloseTrade;
        ReadBridge.OnClose = CloseReading;
        DialogueBridge.OnClose = CloseConversation;
    }

    public void Update(float dt, InputState input)
    {
        TorchTime += dt;
        // The world clock. Only moves here, so it holds still in every menu; slow, so
        // an afternoon is an afternoon. Wraps at midnight.
        GameSession.TimeOfDay = (GameSession.TimeOfDay + dt / GameSession.SecondsPerHour) % 24f;
        Shake = Math.Max(0, Shake - dt * 4f);
        // Emptied here so the sounds raised during this tick are exactly what the
        // frame's render payload carries — see World.Sfx.
        BeginSfxFrame(dt);

        // Hit-stop: the world holds still for a fraction of a second after an
        // impact so the blow lands instead of passing through. Particles and the
        // camera keep running — freezing those too would read as a stutter
        // rather than as weight — so only the simulation's clock is stopped.
        if (HitStop > 0)
        {
            HitStop = Math.Max(0, HitStop - dt);
            UpdateParticles(dt);
            UpdateFloaters(dt);
            return;
        }

        // --- defeat ---
        // A party with no one left standing used to be a soft-lock: the active
        // slot stayed on a corpse, dead heroes are skipped by every loop, and
        // nothing in the world could revive one. Now the fall is a beat — the
        // world holds while the banner reads — and then the party is stood back
        // up: a delve lost to the cave ends outside its mouth; on the surface
        // they come to at the region's spawn.
        if (Phase == "fallen")
        {
            FallenFor += dt;
            UpdateParticles(dt);
            UpdateFloaters(dt);
            if (FallenFor >= FallenHold) Revive();
            return;
        }
        if (Party.Count > 0 && !Party.Any(h => h.Alive))
        {
            // The last death is tallied here: the hero loop below does not run
            // this frame, and the count must not depend on it.
            foreach (var h in Party)
                if (!h.DeathCounted) { h.DeathCounted = true; StatBridge.Record(h.Def.Key, HeroStats.Kind.Deaths); }
            Phase = "fallen";
            FallenFor = 0f;
            Shake = 0.9f;
            return;
        }

        // --- hero switching ---
        // F1-F3 pick a hero directly; Tab cycles to the next living one (Shift+Tab
        // the previous). The number row belongs to the skills now. Suspended while a
        // shop is open: the buyer is chosen inside the window there, so Tab must not
        // quietly swap who the party is steering out from under it.
        if (Busy) { }
        else if (input.switchTo is >= 1 and <= 3 && input.switchTo <= Party.Count)
        {
            int idx = input.switchTo - 1;
            if (Party[idx].Alive) Active = idx;
        }
        else if (input.cycle != 0 && Party.Count > 0)
        {
            int n = Party.Count;
            for (int step = 1; step <= n; step++)
            {
                int idx = ((Active + input.cycle * step) % n + n) % n;
                if (Party[idx].Alive) { Active = idx; break; }
            }
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
            if (!h.Alive)
            {
                // Tallied once, on the frame they fall — not every frame they lie
                // there. Reset when they are back up, so a later death counts too.
                if (!h.DeathCounted) { h.DeathCounted = true; StatBridge.Record(h.Def.Key, HeroStats.Kind.Deaths); }
                continue;
            }
            h.DeathCounted = false;
            TickHeroEffects(h, dt);          // venom and the like gnaw here
            bool isActive = i == Active;
            h.Cool = Math.Max(0, h.Cool - dt);
            h.AbilityCool = Math.Max(0, h.AbilityCool - dt);
            h.PotionCool = Math.Max(0, h.PotionCool - dt);
            for (int s = 1; s <= 4; s++) h.SkillCool[s] = Math.Max(0, h.SkillCool[s] - dt);
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
            // Down and out: a husk whose health is gone (or already falling) plays
            // only its death reel, ticked in the sweep below — no more moving or
            // biting.
            if (k.Dying || k.Hp <= 0) continue;
            k.HitCool = Math.Max(0, k.HitCool - dt);
            k.AbilityCool = Math.Max(0, k.AbilityCool - dt);
            TickHuskEffects(k, dt);
            if (k.Rooted) k.Knock = new Vec(0, 0);   // the training dummy never budges
            var target = NearestHero(k.Pos);

            // Stunned or frozen: it still slides on any knockback it was dealt, but
            // it neither chooses a direction nor acts. The whole reason to carry a
            // hammer.
            if (k.Immobilised)
            {
                k.Pos = MoveBlocked(k.Pos, k.Knock, 13f);
                k.Knock *= 0.86f;
                k.Pos = ResolveCrystalCollision(k.Pos);
                k.Pos = Clamp(k.Pos, TILE);
                k.Flash = Math.Max(0, k.Flash - dt);
                continue;
            }

            var dir = StepAi(k, target, dt);
            float speed = k.Speed * (k.Enraged ? 1.4f : 1f);
            k.Pos = MoveBlocked(k.Pos, dir * speed * (1f - k.Slow) * dt + k.Knock, 13f);
            k.Knock *= 0.86f;
            if (target is not null)
            {
                // The one trick takes priority over the bite on the frame it
                // fires — a caster keeps its distance, a pounder leads with the
                // pound — and only while actually hunting, never mid-patrol.
                bool cast = k.State == AiState.Chase && MonsterAct(k, target);
                if (!cast && (target.Pos - k.Pos).Len() < 30 && k.HitCool <= 0 && target.IFrames <= 0)
                {
                    HurtHero(target, k.Def.Damage, (target.Pos - k.Pos).Norm(), k.Def.Magical);
                    k.HitCool = 0.8f;
                }
            }
            k.Pos = ResolveCrystalCollision(k.Pos);
            k.Pos = Clamp(k.Pos, TILE);
            k.Flash = Math.Max(0, k.Flash - dt);
        }
        FlushSummons();   // fold in any husks a boss called up mid-loop
        // Death is a beat, not a blink. A husk whose health is gone BEGINS its death
        // reel here — the kill and its loot are paid once, as it falls — and it is
        // removed only when the reel has run out. Flesh falls to dust; anything
        // conjured comes apart into light; the great husk falls a little slower.
        foreach (var k in Husks)
            if (k.Hp <= 0 && !k.Dying)
            {
                k.Dying = true;
                k.DeathT = k.Def.Boss ? 0.9f : 0.55f;
                AwardKill(k);
                RollLoot(k);
                Play(k.Def.Material == "flesh" ? "death_dust" : "death_magic", k.Pos);
            }
        foreach (var k in Husks) if (k.Dying) k.DeathT -= dt;
        Husks.RemoveAll(k => k.Dying && k.DeathT <= 0);
        UpdateFloaters(dt);

        // --- projectiles ---
        foreach (var s in Shots)
        {
            s.Pos += s.Vel * dt;
            s.Life -= dt;
            if (IsWallAt(s.Pos.X, s.Pos.Y)) { Play("hit_stone", s.Pos, s.Vel.Norm()); s.Life = 0; }
            foreach (var k in Husks)
            {
                if ((k.Pos - s.Pos).Len() < 22 + k.R)
                {
                    // Through the same Strike the melee cone uses, so a bolt gets
                    // the crit roll, the evasion miss and the creature's resistance
                    // exactly as a swing does. A skill bolt carries its own damage
                    // type and may leave an effect; an ordinary shot takes the
                    // owner's weapon type. An ownerless bolt (a test) just deals its
                    // number.
                    var owner = Party.FirstOrDefault(p => p.Def.Key == s.Owner);
                    if (owner is not null)
                    {
                        Strike(owner, k, s.Damage, s.Vel.Norm(), s.SkillType ?? owner.DamageType,
                               canCrit: s.CanCrit, canStun: false);
                        if (s.OnHit is { } eff) ApplyEffect(k, eff, EffectSeconds(eff), EffectMagnitude(eff), s.Owner);
                    }
                    else { k.Hp -= s.Damage; k.Flash = 0.15f; k.LastHitBy = s.Owner; }
                    k.Knock += s.Vel.Norm() * 46f;
                    Burst(s.Pos, s.Color, 6);
                    s.Life = 0; break;
                }
            }
        }
        Shots.RemoveAll(s => s.Life <= 0 || Outside(s.Pos));

        // --- hostile bolts (the monsters' spit and missiles) ---
        UpdateBolts(dt);

        // --- charging area bursts (the nova and the pound), landed on wind-up end ---
        UpdateAoe(dt);

        // --- slashes (melee arcs, damage applied on spawn; here just fade) ---
        foreach (var sl in Slashes) sl.Life -= dt;
        Slashes.RemoveAll(sl => sl.Life <= 0);

        // --- particles ---
        UpdateParticles(dt);
        UpdateAmbient(dt);

        // --- camera follows active hero ---
        var focus = Party[Active].Alive ? Party[Active].Pos : Camera;
        Camera += (focus - Camera) * Math.Min(1f, dt * 6f);
        Camera = new Vec(ClampCamera(Camera.X, ChamberW, ViewW), ClampCamera(Camera.Y, ChamberH, ViewH));

        UpdateNpcs();
        UpdateSpeech(dt);
        UpdateSeams();
        UpdatePortals();
        UpdateExaminables();
        UpdateRewards(dt);
        if (input.interact) Interact();

        // Stage 1's descent is a doorway now, not a trap: the mouth is a Portal
        // read from the map (see BuildOverworldFromTmx), so going down asks for
        // [E] like every other door instead of firing the moment a hero strays
        // within 46 pixels of it. UsePortal carries the party down.
        if (Stage == 1) return;

        // Interiors (Stage 3) are safe rooms — no husks, no clearing, no descent.
        // The only way on is back out through the door.
        if (Stage != 2) return;

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

    /// <summary>Keeps the camera inside the map — but a room smaller than the screen
    /// (an interior) has no inside to keep it in, so it centres instead. Clamping
    /// there would ask for min &gt; max and throw, which used to freeze the whole
    /// game the instant you stepped through a door.</summary>
    static float ClampCamera(float c, float span, float view) =>
        span <= view ? span / 2f : Math.Clamp(c, view / 2f, span - view / 2f);

    static float AngleDiff(float a, float b)
    {
        float d = a - b;
        while (d > Math.PI) d -= (float)(2 * Math.PI);
        while (d < -Math.PI) d += (float)(2 * Math.PI);
        return d;
    }
}
