namespace AlfarQuest.Client.Game;

// =====================================================================
//  Stage 1 detail: secrets, optional discoveries, landmarks and
//  the arrangements that tell what happened here.
// =====================================================================
public partial class World
{
    // ---- landmarks: one memorable thing per area --------------------
    void BuildLandmarks()
    {
        AddOw(24, 56, "broadleaf", 1.5f, true, 30f);      // the giant tree by the bridge road
        AddOw(6.5f, 44, "ruin", 1.0f, true, 22f);         // ruined shrine deep in the wood
        AddOw(7.5f, 45.5f, "statue", 0.8f, true, 16f);
        AddOw(64, 44, "statue", 1.1f, true, 20f);         // watcher over the eastern flat
        AddOw(70, 20, "ruin", 0.95f, true, 20f);          // collapsed structure below the range
        AddOw(26, 22, "cherry", 1.1f, true, 20f);         // the one bright tree, north wood
    }

    // ---- optional discoveries ---------------------------------------
    void BuildOptional()
    {
        // A pond tucked into the eastern flat, off any road.
        for (int x = 60; x <= 68; x++)
            for (int y = 56; y <= 63; y++)
            {
                float dx = (x - 64) / 4.4f, dy = (y - 59.5f) / 3.6f;
                if (dx * dx + dy * dy <= 1f && Tiles[x, y] == FLOOR) Tiles[x, y] = WATER;
            }
        AddOw(59, 57, "bench", 0.65f, false, 0f);
        AddOw(69, 62, "log", 0.6f, false, 0f);

        // A crystal field on the bare shoulder east of the cleft.
        for (int n = 0; n < 26; n++)
        {
            float x = 52f + (float)_rng.NextDouble() * 14f;
            float y = 12f + (float)_rng.NextDouble() * 8f;
            if (!OpenGround(x, y)) continue;
            AddOw(x, y, "crystal", 0.55f + (float)_rng.NextDouble() * 0.35f, _rng.Next(3) == 0, 12f);
        }

        // An abandoned wagon on the southern spur, where the road peters out.
        AddOw(41, 54, "wagon", 0.85f, true, 22f);
        AddOw(43, 55.5f, "crate", 0.65f, true, 12f);
        AddOw(39.5f, 55.5f, "barrel", 0.65f, true, 11f);
    }

    // ---- environmental storytelling ---------------------------------
    // Small arrangements that imply what happened here without a line of text.
    void BuildStories()
    {
        // A dig that failed: cart tipped, tools dropped, nobody came back.
        AddOw(30, 26, "mineCart", 0.75f, true, 18f);
        AddOw(31.6f, 27.2f, "orePile", 0.6f, false, 0f);
        AddOw(29, 27.6f, "toolRack", 0.6f, true, 14f);
        AddOw(32.4f, 25f, "crate", 0.6f, true, 12f);

        // A camp abandoned in a hurry, halfway along the trail.
        AddOw(47, 40, "campfire", 0.55f, false, 0f);
        AddOw(45.6f, 41.2f, "bench", 0.6f, false, 0f);
        AddOw(48.4f, 41.4f, "barrel", 0.6f, true, 11f);
        AddOw(46.4f, 38.6f, "crate", 0.6f, true, 12f);

        // A line of felled timber that lost the argument with the weather.
        for (int i = 0; i < 4; i++) AddOw(53f + i * 1.8f, 44.5f, "log", 0.6f, false, 0f);
        AddOw(58.5f, 45.2f, "log", 0.6f, false, 0f);

        // A walled graveyard just short of the mine — the last warning before
        // the dark, and the reason the mining camp beyond it stands empty.
        AddOw(31, 22, "graveyard", 0.75f, true, 26f);
        AddOw(36, 20, "gravestone", 0.6f, true, 12f);
        AddOw(37.4f, 21f, "gravestone", 0.6f, true, 12f);
        AddOw(35, 21.6f, "gravestone", 0.55f, true, 11f);
        AddOw(38.6f, 19.4f, "signpost", 0.6f, false, 0f);

        // Ore hauled out and never carried away.
        AddOw(50, 17, "orePile", 0.7f, false, 0f);
        AddOw(51.4f, 18.2f, "orePile", 0.6f, false, 0f);
        AddOw(48.6f, 18f, "mineCart", 0.7f, true, 18f);
    }

