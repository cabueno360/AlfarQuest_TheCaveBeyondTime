namespace AlfarQuest.Shared.Auth;

/// <summary>The one definition of "strong enough", shared so the meter the player
/// watches and the check the server enforces can never drift apart.</summary>
public static class PasswordRules
{
    public const int MinLength = 10;
    public const int StrongLength = 14;

    /// <summary>Scores 0..4. Length carries most of the weight on purpose: it is
    /// the property that actually resists an offline attack, whereas character
    /// classes mostly push people toward "Passw0rd!".</summary>
    public static int Score(string? password)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        var score = 0;
        if (password.Length >= MinLength) score++;
        if (password.Length >= StrongLength) score++;
        if (password.Length >= 20) score++;

        var classes = 0;
        if (password.Any(char.IsLower)) classes++;
        if (password.Any(char.IsUpper)) classes++;
        if (password.Any(char.IsDigit)) classes++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) classes++;
        if (classes >= 3) score++;

        return Math.Min(score, 4);
    }

    public static string Label(int score) => score switch
    {
        0 => "Too short",
        1 => "Weak",
        2 => "Fair",
        3 => "Strong",
        _ => "Excellent",
    };

    /// <summary>The server's hard floor. Deliberately only length: refusing a long
    /// passphrase for lacking a symbol makes users pick worse passwords.</summary>
    public static bool Acceptable(string? password) =>
        !string.IsNullOrEmpty(password) && password.Length >= MinLength;
}
