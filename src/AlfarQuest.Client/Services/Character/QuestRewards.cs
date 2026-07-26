using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Services.Character;

/// <summary>The rare, story-bound items a quest pays out — the ones "Story for
/// Music" names, not the merchant's stock. Kept in their own source so a quest
/// reward is never silently a common drop, and so ItemCatalog carries them by
/// construction (QuestCatalog references them only by id).</summary>
public static class QuestRewards
{
    public static readonly IReadOnlyList<Item> All =
    [
        // The Delver's Pact — the whole story turns on it. The panacea Cerno drew
        // from the Cave, that the Cleric bought with his own mind to wake Mirka.
        new()
        {
            Id = "phial_panacea", Name = "Phial of Panacea", Slot = Slot.Artifact,
            Rarity = Rarity.Mythic, Icon = "⚗", Value = 0,
            Attributes = new() { Wisdom = 5, Vitality = 4 }, Armour = 6, CritChance = 0.04f,
            Flavour = "The cure-all Cerno drew from the Cave, and the Cleric paid for with his " +
                      "own mind. A single drop would wake Mirka; a whole phial cannot be spent.",
        },
        // And a shard of the Heart, the same kind that is fused in the Thief's arm.
        new()
        {
            Id = "crystal_heart_shard", Name = "Crystal Heart Shard", Slot = Slot.Necklace,
            Rarity = Rarity.Legendary, Icon = "◈", Value = 0,
            Attributes = new() { Intelligence = 4, Luck = 2 }, CritChance = 0.06f,
            Flavour = "A splinter of the Heart itself, still humming with the deep. The Thief " +
                      "carries one grown into his arm; this one you may take off again.",
        },

        // The Guide Who Returned — Cerno's mark, from before the black cloud took him.
        new()
        {
            Id = "kaladash_signet", Name = "Signet of Kaladash", Slot = Slot.Ring1,
            Rarity = Rarity.Epic, Icon = "✷", Value = 0,
            Attributes = new() { Strength = 3, Dexterity = 2 }, CritChance = 0.05f,
            Flavour = "Cerno's ring, from when he was only the greatest warrior of Kaladash. " +
                      "He pressed it on you at the cave-mouth — 'so they know you were sent.'",
        },

        // Word for the Cleric — Mirka's locket, warm from her father's hand.
        new()
        {
            Id = "mirka_locket", Name = "Mirka's Locket", Slot = Slot.Necklace,
            Rarity = Rarity.Rare, Icon = "❁", Value = 0,
            Attributes = new() { Wisdom = 2, Vitality = 2 },
            Flavour = "Inside, a curl of hair and a name worn almost smooth. Carry it down, " +
                      "her father said, and let the Cleric see for himself that she still waits.",
        },
    ];
}
