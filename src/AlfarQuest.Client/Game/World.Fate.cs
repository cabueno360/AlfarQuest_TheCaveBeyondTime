using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  The fate dice.
//
//  At the moments the game consults the fates — an ultimate unleashed, a
//  chest's fortune — a d20 is rolled HERE, applied on the spot, and a 3D
//  die is thrown across the screen to land on that same number (see
//  wwwroot/js/dice.js). The simulation never waits on physics: the die
//  is ceremony, the engine's roll is the truth, and they always agree.
// =====================================================================
public partial class World
{
    /// <summary>The frame's fate rolls, copied into the render payload the same
    /// way sounds are and emptied at the top of the next tick.</summary>
    readonly List<RDice> _dice = [];

    /// <summary>A chest whose fortune is still tumbling across the screen. The
    /// loot window waits for the die so the reveal lands with the number —
    /// the contents were decided the moment it was opened.</summary>
    Interactable? _pendingReveal;
    float _revealIn;

    /// <summary>Seconds the reveal waits for the die to settle.</summary>
    const float RevealDelay = 2.1f;

    /// <summary>Seconds a failed fate check locks its attempt out — the ten
    /// minutes the design asks for. Real time, but only while the world runs:
    /// menus do not count the wait down.</summary>
    const float FateLockout = 600f;

    /// <summary>The summoned light — the Mage's little friend. Whether she is
    /// with the party this session; the cave will not have them without her.</summary>
    public bool LightOrb { get; private set; }
    float _summonLockout;
    float _pendingDescent;

    /// <summary>Things that took a failed check badly — a jammed lock, letters
    /// that would not settle — and how long each sulks. Keyed on the thing
    /// itself, whatever kind of thing it is.</summary>
    readonly Dictionary<object, float> _jams = [];

    /// <summary>Warded texts already read true — a check passes once, ever.</summary>
    readonly HashSet<Examinable> _deciphered = [];
    Examinable? _pendingRead;
    float _readIn;

    /// <summary>The light-summoning at the mouth. With the orb already called,
    /// straight down; with the weave still torn from a failed call, a refusal
    /// and the time left; otherwise the Mage (or whoever leads, in a party
    /// without one) rolls d20 + their prime attribute against 12 — success
    /// descends when the die settles, failure holds the door for ten minutes.</summary>
    void TryDescend()
    {
        const int SummonDC = 12;

        if (LightOrb) { EnterCave(); return; }
        if (_pendingDescent > 0) return;                 // the die is still tumbling

        var caster = Party.FirstOrDefault(h => h.Def.Key == "mage") ?? Party[Active];
        if (_summonLockout > 0)
        {
            Floaters.Add(new FloatText(caster.Pos + new Vec(0, -46),
                "The weave is still torn — {0} min", "#9a95b6",
                MathF.Ceiling(_summonLockout / 60f).ToString()));
            return;
        }

        var d = RollFate("summon", 20, CharacterStats.For(caster.Def.Key).FateMod, FateColour(caster.Def.HeroClass));
        if (d.total >= SummonDC)
        {
            d.outcome = "good";
            LightOrb = true;
            Floaters.Add(new FloatText(caster.Pos + new Vec(0, -46), "A small light answers the call", "#f0d99a"));
            _pendingDescent = RevealDelay;               // descend when the die lands
        }
        else
        {
            d.outcome = "bad";
            _summonLockout = FateLockout;
            Floaters.Add(new FloatText(caster.Pos + new Vec(0, -46), "The light gutters — the dark holds the door", "#d98a8a"));
        }
    }

    /// <summary>Seconds this thing still refuses another try, or zero.</summary>
    float JamRemaining(object thing) => _jams.GetValueOrDefault(thing);

