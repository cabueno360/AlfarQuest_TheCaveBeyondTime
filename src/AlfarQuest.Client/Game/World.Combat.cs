using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Attacks, abilities and the particle burst they share.
// =====================================================================
public partial class World
{
    /// <summary>Cumulative combat tallies, never reset — every critical, miss,
    /// block, dodge and stun this session. A running count catches an event that
    /// lives for a single frame, which a live sample cannot.</summary>
    public int CritCount, MissCount, BlockCount, DodgeCount, StunCount;

    /// <summary>The last handful of damage numbers a hero dealt, so a test can see
    /// that the weapon rolls a range rather than a fixed value. Capped — this is a
    /// window, not a log.</summary>
    public readonly List<int> RecentHits = new();

    void DoAttack(Hero h, Vec dir)
    {
        if (dir.Len() < 0.01f) dir = new Vec((float)Math.Cos(h.Facing), (float)Math.Sin(h.Facing));
        h.Cool = h.AttackCooldown;
        // Drives the attack sprite. Kept shorter than the fastest cooldown
        // (the Thief's 0.30s) so the pose always resolves back to the walk
        // cycle instead of latching on during sustained fire.
        h.AttackAnim = 0.22f;

        // One roll for the swing — a fresh number between the weapon's min and max,
        // so no two attacks land the same. The cone shares it; a single swing does
        // one weapon's worth of damage to everything it catches.
        var dmg = h.RollDamage(_rng.NextDouble);

        if (h.Def.Attack == Lore.AttackKind.Ranged)
        {
            Shots.Add(new Projectile(h.Pos + dir * 24f, dir * 640f, dmg, h.Def.ColorAccent, 1.1f, h.Def.Key));
            // The loose of the shot. The bolt landing raises its own sound where it
            // lands, which may be a wall or a body a screen away.
            PlaySound(h.DamageType == DamageType.Fire ? "magic_fire" : "bow", h.Pos, 0.7f);
        }
        else // melee cone, reaching as far as the weapon does — a spear outranges a dagger
        {
            Slashes.Add(new Slash(h.Pos, h.Facing, h.Def.ColorAccent, 0.18f));
            // The swing itself, whether or not it connects. A whiff that is silent
            // reads as a dropped input.
            PlaySound("swing", h.Pos, 0.7f);
            foreach (var k in Husks)
            {
                var to = k.Pos - h.Pos;
                if (to.Len() < h.Range + k.R)
                {
                    float ang = (float)Math.Atan2(to.Y, to.X);
                    if (Math.Abs(AngleDiff(ang, h.Facing)) < 1.0f)
                        Strike(h, k, dmg, to.Norm(), h.DamageType);
                }
            }
        }
    }


    /// <summary>One blow landing on one creature.
    ///
    /// Every source of damage to a creature goes through here — the melee cone,
    /// the crossbow bolt, the ultimate — so the critical roll, the material's
    /// effect, the damage number and the knock are decided once. Three call sites
    /// each rolling their own critical is three chances for them to disagree.</summary>
    void Strike(Hero h, Husk k, int baseDamage, Vec push, DamageType type,
                bool canCrit = true, bool canMiss = true, bool canStun = true)
    {
        var m = CharacterStats.For(h.Def.Key);

        // Accuracy against the creature's evasion — the stat the sheet showed and
        // combat ignored until now. Kept generous so stage one is not a game of
        // whiffs, but present, so a fast thing is genuinely hard to land on and
        // Dexterity genuinely steadies the hand.
        if (canMiss)
        {
            var hit = Math.Clamp(0.90f + (m.Accuracy - 60f) * 0.005f - k.Def.Evasion, 0.55f, 0.99f);
            if (_rng.NextDouble() > hit)
            {
                MissCount++;
                Floaters.Add(new FloatText(k.Pos + new Vec(0, -18), "miss", "#9a95b6"));
                return;
            }
        }

        // Criticals were computed for the character sheet and read by nothing.
        var crit = canCrit && m.CritChance > 0 && _rng.NextDouble() < m.CritChance;
        var raw = crit ? baseDamage * MathF.Max(1f, m.CritDamage) : baseDamage;

        // What this creature turns aside, or takes extra of — the resistance table.
        // Floored at one so nothing is ever wholly immune to a solid hit.
        var damage = Math.Max(1, (int)MathF.Round(raw * (1f - k.Def.Resistance(type))));

        if (crit) CritCount++;
        RecentHits.Add(damage);
        if (RecentHits.Count > 24) RecentHits.RemoveAt(0);

        // Whoever lands the last blow owns the kill. XP is individual now — the
        // hero who finishes a creature levels for it, and nobody else does.
        k.LastHitBy = h.Def.Key;
        StatBridge.Record(h.Def.Key, HeroStats.Kind.DamageDealt, damage);
        Alert(k, k.Pos);           // its cry rouses whatever is nearby
        k.Hp -= damage;
        k.Flash = crit ? 0.28f : 0.15f;
        k.Knock += push * (crit ? 190f : 90f);

        Play(crit ? "crit" : HitEffect(k), k.Pos, push);
        Floaters.Add(new FloatText(
            k.Pos + new Vec(0, -18),
            crit ? $"{damage}!" : $"{damage}",
            crit ? "#ffd77a" : "#e8e6f2"));

        // The hammer's whole argument for being so slow: a chance to put the thing
        // on the floor for a beat. The first real status effect.
        if (canStun && m.WeaponStunChance > 0 && _rng.NextDouble() < m.WeaponStunChance)
        {
            StunCount++;
            ApplyEffect(k, StatusEffectKind.Stun, 1.1f, 0f, h.Def.Key);
        }
    }

    /// <summary>The effect a hit on this creature throws. Derived from its
    /// material, so a new species picks up the right sparks by declaring what it
    /// is made of rather than by being listed here.</summary>
    static string HitEffect(Husk k) => $"hit_{k.Def.Material}";

    void Burst(Vec at, string color, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float a = (float)(_rng.NextDouble() * Math.PI * 2);
            float sp = 40 + (float)_rng.NextDouble() * 160;
            Fx.Add(new Particle(at, new Vec((float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp), color, 0.4f + (float)_rng.NextDouble() * 0.3f));
        }
    }
}
