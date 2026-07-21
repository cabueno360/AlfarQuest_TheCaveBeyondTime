using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Services.Character;

/// <summary>Every skill in the game, as data. Adding one is a single entry here
/// — no panel, category or card needs touching, because the UI renders whatever
/// this returns grouped by category.</summary>
public static class SkillCatalog
{
    public static readonly IReadOnlyList<Skill> All =
    [
        new() { Id = "cleave", PerRank = r => new SkillEffect(Damage: 0.08f + r * 0.04f),
                   Name = "Cleave",        Category = SkillCategory.Combat, Icon = "⚔",
                Description = "Your swing carries through to a second body.",
                NextBonus = r => $"+{8 + r * 4}% damage to the arc" },
        new() { Id = "guard", PerRank = r => new SkillEffect(BlockChance: 0.03f + r * 0.01f),
                    Name = "Steady Guard",  Category = SkillCategory.Combat, Icon = "🛡",
                Description = "Bracing turns a blow aside.",
                NextBonus = r => $"+{3 + r}% block chance" },
        new() { Id = "footwork", PerRank = r => new SkillEffect(DashCooldown: -(0.05f + r * 0.03f)),
                 Name = "Footwork",      Category = SkillCategory.Combat, Icon = "🥾",
                Description = "The dash comes back sooner.",
                NextBonus = r => $"-{5 + r * 3}% dash cooldown" },

        new() { Id = "firebolt", PerRank = r => new SkillEffect(AbilityDamage: 0.10f + r * 0.05f),
                 Name = "Caged Fire",    Category = SkillCategory.Magic,  Icon = "🔥",
                Description = "The demon lends a little of its heat.",
                Classes = ["Mage"],
                NextBonus = r => $"+{10 + r * 5}% magic damage" },
        new() { Id = "nova", PerRank = r => new SkillEffect(AbilityRadius: 12f + r * 4f),
                     Name = "Holy Nova",     Category = SkillCategory.Magic,  Icon = "✨",
                Description = "The light reaches further out.",
                Classes = ["Cleric"],
                NextBonus = r => $"+{12 + r * 4} nova radius" },
        new() { Id = "shard", PerRank = r => new SkillEffect(AttackSpeed: 0.06f + r * 0.03f),
                    Name = "Shard Volley",  Category = SkillCategory.Magic,  Icon = "❄",
                Description = "The crystal in your arm answers faster.",
                Classes = ["Thief"],
                NextBonus = r => $"+{6 + r * 3}% attack speed" },

        new() { Id = "vigour", PerRank = r => new SkillEffect(HealthRegen: 0.2f + r * 0.15f),
                   Name = "Vigour",        Category = SkillCategory.Passive, Icon = "❤",
                Description = "Wounds knit while you walk.",
                NextBonus = r => $"+{0.2f + r * 0.15f:0.0} health per second" },
        new() { Id = "fortune",  Name = "Delver's Luck", Category = SkillCategory.Passive, Icon = "🍀",
                Description = "The dark gives up a little more.",
                NextBonus = r => $"+{4 + r * 2}% rare finds" },

        new() { Id = "prospect", PerRank = r => new SkillEffect(MaterialYield: 0.10f + r * 0.05f),
                Name = "Prospecting",   Category = SkillCategory.Crafting, Icon = "⛏",
                Description = "You read a seam before you break it.",
                NextBonus = r => $"+{10 + r * 5}% ore yield" },
        new() { Id = "smith",    Name = "Field Smithing", Category = SkillCategory.Crafting, Icon = "🔨",
                Description = "Repairs that hold until the next chamber.",
                NextBonus = r => $"+{8 + r * 4}% repair quality" },
    ];

    public static IEnumerable<IGrouping<SkillCategory, Skill>> ForClass(string heroClass) =>
        All.Where(s => s.AvailableTo(heroClass)).GroupBy(s => s.Category);
}
