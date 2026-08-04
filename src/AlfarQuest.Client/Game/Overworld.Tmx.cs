using AlfarQuest.Client.Game.Tiled;
using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 1, built from its Tiled map instead of from the generator.
//
//  This file is the whole of the map-format migration on the engine side:
//  it reads a TmxMap and produces exactly what BuildOverworld used to
//  produce by hand — the same tiles, props, villagers, creatures, doors,
//  chests and discoveries. Nothing downstream knows the difference.
//
//  What lives where:
//    • terrain type comes from LAYER PRECEDENCE, not from tile ids, so a
//      map can be repainted with any tileset and still play the same;
//    • everything else is an object layer entry with custom properties.
//  Collision is not hardcoded: it is the Cliffs and Water layers plus each
//  prop's own Radius, all of it read from the map.
// =====================================================================
public partial class World
{
    /// <summary>The map grid is 16px and the engine's logical cell is 32px, so one
    /// engine cell is a 2x2 block of map cells. Keeping the engine at 32 is what
    /// lets every catalogue, builder and saved coordinate stay as it is.</summary>
    const int MapSub = 2;

    /// <summary>Map pixels → world pixels. The map is drawn at half the engine's
    /// tile size, so every position doubles on the way in.</summary>
    static Vec FromMap(float x, float y) => new(x * MapSub, y * MapSub);

    void BuildOverworldFromTmx(TmxMap m)
    {
        Rev++;
        Stage = 1;
        COLS = m.Width / MapSub; ROWS = m.Height / MapSub;
        Tiles = new byte[COLS, ROWS];
        Props.Clear(); Arches.Clear(); Crystals.Clear(); Husks.Clear();
        Portals.Clear(); Examinables.Clear(); Reading = null;
        Npcs.Clear(); Interactables.Clear(); Discoveries.Clear();

        ReadTerrain(m);
        ReadSafeZones(m);
        ReadProps(m);
        ReadNpcs(m);
        ReadCreatures(m);
        ReadWarps(m);
        ReadContainers(m);
        ReadDiscoveries(m);
        ReadExaminables(m);

        // The cave mouth first, because the wildlife pass keys its NearCave biome
        // off it — the husks gather where the cold comes out. It is also a
        // doorway now, not a trap: the descent asks for [E] like every other
        // door, reading the destination the map has carried all along, instead
        // of firing the moment a hero strays within 46 pixels.
        if (m.Objects("CaveMouth").FirstOrDefault() is { } cm0)
        {
            CaveMouth = FromMap(cm0.X, cm0.Y);
            Portals.Add(new Portal
            {
                Pos = CaveMouth,
                Target = cm0.Str("DestinationMap", Tiled.MapCatalog.Cave),
                Label = cm0.Str("Label", cm0.Name),
                Verb = cm0.Str("Verb", "Descend"),
                R = cm0.Num("Radius", 46f),
            });
        }

        // Populate the wild parts of the map. A region says how many with a
        // Wildlife property; the spawner keeps them out of the safe zones (read
        // just above) and away from the doorstep, and picks each one from the
        // biome of the ground it lands on. Zero, or an unmigrated map, leaves the
        // place empty — the old Stage 1 spawned its own wildlife in its builder.
        int wildlife = m.PropertyInt("Wildlife", 0);
        if (wildlife > 0) SpawnWildlife(wildlife);

        // The map says what is here; the save says what this player has already
        // taken. Without this a chest emptied before a door was stepped through
        // comes back full on the way out.
        ApplyClaims();

        // Where the party starts and where the mine takes them. Both are map
        // objects; the cave mouth doubles as the stage exit.
        if (m.Objects("PlayerSpawn").FirstOrDefault() is { } sp)
        {
            Spawn = FromMap(sp.X, sp.Y);
            // If the map's spawn was placed on water or rock — a hand-edit that
            // dragged it onto the river, or a marker dropped a hair inside a wall —
            // nudge it to the nearest walkable cell so the party never starts stuck.
            if (Blocked(Spawn, 14f)) Spawn = NearestOpen(Spawn);
        }
        Exit = CaveMouth;
        Camera = Spawn;
    }

