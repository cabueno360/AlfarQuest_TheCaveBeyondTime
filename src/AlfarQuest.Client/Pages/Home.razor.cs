using AlfarQuest.Client.Game;
using AlfarQuest.Client.Services;
using AlfarQuest.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AlfarQuest.Client.Pages;

public sealed partial class Home : IAsyncDisposable
{
    private const string IntroTrack = "audio/crystal-deep-intro.wav";
    private const double IntroVolume = 0.5;

    // Canonical party order. The chosen lead is moved to the front; the other
    // two keep this order behind them.
    private static readonly string[] PartyOrder = ["mage", "cleric", "thief"];

    [Inject] private GameApiClient Api { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IReadOnlyList<HeroDto>? Heroes;
    private string Lead = "mage";
    private bool Muted;
    private IJSObjectReference? _music;

    protected override async Task OnInitializedAsync() => Heroes = await Api.GetHeroesAsync();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _music = await JS.InvokeAsync<IJSObjectReference>("import", "./js/titlemusic.js");
        await _music.InvokeVoidAsync("playIntro", IntroTrack, IntroVolume);
    }

    private void SelectLead(string key) => Lead = key;

    private async Task ToggleMusic()
    {
        if (_music is not null) Muted = await _music.InvokeAsync<bool>("toggleIntroMute");
    }

    private async Task StopMusic()
    {
        if (_music is null) return;
        try { await _music.InvokeVoidAsync("stopIntro"); } catch { /* page is going away */ }
    }

    private async Task Begin()
    {
        // The cavern track takes over from here, so fade the intro out first.
        await StopMusic();
        GameSession.PartyKeys = [Lead, .. PartyOrder.Where(k => k != Lead)];
        Nav.NavigateTo("/play");
    }

    public async ValueTask DisposeAsync()
    {
        await StopMusic();
        if (_music is not null)
        {
            try { await _music.DisposeAsync(); } catch { /* already torn down */ }
        }
    }
}
