using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  What monsters do besides bite.
//
//  One entry point — MonsterAct — switched on the species' single
//  ability, and one hostile-projectile loop that mirrors the heroes'.
//  Simple and readable on purpose: these are stage-one creatures, and a
//  telegraphed pound or a lobbed glob is all the depth the first map
//  wants. Bosses get real kits later, elsewhere.
// =====================================================================
public partial class World
{
    /// <summary>Every monster ability ever performed, and every hostile bolt ever
    /// loosed — cumulative, never reset. The live counts cannot answer "did that
    /// creature just cast?" because a bolt at close range is gone in under a
    /// frame; a total that only climbs can.</summary>
    public int MonsterCastsFired, MonsterBoltsFired, HusksSummoned;

    /// <summary>Husks a boss summoned THIS frame. They cannot be added straight to
    /// <see cref="Husks"/> — that list is mid-iteration when a summon fires — so they
    /// wait here and are folded in once the husk loop is done.</summary>
    readonly List<Husk> _summoned = new();

    /// <summary>Performs a creature's one trick, if it has one and it is off
    /// cooldown, and reports whether it did — the caller holds off the ordinary
    /// bite on a frame the creature cast instead.</summary>
    bool MonsterAct(Husk k, Hero target)
    {
        // A boss rotates through a KIT; everything else has its one trick.
        var ability = k.Def.Boss && k.Def.Kit.Count > 0
            ? k.Def.Kit[k.KitIndex % k.Def.Kit.Count]
            : k.Def.Ability;
        if (ability == MonsterAbility.None || k.AbilityCool > 0) return false;

        float range = k.Def.AbilityRangeTiles * TILE;
        var to = target.Pos - k.Pos;
        float dist = to.Len();
        if (dist > range) return false;

        var dir = to.Norm();
        // Below its enrage line a boss speeds up and its cooldowns shorten.
        bool enraged = k.Def.EnrageBelow > 0 && k.Hp <= k.MaxHp * k.Def.EnrageBelow;
        k.AbilityCool = k.Def.AbilityCooldown * (enraged ? 0.55f : 1f);
        if (k.Def.Boss) k.KitIndex++;      // advance to the next trick in the kit
        MonsterCastsFired++;

        switch (ability)
        {
            // Lobbed and flung shots. Same shape, different dressing and one of
            // them magical — the spit is slow and green, the crystal fast and
            // bright, the missile a wandering mote of raw magic.
            case MonsterAbility.PoisonSpit:
                Bolt(k, dir, 320f, "#8fd24a", magical: false, onHit: StatusEffectKind.Poison);
                break;
            case MonsterAbility.CrystalBolt:
                Bolt(k, dir, 380f, "#9fe4ff", magical: false);
                break;
            case MonsterAbility.MagicMissile:
                // A little off-true, so the swarm's magic scatters rather than
                // snipes — and drifts, which reads as unruly rather than aimed.
                Bolt(k, Jitter(dir, 0.25f), 300f, "#c98fff", magical: true);
                break;

            // The bat's lunge: a burst of speed straight at you, spent through the
            // knock channel so the movement solver still keeps it out of walls.
            case MonsterAbility.Dash:
                k.Knock += dir * 300f;
                k.Flash = 0.05f;
                Play("dash_dust", k.Pos, dir * -1f);
                break;

            // The stone things' pound: a short, heavy area hit with a telegraphing
            // ring and a shove. Everything close takes it, so standing on top of a
            // walker to trade blows is punished.
            case MonsterAbility.Smash:
                Smash(k);
                break;

            // --- the Guardian's kit ---
            case MonsterAbility.CrystalVolley:
                CrystalVolley(k, dir);
                break;
            case MonsterAbility.ShardNova:
                ShardNova(k, enraged);
                break;
            case MonsterAbility.SummonHusks:
                SummonHusks(k, enraged ? 3 : 2);
                break;
        }
        return true;
    }

    /// <summary>A fanned spray of five crystal shards, so backing straight off does
    /// not clear it — you have to break the line.</summary>
    void CrystalVolley(Husk k, Vec dir)
    {
        float mid = MathF.Atan2(dir.Y, dir.X);
        for (int i = -2; i <= 2; i++)
        {
            float a = mid + i * 0.17f;
            Bolt(k, new Vec(MathF.Cos(a), MathF.Sin(a)), 360f, "#9fe4ff", magical: true);
        }
        Shake = MathF.Max(Shake, 0.3f);
    }