    /// <summary>A building's interior, from its Tiled map. The same readers as the
    /// overworld — an NPC, a door, a chest and a thing to read are the same objects
    /// wherever they stand — over a simpler terrain rule: a room is walls and floor.
    ///
    /// Called by LoadInterior, which has already read the place's name, daylight
    /// flag and floor material from the map's own properties.</summary>
    void BuildInteriorFromTmx(TmxMap m)
    {
        COLS = m.Width / MapSub; ROWS = m.Height / MapSub;
        Tiles = new byte[COLS, ROWS];

        // Walls block; below them the same precedence the outdoors uses, because a
        // walled place is not always a roofed one — Seoshe is a city, with streets
        // to walk and dock water to fall in. A map with none of those layers is a
        // plain room and every cell falls through to floor.
        //
        // The kitchen's flagstones are NOT a case here: a surface is not a rule, so
        // they are painted on the Ground layer and walked on like any other floor.
        var walls = m.Layer("Walls");
        var water = m.Layer("Water");
        var bridges = m.Layer("Bridges");
        var roads = m.Layer("Roads");
        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
            {
                int mx = x * MapSub, my = y * MapSub;
                bool Any(int[]? l) =>
                    m.Painted(l, mx, my) || m.Painted(l, mx + 1, my) ||
                    m.Painted(l, mx, my + 1) || m.Painted(l, mx + 1, my + 1);

                Tiles[x, y] = Any(walls) ? ROCK
                            : Any(bridges) ? BRIDGE      // the deck beats the dock water it spans
                            : Any(water) ? WATER
                            : Any(roads) ? PATH
                            : FLOOR;
            }

        ReadProps(m);
        ReadNpcs(m);
        ReadWarps(m);
        ReadExaminables(m);
        ReadContainers(m);

        if (m.Objects("PlayerSpawn").FirstOrDefault() is { } sp)
        {
            Spawn = FromMap(sp.X, sp.Y);
            // If the map's spawn was placed on water or rock — a hand-edit that
            // dragged it onto the river, or a marker dropped a hair inside a wall —
            // nudge it to the nearest walkable cell so the party never starts stuck.
            if (Blocked(Spawn, 14f)) Spawn = NearestOpen(Spawn);
        }
    }

