using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Services.Character;

/// <summary>Weapons the world hands out, one of each type so the differences the
/// combat system now reads are things a player can actually feel: a slow axe that
/// hits for a lot, a fast dagger that crits, a spear that reaches. Kept apart from
/// the starting kit so "what can I find" and "what do I start with" are two lists,
/// not one.
///
/// The damage ranges are the brief's own numbers where it gave them. The feel —
/// speed, crit, reach, stun — comes from the <see cref="WeaponType"/>, so these
/// rows only say how much and how rare.</summary>
public static class WeaponRack
{
    public static readonly IReadOnlyList<Item> All =
    [
        new() { Id = "sword_iron", Name = "Iron Sword", Slot = Slot.MainHand, Rarity = Rarity.Uncommon,
                Icon = "⚔", Weapon = WeaponType.Sword, DamageMin = 8, DamageMax = 14,
                Flavour = "Plain, honest, and sharp where it counts." },
        new() { Id = "sword_steel", Name = "Steel Sword", Slot = Slot.MainHand, Rarity = Rarity.Rare,
                Icon = "⚔", Weapon = WeaponType.Sword, DamageMin = 14, DamageMax = 22,
                Attributes = new() { Strength = 2 } },
        new() { Id = "axe_war", Name = "War Axe", Slot = Slot.MainHand, Rarity = Rarity.Uncommon,
                Icon = "🪓", Weapon = WeaponType.Axe, DamageMin = 12, DamageMax = 24,
                Flavour = "Slow to lift, worse to be under." },
        new() { Id = "spear_long", Name = "Long Spear", Slot = Slot.MainHand, Rarity = Rarity.Uncommon,
                Icon = "🔱", Weapon = WeaponType.Spear, DamageMin = 9, DamageMax = 16,
                Attributes = new() { Dexterity = 1 } },
        new() { Id = "hammer_iron", Name = "Iron Warhammer", Slot = Slot.MainHand, Rarity = Rarity.Rare,
                Icon = "🔨", Weapon = WeaponType.Hammer, DamageMin = 16, DamageMax = 28,
                Attributes = new() { Strength = 2 }, Flavour = "The argument that ends arguments." },
        new() { Id = "bow_hunting", Name = "Hunting Bow", Slot = Slot.MainHand, Rarity = Rarity.Common,
                Icon = "🏹", Weapon = WeaponType.Bow, DamageMin = 5, DamageMax = 10 },
        new() { Id = "staff_oak", Name = "Oak Staff", Slot = Slot.MainHand, Rarity = Rarity.Common,
                Icon = "🪄", Weapon = WeaponType.Staff, DamageMin = 6, DamageMax = 12,
                Attributes = new() { Intelligence = 2 } },
        new() { Id = "daggers_twin", Name = "Twin Daggers", Slot = Slot.MainHand, Rarity = Rarity.Uncommon,
                Icon = "🗡", Weapon = WeaponType.Dagger, DamageMin = 5, DamageMax = 9,
                Attributes = new() { Agility = 1 }, Flavour = "Two quick words in the dark." },
        new() { Id = "sword_dawn", Name = "Dawnbreaker", Slot = Slot.MainHand, Rarity = Rarity.Legendary,
                Icon = "🗡", Weapon = WeaponType.Sword, DamageMin = 25, DamageMax = 40,
                DamageKind = DamageType.Holy, Attributes = new() { Strength = 3, Luck = 2 },
                Flavour = "It remembers a sunrise none of them will live to see." },
    ];
}
