namespace AlfarQuest.Client.Game;

/// <summary>The building interiors, as data — one map each, carved from a solid
/// block of stone and dressed with furniture and things to read.
///
/// The Cleric's house is the emotional heart of the first chapter, so it is the
/// most furnished: every room from the design, and a scatter of examinables whose
/// text is drawn straight from the book's chapter "The Cleric". Its furniture is no
/// longer placeholder: the pieces are cut from the concept art itself and placed
/// with <see cref="World.AddHouseObj"/> (see HOUSE_OBJ in atlas.js), room by room to
/// the blueprint. The walls stay the cave's rock on purpose — a flat coursed-block
/// tile was tried here and reverted, because without its lit rims a wall sits at the
/// same value as the lit floorboards and the rooms stop reading as rooms.
///
/// One deliberate reading of the story: the Cleric himself is not here. He is one
/// of the three delvers in your party — he "followed Cerno down" into the cave. So
/// the house is what he left behind: his journal in his own hand, his armour stand
/// standing empty, and Mirka in her sickbed upstairs.</summary>
/// <param name="Outdoor">True for open-air "interiors" like a city's streets, which
/// should render in daylight rather than as a lit dungeon. False for a house or a
/// hall.</param>
/// <param name="FloorTile">A named floor material the renderer lays over the carved
/// floor instead of the default cave stone — "wood" for a lived-in home. Empty
/// leaves the stone floor (a hall, a warehouse, a dungeon).</param>
public sealed record InteriorDef(string Id, string Name, int Cols, int Rows, Action<World> Build, bool Outdoor = false, string FloorTile = "")
{
    public static InteriorDef? Find(string id) => InteriorCatalog.Find(id);
}

public static class InteriorCatalog
{
    public static readonly IReadOnlyList<InteriorDef> All =
    [
        new("cleric_house", "The Cleric's House", 44, 28, BuildClericGround, FloorTile: "wood"),
        new("cleric_house_upper", "The Cleric's House — Upstairs", 44, 24, BuildClericUpper, FloorTile: "wood"),
        new("mage_school", "The Academy Outpost", 44, 24, BuildMageSchool),
        new("seoshe", "Seoshe", 56, 40, BuildSeoshe, Outdoor: true),
        new("thieves_warehouse", "The Burnt Warehouse", 40, 28, BuildThievesWarehouse),
    ];

    public static InteriorDef? Find(string id) => All.FirstOrDefault(d => d.Id == id);

    static readonly Vec UpperArrival = World.TileCentre(21, 15);
    static readonly Vec GroundArrival = World.TileCentre(37, 6);

