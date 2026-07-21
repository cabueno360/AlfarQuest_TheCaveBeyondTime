using System.Net.Http.Json;
using AlfarQuest.Client.Game;
using AlfarQuest.Shared;

namespace AlfarQuest.Client.Services;

// Talks to AlfarQuest.Api. If the backend/MySQL isn't running yet, everything
// degrades gracefully so the game is still fully playable offline.
public class GameApiClient
{
    private readonly HttpClient _http;
    public bool ApiReachable { get; private set; }

    public GameApiClient(HttpClient http) => _http = http;

    public async Task<List<HeroDto>> GetHeroesAsync()
    {
        try
        {
            var heroes = await _http.GetFromJsonAsync<List<HeroDto>>("api/heroes");
            ApiReachable = heroes is not null;
            return heroes ?? Lore.AsDtos();
        }
        catch
        {
            ApiReachable = false;
            return Lore.AsDtos(); // offline fallback: use built-in lore data
        }
    }

    public async Task<SaveGameDto?> SaveAsync(SaveGameDto save)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/saves", save);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<SaveGameDto>();
        }
        catch
        {
            return null; // caller can fall back to localStorage
        }
    }

    /// <summary>The signed-in player's own saves, newest first. No id is needed —
    /// the server works it out from the token, which is also why nobody can ask
    /// for someone else's.</summary>
    public async Task<IReadOnlyList<SaveGameDto>> MySavesAsync()
    {
        try { return await _http.GetFromJsonAsync<List<SaveGameDto>>("api/saves/mine") ?? []; }
        catch { return []; }
    }

    public async Task<SaveGameDto?> LoadAsync(int id)
    {
        try { return await _http.GetFromJsonAsync<SaveGameDto>($"api/saves/{id}"); }
        catch { return null; }
    }
}
