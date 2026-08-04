using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Experience: what pays, and when.
//
//  Two kinds of reward, because they answer different questions.
//  A Discovery pays for *being* somewhere — it fires the moment the
//  player walks in, which is what makes exploring feel noticed. An
//  Interactable pays for *doing* something and needs [E], so the player
//  gets the credit rather than the pathfinding.
//
//  Both are one-shot: a chest reopened for 25 XP would turn a world
//  into a treadmill.
// =====================================================================
public partial class World
{
    public List<Discovery> Discoveries { get; } = [];
    public List<Interactable> Interactables { get; } = [];

    /// <summary>The nearest untouched interactable within reach, or null. Drives
    /// both the [E] prompt and what Interact() acts on, so the two can never
    /// disagree about what is being offered.</summary>
    public Interactable? ThingInReach { get; private set; }

    /// <summary>Set for a moment after a level-up so the HUD can celebrate. The
    /// engine does not know what a level is — it only relays what the party sheet
    /// told it when the XP landed.</summary>
    public float LevelUpGlow { get; private set; }

    const float ReachRange = 46f;

    /// <summary>The steered hero's progression, for the HUD. Falls back to level 1
    /// when the sheet has not published one — a test running the engine alone
    /// still draws a sensible bar rather than dividing by zero.</summary>
    HeroProgress HeroProgressNow =>
        Party.Count == 0 || Active >= Party.Count
            ? HeroProgress.None
            : CharacterStats.ProgressOf(Party[Active].Def.Key);

    /// <summary>What the prompt says you would be doing.
    ///
    /// Read off the kind rather than switched on here, so a new container carries
    /// its own verb and cannot ship with the wrong one. A locked container says
    /// so before it is opened — the prompt is where "what do I need" gets asked.</summary>
    static string VerbFor(Interactable t) =>
        t.Kind.RequiresKey is not null && !t.Opened ? "Unlock" : t.Kind.Verb;

    // ---- awarding ---------------------------------------------------

    /// <summary>The hero who earns a reward that has no striker — a discovery
    /// walked into, a chest opened. Whoever is being steered gets it, because they
    /// are the one who did the thing.</summary>
    string SteeredKey => Party.Count > 0 && Active < Party.Count ? Party[Active].Def.Key : "";

    /// <summary>Pays XP to the steered hero. For loot and discoveries, whose owner
    /// is simply whoever picked them up.</summary>
    void Award(int xp, XpSource source, Vec at, string? label = null)
        => AwardTo(SteeredKey, xp, source, at, label);

    /// <summary>Pays XP to one named hero, floats the text, and celebrates if it
    /// levelled them. Every reward funnels through here, so the feedback is
    /// identical whatever earned it — only who is credited changes.</summary>
    void AwardTo(string heroKey, int xp, XpSource source, Vec at, string? label = null)
    {
        var award = XpAward.For(source);
        var levels = RewardBridge.Grant(xp, source, heroKey);

        Floaters.Add(new FloatText(at, label ?? award.Label, award.Colour));
        Floaters.Add(new FloatText(at + new Vec(0, -16), "+{0} XP", "#cfe8ff", xp.ToString()));

        if (levels > 0) CelebrateLevelUp();
    }

    /// <summary>The level-up moment, as the world sees it: light, a shove of the
    /// camera, and words over the hero. The pause and the window are the UI's
    /// business.</summary>
    void CelebrateLevelUp()
    {
        LevelUpGlow = 1.4f;
        Shake = Math.Max(Shake, 0.7f);

        if (Party.Count == 0 || Active >= Party.Count) return;
        var hero = Party[Active];

        // A ring rather than a puff: it reads as something rising out of the
        // hero instead of an explosion landing on them.
        for (int i = 0; i < 26; i++)
        {
            var angle = i / 26f * MathF.Tau;
            var speed = 70f + (float)_rng.NextDouble() * 40f;
            Fx.Add(new Particle(
                hero.Pos + new Vec(MathF.Cos(angle) * 10, MathF.Sin(angle) * 6),
                new Vec(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed * 0.5f - 40f),
                i % 3 == 0 ? "#f0d99a" : "#e7ccff",
                0.9f + (float)_rng.NextDouble() * 0.4f));
        }

        Floaters.Add(new FloatText(hero.Pos + new Vec(0, -34), "LEVEL UP!", "#f0d99a"));
    }

    // ---- per-frame --------------------------------------------------