    // =================================================================
    //  Ground floor — the welcoming half of the house.
    // =================================================================
    static void BuildClericGround(World w)
    {
        w.Room(2, 2, 41, 25);
        w.Wall(15, 2, 15, 25); w.Room(15, 5, 15, 6); w.Room(15, 13, 15, 14); w.Room(15, 21, 15, 22);
        w.Wall(28, 2, 28, 25); w.Room(28, 5, 28, 6); w.Room(28, 13, 28, 14); w.Room(28, 21, 28, 22);
        w.Wall(2, 9, 41, 9);   w.Room(7, 9, 8, 9); w.Room(21, 9, 22, 9); w.Room(34, 9, 35, 9);
        w.Wall(2, 17, 41, 17); w.Room(7, 17, 8, 17); w.Room(21, 17, 22, 17); w.Room(34, 17, 35, 17);

        // The kitchen is flagged, not boarded — the blueprint gives it a stone floor
        // where the rest of the house is wood. PATH inside a home means flagstones
        // to the renderer (see buildFloorCanvas); it stays as walkable as floor.
        w.Street(16, 2, 27, 8);

        // ---- the entrance hall (bottom-centre) ----
        w.AddPortal(21, 25, World.Portal.Overworld, "outside", default);
        w.Spawn = World.TileCentre(21, 23);
        w.AddHouseObj(19, 24.4f, "candelabra", 0.6f, false, 0f);
        w.AddHouseObj(23, 24.4f, "candelabra", 0.6f, false, 0f);
        // His armour stand and weapon rack, flanking the door — both empty. The one
        // detail that tells the whole story before a word of it is read.
        w.AddHouseObj(17, 19.4f, "cupboard", 0.85f, true, 13f);
        w.AddExamine(17, 19.4f, "Examine", "note", "the armour stand",
            "The stand where his blessed plate once hung. He wore it down into the cave — slept in it, those last nights at her side. It stands empty now.");
        w.AddHouseObj(26, 19.4f, "cabinet", 0.85f, true, 13f);
        w.AddExamine(26, 19.4f, "Examine", "note", "the weapon rack",
            "A cradle for the gilded mace of his clericy. Empty. He carried it into the dark, where a prayer is worth less than a good swing.");
        w.AddHouseObj(21, 19.6f, "wedding", 0.7f, true, 10f);
        w.AddExamine(21, 19.6f, "Examine", "portrait", "the wedding portrait",
            "A wedding portrait above the door. \"Hair of gold more brilliant than any gilding,\" he once said of her. You would not know her now from the woman upstairs.");
        w.AddHouseObj(24, 23, "flowers", 0.55f, false, 0f);
        w.AddExamine(24, 23, "Examine", "note", "a dried bouquet",
            "A bouquet from some warmer year, dried to paper and kept by the door. Nothing has bloomed in this valley in five winters.");

        // ---- storage (top-left) ----
        w.AddHouseObj(4, 5, "chest", 0.85f, true, 13f); w.AddHouseObj(6.4f, 4.4f, "cabinet", 0.85f, true, 12f);
        w.AddHouseObj(13, 4.6f, "cupboard", 0.85f, true, 12f);
        w.AddExamine(4, 5, "Search", "note", "the storeroom",
            "A half-sack of grain, a barrel of salt pork nearly gone. He rationed the house down to nothing so the village might have a little more.");

        // ---- kitchen (top-centre) — the hearth, the shelves ----
        w.AddHouseObj(18, 4.2f, "fireplace", 0.9f, true, 16f);   // the cook-fire, backed onto the wall
        w.AddHouseObj(24, 4, "cupboard", 0.85f, true, 13f);
        w.AddHouseObj(21, 6, "low_table", 0.8f, true, 16f);
        w.AddExamine(24, 4, "Examine", "note", "the kitchen shelves",
            "Dried herbs on strings, a cold kettle, a loaf gone hard. A kitchen that has cooked for two, then for one, and lately for no one at all.");

        // ---- stairway up (top-right) ----
        w.AddStair(38, 4, "cleric_house_upper", UpperArrival, "upstairs");
        w.AddHouseObj(34, 3.4f, "sconce", 0.7f, false, 0f);

        // ---- living room (mid-left) — the hearth, the couch, his shelves ----
        w.AddHouseObj(5, 12, "fireplace", 0.9f, true, 16f);
        w.AddExamine(5, 12, "Examine", "note", "the fireplace",
            "The hearth he kept lit against the winter. Someone has banked the coals since he left — her father, most likely, come up from the village to keep the cold off her.");
        // The rug goes down before the furniture that stands on it — props sort by
        // their ground line, and a tie is broken by the order they were added.
        w.AddHouseObj(9.6f, 14.4f, "rug", 0.9f, false, 0f);
        w.AddHouseObj(11, 15, "armchair", 0.8f, true, 12f);
        w.AddHouseObj(9, 13.6f, "low_table", 0.72f, true, 13f);
        w.AddHouseObj(4, 13.4f, "bookshelf", 0.8f, true, 12f); w.AddHouseObj(6.4f, 13.4f, "bookshelf", 0.8f, true, 12f);
        w.AddExamine(4, 13.4f, "Read", "note", "the bookshelves",
            "Saints' fables, a seminary primer, sermons in his own careful hand. He was full of hope when he wrote these, a young priest fresh from the seminary.");

        // ---- dining area (centre) ----
        w.AddHouseObj(21, 13.6f, "rug", 1.0f, false, 0f);
        w.AddHouseObj(21, 13, "dining_table", 0.82f, true, 22f);
        w.AddHouseObj(18, 13, "chair", 0.66f, true, 9f); w.AddHouseObj(24, 13, "chair", 0.66f, true, 9f, true);
        w.AddHouseObj(20, 11, "chair_b", 0.66f, true, 9f); w.AddHouseObj(22.4f, 15, "chair_b", 0.66f, true, 9f);
        w.AddHouseObj(25, 15, "candelabra", 0.55f, false, 0f);

        // ---- prayer corner (mid-right) — the shrine to the Holy Light ----
        w.AddHouseObj(38, 12.6f, "rug_stone", 0.9f, false, 0f);
        w.AddHouseObj(38, 11.4f, "icon", 0.7f, true, 10f);
        w.AddExamine(38, 11.4f, "Pray", "plaque", "the prayer altar",
            "A small shrine to the Holy Light, worn smooth where he knelt. His canticle is still legible on the rail:",
            "\"O Holy Light of the world. Bringer of Dawn. King of all Kings. Grant me the strength I need. I am a child stumbling in the wilderness, blind and lost and cold without you.\"",
            "\"There are no stars without your grace. No beauty without your love. No hope without your hand. Watch over me — and over her.\"");
        w.AddHouseObj(38, 13, "font", 0.7f, true, 11f);
        w.AddHouseObj(36, 11.4f, "cross", 0.55f, false, 0f);
        w.AddHouseObj(40, 12.6f, "chalice", 0.55f, true, 8f);
        w.AddExamine(40, 12.6f, "Examine", "relic", "the sacred chalice",
            "A chalice of the Holy Light, gilt worn to brass at the lip. He blessed the village's water from it, back when there were enough left to gather. It has not been filled in a long while.");
        w.AddHouseObj(36.6f, 13.4f, "candelabra", 0.55f, false, 0f);
        w.AddHouseObj(35, 13.6f, "flowers", 0.5f, false, 0f);
        // The prayer books, kept at the rail where he knelt.
        w.AddHouseObj(39.6f, 11.2f, "book_red", 0.5f, false, 0f);
        w.AddHouseObj(40.4f, 11.8f, "book_green", 0.5f, true, 7f);
        w.AddExamine(40.2f, 11.5f, "Read", "plaque", "the prayer books",
            "Two breviaries at the rail, their spines cracked from use. One falls open of its own accord at the office for the sick — the page is soft as cloth from being turned so often.",
            "In the margin, in the same careful hand as the journal: \"Said again tonight. And again. He does not answer, but I do not think that means He is not listening.\"");

        // ---- library (bottom-left) — the records of a dying village ----
        w.AddHouseObj(4, 19.4f, "bookshelf", 0.8f, true, 12f); w.AddHouseObj(6.4f, 19.4f, "bookshelf", 0.8f, true, 12f);
        w.AddHouseObj(4, 21.6f, "bookshelf", 0.8f, true, 12f);
        w.AddHouseObj(9, 23, "desk", 0.85f, true, 14f); w.AddHouseObj(9, 24.4f, "chair", 0.62f, true, 9f);
        w.AddHouseObj(10.2f, 22.4f, "records", 0.6f, false, 0f);
        w.AddHouseObj(7, 21, "candle", 0.6f, false, 0f);
        w.AddExamine(9, 23, "Read", "note", "the village records",
            "Harvest ledgers, column after column struck through. \"Came up grey,\" reads one margin. \"Froze before the reaping,\" reads the next. A census with more names crossed out each year than added.");

        // ---- the advice room (bottom-right) — where the village came to him ----
        foreach (var cx in new[] { 31f, 33f, 35f })
        {
            w.AddHouseObj(cx, 20, "chair_b", 0.6f, true, 9f);
            w.AddHouseObj(cx, 22.4f, "chair_b", 0.6f, true, 9f);
        }
        w.AddHouseObj(38.4f, 21, "bench", 0.75f, true, 16f);
        w.AddExamine(38.4f, 21, "Examine", "note", "the advice bench",
            "The bench where villagers waited their turn — a marriage to bless, a quarrel to settle, a sick child to pray over. He was the first they sought, once. Fewer come now; fewer are left to come.");
        w.AddHouseObj(30, 24, "side_table", 0.8f, true, 10f);
        w.AddHouseObj(30, 23.4f, "letter", 0.55f, false, 0f);
        w.AddExamine(30, 24, "Read", "letter", "letters from his Order",
            "A bundle of missives from the seminary, the topmost still unanswered.",
            "\"...more clerics and medics called to the front than we have to send. Blazwitz and Koerig have joined against Tellaran, but the line is pushed north. We may yet recall you.\"",
            "\"The invaders care nothing for the faith, nor the lands they ravage. Worshippers of mad gods, the lot of them.\" He never sent his reply.");
        w.AddHouseObj(35, 18.8f, "candelabra", 0.55f, false, 0f);
    }

