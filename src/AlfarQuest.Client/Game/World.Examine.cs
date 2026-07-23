namespace AlfarQuest.Client.Game;

// =====================================================================
//  Things you can read or look closely at.
//
//  An interior is full of them — the journal on the nightstand, letters
//  from the Order, an armour stand with no armour on it. Each is a point
//  with a title and a few pages of text; walking up and pressing [E]
//  hands them to the reading window. It holds the hero still while open,
//  exactly as talking and trading do, so you cannot wander off mid-page.
// =====================================================================
public partial class World
{
    /// <summary>One readable or examinable thing — a title, how to act on it
    /// ("Read"/"Examine"), and its pages.</summary>
    public sealed class Examinable
    {
        public Vec Pos;
        public float R = 44f;
        public required string Title;
        public string Verb = "Examine";
        public string Kind = "note";     // note | journal | letter | plaque — for styling
        public required IReadOnlyList<string> Pages;
    }

    public List<Examinable> Examinables { get; } = [];

    /// <summary>The examinable within reach of the steered hero, or null.</summary>
    public Examinable? ExamineInReach { get; private set; }

    /// <summary>What is being read, or null. Holds the hero the way talking does.</summary>
    public Examinable? Reading { get; private set; }
    public bool IsReading => Reading is not null;

    void UpdateExaminables()
    {
        ExamineInReach = null;
        if (Party.Count == 0 || Busy) return;
        var hero = Party[Active];
        var best = float.MaxValue;
        foreach (var e in Examinables)
        {
            var d = (e.Pos - hero.Pos).Len();
            if (d < e.R && d < best) { best = d; ExamineInReach = e; }
        }
    }

    /// <summary>Opens an examinable's text, if a reader is listening, and holds the
    /// hero while it is up.</summary>
    void OpenReading(Examinable e)
    {
        if (ReadBridge.Offer(new OpenedReading(e.Title, e.Kind, e.Pages))) Reading = e;
    }

    /// <summary>Closes the reading from the engine's side. Wired to
    /// <see cref="ReadBridge.OnClose"/> so the window and the held hero release
    /// together.</summary>
    public void CloseReading() => Reading = null;

    /// <summary>Registers an examinable point. The visible prop, if any, is placed
    /// by the room builder — this only says "there is something to read here".</summary>
    public void AddExamine(float tx, float ty, string verb, string kind, string title, params string[] pages)
    {
        Examinables.Add(new Examinable
        {
            Pos = TileCentre(tx, ty), Verb = verb, Kind = kind, Title = title, Pages = pages,
        });
    }
}
