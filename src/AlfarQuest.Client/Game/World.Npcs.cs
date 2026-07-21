namespace AlfarQuest.Client.Game;

// =====================================================================
//  Villagers and talking to them.
//
//  Interaction is a two-state machine on purpose: in range shows a prompt,
//  pressing the key opens a line, pressing it again closes. Nothing about
//  it assumes a single sentence, so a dialogue tree can replace
//  CurrentLine without touching the engine or the renderer.
// =====================================================================
public partial class World
{
    public List<Npc> Npcs { get; } = [];

    /// <summary>Whoever the active hero could talk to, or null.</summary>
    public Npc? NpcInReach { get; private set; }

    /// <summary>Whoever they are talking to, or null.</summary>
    public Npc? Talking { get; private set; }

    /// <summary>Talking holds the hero still — walking away mid-sentence would
    /// leave the balloon hanging over an empty patch of grass.</summary>
    public bool IsTalking => Talking is not null;

    const float TalkRange = 56f;

    void PlaceNpcs()
    {
        Npcs.Clear();
        foreach (var (id, x, y) in NpcCatalog.Placements)
        {
            if (NpcCatalog.Find(id) is not { } def) continue;
            var pos = TileCentre(x, y);
            // Never strand a villager inside rock or water; nudge to open ground.
            if (Blocked(pos, 14f)) pos = NearestOpen(pos);
            Npcs.Add(new Npc(def, pos));
        }
    }

    Vec NearestOpen(Vec from)
    {
        for (float r = TILE; r <= TILE * 6; r += TILE)
            for (int a = 0; a < 12; a++)
            {
                var p = from + new Vec(MathF.Cos(a * MathF.Tau / 12) * r, MathF.Sin(a * MathF.Tau / 12) * r);
                if (!Blocked(p, 14f)) return p;
            }
        return from;
    }

    void UpdateNpcs()
    {
        if (Party.Count == 0) { NpcInReach = null; return; }
        var hero = Party[Active];

        NpcInReach = null;
        var best = TalkRange;
        foreach (var n in Npcs)
        {
            var d = (n.Pos - hero.Pos).Len();
            if (d < best) { best = d; NpcInReach = n; }
        }

        // Step out of range and the conversation ends by itself.
        if (Talking is not null && (Talking.Pos - hero.Pos).Len() > TalkRange * 1.6f) Talking = null;
    }

    /// <summary>What [E] does. One key, three meanings, resolved in the order a
    /// player would expect: finish the sentence you are reading, then use the
    /// thing at your feet, then greet whoever is standing there.</summary>
    public void Interact()
    {
        if (Talking is not null)
        {
            Talking.LineIndex++;          // next visit gets the next line
            Talking = null;
            return;
        }

        // Chests, seams and tablets take precedence over a nearby villager: if
        // both are in reach the player is almost certainly standing on the chest
        // deliberately, whereas the villager is scenery they walked past.
        if (UseThingInReach()) return;

        if (NpcInReach is null) return;

        Talking = NpcInReach;
        var hero = Party[Active];
        var to = hero.Pos - Talking.Pos;
        if (to.Len() > 0.01f) Talking.Facing = MathF.Atan2(to.Y, to.X);
    }
}
