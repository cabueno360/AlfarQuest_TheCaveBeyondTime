using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Active skills.
//
//  One entry point — CastSkill(hero, slot) — and one shape switch. The
//  catalogue says what a skill is (a bolt, a burst, a mend), how much it
//  costs and when it unlocks; this turns that into projectiles, a nova or
//  healing. Nothing here knows the name of a skill, so a new one is a row
//  in ActiveSkill.All and a line on the hotbar, never a branch in combat.
// =====================================================================
public partial class World
{
    /// <summary>Health a draught restores, as a fraction of the hero's maximum,
    /// and how long before the next one.</summary>
    const float PotionHeal = 0.4f;
    const float PotionCooldown = 12f;

    /// <summary>Where the caster is aiming — the mouse in the world, falling back
    /// to the way they face. Held between frames so a skill fired without the
    /// mouse moving still goes somewhere sensible.</summary>
    Vec _castAim;

    /// <summary>Casts the active hero's skill in a hotbar slot, if it is unlocked,
    /// off cooldown and can be paid for. Failing is quiet but legible — a floating
    /// word says why, and no cooldown is spent on a cast that never happened.</summary>
    void CastSkill(Hero h, int slot)
    {
        if (ActiveSkill.At(h.Def.HeroClass, slot) is not { } skill) return;

        // Not learned yet — the slot is on the bar as a promise, not a button.
        if (HeroLevelOf(h) < skill.UnlockLevel)
        {
            Floaters.Add(new FloatText(h.Pos + new Vec(0, -30), "not learned yet", "#9a95b6"));
            return;
        }
        if (h.SkillCool[slot] > 0) return;
        if (!h.SpendMana(skill.ManaCost))
        {
            Floaters.Add(new FloatText(h.Pos + new Vec(0, -30), "Not enough mana", "#8fb0ff"));
            h.SkillCool[slot] = 0.5f;      // a beat, so holding the key does not spam the message
            return;
        }

        h.SkillCool[slot] = skill.Cooldown;
        h.AbilityAnim = 0.4f;              // the channel pose
        DoSkill(h, skill);
    }

    void DoSkill(Hero h, ActiveSkill skill)
    {
        var dir = AimDir(h);
        var dmg = SkillDamage(h, skill);
        var heal = skill.Heal;

        // The ultimate consults the fates: a d20 plus the class's prime
        // attribute, thrown big across the screen. The number is decided here
        // and applied NOW — the 3D die lands on the same value a moment later,
        // so combat never waits on physics. High and the skill surges; a
        // gutter roll and it falters; most casts are simply themselves.
        if (skill.Slot == 4)
        {
            var m = CharacterStats.For(h.Def.Key);
            var (_, total) = RollFate("surge", m.FateMod, FateColour(h.Def.HeroClass));
            var mult = total >= 18 ? 1.5f : total <= 4 ? 0.75f : 1f;
            if (mult != 1f)
            {
                dmg = (int)MathF.Round(dmg * mult);
                heal = (int)MathF.Round(heal * mult);
                Floaters.Add(new FloatText(h.Pos + new Vec(0, -44),
                    mult > 1f ? "Fate surges — {0}!" : "Fate falters — {0}",
                    mult > 1f ? "#f0d99a" : "#9a95b6", total.ToString()));
            }
        }

        switch (skill.Shape)
        {
            case SkillShape.Projectile:
                CastProjectiles(h, skill, dir, dmg);
                break;
            case SkillShape.Nova:
                CastNova(h, skill, dmg, heal);
                break;
            case SkillShape.Heal:
                CastHeal(h, heal);
                break;
        }

        Play(EffectForType(skill.DamageType), h.Pos, dir, 1f);
        PlaySound(SoundForType(skill.DamageType), h.Pos, 0.8f);
    }

    /// <summary>Skill damage: the base, plus the caster's spellcraft or strength,
    /// scaled by ability power. So gear and attributes lift a skill the same way
    /// they lift a swing — a skill is not a flat number bolted onto a growing
    /// hero.</summary>
    int SkillDamage(Hero h, ActiveSkill skill)
    {
        if (skill.Damage == 0) return 0;
        var m = CharacterStats.For(h.Def.Key);
        // Magic skills ride on spellcraft, physical ones on the same power that
        // lifts a swing — so gear and attributes reach a skill, not just a base
        // number. Ability power then scales the lot.
        float attrBonus = DamageTypeInfo.Of(skill.DamageType).IsMagic ? m.SpellPower : m.BonusDamage * 0.6f;
        return Math.Max(1, (int)MathF.Round((skill.Damage + attrBonus) * (1f + m.AbilityDamageMultiplier)));
    }

    void CastProjectiles(Hero h, ActiveSkill skill, Vec dir, int dmg)
    {
        // A single aimed bolt, or a fan of them. The spread is centred on the aim,
        // so a three-shot still points where the player pointed.
        for (int i = 0; i < Math.Max(1, skill.Count); i++)
        {
            var d = skill.Count > 1
                ? Rotate(dir, (i - (skill.Count - 1) / 2f) * skill.Spread)
                : dir;
            Shots.Add(new Projectile(h.Pos + d * 24f, d * skill.Speed, dmg, skill.Colour, 1.2f, h.Def.Key)
            {
                SkillType = skill.DamageType,
                OnHit = skill.OnHit,
                CanCrit = skill.CanCrit,
            });
        }
    }

