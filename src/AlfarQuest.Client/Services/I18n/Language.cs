namespace AlfarQuest.Client.Services.I18n;

/// <summary>The languages the game speaks.
///
/// English is not "the default" so much as the source: every translatable string
/// is written in English in the code, and a translation is a lookup away from it.
/// A missing entry therefore degrades to readable English rather than to a key.</summary>
public enum Language
{
    English,
    Portuguese,
}

public static class Languages
{
    /// <summary>The BCP 47 tag — what goes in localStorage and in &lt;html lang&gt;.</summary>
    public static string Code(this Language language) =>
        language == Language.Portuguese ? "pt-BR" : "en-US";

    /// <summary>The language's name in itself. A picker that says "Portuguese" to
    /// someone who only reads Portuguese has the label in the wrong language.</summary>
    public static string NativeName(this Language language) =>
        language == Language.Portuguese ? "Português" : "English";

    /// <summary>Reads a stored or browser-supplied tag. Anything unrecognised is
    /// English, so a corrupted setting cannot leave the player with no words.</summary>
    public static Language Parse(string? tag) =>
        tag?.StartsWith("pt", StringComparison.OrdinalIgnoreCase) == true
            ? Language.Portuguese
            : Language.English;
}
