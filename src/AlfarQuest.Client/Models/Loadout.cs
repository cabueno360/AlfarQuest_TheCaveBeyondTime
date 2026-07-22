namespace AlfarQuest.Client.Models;

/// <summary>What a character is wearing. A slot holds at most one item; equipping
/// into a full slot returns what came off, so the caller decides where it goes.</summary>
public sealed class Loadout
{
    private readonly Dictionary<Slot, Item> _worn = [];

    public Item? this[Slot slot] => _worn.GetValueOrDefault(slot);
    public IEnumerable<Item> Worn => _worn.Values;

    /// <summary>The weapon in hand, if any. What decides how the wearer fights —
    /// its range, speed, crit and damage type — so the stat calculation reaches
    /// for it directly rather than hunting the main-hand slot each time.</summary>
    public Item? Weapon => this[Slot.MainHand] is { IsWeapon: true } w ? w : null;

    public Item? Equip(Item item)
    {
        var previous = this[item.Slot];
        _worn[item.Slot] = item;
        return previous;
    }

    public Item? Unequip(Slot slot)
    {
        if (!_worn.Remove(slot, out var item)) return null;
        return item;
    }

    /// <summary>Takes everything off, for the same reason the pack can be
    /// emptied: a restore replaces what is worn rather than layering onto it.</summary>
    public void Clear() => _worn.Clear();

    /// <summary>Everything the worn set adds, summed once so callers do not each
    /// re-walk the loadout.</summary>
    public EquipmentBonus Total()
    {
        var attrs = new Attributes();
        float dmg = 0, arm = 0, crit = 0;
        foreach (var i in _worn.Values)
        {
            attrs += i.Attributes;
            dmg += i.Damage; arm += i.Armour; crit += i.CritChance;
        }
        return new EquipmentBonus(attrs, dmg, arm, crit);
    }
}

public readonly record struct EquipmentBonus(Attributes Attributes, float Damage, float Armour, float CritChance)
{
    public static readonly EquipmentBonus None = new(new Attributes(), 0, 0, 0);
}
