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
// Singleton so the character window reopens on the tab it was left on, with the
// search and filters still set — component state would not survive it closing.
builder.Services.AddSingleton<AlfarQuest.Client.Services.Character.CharacterWindowState>();
// What is inside the container the player has open. Singleton because it holds
// the engine's own loot stack by reference — a second instance would be a second
// opinion about what is left in the chest.
builder.Services.AddSingleton<LootState>();
// The shop the player has open. Singleton because it holds each merchant's live
// shelves for the session — a second instance would forget what was already
// bought and quietly restock the shelf on the next visit.
builder.Services.AddSingleton<ShopState>();
// The journal, a letter, a thing looked closely at. Singleton so the reading
// panel is wired once and every examinable in the world feeds the same window.
builder.Services.AddSingleton<ReadState>();
// The question menu a villager answers. Singleton so the engine's offer is
// wired once and every talking NPC feeds the same window.
builder.Services.AddSingleton<DialogueState>();

builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
