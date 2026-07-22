namespace AlfarQuest.Client.Game;

// =====================================================================
//  The render payload. Split in two on purpose: Render() runs every
//  frame and must stay small, TakeSnapshot() only on a world rebuild.
// =====================================================================
public partial class World
{
    public WorldSnapshot TakeSnapshot() => new()
    {
        rev = Rev, tile = TILE, cols = COLS, rows = ROWS, stage = Stage,
        chamberW = ChamberW, chamberH = ChamberH,
        map = MapRows(),
        props = Props.ConvertAll(p => new RProp { x = p.X, y = p.Y, s = p.S, cx = p.Cx, cy = p.Cy, k = p.Kind, v = p.Variant, flip = p.Flip }),
        arches = Arches.ConvertAll(a => new RVec { x = a.X, y = a.Y }),
        npcs = Npcs.ConvertAll(n => new RNpc { x = n.Pos.X, y = n.Pos.Y, f = n.Facing,
                                               kind = n.Def.Kind, name = n.Def.Name }),
        spawn = new RVec { x = Spawn.X, y = Spawn.Y },
        exit = new RVec { x = Exit.X, y = Exit.Y },
    };

    Vec ScreenToWorld(Vec screen, InputState input) =>
        Camera - new Vec(input.viewW / 2, input.viewH / 2) + screen;

