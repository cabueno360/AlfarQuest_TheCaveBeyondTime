namespace AlfarQuest.Client.Game;

// =====================================================================
//  Story moments from the source tale, staged in the world.
//
//  The Crystal Cistern is where the cave first looked back: each of the
//  three saw their own reflection and fell into the memory of how they
//  came to be here. The mirrors stand near the mouth of depth 1 as
//  readable faces in the crystal — the three backstories, told by the
//  crystal in the second person of a reflection. And the cave keeps two
//  smaller habits from the tale: the Cleric's canticle on the way down,
//  and Bruelos at the edge of the Mage's eye.
// =====================================================================
public partial class World
{
    /// <summary>The three faces in the crystal, placed a few steps into the
    /// Cistern. Reading one is optional and repeatable — a memory has no lock
    /// and pays nothing; it is the point of the place.</summary>
    void AddCisternMirrors()
    {
        void Mirror(Vec at, string title, params string[] pages) =>
            Examinables.Add(new Examinable
            {
                Pos = at, R = 58f, Title = title, Verb = "Look into",
                Kind = "plaque", Pages = pages,
            });

        Mirror(Spawn + new Vec(-70f, -26f), "a face in the crystal — golden-haired",
            "The crystal holds a summer street in Kae Ychel: silk strung against the twin suns, a young apprentice hurrying with a tome of chocolate leather heavy in his satchel — certain, this year, that the boon is his.",
            "Then a field of granite and a hundred candidates, and fire: a column of it a hundred feet tall. A friend's hand reaches out of the light a moment before there is nothing left to reach with. The Regent's mask turns. A heart is plucked from a chest as simply as a berry from a vine, and something vast is folded a thousand and one times and stitched back in, burning.",
            "Six years of stone, and then a verdict colder than any cell: your boon is life. Seek the Cave Beyond Time, carry the fiend out of the world, or do not come home. The face in the crystal is your own — and it is still burning.");

        Mirror(Spawn + new Vec(0f, -58f), "a face in the crystal — armoured",
            "The crystal holds a northern valley, ten good years deep: a sermon nearly lost mid-word for golden hair in the third row, brighter than any gilding on any mace. A humble wedding band, warm on a warm hand.",
            "Then the winters that would not end, and the wasting that rode them: colour, strength, waking hours, drawn out by inches. Prayer upon prayer upon prayer, and God saying nothing. A stranger at the door with six drops of moonlight — and a price.",
            "A month of hope. The relapse. A journal filling with smaller and smaller handwriting, and at the end of it a bargain kept: come, Cerno — lead me on to damnation. The face in the crystal is your own — and it is still praying.");

        Mirror(Spawn + new Vec(70f, -26f), "a face in the crystal — grinning",
            "The crystal holds Seoshe in the sun: High Street in velvet, every alley yours, the guard drinking on your coin. Kas at the corner sign, near bursting out of his skin — his first big catch. A red lacquered box, off a Kae Ychellen trireme.",
            "The crew around the table at dawn. Essil grumbling soot onto his maps, the twins still dusted with tunnel-dirt, Yash chanting the seals loose one by one. The last charm comes away in one grand gesture — and the light that screams out of the box knows your name.",
            "Silver-blue smoke where five people were, and a building coming down. And the thought carried down every stair since, polished smooth as a coin: not dead. Taken. The cave in the visions has them. The face in the crystal is your own — and it is grinning.");
    }

    /// <summary>Kazzat's hut by the Forgotten Shrine — the guardian of the
    /// Cistern, his kettle, and the way onward. He lives on depth 1: the shrine
    /// room is the tale's island at the bottom of the cistern, the one safe
    /// kindness the delve offers before the Warden's door.</summary>
    void AddKazzat()
    {
        var shrine = CaveRegions.FirstOrDefault(r => r.Key == "sanctuary");
        if (shrine is null || NpcCatalog.Find("kazzat") is not { } def) return;

        var at = TileCentre(shrine.Cx, shrine.Cy);
        Npcs.Add(new Npc(def, at));

        Examinables.Add(new Examinable
        {
            Pos = at + new Vec(52f, -30f), R = 56f, Verb = "Examine", Kind = "note",
            Title = "the guardian's hut",
            Pages =
            [
                "A squat, wide shape of dark bricks and waterlogged copper sheeting, put together with more patience than mortar. Faint light leaks through the shutter seams, and a kettle inside is giving off cinnamon, ginger, and a strong dark tea.",
                "The door hangs crooked on old hinges, mended many times and never quite level. Whoever keeps this place has been keeping it a very long while.",
            ],
        });
    }

    /// <summary>Bruelos, at the edge of the light. Once per session, somewhere in
    /// the first stretch of a delve, the Mage sees him — the way he has seen him
    /// in every dark corner for six years. Armed on entering the cave.</summary>
    bool _bruelosShown;
    float _bruelosIn;

    /// <summary>The pipes letting go: every few minutes underground, something
    /// vast comes home to the Cistern's sea, heard rather than seen. Endless —
    /// the sea has been fed since before the stone.</summary>
    float _pipeFallIn;
    int _pipeFallNext;

    static readonly string[] PipeFalls =
    [
        "(Far off, a pipe lets go — something vast falls a long time before the sea takes it.)",
        "(A ship — grey steel, of no yard that ever was — slides from a high pipe and breaks its back on the waves below.)",
        "(Something with too many arms tumbles past the far light and is gone. The sea does not even splash.)",
    ];

    /// <summary>Arms the cave's story timers. Called by EnterCave.</summary>
    void ArmCaveStory()
    {
        if (!_bruelosShown && Party.Any(h => h.Def.Key == "mage"))
            _bruelosIn = 50f + (float)_rng.NextDouble() * 70f;
        _pipeFallIn = 70f + (float)_rng.NextDouble() * 60f;
    }

    void UpdateStory(float dt)
    {
        if (Stage != 2) return;

        if (_bruelosIn > 0)
        {
            _bruelosIn -= dt;
            if (_bruelosIn <= 0)
            {
                _bruelosShown = true;
                Say("The Fallen Mage",
                    "(At the edge of the light — Bruelos. Wine-flushed, sneering, exactly as he was. Gone the instant I turn. Six years, and he keeps finding me.)", 7f);
            }
        }

        if (_pipeFallIn > 0)
        {
            _pipeFallIn -= dt;
            if (_pipeFallIn <= 0)
            {
                if (Party.Count > 0)
                    Say(Party[Active].Def.Name, PipeFalls[_pipeFallNext % PipeFalls.Length], 6.5f);
                _pipeFallNext++;
                Shake = MathF.Max(Shake, 0.45f);
                PlaySound("mine", Camera, 0.6f);
                _pipeFallIn = 120f + (float)_rng.NextDouble() * 120f;   // and again, forever
            }
        }
    }
}