    // ---- the Cleric's house: the first enterable building -----------
    // A stone-and-timber cottage dug into the northern foothills — the Cleric's home
    // in his mountain village, where he kept vigil over Mirka. Its door is the one
    // place on this map that opens onto a whole other map (see World.Interiors).
    void BuildClericHamlet()
    {
        // A stone footprint for collision — hidden under the cottage sprite drawn
        // on top of it, so the building blocks like a wall but reads as a house.
        for (int x = 18; x <= 22; x++)
            for (int y = 20; y <= 23; y++)
                if (x < COLS && y < ROWS) Tiles[x, y] = ROCK;

        // The cottage itself, assembled from the modular house tiles
        // (tools/gen-house-tiles.py → clericHouse.png): stone foundation, timber
        // upper storey, slate roof, chimney, an arched door centred at its base. It
        // is drawn on top of the stone footprint above, which does the blocking — the
        // sprite is visual only, so the doorway below it stays walkable and the
        // entrance portal is reachable. Its own door sits where the portal is, so the
        // portal adds no archway of its own (null prop).
        AddOw(20, 24, "clericHouse", 1.2f, false, 0f);
        AddPortal(20, 24, "cleric_house", "the Cleric's house", TileCentre(20, 25.5f), null);

        // A path up from the road to the doorstep, so the house is approached rather
        // than stumbled into.
        Road(20, 30, 20, 25, 1.4f);

        // The yard, from the concept's own exterior pieces: a fence along the front
        // with the gate left open at the path, the handcart and bucket by the wall,
        // bushes and flowers along the way in. The well, the wayside shrine, the
        // graves the winter filled and the lanterns stay as they were — the Outside
        // pack has those and the concept sheet does not.
        foreach (var fx in new[] { 15.4f, 16.8f, 18.2f })                 // fence, west run
            AddHouseObj(fx, 27.4f, "fence", 0.9f, true, 12f);
        foreach (var fx in new[] { 21.8f, 23.2f, 24.6f })                 // fence, east run
            AddHouseObj(fx, 27.4f, "fence", 0.9f, true, 12f);
        AddHouseObj(24.2f, 25.4f, "cart", 0.9f, true, 15f);
        AddHouseObj(18.2f, 25.2f, "bucket", 0.8f, false, 0f);
        AddHouseObj(16.4f, 25f, "bush", 0.85f, true, 12f);
        AddHouseObj(23.6f, 23.4f, "bush", 0.8f, true, 12f);
        AddHouseObj(17.4f, 26.6f, "flowerbush", 0.8f, false, 0f);
        AddHouseObj(22.8f, 26.6f, "flowerbush", 0.8f, false, 0f);
        AddHouseObj(15.2f, 26.2f, "wildflowers", 0.8f, false, 0f);
        AddHouseObj(25.4f, 26.8f, "wildflowers", 0.8f, false, 0f);
        AddHouseObj(14.2f, 23.6f, "rock", 0.8f, true, 13f);
        AddHouseObj(26.2f, 24.4f, "stone", 0.8f, false, 0f);
        AddHouseObj(16.6f, 28.4f, "signpost", 0.85f, false, 0f);

        AddOw(15f, 24.6f, "statue", 0.75f, true, 15f);       // shrine to the Holy Light
        AddOw(13.6f, 26f, "well", 0.7f, true, 16f);
        AddOw(25.2f, 24.6f, "gravestone", 0.6f, true, 12f);
        AddOw(26.4f, 25.8f, "gravestone", 0.55f, true, 11f);
        AddOw(18.4f, 26.2f, "lantern", 0.58f, false, 0f);
        AddOw(21.6f, 26.2f, "lantern", 0.58f, false, 0f);
        AddOw(23, 34, "signpost", 0.6f, false, 0f);          // at the foothill junction
    }

    // ---- secrets ----------------------------------------------------
    void BuildSecrets()
    {
        // SECRET 1 — a cache behind the waterfall at (8,18). The pocket is
        // carved out of the mountain and reached along the water's edge.
        for (int x = 6; x <= 11; x++)
            for (int y = 16; y <= 20; y++)
                if (x < COLS && y < ROWS) Tiles[x, y] = PATH;
        AddOw(8, 15.5f, "waterfall", 0.9f, false, 0f);
        AddOw(8, 19, "crystal", 0.8f, false, 0f);
        AddOw(9.5f, 19.5f, "crate", 0.7f, true, 12f);
        AddOw(6.8f, 19, "crystal", 0.7f, false, 0f);
        Road(11, 19, 16, 24, 1.2f);
        Road(16, 24, 22, 32, 1.2f);

        // SECRET 2 — a forest hollow at (18,34), screened by bushes rather than
        // walled off, so it reads as somewhere you push through.
        for (int x = 16; x <= 21; x++)
            for (int y = 32; y <= 36; y++)
                if (x < COLS && y < ROWS && Tiles[x, y] == FLOOR) Tiles[x, y] = PATH;
        AddOw(18.5f, 34, "ruin", 0.7f, true, 16f);
        AddOw(20, 35.5f, "crystal", 0.7f, false, 0f);
        AddOw(17, 32.5f, "gravestone", 0.6f, true, 12f);
        foreach (var (bx, by) in new[] { (15.5f, 33f), (15.5f, 35f), (16f, 36.8f), (21.6f, 33.5f), (21.6f, 35.5f) })
            AddOw(bx, by, "bush", 0.7f, false, 0f);
    }
}
