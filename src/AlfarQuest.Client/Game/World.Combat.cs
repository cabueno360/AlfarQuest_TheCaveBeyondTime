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
                    {
                        k.Hp -= h.Damage; k.Flash = 0.15f;
                        k.Knock += to.Norm() * 90f;
                        Burst(k.Pos, h.Def.ColorAccent, 5);
                    }
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
        h.AbilityCool = 6f;
        // Held slightly longer than the nova it spawns (Slash life 0.4s) so the
        // channel pose outlasts its own shockwave instead of snapping back mid-blast.
        h.AbilityAnim = 0.45f;
        // Mage = hellfire nova (the caged demon); Cleric = holy nova (+party heal); Thief = dash-blades burst.
        float radius = (h.Def.HeroClass == "Mage" ? 240 : 170) + h.AbilityRadiusBonus;
        int dmg = (int)MathF.Round((h.Def.HeroClass == "Mage" ? 60 : 34) * h.AbilityDamageMultiplier);
        string col = h.Def.HeroClass == "Cleric" ? "#f0d99a" : h.Def.ColorAccent;

        Slashes.Add(new Slash(h.Pos, 0, col, 0.4f) { Nova = true, Radius = radius });
        foreach (var k in Husks)
        {
            var to = k.Pos - h.Pos;
            if (to.Len() < radius)
            {
                k.Hp -= dmg; k.Flash = 0.2f;
                k.Knock += to.Norm() * 220f;
            }
        }
        if (h.Def.HeroClass == "Cleric")
            foreach (var m in Party) if (m.Alive) m.Hp = Math.Min(m.MaxHp, m.Hp + 25);
        Burst(h.Pos, col, 26);
        Shake = 0.7f;
    }

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
