using AlfarQuest.Api.Accounts;
using AlfarQuest.Api.Profiles;
using Microsoft.AspNetCore.Authentication;

namespace AlfarQuest.Api.Auth;

/// <summary>One registration point for everything authentication and profiles
/// need, so Program.cs stays a list of what the app is made of rather than how
/// each part is built.</summary>
public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddPlayerAccounts(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));

        // Injected rather than DateTime.UtcNow, so expiry and revocation can be
        // tested by moving a clock instead of by sleeping.
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ExternalAccountLinker>();

        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAvatarService, AvatarService>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();

        services.AddAuthenticationRateLimiting();

        services.AddAuthentication(AuthSchemes.Session)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(AuthSchemes.Session, _ => { });

        services.AddAuthorization();

        return services;
    }
}
