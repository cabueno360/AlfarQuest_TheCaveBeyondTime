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

        BuildWorld();
        BuildChamber();

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

    // Walking into the mouth once the chamber is quiet carries the party down.
    void Descend()
    {
        Level++;

        // The delve's goal, in quest terms: the deepest NAMED depth is the Cave
        // Beyond Time itself (RegionNames' last), and reaching it advances (and
        // here, completes) the main quest. Once-ever, like every other flag.
        if (Level >= RegionNames.Length)
            RewardBridge.Claim("cave_deep");

        Phase = "playing";
        ClearedFor = 0f;
        Crystals.Clear(); Husks.Clear(); Shots.Clear(); Slashes.Clear(); Aoe.Clear(); Fx.Clear();
        _rng = new Random(42 + Level * 101);

        BuildWorld();
        BuildChamber();

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
