using AlfarQuest.Shared;

namespace AlfarQuest.Client.Game;

// Canon-facing hero data. Names are placeholders — in "Story for Music" the three
// delvers are only ever "The Mage", "The Cleric" and "The Thief", so these display
// names are marked provisional until they're named in the band's canon.
public static class Lore
{
    public record HeroDef(
        string Key, string Name, string Title, string HeroClass, string Description,
        int BaseHp, float Speed, AttackKind Attack, float Range, int Damage,
        float AttackCooldown, string ColorPrimary, string ColorAccent, string SwatchCss,
        bool UnlockedByDefault);

    public enum AttackKind { Melee, Ranged }

    public static readonly IReadOnlyList<HeroDef> Heroes = new List<HeroDef>
    {
        new(
            Key: "mage",
            Name: "The Fallen Mage",
            Title: "Bearer of the Caged Fire",
            HeroClass: "Mage",
            Description: "An exile of Kae Ychel with a greater demon bound inside his heart. Hurls discs of light — and, when pressed, unleashes the hellfire he can barely contain.",
            BaseHp: 90, Speed: 118f, Attack: AttackKind.Ranged, Range: 520f, Damage: 16,
            AttackCooldown: 0.42f, ColorPrimary: "#1f6f6a", ColorAccent: "#d8b45a",
            SwatchCss: "linear-gradient(90deg,#1f6f6a,#d8b45a)", UnlockedByDefault: true),

        new(
            Key: "cleric",
            Name: "The Grieving Cleric",
            Title: "Whose Faith Fractured",
            HeroClass: "Cleric",
            Description: "A priest who traded his own descent into madness for a phial of panacea. Wades in with blessed plate and gilded mace, and can loose a nova of holy light.",
            BaseHp: 140, Speed: 104f, Attack: AttackKind.Melee, Range: 74f, Damage: 26,
            AttackCooldown: 0.55f, ColorPrimary: "#e9e7f0", ColorAccent: "#c1442e",
            // Not from the start: the Cleric is still in his northern village at his
            // wife's bedside. He joins the party at the mouth of the Cave (in Cerno's
            // stead), so he is locked on the select screen until that first descent.
            SwatchCss: "linear-gradient(90deg,#e9e7f0,#c1442e)", UnlockedByDefault: false),

        new(
            Key: "thief",
            Name: "The Hollow Thief",
            Title: "Whose Crew the Crystal Took",
            HeroClass: "Thief",
            Description: "A Seoshe gambler with a shard of the stolen crystal fused into his arm. Fires a crossbow from the dark and dashes through danger with reckless luck.",
            BaseHp: 100, Speed: 132f, Attack: AttackKind.Ranged, Range: 430f, Damage: 14,
            AttackCooldown: 0.30f, ColorPrimary: "#2f5d43", ColorAccent: "#8fd0a6",
            SwatchCss: "linear-gradient(90deg,#2f5d43,#8fd0a6)", UnlockedByDefault: true),
    };

    public static HeroDef ByKey(string key) => Heroes.First(h => h.Key == key);

    public static List<HeroDto> AsDtos() => Heroes.Select(h => new HeroDto
    {
        Key = h.Key, Name = h.Name, Title = h.Title, HeroClass = h.HeroClass,
        Description = h.Description, BaseHp = h.BaseHp, UnlockedByDefault = h.UnlockedByDefault
    }).ToList();
}
