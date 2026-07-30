namespace AlfarQuest.Client.Services;

/// <summary>The hand-made dialogue/shop portraits — a bust that stands in for the
/// speaker in the conversation and shop panels.
///
/// Keyed by the NPC's sprite <c>Kind</c>, not the individual, so every merchant
/// wears the merchant's face until a named one earns their own. A kind with no
/// asset falls back to the role glyph the windows drew before — so this list is
/// simply "who has art yet", and adding a portrait is one entry plus the PNG in
/// <c>wwwroot/assets/portraits/</c>.</summary>
public static class Portraits
{
    static readonly HashSet<string> Have = new(StringComparer.Ordinal)
    {
        "npcMerchant",
        "npcHunter",
        "npcGuard",
        "npcWagoner",
        "npcNoble",
        "npcBlacksmith",
        "npcAlchemist",
        "npcOldWoman",
        "npcApprentice",
    };

    /// <summary>Whether this sprite-kind has a portrait asset to show.</summary>
    public static bool Has(string kind) => Have.Contains(kind);

    /// <summary>Where its portrait lives, relative to wwwroot.</summary>
    public static string Path(string kind) => $"assets/portraits/{kind}.png";
}
