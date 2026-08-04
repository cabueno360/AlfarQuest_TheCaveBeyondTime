namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage and depth progression — entering the cave, descending.
// =====================================================================
public partial class World
{
    public Vec ExitPos => Exit;

    /// <summary>A one-shot, persisted flag (rides the ClaimedRewards set, like any
    /// other once-ever fact) marking that the party has descended into the Cave
    /// Beyond Time for the first time. Gates the Entrance-of-Cave cutscene and the
    /// Cleric joining the party, so both happen once ever, not once per visit.</summary>
    const string CaveEnteredFlag = "cave_entered";

    public bool HasEnteredCave => RewardBridge.Claimed().Contains(CaveEnteredFlag);

    // Crossing the mine mouth ends Stage 1 and drops the party into the cave.
    void EnterCave()
    {
        bool firstDescent = !HasEnteredCave;

        Stage = 2;
        Level = 1;
        // The cave is its own size. Without this it inherits the dimensions of
        // whatever map the party just left — which on the authored path is put
        // right by the map itself, and on the generator path leaves the delve
        // shaped like the overworld and sealed past its own regions.
        COLS = CAVE_COLS; ROWS = CAVE_ROWS;
        Phase = "playing";
        ClearedFor = 0f;
        _rng = new Random(42);
        Props.Clear(); Crystals.Clear(); Husks.Clear(); Shots.Clear(); Slashes.Clear(); Aoe.Clear(); Fx.Clear();
        Portals.Clear(); Examinables.Clear(); Reading = null;
        // The village must not follow the party down: without this, overworld
        // NPCs lingered in the list at coordinates the cave now owns.
        Npcs.Clear(); Talking = null;

        BuildWorld();
        BuildChamber();
        // The Cistern looks back: the three faces in the crystal, a few steps in —
        // and Kazzat keeps his kettle by the Forgotten Shrine.
        AddCisternMirrors();
        AddKazzat();
        ArmCaveStory();

        var start = Spawn;
        for (int i = 0; i < Party.Count; i++)
        {
            Party[i].Pos = start + new Vec((i - 1) * 40f, 0);
            Party[i].Cool = Party[i].AbilityCool = Party[i].DashCool = Party[i].PotionCool = 0f;
            Array.Clear(Party[i].SkillCool);
            Party[i].Effects.Clear();
            Party[i].AttackAnim = Party[i].AbilityAnim = Party[i].IFrames = Party[i].Flash = 0f;
        }
        Camera = start;
        Shake = 0.7f;
        SpawnHusks(11);
        SpawnBoss();      // the Guardian holds the Crystal Heart at every depth

        // The first crossing is a story beat: the "Entrance of Cave" cutscene plays
        // and the game holds behind it; when it ends, the cave is revealed with the
        // Cleric now at the party's side. Recorded so it is a once-ever moment.
        if (firstDescent)
        {
            RewardBridge.Claim(CaveEnteredFlag);
            RecruitCleric();
            // His canticle, whispered on the way down — queued behind his joining
            // lines, spoken as the descent begins in earnest.
            Say("The Grieving Cleric",
                "O' Holy Light of the world. Bringer of Dawn. Be with me now — I am a child stumbling in the wilderness, blind and lost and cold without you. Watch over me.", 8f);
            VideoBridge.Play("assets/Videos/entrance-of-cave.mp4");
        }
    }

    /// <summary>The Cleric joins the party at the mouth of the Cave — the moment
    /// he takes up Cerno's bargain. Idempotent: a reloaded save that already carries
    /// him returns straight away, so he is never doubled or reset. The sheet side is
    /// told first, so his attribute-derived numbers resolve the instant the engine
    /// Hero reads them.</summary>
    void RecruitCleric()
    {
        if (Party.Any(h => h.Def.Key == "cleric")) return;
        PartyBridge.Recruit("cleric");
        if (!GameSession.PartyKeys.Contains("cleric"))
            GameSession.PartyKeys = [.. GameSession.PartyKeys, "cleric"];
        Party.Add(new Hero(Lore.ByKey("cleric"), Party[Active].Pos + new Vec(40f, 0)));

        // He has a voice now. The lines queue behind the cutscene (the game is
        // held while it plays) and show as the cave is first revealed — his
        // joining, and, if Mirka's father sent word, its delivery: the moment
        // "Word for the Cleric" was always about but never staged. The quest
        // still completes on the same cave_entered flag it always did.
        Say("The Grieving Cleric",
            "So the light was yours. I came down in Cerno's stead — the bargain is mine to finish. Keep close; the dark here is not empty.", 6.5f);
        if (RewardBridge.Claimed().Contains("sq_word_started"))
        {
            Say(Party[Active].Def.Name,
                "Word from the vale, Cleric: Mirka still breathes. Her father sends it — help enough, he said, for an old man.", 6.5f);
            Say("The Grieving Cleric",
                "She breathes… then the year was not the last of it. Whatever waits below, there is a door worth climbing back to. That word is worth more than the phial.", 7.5f);
        }
    }