    void CastNova(Hero h, ActiveSkill skill, int dmg, int heal)
    {
        float radius = skill.Radius + h.AbilityRadiusBonus;
        Slashes.Add(new Slash(h.Pos, 0, skill.Colour, 0.4f) { Nova = true, Radius = radius });
        Shake = MathF.Max(Shake, 0.4f);

        foreach (var k in Husks)
        {
            var to = k.Pos - h.Pos;
            if (to.Len() > radius) continue;
            if (dmg > 0) Strike(h, k, dmg, to.Norm(), skill.DamageType,
                                canCrit: skill.CanCrit, canMiss: false, canStun: false);
            if (skill.OnHit is { } eff) ApplyEffect(k, eff, EffectSeconds(eff), EffectMagnitude(eff), h.Def.Key);
            k.Knock += to.Norm() * 150f;
        }

        if (heal > 0) HealParty(heal);
    }

    void CastHeal(Hero h, int heal)
    {
        HealParty(heal);
        Play("holy", h.Pos, null, 1f);
    }

    void HealParty(int amount)
    {
        foreach (var m in Party)
            if (m.Alive)
            {
                m.Hp = Math.Min(m.MaxHp, m.Hp + amount);
                Floaters.Add(new FloatText(m.Pos + new Vec(0, -24), $"+{amount}", "#7fd694"));
            }
    }

    /// <summary>The healing draught: a big self-heal on a long cooldown, no mana.
    /// The panic button every class carries.</summary>
    void UsePotion(Hero h)
    {
        if (h.PotionCool > 0) return;
        h.PotionCool = PotionCooldown;
        var heal = (int)MathF.Round(h.MaxHp * PotionHeal);
        h.Hp = Math.Min(h.MaxHp, h.Hp + heal);
        Floaters.Add(new FloatText(h.Pos + new Vec(0, -26), $"+{heal}", "#7fd694"));
        Play("holy", h.Pos, null, 0.8f);
        PlaySound("magic_holy", h.Pos, 0.7f);
    }

    /// <summary>The active hero's hotbar for the render payload — each skill's
    /// cooldown fraction and whether it can be cast right now, rebuilt every frame
    /// so the bar follows whoever is steered and fills in as they learn skills.</summary>
    List<RSkill> ActiveHotbar()
    {
        var bar = new List<RSkill>();
        if (Party.Count == 0 || Active >= Party.Count) return bar;
        var h = Party[Active];
        int level = HeroLevelOf(h);

        foreach (var skill in ActiveSkill.For(h.Def.HeroClass))
        {
            bool unlocked = level >= skill.UnlockLevel;
            float cd = h.SkillCool[skill.Slot];
            bool affordable = h.Mana >= skill.ManaCost;
            bar.Add(new RSkill
            {
                slot = skill.Slot,
                name = skill.Name,
                icon = skill.Icon,
                shortcut = skill.Slot.ToString(),
                desc = skill.Description,
                manaCost = skill.ManaCost,
                unlockLevel = skill.UnlockLevel,
                cdFrac = skill.Cooldown > 0 ? Math.Clamp(cd / skill.Cooldown, 0f, 1f) : 0f,
                unlocked = unlocked,
                affordable = affordable,
                ready = unlocked && cd <= 0 && affordable,
            });
        }
        return bar;
    }

    // ---- helpers --------------------------------------------------------

    Vec AimDir(Hero h)
    {
        var d = _castAim - h.Pos;
        return d.Len() > 4f ? d.Norm() : new Vec(MathF.Cos(h.Facing), MathF.Sin(h.Facing));
    }

    static Vec Rotate(Vec v, float radians)
    {
        float c = MathF.Cos(radians), s = MathF.Sin(radians);
        return new Vec(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    int HeroLevelOf(Hero h) => CharacterStats.ProgressOf(h.Def.Key).Level;

    /// <summary>Whether this hero's ultimate — hotbar slot 4 — could be cast now.
    /// Used only for the party-panel badge; the hotbar reads the slots itself.</summary>
    bool UltimateReady(Hero h) =>
        ActiveSkill.At(h.Def.HeroClass, 4) is { } u
        && HeroLevelOf(h) >= u.UnlockLevel && h.SkillCool[4] <= 0 && h.Mana >= u.ManaCost;

    static string EffectForType(DamageType t) => t switch
    {
        DamageType.Fire => "fire",
        DamageType.Ice => "frost",
        DamageType.Holy => "holy",
        DamageType.Lightning => "frost",
        _ => "hit_crystal",
    };

    static string SoundForType(DamageType t) => DamageTypeInfo.Of(t).IsMagic
        ? t switch { DamageType.Fire => "magic_fire", DamageType.Holy => "magic_holy", _ => "magic_frost" }
        : "bow";

    static float EffectSeconds(StatusEffectKind kind) => kind switch
    {
        StatusEffectKind.Stun => 1.1f,
        StatusEffectKind.Slow => 2.5f,
        StatusEffectKind.Poison => 4f,
        _ => 3f,
    };

    static float EffectMagnitude(StatusEffectKind kind) => kind switch
    {
        StatusEffectKind.Poison => 5f,
        StatusEffectKind.Slow => 0.5f,
        _ => 0f,
    };
}