    // =================================================================
    //  Upstairs — Mirka's sickroom, and the journal that tells all.
    // =================================================================
    static void BuildClericUpper(World w)
    {
        w.Room(2, 2, 41, 21);
        w.Wall(16, 2, 16, 21); w.Room(16, 6, 16, 7); w.Room(16, 15, 16, 16);
        w.Wall(27, 2, 27, 21); w.Room(27, 6, 27, 7); w.Room(27, 15, 27, 16);
        w.Wall(2, 10, 41, 10); w.Room(8, 10, 9, 10); w.Room(21, 10, 22, 10); w.Room(33, 10, 34, 10);

        // ---- Mirka's room (top-left) — the emotional centre ----
        w.AddHouseObj(6.4f, 7.6f, "rug", 0.85f, false, 0f);  // beside the bed
        // Mirka in her sickbed — bed and sleeper are one prop, cut from the concept's
        // own second-floor plan, because the renderer has no lying pose. The NPC
        // behind it is Bedridden, so she is talkable but draws no standing figure.
        w.AddHouseObj(6, 6.6f, "bed_mirka", 0.95f, true, 16f);
        w.AddNpc("mirka", 6, 5.4f);
        w.AddNpc("mirkafather", 10, 4);                      // her father, keeping the vigil
        w.AddHouseObj(3.6f, 5.4f, "chair", 0.72f, true, 9f); // the vigil chair
        w.AddExamine(3.6f, 5.4f, "Examine", "note", "the vigil chair",
            "A hard wooden chair drawn up to the bedside, its seat worn smooth. He slept upright in it for the better part of a year, in his full armour, so as never to leave her.");
        w.AddHouseObj(3, 3.4f, "curtains", 0.7f, false, 0f);
        // The bedside table, and on it the journal — the heart of the house.
        w.AddHouseObj(8.4f, 5, "bedside", 0.78f, true, 11f);
        w.AddHouseObj(8.4f, 4.3f, "journal", 0.5f, false, 0f);
        // The journal fills in as the tale is earned: the setup is open from the
        // first read, but the two darkest leaves — the panacea's price coming due,
        // and the Cleric's own descent — reveal only once the party has entered the
        // cave and reached its heart, so the ending lands where the player is.
        w.AddExamineGated(8.4f, 5, "Read", "journal", "the Cleric's journal",
            ("The Cleric's journal, in his own careful hand, left open on the nightstand the morning he followed Cerno down into the cave.", ""),
            ("Her father came in the dark with a stranger — Cerno, a warrior of Kaladash, grim and honest. He put into my hand a phial of panacea, drawn, he swore, from the Cave Beyond Time. I did not believe him. I drank his word like a dying man drinks water.", ""),
            ("The first month, the wasting slowed. Her fingers looked human again. She wakes only minutes a day, but each waking is more determined than the last. I have not told her of my bargain — only that through her father's diligence and the will of God, we found a way.", ""),
            ("A letter from my Order. More healers called to the war than exist to send; Blazwitz and Koerig have joined against Tellaran, and the line is pushed north. They speak of recalling me. Outside my window they light the corpse-pyres now, great pillars of flame, and all I can do is pray He accepts the departed.", ""),
            ("Three months, and her progress has stalled. Then her skin grew pale as the snow again, and bruises rose wherever I touched her, as though her flesh rejected me. The panacea's gift was only a loan. There has been no waking in weeks.", "cave_entered"),
            ("Cerno comes tomorrow to lead me to the cave, and I will descend into the madness he promised. Mirka — tomorrow is the first time I will leave your side in nearly a year. By God, at least I had this year with you. Come, Cerno. Lead me on to damnation.", "cave_heart"));
        // The medicine table — everything he tried, none of it enough.
        w.AddHouseObj(11.5f, 7, "apothecary", 0.78f, true, 18f);
        w.AddExamine(11.5f, 7, "Examine", "note", "the medicine table",
            "Phials and cloths, a bowl of water gone cold, herbs ground to paste. Prayer, ritual, divination, every miracle he was blessed with — he tried them all on the wasting, and the wasting did not so much as slow.");
        w.AddHouseObj(13, 4.6f, "herbs", 0.6f, false, 0f);
        w.AddHouseObj(9.6f, 7.6f, "washstand", 0.7f, true, 9f);
        w.AddHouseObj(9.4f, 6.4f, "flowers", 0.5f, false, 0f);
        w.AddExamine(9.4f, 6.4f, "Examine", "note", "wilting flowers",
            "Fresh flowers someone still brings to the bedside, though they wilt within a day in the cold of this room. A small, stubborn kindness.");
        // The panacea phial, kept though drained — the story's turning point.
        w.AddHouseObj(7.4f, 6.6f, "panacea", 0.5f, true, 7f);
        w.AddExamine(7.4f, 6.6f, "Examine", "relic", "the Panacea phial",
            "The phial Cerno drew from the Cave Beyond Time — no larger than a green bean, its gilt water long since drunk, its wax seal broken. Six drops. It bought her a single month, and Cerno's price for it was the Cleric's own descent into madness.");
        w.AddHouseObj(3.4f, 4, "candelabra", 0.55f, false, 0f);
        w.AddHouseObj(13.2f, 8, "candle", 0.55f, false, 0f);

        // ---- master bedroom (top-right) — unslept-in ----
        w.AddHouseObj(35, 5, "bed", 0.88f, true, 18f);
        w.AddExamine(35, 5, "Examine", "note", "the master bed",
            "His own bed, made and unslept-in. Why lie here, when she was three doors down and every night might be the last? He kept his vigil in the chair beside hers.");
        w.AddHouseObj(38.6f, 4.4f, "cupboard", 0.85f, true, 12f);
        w.AddHouseObj(31, 7, "plant", 0.6f, false, 0f);
        w.AddHouseObj(39, 3.4f, "candle", 0.55f, false, 0f);

        // ---- private study (bottom-left) ----
        w.AddHouseObj(5, 15.4f, "desk", 0.85f, true, 15f);
        w.AddHouseObj(5, 16.8f, "chair", 0.62f, true, 9f);
        w.AddHouseObj(6.4f, 14.6f, "records", 0.58f, false, 0f);
        w.AddHouseObj(3.6f, 13.6f, "bookshelf", 0.8f, true, 12f); w.AddHouseObj(3.6f, 18.2f, "bookshelf", 0.8f, true, 12f);
        w.AddExamine(5, 15.4f, "Read", "note", "the study desk",
            "A half-written sermon on righteousness, a map of the valley and its white-peaked mountains, a missive to the seminary begun and abandoned. He had run clean out of things to say to God.");
        w.AddHouseObj(8, 13.4f, "candle", 0.55f, false, 0f);

        // ---- stairs down (centre) ----
        w.AddStair(21, 13, "cleric_house", GroundArrival, "downstairs");
        w.Spawn = World.TileCentre(21, 15);

        // ---- storage (bottom-centre) ----
        w.AddHouseObj(19, 18.4f, "chest", 0.85f, true, 12f); w.AddHouseObj(23, 18.4f, "cabinet", 0.82f, true, 12f);
        w.AddExamine(19, 18.4f, "Search", "note", "Mirka's things",
            "A chest of her belongings, folded and put away: a summer dress, a spindle, letters he wrote her before they married. He could not bear to look at them, and could not bear to throw them out.");

        // ---- balcony (bottom-right) ----
        w.AddHouseObj(38, 15.4f, "bench", 0.72f, true, 15f);
        w.AddHouseObj(31, 13.6f, "flowers", 0.5f, false, 0f); w.AddHouseObj(34, 13.6f, "plant", 0.55f, false, 0f);
        w.AddHouseObj(37, 13.6f, "flowers_yellow", 0.5f, false, 0f); w.AddHouseObj(40, 13.6f, "flowers", 0.5f, false, 0f);
        w.AddExamine(38, 15.4f, "Examine", "note", "the balcony",
            "A narrow balcony over the village. From here you can see the frozen slopes where the sheep once grazed on shockingly green grass, and the smoke of the pyres. He came out here to pray where she could not hear him weep.");
        w.AddHouseObj(30, 19, "candelabra", 0.55f, false, 0f); w.AddHouseObj(40, 19, "candle", 0.55f, false, 0f);
    }

