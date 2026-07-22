namespace AlfarQuest.Client.Game;

// =====================================================================
//  Test seams.
//
//  Driving a headless party across a map full of terrain to find one
//  particular creature is unreliable — a walk wedges on a fence or ends
//  up in a safe zone. So a test can instead ask for a controlled
//  encounter: the party set down on open ground with a fixed cast of
//  creatures around it, already hunting. What the AI then does — chase,
//  cast, drop loot, pay its killer — is the real behaviour, observed on
//  a deterministic stage rather than a lucky one.
//
//  Nothing here is reachable from play; it exists only behind the
//  JSInvokable of the same name, which the game UI never calls.
// =====================================================================
public partial class World
{
    /// <summary>Sets the party down on open forest ground and gathers a fixed
    /// cast of creatures around it, already chasing — two casters, a bruiser and
    /// a darter — so a test can watch hunting and casting without hunting for a
    /// fight first.</summary>
    public void DebugEncounter()
    {
        if (Party.Count == 0 || Active >= Party.Count) return;

        var here = FindOpenForestSpot();
        foreach (var h in Party) { h.Pos = here; h.Hp = h.MaxHp; }
        Camera = here;
        Husks.Clear();
        Bolts.Clear();

        // A caster to each side, a bruiser and a darter above and below — close
        // enough to be inside the casters' reach at once.
        var cast = new (string Id, Vec Off)[]
        {
            ("slime",  new Vec( 84f, 0f)),
            ("spider", new Vec(-84f, 0f)),
            ("beast",  new Vec(0f, -84f)),
            ("bat",    new Vec(0f,  84f)),
        };
        foreach (var (id, off) in cast)
            Husks.Add(new Husk(here + off, CreatureCatalog.Of(id))
            {
                State = AiState.Chase,
                Alerted = 6f,      // committed, so it hunts rather than drifting home
            });
    }

    /// <summary>Sets the party down beside one immortal, evasive target — a
    /// training dummy. A live creature dies in a hit or two, far too few rolls for
    /// a 20%-chance stun or an evasive miss to show; a dummy that cannot die gives
    /// as many swings as a test needs.</summary>
    public void DebugTrainingDummy()
    {
        if (Party.Count == 0 || Active >= Party.Count) return;

        var here = FindOpenForestSpot();
        foreach (var h in Party) { h.Pos = here; h.Hp = h.MaxHp; }
        Camera = here;
        Husks.Clear();
        Bolts.Clear();

        // Enormous health, hard to hit, and rooted so it neither wanders off nor
        // hits back — a post to practise on, not a fight.
        var dummy = CreatureCatalog.Of("beast") with
        {
            Id = "dummy", Name = "Training Dummy",
            MaxHp = 1_000_000f, Evasion = 0.25f, Damage = 0, Speed = 0f, AggroTiles = 999f,
        };
        Husks.Add(new Husk(here + new Vec(38f, 0f), dummy) { State = AiState.Chase, Rooted = true });
    }

    /// <summary>An open, walkable forest tile that is not in a safe zone — so the
    /// creatures set down beside it will actually hunt.</summary>
    Vec FindOpenForestSpot()
    {
        for (int ty = 46; ty < 56; ty++)
            for (int tx = 10; tx < 44; tx++)
            {
                var p = new Vec(tx * TILE + TILE * 0.5f, ty * TILE + TILE * 0.5f);
                if (Blocked(p, 18f) || InSafeZone(p.X, p.Y)) continue;
                if (BiomeAt(p.X, p.Y) != Biome.Forest) continue;
                return p;
            }
        return Spawn;
    }
}
