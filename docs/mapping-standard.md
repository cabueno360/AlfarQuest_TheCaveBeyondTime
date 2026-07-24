# Mapping standard

The official way maps are authored in Alfar Quest. **Stage 1 is the reference
implementation** — every future map follows what is written here.

Only Stage 1 is migrated. The cave and every building interior still come from
their own builders in C#, and will until they are migrated in turn. That is
deliberate: the engine supports both at once, so a map can move over one at a
time without a flag day.

---

## The look we are aiming for

The reference is the top-down pixel style of Stardew/CrossCode-era art, and the
project has screenshots of it. What that style does, and what our first attempt
got wrong:

| the reference | our first attempt |
|---|---|
| The ROOF is the building. A big solid gable fills most of the shape; you read the house as a roof from above with a sliver of wall under it | The roof was a thin peak outline and the wall below was the mass — the result read as an open-topped crate you could see into |
| The facade is a SHORT solid strip — wall, door, one window — one or two tiles tall | The facade was four courses of open-looking planks, which read as a fence or a stock pen |
| The roof OVERHANGS the wall on both sides and casts a shadow | Roof and wall were the same width, so nothing read as built |
| Paths are NARROW — one or two tiles of worn dirt, curving | Roads were three cells wide, which at this zoom is a highway |
| Density: fences, crops, small props, grass tufts and flowers pack the space between buildings | Large flat areas of one colour |

**A house must read as a house at a glance.** If a screenshot shows something you
would call a pen, a crate or a fence, it is wrong no matter how the tiles were
picked. Check every building against a reference screenshot before shipping it.

---

## Folder structure

Everything lives under the client's `wwwroot`, so **what Tiled opens is exactly
what the game loads** — no build step, no copied assets, no chance of the two
drifting apart.

```
src/AlfarQuest.Client/wwwroot/
  Maps/
    Outside/
      Stage01_Outside.tmx        the OLD Stage 1 — export baseline only now,
                                 no longer fetched or played (see below)
    Interiors/
      ClericHouse_Ground.tmx     the Cleric's house, both floors
      ClericHouse_Upper.tmx
      MageSchool.tmx             the Academy Outpost
      Seoshe.tmx                 the city — walled, but under open sky
      ThievesWarehouse.tmx       the burnt warehouse
    Cave/
      Cave_Descent.tmx           the delve; one map serves every depth
    Tilesets/
      Floors.tsx  Water.tsx  Walls.tsx      external, never embedded
      Vegetation.tsx  Rocks.tsx
      Tree*.tsx                             one per tree sheet
    Pixel Crawler/                          the art pack, referenced in place
  assets/                                   our own art (NPCs, heroes), untouched
```

Stage 1 is now the RING of four regions under `Maps/Regions/` (R1_Ashwold …
R4_KaeYchelRoad); the game opens in Ashwold. `Stage01_Outside.tmx` is kept as the
export baseline behind `tools/make-tmx.py` and as the id the procedural generator
answers to if every region fails to load, but it is no longer fetched at startup
or played — shipping its 1.2 MB to every player for a map none of them see was
dead weight. Open a region's `.tmx` in Tiled and edit it; save, reload, play.

---

## The two grids

| | tile size | why |
|---|---|---|
| **Tiled map** | **16 × 16** | the authoring grid, as specified |
| **Engine cell** | 32 × 32 | unchanged |

One engine cell is a **2 × 2 block** of map cells, so Stage 1 is `400 × 256` in
Tiled and `200 × 128` to the engine. Object positions are in map pixels and are
doubled on the way in (`World.FromMap`).

The engine deliberately stays at 32. Every catalogue, builder, saved coordinate,
interior and test in the project is written in 32px tile units; changing that
constant would touch all of them at once, which is exactly the large refactor the
migration brief rules out. The 16px grid buys finer authoring — a path can bend
on a half-cell — without disturbing anything downstream.

**Terrain is read at 2 × 2 granularity:** if *any* of an engine cell's four map
cells is painted on a layer, the whole engine cell takes that terrain. So paint
terrain in whole 2 × 2 blocks unless you mean to round a cell up.

---

## Tilesets (TSX)

External, never embedded: `<tileset firstgid="1" source="../Tilesets/Floors.tsx"/>`.

Stage 1 is painted with **Pixel Crawler**, which is already a native 16 × 16 grid,
so every .tsx **references its PNG in place** — no image is copied, sliced or
rescaled. The pack lives under `Maps/Pixel Crawler/`.

