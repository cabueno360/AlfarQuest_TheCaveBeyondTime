using AlfarQuest.Client.Game.Tiled;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  The hand-authored overworld regions.
//
//  Stage 1 is being rebuilt as a ring of four regions — Ashwold, the Whispering
//  Wood, Deepdelve and the Kae Ychel road — each its own .tmx, each cut from one
//  logical world so that roads, rivers, woods and rock continue across the seams
//  by construction rather than by hand-matching.
//
//  A region is an overworld: same stage, same rules, same readers. The only new
//  thing here is which map is standing, and that a region names its neighbours
//  so the party can walk out of one and into the next.
//
//  Until the ring closes, this is reachable only through the debug seam. A
//  region you cannot leave and a cave you cannot reach are not a game, and the
//  build has to stay playable between iterations.
// =====================================================================
public partial class World
{
    /// <summary>The region currently standing, or null when Stage 1 is being
    /// served by its own map or by the generator underneath it.</summary>
    public string? CurrentRegion { get; private set; }

    /// <summary>What the standing region calls itself. The HUD's one region name
    /// (World.Cave) reads this when a region is up, so a place is named by its own
    /// map instead of by a constant in the code.</summary>
    public string RegionTitle { get; private set; } = "";

    /// <summary>The regions this one adjoins, by map id. Empty means the world
    /// ends that way and the party is simply clamped, as it always was.</summary>
    string _north = "", _south = "", _east = "", _west = "";

    /// <summary>Whether the last map change was a step across a seam rather than
    /// a door. The renderer fades on a door and must not fade on a seam — the
    /// whole point of cutting one world into four is that you cannot tell.</summary>
    public bool CrossedSeam { get; private set; }

    /// <summary>Stands a region up. Everything about it is read from its map:
    /// the same readers the overworld already uses, over the same terrain rules,
    /// plus the four neighbours it names.</summary>
    public bool LoadRegion(string id, Vec? arriveAt = null)
    {
        if (MapCatalog.Find(id) is not { } m) return false;

        CurrentInterior = null;
        CurrentRegion = id;
        Stage = 1;
        Phase = "playing";
        Talking = null; TradingWith = null;

        BuildOverworldFromTmx(m);

        RegionTitle = m.Property("DisplayName", id);
        _north = m.Property("NorthMap");
        _south = m.Property("SouthMap");
        _east = m.Property("EastMap");
        _west = m.Property("WestMap");

        var at = arriveAt ?? Spawn;
        foreach (var h in Party) h.Pos = at;
        Camera = at;
        return true;
    }

    /// <summary>Walks the party out of one region and into the next.
    ///
    /// The arriving position is the departing one mirrored across the seam, with
    /// the perpendicular coordinate kept — so a road that leaves this map at a
    /// given height enters the next one at the same height, and stepping over
    /// costs the player nothing but a stride.</summary>
    bool CrossSeam(string to, int dx, int dy)
    {
        if (to.Length == 0 || MapCatalog.Find(to) is not { } m) return false;

        var hero = Party[Active].Pos;
        int cols = m.Width / MapSub, rows = m.Height / MapSub;
        // Two cells in from the far edge. One was not enough: the arriving
        // position has to clear the OTHER side's trigger band by a margin, or a
        // party that walks north lands inside the southern band and is bounced
        // straight back, forever.
        const float Inset = TILE * 2f;
        var at = new Vec(
            dx > 0 ? Inset : dx < 0 ? cols * TILE - Inset : Math.Clamp(hero.X, Inset, cols * TILE - Inset),
            dy > 0 ? Inset : dy < 0 ? rows * TILE - Inset : Math.Clamp(hero.Y, Inset, rows * TILE - Inset));

        if (!LoadRegion(to, at)) return false;
        CrossedSeam = true;
        return true;
    }

    /// <summary>Checked once a frame, after the party has moved. A hero pressed
    /// up against the edge of the map with a region on the other side steps
    /// through; with nothing on the other side, the old clamp still holds them.</summary>
    void UpdateSeams()
    {
        if (CurrentRegion is null || Party.Count == 0 || Busy) return;
        var p = Party[Active].Pos;
        // The hero is already clamped to a full cell in from the wall
        // (World.Collision.Clamp, pad = TILE), so a band NARROWER than that can
        // never be reached and the seam never fires. It has to be wider than the
        // clamp, not narrower than the map.
        const float Edge = TILE * 1.15f;

        if (p.X <= Edge && CrossSeam(_west, -1, 0)) return;
        if (p.X >= COLS * TILE - Edge && CrossSeam(_east, 1, 0)) return;
        if (p.Y <= Edge && CrossSeam(_north, 0, -1)) return;
        if (p.Y >= ROWS * TILE - Edge) CrossSeam(_south, 0, 1);
    }

    /// <summary>Consumed by the snapshot: a seam is news for exactly one frame.</summary>
    public bool TakeCrossedSeam()
    {
        var was = CrossedSeam;
        CrossedSeam = false;
        return was;
    }
}
