using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Attacks, abilities and the particle burst they share.
// =====================================================================
public partial class World
{
    void DoAttack(Hero h, Vec dir)
    {
        if (dir.Len() < 0.01f) dir = new Vec((float)Math.Cos(h.Facing), (float)Math.Sin(h.Facing));
        h.Cool = h.AttackCooldown;
        // Drives the attack sprite. Kept shorter than the fastest cooldown
        // (the Thief's 0.30s) so the pose always resolves back to the walk
        // cycle instead of latching on during sustained fire.
        h.AttackAnim = 0.22f;

        if (h.Def.Attack == Lore.AttackKind.Ranged)
        {
            Shots.Add(new Projectile(h.Pos + dir * 24f, dir * 640f, h.Damage, h.Def.ColorAccent, 1.1f));
        }
        else // melee cone
        {
            Slashes.Add(new Slash(h.Pos, h.Facing, h.Def.ColorAccent, 0.18f));
            foreach (var k in Husks)
            {
                var to = k.Pos - h.Pos;
                if (to.Len() < h.Def.Range + k.R)
                {
                    float ang = (float)Math.Atan2(to.Y, to.X);
                    if (Math.Abs(AngleDiff(ang, h.Facing)) < 1.0f)
                        Strike(h, k, h.Damage, to.Norm());
                }
            }
        }
    }

    /// <summary>Casts the ultimate if it can be paid for.
    ///
    /// The check lives here rather than at the call site so there is one place
    /// that knows an ability costs mana. Failing is quiet but visible: no
    /// cooldown is spent, and the hero says why — a button that does nothing at
    /// all reads as a broken game.</summary>
    void TryAbility(Hero h)
    {
        if (!h.SpendMana(h.AbilityCost))
        {
            Floaters.Add(new FloatText(h.Pos + new Vec(0, -30), "Not enough mana", "#8fb0ff"));
            // A short beat before it can be tried again, so holding the key does
            // not paint the screen with the same message sixty times a second.
            h.AbilityCool = 0.6f;
            return;
        }

        DoAbility(h);
    }

    void DoAbility(Hero h)
    {
        // Read from the catalogue rather than switched on the class name here.
        // These numbers are also what the Skills tab shows, and a copy of them in
        // the interface would have drifted the first time one was tuned.
        var ability = Ability.For(h.Def.HeroClass);

        h.AbilityCool = ability.Cooldown;
        // Held slightly longer than the nova it spawns (Slash life 0.4s) so the
        // channel pose outlasts its own shockwave instead of snapping back mid-blast.
        h.AbilityAnim = 0.45f;
        float radius = ability.Radius + h.AbilityRadiusBonus;
        int dmg = (int)MathF.Round(ability.Damage * h.AbilityDamageMultiplier);
        string col = ability.Colour;

        Slashes.Add(new Slash(h.Pos, 0, col, 0.4f) { Nova = true, Radius = radius });
        foreach (var k in Husks)
        {
            var to = k.Pos - h.Pos;
            if (to.Len() < radius)
            {
                // The ultimate does not roll criticals: it already is the big
                // moment, and a critical on top would be noise stacked on noise.
                Strike(h, k, dmg, to.Norm(), canCrit: false);
                k.Knock += to.Norm() * 160f;
            }
        }
        if (ability.PartyHeal > 0)
            foreach (var m in Party) if (m.Alive) m.Hp = Math.Min(m.MaxHp, m.Hp + ability.PartyHeal);
        // The school decides how it looks, so a new class ability picks its own
        // rather than being drawn by whatever the nova code happened to do.
        Play(h.Def.HeroClass switch { "Mage" => "fire", "Cleric" => "holy", _ => "frost" }, h.Pos);
    }

    /// <summary>One blow landing on one creature.
    ///
    /// Every source of damage to a creature goes through here — the melee cone,
    /// the crossbow bolt, the ultimate — so the critical roll, the material's
    /// effect, the damage number and the knock are decided once. Three call sites
    /// each rolling their own critical is three chances for them to disagree.</summary>
    void Strike(Hero h, Husk k, int baseDamage, Vec push, bool canCrit = true)
    {
        var m = CharacterStats.For(h.Def.Key);

        // Criticals were computed for the character sheet and read by nothing.
        // The sheet has promised a critical chance since the sheet existed.
        var crit = canCrit && m.CritChance > 0 && _rng.NextDouble() < m.CritChance;
        var damage = crit ? (int)MathF.Round(baseDamage * MathF.Max(1f, m.CritDamage)) : baseDamage;

        k.Hp -= damage;
        k.Flash = crit ? 0.28f : 0.15f;
        k.Knock += push * (crit ? 190f : 90f);

        Play(crit ? "crit" : HitEffect(k), k.Pos, push);
        Floaters.Add(new FloatText(
            k.Pos + new Vec(0, -18),
            crit ? $"{damage}!" : $"{damage}",
            crit ? "#ffd77a" : "#e8e6f2"));
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
