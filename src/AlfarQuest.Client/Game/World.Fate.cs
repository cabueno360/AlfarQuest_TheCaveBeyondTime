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

    /// <summary>Locks that took a failed pick badly, and how long each sulks.</summary>
    readonly Dictionary<Interactable, float> _jams = [];

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

    /// <summary>Seconds this lock still refuses the pick, or zero.</summary>
    float JamRemaining(Interactable thing) => _jams.GetValueOrDefault(thing);

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
