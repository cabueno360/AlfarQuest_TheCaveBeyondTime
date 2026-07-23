using AlfarQuest.Client.Game;

namespace AlfarQuest.Client.Services;

/// <summary>The page of text the player currently has open — a journal entry, a
/// letter, the description of a thing they looked closely at.
///
/// The reading side of <see cref="LootState"/>: the engine offers some text, this
/// opens it, and turning pages is local — no coin changes hands and nothing in the
/// world moves, so unlike the shop this holds no game state, only which page.</summary>
public sealed class ReadState
{
    public OpenedReading? Open { get; private set; }
    public int Page { get; private set; }

    public event Action? Changed;

    public bool HasNext => Open is { } r && Page < r.Pages.Count - 1;
    public bool HasPrev => Page > 0;

    /// <summary>The current page's text, or empty when nothing is open.</summary>
    public string Text => Open is { } r && Page >= 0 && Page < r.Pages.Count ? r.Pages[Page] : "";

    public void Attach()
    {
        ReadBridge.OnOpen = reading =>
        {
            Open = reading;
            Page = 0;
            Changed?.Invoke();
            return true;
        };
    }

    public void Next() { if (HasNext) { Page++; Changed?.Invoke(); } }
    public void Prev() { if (HasPrev) { Page--; Changed?.Invoke(); } }

    public void Close()
    {
        if (Open is null) return;
        Open = null;
        Page = 0;
        ReadBridge.Close();     // release the hero the engine was holding
        Changed?.Invoke();
    }
}
