using AlfarQuest.Client.Game;
using AlfarQuest.Client.Models;
using AlfarQuest.Client.Services;
using AlfarQuest.Client.Services.Profile;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AlfarQuest.Client.Pages;

public sealed partial class Play : IAsyncDisposable
{
    // Above ground and below it. Which one plays is decided by the world's own
    // stage, frame by frame, rather than by a transition — so entering the mine
    // and loading a save that is already in it sound the same.
    private const string ApproachTrack = "audio/the-cleric-game.mp3";
    private const string CavernTrack = "audio/ballad-of-the-wandering.mp3";

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private GameClock Clock { get; set; } = default!;
    [Inject] private PartyState Party { get; set; } = default!;
    [Inject] private PlayTimeTracker PlayTime { get; set; } = default!;
    [Inject] private CampaignSaveService Campaign { get; set; } = default!;
    [Inject] private LootState Loot { get; set; } = default!;

    private const string SheetHold = "character-window";
    private const string LevelUpHold = "level-up";

    private IJSObjectReference? _module;
    private bool Muted;
    private bool SheetOpen;
    private bool AtCraftsman;
    private DotNetObjectReference<Play>? _self;

    /// <summary>The level-up on screen. Held here rather than peeked from the
    /// queue each render, so dismissing one and showing the next is a single
    /// deliberate step.</summary>
    private LevelUp? CurrentLevelUp;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // Nothing above this line touches game state. Reaching here at all means
        // the route guard let the page render, which means there is a session.
        Party.Load(GameSession.PartyKeys);
        // Before the world is built: the engine offers an opened container to
        // whoever is listening, and nothing listening means the contents go
        // straight to the pack with no window at all.
        Loot.Attach();

        // Before the world is built, so a returning player starts the session at
        // the level they left it — the engine reads attribute-derived numbers on
        // its first tick, and restoring after that would spend a frame with the
        // wrong hero.
        await Campaign.LoadAsync();

