namespace AlfarQuest.Client.Game;

// Lightweight in-memory hand-off between the hero-select screen and the play screen.
public static class GameSession
{
    // Party order; index 0 is the hero the player starts controlling. The Cleric is
    // NOT here at the start — he joins at the Cave (see World.RecruitCleric), after
    // which the save carries him and the select screen unlocks him.
    public static string[] PartyKeys { get; set; } = { "mage", "thief" };
    public static string PlayerName { get; set; } = "Wanderer";

    /// <summary>The world clock, in hours 0–24. Advances only while the game runs —
    /// the update loop moves it, so it never ticks in a menu. Starts mid-morning.</summary>
    public static float TimeOfDay { get; set; } = 8f;

    /// <summary>Real seconds per in-world hour. 90 → a full day-night cycle takes 36
    /// real minutes; the hours pass slowly, by request. One knob to retune the pace.</summary>
    public const float SecondsPerHour = 90f;
}