| tileset | source | used for |
|---|---|---|
| `Floors` | `Tilesets/Floors_Tiles.png` | grass, dirt roads, cobble |
| `Water` | `Tilesets/Water_tiles.png` | water and its shoreline |
| `Walls` | `Tilesets/Wall_Tiles.png` | cliff plateaus and faces |
| `Vegetation` | `Props/Static/Vegetation.png` | plants, flowers, dead trees |
| `Rocks` | `Props/Static/Rocks.png` | boulders and ore |
| `Tree*` | `Props/Static/Trees/Model_0*/Size_0*.png` | one per tree sheet |

**NPCs and heroes stay on our own atlases** — only the world is Pixel Crawler.

### Two tileset libraries

Both are for authoring: open any of them in Tiled with **Add External Tileset**.
The short-named tilesets in `Maps/Tilesets/` are the ones the maps reference.

| library | what | how |
|---|---|---|
| `Maps/Tilesets/PixelCrawler/` | the whole pack, 181 tilesets | every PNG referenced in place; animation strips get their real frame size |
| `Maps/Tilesets/Ours/` | our own art, 9 tilesets | see below |

Our art comes in two kinds, so it is imported two ways:

* **Grid sheets referenced in place** — `OurHeroes` (the three heroes and the husk
  on their 51×63 grid: idle+walk 0-5, attack 6-7, ability 8), `OurCaveTiles`,
  `OurCaveDeco`, `OurOres`, `OurDecorations`, `OurOutsideTiles`.
* **Image collections** — `OurNpcs` (84 frames) and `OurProps` (197) are packed
  atlases with a JSON frame table at 78 and 188 *different* sizes, on no grid at
  all. No grid tileset can address those, so each frame is cut to its own PNG
  under `Ours/sprites/` and collected, each tile carrying its atlas `Name`. The
  pixels are untouched — the same art, made addressable. `OurHouseObjects` is
  already one PNG per piece, so it is collected in place.

Regenerate either with `tools/import-pixelcrawler.py` / `tools/import-ours.py`.

### How the autotiling works

Each `Floors` material is a block of 5 columns: a 5 × 5 **ring** of "material with
a bite taken out of one side" around a hole (rows 0–4), plus solid fills on rows
10–11. Painting picks the ring tile whose bite faces the differing neighbour, so
edges come out organic rather than square.

* **Water**'s ring is an *island in water*, so it is placed on the **land** cell —
  beaches then draw themselves.
* **Cliffs** use the `Wall_Tiles` **brown** block (cols 0–5; the grey block is a
  flat dungeon wall and reads as a slab outdoors): the full rim around the
  plateau, plus two courses of vertical face on the cells below, written to a
  `CliffFace` layer that is **never read for collision**.
* **Trees** are stamped tile by tile onto `Trees`, bottom-centred on the prop's
  cell, skipping blank source tiles so one tree never erases the canopy behind it.

> Paint terrain on the 16px grid, not per engine cell. Filling an engine cell's
> whole 2 × 2 block with one tile makes every edge and tuft read as a doubled
> 32px blob.

---

## Tile layers

In draw order, bottom first. Empty layers are kept in the template so every map
has the same shape.

| # | layer | meaning |
|---|---|---|
| 1 | `Ground` | the bed. Grass everywhere; nothing is transparent underneath |
| 2 | `GroundDetails` | scuffs, patches, flowers painted into the ground |
| 3 | `Roads` | dirt road → engine `PATH` |
| 4 | `Bridges` | bridge deck → engine `BRIDGE` (walkable over water) |
| 5 | `Water` | → engine `WATER` (blocks) |
| 6 | `Shore` | the beach — **decorative only, never blocks** |
| 7 | `Cliffs` | rock and mountain → engine `ROCK` (blocks) |
| 8 | `Walls` | a room's or a city block's wall → engine `ROCK` (blocks) |
| 9 | `WallFace` | the courses under a wall's southern edge — decorative |
| 10 | `Buildings` | structures drawn as tiles |
| 11 | `Objects` | tile-drawn scenery |
| 12 | `Trees` / `Furniture` | canopy and furnishings below the player |
| 13 | `AbovePlayer` | drawn over the player — eaves, upper canopy |
| 14 | `Shadows` | contact shadows |

`Shore` and `WallFace` exist because the shoreline and the wall face are drawn
on cells the player may still walk on. The shoreline ring is an *island in
water*, so it is painted on the LAND cell — put it on `Water` and the last
strip of every beach becomes water nobody can stand on. Keep decoration off the
layers that decide collision.

### Terrain is decided by layer precedence

