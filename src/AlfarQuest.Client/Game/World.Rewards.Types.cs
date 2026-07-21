using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  What a reward *is*. Kept apart from the code that awards them so the
//  shapes can be read without the machinery, and so adding a kind of
//  reward starts in an obvious place.
//
//  Both are nested in World on purpose: they are meaningless outside a
//  world, and nesting says so without a namespace of its own.
// =====================================================================
public partial class World
{
    /// <summary>Somewhere worth finding. Entering the radius once pays.</summary>
    public sealed class Discovery(string name, Vec pos, float radius, XpSource source)
    {
        public string Name { get; } = name;
        public Vec Pos { get; } = pos;
        public float Radius { get; } = radius;
        public XpSource Source { get; } = source;
        public bool Found;
    }

    /// <summary>Something to open, mine or read. Sits on a prop that is already
    /// in the world, so nothing extra is drawn — only the prompt and the mote
    /// above it while it is still untouched.</summary>
    public sealed class Interactable(string name, Vec pos, XpSource source, string? loot = null, int lootCount = 1)
    {
        public string Name { get; } = name;
        public Vec Pos { get; } = pos;
        public XpSource Source { get; } = source;
        public string? Loot { get; } = loot;
        public int LootCount { get; } = lootCount;
        public bool Used;
    }
}
