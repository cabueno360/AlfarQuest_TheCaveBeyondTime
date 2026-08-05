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

    /// <summary>The way down from the Cistern is the Diver: the metal sphere in
    /// the crack by Kazzat's door, and it does not wake without his bronze key.
    /// Without kazzat_key the sealed door refuses; with it, the first boarding
    /// plays the tale's beat — the Mage at the levers — and the crossing lands
    /// on the other shore: the Great Coral Tree, exactly as Kazzat promised.
    /// Deeper floors keep their plain way down.</summary>
    bool _diverAwoken;
    float _diveIn;
    float _diveRefusedFor;

    void TryDive()
    {
        if (Level != 1) { Descend(); return; }
        if (_diveIn > 0) return;                 // the boarding is already playing

        if (!RewardBridge.Claimed().Contains("kazzat_key"))
        {
            if (_diveRefusedFor <= 0)
            {
                Floaters.Add(new FloatText(ExitPos + new Vec(0, -40),
                    "A sealed metal door — the guardian by the shrine keeps its key", "#9a95b6"));
                _diveRefusedFor = 3f;
            }
            return;
        }

        if (_diverAwoken) { Descend(); return; } // the sphere knows them now

        // The deeper dark's level gate speaks BEFORE the boarding scene — a
        // party turned back at the hatch should not have watched the door open.
        if (PartyLevel() is > 0 and var have && have < RecommendedLevel(Level + 1)) { Descend(); return; }

        _diverAwoken = true;
        Say(Party[Active].Def.Name,
            "(The key turns in its socket. Somewhere inside the rock, old metal wakes with a hiss, and a door that has not moved in ages swings wide on a great riveted sphere.)", 6.5f);
        if (Party.Any(h => h.Def.Key == "mage"))
            Say("The Fallen Mage", "Come now, holy man — I can pilot this thing to the bottom. It doesn't seem too complicated.", 5.5f);
        if (Party.Any(h => h.Def.Key == "cleric"))
            Say("The Grieving Cleric", "That is precisely what worries me.", 4.5f);
        _diveIn = 7.5f;                          // the crossing follows the scene
    }

    /// <summary>The other shore, dressed: the Tree itself at the roots-room, and
    /// one ship of the pipe-fallen fleet among the ruins. The tale never went
    /// past the crossing — Kazzat's one line about the Tree is all it gave — so
    /// this shore is the game's own, grown from that line. Reading is optional
    /// and repeatable, like the mirrors.</summary>
    void AddCoralTree()
    {
        void Sight(string regionKey, Vec off, string title, string verb, params string[] pages)
        {
            if (CaveRegions.FirstOrDefault(r => r.Key == regionKey) is not { } reg) return;
            var at = TileCentre(reg.Cx, reg.Cy) + off;
            if (Blocked(at, 14f)) at = NearestOpen(at);
            Examinables.Add(new Examinable
            {
                Pos = at, R = 58f, Title = title, Verb = verb, Kind = "plaque", Pages = pages,
            });
        }

        Sight("sanctuary", new Vec(0f, -TILE * 1.5f), "the Great Coral Tree", "Behold",
            "It has no crown you can see. The trunk is coral upon coral, terrace over terrace — rose, bone-white, deep red — climbing past the reach of any lamp into the dark above. The sea outside this shore is dead. The Tree is not.",
            "Lean close and the hum is there: the same note the Heart's crystal carries, far off and far down, like a bell heard through a wall. Whatever feeds the Tree drinks from the same deep the delve is walking toward.",
            "Kazzat never said what the Tree was. Only that the Diver crosses to it, and that the sea between is an ocean of every monstrosity across all times and places. Standing under it, you understand why something would grow a shore here — even the dead sea wanted one living thing.");

        Sight("ruins", new Vec(TILE * 1.2f, 0f), "a ship of the fallen fleet", "Examine",
            "Grey steel, of no yard that ever was, broken-backed across the coral where the sea set it down. The pipes above the Cistern have fed this ocean since before the stone; this is where some of what falls comes to rest.",
            "The plates are scoured clean and the holds are long empty. On what is left of the bow, under later growth, runs a line of characters in no alphabet the Academy teaches. The crew did not leave by any gangway. Some of them are still walking the shoals.");
    }

    /// <summary>The alcove — the tale's reprieve off the slope trail, one small
    /// camp per depth by the way back up. Resting throws a d20 + the party's
    /// best Vitality: a quiet watch heals well; a haunted one heals half as
    /// much, and someone's nightmare says whose. Once per depth — the second
    /// watch never comes in a place like this.</summary>
    public const string RestTarget = "__rest__";
    bool _restedThisDepth;

    void AddAlcove()
    {
        _restedThisDepth = false;
        var entrance = CaveRegions.FirstOrDefault(r => r.Key == "entrance");
        if (entrance is null) return;

        var at = TileCentre(entrance.Cx, entrance.Cy) + new Vec(TILE * 2.6f, TILE * 1.1f);
        if (Blocked(at, 14f)) at = NearestOpen(at);
        Portals.Add(new Portal { Pos = at, Target = RestTarget, Label = "the alcove", Verb = "Rest", R = 46f });
        Props.Add(new Prop { X = at.X, Y = at.Y - 6f, Kind = "campfire", S = 0.8f, Solid = false, R = 0f });
    }

    void RestAtAlcove()
    {
        if (Party.Count == 0) return;
        var at = Party[Active].Pos;
        if (_restedThisDepth)
        {
            Floaters.Add(new FloatText(at + new Vec(0, -34), "The watch is spent — the alcove gives one rest a depth", "#9a95b6"));
            return;
        }
        _restedThisDepth = true;

        var mod = Party.Max(h => CharacterStats.For(h.Def.Key).VitMod);
        var d = RollFate("rest", 20, mod, "#7fd694");
        var quiet = d.total >= 12;
        d.outcome = quiet ? "good" : "bad";

        foreach (var h in Party)
            if (h.Alive)
            {
                var heal = (int)MathF.Round(h.MaxHp * (quiet ? 0.45f : 0.22f));
                h.Hp = Math.Min(h.MaxHp, h.Hp + heal);
                Floaters.Add(new FloatText(h.Pos + new Vec(0, -24), "+{0}", "#7fd694", heal.ToString()));
            }
        Play("holy", at, null, 0.8f);

        if (quiet)
        {
            Say(Party[Active].Def.Name, "(The watch passes quietly. For a little while, the dark is only dark.)", 6f);
            return;
        }

        // A haunted watch: whoever dreams worst tonight says so — each nightmare
        // is that hero's own chapter leaning on them.
        var dreamer = Party[(int)(TorchTime * 7) % Party.Count];
        Say(dreamer.Def.Name, dreamer.Def.Key switch
        {
            "mage" => "(Sleep comes, and Bruelos is in it — reaching out of the light again. I wake with my heart trying to leave my chest. Half a rest is what the dark allows.)",
            "cleric" => "(I dream of her hand going grey in mine, and the phial always an inch too far. I wake more tired than I lay down. The prayer helps. A little.)",
            "thief" => "(Kas screams in the dream, same as he did. Every time I sleep down here he screams. Half a rest, then — the dice owe me one.)",
            _ => "(The dreams down here are not ours. Half a rest is what the dark allows.)",
        }, 7f);
    }

    /// <summary>The first time the deeper dark visibly presses the light in
    /// (depth 3, where the eating is a quarter gone), someone says so — once a
    /// session, so the rule is FELT before it is deduced.</summary>
    bool _darkNoted;

    void NoteDeepDark()
    {
        if (_darkNoted || Level < 3 || Party.Count == 0) return;
        _darkNoted = true;
        Say(Party[Active].Def.Name,
            "(The torch is the same torch. The dark is not the same dark — it leans in now, and the light gives ground.)", 6.5f);
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

        if (_diveRefusedFor > 0) _diveRefusedFor -= dt;
        if (_diveIn > 0)
        {
            _diveIn -= dt;
            if (_diveIn <= 0)
            {
                Descend();
                if (Level > 1)
                {
                    // Kazzat's warning, rolled: many horrors prowl the waves. The
                    // Mother of all Luck decides whether the crossing goes
                    // unnoticed — a failed die is a hull the sea tested, and the
                    // party lands scraped. Once a session, like the boarding.
                    var mod = Party.Count > 0 ? Party.Max(h => CharacterStats.For(h.Def.Key).LuckMod) : 0;
                    var d = RollFate("crossing", 20, mod, "#4fa3a0");
                    var quiet = d.total >= 10;
                    d.outcome = quiet ? "good" : "bad";
                    if (!quiet)
                    {
                        foreach (var h in Party)
                            if (h.Alive)
                            {
                                var dmg = (int)MathF.Round(h.MaxHp * 0.14f);
                                h.Hp = MathF.Max(1f, h.Hp - dmg);
                                Floaters.Add(new FloatText(h.Pos + new Vec(0, -24), "-{0}", "#d98a8a", dmg.ToString()));
                            }
                        Shake = MathF.Max(Shake, 0.8f);
                        Say(Party[Active].Def.Name,
                            "(Mid-crossing, something vast finds the hull — one slow scrape along the plates, testing, then gone. The craft groans; the sea keeps the sound.)", 6.5f);
                    }
                    Say(Party[Active].Def.Name,
                        "(The sea lets go at last, and the lamps find coral — terrace over terrace of it, climbing out of sight. Another shore. The Tree is real, and it is alive.)", 7f);
                }
            }
        }

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
