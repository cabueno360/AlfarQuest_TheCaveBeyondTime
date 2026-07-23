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
        new() { Id = "trader", Kind = "npcMerchant", Name = "Sella of the Road", Role = "Traveling Merchant",
                Services = NpcServices.Shop,
                Lines = ["I go where the roads still hold. This one, not much longer.",
                         "Bought it in one town, I'll sell it in the next. That's the trade."] },
        // A rare find — tucked away, and worth the walk. The one merchant a player
        // has to go looking for.
        new() { Id = "scholar", Kind = "npcLibrarian", Name = "The Keeper", Role = "Magic Scholar",
                Services = NpcServices.Shop,
                Lines = ["I keep what the mine gave up before it turned. For a price.",
                         "You feel the light down there too, don't you. It is not mineral."] },
        // ---- from the book "Story for Music" ----------------------------
        // Cerno of Kaladash — the adventurer who found the panacea in the Cave and
        // came back half-mad, the one soul here who has been down and returned.
        new() { Id = "cerno", Kind = "npcHunter", Name = "Cerno", Role = "Adventurer of Kaladash",
                Services = NpcServices.Quest,
                Lines = ["This cave of myth is real. I drew the panacea from its very halls, and left the rest of my mind behind.",
                         "I turned back too soon. What waits below, no man was meant to carry back out.",
                         "You mean to descend? Then hear me plainly — you will descend into madness.",
                         "There are three roads from the crossing: east to Kae Ychel and its Academy, west to Seoshe on the coast, north to the Vale. All of them safer than this one."] },
        // Mirka's father — sent his son-in-law, the Cleric, into the dark to save
        // his dying daughter; keeps her house on the hill while the priest is gone.
        // The concept art's "ideas of conversation", made literal: he answers a menu
        // of questions rather than cycling one-liners, since the priest himself is
        // gone below and he is the only one left who can tell any of it. Lines are
        // kept as the fallback for a headless run with no dialogue window attached.
        new() { Id = "mirkafather", Kind = "npcOldMan", Name = "Mirka's Father", Role = "Villager of the Vale",
                Greeting = "You'll be the delvers, then. Come in — she is asleep, she is always asleep. Ask what you like; I have little left to do but answer.",
                Topics =
                [
                    new("Who are you?",
                        "I am her father — Mirka's. This is her house, and her husband's: the cleric who kept this valley in prayer. I keep the hearth lit while he is gone."),
                    new("Tell me about your daughter.",
                        "She married the priest for love, not for station. \"Hair of gold more brilliant than any gilding,\" he wrote of her.",
                        "The wasting has taken even that. She wakes minutes a day now, if that. Go up and sit with her if you like — she does not mind visitors."),
                    new("What happened to this village?",
                        "The Vale is dying, stranger. Crops come up grey, sheep freeze in green pasture, and each winter fewer of us are left to bury the rest.",
                        "They light the corpse-pyres on the far slope now. You will smell them before you see them."),
                    new("What is the Wasting?",
                        "No fever you can sweat out. It draws the colour from a body, then the strength, then the waking hours — until only breath is left.",
                        "It took her by inches. He tried prayer, ritual, divination, every miracle he was blessed with. It did not so much as slow."),
                    new("Can I help?",
                        "If you are truly going down into that cave — find the cleric in the gilded plate, and tell him she still breathes. That is help enough."),
                    new("What is that journal?",
                        "He kept it at her bedside, in his own careful hand. Every remedy he tried, every prayer, every bargain.",
                        "Read it, if you have the stomach. It is a year of a man's hope running out, written down."),
                    new("What lies beyond the forest?",
                        "Three roads run from the crossing: east to the Academy at Kae Ychel, west to Seoshe on the coast, north deeper into the Vale.",
                        "And below the graves, the old mine — and past that, the cave. Every one of them safer than the last."),
                    new("Have you heard of the Cave Beyond Time?",
                        "I did not believe in it either, till Cerno set a phial of its water into my hand.",
                        "They say time runs strange down there, and that men who go in come back changed — if they come back at all. My son-in-law has not."),
                    new("Who is Cerno?",
                        "Cerno of Kaladash. A warrior, grim and honest. He brought the panacea up out of the dark, and left half his mind behind to do it.",
                        "It was he who led my son-in-law down. I asked him to. God forgive me — I asked him to."),
                    new("What is the Panacea?",
                        "Six drops, drawn from the Cave. It slowed the wasting a month — her fingers looked human again, and she woke, and she knew me.",
                        "Then it stopped. The gift was only a loan. And Cerno's price for those drops was the cleric's own descent into madness."),
                    new("Why would a priest wear armour?",
                        "He was a war-cleric, called to the front once — Blazwitz and Koerig against Tellaran, and healers called up faster than the Order could send them.",
                        "He wore the plate down into the cave. Said a prayer is worth less there than a good swing."),
                ],
                Lines = ["I am her father — Mirka's. This is her house, and her husband's, the cleric who kept this valley in prayer. I keep the hearth lit while he is gone.",
                         "My daughter married the priest for love, not station. 'Hair of gold more brilliant than any gilding,' he wrote of her. The wasting has taken even that.",
                         "The Vale is dying, stranger. Crops come up grey, sheep freeze in green pasture, and each winter fewer of us are left to bury the rest.",
                         "The wasting is no fever you sweat out. It draws the colour from a body, the strength, the waking hours — until only breath is left. It took her by inches.",
                         "If you are truly going down into that cave — find the cleric in the gilded plate, and tell him she still breathes. That is help enough.",
                         "He kept a journal at her bedside, in his own hand. Every remedy, every prayer. Read it, if you've the stomach — it is a year of a man's hope running out.",
                         "Beyond the treeline, three roads run from the crossing: east to the Academy at Kae Ychel, west to Seoshe on the coast, north deeper into the Vale. And below the graves, the old mine.",
                         "The Cave Beyond Time — I did not believe in it either, till Cerno set a phial of its water in my hand. They say time runs strange down there, and men come back changed.",
                         "Cerno of Kaladash — a warrior, grim and honest. He brought the panacea up out of the dark and left half his mind behind to do it. It was he who led my son-in-law down.",
                         "The panacea: six drops, drawn from the Cave. It slowed the wasting one month, no more. Cerno's price for those drops was the cleric's own descent into madness.",
                         "You wonder at a priest in gilded plate? He was a war-cleric, called to the front once. He wore it down into the cave — said a prayer is worth less there than a good swing."] },
        // Mirka — the Cleric's dying wife, kept in her upstairs room. She is far
        // past speech; her "lines" are what you see, watching over her.
        new() { Id = "mirka", Kind = "npcFarmwife", Name = "Mirka", Role = "The Cleric's Wife",
                Bedridden = true,   // drawn lying in the bed prop, not standing beside it
                Lines = ["(She sleeps, her breath shallow. The wasting has drawn the gold from her hair and the warmth from her hands.)",
                         "(On the nightstand: a wedding band grown too loose for her fingers, and the Cleric's journal, left open.)",
                         "(For a heartbeat her eyes flutter — then she is gone again, somewhere the panacea cannot follow.)"] },
        // A fisher on the coast road, come up from Seoshe's Low Docks.
        new() { Id = "coastfisher", Kind = "npcFisherman", Name = "Old Nets", Role = "Fisher of the Coast Road",
                Lines = ["Followed the river up from Seoshe. The catch there's gone strange since the crystal trade started.",
                         "West and south, past the stones, the road runs down to the crescent shore. Mind the smugglers on the way.",
                         "The Neruum flotilla's due at the Low Docks by month's end. Spices, pelts — and worse, if the rumours hold."] },
        // The Academy outpost on the east road, toward Kae Ychel.
        // The grandmaster, inside the hall — placed by the interior builder.
        new() { Id = "grandmaster", Kind = "npcMage", Name = "The Grandmaster", Role = "Academy of Kae Ychel",
                Lines = ["This is but an outpost. The Academy itself takes up a quarter of Kae Ychel — gardens, towers, libraries, the greatest school on the continent.",
                         "Each midsummer the finest hundred are tested before the Twin Sun King's avatar. Ten receive his boon. It is the making of a mage, or the breaking of one.",
                         "We had an apprentice once who reached past his grasp. He bound a greater demon at the tests, and it took half his year with it. The Regent sealed the thing in his heart and cast him out. We do not say his name.",
                         "If you are bound for the mine, mage-craft will not save you down there. That is not our kind of magic. It is not anyone's."] },
        // Apprentices walking the courtyard outside.
        new() { Id = "apprentice_ward", Kind = "npcMage", Name = "An Apprentice", Role = "Of the Academy",
                Lines = ["Wards, always wards. Boring, the others say. Boring keeps you alive when a working turns on you.",
                         "Gersimo did wards too. He put one up at the tests, the year of the fire. It did not matter. Nothing did.",
                         "A five-point array is stable. Fewer points and it wants to come apart in your hands. Never trust a three-point array — whatever they tell you about prodigies."] },
        new() { Id = "apprentice_fire", Kind = "npcBoy", Name = "An Apprentice", Role = "Of the Academy",
                Lines = ["Lightning's the thing. A good enough show at the tests and you're Evoker Corps, the King's own war-mages.",
                         "Thami tried lightning three years running. They say he cared more for the Corps than the boon itself. They say a lot about Thami now.",
                         "Fire and force. Who wants to spend their life warding doors that were never going to open?"] },
        new() { Id = "apprentice_scry", Kind = "npcGirl", Name = "An Apprentice", Role = "Of the Academy",
                Lines = ["I'm reading up on scrying spheres. A three-point array to hold one — mad, unstable, the grandmasters gasp when anyone manages it.",
                         "There's a whole shelf gone from the library. Bindings, summonings. Chained shut, and still someone took them, years back.",
                         "You feel it too, out east? The air's wrong past the caravan. Like the light down in that mine, only... reaching."] },
        // ---- Seoshe, the crescent-coast city (placed inside its own map) ----
        new() { Id = "seoshe_guard", Kind = "npcGuard", Name = "A Harbour Watchman", Role = "Guard of Seoshe",
                Lines = ["Welcome to Seoshe. Keep your purse close and your questions closer.",
                         "My captain drinks on the crews' coin, and the crews drink on his. That's the whole of the law down here.",
                         "There was a fire at the docks, months back. A whole crew gone, and a warehouse with them. We don't dig into it. Neither should you."] },
        new() { Id = "seoshe_burgher", Kind = "npcNoble", Name = "A Burgher", Role = "Of High Street",
                Lines = ["A fine city, Seoshe — if you keep to High Street and don't ask what the Low Docks import.",
                         "The Neruum flotilla's due. Spices, silks. Half of it above board, and the other half is why the guard looks the other way."] },
        new() { Id = "seoshe_noble", Kind = "npcNoble", Name = "A Baron of the Hill", Role = "Baron Hill",
                Lines = ["Baron Hill. Manors and small palaces, and gates that do not open to the likes of the harbour crowd.",
                         "House Cayhall has shuttered its doors. A ship of theirs came in carrying nothing, and burned a warehouse the night it docked. Draw your own conclusions; I have drawn mine."] },
        new() { Id = "seoshe_sailor", Kind = "npcFisherman", Name = "A Dock Hand", Role = "The Low Docks",
                Lines = ["Off the Neruum flotilla, me. Spices, pelts — and once, a red lacquer box that half the harbour would've killed for.",
                         "The crews here were the best on the coast. Then one night they weren't anything at all. Silver-blue ash, and a boss who walked out and kept walking."] },
        new() { Id = "seoshe_trader", Kind = "npcMerchant", Name = "A Neruum Trader", Role = "Market of Seoshe",
                Services = NpcServices.Shop,
                Lines = ["Off the flotilla, everything you see. Ask no prices you're not ready to pay.",
                         "Foreign goods, foreign luck. Both spend the same."] },
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
        ("scholar",    52, 26),   // out past the mining camp — a walk to find
        ("cerno",      43, 12),   // at the cave forecourt — the guide who has been down
        ("mirkafather", 22, 26),  // on the path below the Cleric's house
        ("trader",    116, 55),   // the traveling merchant, at the caravan on the east road
        ("coastfisher", 48, 107), // at the fishing steps on the coast road
        ("apprentice_ward", 92, 48),  // the Academy outpost courtyard, east road
        ("apprentice_fire", 99, 47),
        ("apprentice_scry", 96, 51),
        ("seoshe_guard",   28, 77),   // the gate of Seoshe, on the coast road
        ("seoshe_guard",   34, 77),
    ];

    public static NpcDefinition? Find(string id) => All.FirstOrDefault(n => n.Id == id);
}
