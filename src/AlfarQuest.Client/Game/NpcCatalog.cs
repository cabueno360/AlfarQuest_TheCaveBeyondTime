namespace AlfarQuest.Client.Game;

/// <summary>Everyone the party can meet in Stage 1, as data. Adding a villager is
/// one entry; the placement table below decides where they stand.</summary>
public static class NpcCatalog
{
    public static readonly IReadOnlyList<NpcDefinition> All =
    [
        new() { Id = "elder", Kind = "npcOldMan", Name = "Old Halvard", Role = "Elder",
                Services = NpcServices.Quest,
                Lines = ["The mine took my son's crew. Do not go in light.",
                         "Follow the road north. It still remembers the way.",
                         "Nobody has come back up since the spring."] },
        new() { Id = "wife", Kind = "npcOldWoman", Name = "Mother Sena", Role = "Herbalist",
                Services = NpcServices.Shop,
                Lines = ["Take the herbs by the water. They keep the shakes off.",
                         "I told him not to dig so deep. He dug anyway."] },
        new() { Id = "hunter", Kind = "npcHunter", Name = "Rook", Role = "Hunter",
                Lines = ["Something moved in the treeline that wasn't a deer.",
                         "Keep to the path and you'll keep your ankles."] },
        new() { Id = "woodcutter", Kind = "npcWoodcutter", Name = "Bern", Role = "Woodcutter",
                Lines = ["Felled these myself. Marks the edge of anywhere safe.",
                         "Axe is blunt and the forest is not."] },
        new() { Id = "fisher", Kind = "npcFisherman", Name = "Old Tace", Role = "Fisherman",
                Lines = ["River's gone cold since they broke through down there.",
                         "Caught nothing but stones for a week."] },
        new() { Id = "alchemist", Kind = "npcAlchemist", Name = "Perrin", Role = "Alchemist",
                Services = NpcServices.Shop | NpcServices.Crafting,
                Lines = ["Bring me crystal shards and I'll make them useful.",
                         "The glow down there isn't mineral. I've tested it."] },
        new() { Id = "smith", Kind = "npcBlacksmith", Name = "Dagna", Role = "Blacksmith",
                Services = NpcServices.Shop | NpcServices.Crafting,
                Lines = ["Your edge is dull. Everything down there is not.",
                         "I shod the carts that went in. None came back for repair."] },
        new() { Id = "wagoner", Kind = "npcWagoner", Name = "Cott", Role = "Wagon Driver",
                Lines = ["Hauled ore out of that mouth for nine years.",
                         "Wouldn't take the cart past the graves now."] },
        new() { Id = "boy", Kind = "npcBoy", Name = "Little Add", Role = "Village Child",
                Lines = ["Da says the mine sings at night. I heard it too.",
                         "Are you going in? Really in?"] },
        new() { Id = "girl", Kind = "npcGirl", Name = "Wren", Role = "Village Child",
                Lines = ["There's a hollow behind the bushes. Don't tell.",
                         "I found a blue rock. It was warm."] },
        new() { Id = "guard", Kind = "npcGuard", Name = "Serjeant Vosk", Role = "Guard",
                Services = NpcServices.Quest,
                Lines = ["Past the graves you're on your own. That's the rule.",
                         "Sign's there for a reason. Read it twice."] },
        new() { Id = "farmwife", Kind = "npcFarmwife", Name = "Ilsa", Role = "Farmer",
                Lines = ["Crops came up grey this year. Draw your own conclusion.",
                         "You'll want a full stomach before that dark."] },
    ];

    /// <summary>Where each one stands, in tile coordinates. Placed by hand so the
    /// village reads as a place people chose to live, not a scatter.</summary>
    public static readonly (string Id, int X, int Y)[] Placements =
    [
        ("elder",      15, 68),   // by the campfire
        ("wife",       11, 71),
        ("farmwife",   18, 73),
        ("boy",        16, 64),
        ("girl",       19, 66),
        ("smith",      20, 62),
        ("alchemist",  12, 64),
        ("woodcutter", 24, 58),   // out along the road
        ("hunter",     27, 52),   // near the bridge
        ("fisher",     30, 55),   // at the water
        ("wagoner",    45, 37),   // the crossroad
        ("guard",      37, 23),   // last post before the graves
    ];

    public static NpcDefinition? Find(string id) => All.FirstOrDefault(n => n.Id == id);
}
