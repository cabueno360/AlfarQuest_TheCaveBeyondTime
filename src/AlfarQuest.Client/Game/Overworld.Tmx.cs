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
        // off it — the husks gather where the cold comes out.
        if (m.Objects("CaveMouth").FirstOrDefault() is { } cm0) CaveMouth = FromMap(cm0.X, cm0.Y);

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
        if (m.Objects("PlayerSpawn").FirstOrDefault() is { } sp) Spawn = FromMap(sp.X, sp.Y);
        if (m.Objects("CaveMouth").FirstOrDefault() is { } cm) CaveMouth = FromMap(cm.X, cm.Y);
        Exit = CaveMouth;
        Camera = Spawn;
    }

    /// <summary>A building's interior, from its Tiled map. The same readers as the
    /// overworld — an NPC, a door, a chest and a thing to read are the same objects
    /// wherever they stand — over a simpler terrain rule: a room is walls and floor.
    ///
    /// Called by LoadInterior in place of the C# builder when the interior has a
    /// map. The def still supplies the name, the daylight flag and the floor
    /// material, because those describe the place rather than its geometry.</summary>
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
                            : Any(water) ? WATER
                            : Any(bridges) ? BRIDGE
                            : Any(roads) ? PATH
                            : FLOOR;
            }

        ReadProps(m);
        ReadNpcs(m);
        ReadWarps(m);
        ReadExaminables(m);
        ReadContainers(m);

        if (m.Objects("PlayerSpawn").FirstOrDefault() is { } sp) Spawn = FromMap(sp.X, sp.Y);
    }

    /// <summary>Terrain by layer precedence: whatever is painted highest wins.
    /// Ground is the bed everything else sits on, so an unpainted cell is floor.</summary>
    void ReadTerrain(TmxMap m)
    {
        var cliffs = m.Layer("Cliffs");
        // A building's FACADE is on the Walls layer, and the wall you can see is
        // the wall you cannot walk through. The roof above it lives on Buildings,
        // which blocks nothing, so the eaves overhang the doorstep the way eaves
        // do instead of fencing it off.
        var walls = m.Layer("Walls");
        var water = m.Layer("Water");
        var bridges = m.Layer("Bridges");
        var roads = m.Layer("Roads");

        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
            {
                // Any of the cell's 2x2 map cells painted counts as painted, so a
                // map drawn at 16px resolution never leaves half-blocking cells.
                int mx = x * MapSub, my = y * MapSub;
                bool Any(int[]? l) =>
                    m.Painted(l, mx, my) || m.Painted(l, mx + 1, my) ||
                    m.Painted(l, mx, my + 1) || m.Painted(l, mx + 1, my + 1);

                Tiles[x, y] = Any(cliffs) || Any(walls) ? ROCK
                            : Any(water) ? WATER
                            : Any(bridges) ? BRIDGE
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

    void ReadNpcs(TmxMap m)
    {
        foreach (var o in m.Objects("NPCSpawn"))
        {
            var id = o.Str("NpcId", o.Name);
            if (NpcCatalog.Find(id) is not { } def) continue;
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
            if (CreatureCatalog.All.FirstOrDefault(c => c.Id == id) is not { } def) continue;
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
            Portals.Add(new Portal
            {
                Pos = FromMap(o.X, o.Y),
                Target = o.Str("DestinationMap", Portal.Overworld),
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
            if (ContainerKind.Find(o.Str("ContainerKind")) is not { } kind) continue;
            Interactables.Add(new Interactable(o.Name, FromMap(o.X, o.Y), kind));
        }
    }

    void ReadDiscoveries(TmxMap m)
    {
        foreach (var o in m.Objects("Discovery"))
        {
            if (!Enum.TryParse<XpSource>(o.Str("XpSource"), out var source)) source = XpSource.RegionDiscovered;
            Discoveries.Add(new Discovery(o.Name, FromMap(o.X, o.Y), o.Num("Radius", 90f), source));
        }
    }

    /// <summary>Things to read out in the world. Pages are held in one property,
    /// separated by a unit separator, because Tiled has no list type.</summary>
    void ReadExaminables(TmxMap m)
    {
        foreach (var o in m.Objects("Interaction"))
        {
            if (!o.Str("InteractionType", "Examine").Equals("Examine", StringComparison.OrdinalIgnoreCase)) continue;
            var pages = o.Str("Pages").Split('␟', StringSplitOptions.RemoveEmptyEntries);
            if (pages.Length == 0) continue;
            Examinables.Add(new Examinable
            {
                Pos = FromMap(o.X, o.Y),
                R = o.Num("Radius", 44f),
                Title = o.Name,
                Verb = o.Str("Verb", "Examine"),
                Kind = o.Str("ReadKind", "note"),
                Pages = pages,
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
                            : Any(water) ? WATER
                            : Any(bridges) ? BRIDGE
                            : FLOOR;
            }

        // The cave mouths where a corridor leaves a room, as the map places them.
        foreach (var a in m.Objects("Arch")) Arches.Add(FromMap(a.X, a.Y));
    }
}
