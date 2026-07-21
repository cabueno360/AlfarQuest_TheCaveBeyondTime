namespace AlfarQuest.Client.Game;

// Lightweight in-memory hand-off between the hero-select screen and the play screen.
public static class GameSession
{
    // Party order; index 0 is the hero the player starts controlling.
    public static string[] PartyKeys { get; set; } = { "mage", "cleric", "thief" };
    public static string PlayerName { get; set; } = "Wanderer";
}