    /// <summary>A wide burst of crystal light from the Guardian — heavier than a
    /// stone-pound and it reaches, so the answer is to get OUT, not to trade.</summary>
    void ShardNova(Husk k, bool enraged)
    {
        float radius = TILE * (enraged ? 3.7f : 3.0f);
        // Charged, not instant: the danger ring blooms first as a warning and the
        // crystal light only goes off when it fills, so there is a real window to
        // get out of the heart. Enraged, the window is tighter.
        ScheduleAoe(k.Pos, radius, k.Def.Damage + 6, magical: true, push: 28f,
                    colour: "#9fe4ff", windup: enraged ? 0.55f : 0.8f);
        Shake = MathF.Max(Shake, 0.3f);
        Play("hit_crystal", k.Pos);      // the charge whining up
    }

    /// <summary>Pulls fresh husks from the walls around the Guardian — pressure, so
    /// the fight is not just kiting the one big thing.</summary>
    void SummonHusks(Husk k, int n)
    {
        Play("hit_crystal", k.Pos);
        for (int i = 0; i < n; i++)
        {
            float a = (float)_rng.NextDouble() * MathF.Tau;
            var p = k.Pos + new Vec(MathF.Cos(a), MathF.Sin(a)) * (TILE * 2.2f);
            if (Blocked(p, 18f)) p = NearestOpen(p);
            // Deferred: Husks is being iterated right now (this fired from inside the
            // husk loop). FlushSummons folds these in once the loop is done.
            _summoned.Add(new Husk(p) { State = AiState.Chase });
            HusksSummoned++;   // cumulative — the summoned husk may be cut down before a snapshot sees it
        }
    }

    /// <summary>Adds the frame's summoned husks to the field, once the husk loop that
    /// spawned them has finished iterating.</summary>
    void FlushSummons()
    {
        if (_summoned.Count == 0) return;
        Husks.AddRange(_summoned);
        _summoned.Clear();
    }

    /// <summary>Spawns one hostile projectile from a creature toward where it is
    /// facing the party.</summary>
    void Bolt(Husk k, Vec dir, float speed, string colour, bool magical, StatusEffectKind? onHit = null)
    {
        Bolts.Add(new Projectile(k.Pos + dir * (k.R + 6f), dir * speed, k.Def.Damage, colour, 2.4f,
                                 owner: "", magical: magical, onHit: onHit));
        MonsterBoltsFired++;
        Play(magical ? "frost" : "hit_crystal", k.Pos + dir * k.R, dir, 0.5f);
    }

    /// <summary>The ground-pound. Damage lands with the ring, not before it — the
    /// ring is the tell — and pushes what it hits back.</summary>
    void Smash(Husk k)
    {
        // The tell is the point: the ring blooms as the stone thing rears up, and
        // the pound only lands when it closes — step off the marked ground in time
        // and you take nothing.
        ScheduleAoe(k.Pos, TILE * 2.2f, k.Def.Damage, magical: k.Def.Magical, push: 22f,
                    colour: "#c9a06a", windup: 0.5f);
        Shake = MathF.Max(Shake, 0.25f);
        Play("hit_stone", k.Pos);        // the wind-up heave
    }

    /// <summary>A charging area burst — a telegraphed danger ring that lands its blow
    /// only when its wind-up runs out (see <see cref="UpdateAoe"/>). Both the boss's
    /// nova and the stone things' pound go through here, so every ground-slam reads
    /// the same way: a ring you are given time to leave.</summary>
    public sealed class TelegraphedAoe
    {
        public Vec Pos;
        public float Radius, Timer, Total, Push;
        public int Damage;
        public bool Magical;
        public string Colour = "#ffffff";
    }

    /// <summary>The area bursts currently charging. Short-lived; cleared with the
    /// slashes on any stage change.</summary>
    public readonly List<TelegraphedAoe> Aoe = new();

    void ScheduleAoe(Vec pos, float radius, int damage, bool magical, float push, string colour, float windup)
    {
        Aoe.Add(new TelegraphedAoe
        {
            Pos = pos, Radius = radius, Timer = windup, Total = windup,
            Damage = damage, Magical = magical, Push = push, Colour = colour,
        });
    }