Outdoors: `Cliffs > Water > Bridges > Roads > Ground`
Indoors: `Walls > Water > Bridges > Roads > Ground` — a walled place is not
always a roofed one, and Seoshe has streets to walk and dock water to fall in.
Underground: `Walls > Water > Bridges > Floor`.

The **highest painted layer wins**; an unpainted cell is floor. Terrain type is
never read from tile ids, so a map can be entirely repainted with a different
tileset and still play identically. Erase a road and the grass beneath it is
already there.

---

## Object layers

Every gameplay thing is an object. Position is a point (or a rectangle for zones).

| layer | what it places | key properties |
|---|---|---|
| `PlayerSpawn` | where the party starts | — |
| `NPCSpawn` | a villager | `NpcId` |
| `EnemySpawn` | a creature | `EnemyId`, `Respawn` |
| `MerchantSpawn` | a shopkeeper | `MerchantId` |
| `TreasureSpawn` | a container | `ContainerKind`, `InteractionType` |
| `Discovery` | an area that pays XP on entry | `XpSource`, `Radius`, `QuestId` |
| `Warp` | a doorway to another map | `DestinationMap`, `DestinationSpawn`, `Label`, `Verb`, `Radius` |
| `Interaction` | something to read or examine | `InteractionType`, `Verb`, `ReadKind`, `Pages`, `Radius` |
| `Props` | scenery sprites | `Kind`, `Variant`, `Scale`, `Flip`, `Solid`, `Radius`, `Painted` |
| `SafeZone` | rectangle creatures will not enter | — |
| `CaveMouth` | the descent | `InteractionType`, `DestinationMap` |
| `QuestTrigger` | a story beat | `QuestId`, `StoryEvent` |
| `MusicZone` | rectangle | `Music` |
| `CameraZone` | rectangle | camera behaviour |
| `ParticleEmitter` | an effect | effect id |
| `LightingMarker` | a light | `LightRadius`, colour |
| `SavePoint` | a save | — |

### Why props are objects, not tiles

Props are bottom-anchored sprites at **float** positions with a per-instance
scale, flip and variant, drawn from a packed atlas addressed by name. A tile grid
can represent none of that. As objects they keep their exact position and their
own collision radius.

---

## Collision

Collision is **map data, not code**:

* **Terrain** — the `Cliffs`, `Walls` and `Water` layers. Painting a cliff blocks
  it. `Shore` and `WallFace` are decoration and block nothing.
* **Props** — each prop object's own `Solid` and `Radius`.

Nothing in C# hardcodes where the walls are. To make somewhere impassable, paint
it or give the prop a radius.

> Still in code: the `SafeZone` rectangles are written into the map for future
> use, but the creature AI currently reads the static table in
> `World.Creatures.cs`. Moving it to read the map objects is a small follow-up.

---

## Custom properties

Names are `PascalCase` and stable — the loader reads them by name.

`NpcId` · `EnemyId` · `MerchantId` · `QuestId` · `DestinationMap` ·
`DestinationSpawn` · `Music` · `Weather` · `StoryEvent` · `Respawn` ·
`LightRadius` · `InteractionType` · `ContainerKind` · `XpSource` · `Radius` ·
`Kind` · `Variant` · `Scale` · `Flip` · `Solid` · `Verb` · `Label` ·
`ReadKind` · `Pages` · `Painted`

Ids must match the catalogues in C# (`NpcCatalog`, `CreatureCatalog`,
`ContainerKind`, `MerchantCatalog`). An id the game does not know is **skipped,
not crashed** — a typo costs you one object, never the map.

`Pages` holds multi-page reading text separated by `␟` (U+241F), because Tiled has
no list type.

`Painted` says whether THIS map already drew the prop's art into its tile layers.
When it is true the renderer leaves the sprite off, so nothing is drawn twice; the
engine keeps the prop either way, because the prop is what carries the collision.
It is per prop and not per kind on purpose — the same crate is painted into the
warehouse floor and drawn from our own atlas out on the Stage 1 road. A map that
says nothing falls back to judging by kind, which is all the older maps offer.

### Map-level properties

`Stage` (int) · `DisplayName` · `Music` · `EngineTile` (int)

---

## Naming conventions

* Maps `Stage01_Outside.tmx` — `StageNN_Area`, PascalCase, no spaces.
* Tilesets `Terrain.tsx` — singular noun for the concern.
* Layers exactly as tabulated above; the loader matches by name.
* Objects are named for what they are (`Camp Barrel`, `the Cleric's house`); the
  name is shown to the player for containers and warps.

---

## Who draws the map

