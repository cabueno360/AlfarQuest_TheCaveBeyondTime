using AlfarQuest.Client.Services.Profile;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace AlfarQuest.Client.Services.Auth;

/// <summary>Wires the authentication and profile stack, including the one
/// HttpClient every authenticated call goes through.</summary>
public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddPlayerAccounts(this IServiceCollection services, Uri apiBase)
    {
        // Singletons, not scoped. A WebAssembly app is one long-lived scope, and
        // registering the session per-scope would mean each component graph could
        // end up with its own idea of who is signed in.
        services.AddSingleton<TokenStore>();
        services.AddSingleton<UserSessionService>();
        services.AddSingleton<AlfarQuestAuthenticationStateProvider>();
        services.AddSingleton<CurrentUserProvider>();
        services.AddSingleton<NotificationService>();

        // Blazor resolves the base type; both registrations must reach the same
        // instance, or the router would watch a provider nobody ever notifies.
        services.AddSingleton<AuthenticationStateProvider>(
            sp => sp.GetRequiredService<AlfarQuestAuthenticationStateProvider>());

        // The token handler sits on the pipe rather than beside it: there is one
        // HttpClient in the app, and it cannot be built without going through
        // this, so no call can be made without a token attached.
        services.AddSingleton(sp => new AuthTokenHandler(sp.GetRequiredService<UserSessionService>())
        {
            InnerHandler = new HttpClientHandler(),
        });

        services.AddSingleton(sp => new HttpClient(sp.GetRequiredService<AuthTokenHandler>())
        {
            BaseAddress = apiBase,
        });

        services.AddSingleton<AuthenticationApi>();
        services.AddSingleton<AuthenticationService>();
        services.AddSingleton<SessionBootstrapper>();

        services.AddSingleton<ProfileService>();
        services.AddSingleton<AvatarService>();
        services.AddSingleton<PlayTimeTracker>();
        services.AddSingleton<CampaignSaveService>();

        return services;
    }
}
