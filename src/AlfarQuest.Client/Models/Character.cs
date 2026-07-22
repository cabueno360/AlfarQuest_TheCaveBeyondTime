using AlfarQuest.Client.Game;
using AlfarQuest.Client.Services.Character;

namespace AlfarQuest.Client.Models;

/// <summary>A delver's sheet: everything the character window shows that is not
/// a live simulation value. The engine owns position and current HP; this owns
/// progression, attributes and equipment.</summary>
public sealed class Character(Lore.HeroDef def)
{
    public Lore.HeroDef Def { get; } = def;

    public string Key => Def.Key;
    public string Name => Def.Name;
    public string Title => Def.Title;
    public string Class => Def.HeroClass;
    public string Description => Def.Description;
    public string Race { get; init; } = "Àlfar";

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    public int AttributePoints { get; private set; } = 5;
    public int SkillPoints { get; private set; } = 1;

    /// <summary>Points the player has spent. Kept apart from the class baseline so
    /// a respec is just clearing this, and so the window can show what was earned
    /// rather than what was granted.</summary>
    public Attributes Spent { get; private set; } = new();

    public Loadout Gear { get; } = new();
    public SkillBook Skills { get; } = new();

    /// <summary>This hero's own pack, gold and record. Individual now — what a hero
    /// picks up, earns and does belongs to them, and switching leaders shows the
    /// new one's, not a shared pool. Materials stay shared on the party (they feed
    /// crafting, which is a party workbench), so they are not here.</summary>
    public Inventory Bag { get; } = new();
    public Wallet Purse { get; } = new();
    public HeroStats Stats { get; } = new();

    public Attributes Base => Baselines.For(Def.HeroClass);

    /// <summary>Everything the sheet is worth: class baseline, points spent, and
    /// what is worn. Derived stats read this, so equipping a ring moves the same
    /// numbers that raising an attribute does.</summary>
    public Attributes Total => Base + Spent + Gear.Total().Attributes;

    // Live values mirrored from the simulation each frame; the sheet never owns
    // them, it only displays them.
    public float Hp { get; set; }
    public float MaxHp { get; set; }
    public float Mana { get; set; }
    public float MaxMana { get; set; }
    public float Stamina { get; set; }
    public float MaxStamina { get; set; }

    public bool CanRaise(AttributeKind _) => AttributePoints > 0;

    public int RankOf(Skill s) => Skills.RankOf(s.Id);

    /// <summary>Everything the learned skills contribute, summed across each rank
    /// bought — rank 3 of a skill is the sum of ranks 0, 1 and 2's steps.</summary>
    public SkillEffect SkillEffects()
    {
        var total = new SkillEffect();
        foreach (var s in SkillCatalog.All)
        {
            if (s.PerRank is null) continue;
            for (var r = 0; r < RankOf(s); r++) total += s.PerRank(r);
        }
        return total;
    }

    public bool CanLearn(Skill s) =>
        s.AvailableTo(Class) && RankOf(s) < s.MaxLevel && SkillPoints >= s.CostFor(RankOf(s));

    public bool Learn(Skill s)
    {
        if (!CanLearn(s)) return false;
        var rank = RankOf(s);
        SkillPoints -= s.CostFor(rank);
        Skills.Set(s.Id, rank + 1);
        return true;
    }

    public bool Raise(AttributeKind kind)
    {
        if (AttributePoints <= 0) return false;
        Spent = Spent.With(kind, 1);
        AttributePoints--;
        return true;
    }

    /// <summary>Puts a saved sheet back exactly as it was left.
    ///
    /// A single method rather than public setters on each field: progression is
    /// only ever written by earning it or by loading it, and leaving Level
    /// settable would let any future code hand out a level by assignment.
    /// Nothing here re-derives anything — the sheet computes from these.</summary>
    public void RestoreProgress(
        int level, int xp, int attributePoints, int skillPoints,
        Attributes spent, IEnumerable<(string SkillId, int Rank)> skills)
    {
        Level = Math.Clamp(level, 1, Progression.MaxLevel);
        Xp = Math.Max(0, xp);
        AttributePoints = Math.Max(0, attributePoints);
        SkillPoints = Math.Max(0, skillPoints);
        Spent = spent;

        Skills.Clear();
        foreach (var (id, rank) in skills) Skills.Set(id, rank);
    }

    /// <summary>Puts this hero's saved pack, gold and record back. Replaces rather
    /// than adds — a fresh sheet was handed a starting purse and kit before a save
    /// loads, and adding would leave a returning hero with two of everything.</summary>
    public void RestoreInventory(IEnumerable<Item> items)
    {
        Bag.Clear();
        foreach (var item in items) Bag.Add(item);
    }

    public void RestoreGold(IEnumerable<(string Key, long Amount)> amounts)
    {
        Purse.Clear();
        foreach (var (key, amount) in amounts) Purse.Set(key, amount);
    }

    public void RestoreStats(HeroStats stats) => Stats.CopyFrom(stats);

    public void GainXp(int amount)
    {
        if (amount <= 0) return;
        Xp += amount;
        while (Xp >= Progression.XpForNext(Level))
        {
            Xp -= Progression.XpForNext(Level);
            Level++;
            AttributePoints += Progression.AttributePointsPerLevel;
            SkillPoints += Progression.SkillPointsPerLevel;
        }
    }
}

/// <summary>Per-class starting attributes. A table rather than a constructor
/// switch, so a new class is one row.</summary>
public static class Baselines
{
    private static readonly Dictionary<string, Attributes> ByClass = new()
    {
        ["Mage"]   = new() { Strength = 4, Dexterity = 6,  Agility = 6,  Vitality = 5,  Intelligence = 12, Wisdom = 9,  Defense = 3, Luck = 6 },
        ["Cleric"] = new() { Strength = 9, Dexterity = 5,  Agility = 4,  Vitality = 11, Intelligence = 8,  Wisdom = 10, Defense = 9, Luck = 5 },
        ["Thief"]  = new() { Strength = 6, Dexterity = 11, Agility = 10, Vitality = 6,  Intelligence = 5,  Wisdom = 4,  Defense = 5, Luck = 9 },
    };

    public static Attributes For(string heroClass) =>
        ByClass.TryGetValue(heroClass, out var a) ? a : new Attributes();
}