    // ---------------------------------------------------------------
    //  Build the frame's render payload
    // ---------------------------------------------------------------
    public RenderState Render()
    {
        var ents = new List<REnt>();
        var lights = new List<RLight>();

        foreach (var c in Crystals)
        {
            ents.Add(new REnt { t = "crystal", x = c.Pos.X, y = c.Pos.Y, r = c.R });
            lights.Add(new RLight { x = c.Pos.X, y = c.Pos.Y, rad = c.R * 2.4f, c = "#3a4d8a" });
        }

        // exit glow appears once the chamber is cleared
        if (Phase == "cleared")
        {
            ents.Add(new REnt { t = "exit", x = ExitPos.X, y = ExitPos.Y, r = 60 });
            lights.Add(new RLight { x = ExitPos.X, y = ExitPos.Y, rad = 260, c = "#8fd0ff" });
        }

        foreach (var p in Fx)
            ents.Add(new REnt
            {
                t = "particle", x = p.Pos.X, y = p.Pos.Y,
                r = p.Size, c = p.Color, life = p.Life,
                t01 = p.MaxLife > 0 ? p.Life / p.MaxLife : 0,
                add = p.Additive,
            });

        foreach (var k in Husks)
            ents.Add(new REnt { t = "husk", x = k.Pos.X, y = k.Pos.Y, r = k.R, hp = k.Hp, mhp = k.MaxHp,
                                flash = k.Flash, name = k.Def.Kind, s = k.Def.Scale });

        foreach (var s in Shots)
            ents.Add(new REnt { t = "proj", x = s.Pos.X, y = s.Pos.Y, r = 6, c = s.Color, f = (float)Math.Atan2(s.Vel.Y, s.Vel.X) });

        // Drawn the same way as the heroes' shots — a bolt is a bolt — but from
        // their own colours, so the poison reads green and the missile violet.
        foreach (var b in Bolts)
            ents.Add(new REnt { t = "proj", x = b.Pos.X, y = b.Pos.Y, r = 6, c = b.Color, f = (float)Math.Atan2(b.Vel.Y, b.Vel.X) });

        foreach (var sl in Slashes)
            ents.Add(new REnt { t = sl.Nova ? "nova" : "slash", x = sl.Pos.X, y = sl.Pos.Y, f = sl.Angle, c = sl.Color, life = sl.Life, r = sl.Radius });

        for (int i = 0; i < Party.Count; i++)
        {
            var h = Party[i];
            ents.Add(new REnt
            {
                t = "hero", x = h.Pos.X, y = h.Pos.Y, r = 16, f = h.Facing,
                c = h.Def.ColorPrimary, a = h.Def.ColorAccent, hero = true,
                active = i == Active, dead = !h.Alive, flash = h.Flash, iframe = h.IFrames,
                atk = h.AttackAnim, abl = h.AbilityAnim, name = h.Def.HeroClass
            });
            h.Flash = Math.Max(0, h.Flash - 0.016f);
        }

        // torch / mage-light on the active hero, with a flicker
        var act = Party[Active];
        float flick = 0.85f + (float)Math.Sin(TorchTime * 11f) * 0.06f + (float)Math.Sin(TorchTime * 3.3f) * 0.05f;
        lights.Add(new RLight { x = act.Pos.X, y = act.Pos.Y, rad = 300 * flick, c = act.Def.HeroClass == "Mage" ? "#2f8f88" : "#e8a94a" });
        foreach (var (h, i) in Party.Select((h, i) => (h, i)))
            if (i != Active && h.Alive) lights.Add(new RLight { x = h.Pos.X, y = h.Pos.Y, rad = 120, c = "#5566aa" });

        var hud = new RHud
        {
            phase = Phase,
            enemies = Husks.Count,
            asleep = Husks.Count(k => k.State == AiState.Sleep),
            chasing = Husks.Count(k => k.State == AiState.Chase),
            fleeing = Husks.Count(k => k.State == AiState.Flee),
            bolts = Bolts.Count,
            enemyKinds = string.Join(",", Husks.Select(k => k.Def.Id).Distinct()),
            nearestTiles = NearestCreatureTiles(out var nd, out var nk),
            nearDx = nd.X, nearDy = nd.Y,
            nearKind = nk?.Def.Id ?? "", nearState = nk?.State.ToString() ?? "",
            nearAbility = nk?.Def.Ability.ToString() ?? "",
            casterTiles = NearestCasterTiles(out var cd), casterDx = cd.X, casterDy = cd.Y,
            inSafeZone = Party.Count > 0 && Active < Party.Count
                         && InSafeZone(Party[Active].Pos.X, Party[Active].Pos.Y),
            heroTx = Party.Count > 0 && Active < Party.Count ? (int)(Party[Active].Pos.X / TILE) : 0,
            heroTy = Party.Count > 0 && Active < Party.Count ? (int)(Party[Active].Pos.Y / TILE) : 0,
            castsFired = MonsterCastsFired, boltsFired = MonsterBoltsFired,
            crits = CritCount, misses = MissCount, blocks = BlockCount,
            dodges = DodgeCount, stuns = StunCount,
            stunnedNow = Husks.Count(k => k.Immobilised),
            recentHits = [.. RecentHits],
            stage = Stage,
            level = Level,
            region = RegionName,
            shake = Shake,
            clearedFor = ClearedFor,
            objective = Stage == 1 ? "Find the old mine" : "",
            dropsTried = LootBridge.Attempted,
            dropsDelivered = LootBridge.Delivered,
            // What [E] would do right now, in the same order Interact() resolves
            // it — so the prompt can never offer one thing and the key do another.
            promptName = Talking is not null ? ""
                : ThingInReach?.Name ?? NpcInReach?.Def.Name ?? "",
            promptVerb = Talking is not null ? ""
                : ThingInReach is { } t ? VerbFor(t) : NpcInReach is not null ? "Talk to" : "",
            // Crafting is gated on standing beside someone who can do it.
            atCraftsman = (Talking ?? NpcInReach)?.Def.Services.HasFlag(NpcServices.Crafting) ?? false,
            talkName = Talking?.Def.Name ?? "",
            talkRole = Talking?.Def.Role ?? "",
            talkLine = Talking?.CurrentLine ?? "",
            message = Phase == "cleared"
                ? "The husks are still. The tunnel above stands open — walk into it to descend."
                : "",
            heroLevel = HeroProgressNow.Level,
            xp = HeroProgressNow.Xp,
            xpNext = HeroProgressNow.XpNext,
            xpFrac = HeroProgressNow.Fraction,
            levelUpGlow = LevelUpGlow,
            hitStop = HitStop,
            particles = Fx.Count,
            particleCap = MaxParticles,
            particlesEmitted = FxEmitted,
            xpAwards = RewardBridge.Awarded,
            xpTotal = RewardBridge.TotalXp,
            claimsHeld = RewardBridge.Claimed().Count,
            rewardsLeft = Discoveries.Count(d => !d.Found) + Interactables.Count(t => t.Offers),
            party = Party.Select((h, i) => new RHero
            {
                key = h.Def.Key, name = h.Def.Name, cls = h.Def.HeroClass, hp = Math.Max(0, h.Hp), mhp = h.MaxHp,
                mana = Math.Max(0, h.Mana), mmana = h.MaxMana,
                stam = Math.Max(0, h.Stamina), mstam = h.MaxStamina,
                // Each hero's own progression, so switching shows theirs and a test
                // can prove a kill paid one hero and not the others.
                level = CharacterStats.ProgressOf(h.Def.Key).Level,
                xp = CharacterStats.ProgressOf(h.Def.Key).Xp,
                xpNext = CharacterStats.ProgressOf(h.Def.Key).XpNext,
                // The weapon as combat reads it, so a test can tell the weapons apart.
                dmgMin = (int)MathF.Round(CharacterStats.For(h.Def.Key).WeaponMin),
                dmgMax = (int)MathF.Round(CharacterStats.For(h.Def.Key).WeaponMax),
                dmgType = CharacterStats.For(h.Def.Key).WeaponDamage.ToString(),
                weaponSpeed = CharacterStats.For(h.Def.Key).WeaponSpeedFactor,
                stunChance = CharacterStats.For(h.Def.Key).WeaponStunChance,
                dodge = CharacterStats.For(h.Def.Key).DodgeChance,
                accuracy = CharacterStats.For(h.Def.Key).Accuracy,
                active = i == Active, dead = !h.Alive, color = h.Def.ColorAccent,
                abilityReady = h.AbilityCool <= 0 && h.Mana >= h.AbilityCost,
                abilityAffordable = h.Mana >= h.AbilityCost,
                slot = i + 1
            }).ToList()
        };

        return new RenderState { cam = new RVec { x = Camera.X, y = Camera.Y },
                                 chamberW = ChamberW, chamberH = ChamberH, level = Level,
                                 tile = TILE, stage = Stage, rev = Rev,
                                 ents = ents, lights = lights, hud = hud,
                                 // A shallow copy: the frame owns this list, and the next
                                 // Update clears the original out from under it.
                                 sounds = new List<RSound>(_sounds),
                                 floats = Floaters.ConvertAll(f => new RFloat {
                                     x = f.Pos.X, y = f.Pos.Y, life = f.Life, text = f.Text, c = f.Colour }) };
    }
}
