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

    /// <summary>What the prompt says you would be doing. Reads from the source so
    /// a new kind of interactable cannot ship with the wrong verb.</summary>
    static string VerbFor(Interactable t) => t.Source switch
    {
        XpSource.TreasureChest => "Open",
        XpSource.OreVein => "Mine",
        XpSource.RareCrystal => "Prise",
        XpSource.AncientTablet => "Read",
        XpSource.Relic => "Take",
        _ => "Use",
    };

    // ---- awarding ---------------------------------------------------

    /// <summary>Pays XP, floats the text, and celebrates if it levelled anyone.
    /// Every reward in the game funnels through here, so the feedback is
    /// identical whatever earned it.</summary>
    void Award(int xp, XpSource source, Vec at, string? label = null)
    {
        var award = XpAward.For(source);
        var lead = Party.Count > 0 && Active < Party.Count ? Party[Active].Def.Key : "";
        var levels = RewardBridge.Grant(xp, source, lead);

        Floaters.Add(new FloatText(at, label ?? award.Label, award.Colour));
        Floaters.Add(new FloatText(at + new Vec(0, -16), $"+{xp} XP", "#cfe8ff"));

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
        }

        ThingInReach = null;
        var best = ReachRange;
        foreach (var t in Interactables)
        {
            if (t.Used) continue;
            var d = (t.Pos - hero.Pos).Len();
            if (d < best) { best = d; ThingInReach = t; }
        }
    }

    /// <summary>Uses whatever is in reach. Returns false when there was nothing,
    /// so the caller can fall through to talking to an NPC.</summary>
    bool UseThingInReach()
    {
        if (ThingInReach is not { } thing) return false;

        thing.Used = true;
        Claim(thing.Name);
        Award(XpAward.Value(thing.Source), thing.Source, thing.Pos, thing.Name);

        if (thing.Loot is { } item)
        {
            LootBridge.Drop(item, thing.LootCount);
            Floaters.Add(new FloatText(thing.Pos + new Vec(0, -32), $"+{thing.LootCount} {item}", "#9fe4ff"));
        }

        Burst(thing.Pos, "#f0d99a", 12);
        return true;
    }

    /// <summary>Records a one-shot reward as taken, for Stage 1 only.
    ///
    /// The overworld is a fixed place: a chest opened there stays open across
    /// sessions. The cave is regenerated on every descent, so persisting its
    /// claims would silently empty a brand-new cave — and its names repeat, which
    /// would make the two collide.</summary>
    void Claim(string key)
    {
        if (Stage == 1) RewardBridge.Claim(key);
    }

    /// <summary>Marks everything this player has already taken, before the world
    /// is first shown. Called at the end of the overworld build.</summary>
    void ApplyClaims()
    {
        var claimed = RewardBridge.Claimed();
        if (claimed.Count == 0) return;

        foreach (var d in Discoveries) if (claimed.Contains(d.Name)) d.Found = true;
        foreach (var t in Interactables) if (claimed.Contains(t.Name)) t.Used = true;
    }

    /// <summary>XP for a kill. Separate from the table because the value belongs
    /// to the species, and because a mini-boss pays the headline rate.</summary>
    void AwardKill(Husk k)
    {
        var (source, xp) = k.Def.MiniBoss
            ? (XpSource.MiniBoss, Math.Max(k.Def.Xp, XpAward.Value(XpSource.MiniBoss)))
            : (XpSource.Kill, k.Def.Xp);

        Award(xp, source, k.Pos, k.Def.MiniBoss ? XpAward.For(XpSource.MiniBoss).Label : "");
    }
}