        Party.LevelledUp += OnLevelledUp;
        PlayTime.Start();

        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/game.js");
        _self = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("startGame",
            string.Join(',', GameSession.PartyKeys),
            Nav.BaseUri + ApproachTrack,
            Nav.BaseUri + CavernTrack,
            _self);
    }

    /// <summary>Raised by the JS input layer when a menu key is pressed. Keeping
    /// the binding there rather than on a Blazor element means it works while the
    /// canvas has focus, which is almost always.</summary>
    /// <summary>A menu key arrived from the input layer.
    ///
    /// Escape means "close the thing in front of me", and what that is depends on
    /// what is open. Resolved here because this page is the only thing that can
    /// see all of them at once — shallowest first, so one press does not close
    /// the whole stack.</summary>
    [JSInvokable]
    public async Task MenuKey(string action, string? activeHeroKey = null)
    {
        if (action != "close") { await ToggleCharacterSheet(activeHeroKey); return; }

        // The level-up window is deliberately not in this list: it is dismissed
        // by reading it, and an Escape reflex should not skip past a level.
        // StateHasChanged after each: this arrives from JS, and Blazor only
        // re-renders automatically after its own event handlers. Without it the
        // sheet closed in state and stayed on screen.
        if (Loot.Open is not null) { Loot.Close(); StateHasChanged(); return; }
        if (SheetOpen) { await CloseSheet(); StateHasChanged(); }
    }

    [JSInvokable]
    public async Task ToggleCharacterSheet(string? activeHeroKey = null)
    {
        SheetOpen = !SheetOpen;
        // Open on whoever the player is steering, not on whoever happens to be
        // first in the party.
        if (SheetOpen)
        {
            if (!string.IsNullOrEmpty(activeHeroKey)) Party.Select(activeHeroKey);
            await SyncVitals();
        }
        await ApplyPause();
        StateHasChanged();
    }

    private async Task SyncVitals()
    {
        if (_module is null) return;
        var vitals = await _module.InvokeAsync<HeroVitals[]>("partyVitals");
        Party.SyncVitals(vitals);
        AtCraftsman = await _module.InvokeAsync<bool>("atCraftsman");
    }

    /// <summary>Same action as the C key, for players who reach for a button.</summary>
    private async Task OpenCharacterSheet()
    {
        var key = _module is null ? null : await _module.InvokeAsync<string>("activeHero");
        await ToggleCharacterSheet(key);
    }

    private Task CloseSheet()
    {
        SheetOpen = false;
        return ApplyPause();
    }

    // ---- levelling ------------------------------------------------------

    /// <summary>A hero grew. Freeze the world and show the first one waiting.
    ///
    /// Raised from the simulation's frame callback, so the re-render has to be
    /// marshalled onto the renderer — and the whole thing is fire-and-forget
    /// because the engine is mid-tick and must not be made to await a dialog.</summary>
    private void OnLevelledUp() => _ = InvokeAsync(async () =>
    {
        if (CurrentLevelUp is not null) return;      // one at a time; the rest queue
        await ShowNextLevelUp();
    });

    private async Task ShowNextLevelUp()
    {
        CurrentLevelUp = Party.PendingLevelUps.Count > 0 ? Party.PendingLevelUps.Dequeue() : null;

        if (CurrentLevelUp is not null)
        {
            // Show the sheet the new numbers, not the ones from before the kill.
            await SyncVitals();
            await _module!.InvokeVoidAsync("playLevelUp");
        }

        await ApplyPause();
        StateHasChanged();
    }

    /// <summary>Continue: show the next level-up, or hand off to the character
    /// window so the points earned are spent while the reason is still on screen.
    /// Making the player find the sheet themselves is how points go unspent.</summary>
    private async Task DismissLevelUp()
    {
        var last = CurrentLevelUp;
        CurrentLevelUp = null;

        if (Party.PendingLevelUps.Count > 0) { await ShowNextLevelUp(); return; }

        if (last is not null)
        {
            Party.Select(last.Hero.Key);
            SheetOpen = true;
        }

        await ApplyPause();
        StateHasChanged();
    }

    private async Task ApplyPause()
    {
        if (SheetOpen) Clock.Hold(SheetHold); else Clock.Release(SheetHold);
        if (CurrentLevelUp is not null) Clock.Hold(LevelUpHold); else Clock.Release(LevelUpHold);
        if (_module is not null) await _module.InvokeVoidAsync("setPaused", Clock.IsPaused);
    }

    private async Task ToggleMute()
    {
        if (_module is not null) Muted = await _module.InvokeAsync<bool>("toggleMute");
    }

    private async Task Quit()
    {
        await StopAsync();
        // Both before navigating: once the router moves on this component is
        // disposed, and an unawaited call would be cancelled mid-flight.
        await PersistAsync();
        Nav.NavigateTo("/heroes");
    }

    /// <summary>Writes what the session earned. Called on the way out by either
    /// route, and safe twice — the tracker will not double-count and the save is
    /// an upsert.</summary>
    private async Task PersistAsync()
    {
        await Campaign.SaveAsync(_module is null ? "the approach" : await CurrentRegion());
        await PlayTime.ReportAsync();
    }

    private async Task<string> CurrentRegion()
    {
        try { return await _module!.InvokeAsync<string>("currentRegion"); }
        catch { return "the approach"; }
    }

    private async Task StopAsync()
    {
        if (_module is null) return;
        try { await _module.InvokeVoidAsync("stopGame"); } catch { /* page is going away */ }
    }

    public async ValueTask DisposeAsync()
    {
        Party.LevelledUp -= OnLevelledUp;
        Clock.Release(SheetHold);
        Clock.Release(LevelUpHold);
        await StopAsync();
        // Covers leaving by any other route — the top bar, the back button.
        // Both calls are idempotent, so Quit having already run is harmless.
        await PersistAsync();
        _self?.Dispose();
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch { /* already torn down */ }
        }
    }
}