    // =================================================================
    //  The Academy Outpost — a college of the Academy of Kae Ychel, on the
    //  road east. A lecture hall, an arcane library, a warded casting circle,
    //  and a memorial to the apprentices lost at the tests. The banished
    //  apprentice whose ritual killed them is one of the three delvers in the
    //  party — his name struck from every roll here.
    // =================================================================
    static void BuildMageSchool(World w)
    {
        w.Room(2, 2, 41, 21);
        w.Wall(14, 2, 14, 21); w.Room(14, 10, 14, 11);   // library, left
        w.Wall(29, 2, 29, 21); w.Room(29, 10, 29, 11);   // casting circle, right

        // The way out, south, and where you appear.
        w.AddPortal(21, 21, World.Portal.Overworld, "the courtyard", default);
        w.Spawn = World.TileCentre(21, 19);
        w.AddOw(19, 20, "lantern", 0.55f, false, 0f); w.AddOw(23, 20, "lantern", 0.55f, false, 0f);

        // ---- the lecture hall (centre) ----
        w.AddOw(21, 3.4f, "statue", 0.7f, true, 15f);     // a bust above the lectern
        w.AddOw(21, 5, "workbench", 0.6f, true, 17f);     // the lectern
        w.AddNpc("grandmaster", 19, 6);
        w.AddExamine(21, 5, "Read", "plaque", "the midsummer tests",
            "A notice pinned to the lectern. \"The Academy of Kae Ychel. Each midsummer the hundred finest apprentices are tested before the avatar of the Twin Sun King. The ten best are blessed with a boon — only one may claim the greatest of them. This outpost prepares those who would sit the tests.\"");
        foreach (var (cx, cy) in new[] { (17f, 9f), (19f, 9f), (23f, 9f), (25f, 9f), (17f, 11f), (19f, 11f), (23f, 11f), (25f, 11f), (17f, 13f), (19f, 13f), (23f, 13f), (25f, 13f) })
            w.AddOw(cx, cy, "chair", 0.5f, true, 9f);
        w.AddOw(16, 3, "banner", 0.6f, false, 0f); w.AddOw(26, 3, "banner", 0.6f, false, 0f);
        w.AddOw(16, 7, "lantern", 0.5f, false, 0f); w.AddOw(26, 7, "lantern", 0.5f, false, 0f);
        w.AddOw(25, 16, "crate", 0.5f, true, 10f);
        w.AddExamine(25, 16, "Read", "letter", "the Evoker Corps notice",
            "A recruitment bill. \"THE EVOKER CORPS. Lightning, fire, force — the King's own war-mages. A show impressive enough at the tests, and a place is yours.\" Some apprentices, they say, want the Corps more than they want the boon.");

        // ---- the arcane library (left) ----
        foreach (var ly in new[] { 3f, 5f, 7f, 9f, 12f, 14f, 16f })
            w.AddOw(3, ly, "toolRack", 0.6f, true, 14f);
        w.AddOw(5, 3, "toolRack", 0.58f, true, 13f); w.AddOw(7, 3, "toolRack", 0.58f, true, 13f);
        w.AddOw(9, 17, "workbench", 0.58f, true, 16f); w.AddOw(9, 18.2f, "chair", 0.5f, true, 9f);
        w.AddOw(6, 10, "lantern", 0.5f, false, 0f); w.AddOw(11, 15, "lantern", 0.5f, false, 0f);
        w.AddExamine(3, 5, "Read", "note", "the arcane library",
            "Treatises on incantation and evocation, on arrays of three points and five, on the trapping of fey beasts in cold iron. The lowest shelves are chained shut — bindings and summonings, the workings an apprentice is forbidden to attempt alone. A whole shelf's worth of them is missing.");

        // ---- the casting circle (right) — where a working went wrong ----
        w.AddOw(35, 7, "statue", 0.85f, true, 16f);       // the array's heart
        foreach (var (cx, cy) in new[] { (32f, 5f), (38f, 5f), (32f, 9f), (38f, 9f), (35f, 4f), (35f, 10f), (31f, 7f), (39f, 7f) })
            w.AddOw(cx, cy, "crystal", 0.5f, false, 0f);
        w.AddExamine(35, 7, "Examine", "note", "the casting circle",
            "A granite floor scored with arcane circles — formulae given physical form, written in special ink. Apprentices prepare their workings here, sealed and warded from all intrusion. One such preparation, some years past, went so catastrophically wrong the masters will not speak of it above a whisper.");
        // The memorial — the emotional and canon heart of the room.
        w.AddOw(35, 16, "statue", 0.72f, true, 15f);
        w.AddOw(33.4f, 17, "flowers", 0.5f, false, 0f); w.AddOw(36.6f, 17, "flowers", 0.5f, false, 0f);
        w.AddExamine(35, 16, "Read", "plaque", "the memorial",
            "A plaque, and beneath it fresh flowers. \"In memory of the apprentices lost at the tests: BRUELOS. GERSIMO. THAMI. RAZA. ILLELLI. EMRYS. And the grandmaster who burned holding the fire back.\"",
            "Below, a line has been chiselled out and left blank. \"The one who summoned it was not executed, but banished — a greater demon sealed within his own heart, to carry until he can be rid of it. His name is struck from every roll of this Academy.\"");
        w.AddOw(31, 13, "lantern", 0.5f, false, 0f); w.AddOw(39, 13, "lantern", 0.5f, false, 0f);
        w.AddOw(39, 19, "crate", 0.5f, true, 10f);
        w.AddExamine(39, 19, "Examine", "note", "a warded door",
            "A low door sealed with arcane script that hums faintly under your hand. Whatever the Academy keeps behind it is not for apprentices — nor, by the look of the seals, is it meant to get out.");
    }

