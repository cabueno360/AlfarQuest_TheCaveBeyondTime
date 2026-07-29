using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AlfarQuest.Client.Services.I18n;

/// <summary>Turns an English string into the player's language.
///
/// The English text is itself the key. That is the whole reason this could be
/// retrofitted onto a game already written: no key has to be invented for a line
/// that already exists, the code still reads as prose, and a line with no
/// translation yet shows in English instead of showing "ui.hero.title.long".
///
/// The table is a JSON file rather than a compiled resource so the same file
/// feeds the canvas layer (js/i18n.js) — the HUD and the Razor windows must
/// never disagree about what a skill is called.</summary>
public sealed class Translator(HttpClient http, IJSRuntime js, NavigationManager nav)
{
    private const string StorageKey = "alfarquest.language";

    private Dictionary<string, string> _table = new(StringComparer.Ordinal);

    public Language Current { get; private set; } = Language.English;

    /// <summary>The translated text, or the English given if there is none.</summary>
    public string this[string english] =>
        _table.TryGetValue(english, out var translated) && translated.Length > 0 ? translated : english;

    /// <summary>Translate, then fill in the placeholders — in that order, because a
    /// translation may put "{0}" somewhere else in the sentence than English does.</summary>
    public string Format(string english, params object?[] args) =>
        string.Format(this[english], args);

    /// <summary>Loads the stored choice and its table before the first render, so
    /// nobody sees a screen in English redraw itself into Portuguese.</summary>
    public async Task InitialiseAsync()
    {
        Current = Languages.Parse(await ReadStoredAsync());
        await LoadTableAsync();
        await ApplyToDocumentAsync();
    }

    /// <summary>Switches language and reloads.
    ///
    /// A reload rather than a re-render: every component in the app would
    /// otherwise have to subscribe to know its words changed, and a page that
    /// missed the memo would sit there in the old language. Changing language is
    /// rare and deliberate; a reload is a moment, and it is always right.</summary>
    public async Task SetAsync(Language language)
    {
        if (language == Current) return;
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, language.Code());
        nav.NavigateTo(nav.Uri, forceLoad: true);
    }

    private async Task<string?> ReadStoredAsync()
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(stored)) return stored;
            // Never chosen: follow the browser, which is the closest thing to
            // knowing what the player reads.
            return await js.InvokeAsync<string?>("eval", "navigator.language");
        }
        catch (JSException) { return null; }   // storage disabled — English, and the picker still works for the session
    }

    private async Task LoadTableAsync()
    {
        if (Current == Language.English) return;   // English is the source; there is nothing to load
        try
        {
            _table = await http.GetFromJsonAsync<Dictionary<string, string>>($"i18n/{Current.Code()}.json")
                     ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception)
        {
            // A missing or broken table must not stop the game starting. English
            // is always a working fallback, which is the point of keying by it.
            _table = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>Hands the table to the canvas layer and sets &lt;html lang&gt;, which is
    /// what a screen reader and the browser's own translation prompt read.
    ///
    /// The module is imported rather than reached through a global: js/i18n.js is
    /// the same module instance the renderer imports, so installing here is what
    /// the HUD reads.</summary>
    private async Task ApplyToDocumentAsync()
    {
        try
        {
            await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/i18n.js");
            await module.InvokeVoidAsync("install", Current.Code(), _table);
        }
        catch (JSException) { /* the canvas simply stays in English */ }
    }
}