    /// <summary>A warded text: the party's best mind rolls d20 + Intelligence
    /// against the text's own DC. Success opens the page when the die settles
    /// and pays a Puzzle's XP; failure locks the letters for ten minutes — and
    /// a text with teeth (FateBite) takes its due out of whoever leads.</summary>
    void TryDecipher(Examinable e)
    {
        if (_pendingRead is not null) return;            // a die is already tumbling
        if (JamRemaining(e) is > 0 and var left)
        {
            Floaters.Add(new FloatText(e.Pos + new Vec(0, -34),
                "The letters still swim — {0} min", "#9a95b6",
                MathF.Ceiling(left / 60f).ToString()));
            return;
        }

        // The best mind present does the reading, whoever is steered.
        var reader = Party.OrderByDescending(h => CharacterStats.For(h.Def.Key).IntMod).First();
        var d = RollFate("lore", 20, CharacterStats.For(reader.Def.Key).IntMod, FateColour(reader.Def.HeroClass));

        if (d.total >= e.CheckDC)
        {
            d.outcome = "good";
            _deciphered.Add(e);
            Floaters.Add(new FloatText(e.Pos + new Vec(0, -34), "The letters settle into sense", "#f0d99a"));
            Award(40, XpSource.Puzzle, e.Pos);
            _pendingRead = e;
            _readIn = RevealDelay;                       // the page opens with the die
        }
        else
        {
            d.outcome = "bad";
            _jams[e] = FateLockout;
            Floaters.Add(new FloatText(e.Pos + new Vec(0, -34), "The letters swim before the eyes", "#9a95b6"));
            if (e.CheckBite > 0 && Party.Count > 0)
            {
                var h = Party[Active];
                h.Hp = Math.Max(1, h.Hp - e.CheckBite);  // a ward stings; it does not kill
                Floaters.Add(new FloatText(h.Pos + new Vec(0, -26), "-{0}", "#d98a8a", e.CheckBite.ToString()));
                Play("hit_crystal", h.Pos);
                PlaySound("magic_frost", h.Pos, 0.7f);
            }
        }
    }

    /// <summary>Rolls a die, applies nothing, hides nothing: the caller applies
    /// the outcome (and stamps it on the returned record, so the screen's plaque
    /// can say Success or Failure) — this only throws and remembers.</summary>
    RDice RollFate(string kind, int sides, int mod, string colour)
    {
        var roll = _rng.Next(1, sides + 1);
        var d = new RDice { sides = sides, value = roll, mod = mod, total = roll + mod, kind = kind, c = colour };
        _dice.Add(d);
        return d;
    }

    /// <summary>The die a hero throws wears their class's colour.</summary>
    static string FateColour(string heroClass) => heroClass switch
    {
        "Mage" => "#7b6be0",
        "Thief" => "#4f9e64",
        "Cleric" => "#e0c66b",
        "Warrior" => "#c05b4d",
        _ => "#c9a227",
    };

    /// <summary>Counts the fate timers down: the pending chest reveal, the
    /// pending descent, the summoning lockout, the sulking locks. Called each
    /// tick, so none of them move while a menu holds the world.</summary>
    void UpdateFate(float dt)
    {
        if (_summonLockout > 0) _summonLockout -= dt;
        foreach (var key in _jams.Keys.ToList())
        {
            _jams[key] -= dt;
            if (_jams[key] <= 0) _jams.Remove(key);
        }

        if (_pendingDescent > 0)
        {
            _pendingDescent -= dt;
            if (_pendingDescent <= 0) EnterCave();
        }

        if (_pendingRead is { } text)
        {
            _readIn -= dt;
            if (_readIn <= 0) { _pendingRead = null; OpenReadingNow(text); }
        }

        if (_pendingReveal is not { } thing) return;
        _revealIn -= dt;
        if (_revealIn > 0) return;
        _pendingReveal = null;
        Reveal(thing);
    }

    /// <summary>The half of opening a container that the player SEES — effect,
    /// empty-words or the loot window. Split from the roll so a chest can wait
    /// for its fortune die while a barrel opens on the spot.</summary>
    void Reveal(Interactable thing)
    {
        Play(EffectFor(thing.Kind), thing.Pos);

        var contents = thing.Contents!;
        if (contents.IsEmpty)
        {
            Floaters.Add(new FloatText(thing.Pos + new Vec(0, -32), thing.Kind.WhenEmpty, "#9a95b6"));
            return;
        }

        if (!ContainerBridge.Offer(new OpenedContainer(thing.Name, thing.Kind, contents, SteeredKey)))
            TakeEverything(thing);
    }
}