    // =================================================================
    //  Seoshe — the crescent-coast trade city, the Thief's own. An open-air
    //  interior (daylight, not dungeon-dark): High Street up the middle,
    //  Baron Hill and its manors to the north, the Low Docks and the shore
    //  to the east, and the burnt warehouse where his crew died. A first
    //  slice — much of it is placeholder blocks and future doors.
    // =================================================================
    static void BuildSeoshe(World w)
    {
        // Solid blocks are the buildings; the streets are carved between them.
        w.Street(26, 5, 29, 37);          // High Street, the spine
        w.Street(15, 15, 41, 28);         // the main square
        w.Street(3, 20, 26, 23);          // west cross-street
        w.Street(29, 20, 45, 23);         // east cross-street, to the docks
        w.Street(13, 6, 43, 10);          // Baron Hill forecourt, north
        w.Street(44, 11, 48, 37);         // the Low Docks quay
        w.Water(49, 11, 54, 37);          // the crescent shore

        // The gate back out to the coast road.
        w.AddPortal(27, 37, World.Portal.Overworld, "the gate", default);
        w.Spawn = World.TileCentre(27, 35);
        w.AddOw(24, 37, "support", 0.85f, true, 15f); w.AddOw(31, 37, "support", 0.85f, true, 15f);
        w.AddOw(25, 36, "banner", 0.65f, false, 0f); w.AddOw(30, 36, "banner", 0.65f, false, 0f);
        w.AddNpc("seoshe_guard", 24, 35); w.AddNpc("seoshe_guard", 31, 35);
        w.AddExamine(27, 36, "Read", "plaque", "the city gate",
            "\"SEOSHE.\" The crescent-coast city, and the finest den of thieves on the continent — though the sign does not say so. Its guard captains drink on the crews' coin, and the crews drink on theirs.");

        // ---- the main square ----
        w.AddOw(28, 21, "well", 0.8f, true, 17f);          // the fountain
        w.AddOw(28, 24, "statue", 0.8f, true, 16f);
        foreach (var (sx, sy) in new[] { (19f, 17f), (37f, 17f), (19f, 26f), (37f, 26f) })
            w.AddOw(sx, sy, "stall", 0.7f, true, 17f);
        w.AddOw(21, 18, "barrel", 0.6f, true, 11f); w.AddOw(35, 18, "crate", 0.6f, true, 12f);
        w.AddOw(17, 16, "banner", 0.65f, false, 0f); w.AddOw(39, 16, "banner", 0.65f, false, 0f);
        w.AddNpc("seoshe_burgher", 23, 25);
        w.AddNpc("seoshe_trader", 34, 25);                 // a market merchant, opens a shop
        w.AddExamine(28, 18, "Read", "plaque", "a proclamation",
            "Nailed to a post in the square. \"By order of the harbour watch: a stolen cargo — a red lacquer box, sought under the King's own seal. Any word of it, or of the crew that took it, to be brought forth at once.\" No reward is named. That, the old hands say, is a very bad sign.");

        // ---- Baron Hill (north) — the gated manors ----
        w.AddOw(16, 8, "statue", 0.9f, true, 17f); w.AddOw(40, 8, "statue", 0.9f, true, 17f);
        w.AddOw(20, 6, "caveEntrance", 0.8f, false, 0f);   // a manor gate (not yet enterable)
        w.AddOw(36, 6, "caveEntrance", 0.8f, false, 0f);
        w.AddOw(14, 7, "banner", 0.65f, false, 0f); w.AddOw(42, 7, "banner", 0.65f, false, 0f);
        w.AddNpc("seoshe_noble", 30, 8);
        w.AddExamine(36, 6, "Read", "plaque", "House Cayhall",
            "The gates of House Cayhall. They deal in fine lumber and ores — or did, until a ship of theirs came into the Low Docks barely hauling anything, and a warehouse burned to the waterline the very night it berthed. The House has said nothing since.");

        // ---- the Low Docks (east) ----
        foreach (var (bx, by) in new[] { (48f, 15f), (48f, 22f), (48f, 30f) })
            w.AddOw(bx, by, "log", 0.7f, false, 0f);       // drawn-up boats
        w.AddOw(45, 13, "crate", 0.6f, true, 12f); w.AddOw(46, 14, "barrel", 0.6f, true, 11f);
        w.AddOw(45, 31, "crate", 0.6f, true, 12f); w.AddOw(46, 32, "barrel", 0.6f, true, 11f);
        w.AddOw(45, 18, "scaffold", 0.8f, true, 16f);      // a dock crane
        w.AddNpc("seoshe_sailor", 45, 25);
        w.AddExamine(45, 13, "Read", "plaque", "the Neruum flotilla",
            "A harbour notice. \"The Neruum flotilla makes port at month's end — exotic spices, pelts and fabrics from far-off lands. Berths assigned at the Low Docks; manifests filed with the harbourmaster.\" The docks stagger out along the crescent like the teeth of a yawning beast.");
        // The burnt warehouse — where his crew died, and the shard was loosed. It
        // can be entered: the door faces the quay, its interior a map of its own.
        w.AddOw(42, 33, "ruin", 1.0f, true, 22f);
        w.AddOw(42, 31, "crate", 0.55f, true, 11f); w.AddOw(43, 35, "barrel", 0.55f, true, 10f);
        w.AddDoor(43, 33, "thieves_warehouse", World.TileCentre(19, 23), "the burnt warehouse");
        w.AddExamine(45, 36, "Read", "plaque", "the Low Docks",
            "\"THE LOW DOCKS.\" Gulls and cargo and every colour of merchant by day; and after dark, other trades entirely. High Street runs north from here to Baron Hill, if your coat is fine enough for the walk.");
    }