    void UpdateRewards(float dt)
    {
        LevelUpGlow = Math.Max(0, LevelUpGlow - dt);

        if (Party.Count == 0 || Active >= Party.Count) { ThingInReach = null; return; }
        var hero = Party[Active];

        // Discoveries pay on arrival. Only the steered hero counts — a companion
        // wandering into a secret should not spend the player's moment.
        foreach (var d in Discoveries)
        {
            if (d.Found || (d.Pos - hero.Pos).Len() > d.Radius) continue;
            d.Found = true;
            Claim(d.Name);
            Award(XpAward.Value(d.Source), d.Source, hero.Pos, d.Name);

            // The boss chamber is a story milestone, not cave loot. Ordinary cave
            // claims are deliberately forgotten each descent (Claim() only persists
            // in Stage 1, so the regenerating cave stays full) — but the Pact's
            // "find the Crystal Heart" step has to STICK once reached, so it rides a
            // dedicated flag persisted straight through the bridge. Once-ever.
            if (Stage == 2 && d.Name == "The Crystal Heart")
                RewardBridge.Claim("cave_heart");
        }

        ThingInReach = null;
        var best = ReachRange;
        var now = DateTime.UtcNow;
        foreach (var t in Interactables)
        {
            // Checked here rather than only at world build, so a barrel refills
            // during a long session instead of only between them.
            if (t.DueToRefill(now)) t.Refill();

            if (!t.Offers) continue;
            var d = (t.Pos - hero.Pos).Len();
            if (d < best) { best = d; ThingInReach = t; }
        }
    }

    /// <summary>Uses whatever is in reach. Returns false when there was nothing,
    /// so the caller can fall through to talking to an NPC.</summary>
    bool UseThingInReach()
    {
        if (ThingInReach is not { } thing) return false;
        // One fortune at a time: a die is still tumbling for the last chest.
        if (_pendingReveal is not null) return true;

        // A lock is checked before anything else happens: no XP, no particles and
        // no "opened" flag for a chest that did not open. Without the key, the
        // Thief (or whoever leads) can try the pick: d20 + their prime attribute
        // against 14. Success opens without spending anything; failure jams the
        // mechanism for ten minutes and the box sits there smug about it.
        var picked = false;
        if (thing.Kind.RequiresKey is { } key && !thing.Opened)
        {
            if (!ContainerBridge.CarryingKey(key))
            {
                const int PickDC = 14;
                if (JamRemaining(thing) is > 0 and var left)
                {
                    Floaters.Add(new FloatText(thing.Pos + new Vec(0, -30),
                                               "The lock is jammed — {0} min", "#9a95b6",
                                               MathF.Ceiling(left / 60f).ToString()));
                    return true;
                }
                var picker = Party.FirstOrDefault(h => h.Def.Key == "thief") ?? Party[Active];
                var pick = RollFate("pick", 20, CharacterStats.For(picker.Def.Key).FateMod, FateColour(picker.Def.HeroClass));
                if (pick.total >= PickDC)
                {
                    pick.outcome = "good";
                    picked = true;
                    Floaters.Add(new FloatText(thing.Pos + new Vec(0, -46), "The lock yields to the pick", "#f0d99a"));
                }
                else
                {
                    pick.outcome = "bad";
                    _jams[thing] = FateLockout;
                    Floaters.Add(new FloatText(thing.Pos + new Vec(0, -46), "The pick slips — the lock jams", "#d98a8a"));
                    return true;
                }
            }
            else
            {
                ContainerBridge.SpendKey(key);
                Floaters.Add(new FloatText(thing.Pos + new Vec(0, -46), "{0} turns", "#f0d99a", key));
            }
        }

        // First opening rolls it; later ones show whatever is still inside.
        if (!thing.Opened)
        {
            var (luck, _) = CharacterStats.PartyFortune?.Invoke() ?? (0f, 0f);

            // A CHEST consults the fates: a d6 plus the opener's Luck, tumbled
            // at the centre of the screen. The number sweetens (or sours) the
            // loot table's chances, and the reveal waits for the die to settle
            // — barrels, ore and herb patches stay quick and quiet. A lock that
            // just yielded to the pick skips the fortune die: that throw was
            // the pick's, and two dice for one box is a casino.
            var fated = !picked && thing.Kind.Verb is "Open" or "Unlock";
            if (fated)
            {
                var d = RollFate("fortune", 6, CharacterStats.For(SteeredKey).LuckMod, "#c9a227");
                luck = MathF.Max(0f, luck + (d.total - 3.5f) * 0.12f);
                d.outcome = d.total >= 6 ? "good" : d.total <= 2 ? "bad" : "plain";
                if (d.outcome == "good")
                    Floaters.Add(new FloatText(thing.Pos + new Vec(0, -46), "The fates smile — {0}!", "#f0d99a", d.total.ToString()));
                else if (d.outcome == "bad")
                    Floaters.Add(new FloatText(thing.Pos + new Vec(0, -46), "The fates look away — {0}", "#9a95b6", d.total.ToString()));
            }

            thing.Contents = thing.Kind.Loot.Roll(_rng.NextDouble, luck);
            thing.Opened = true;
            thing.OpenedAt = DateTime.UtcNow;

            Claim(thing.Name);
            Record(thing);
            StatBridge.Record(SteeredKey, HeroStats.Kind.TreasuresOpened);
            Award(XpAward.Value(thing.Kind.Xp), thing.Kind.Xp, thing.Pos, thing.Name);

            // The window opens when the die lands — see UpdateFate. A picked
            // lock waits on the pick's own die the same way.
            if (fated || picked) { _pendingReveal = thing; _revealIn = RevealDelay; return true; }
        }

        Reveal(thing);
        return true;
    }

