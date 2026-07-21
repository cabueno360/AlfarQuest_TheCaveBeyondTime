using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Services.Character;

/// <summary>What the character window remembers between openings.
///
/// A service rather than component state, because Blazor destroys a component's
/// fields when it stops rendering: without this, closing the window mid-fight and
/// reopening it would reset the tab, clear a typed search and lose the item being
/// compared. The brief asks for exactly that not to happen.
///
/// Deliberately holds only UI state. Nothing here can change a character — a
/// service that remembered "selected item" *and* could equip it would be two
/// responsibilities wearing one name.</summary>
public sealed class CharacterWindowState
{
    private string _tab = CharacterTab.Default;

    /// <summary>The open tab. Setting an unknown or unavailable key falls back to
    /// the default rather than blanking the window — a stale key from an older
    /// build should not leave someone staring at nothing.</summary>
    public string Tab
    {
        get => _tab;
        set => _tab = CharacterTab.Find(value) is { Available: true } ? value : CharacterTab.Default;
    }

    // ---- inventory ----------------------------------------------------
    public string Search { get; set; } = "";
    public ItemCategory Filter { get; set; } = ItemCategory.All;
    public InventoryView View { get; set; } = InventoryView.Grid;
    public InventorySortOrder Sort { get; set; } = InventorySortOrder.Rarity;

    /// <summary>The item whose details are pinned open. Held by id rather than by
    /// reference so it survives the pack being rebuilt by a save restore.</summary>
    public string? SelectedItemId { get; set; }

    // ---- skills -------------------------------------------------------
    public string? SelectedSkillId { get; set; }

    public event Action? Changed;

    /// <summary>Raised when something inside the window asks to close it.
    ///
    /// The search box needs this: it swallows keys so the caret can be moved, and
    /// that would otherwise make Escape — the way out of the window — do nothing
    /// while a player is typing. Routed through here rather than drilled down as a
    /// callback, because this object already mediates between the strip, the
    /// toolbar and the shell.</summary>
    public event Action? CloseRequested;

    public void RequestClose() => CloseRequested?.Invoke();

    /// <summary>Announces a change so the window re-renders. Called by the tab
    /// strip and the toolbar; the panels themselves only read.</summary>
    public void Notify() => Changed?.Invoke();

    public void GoTo(string tab)
    {
        if (Tab == tab) return;
        Tab = tab;
        Notify();
    }
}

public enum InventoryView { Grid, Compact }
