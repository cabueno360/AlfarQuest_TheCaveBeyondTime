namespace AlfarQuest.Client.Game;

/// <summary>How the simulation grows the party mid-campaign — the Cleric joining
/// at the Cave.
///
/// The engine adds the new <see cref="Hero"/> to its own party for movement and
/// combat; this tells whoever owns the character sheets to give the newcomer a
/// sheet, starting gear and a place in the save. Unset (a test, or before the UI
/// loads) the engine party still grows — only the sheet side is skipped, so the
/// simulation runs with no interface attached.</summary>
public static class PartyBridge
{
    /// <summary>Engine → sheets: give this hero key a character sheet and add them
    /// to the party roster. Idempotent on the sheet side.</summary>
    public static Action<string>? OnRecruit;

    /// <summary>How many recruits have been announced — a counter for tests.</summary>
    public static int Recruited { get; private set; }

    public static void Recruit(string key)
    {
        OnRecruit?.Invoke(key);
        Recruited++;
    }
}
