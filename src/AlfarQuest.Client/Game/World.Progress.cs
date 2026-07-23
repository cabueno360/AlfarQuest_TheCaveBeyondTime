namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage and depth progression — entering the cave, descending.
// =====================================================================
public partial class World
{
    public Vec ExitPos => Exit;

    // Crossing the mine mouth ends Stage 1 and drops the party into the cave.
    void EnterCave()
    {
        Stage = 2;
        Level = 1;
        Phase = "playing";
        ClearedFor = 0f;
        _rng = new Random(42);
        Props.Clear(); Crystals.Clear(); Husks.Clear(); Shots.Clear(); Slashes.Clear(); Fx.Clear();
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
    }

    // Walking into the mouth once the chamber is quiet carries the party down.
    void Descend()
    {
        Level++;
        Phase = "playing";
        ClearedFor = 0f;
        Crystals.Clear(); Husks.Clear(); Shots.Clear(); Slashes.Clear(); Fx.Clear();
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
    }
}