    /// <summary>Terrain by layer precedence: whatever is painted highest wins.
    /// Ground is the bed everything else sits on, so an unpainted cell is floor.</summary>
    void ReadTerrain(TmxMap m)
    {
        var cliffs = m.Layer("Cliffs");
        var walls = m.Layer("Walls");
        // Buildings and the tree layers block too: a house or a tree is a thing you
        // walk AROUND, not through. NOTE this ends the eaves-overhang — a roof cell
        // now carries a body, so a house is solid to its full drawn extent (mark a
        // door's threshold on Roads/Ground, NOT Buildings, if you want to step onto
        // it). Trees block their whole footprint, canopy included, so a dense wood
        // reads as a wall you follow the road around.
        var buildings = m.Layer("Buildings");
        var trees = new[]
        {
            m.Layer("Trees"), m.Layer("Trees2"), m.Layer("Trees3"),
            m.Layer("Trees4"), m.Layer("Trees5"), m.Layer("Trees6"),
        };
        var water = m.Layer("Water");
        var bridges = m.Layer("Bridges");
        var roads = m.Layer("Roads");

        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
            {
                // Any of the cell's 2x2 map cells painted counts as painted, so a
                // map drawn at 16px resolution never leaves half-blocking cells.
                bool AnyAt(int[]? l, int cx, int cy)
                {
                    int bx = cx * MapSub, by = cy * MapSub;
                    return m.Painted(l, bx, by) || m.Painted(l, bx + 1, by)
                        || m.Painted(l, bx, by + 1) || m.Painted(l, bx + 1, by + 1);
                }
                bool Any(int[]? l) => AnyAt(l, x, y);
                bool AnyTreeAt(int cx, int cy)
                {
                    foreach (var t in trees) if (AnyAt(t, cx, cy)) return true;
                    return false;
                }

                // A tree and a building are TALL sprites: the canopy and the roof are
                // drawn where you may still walk — behind them — and only the FOOT of
                // the sprite, the trunk and the doorsill course, stops you. So each
                // blocks only at its base: painted in this cell but NOT in the cell
                // below it, which is where it meets the ground. Everything above that
                // is walk-behind. (A house's solid front is the Walls-layer courses
                // stamp_house lays under the sprite, which still block in full.)
                bool treeBlocks = AnyTreeAt(x, y) && !AnyTreeAt(x, y + 1);
                // A house is SOLID over its whole drawn footprint (the Buildings
                // layer) — you cannot walk into it or behind it. That is what keeps
                // an actor always in FRONT of the house, drawn over it and visible,
                // instead of slipping behind a wall or a roof and being hidden. The
                // door threshold sits on Roads/Ground out front, so a solid house is
                // still enterable. Trees stay base-only (their trunk) so a wood is
                // still walkable.
                bool bldgBlocks = AnyAt(buildings, x, y);

                // A BRIDGE is the crossing, and it beats EVERYTHING drawn on its deck —
                // the water it spans, the railings (whether the author painted them on
                // Walls or Buildings), and any overhanging canopy (Trees). Only a Cliff
                // outranks a bridge, since a deck cannot pass through a rock face. This
                // is what keeps a railed wooden bridge walkable now that Walls, Buildings
                // and Trees all block: the deck stays a deck, and it is the water off its
                // sides — not the railing tiles — that keeps you on it. (Reading water
                // before bridges once sank every ford whose water was not cleared under
                // it, splitting Ashwold in two; reading walls before bridges then walled
                // the Ashwold footbridge mid-span where a railing tile sat on the Walls
                // layer — the party could step onto the deck but never off the far end.)
                Tiles[x, y] = Any(cliffs) ? ROCK
                            : Any(bridges) ? BRIDGE
                            : Any(walls) || bldgBlocks ? ROCK   // walls AND the whole house body are solid
                            : treeBlocks ? TRUNK                // a soft, narrow trunk — not a full block
                            : Any(water) ? WATER
                            : Any(roads) ? PATH
                            : FLOOR;
            }
    }

    /// <summary>The rectangles where creatures will not go, from the map. A region
    /// carries its own — the village, the fields, the gate — so "where people live"
    /// is the map's answer, not one fixed set of coordinates from the old Stage 1.
    /// An object's x/y/width/height are map pixels; an engine tile is 16 of them
    /// (a 32px cell over a 16px map).</summary>
    void ReadSafeZones(TmxMap m)
    {
        _mapSafeZones.Clear();
        foreach (var o in m.Objects("SafeZone"))
            _mapSafeZones.Add(((int)(o.X / 16), (int)(o.Y / 16),
                               (int)((o.X + o.Width) / 16), (int)((o.Y + o.Height) / 16)));
    }

    void ReadProps(TmxMap m)
    {
        foreach (var o in m.Objects("Props"))
        {
            var p = FromMap(o.X, o.Y);
            Props.Add(new Prop
            {
                X = p.X, Y = p.Y,
                Kind = o.Str("Kind", o.Name),
                Variant = o.Int("Variant"),
                S = o.Num("Scale", 1f),
                Solid = o.Flag("Solid"),
                R = o.Num("Radius"),
                Flip = o.Flag("Flip"),
                Painted = o.Has("Painted") ? o.Flag("Painted") : null,
            });
        }
    }

    // A reader that cannot resolve an id SAYS SO. These lookups used to
    // `continue` silently, so a typo in Tiled cost an object with no diagnostic —
    // the worst failure mode a map editor can have.
    void ReadNpcs(TmxMap m)
    {
        foreach (var o in m.Objects("NPCSpawn"))
        {
            var id = o.Str("NpcId", o.Name);
            if (NpcCatalog.Find(id) is not { } def)
            {
                Console.Error.WriteLine($"tmx: NPCSpawn '{id}' at ({o.X:0},{o.Y:0}) matches no NpcCatalog entry — skipped");
                continue;
            }
            var pos = FromMap(o.X, o.Y);
            if (Blocked(pos, 14f)) pos = NearestOpen(pos);
            Npcs.Add(new Npc(def, pos));
        }
    }