    /// <summary>The epilogue: the Panacea set to Mirka's lips. Fires from Interact
    /// when the player uses [E] on her with the deepest depth reached and the
    /// phial actually in a pack — and SPENDS the phial, which is the point of it.
    /// The scene plays in the party's own speech balloons; the flag completes
    /// "The Way Back Up" through the same claim door every quest uses.</summary>
    bool TryWakeMirka(Npc npc)
    {
        if (npc.Def.Id != "mirka") return false;
        var claimed = RewardBridge.Claimed();
        if (claimed.Contains("mirka_woken") || !claimed.Contains("cave_deep")) return false;
        if (PartyBridge.TakeItem?.Invoke("phial_panacea") != true) return false;

        // The fates attend the epilogue. The die cannot fail her — the Panacea
        // is the Panacea — it only chooses HOW the dawn comes: a high throw and
        // she wakes to their presence before the phial ever touches her; a rare
        // gutter roll and there is one long terrible breath of nothing first.
        var healer = Party.FirstOrDefault(h => h.Def.Key == "cleric") ?? Party[Active];
        var wake = RollFate("wake", 20, CharacterStats.For(healer.Def.Key).FateMod, FateColour("Cleric"));
        wake.outcome = wake.total >= 18 ? "good" : wake.total < 8 ? "bad" : "plain";

        Burst(npc.Pos, "#f0d99a", 26);
        Play("chest_rare", npc.Pos);
        if (wake.outcome == "good")
            Say("Mirka", "(Her eyes open before the phial touches her lips — as though it was the waiting, all along, that held her under.)", 6.5f);
        else if (wake.outcome == "bad")
            Say("Mirka", "(The phial trembles at her lips. For one long breath — nothing. Then colour, the way dawn comes after a false dawn: doubted first, then everywhere.)", 7f);
        else
            Say("Mirka", "(The phial touches her lips. Colour comes back the way dawn comes — slowly, then all at once.)", 6.5f);
        Say("Mirka", "…I know your faces. He wrote of you in the margins of his journal. Is he— is my husband here?", 6.5f);
        if (Party.Any(h => h.Def.Key == "cleric"))
            Say("The Grieving Cleric", "I am here, Mirka. The bargain is paid, and the dark is behind me. I am home.", 7.5f);
        RewardBridge.Claim("mirka_woken");
        return true;
    }

    /// <summary>The way back UP: leaves the delve and stands the party at the mouth
    /// of the cave on the region they came in from. Until now the only way out was
    /// Abandon Delve, which quit to the roster — this walks you home instead. Run by
    /// the "Leave the cave" step placed at the cave's entrance (see AddCaveExit).</summary>
    public void LeaveCave()
    {
        // The delve's own state goes; standing the region up lays its ground fresh.
        Husks.Clear(); Bolts.Clear(); Shots.Clear(); Slashes.Clear(); Aoe.Clear();
        Crystals.Clear(); Fx.Clear(); Portals.Clear(); Examinables.Clear(); Reading = null;

        string region = CurrentRegion ?? "r1_ashwold";
        if (!StandRegion(region)) BuildOverworld();

        // Out in FRONT of the mouth, a couple of tiles clear of its enter-trigger,
        // so coming back up does not drop you straight down the hole again.
        var at = NearestOpen(CaveMouth + new Vec(0, TILE * 2.6f));
        PlaceParty(at);
        Camera = at;
    }

    /// <summary>Stands a wiped party back up, once the fall has had its moment.
    /// A wipe in the cave loses the delve — the party comes to at the mouth,
    /// outside — and a wipe on the surface or indoors wakes them at the place's
    /// spawn. Deliberately no cost beyond the ground lost: taxing the purse on
    /// top of the walk back would read as punishment, not consequence.</summary>
    void Revive()
    {
        foreach (var h in Party)
        {
            h.Hp = h.MaxHp * 0.6f;
            h.Mana = Math.Max(h.Mana, h.MaxMana * 0.5f);
            h.Stamina = h.MaxStamina;
            h.Effects.Clear();
            h.Cool = h.AbilityCool = h.DashCool = h.PotionCool = 0f;
            Array.Clear(h.SkillCool);
            h.AttackAnim = h.AbilityAnim = h.Flash = 0f;
            h.IFrames = 1.5f;          // a breath before anything can bite again
        }
        Phase = "playing";
        ClearedFor = 0f;
        FallenFor = 0f;
        Active = 0;

        if (Stage == 2) LeaveCave();
        else { PlaceParty(Spawn); Camera = Spawn; }
    }

