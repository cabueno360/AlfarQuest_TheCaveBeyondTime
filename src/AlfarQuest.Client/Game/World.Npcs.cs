namespace AlfarQuest.Client.Game;

// =====================================================================
//  Villagers and talking to them.
//
//  Interaction is a two-state machine on purpose: in range shows a prompt,
//  pressing the key opens a line, pressing it again closes. Nothing about
//  it assumes a single sentence, so a dialogue tree can replace
//  CurrentLine without touching the engine or the renderer.
// =====================================================================
public partial class World
{
    public List<Npc> Npcs { get; } = [];

    /// <summary>Whoever the active hero could talk to, or null.</summary>
    public Npc? NpcInReach { get; private set; }

    /// <summary>Whoever they are talking to, or null.</summary>
    public Npc? Talking { get; private set; }

    /// <summary>The merchant whose shop is open, or null. Separate from
    /// <see cref="Talking"/> because a shop is not a line of dialogue: the balloon
    /// stays shut and the window in the browser does the talking instead.</summary>
    public Npc? TradingWith { get; private set; }

    /// <summary>The NPC whose question menu is open, or null. Like a shop, this is a
    /// browser window rather than a canvas balloon — Mirka's Father and the like, who
    /// answer a list of questions rather than cycling one-liners.</summary>
    public Npc? Conversing { get; private set; }

    /// <summary>Talking holds the hero still — walking away mid-sentence would
    /// leave the balloon hanging over an empty patch of grass.</summary>
    public bool IsTalking => Talking is not null;

    /// <summary>A shop is open, which holds the hero exactly as talking does — you
    /// do not wander off from a counter mid-purchase.</summary>
    public bool IsTrading => TradingWith is not null;

    /// <summary>A question menu is open, holding the hero as a shop does.</summary>
    public bool InDialogue => Conversing is not null;

    /// <summary>Talking, trading, reading or in a question menu: any one pins the
    /// steered hero in place and suspends switching to another, so the party stays
    /// put at the counter, the bedside, or wherever it is reading.</summary>
    public bool Busy => IsTalking || IsTrading || IsReading || InDialogue;

    // -----------------------------------------------------------------
    //  A party member speaking — the Cleric joining at the mouth, word of
    //  Mirka delivered. The same balloon a villager talks in, but nothing
    //  is held and nothing is pressed: lines queue, each shows for its few
    //  seconds, and the game plays on underneath. A story beat, not a menu.
    // -----------------------------------------------------------------
    readonly Queue<(string Name, string Line, float Hold)> _speechQueue = new();
    (string Name, string Line)? _speechNow;
    float _speechFor;

    /// <summary>Queues one spoken line. Names and lines are English — the
    /// display side translates them, like every other string.</summary>
    public void Say(string name, string line, float hold = 5.5f) =>
        _speechQueue.Enqueue((name, line, hold));

    /// <summary>What the balloon should show right now, or null. Suppressed while
    /// an NPC window is up, so two balloons never fight over the same pixels.</summary>
    public (string Name, string Line)? SpeechNow => Busy ? null : _speechNow;

    void UpdateSpeech(float dt)
    {
        // Held while a window is open: a line must not tick away unseen behind
        // a shop or a conversation the player is reading.
        if (Busy) return;
        if (_speechNow is not null)
        {
            _speechFor -= dt;
            if (_speechFor > 0) return;
            _speechNow = null;
        }
        if (_speechQueue.Count > 0)
        {
            var (name, line, hold) = _speechQueue.Dequeue();
            _speechNow = (name, line);
            _speechFor = hold;
        }
    }

    /// <summary>Closes the shop from the engine's side, releasing the held hero.
    /// Wired to <see cref="MerchantBridge.OnClose"/> so the window closing in the
    /// browser and the hero being freed are the same event.</summary>
    public void CloseTrade() => TradingWith = null;

    /// <summary>Closes the question menu from the engine's side, releasing the held
    /// hero. Wired to <see cref="DialogueBridge.OnClose"/>.</summary>
    public void CloseConversation() => Conversing = null;

