namespace AlfarQuest.Client.Game;

// Lightweight in-memory hand-off between the hero-select screen and the play screen.
public static class GameSession
{
    // Party order; index 0 is the hero the player starts controlling. The Cleric is
    // NOT here at the start — he joins at the Cave (see World.RecruitCleric), after
    // which the save carries him and the select screen unlocks him.
    public static string[] PartyKeys { get; set; } = { "mage", "thief" };
    public static string PlayerName { get; set; } = "Wanderer";

    /// <summary>Which save slot this session plays. Zero means a NEW game: the
    /// campaign loader reads nothing and the first exit creates a fresh save.</summary>
    public static int SaveId { get; set; }

    /// <summary>Whether the select screen actually made a choice this app
    /// lifetime. False on a cold arrival at /play (a refresh mid-game, a
    /// bookmark) — where a zero SaveId means "nothing chosen", NOT "new game",
    /// and the campaign loader resumes the newest save instead.</summary>
    public static bool SlotChosen { get; set; }

    /// <summary>The playthrough's name — chosen at New Game, shown on the slot.</summary>
    public static string SaveName { get; set; } = "";

    /// <summary>Where a continued save stands the party back up. Set by the
    /// campaign loader from the save, read ONCE by the World constructor and
    /// cleared, so a later fresh run starts at the gate as ever.</summary>
    public static string? ResumeRegion { get; set; }
    public static float ResumeX { get; set; }
    public static float ResumeY { get; set; }

    /// <summary>The world clock, in hours 0–24. Advances only while the game runs —
    /// the update loop moves it, so it never ticks in a menu. Starts mid-morning.</summary>
    public static float TimeOfDay { get; set; } = 8f;

    /// <summary>Real seconds per in-world hour. 90 → a full day-night cycle takes 36
    /// real minutes; the hours pass slowly, by request. One knob to retune the pace.</summary>
    public const float SecondsPerHour = 90f;

    /// <summary>Forgets everything belonging to the signed-in player. Called on
    /// sign-out: these are statics, and a sign-out is only a navigation — without
    /// this, the NEXT account inherited the last one's slot id and delve name,
    /// and its first save was written wearing a stranger's title.</summary>
    public static void ForgetPlayer()
    {
        PartyKeys = ["mage", "thief"];
        PlayerName = "Wanderer";
        SaveId = 0;
        SlotChosen = false;
        SaveName = "";
        ResumeRegion = null;
        ResumeX = ResumeY = 0f;
        TimeOfDay = 8f;
    }
}