    /// <summary>Ticks the charging bursts; when a wind-up runs out the ring lands —
    /// the impact flash goes up and everyone STILL inside is hit and flung. Damage
    /// is dealt only here, so the telegraph is a genuine chance to step clear.</summary>
    void UpdateAoe(float dt)
    {
        for (int i = Aoe.Count - 1; i >= 0; i--)
        {
            var a = Aoe[i];
            a.Timer -= dt;
            if (a.Timer > 0) continue;

            // The impact — the expanding ring the old code drew as it hit.
            Slashes.Add(new Slash(a.Pos, 0, a.Colour, 0.4f) { Nova = true, Radius = a.Radius });
            Shake = MathF.Max(Shake, 0.65f);
            Play("mine", a.Pos);

            foreach (var h in Party)
            {
                if (!h.Alive || h.IFrames > 0) continue;
                var to = h.Pos - a.Pos;
                if (to.Len() > a.Radius) continue;
                HurtHero(h, a.Damage, to.Norm(), a.Magical);
                h.Pos = MoveBlocked(h.Pos, to.Norm() * a.Push, 14f);
            }
            Aoe.RemoveAt(i);
        }
    }

    /// <summary>Steps every hostile bolt, and lands the ones that reach a hero.
    /// The mirror of the heroes' projectile loop, aimed the other way.</summary>
    void UpdateBolts(float dt)
    {
        foreach (var b in Bolts)
        {
            b.Pos += b.Vel * dt;
            b.Life -= dt;
            if (IsWallAt(b.Pos.X, b.Pos.Y)) { Play("hit_stone", b.Pos, b.Vel.Norm()); b.Life = 0; continue; }

            foreach (var h in Party)
            {
                if (!h.Alive || h.IFrames > 0) continue;
                if ((h.Pos - b.Pos).Len() > 16f) continue;

                HurtHero(h, b.Damage, b.Vel.Norm(), b.Magical, b.OnHit);
                b.Life = 0;
                break;
            }
        }
        Bolts.RemoveAll(b => b.Life <= 0 || Outside(b.Pos));
    }

    /// <summary>One blow landing on a hero, from a bolt or a smash. Blocks, armour,
    /// magic resistance, the flash, the number and a shove all decided here so a
    /// monster's spit and a monster's melee treat the hero identically.</summary>
    void HurtHero(Hero h, int damage, Vec push, bool magical, StatusEffectKind? onHit = null)
    {
        // Agility slips the blow entirely — the dodge the sheet has shown all
        // along and combat never granted. Rolled first: you cannot block or be
        // poisoned by a hit that never touched you.
        if (h.DodgeChance > 0 && _rng.NextDouble() < h.DodgeChance)
        {
            DodgeCount++;
            Floaters.Add(new FloatText(h.Pos + new Vec(0, -22), "DODGE", "#a8f0de"));
            h.IFrames = MathF.Max(h.IFrames, 0.1f);
            return;
        }

        if (h.BlockChance > 0 && _rng.NextDouble() < h.BlockChance)
        {
            BlockCount++;
            Play("block", h.Pos, push);
            Floaters.Add(new FloatText(h.Pos + new Vec(0, -20), "block", "#cfd6ff"));
            return;
        }

        var taken = h.Absorb(damage, magical);
        h.Hp -= taken;
        StatBridge.Record(h.Def.Key, HeroStats.Kind.DamageTaken, (long)MathF.Round(taken));
        h.Flash = 0.15f;
        h.IFrames = MathF.Max(h.IFrames, 0.12f);   // a brief mercy window, so bolts can't stunlock
        Play(magical ? "frost" : "hit_flesh", h.Pos, push);
        Floaters.Add(new FloatText(h.Pos + new Vec(0, -22), $"-{taken:0}", "#e2687a"));
        Shake = MathF.Max(Shake, 0.35f);

        // Whatever the blow carried — venom from a spider's spit. Four points a
        // second for four seconds, credited to no hero since the world dealt it.
        if (onHit is { } eff)
            ApplyEffect(h, eff, 4f, 4f, null);
    }

    /// <summary>A direction nudged off-true by up to <paramref name="amount"/>
    /// radians. For the shots that are meant to miss a little.</summary>
    Vec Jitter(Vec dir, float amount)
    {
        float a = MathF.Atan2(dir.Y, dir.X) + ((float)_rng.NextDouble() - 0.5f) * 2f * amount;
        return new Vec(MathF.Cos(a), MathF.Sin(a));
    }
}