    /// <summary>Whether this NPC actually keeps a shop — the lore role says shop
    /// AND the trading catalogue has shelves for them. Both, so a role can be
    /// tagged for shop before its stock is written without the prompt lying.</summary>
    static bool IsMerchant(Npc n) =>
        n.Def.Services.HasFlag(NpcServices.Shop) && MerchantCatalog.Trades(n.Def.Id);

    const float TalkRange = 56f;

    Vec NearestOpen(Vec from)
    {
        for (float r = TILE; r <= TILE * 6; r += TILE)
            for (int a = 0; a < 12; a++)
            {
                var p = from + new Vec(MathF.Cos(a * MathF.Tau / 12) * r, MathF.Sin(a * MathF.Tau / 12) * r);
                if (!Blocked(p, 14f)) return p;
            }
        return from;
    }

    void UpdateNpcs()
    {
        if (Party.Count == 0) { NpcInReach = null; return; }
        var hero = Party[Active];

        NpcInReach = null;
        var best = TalkRange;
        foreach (var n in Npcs)
        {
            var d = (n.Pos - hero.Pos).Len();
            if (d < best) { best = d; NpcInReach = n; }
        }

        // Step out of range and the conversation ends by itself.
        if (Talking is not null && (Talking.Pos - hero.Pos).Len() > TalkRange * 1.6f) Talking = null;
    }

    /// <summary>What [E] does. One key, three meanings, resolved in the order a
    /// player would expect: finish the sentence you are reading, then use the
    /// thing at your feet, then greet whoever is standing there.</summary>
    public void Interact()
    {
        // The shop, the reading panel or the question menu owns the key while open;
        // pressing [E] again does nothing rather than poking at the world behind it.
        if (IsTrading || IsReading || InDialogue) return;

        if (Talking is not null)
        {
            Talking.LineIndex++;          // next visit gets the next line
            Talking = null;
            return;
        }

        // A doorway is the most deliberate target of all — you walked up to it to
        // go somewhere — so it comes before everything else.
        if (PortalInReach is { } door) { UsePortal(door); return; }

        // Then a thing to read — the journal, a letter — before a villager, since a
        // book on a table is a deliberate target and a person nearby is not.
        if (ExamineInReach is { } ex) { OpenReading(ex); return; }

        // Chests, seams and tablets take precedence over a nearby villager: if
        // both are in reach the player is almost certainly standing on the chest
        // deliberately, whereas the villager is scenery they walked past.
        if (UseThingInReach()) return;

        if (NpcInReach is null) return;

        // The story's one hand-off outranks anything else this NPC might do:
        // the Panacea, delivered at Mirka's bedside at last.
        if (TryWakeMirka(NpcInReach)) { FaceHero(NpcInReach); return; }

        // A shopkeeper opens their shop instead of a line of talk — but only if a
        // window is listening for it. In a headless run nothing answers the offer,
        // and they fall back to talking like any other villager, so the world still
        // behaves with no UI attached.
        if (IsMerchant(NpcInReach) &&
            MerchantBridge.Offer(NpcInReach.Def.Id, Party[Active].Def.Key))
        {
            TradingWith = NpcInReach;
            FaceHero(TradingWith);
            return;
        }

        // Someone who answers a menu of questions opens the dialogue window — but
        // only if a window is listening. Headless, the offer is refused and they
        // fall through to the one-line balloon like any other villager.
        if (NpcInReach.Def.HasDialogue && DialogueBridge.Offer(NpcInReach.Def.Id))
        {
            Conversing = NpcInReach;
            FaceHero(Conversing);
            return;
        }

        Talking = NpcInReach;
        FaceHero(Talking);
    }

    /// <summary>Turns an NPC to look at the steered hero, so a greeting or a sale
    /// is delivered face to face rather than to the back of their head.</summary>
    void FaceHero(Npc n)
    {
        var to = Party[Active].Pos - n.Pos;
        if (to.Len() > 0.01f) n.Facing = MathF.Atan2(to.Y, to.X);
    }
}