    /// <summary>Where a save should stand the party back up: the region, and the
    /// spot in it. Indoors that is the doorstep the building was entered from;
    /// below ground it is the front of the cave mouth — the WALK is resumed, not
    /// the room, because interiors and the delve rebuild themselves.</summary>
    public (string Region, float X, float Y) ResumePoint()
    {
        var region = CurrentRegion ?? StartRegion;
        var at = Stage switch
        {
            3 => _interiorReturn,
            2 => CaveMouth + new Vec(0, TILE * 2.6f),
            _ => Party.Count > 0 ? Party[Active].Pos : Spawn,
        };
        return (region, at.X, at.Y);
    }

    /// <summary>The level each named depth is tuned to — its keeper's stats and
    /// its husks' assume a party of about this strength. Past the named depths
    /// the ask keeps climbing.</summary>
    public static int RecommendedLevel(int depth) => depth switch
    {
        <= 1 => 1, 2 => 3, 3 => 5, 4 => 8, 5 => 11,
        _ => 11 + (depth - 5) * 2,
    };

    /// <summary>The party's standing: the average level of its members' sheets.
    /// Zero when no sheet side is attached (a probe, a bare engine test), which
    /// the descent gate reads as "do not gate".</summary>
    int PartyLevel()
    {
        if (CharacterStats.Progress is null) return 0;
        var sum = 0; var n = 0;
        foreach (var h in Party) { sum += CharacterStats.ProgressOf(h.Def.Key).Level; n++; }
        return n == 0 ? 0 : (int)MathF.Round(sum / (float)n);
    }

    // Walking into the mouth once the chamber is quiet carries the party down.
    void Descend()
    {
        // The deeper dark asks for levels. Each depth is tuned to a number
        // (RecommendedLevel), and a party under it is turned back at the mouth
        // with that number said plainly — better a refusal at the door than a
        // keeper it cannot yet touch. An engine with no sheets attached reads
        // as level 0 and is never gated, so probes still walk everywhere.
        int have = PartyLevel();
        int asks = RecommendedLevel(Level + 1);
        if (have > 0 && have < asks)
        {
            Floaters.Add(new FloatText(Party[Active].Pos + new Vec(0, -34),
                "The deeper dark asks for level {0}", "#d98a8a", asks.ToString()));
            ClearedFor = 0.2f;   // re-arms the grace, so the refusal does not repeat every frame
            return;
        }

        Level++;

        // The delve's goal, in quest terms: the deepest NAMED depth is the Cave
        // Beyond Time itself (RegionNames' last), and reaching it advances (and
        // here, completes) the main quest. Once-ever, like every other flag.
        if (Level >= RegionNames.Length)
            RewardBridge.Claim("cave_deep");

        Phase = "playing";
        ClearedFor = 0f;
        Crystals.Clear(); Husks.Clear(); Shots.Clear(); Slashes.Clear(); Aoe.Clear(); Fx.Clear();
        // Depth 1's mirrors, Kazzat and any other readable must not linger into
        // the deeper floors at their old coordinates.
        Examinables.Clear(); Reading = null;
        Npcs.Clear(); Talking = null;
        _rng = new Random(42 + Level * 101);

        BuildWorld();
        BuildChamber();
        // The deeper dark presses the light in (see the eat factor in the
        // snapshot) — the first time it visibly bites, someone says so.
        NoteDeepDark();

        var start = Spawn;
        for (int i = 0; i < Party.Count; i++)
        {
            Party[i].Pos = start + new Vec((i - 1) * 40f, 0);
            Party[i].Cool = Party[i].AbilityCool = Party[i].DashCool = Party[i].PotionCool = 0f;
            Array.Clear(Party[i].SkillCool);
            Party[i].Effects.Clear();
            Party[i].AttackAnim = Party[i].AbilityAnim = Party[i].IFrames = Party[i].Flash = 0f;
            // A breather between levels, but the fallen stay fallen.
            if (Party[i].Alive) Party[i].Hp = Math.Min(Party[i].MaxHp, Party[i].Hp + 45f);
        }
        Camera = start;
        Shake = 0.6f;

        SpawnHusks(9 + Level * 2);   // each descent is a little worse
        SpawnBoss();
    }
}
