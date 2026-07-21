using AlfarQuest.Client.Services;
using AlfarQuest.Client.Services.Auth;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<AlfarQuest.Client.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Base address of the AlfarQuest.Api backend (edit in wwwroot/appsettings.json).
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7080/";

// Accounts, sessions, profiles, avatars — and the single authenticated
// HttpClient the rest of the app shares.
builder.Services.AddPlayerAccounts(new Uri(apiBase));

// Singleton, not scoped: CampaignSaveService is a singleton and holds one,
// and a scoped dependency of a singleton is a captive that outlives its scope.
builder.Services.AddSingleton<GameApiClient>();
// Shared by the game page and every menu that freezes or reads the party.
builder.Services.AddSingleton<GameClock>();
builder.Services.AddSingleton<PartyState>();

builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