    // =================================================================
    //  The Burnt Warehouse — the Thief's crew's hideout, on the Low Docks
    //  of Seoshe. A ruined, fire-gutted building, entered from the city. It
    //  is empty of the living: the crew died here the night the crystal shard
    //  was opened. The player walks the room where they planned the job, the
    //  tunnel the twins dug, and the fused-glass spot where they came apart.
    // =================================================================
    static void BuildThievesWarehouse(World w)
    {
        w.Room(2, 2, 37, 25);
        // The foreman's back room, walled off top-left, entered from the main floor.
        w.Wall(13, 2, 13, 9); w.Room(13, 6, 13, 7);
        w.Wall(2, 9, 13, 9);  w.Room(7, 9, 8, 9);
        // The collapsed half — a slope of rubble filling the northeast.
        w.Wall(26, 2, 37, 14);

        // The way back out, onto the docks of Seoshe.
        w.AddDoor(19, 25, "seoshe", World.TileCentre(44, 33), "the docks");
        w.Spawn = World.TileCentre(19, 23);
        w.AddOw(17, 24, "crate", 0.5f, true, 10f); w.AddOw(21, 24, "barrel", 0.5f, true, 10f);

        // ---- the back room: where the crew planned the job ----
        w.AddOw(7, 5, "workbench", 0.6f, true, 17f);      // the round table
        w.AddOw(5, 5, "chair", 0.5f, true, 9f); w.AddOw(9, 5, "chair", 0.5f, true, 9f);
        w.AddOw(7, 3.4f, "chair", 0.5f, true, 9f);
        w.AddOw(4, 3, "lantern", 0.55f, false, 0f); w.AddOw(11, 3, "lantern", 0.55f, false, 0f);
        w.AddOw(11, 7, "crate", 0.5f, true, 10f); w.AddOw(4.4f, 7, "barrel", 0.52f, true, 10f);
        w.AddExamine(7, 6, "Read", "note", "Essil's map",
            "A rough map on the round table, smeared with soot-black notes in a careful hand. A ship's manifest beside it — untreated fabric and simple lumber, filed for House Cayhall — and pencilled in the margin: \"too thin. hides something, or hides a trap.\" Essil worried it was the second. He was right.");
        // The tunnel the twins dug, down in the corner.
        w.AddOw(3, 8, "ladder", 0.6f, false, 0f);
        w.AddExamine(3, 8, "Examine", "note", "the tunnel",
            "A hole in the corner, no more than two feet high, dropping into a passage that runs two hundred yards beneath the streets. Marko and Mako dug it in a single night — men of stone, stout and red-haired and identical, who spoke only in unison. Three strips of arcane paper still cling to the mouth, a one-way illusion long since gone dark.");

        // ---- the main floor: burnt, half-fallen ----
        w.AddOw(20, 11, "log", 0.7f, false, 0f); w.AddOw(23, 17, "log", 0.65f, false, 0f);
        w.AddOw(16, 15, "orePile", 0.7f, false, 0f); w.AddOw(21, 20, "rock", 0.7f, true, 13f);
        w.AddOw(18, 13, "crate", 0.55f, true, 11f); w.AddOw(14, 19, "barrel", 0.55f, true, 10f);
        w.AddOw(30, 20, "lantern", 0.5f, false, 0f);
        w.Water(9, 16, 11, 17);                            // a soot-black puddle

        // The stash pit beneath the rubble.
        w.AddOw(28, 22, "caveEntrance", 0.7f, false, 0f);
        w.AddExamine(28, 22, "Search", "note", "the stash pit",
            "A pit dug beneath the rubble, where they meant to sink the take until the heat died down. Empty now, but for a red lacquer lid, its cherry paint blistered, and a scatter of spent arcane seals that once bound whatever the box had held.");

        // ---- where it happened ----
        w.AddOw(24, 12, "crystal", 0.55f, false, 0f); w.AddOw(22, 13, "crystal", 0.5f, false, 0f);
        w.AddExamine(23, 13, "Examine", "note", "where it happened",
            "The floor here is scorched black and fused to glass, and in the cracks lies a fine silver-blue ash that will not brush away. Here the box was opened; here the shard woke and screamed, and the crew — Kas, Essil, Yash, the twins — came apart into that smoke one by one, while their boss hung in the air and could not look away.",
            "Crouch close, and in the ash you can almost see what he saw: a great storm of every pain across all times and places, and behind it, the yawning maw of a cave.");

        // The crew's things, and the boss's — a memorial nobody tends.
        w.AddOw(8, 21, "crate", 0.55f, true, 11f);
        w.AddExamine(8, 21, "Search", "plaque", "the crew's things",
            "What the fire left of them, gathered into one crate by some kind hand and never claimed. Kas's rolls of tavern gossip. Yash's fetishes of bone and silk twine. Essil's neat folios. Two sets of stonemason's picks, identical. There is no one left to come for them.");
        w.AddOw(5, 22, "barrel", 0.55f, true, 10f);
        w.AddExamine(5, 22, "Examine", "note", "the boss's coat",
            "A burgher's overcoat of brushed velvet, fine enough to walk High Street unremarked, folded over a broken chair. In its pocket: a pair of loaded dice and a black headscarf. He prayed to the Mother of all Luck, and she loved him right up until the night she didn't.");
        w.AddOw(10, 22, "lantern", 0.5f, false, 0f);
    }
}
