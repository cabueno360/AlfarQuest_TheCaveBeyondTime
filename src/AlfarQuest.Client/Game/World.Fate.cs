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

    /// <summary>Counts the pending chest reveal down and opens the loot window
    /// when the die has settled. Called each tick.</summary>
    void UpdateFate(float dt)
    {
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
