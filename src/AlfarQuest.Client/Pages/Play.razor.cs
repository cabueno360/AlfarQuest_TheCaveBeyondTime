using AlfarQuest.Client.Game;
using AlfarQuest.Client.Models;
using AlfarQuest.Client.Services;
using AlfarQuest.Client.Services.Character;
using AlfarQuest.Client.Services.Profile;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AlfarQuest.Client.Pages;

public sealed partial class Play : IAsyncDisposable
{
    // One track per place, chosen by the world's own stage frame by frame (not by
    // a transition) — so loading a save already in the mine sounds the same as
    // walking into it. The overworld wanders, the deep is the crystal cave, and a
    // roof overhead — the Cleric's house and the rest — takes the quiet interior
    // theme. (Seoshe, an open-air city, keeps the road's ballad; see game.js.)
    private const string ApproachTrack = "audio/ballad-of-the-wandering.mp3";
    private const string CavernTrack = "audio/into-the-crystal-deep.mp3";
    // The interior theme is in two parts — Pt.1 opens once, Pt.2 loops beneath it
    // (the same intro-then-loop shape the title screen uses). Converted from the
    // orchestral .wav masters in /audio-masters to m4a so the game isn't shipping
    // 140 MB of uncompressed audio.
    private const string InteriorIntroTrack = "audio/the-cleric-pt1.m4a";
    private const string InteriorLoopTrack = "audio/the-cleric-pt2.m4a";

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private GameClock Clock { get; set; } = default!;
    [Inject] private PartyState Party { get; set; } = default!;
    [Inject] private PlayTimeTracker PlayTime { get; set; } = default!;
    [Inject] private CampaignSaveService Campaign { get; set; } = default!;
    [Inject] private LootState Loot { get; set; } = default!;
    [Inject] private ShopState Shop { get; set; } = default!;
    [Inject] private ReadState Read { get; set; } = default!;
    [Inject] private DialogueState Dialogue { get; set; } = default!;

    private const string SheetHold = "character-window";
    private const string LevelUpHold = "level-up";
    private const string CutsceneHold = "cutscene";
    private const string JournalHold = "quest-journal";

    /// <summary>Whether the quest journal (J) is open. Freezes the game like the
    /// character sheet while it shows.</summary>
    private bool JournalOpen;

    /// <summary>The fullscreen cutscene playing right now, or null. Set when the
    /// engine asks for a video (the first descent into the Cave); cleared when it
    /// ends or is skipped. While set, the game is frozen behind it.</summary>
    private string? _cutscene;

    /// <summary>Whether the current cutscene has already been nudged into playing —
    /// so the autoplay fallback fires once per cutscene, not every render.</summary>
    private bool _cuePlaying;

    private IJSObjectReference? _module;
    private bool Muted;
    private bool SheetOpen;
    private bool AtCraftsman;
    private DotNetObjectReference<Play>? _self;

    /// <summary>The level-up on screen. Held here rather than peeked from the
    /// queue each render, so dismissing one and showing the next is a single
    /// deliberate step.</summary>
    private LevelUp? CurrentLevelUp;

    /// <summary>Quest-complete banners currently on screen. Non-blocking, unlike a
    /// level-up — each is added when a quest finishes and drops itself after its
    /// animation, so several can stack without ever pausing the game.</summary>
    private readonly List<QuestToast> _questToasts = [];
    private int _questToastSeq;

    private sealed record QuestToast(int Id, string Title, IReadOnlyList<RewardChip> Items, int Xp, int Gold);
    private sealed record RewardChip(string Name, string Icon, string Colour);

    /// <summary>Whether the old clock is up. Non-blocking — the world runs behind it —
    /// so it does not go through the pause-hold path the sheet and journal use.</summary>
    private bool _clockOpen;
    private void ToggleClock() => _clockOpen = !_clockOpen;
    private void CloseClock() => _clockOpen = false;

