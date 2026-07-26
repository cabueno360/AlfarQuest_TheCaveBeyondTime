namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 1 — "The Approach".
//
//  The overworld is the ring of authored regions now — see
//  Overworld.Regions.cs (the ring and the seams) and Overworld.Tmx.cs
//  (reading a .tmx into a world). This file holds only what those share:
//  the cave-mouth field, the prop helper the interiors furnish with, and
//  a last-resort fallback for the case where no region map loads at all.
//
//  The old procedural generator that used to build Stage 1 here — some
//  twenty Build* passes across Overworld.Detail/Expanse/Terrain/Zones/
//  Rewards.cs — has been retired: the regions replaced it and it had
//  become dead weight nothing ran. What is left below is not that world,
//  only a guarantee that a missing shipped map yields a place to stand
//  instead of a crash.
// =====================================================================
public partial class World
{
    public const string OverworldName = "The Approach";

    // Where the mine mouth sits — set by whichever region carries it, and by the
    // fallback below when there is no region at all.
    public Vec CaveMouth;

    /// <summary>Last resort only: reached when a region map failed to load, which
    /// means a shipped asset is missing and should never happen in a real build.
    /// The authored regions are the overworld; this just lays down a flat walkable
    /// field with a spawn and a mouth, so the game opens on solid ground rather than
    /// falling over. No props, no NPCs, no story — those live in the maps.</summary>
    void BuildOverworld()
    {
        Rev++;
        Stage = 1;
        CurrentRegion = null;
        COLS = 72; ROWS = 56;
        Tiles = new byte[COLS, ROWS];
        Props.Clear(); Arches.Clear(); Crystals.Clear(); Husks.Clear();
        Portals.Clear(); Examinables.Clear(); Reading = null;
        Npcs.Clear(); Interactables.Clear(); Discoveries.Clear();

        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
                Tiles[x, y] = FLOOR;

        Spawn = TileCentre(COLS / 2, ROWS - 6);
        CaveMouth = TileCentre(COLS / 2, 4);
        Exit = CaveMouth;
        Camera = Spawn;
    }

    // A prop dropped into whichever world is standing. Shared with the interiors,
    // which furnish themselves through it (see InteriorCatalog) — the one piece of
    // the old placement code that outlived the generator.
    public void AddOw(float tx, float ty, string kind, float scale, bool solid, float radius)
    {
        Props.Add(new Prop
        {
            X = tx * TILE + TILE * 0.5f,
            Y = ty * TILE + TILE * 0.5f,
            Kind = kind,
            Variant = _rng.Next(64),        // client wraps this into the kind's list
            S = scale,
            Solid = solid,
            R = radius,
            Flip = _rng.Next(2) == 0,
        });
    }
}
