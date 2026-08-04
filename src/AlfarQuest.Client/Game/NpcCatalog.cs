namespace AlfarQuest.Client.Game;

/// <summary>Everyone the party can meet in Stage 1, as data. Adding a villager is
/// one entry; the placement table below decides where they stand.</summary>
public static class NpcCatalog
{
    public static readonly IReadOnlyList<NpcDefinition> All =
    [
        // ---- the Kae Ychel road, and the caravan on it ----
        new() { Id = "caravan_master", Kind = "npcWagoner", Name = "Ondu the Long-Hauler", Role = "Caravan Master",
                Lines = ["Kae Ychel is nine days east if the wells hold. Eleven if they do not.",
                         "I water here every trip. The old road knew where to put an oasis.",
                         "Silk and glass out, ore and hides back. That is the whole of the road."] },
        new() { Id = "road_priest", Kind = "npcPriest", Name = "Sister Vell", Role = "Of the Twin Sun",
                Lines = ["The Sun King's light falls hardest on this road. That is why it is a hard road.",
                         "I walk to Kae Ychel and back, and I bless the wells at every stop.",
                         "The ruins you passed were a city once, before the sun found it."] },

        // ---- Deepdelve, the pit under the pass ----
        // What is left of a working mine after the thing below it stopped giving
        // ore and started taking crews.
        new() { Id = "pit_captain", Kind = "npcBlacksmith", Name = "Captain Orlo", Role = "Pit Captain",
                Lines = ["I sign them in and I sign them out. The second column has gone quiet.",
                         "The seam was good for nine years. Then it was not a seam any more.",
                         "You want the mouth, it is up the switchback. I will not walk you to it."] },
        new() { Id = "winch_hand", Kind = "npcWagoner", Name = "Bel", Role = "Winch Hand",
                Lines = ["The gear is greased and the drum is sound. Nothing to wind up.",
                         "Last cage I sent down came back up with the rope cut clean. Cut, not frayed.",
                         "Load's still on that cart. Been on it since the feast day."] },
        new() { Id = "stranded_delver", Kind = "npcMerchant", Name = "Yeska", Role = "Delver",
                Services = NpcServices.Shop,
                Lines = ["I sell what the last lot did not need. They will not be needing it.",
                         "Rope, oil, a good lamp. Go down without all three and do not come back.",
                         "I have been at this mouth eleven days. I have not gone in."] },

        // ---- the Whispering Wood, and the chapel hamlet in its vale ----
        // The wood's people are not the village's people: they live off it, not
        // off the road, and they all know somebody the cave has taken.
        new() { Id = "collier", Kind = "npcWoodcutter", Name = "Ash-Hand Weyl", Role = "Charcoal Burner",
                Lines = ["A kiln wants three days and no sleep. Rush it and you get smoke and grief.",
                         "Ashwold's forge burns what I make. That is the whole of my trade.",
                         "The wood is quieter this year. I do not say that as a good thing."] },
        new() { Id = "forester", Kind = "npcHunter", Name = "Ilsa", Role = "Forester",
                Lines = ["Stay on the road above the ford. Below it the ground lies to you.",
                         "I mark the trees that are safe to fell. Lately I have been marking fewer.",
                         "Deer will not cross the high path any more. Ask yourself what taught them that."] },
        new() { Id = "chapel_keeper", Kind = "npcPriest", Name = "Brother Enoch", Role = "Chapel Keeper",
                Greeting = "Peace of the Dawn on you, travellers. You have the look of the road about you — and of the road's end, the one that climbs to the mine. Sit. Ask what you like.",
                Topics =
                [
                    new("Who are you?",
                        "Brother Enoch, keeper of this poor chapel of the Holy Light — the Bringer of Dawn, King of all Kings. There is no priest left to lead it: the Cleric went down into the dark, and I keep the candles lit in his place."),
                    new("What is that book you carry?",
                        "The tally. Every soul that goes up the mine road, I write down — the day they went, and the day they came back.",
                        "I have run out of pages twice. And the second column, the coming-back one, has gone very quiet. I leave it blank now, and pray I am wrong to."),
                    new("Have you heard of the Cave Beyond Time?",
                        "The whole vale has heard of it. It is why we die by inches — the men go up to the old workings for want of anything else, and the cave keeps them.",
                        "They say time runs wrong down there. I only know it runs one way for those who go in: away from us.")
                        { SetsFlag = "quest_cave_learned" },
                    new("Is it true the Cleric's wife is dying?",
                        "Mirka. Yes. The wasting has her, and it does not let go. Her father keeps the house on the hill — speak with him if you would know what drove a priest down into that pit.",
                        "Pray at her door if you pass it. Not in it. She has had enough of prayers said over her."),
                    new("What is the Holy Light?",
                        "The Light is not a lamp you carry, child. It is the one you are seen by. The Dawn breaks whether or not we wake to it.",
                        "I am no theologian. I bury the dead, I keep the tally, and I trust the morning comes. In a place like this, that is faith enough."),
                    // He receives them differently once they have gone down and returned.
                    new("We have been into the cave, Brother.",
                        "Then you stand in my tally twice — gone, and come back. Do you know how few names carry that second mark? Sit a while, and let me look at you. The Dawn is kind today.")
                        { ShowWhen = "cave_entered" },
                ],
                Lines = ["The Light is not a lamp you carry. It is one you are seen by.",
                         "Every soul that goes up the mine road is written in my book. I have run out of pages twice.",
                         "The Cleric's wife lies in that house. Pray at her door, not in it."] },
        new() { Id = "vale_widow", Kind = "npcOldWoman", Name = "Goodwife Marrow", Role = "Of the Vale",
                Greeting = "Delvers. I can always tell — you walk toward the mine road, not away from it. Come here, then, and let an old woman say her piece before you go.",
                Topics =
                [
                    new("Who are you?",
                        "Goodwife Marrow. I have kept a house in this vale sixty years, and buried a husband and two brothers out of it. The vale takes more than it gives — and lately it gives nothing at all."),
                    new("Why does the bell ring?",
                        "It rings once for a delve going up the road. It rings twice for one coming back down it.",
                        "I sit by my window and I count. It has been a long, long while since I heard it ring twice."),
                    new("What happened to your family?",
                        "The wasting took my husband, same as it is taking the cleric's poor wife. My brothers went up the mine road for want of bread, and never rang the bell coming home.",
                        "You will smell the corpse-pyres on the far slope. We have too many to bury the old way now."),
                    new("Have you heard of the cave?",
                        "The Cave Beyond Time. Aye. It is the mouth this whole valley is being fed into, one man at a time.",
                        "If you mean to go — and you do, I can see it in you — then go with your eyes open. It gives no one back whole.")
                        { SetsFlag = "quest_cave_learned" },
                    new("Have you any counsel for the road?",
                        "Take bread before the ford. There is nothing to eat past it, and nothing past it that will not try to eat you.",
                        "And keep together. The ones who go down alone are the ones the bell never rings for at all."),
                    // Her manner softens toward them once they have come back up.
                    new("We came back from the cave, Goodwife.",
                        "Came back, did you. Then I will ring the bell myself tonight — twice, and loud, so the whole dying vale hears it once more. Bless you. It has been so long.")
                        { ShowWhen = "cave_entered" },
                ],
                Lines = ["I have buried a husband and two brothers out of this vale.",
                         "The bell rings for a delve going up. It rings twice for one coming back.",
                         "Take bread before the ford. There is nothing to eat past it."] },

        new() { Id = "elder", Kind = "npcOldMan", Name = "Old Halvard", Role = "Elder",
                Services = NpcServices.Quest,
                Greeting = "You've the look of folk bound for the mine. Sit a moment — an old man's word is cheaper than a healer's.",
                Topics =
                [
                    new("What can you tell me of the mine?",
                        "The old workings north of the graves. My own son's crew went in and did not come up — that was the spring, and no one since.",
                        "It is not the rock that took them. It is what the rock opened onto: the cave below the cave, that the priest went down to find."),
                    new("What is this 'cave below the cave'?",
                        "The Cave Beyond Time, the old songs call it. I took it for a song until Cerno came back from it with his mind half gone and a phial of the water that stayed the Wasting a month.",
                        "The Cleric went down after him, in Cerno's stead — that descent was the phial's price. Find him, if you go, and mind you come back up, which is more than most manage.")
                        { SetsFlag = "quest_cave_learned" },
                    new("Who is Cerno?",
                        "Cerno of Kaladash — the one man to walk back out of that cave, though he left half his wits below to do it.",
                        "He keeps a fire at the forecourt of the mouth and will not come down among us. Hear him before you go in; his madness knows more than our sense does.")
                        { OffersQuest = "seek_cerno", ShowWhen = "quest_cave_learned" },
                    new("Where should we make for?",
                        "North through the Whispering Wood, then east at the vale to the old workings. The road still remembers the way even if the men who walked it do not.",
                        "Take light, and take more of it than you think you need.")
                        { ShowWhen = "quest_cave_learned" },
                    // A persuasion — the thing Halvard swore not to repeat.
                    new("Press him: what did Cerno not say?",
                        "The old man holds your eye a long moment, then looks at the fire. 'He wept. There — you have what he'd kill me for telling. The great Cerno of Kaladash sat at my table and wept like a boy.'",
                        "'He said the cave gives back what you love most — wrong. Changed. That is all he would say, and all I will. He did not go down there for treasure, whatever the songs decide later.'")
                        { Persuade = 12, ShowWhen = "quest_cave_learned",
                          FailA = ["'Some things were said at my table in confidence. A table you are a guest at, mind.' He stirs the fire and lets the silence answer the rest."] },
                ],
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
        // No Quest flag: he gives no quest, and a service flag with nothing
        // behind it is a prompt that lies.
        new() { Id = "guard", Kind = "npcGuard", Name = "Serjeant Vosk", Role = "Guard",
                Lines = ["Past the graves you're on your own. That's the rule.",
                         "Sign's there for a reason. Read it twice."] },
        // "Hedda", not a second Ilsa — the forester in the Wood already carries
        // that name, and two Ilsas a region apart read as a copy-paste, not kin.
        new() { Id = "farmwife", Kind = "npcFarmwife", Name = "Hedda", Role = "Farmer",
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
        new() { Id = "cerno", Kind = "npcCerno", Name = "Cerno", Role = "Adventurer of Kaladash",
                Services = NpcServices.Quest,
                Greeting = "You have the smell of the deep road on you already. I am Cerno — I went down, and I came back, which is more than the cave usually allows. Ask, then.",
                Topics =
                [
                    new("You have been inside the Cave?",
                        "I drew the panacea from its very halls, and left the rest of my mind behind to carry it out.",
                        "Time runs strange down there — a day below is a year of your sleep. I turned back too soon, and still what I saw follows me up here.")
                        { SetsFlag = "sq_cerno_heard" },
                    new("What should we know before we descend?",
                        "Light. Take more than you think you need, then more again. The dark down there is not the absence of light — it is the presence of something else.",
                        "And when your own mind starts to argue with you in a voice you almost know — that is the cave. Do not answer it."),
                    new("You led the Cleric down?",
                        "I did. His wife was dying and the panacea was the only thing that stayed it, so I struck the bargain: his descent for the phial. He took it without flinching.",
                        "Find him below, if you can. Tell him the old man in the vale keeps the hearth lit."),
                    new("Where do the other roads lead?",
                        "Two run from this pit: west into the Vale and its chapel, south to the sun-road that goes on to Kae Ychel. Either is kinder than the climb behind me."),
                ],
                Lines = ["This cave of myth is real. I drew the panacea from its very halls, and left the rest of my mind behind.",
                         "I turned back too soon. What waits below, no man was meant to carry back out.",
                         "You mean to descend? Then hear me plainly — you will descend into madness.",
                         "Two roads run from this pit: west into the Vale, south to the sun-road for Kae Ychel. Either is kinder than the climb behind me."] },
        // Kazzat — the guardian at the bottom of the Cistern, keeping his hut of
        // dark brick and copper by the Forgotten Shrine. From the source tale: the
        // seven-foot lizard who serves tea, sees everyone for what they are, and
        // holds the bronze key to the Diver. Placed by World.Story on depth 1.
        // His art is the Gemini sheet (assets/sprites/kazzat-sheet.png), the
        // cane-idle pose packed into atlas_chars as npcKazzat.
        new() { Id = "kazzat", Kind = "npcKazzat", Name = "Kazzat", Role = "Guardian of the Cistern",
                Greeting = "Who goes— no. No one should be down here. And yet here you stand, three of you, dripping fate onto my floor. I am Kazzat, keeper of this place. Sit; the kettle has just sung.",
                Topics =
                [
                    new("What is this place?",
                        "The Great Cistern. Every water from every place and every time comes home through those pipes — the sea below is the ocean made of all oceans, the final place all water returns to.",
                        "Things come home through the pipes too. Ships. Beasts. Once, an island. The sea takes them all, and does not even slow."),
                    new("What are you?",
                        "I am Kazzat, and I keep this place. That is the whole of what I am, and more of an answer than you have earned.",
                        "But I will give you this for nothing: I see you. I see all of you. It is my gift, my way. The heart that burns, the bargain half-paid, the box that should have stayed shut — I see them plainly, and I am not in the habit of telling."),
                    new("What waits deeper?",
                        "You are walking through a place older than the very stone that shapes around it. More ancient than the ancientmost of the gods of your peoples.",
                        "You understand stories — fanciful tales from the few who crawl out, desperate to justify their madness. You do not understand. You cannot. Only those who go to the very depths can begin to comprehend, and to comprehend is to be changed completely.",
                        "Changed how, you will ask. Changed. You will see, if you get deep enough."),
                    new("How do we go on from here?",
                        "With this. A key of bronze, a triple helix around a sliver of sleeping crystal. There is a cave a short walk from my door; the key wakes the Diver that sleeps there, and the Diver crosses beneath the sea to the Great Coral Tree.",
                        "Beware the crossing. That is an ocean of every monstrosity across all times and places, and the Diver is a very small bell to ring in it.")
                        { SetsFlag = "kazzat_key" },
                    new("Has anyone else come this way?",
                        "The one you call Cerno sat where you sit, in that same wet-dog silence. He drank his tea, would not speak of what he had seen below, and went back UP.",
                        "The wiser direction, if you ask the kettle. Nobody asks the kettle."),
                ],
                Lines = ["No one should be down here. Drink your tea.",
                         "I see you. I see all of you. It is my gift, my way.",
                         "To comprehend is to be changed completely. You will see, if you get deep enough."] },
        // Mirka's father — sent his son-in-law, the Cleric, into the dark to save
        // his dying daughter; keeps her house on the hill while the priest is gone.
        // The concept art's "ideas of conversation", made literal: he answers a menu
        // of questions rather than cycling one-liners, since the priest himself is
        // gone below and he is the only one left who can tell any of it. Lines are
        // kept as the fallback for a headless run with no dialogue window attached.
        new() { Id = "mirkafather", Kind = "npcMirkaFather", Name = "Mirka's Father", Role = "Villager of the Vale",
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
                        "If you are truly going down into that cave — find the cleric in the gilded plate, and tell him she still breathes. That is help enough.")
                        { OffersQuest = "word_for_cleric" },
                    new("What is that journal?",
                        "He kept it at her bedside, in his own careful hand. Every remedy he tried, every prayer, every bargain.",
                        "Read it, if you have the stomach. It is a year of a man's hope running out, written down."),
                    new("What lies beyond the forest?",
                        "South lies Ashwold: its west gate opens on Seoshe, and its east road runs out for Kae Ychel.",
                        "And east of here, past the graves, the old workings of Deepdelve — and past them, the cave. Every one of them safer than the last."),
                    new("Have you heard of the Cave Beyond Time?",
                        "I did not believe in it either, till Cerno set a phial of its water into my hand.",
                        "They say time runs strange down there, and that men who go in come back changed — if they come back at all. My son-in-law has not.")
                        { SetsFlag = "quest_cave_learned" },
                    // Only once they know to look for it — the road to the mine.
                    new("Then where does the old mine lie?",
                        "East, past the graves on the high road. The old workings first, and past them the mouth of the cave itself.",
                        "Follow the ford up out of the vale and keep to the road. You will not mistake the dark of it.")
                        { ShowWhen = "quest_cave_learned" },
                    new("Who is Cerno?",
                        "Cerno of Kaladash. A warrior, grim and honest. He brought the panacea up out of the dark, and left half his mind behind to do it.",
                        "It was he who led my son-in-law down. I asked him to. God forgive me — I asked him to. He keeps to the cave forecourt now; seek him there before you go in.")
                        { OffersQuest = "seek_cerno" },
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
                         "South of the treeline lies Ashwold — Seoshe through its west gate, the Kae Ychel road out its east. And east of here the old workings, and past them the cave.",
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
                         "(For a heartbeat her eyes flutter — then she is gone again, somewhere the panacea cannot follow.)"],
                // After the Panacea is set to her lips (World.TryWakeMirka), she is
                // awake — and the room stops being a sickroom.
                LinesWhen = "mirka_woken",
                LinesAfter = ["(The colour has come back to her. She sits up against the pillows, watching the window as though it were new.)",
                              "He sits with me at dawn now, instead of praying at my door.",
                              "You carried the deep water up in your own hands. Neither of us will forget it."] },
        // A fisher on the coast road, come up from Seoshe's Low Docks.
        new() { Id = "coastfisher", Kind = "npcFisherman", Name = "Old Nets", Role = "Fisher of the Coast Road",
                Lines = ["Followed the river up from Seoshe. The catch there's gone strange since the crystal trade started.",
                         "West and south, past the stones, the road runs down to the crescent shore. Mind the smugglers on the way.",
                         "The Neruum flotilla's due at the Low Docks by month's end. Spices, pelts — and worse, if the rumours hold."] },
        // The Academy outpost on the east road, toward Kae Ychel.
        // The grandmaster, inside the hall — placed by the interior builder.
        new() { Id = "grandmaster", Kind = "npcMage", Name = "The Grandmaster", Role = "Academy of Kae Ychel",
                Services = NpcServices.Quest,
                Greeting = "An outpost sees few travellers who are not running from something. Ask, then — the Academy answers what it chooses to.",
                Topics =
                [
                    new("What is this place?",
                        "This is but an outpost. The Academy itself takes up a quarter of Kae Ychel — gardens, towers, libraries, the greatest school on the continent."),
                    new("What are the midsummer tests?",
                        "Each midsummer the finest hundred are tested before the Twin Sun King's avatar. Ten receive his boon. It is the making of a mage, or the breaking of one."),
                    new("Whose name was struck from the memorial?",
                        "We do not say his name. He bound a greater demon at the tests, it took half his year with it, and the Regent sealed the thing in his heart and cast him out. That is all the Academy owes the question.",
                        "But stones keep what mouths will not. Read the memorial, if you must — and the courtyard wards the fire could not unmake. Then leave it be.")
                        { OffersQuest = "struck_name" },
                    // A persuasion: press the Grandmaster to recite what the stone
                    // says, and the memorial's step is earned without the walk.
                    // Only once the quest is taken; gone once the stone is read.
                    new("Press him: say what the memorial says",
                        "For a long moment he only looks at you. Then, very quietly, he recites what the stone will not be spared: the name's absence, the year, the sentence in full. 'There. You have it without troubling the dead. Do not make me regret the telling.'")
                        { Persuade = 13, SetsFlag = "sq_name_memorial",
                          ShowWhen = "sq_name_started", HideWhen = "sq_name_memorial",
                          FailA = ["'You press like a creditor.' His face closes like a door. 'The stone in the hall has more patience than I do. Ask it.'"] },
                ],
                Lines = ["This is but an outpost. The Academy itself takes up a quarter of Kae Ychel — gardens, towers, libraries, the greatest school on the continent.",
                         "Each midsummer the finest hundred are tested before the Twin Sun King's avatar. Ten receive his boon. It is the making of a mage, or the breaking of one.",
                         "We had an apprentice once who reached past his grasp. He bound a greater demon at the tests, and it took half his year with it. The Regent sealed the thing in his heart and cast him out. We do not say his name.",
                         "If you are bound for the mine, mage-craft will not save you down there. That is not our kind of magic. It is not anyone's."] },
        // Apprentices walking the courtyard outside.
        new() { Id = "apprentice_ward", Kind = "npcApprentice", Name = "An Apprentice", Role = "Of the Academy",
                Lines = ["Wards, always wards. Boring, the others say. Boring keeps you alive when a working turns on you.",
                         "Gersimo did wards too. He put one up at the tests, the year of the fire. It did not matter. Nothing did.",
                         "A five-point array is stable. Fewer points and it wants to come apart in your hands. Never trust a three-point array — whatever they tell you about prodigies."] },
        new() { Id = "apprentice_fire", Kind = "npcBoy", Name = "An Apprentice", Role = "Of the Academy",
                Lines = ["Lightning's the thing. A good enough show at the tests and you're Evoker Corps, the King's own war-mages.",
                         "Thami tried lightning three years running. They say he cared more for the Corps than the boon itself. They say a lot about Thami now.",
                         "Fire and force. Who wants to spend their life warding doors that were never going to open?"] },
        new() { Id = "apprentice_scry", Kind = "npcScryer", Name = "An Apprentice", Role = "Of the Academy",
                Lines = ["I'm reading up on scrying spheres. A three-point array to hold one — mad, unstable, the grandmasters gasp when anyone manages it.",
                         "There's a whole shelf gone from the library. Bindings, summonings. Chained shut, and still someone took them, years back.",
                         "You feel it too, out east? The air's wrong past the caravan. Like the light down in that mine, only... reaching."] },
        // ---- Seoshe, the crescent-coast city (placed inside its own map) ----
        new() { Id = "seoshe_guard", Kind = "npcGuard", Name = "A Harbour Watchman", Role = "Guard of Seoshe",
                Services = NpcServices.Quest,
                Greeting = "Welcome to Seoshe. Keep your purse close and your questions closer — though you look like the kind that asks them anyway.",
                Topics =
                [
                    new("Who keeps the law down here?",
                        "My captain drinks on the crews' coin, and the crews drink on his. That's the whole of the law down here."),
                    new("What burned at the docks?",
                        "A warehouse on the Low Docks, months back — a whole crew inside it. Silver-blue ash, Cayhall goods, and a boss who walked out and kept walking. No inquiry.",
                        "We don't dig into it. But you're not the watch, are you. If you've the stomach, dig where we didn't — and don't bring me what you find.")
                        { OffersQuest = "crew_ashes" },
                    // A persuasion: the watch DID make a tally the morning after,
                    // whatever the captain says — press for it and the warehouse
                    // walk-through is spared. Only once the quest is taken.
                    new("Press the watch: there was a tally, wasn't there?",
                        "A long look up and down the quay. 'There was. Sea-chests full of Cayhall silks, a crew's worth of berths slept in, and papers for cargo that never came off any ship. You didn't hear the half of it from me.'")
                        { Persuade = 13, SetsFlag = "sq_crew_seen",
                          ShowWhen = "sq_crew_started", HideWhen = "sq_crew_seen",
                          FailA = ["'You've mistaken me for someone the captain doesn't own.' The watchman looks past you at the harbour and is done talking about fires."] },
                    // A persuasion — the ledger behind the captain's bar bill.
                    new("Press the watch: who does the captain answer to?",
                        "'You didn't hear this.' A glance down the quay. 'Cayhall gold pays the captain's bar bill, and a Cayhall clerk collects his ledger the first of every month. The fire inquiry died on that clerk's desk.'",
                        "'Now buy a fish or move along, before somebody wonders what we're discussing.'")
                        { Persuade = 13,
                          FailA = ["'That question gets watchmen reassigned to counting grain sacks.' He straightens his coat and becomes very interested in the far end of the harbour."] },
                ],
                Lines = ["Welcome to Seoshe. Keep your purse close and your questions closer.",
                         "My captain drinks on the crews' coin, and the crews drink on his. That's the whole of the law down here.",
                         "There was a fire at the docks, months back. A whole crew gone, and a warehouse with them. We don't dig into it. Neither should you."] },
        new() { Id = "seoshe_burgher", Kind = "npcNoble", Name = "A Burgher", Role = "Of High Street",
                Lines = ["A fine city, Seoshe — if you keep to High Street and don't ask what the Low Docks import.",
                         "The Neruum flotilla's due. Spices, silks. Half of it above board, and the other half is why the guard looks the other way."] },
        new() { Id = "seoshe_noble", Kind = "npcNoble", Name = "A Baron of the Hill", Role = "Baron Hill",
                Greeting = "A caller. How novel. Baron Hill does not often receive the... travelling sort. Say your piece, then, before my patience remembers its manners.",
                Topics =
                [
                    new("What of House Cayhall?",
                        "Shuttered doors, drawn curtains, and a ship of theirs that came in carrying nothing and burned a warehouse the night it docked. In my circles we call that a statement.",
                        "A statement of what, none of us cares to guess aloud. Guessing aloud is how one stops being invited to things."),
                    // A persuasion — what the Hill actually knows about that ship.
                    new("Press the Baron: what was ON that ship?",
                        "He examines his rings for a long moment. 'A box. Red lacquer, sealed like a king's tomb, and a Ychellen crew that would not let the harbourmaster within ten paces of it.'",
                        "'Cayhall did not order it — Cayhall was PAID to receive it, and handsomely. And whatever was in that box... it never left the Low Docks. Nothing that took delivery of it did, either.'")
                        { Persuade = 14,
                          FailA = ["'You mistake yourself for someone with the standing to press me.' The Baron's smile is a closed door with excellent hinges."] },
                ],
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

    // The old hand-placed overworld positions lived here (a (Id,X,Y) table read by
    // the retired generator's PlaceNpcs). The regions place their own NPCs now, as
    // NPCSpawn objects in each .tmx, so the table went with the generator. The
    // definitions above are still the single source of who each villager is.

    public static NpcDefinition? Find(string id) => All.FirstOrDefault(n => n.Id == id);
}
