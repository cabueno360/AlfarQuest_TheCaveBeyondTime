using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Services.Character;

/// <summary>The derived stats, grouped the way the Stats tab shows them.
///
/// The grouping lives here rather than in the component so the tab renders
/// whatever it is handed: a new stat is a row in one of these lists and it
/// appears under the right heading with no markup change.
///
/// Some rows are <see cref="DerivedStat.Pending"/>. Those are stats the brief
/// asks for that nothing in the game feeds yet — there is no mining, no fishing
/// and no experience bonus. They are shown greyed with a dash rather than
/// omitted or, worse, given an invented number: a player reading "Mining Speed
/// 100%" would reasonably conclude mining exists.</summary>
public static class StatGroups
{
    public static IReadOnlyList<StatGroup> For(Models.Character c)
    {
        var a = c.Total;
        var gear = c.Gear.Total();
        var sk = c.SkillEffects();
        var def = c.Def;

        var armour = StatCalculator.PhysicalDefense(a) + gear.Armour;

        return
        [
            new("Offensive", "⚔",
            [
                new("Attack", $"{(StatCalculator.PhysicalDamage(def.Damage, a) + gear.Damage) * (1f + sk.Damage):0}",
                    "Strength, your weapon and skills."),
                new("Magic Attack", $"{StatCalculator.MagicDamage(def.Damage, a) * (1f + sk.AbilityDamage):0}",
                    "Intelligence and skills."),
                new("Critical Chance", $"{(StatCalculator.CritChance(a) + gear.CritChance) * 100:0.0}%",
                    "Dexterity and Luck."),
                new("Critical Damage", $"{StatCalculator.CritDamage(a) * 100:0}%",
                    "How much a critical adds."),
                new("Attack Speed", $"{StatCalculator.AttackSpeed(def.AttackCooldown, a) * (1f + sk.AttackSpeed):0.00}/s",
                    "Dexterity and skills shorten the cooldown."),
                new("Accuracy", $"{StatCalculator.Accuracy(a):0}",
                    "Dexterity steadies the hand."),
            ]),

            new("Defensive", "🛡",
            [
                new("Armor", $"{armour:0}", "Defense and worn armour."),
                new("Damage Reduction", $"{StatCalculator.DamageReduction(armour) * 100:0.0}%",
                    "What that armour actually removes from every blow."),
                new("Magic Resistance", $"{StatCalculator.MagicResistance(a) * 100:0.0}%",
                    "Wisdom turns aside magic. Crystal swarms and husks strike that way."),
                new("Block Chance", $"{Math.Min(0.9f, StatCalculator.Block(a) + sk.BlockChance) * 100:0.0}%",
                    "Defense and Steady Guard."),
                new("Dodge Chance", $"{StatCalculator.Dodge(a) * 100:0.0}%", "Agility slips the blow."),
                new("Health Regen", $"{StatCalculator.HealthRegen(a) + sk.HealthRegen:0.0}/s", "Vitality and Vigour."),
                new("Mana Regen", $"{StatCalculator.ManaRegen(a):0.0}/s", "Wisdom refills the well."),
            ]),

            new("General", "◈",
            [
                new("Movement Speed", $"{StatCalculator.MoveSpeed(def.Speed, a):0}", "Agility carries you further."),
                new("Stamina Regen", $"{StatCalculator.StaminaRegen(a):0.0}/s", "Agility gets your wind back."),
                new("Ability Cost", $"{ResourceCosts.Ability(def.HeroClass)} mana", "What your ultimate spends."),
                new("Luck", $"{a.Luck}", "Nudges criticals and what the dark gives up."),
                new("Loot Bonus", $"+{(StatCalculator.LootChance(a) + sk.LootChance) * 100:0}%",
                    "Luck and Delver's Luck, applied to every drop roll."),

                // Named in the brief, fed by nothing.
                new("Experience Bonus", "—", "No source of bonus experience exists yet.", Pending: true),
                new("Mining Speed", "—", "There is no mining system yet — ore seams are opened, not timed.", Pending: true),
                new("Fishing Speed", "—", "There is no fishing yet.", Pending: true),
                new("Crafting Bonus", "—", "Crafting has no speed or quality modifier yet.", Pending: true),
            ]),
        ];
    }
}

public sealed record StatGroup(string Name, string Icon, IReadOnlyList<DerivedStat> Stats);
