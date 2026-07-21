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

    /// <summary>Something in the world to open, search, mine or read.
    ///
    /// The kind carries the behaviour — what it is called, what verb the prompt
    /// offers, what can be inside, whether it needs a key and whether it ever
    /// comes back. This class only holds where it is and what has happened to it,
    /// which is why adding a wardrobe touches the catalogue and not this file.</summary>
    public sealed class Interactable(string name, Vec pos, ContainerKind kind)
    {
        public string Name { get; } = name;
        public Vec Pos { get; } = pos;
        public ContainerKind Kind { get; } = kind;

        /// <summary>Rolled the first time it is opened, then kept. Rolling again
        /// on every open would let a player reload for a better result, and
        /// rolling at world build would spend the work on containers nobody ever
        /// walks to — there are dozens of them.</summary>
        public LootStack? Contents;

        /// <summary>True once it has been opened, whatever was inside. What is
        /// left in <see cref="Contents"/> is still takeable — an opened chest with
        /// something still in it is a chest you can come back to.</summary>
        public bool Opened;

        /// <summary>When it was opened, for the kinds that come back. Game-world
        /// seconds are not wall time, so this is stored as a UTC stamp: a barrel
        /// that refills in half an hour should refill while the game is closed.</summary>
        public DateTime? OpenedAt;

        /// <summary>Whether it is worth walking up to right now.
        ///
        /// Not simply "unopened": a chest with something still in it is worth
        /// coming back to, and one that is empty is not — even if it will refill
        /// later. A container that kept inviting you to open it for nothing is
        /// worse than no container, and the world has forty of them.</summary>
        public bool Offers => !Opened || !(Contents?.IsEmpty ?? true);

        /// <summary>Long enough closed to hold something again. Only the kinds
        /// that come back ever answer true.</summary>
        public bool DueToRefill(DateTime now) =>
            Opened && (Contents?.IsEmpty ?? true)
            && Kind.RespawnHours is { } hours
            && OpenedAt is { } at && (now - at).TotalHours >= hours;

        /// <summary>Forgets it was ever opened, so the next visitor rolls it
        /// fresh. Rolling here instead would spend the work on a barrel nobody
        /// walks back to.</summary>
        public void Refill()
        {
            Opened = false;
            OpenedAt = null;
            Contents = null;
        }
    }
}
