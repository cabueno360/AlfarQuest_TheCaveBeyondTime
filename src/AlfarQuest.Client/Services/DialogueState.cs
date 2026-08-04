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
public sealed class DialogueState(PartyState party)
{
    private readonly PartyState _party = party;

    /// <summary>The open conversation, resolved from the NPC so the window can draw
    /// a header without reaching back for anything. The QUESTIONS are not snapshot
    /// here — see <see cref="VisibleTopics"/>, which refilters live.</summary>
    public sealed record OpenTalk(string Name, string Role, string Kind, string Greeting, NpcDefinition Npc);

    public OpenTalk? Open { get; private set; }

    /// <summary>The questions currently on the table, refiltered against the flag
    /// set EVERY read — so a topic unlocked by an answer given this conversation
    /// (Halvard naming the Cave unlocks the ones gated on knowing of it) appears
    /// the moment the player is back at the menu, not on the next visit.</summary>
    public IReadOnlyList<DialogueTopic> VisibleTopics =>
        Open is null ? [] : [.. Open.Npc.Topics.Where(t => t.Visible(_party.ClaimedRewards))];

    /// <summary>The question being answered, or null while the menu of questions is
    /// showing. <see cref="Page"/> is which leaf of a multi-page answer is up.</summary>
    public DialogueTopic? Asked { get; private set; }
    public int Page { get; private set; }

    /// <summary>The quest currently being OFFERED — set when an offer topic is asked,
    /// cleared on Accept or Later. While it is set the conversation shows the offer
    /// panel (its terms and reward) instead of an answer, and only Accept starts it.</summary>
    public QuestDef? Offer { get; private set; }

    /// <summary>Which questions have already been put to them this conversation, so
    /// the menu can mark them as asked — a small nudge toward the ones left.</summary>
    private readonly HashSet<string> _heard = [];
    public bool AlreadyAsked(DialogueTopic t) => _heard.Contains(t.Q);

    /// <summary>Persuasions that failed, and when each may be pressed again.
    /// Keyed by the question; real time, because a conversation is real time.</summary>
    private readonly Dictionary<string, DateTime> _rebuffed = [];
    public bool Rebuffed(DialogueTopic t) =>
        _rebuffed.TryGetValue(t.Q, out var until) && until > DateTime.UtcNow;

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
            Open = new OpenTalk(npc.Name, npc.Role, npc.Kind, greeting, npc);
            Asked = null; Page = 0; Offer = null; _heard.Clear();
            Changed?.Invoke();
            return true;
        };
    }

    /// <summary>Puts a question to them — shows its first answer page and remembers
    /// it was asked. A PERSUASION rolls first: d20 + the party's best Wisdom
    /// against the topic's DC. Winning answers properly and claims the flag;
    /// losing answers with the rebuff and locks the pressing for ten minutes.
    /// Returns the throw for the dice overlay, or null when no die was cast.</summary>
    public object? Ask(DialogueTopic t)
    {
        _heard.Add(t.Q); Page = 0;

        // A persuasion already WON stays won: its flag is claimed, and clicking
        // the topic again just repeats the answer — the fates are not asked to
        // re-judge a case they closed.
        if (t.Persuade > 0 && (t.SetsFlag.Length == 0 || !_party.ClaimedRewards.Contains(t.SetsFlag)))
        {
            if (Rebuffed(t))
            {
                // Still smarting from the last press: one curt line, no roll.
                Asked = t with { A = ["They have heard enough of your pressing for now. Come back with a cooler tongue."] };
                Changed?.Invoke();
                return null;
            }

            var mod = _party.Members.Count == 0 ? 0
                : _party.Members.Max(m => Math.Clamp((m.Total.Wisdom - 10) / 2, 0, 5));
            var roll = Random.Shared.Next(1, 21);
            var total = roll + mod;
            var won = total >= t.Persuade;

            if (won)
            {
                Asked = t;
                if (t.SetsFlag.Length > 0) _party.ClaimFlag(t.SetsFlag);
            }
            else
            {
                Asked = t with { A = t.FailA.Length > 0 ? t.FailA : ["They look at you a long moment, and say nothing at all."] };
                _rebuffed[t.Q] = DateTime.UtcNow.AddMinutes(10);
            }
            Changed?.Invoke();
            return new { sides = 20, value = roll, mod, total, kind = "persuade", c = "#e0c66b", outcome = won ? "good" : "bad" };
        }

        Asked = t;
        // A plain hint-topic sets its flag now. An OFFER topic holds off: its pitch
        // (the answer) plays first, and the panel with the terms comes only once it
        // is read — see Next() — so the player hears them out before deciding.
        if (t.OffersQuest.Length == 0 && t.SetsFlag.Length > 0)
            _party.ClaimFlag(t.SetsFlag);
        Changed?.Invoke();
        return null;
    }

    /// <summary>Whether asking this topic to its end will lay a quest on the table —
    /// it offers one, and that one is not already underway or done.</summary>
    public bool WillOffer(DialogueTopic t) =>
        t.OffersQuest.Length > 0
        && QuestCatalog.Find(t.OffersQuest) is { } q
        && !q.IsStarted(_party.ClaimedRewards);

    /// <summary>Accept the offered quest — NOW its start flag is set, and it appears
    /// in the journal. Back to the question list afterwards.</summary>
    public void AcceptOffer()
    {
        if (Offer is { StartFlag.Length: > 0 } quest) _party.ClaimFlag(quest.StartFlag);
        Offer = null; Asked = null; Page = 0;
        Changed?.Invoke();
    }

    /// <summary>Leave the offer on the table — nothing is set, so the topic can offer
    /// it again next time. Back to the question list.</summary>
    public void DeclineOffer()
    {
        Offer = null; Asked = null; Page = 0;
        Changed?.Invoke();
    }

    /// <summary>Turns to the next page of the current answer, or back to the list of
    /// questions once the answer is done.</summary>
    public void Next()
    {
        if (Asked is null) return;
        if (Page + 1 < Asked.A.Length) { Page++; Changed?.Invoke(); return; }

        // The answer is done. If it was a quest offer not yet taken, lay its terms
        // out now — the pitch has been heard, so the choice comes next. Otherwise
        // back to the list of questions.
        if (WillOffer(Asked)) Offer = QuestCatalog.Find(Asked.OffersQuest);
        else { Asked = null; Page = 0; }
        Changed?.Invoke();
    }

    /// <summary>Abandons the current answer and returns to the questions.</summary>
    public void BackToMenu() { Asked = null; Page = 0; Changed?.Invoke(); }

    public void Close()
    {
        if (Open is null) return;
        Open = null; Asked = null; Page = 0; Offer = null; _heard.Clear();
        DialogueBridge.Close();      // release the hero the engine was holding
        Changed?.Invoke();
    }
}
