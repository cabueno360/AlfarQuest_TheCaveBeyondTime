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

    /// <summary>Stands the steered hero beside a named merchant and presses [E] on
    /// them — so a test can open a shop deterministically instead of steering a
    /// headless party across the map to find one. It goes through the same
    /// Interact() path play does, so what opens is the real offer, the real hero as
    /// buyer, and the real hold; only the walk there is skipped.</summary>
    public void DebugTradeWith(string npcId)
    {
        if (Party.Count == 0 || Active >= Party.Count) return;
        var npc = Npcs.FirstOrDefault(n => n.Def.Id == npcId);
        if (npc is null) return;

        Talking = null;
        Party[Active].Pos = npc.Pos + new Vec(28f, 0f);
        Camera = npc.Pos;
        NpcInReach = npc;      // set now so Interact acts on them this instant
        Interact();
    }

    /// <summary>Drops the steered hero onto a tile — a test seam so a probe can
    /// reach a doorway or a landmark without walking the whole map to it.</summary>
    public void DebugWarp(float tx, float ty)
    {
        if (Party.Count == 0 || Active >= Party.Count) return;
        var p = TileCentre(tx, ty);
        if (Blocked(p, 14f)) p = NearestOpen(p);
        Party[Active].Pos = p;
        Camera = p;
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

    /// <summary>Drops the party into the cave so it can be exported like the rest.
    /// The cave's layout is hand-authored and identical at every depth — only its
    /// population changes — so one capture describes every level.</summary>
    public void DebugEnterCave() => EnterCave();

    /// <summary>The whole built map, as plain data — the export side of the Tiled
    /// migration. Stage 1 is generated from a fixed seed rather than authored, so
    /// the only way to "use the current layout exactly" is to run the generator
    /// once and write down what it produced. tools/export-stage01.mjs calls this
    /// and tools/make-tmx.py turns the result into Stage01_Outside.tmx.
    ///
    /// Positions are world pixels (TILE = 32); the TMX writer converts to its own
    /// 16px grid. Nothing here is reachable from play.</summary>
    public object ExportMap() => new
    {
        name = OverworldName,
        cols = COLS, rows = ROWS, tile = TILE,
        tiles = MapRows(),                       // '#' rock '.' floor '~' water '=' bridge ',' path
        spawn = new { x = Spawn.X, y = Spawn.Y },
        exit = new { x = Exit.X, y = Exit.Y },
        caveMouth = new { x = CaveMouth.X, y = CaveMouth.Y },
        safeZones = SafeZones.Select(z => new { x0 = z.X0, y0 = z.Y0, x1 = z.X1, y1 = z.Y1 }),
        props = Props.Select(p => new { x = p.X, y = p.Y, kind = p.Kind, v = p.Variant,
                                       s = p.S, solid = p.Solid, r = p.R, flip = p.Flip,
                                       cx = p.Cx, cy = p.Cy }),   // cave props are a cell, not a name
        npcs = Npcs.Select(n => new { id = n.Def.Id, x = n.Pos.X, y = n.Pos.Y }),
        portals = Portals.Select(p => new { x = p.Pos.X, y = p.Pos.Y, r = p.R, target = p.Target,
                                           label = p.Label, verb = p.Verb,
                                           retX = p.Return.X, retY = p.Return.Y }),
        examinables = Examinables.Select(e => new { x = e.Pos.X, y = e.Pos.Y, r = e.R,
                                                   title = e.Title, verb = e.Verb, kind = e.Kind,
                                                   pages = e.Pages }),
        containers = Interactables.Select(i => new { x = i.Pos.X, y = i.Pos.Y, name = i.Name,
                                                    kind = i.Kind.Id }),
        discoveries = Discoveries.Select(d => new { x = d.Pos.X, y = d.Pos.Y, name = d.Name,
                                                   radius = d.Radius, source = d.Source.ToString() }),
        arches = Arches.Select(a => new { x = a.X, y = a.Y }),
        creatures = Husks.Select(k => new { id = k.Def.Id, x = k.Pos.X, y = k.Pos.Y }),
    };
}
