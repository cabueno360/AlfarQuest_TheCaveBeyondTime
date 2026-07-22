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
    public int MonsterCastsFired, MonsterBoltsFired;

    /// <summary>Performs a creature's one trick, if it has one and it is off
    /// cooldown, and reports whether it did — the caller holds off the ordinary
    /// bite on a frame the creature cast instead.</summary>
    bool MonsterAct(Husk k, Hero target)
    {
        if (k.Def.Ability == MonsterAbility.None || k.AbilityCool > 0) return false;

        float range = k.Def.AbilityRangeTiles * TILE;
        var to = target.Pos - k.Pos;
        float dist = to.Len();
        if (dist > range) return false;

        var dir = to.Norm();
        k.AbilityCool = k.Def.AbilityCooldown;
        MonsterCastsFired++;

        switch (k.Def.Ability)
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
        }
        return true;
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
        float radius = TILE * 2.2f;
        Slashes.Add(new Slash(k.Pos, 0, "#c9a06a", 0.4f) { Nova = true, Radius = radius });
        Shake = MathF.Max(Shake, 0.5f);
        Play("mine", k.Pos);

        foreach (var h in Party)
        {
            if (!h.Alive || h.IFrames > 0) continue;
            var to = h.Pos - k.Pos;
            if (to.Len() > radius) continue;

            HurtHero(h, k.Def.Damage, to.Norm(), k.Def.Magical);
            h.Pos = MoveBlocked(h.Pos, to.Norm() * 22f, 14f);   // shoved out of the ring
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