    /// <summary>Hands a container's whole contents to the party. The fallback for
    /// a headless run, and what the interface calls for "Take All".</summary>
    void TakeEverything(Interactable thing)
    {
        if (thing.Contents is not { } c) return;

        var owner = SteeredKey;
        if (c.Coin > 0) LootBridge.Drop("Coins", c.Coin, owner);
        foreach (var (id, n) in c.Materials) LootBridge.Drop(id, n, owner);
        foreach (var id in c.Items) LootBridge.Drop(id, 1, owner);
        c.Clear();
        Record(thing);
    }

    /// <summary>Writes a container's state down — everywhere but the cave, which
    /// is regenerated on every descent, so its containers are meant to come back.
    /// Interiors count: a room's chest is as fixed a place as the field outside.</summary>
    void Record(Interactable thing)
    {
        if (Stage == 2 || thing.Contents is null || thing.OpenedAt is not { } at) return;
        RewardBridge.SaveContainer(ContainerSave.From(thing.Name, at, thing.Contents));
    }

    /// <summary>What opening this container looks like.
    ///
    /// Chosen from the verb and the tier rather than from the id, so a new kind
    /// of thing to mine gets rock fragments and a new rare chest gets the loud
    /// version without either being registered anywhere.</summary>
    static string EffectFor(ContainerKind kind) => kind.Verb switch
    {
        "Mine" or "Prise" => "mine",
        "Gather" => "gather",
        _ => kind.Tier >= Rarity.Rare ? "chest_rare" : "chest_open",
    };

    /// <summary>Records a one-shot reward as taken — everywhere but the cave.
    ///
    /// The overworld and the buildings on it are fixed places: a chest opened
    /// there stays open across sessions. The cave is regenerated on every
    /// descent, so persisting its claims would silently empty a brand-new cave —
    /// and its names repeat, which would make the two collide.</summary>
    void Claim(string key)
    {
        if (Stage != 2) RewardBridge.Claim(key);
    }

    /// <summary>Marks everything this player has already taken, before the world
    /// is first shown. Called at the end of the overworld build.</summary>
    void ApplyClaims()
    {
        var claimed = RewardBridge.Claimed();
        foreach (var d in Discoveries) if (claimed.Contains(d.Name)) d.Found = true;

        var saved = RewardBridge.Containers();
        var now = DateTime.UtcNow;

        foreach (var t in Interactables)
        {
            if (!saved.TryGetValue(t.Name, out var state)) continue;

            // Long enough closed to have refilled: left untouched, so it rolls
            // fresh the next time it is opened. This is also why the roll happens
            // on opening rather than at world build — a respawned barrel should
            // not hold what it held last time.
            if (state.HasRespawned(t.Kind, now)) continue;

            t.Opened = true;
            t.OpenedAt = state.OpenedAt;
            t.Contents = state.ToStack();
        }
    }

    /// <summary>XP for a kill. Separate from the table because the value belongs
    /// to the species, and because a mini-boss pays the headline rate.</summary>
    void AwardKill(Husk k)
    {
        var (source, xp) = k.Def.MiniBoss
            ? (XpSource.MiniBoss, Math.Max(k.Def.Xp, XpAward.Value(XpSource.MiniBoss)))
            : (XpSource.Kill, k.Def.Xp);

        // The killing blow decides it. A creature that died with no striker on
        // record — nothing does this yet, but a trap or a hazard would — pays the
        // steered hero rather than nobody.
        var killer = k.LastHitBy ?? SteeredKey;
        StatBridge.Record(killer, HeroStats.Kind.EnemiesDefeated);
        if (k.Def.MiniBoss) StatBridge.Record(killer, HeroStats.Kind.BossesDefeated);
        AwardTo(killer, xp, source, k.Pos, k.Def.MiniBoss ? XpAward.For(XpSource.MiniBoss).Label : "");
    }
}
