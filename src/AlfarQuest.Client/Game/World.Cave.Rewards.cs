using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 2 — where the cave's experience is.
//
//  Unlike the overworld's, these cannot be hand-placed: the cave is
//  generated, so the rewards are derived from what the generator
//  actually produced. That is why this runs after the dressing pass.
// =====================================================================
public partial class World
{
    /// <summary>Stage 2's rewards. The cave is generated, so unlike the overworld
    /// these cannot be hand-placed — they are derived from what the generator
    /// actually produced, which is why this runs after the dressing pass.</summary>
    void PlaceCaveRewards()
    {
        Discoveries.Clear();
        Interactables.Clear();

        // Each named region pays once for being reached. Radius from the room's
        // own size, so a large hall is not harder to "discover" than a closet.
        foreach (var r in Regions)
            Discoveries.Add(new Discovery(
                r.Name,
                TileCentre(r.Cx, r.Cy),
                Math.Min(r.Rx, r.Ry) * TILE * 0.8f,
                XpSource.RegionDiscovered));

        // A few of the spires are worth prising apart. Spread across the list
        // rather than taken from the front, so they are not all in one chamber.
        if (Crystals.Count == 0) return;
        var step = Math.Max(1, Crystals.Count / 4);
        for (int i = 0; i < Crystals.Count; i += step)
            Interactables.Add(new Interactable(
                $"Rich Seam {i}", Crystals[i].Pos, ContainerKind.Find("crystal")!));
    }
}