    private void OnQuestCompleted(QuestDef quest) => _ = InvokeAsync(async () =>
    {
        var id = ++_questToastSeq;
        var items = quest.Reward.ItemIds
            .Select(ItemCatalog.Find)
            .Where(i => i is not null)
            .Select(i => new RewardChip(i!.Name, i.Icon, RarityInfo.Of(i.Rarity).Colour))
            .ToList();
        _questToasts.Add(new QuestToast(id, quest.Title, items, quest.Reward.Xp, quest.Reward.Gold));
        StateHasChanged();
        await Task.Delay(5400);   // a little longer: there is a reward to read now
        _questToasts.RemoveAll(t => t.Id == id);
        StateHasChanged();
    });

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            // The cutscene <video> only exists once it has rendered; nudge it into
            // playing in case the browser declines the `autoplay` attribute. Once
            // per cutscene, guarded by _cuePlaying.
            if (_cutscene is not null && !_cuePlaying && _module is not null)
            {
                _cuePlaying = true;
                await _module.InvokeVoidAsync("playCutscene");
            }
            return;
        }

        // Nothing above this line touches game state. Reaching here at all means
        // the route guard let the page render, which means there is a session.
        // Fresh sheets first: without this, a hero cached from the PREVIOUS slot
        // kept their old levels when this one recruited them — the campaign load
        // below rebuilds everything the chosen save actually holds.
        Party.StartFresh();
        Party.Load(GameSession.PartyKeys);
        // Before the world is built: the engine offers an opened container to
        // whoever is listening, and nothing listening means the contents go
        // straight to the pack with no window at all.
        Loot.Attach();
        // Likewise a merchant: with a listener the engine opens the shop on [E],
        // and without one a shopkeeper simply talks like any other villager.
        Shop.Attach();
        // And the reading panel — the journal, letters, examined things.
        Read.Attach();
        // And the question menu: with a listener the engine opens a conversation
        // on [E], and without one a villager just cycles their one-line balloon.
        Dialogue.Attach();

        // The engine can ask for a fullscreen cutscene — the first descent into the
        // Cave. It fires from inside a tick, so the handler marshals onto the
        // renderer and is fire-and-forget (the engine must not await a dialog); the
        // game freezes behind the video until it ends.
        VideoBridge.OnPlay = src =>
        {
            _ = InvokeAsync(async () =>
            {
                _cutscene = src;
                await ApplyPause();
                StateHasChanged();
            });
            return true;
        };

        // Before the world is built, so a returning player starts the session at
        // the level they left it — the engine reads attribute-derived numbers on
        // its first tick, and restoring after that would spend a frame with the
        // wrong hero.
        await Campaign.LoadAsync();

        Party.LevelledUp += OnLevelledUp;
        Party.QuestCompleted += OnQuestCompleted;
        PlayTime.Start();

        // Stage 1 is authored in Tiled. The world is built synchronously inside
        // startGame, and a .tmx has to be fetched, so it is registered here first —
        // see Game/Tiled/MapCatalog. A map that fails to arrive is not fatal: the
        // stage falls back to the generator it was migrated from.
        await LoadStageMapsAsync();

        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/game.js");
        _self = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("startGame",
            string.Join(',', GameSession.PartyKeys),
            Nav.BaseUri + ApproachTrack,
            Nav.BaseUri + CavernTrack,
            Nav.BaseUri + InteriorIntroTrack,
            Nav.BaseUri + InteriorLoopTrack,
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
        if (action == "character") { await ToggleCharacterSheet(activeHeroKey); return; }
        if (action == "journal") { await ToggleJournal(); return; }

        // The level-up window is deliberately not in this list: it is dismissed
        // by reading it, and an Escape reflex should not skip past a level.
        // StateHasChanged after each: this arrives from JS, and Blazor only
        // re-renders automatically after its own event handlers. Without it the
        // sheet closed in state and stayed on screen.
        if (Read.Open is not null) { Read.Close(); StateHasChanged(); return; }
        if (Shop.Open is not null) { Shop.Close(); StateHasChanged(); return; }
        if (Loot.Open is not null) { Loot.Close(); StateHasChanged(); return; }
        if (JournalOpen) { await CloseJournal(); StateHasChanged(); return; }
        if (SheetOpen) { await CloseSheet(); StateHasChanged(); }
    }

    [JSInvokable]
    public async Task ToggleCharacterSheet(string? activeHeroKey = null)
    {
        // The counter or the page comes first: pressing C mid-purchase or mid-read
        // should not stack the sheet on top. Finish, then open it.
        if (Shop.Open is not null || Read.Open is not null) return;

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

    /// <summary>Open or close the quest journal (J). Not stacked over the counter or
    /// a page, like the character sheet.</summary>
    private async Task ToggleJournal()
    {
        if (Shop.Open is not null || Read.Open is not null) return;
        JournalOpen = !JournalOpen;
        await ApplyPause();
        StateHasChanged();
    }

    private Task CloseJournal()
    {
        JournalOpen = false;
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

    /// <summary>The cutscene ended, or the player skipped it. Unfreeze and let the
    /// cave — already built behind the video, with the Cleric now at the party's
    /// side — come into view.</summary>
    private async Task EndCutscene()
    {
        if (_cutscene is null) return;
        _cutscene = null;
        _cuePlaying = false;
        await ApplyPause();
        VideoBridge.NotifyEnded();
        StateHasChanged();
    }

    private async Task ApplyPause()
    {
        if (SheetOpen) Clock.Hold(SheetHold); else Clock.Release(SheetHold);
        if (CurrentLevelUp is not null) Clock.Hold(LevelUpHold); else Clock.Release(LevelUpHold);
        if (_cutscene is not null) Clock.Hold(CutsceneHold); else Clock.Release(CutsceneHold);
        if (JournalOpen) Clock.Hold(JournalHold); else Clock.Release(JournalHold);
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
    /// an upsert. Where the party stood comes straight from the engine now.</summary>
    private async Task PersistAsync()
    {
        await Campaign.SaveAsync();
        await PlayTime.ReportAsync();
    }

    private async Task StopAsync()
    {
        if (_module is null) return;
        try { await _module.InvokeVoidAsync("stopGame"); } catch { /* page is going away */ }
    }

    public async ValueTask DisposeAsync()
    {
        Party.LevelledUp -= OnLevelledUp;
        Party.QuestCompleted -= OnQuestCompleted;
        VideoBridge.OnPlay = null;
        Clock.Release(SheetHold);
        Clock.Release(LevelUpHold);
        Clock.Release(CutsceneHold);
        Clock.Release(JournalHold);
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

    /// <summary>Fetches every map named in Maps/manifest.json and registers it for
    /// the engine to build from. The manifest is the ONE list of maps — game.js
    /// draws from the same file — so adding a place is a Tiled save plus one JSON
    /// line. Failure is logged and swallowed: an unregistered map means that place
    /// falls back (a region to the bare world, an interior to a door that says why).</summary>
    private async Task LoadStageMapsAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(Nav.BaseUri) };
            var manifest = await System.Net.Http.Json.HttpClientJsonExtensions
                .GetFromJsonAsync<MapManifest>(http, "Maps/manifest.json");
            foreach (var (id, path) in manifest?.Maps ?? [])
            {
                var xml = await http.GetStringAsync(path);
                // The dev server answers a missing asset with the SPA's index.html,
                // so anything that is not a map document is a manifest line whose
                // file is absent — say so, rather than quietly playing without it.
                if (xml.TrimStart().StartsWith("<?xml", StringComparison.Ordinal))
                    Game.Tiled.MapCatalog.Register(id, xml);
                else
                    Console.Error.WriteLine($"map '{id}': '{path}' did not return a map document");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"map manifest not loaded, using the fallbacks: {ex.Message}");
        }
    }

    /// <summary>The shape of Maps/manifest.json: id → path under wwwroot.</summary>
    private sealed class MapManifest
    {
        public Dictionary<string, string> Maps { get; set; } = [];
    }
}
