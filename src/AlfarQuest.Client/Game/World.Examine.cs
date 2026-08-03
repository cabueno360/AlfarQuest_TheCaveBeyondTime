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

        /// <summary>Optional, one per page: a story flag that must be set before that
        /// page can be read, so a journal fills in as the tale earns it. An empty
        /// flag (or no list at all) means the page is always open.</summary>
        public IReadOnlyList<string>? PageFlags;

        /// <summary>A story flag raised the first time this is read — how READING
        /// advances a quest: the memorial names the struck name, the burnt crate
        /// says what the fire left. Empty = reading it claims nothing. Rides the
        /// same one-shot claim set everything else does, so it pays once ever.</summary>
        public string SetsFlag = "";
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
        var pages = e.Pages;
        if (e.PageFlags is { } gates)
        {
            // Only the pages the story has earned. The gates run in story order, so
            // what shows is a growing prefix; when any remain locked, a closing line
            // says so, and the reader comes back to a fuller book later.
            var claimed = RewardBridge.Claimed();
            var shown = new List<string>();
            bool locked = false;
            for (int i = 0; i < e.Pages.Count; i++)
            {
                var flag = i < gates.Count ? gates[i] : "";
                if (flag.Length == 0 || claimed.Contains(flag)) shown.Add(e.Pages[i]);
                else locked = true;
            }
            if (locked)
                shown.Add("The entries that follow are pressed deep and torn, the hand grown wild — you cannot yet make them out. Perhaps when you have gone where he went.");
            pages = shown;
        }
        if (ReadBridge.Offer(new OpenedReading(e.Title, e.Kind, pages))) Reading = e;

        // Reading it is the deed: the flag goes up the moment the page is open,
        // through the same gated claim every one-shot uses — so an examinable in
        // a Tiled map can advance a quest with one SetsFlag property.
        if (Reading is not null && e.SetsFlag.Length > 0) Claim(e.SetsFlag);
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

    /// <summary>Like <see cref="AddExamine"/>, but each page carries a flag that
    /// gates it: the later leaves of a journal open as the story sets them (empty
    /// flag = always open). See <see cref="Examinable.PageFlags"/>.</summary>
    public void AddExamineGated(float tx, float ty, string verb, string kind, string title, params (string Page, string Flag)[] pages)
    {
        Examinables.Add(new Examinable
        {
            Pos = TileCentre(tx, ty), Verb = verb, Kind = kind, Title = title,
            Pages = pages.Select(p => p.Page).ToArray(),
            PageFlags = pages.Select(p => p.Flag).ToArray(),
        });
    }
}
