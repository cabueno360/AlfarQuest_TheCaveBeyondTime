namespace AlfarQuest.Api.Auth;

/// <summary>Tunables for the authentication layer, bound from configuration so a
/// deployment can shorten sessions without a rebuild.</summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>How long a plain sign-in lasts. Short enough that a token left on
    /// a shared machine goes stale by the next day.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(12);

    /// <summary>How long "Remember me" lasts instead.</summary>
    public TimeSpan RememberMeLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Sign-in attempts allowed per address per window before the endpoint
    /// starts refusing. Blunt, but it turns an online guessing attack from cheap
    /// into pointless.</summary>
    public int SignInAttemptsPerWindow { get; set; } = 8;
    public TimeSpan SignInWindow { get; set; } = TimeSpan.FromMinutes(5);
}

public static class AuthSchemes
{
    /// <summary>Named rather than "Bearer" so that adding a JWT scheme later for
    /// service-to-service calls does not collide with this one.</summary>
    public const string Session = "AlfarSession";
}
