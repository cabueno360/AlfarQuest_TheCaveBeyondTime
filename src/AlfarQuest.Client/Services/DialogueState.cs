using AlfarQuest.Client.Game;

namespace AlfarQuest.Client.Services;

/// <summary>The conversation the player currently has open — the question menu and
/// the answer they are reading.
///
/// The talking side of <see cref="ShopState"/>: the engine offers an NPC who
/// answers a menu of questions, this opens, and the whole exchange (questions and
/// their answers) is resolved here in the browser — only opening and closing cross
/// back to the engine, which holds the hero still meanwhile. An NPC with no topics
/// never reaches here; they cycle their one-line balloon on the canvas instead.</summary>
public sealed class DialogueState
{
    /// <summary>The open conversation, resolved from the NPC so the window can draw a
    /// header and its list of questions without reaching back for anything.</summary>
    public sealed record OpenTalk(string Name, string Role, string Kind, string Greeting, IReadOnlyList<DialogueTopic> Topics);

    public OpenTalk? Open { get; private set; }

    /// <summary>The question being answered, or null while the menu of questions is
    /// showing. <see cref="Page"/> is which leaf of a multi-page answer is up.</summary>
    public DialogueTopic? Asked { get; private set; }
    public int Page { get; private set; }

    /// <summary>Which questions have already been put to them this conversation, so
    /// the menu can mark them as asked — a small nudge toward the ones left.</summary>
    private readonly HashSet<string> _heard = [];
    public bool AlreadyAsked(DialogueTopic t) => _heard.Contains(t.Q);

    public event Action? Changed;

    /// <summary>Registers with the engine. The offer carries only which NPC — the
    /// questions and answers are static data pulled straight from the catalogue.</summary>
    public void Attach()
    {
        DialogueBridge.OnOpen = npcId =>
        {
            if (NpcCatalog.Find(npcId) is not { } npc || !npc.HasDialogue)
                return false;   // not someone with a question menu — decline
            var greeting = string.IsNullOrEmpty(npc.Greeting)
                ? (npc.Lines.Count > 0 ? npc.Lines[0] : "")
                : npc.Greeting;
            Open = new OpenTalk(npc.Name, npc.Role, npc.Kind, greeting, npc.Topics);
            Asked = null; Page = 0; _heard.Clear();
            Changed?.Invoke();
            return true;
        };
    }

    /// <summary>Puts a question to them — shows its first answer page and remembers
    /// it was asked.</summary>
    public void Ask(DialogueTopic t)
    {
        Asked = t; Page = 0; _heard.Add(t.Q);
        Changed?.Invoke();
    }

    /// <summary>Turns to the next page of the current answer, or back to the list of
    /// questions once the answer is done.</summary>
    public void Next()
    {
        if (Asked is null) return;
        if (Page + 1 < Asked.A.Length) Page++;
        else { Asked = null; Page = 0; }
        Changed?.Invoke();
    }

    /// <summary>Abandons the current answer and returns to the questions.</summary>
    public void BackToMenu() { Asked = null; Page = 0; Changed?.Invoke(); }

    public void Close()
    {
        if (Open is null) return;
        Open = null; Asked = null; Page = 0; _heard.Clear();
        DialogueBridge.Close();      // release the hero the engine was holding
        Changed?.Invoke();
    }
}
