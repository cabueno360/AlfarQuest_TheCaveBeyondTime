namespace AlfarQuest.Client.Models;

/// <summary>One page of the character window.
///
/// A catalogue rather than a switch, because the brief's real requirement is that
/// future tabs — achievements, bestiary, reputation, guild — cost one entry and
/// no redesign. The window renders whatever is in this list; adding a row and a
/// component is the whole job.
///
/// <see cref="Available"/> is what lets a planned tab be shown honestly: visible,
/// named, and plainly not ready, rather than either missing or pretending.</summary>
public sealed record CharacterTab(string Key, string Name, string Icon, bool Available = true)
{
    public static readonly IReadOnlyList<CharacterTab> All =
    [
        new("character", "Character", "◈"),
        new("equipment", "Equipment", "🛡"),
        new("skills",    "Skills",    "✦"),
        new("inventory", "Inventory", "🎒"),
        new("stats",     "Stats",     "⚔"),
        new("crafting",  "Crafting",  "🔨"),
        new("quests",    "Quests",    "📜", Available: false),
    ];

    /// <summary>Where the window opens the first time. Named rather than "the
    /// first row", so reordering the strip cannot silently change it.</summary>
    public const string Default = "character";

    public static CharacterTab? Find(string? key) => All.FirstOrDefault(t => t.Key == key);

    /// <summary>Ordered keys a keyboard can move between — the unavailable ones
    /// are skipped, so arrowing along the strip never lands somewhere with
    /// nothing in it.</summary>
    public static IReadOnlyList<string> Navigable =>
        [.. All.Where(t => t.Available).Select(t => t.Key)];
}