Both halves read the same file:

* **C#** (`Game/Tiled/`) reads it for collision and for the objects it spawns.
* **JS** (`js/world/tmx.js`) reads it for the picture — it fetches the same .tmx,
  inflates the layers with the browser's own `DecompressionStream`, loads the
  tilesets and paints them into the ground canvas.

So what Tiled shows is what the game plays *and* draws. If the map will not load,
JS falls back to the terrain sampling it used before, exactly as C# falls back to
the generator.

Two things follow from this:

* **The painted map is cached** (`stageMapCanvas`). Stage 1 is hundreds of
  thousands of tiles and never changes, so repainting it on every world rebuild —
  which happens each time a door is stepped through — was pure waste.
* **Scenery painted into the map is not drawn twice.** A prop's `Painted`
  property says so, and `PAINTED_BY_MAP` in `game.js` is the by-kind fallback for
  maps that do not. The engine still keeps those props, because they carry the
  collision — a tree you can walk through is a worse bug than a tree drawn twice —
  but their sprite is suppressed.

## How the engine loads a map

```
Play.razor.cs   fetches Maps/Outside/Stage01_Outside.tmx  →  MapCatalog.Register
                                                              │
World.BuildOverworld()  ── map registered? ──► BuildOverworldFromTmx  (Overworld.Tmx.cs)
                        └─ not registered ──► the old generator (unchanged)
```

* `Game/Tiled/TmxMap.cs` — reads .tmx (base64+zlib or CSV). Knows nothing about the game.
* `Game/Tiled/MapCatalog.cs` — holds parsed maps. A map that fails to parse is left
  unregistered, so the stage falls back and the player still gets a world.
* `Game/Overworld.Tmx.cs` — turns map data into the world.

The map must be fetched **before** the world is built, because the world is built
synchronously and HTTP is not. That is why registration happens in the page.

**The fallback is tested**: with the .tmx removed, Stage 1 builds from the
generator and the full worldmap probe still passes 61/61.

---

## Adding a new map

1. Copy `Stage01_Outside.tmx` as a template — it already has every layer.
2. Set the map properties (`Stage`, `DisplayName`, `Music`).
3. Paint terrain, respecting the precedence order.
4. Place objects, filling in ids that match the C# catalogues.
5. Save under `Maps/<Area>/`.
6. Register it: add its id to `MapCatalog` and fetch it alongside Stage 1.
7. Point the stage's builder at `BuildOverworldFromTmx`.

Steps 6–7 are a few lines. Everything else is Tiled.

---

## Re-baselining from the generator

Stage 1 was **exported** from its generator rather than redrawn, because it was
procedural (`Random(42)`), and exporting is the only way to reproduce it exactly:

```bash
node tools/export-stage01.mjs     # Stage 1     → tools/refs/stage01-world.json
python3 tools/make-tmx.py

node tools/export-interiors.mjs   # the interiors → tools/refs/cleric-house.json
python3 tools/make-house-tmx.py   #   the Cleric's house, both floors
python3 tools/make-places-tmx.py  #   the Academy, Seoshe, the burnt warehouse

node tools/export-cave.mjs        # the cave    → tools/refs/cave.json
python3 tools/make-cave-tmx.py
```

> **Run this only to re-baseline.** Once a map is authored in Tiled the `.tmx` is
> the source of truth, and regenerating overwrites hand edits. `export-stage01.mjs`
> also reads whatever the engine currently builds — so with the map in place it
> exports the map back to itself. Move the `.tmx` aside first if you truly mean to
> re-baseline from the generator.
>
> This is not theoretical. Exporting with the maps in place, then regenerating,
> fed a rendering bug back in as if it were level design: the shoreline had been
> painted on the collision layer, so the beach exported as water, and the next
> generation drew it as water on purpose. **A round trip only proves the map is
> faithful if the JSON came from the generator.** Diff the two — an interior
> should come back byte for byte.

---

## Best practices

* **Paint terrain in 2 × 2 blocks** unless you want a cell rounded up.
* **Never reorder `OUT`** in `make-tmx.py`; append only.
* **Keep `Ground` complete** — every cell painted. Higher layers are erasable
  because grass is already underneath.
* **One concern per tileset**, external, always.
* **Ids over coordinates** — reference a catalogue id and let C# own the
  behaviour. Maps hold placement; they never hold logic.
* **No logic in the map.** Combat, AI, inventory, quests, dialogue, saving,
  lighting and audio stay in C#. The map says *what is here and where*.
* **Check both paths after changing the loader** — with the map, and with it
  moved aside.
