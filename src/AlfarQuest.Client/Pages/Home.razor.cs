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

    /// <summary>Every save this player owns — the slot picker. The roster below
    /// shows each hero at the level and gear of the SELECTED slot.</summary>
    private List<SaveGameDto>? _saves;
    private SaveGameDto? SelectedSave;

    /// <summary>Whether the New Game panel is open — a fresh roster and a name
    /// box instead of a slot's party.</summary>
    private bool _newGame;
    private string _newName = "";

    /// <summary>The save id whose delete button was pressed once — the second
    /// press deletes. Selecting anything else resets it, so a stray click never
    /// costs a campaign.</summary>
    private int _confirmDelete;

    protected override async Task OnInitializedAsync()
    {
        Heroes = await Api.GetHeroesAsync();

        // Best-effort: a new player has no save, and the roster simply shows fresh
        // recruits. A failure here must never keep the player off the title screen.
        try
        {
            _saves = [.. await Api.MySavesAsync()];
            SelectedSave = _saves.FirstOrDefault();
            _newGame = _saves.Count == 0;
            if (SelectedSave is not null && IsUnlocked(SelectedSave.ActiveHeroKey))
                Lead = SelectedSave.ActiveHeroKey;
        }
        catch { _saves = []; _newGame = true; }
    }

    private SaveHeroDto? ProgressFor(string key) =>
        _newGame ? null : SelectedSave?.Party.FirstOrDefault(h => h.HeroKey == key);

    /// <summary>Whether a hero can be chosen yet. Unlocked from the start (the Mage
    /// and the Thief), or already recruited in the SELECTED save (the Cleric, once
    /// he has joined at the Cave). A new game starts with only the defaults.</summary>
    private bool IsUnlocked(string key) =>
        (Heroes?.FirstOrDefault(h => h.Key == key)?.UnlockedByDefault ?? false)
        || (!_newGame && SelectedSave?.Party.Any(h => h.HeroKey == key) == true);

    private void SelectSave(SaveGameDto s)
    {
        SelectedSave = s;
        _newGame = false;
        _confirmDelete = 0;
        Lead = s.Party.Any(h => h.HeroKey == s.ActiveHeroKey) || s.ActiveHeroKey is "mage" or "thief"
            ? s.ActiveHeroKey : "mage";
    }

    private void OpenNewGame()
    {
        _newGame = true;
        SelectedSave = null;
        _confirmDelete = 0;
        _newName = "";
        Lead = "mage";
    }

    /// <summary>Deletes a slot — on the SECOND press. The first arms it and turns
    /// the ✕ into a question, so one slip never costs a campaign.</summary>
    private async Task DeleteSave(SaveGameDto s)
    {
        if (_confirmDelete != s.Id) { _confirmDelete = s.Id; return; }
        _confirmDelete = 0;
        if (!await Api.DeleteSaveAsync(s.Id)) return;
        _saves?.Remove(s);
        if (SelectedSave?.Id == s.Id) SelectedSave = _saves?.FirstOrDefault();
        if (SelectedSave is null) _newGame = true;
    }

    private string SlotName(SaveGameDto s) =>
        string.IsNullOrWhiteSpace(s.PlayerName) ? L["Unnamed delve"] : s.PlayerName;

    private string Prettify(string? region)
    {
        if (string.IsNullOrWhiteSpace(region)) return L["The approach"];
        // Region ids read as "r2_whispering_wood"; the slot shows "Whispering Wood".
        var trimmed = System.Text.RegularExpressions.Regex.Replace(region, @"^r\d+_", "");
        var name = string.Join(' ', trimmed.Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(w[0]) + w[1..]));
        return L[name];
    }

    private string Ago(DateTime whenUtc)
    {
        var d = DateTime.UtcNow - whenUtc;
        if (d.TotalMinutes < 1) return L["just now"];
        if (d.TotalHours < 1) return L.Format("{0} min ago", (int)d.TotalMinutes);
        if (d.TotalDays < 1) return L.Format("{0} h ago", (int)d.TotalHours);
        return L.Format("{0} d ago", (int)d.TotalDays);
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

        // Which slot this session plays. Cleared resume hand-off either way — the
        // campaign loader sets it again from the save it actually reads.
        GameSession.SlotChosen = true;
        GameSession.ResumeRegion = null;
        GameSession.ResumeX = GameSession.ResumeY = 0;
        if (_newGame || SelectedSave is null)
        {
            GameSession.SaveId = 0;
            GameSession.SaveName = string.IsNullOrWhiteSpace(_newName)
                ? L.Format("Delve of {0}", DateTime.Now.ToString("dd/MM HH:mm"))
                : _newName.Trim();
        }
        else
        {
            GameSession.SaveId = SelectedSave.Id;
            GameSession.SaveName = SelectedSave.PlayerName;
        }
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
