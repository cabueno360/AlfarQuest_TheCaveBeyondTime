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

    /// <summary>Seconds the party's pick is still chipped — a bare 1 on the
    /// harvest die. While it runs, nothing mines.</summary>
    float _pickChipFor;

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

    /// <summary>A climb whose die is still tumbling — the party jumps to the
    /// brink when it settles, like every other fated reveal.</summary>
    Portal? _pendingClimb;
    float _climbIn;

    /// <summary>A failed climb sulks for three minutes, not ten: wet holds dry,
    /// and a shortcut that locks for a delve's length is just a wall.</summary>
    const float ClimbLockout = 180f;

    /// <summary>The scar of handholds: the party's quickest hands roll d20 +
    /// Dexterity against the rock's DC. Success carries everyone to the brink
    /// when the die settles; failure is a short fall — a graze, and the holds
    /// refused for a while. The scar works both ways, so it is a true shortcut,
    /// never a one-way drop.</summary>
    void TryClimb(Portal p)
    {
        if (_pendingClimb is not null || p.Arrive is not { } top) return;
        if (JamRemaining(p) is > 0 and var left)
        {
            Floaters.Add(new FloatText(p.Pos + new Vec(0, -40),
                "The arms still remember the fall — {0} min", "#9a95b6",
                MathF.Ceiling(left / 60f).ToString()));
            return;
        }

        var climber = Party.OrderByDescending(h => CharacterStats.For(h.Def.Key).DexMod).First();
        var d = RollFate("climb", 20, CharacterStats.For(climber.Def.Key).DexMod, FateColour(climber.Def.HeroClass));

        if (d.total >= (p.CheckDC > 0 ? p.CheckDC : 12))
        {
            d.outcome = "good";
            Floaters.Add(new FloatText(p.Pos + new Vec(0, -40), "The holds are where they should be", "#f0d99a"));
            _pendingClimb = p;
            _climbIn = RevealDelay;                      // the party goes up with the die
        }
        else
        {
            d.outcome = "bad";
            _jams[p] = ClimbLockout;
            var h = Party[Active];
            var graze = (int)MathF.Round(h.MaxHp * 0.08f);
            h.Hp = MathF.Max(1f, h.Hp - graze);
            Floaters.Add(new FloatText(p.Pos + new Vec(0, -40), "Ten feet up, the holds run out", "#d98a8a"));
            Floaters.Add(new FloatText(h.Pos + new Vec(0, -26), "-{0}", "#d98a8a", graze.ToString()));
            PlaySound("hit", h.Pos, 0.7f);
        }
    }

    /// <summary>The boulder: the party's strongest back rolls d20 + Strength
    /// against the stone's DC. Success rolls it aside once and forever — the
    /// flag keeps it moved — and what it was sitting on is a find worth the
    /// shoulders. Failure burns them for a few minutes and moves nothing.</summary>
    void TryShove(Portal p)
    {
        if (JamRemaining(p) is > 0 and var left)
        {
            Floaters.Add(new FloatText(p.Pos + new Vec(0, -40),
                "The shoulders still burn — {0} min", "#9a95b6",
                MathF.Ceiling(left / 60f).ToString()));
            return;
        }

        var strong = Party.OrderByDescending(h => CharacterStats.For(h.Def.Key).StrMod).First();
        var d = RollFate("shove", 20, CharacterStats.For(strong.Def.Key).StrMod, FateColour(strong.Def.HeroClass));

        if (d.total >= (p.CheckDC > 0 ? p.CheckDC : 13))
        {
            d.outcome = "good";
            Portals.Remove(p);
            if (p.SetsFlag.Length > 0) RewardBridge.Claim(p.SetsFlag);
            Award(60, XpSource.SecretArea, p.Pos);
            Floaters.Add(new FloatText(p.Pos + new Vec(0, -40), "The boulder tips, holds — and goes", "#f0d99a"));
            if (Models.ContainerKind.Find("chest_iron") is { } cache)
                Interactables.Add(new Interactable("A hollow under the boulder", p.Pos, cache));
            Shake = MathF.Max(Shake, 0.6f);
            PlaySound("mine", p.Pos, 0.8f);
        }
        else
        {
            d.outcome = "bad";
            _jams[p] = 240f;
            Floaters.Add(new FloatText(p.Pos + new Vec(0, -40), "The stone does not care", "#9a95b6"));
        }
    }

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
        if (_pickChipFor > 0) _pickChipFor -= dt;
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

        if (_pendingClimb is { Arrive: { } brink } scar)
        {
            _climbIn -= dt;
            if (_climbIn <= 0)
            {
                _pendingClimb = null;
                var at = Blocked(brink, 14f) ? NearestOpen(brink) : brink;
                // The STEERED hero lands on the mark — the rest fan out from
                // them — so the way back down is in reach the moment you arrive.
                for (int i = 0; i < Party.Count; i++)
                    Party[i].Pos = at + new Vec((i - Active) * 34f, 0);
                Camera = at;
                Shake = MathF.Max(Shake, 0.3f);
                PlaySound("step_stone", at, 0.7f);
            }
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
