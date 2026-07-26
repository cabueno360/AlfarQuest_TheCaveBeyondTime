using AlfarQuest.Client.Game;
using AlfarQuest.Client.Services;
using AlfarQuest.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AlfarQuest.Client.Pages;

public sealed partial class Home : IAsyncDisposable
{
    // The intro plays once and hands over to the theme, which loops until the
    // player leaves for the world.
    private const string IntroTrack = "audio/crystal-deep-intro.wav";
    private const string ThemeTrack = "audio/into-the-crystal-deep.mp3";
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

    /// <summary>The newest save, if any, so the roster can show each hero at the
    /// level and gear the player left them — the selection screen the brief asks
    /// for, where progression is visible before the descent.</summary>
    private SaveGameDto? _save;
    private IReadOnlyDictionary<string, SaveHeroDto> _progress =
        new Dictionary<string, SaveHeroDto>();

    protected override async Task OnInitializedAsync()
    {
        Heroes = await Api.GetHeroesAsync();

        // Best-effort: a new player has no save, and the roster simply shows fresh
        // recruits. A failure here must never keep the player off the title screen.
        try
        {
            var saves = await Api.MySavesAsync();
            _save = saves.Count > 0 ? saves[0] : null;
            _progress = _save?.Party.ToDictionary(h => h.HeroKey) ?? _progress;
        }
        catch { /* no save, or the server is briefly unreachable */ }
    }

    private SaveHeroDto? ProgressFor(string key) => _progress.GetValueOrDefault(key);

    /// <summary>Whether a hero can be chosen yet. Unlocked from the start (the Mage
    /// and the Thief), or already recruited in the save (the Cleric, once he has
    /// joined at the Cave — he is then in the save's party). The Cleric stays
    /// locked until that first descent.</summary>
    private bool IsUnlocked(string key) =>
        (Heroes?.FirstOrDefault(h => h.Key == key)?.UnlockedByDefault ?? false)
        || _progress.ContainsKey(key);

    /// <summary>Where the party stands and when it last did — the save-level lines
    /// the selection screen shows above the roster. Null when there is no save to
    /// summarise, so the markup can leave the strip out for a new player.</summary>
    private bool HasSave => _save is not null;
    private string Location => Prettify(_save?.Region);
    private string LastPlayed => _save is null ? "—" : Ago(_save.UpdatedAt);
    private string PartyPlayTime => _save is null ? "—" : Duration(_save.PlaytimeSeconds);

    private static string Prettify(string? region) => string.IsNullOrWhiteSpace(region)
        ? "The approach"
        : string.Join(' ', region.Replace('_', ' ').Split(' ',
            StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpper(w[0]) + w[1..]));

    private static string Ago(DateTime whenUtc)
    {
        var d = DateTime.UtcNow - whenUtc;
        if (d.TotalMinutes < 1) return "just now";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} min ago";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours} h ago";
        return $"{(int)d.TotalDays} d ago";
    }

    private static string Duration(long seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{t.Minutes}m";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _music = await JS.InvokeAsync<IJSObjectReference>("import", "./js/titlemusic.js");
        await _music.InvokeVoidAsync("playIntro", IntroTrack, ThemeTrack, IntroVolume);
    }

    private void SelectLead(string key)
    {
        if (IsUnlocked(key)) Lead = key;
    }

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
        // Only heroes that are available — the Cleric is left out until he has
        // joined at the Cave (and so appears in the save). The lead is whoever was
        // chosen, if they are available; otherwise the first one who is.
        var roster = PartyOrder.Where(IsUnlocked).ToList();
        var lead = roster.Contains(Lead) ? Lead : roster[0];
        GameSession.PartyKeys = [lead, .. roster.Where(k => k != lead)];
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