    void ReadCreatures(TmxMap m)
    {
        foreach (var o in m.Objects("EnemySpawn"))
        {
            var id = o.Str("EnemyId", o.Name);
            if (CreatureCatalog.All.FirstOrDefault(c => c.Id == id) is not { } def)
            {
                Console.Error.WriteLine($"tmx: EnemySpawn '{id}' at ({o.X:0},{o.Y:0}) matches no creature — skipped");
                continue;
            }
            Husks.Add(new Husk(FromMap(o.X, o.Y), def));
        }
    }

    /// <summary>Doorways. A warp names the map it leads to and where it puts you
    /// back — the same Portal the interiors already use, so stepping through one
    /// from a Tiled map and from a hand-built interior are the same act.</summary>
    void ReadWarps(TmxMap m)
    {
        foreach (var o in m.Objects("Warp"))
        {
            var back = o.Str("DestinationSpawn").Split(',');
            var ret = back.Length == 2
                && float.TryParse(back[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rx)
                && float.TryParse(back[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ry)
                ? new Vec(rx, ry) : default;
            var target = o.Str("DestinationMap", Portal.Overworld);
            // The door is still placed when its far side is unknown — a visibly
            // dead door beats one that silently never existed — but the author is
            // told which manifest line or DestinationMap spelling is wrong.
            if (target != Portal.Overworld && !Tiled.MapCatalog.Has(target))
                Console.Error.WriteLine($"tmx: Warp '{o.Name}' leads to unregistered map '{target}' — the door will do nothing");
            Portals.Add(new Portal
            {
                Pos = FromMap(o.X, o.Y),
                Target = target,
                Label = o.Str("Label", o.Name),
                Verb = o.Str("Verb", "Enter"),
                R = o.Num("Radius", 42f),
                Return = ret,
            });
        }
    }

    void ReadContainers(TmxMap m)
    {
        foreach (var o in m.Objects("TreasureSpawn"))
        {
            var kindId = o.Str("ContainerKind");
            if (ContainerKind.Find(kindId) is not { } kind)
            {
                Console.Error.WriteLine($"tmx: TreasureSpawn '{o.Name}' names unknown ContainerKind '{kindId}' — skipped");
                continue;
            }
            Interactables.Add(new Interactable(o.Name, FromMap(o.X, o.Y), kind));
        }
    }

    void ReadDiscoveries(TmxMap m)
    {
        foreach (var o in m.Objects("Discovery"))
        {
            if (!Enum.TryParse<XpSource>(o.Str("XpSource"), out var source))
            {
                if (o.Has("XpSource"))
                    Console.Error.WriteLine($"tmx: Discovery '{o.Name}' has unknown XpSource '{o.Str("XpSource")}' — using RegionDiscovered");
                source = XpSource.RegionDiscovered;
            }
            Discoveries.Add(new Discovery(o.Name, FromMap(o.X, o.Y), o.Num("Radius", 90f), source));
        }
    }

    /// <summary>Things to read out in the world. Pages are held in one property,
    /// separated by a unit separator, because Tiled has no list type. An optional
    /// PageFlags property — same separator, index-aligned with Pages, empty entry
    /// = always open — gates later pages behind story flags, so a journal in a
    /// Tiled map fills in as the tale is earned exactly as a C#-built one did.</summary>
    void ReadExaminables(TmxMap m)
    {
        foreach (var o in m.Objects("Interaction"))
        {
            var itype = o.Str("InteractionType", "Examine");
            if (!itype.Equals("Examine", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"tmx: Interaction '{o.Name}' has type '{itype}', which nothing implements yet — skipped");
                continue;
            }
            var pages = o.Str("Pages").Split('␟', StringSplitOptions.RemoveEmptyEntries);
            if (pages.Length == 0)
            {
                Console.Error.WriteLine($"tmx: Interaction '{o.Name}' has no Pages — skipped");
                continue;
            }
            string[]? flags = null;
            if (o.Has("PageFlags"))
            {
                // Split WITHOUT dropping empties: alignment with Pages is the point.
                flags = o.Str("PageFlags").Split('␟');
                if (flags.Length != pages.Length)
                    Console.Error.WriteLine($"tmx: Interaction '{o.Name}' has {pages.Length} pages but {flags.Length} PageFlags — unmatched pages stay open");
            }
            Examinables.Add(new Examinable
            {
                Pos = FromMap(o.X, o.Y),
                R = o.Num("Radius", 44f),
                Title = o.Name,
                Verb = o.Str("Verb", "Examine"),
                Kind = o.Str("ReadKind", "note"),
                Pages = pages,
                PageFlags = flags,
                SetsFlag = o.Str("SetsFlag"),
                // A warded text: the fate check that guards it, and what a
                // failed reading bites out of the reader. Both optional.
                CheckDC = o.Int("FateCheck"),
                CheckBite = o.Int("FateBite"),
            });
        }
    }

    /// <summary>The cave's rock, water and crossings, from its Tiled map. Terrain
    /// only: what lives down there is placed per descent by the dressing pass, so
    /// one map can serve every depth while each delve is still its own.</summary>
    void BuildCaveFromTmx(TmxMap m)
    {
        COLS = m.Width / MapSub; ROWS = m.Height / MapSub;
        Tiles = new byte[COLS, ROWS];
        Props.Clear(); Arches.Clear();

        var walls = m.Layer("Walls");
        var water = m.Layer("Water");
        var bridges = m.Layer("Bridges");
        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
            {
                int mx = x * MapSub, my = y * MapSub;
                bool Any(int[]? l) =>
                    m.Painted(l, mx, my) || m.Painted(l, mx + 1, my) ||
                    m.Painted(l, mx, my + 1) || m.Painted(l, mx + 1, my + 1);

                Tiles[x, y] = Any(walls) ? ROCK
                            : Any(bridges) ? BRIDGE      // a plank walk beats the water it crosses
                            : Any(water) ? WATER
                            : FLOOR;
            }

        // The cave mouths where a corridor leaves a room, as the map places them.
        foreach (var a in m.Objects("Arch")) Arches.Add(FromMap(a.X, a.Y));

        ReadCaveRegions(m);
    }

    /// <summary>The cave's authored dressing — Miner_Decorations cells placed by
    /// hand in Tiled, Cx/Cy naming the sheet cell — read for depth 1 in place of
    /// the procedural kits, so the Cistern can be furnished visually. Deeper
    /// floors are the delve and stay rolled.</summary>
    void ReadCaveProps(TmxMap m)
    {
        foreach (var o in m.Objects("Props"))
        {
            var p = FromMap(o.X, o.Y);
            Props.Add(new Prop
            {
                X = p.X, Y = p.Y,
                Cx = o.Int("Cx"), Cy = o.Int("Cy"),
                S = o.Num("Scale", 0.5f),
                Solid = o.Flag("Solid"),
                R = o.Num("Radius"),
                Flip = o.Flag("Flip"),
            });
        }
    }

    /// <summary>The cave's rooms, from the Region rectangles the map carries — the
    /// Descent, the Drowned Hollow, the Crystal Heart. The dressing and the
    /// rewards read these, so a room moved in Tiled moves the game with it. Left
    /// empty when the map has none, and the generator's own room graph stands.
    ///
    /// A rectangle's x/y/width/height are map pixels; an engine tile is 16 of them
    /// (a 32px cell over a 16px map). The Seed is the generator's business, so a
    /// map-read room is given none.</summary>
    void ReadCaveRegions(TmxMap m)
    {
        _caveRegions = null;
        var rooms = new List<Region>();
        foreach (var o in m.Objects("Region"))
        {
            int x0 = (int)(o.X / 16), y0 = (int)(o.Y / 16);
            int w = (int)(o.Width / 16), h = (int)(o.Height / 16);
            if (w <= 0 || h <= 0) continue;
            rooms.Add(new Region(
                o.Str("RegionKey", o.Name), o.Str("RegionName", o.Name),
                x0 + w / 2, y0 + h / 2, w / 2, h / 2, 0f));
        }
        if (rooms.Count > 0) _caveRegions = rooms;
    }
}
