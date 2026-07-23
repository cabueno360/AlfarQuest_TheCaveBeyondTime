# The Cleric's House — Tileset Asset Checklist

A complete list of the pixel-art assets needed to give the Cleric's house its own
look, replacing the placeholder props (campfire-as-hearth, bench-as-bed, statue-as-
shrine) the interior is currently dressed with.

**Do not generate the art from this file** — it is a specification for an artist or
a generator. When the art exists, pack it as described under *Packaging* and drop it
into `wwwroot/assets/Interior/`.

## Conventions

- **Base tile:** `32 × 32 px`, matching the game's `TILE = 32`. All footprints below
  are given in tiles (`w × h`) and in pixels.
- **Palette:** warm, low-saturation — aged wood browns, cold grey mountain stone,
  candle-gold, wool-red and moss-green accents. Match the game's `--aq-gold`,
  `--aq-crystal` and void tones so interiors sit beside the overworld.
- **Perspective:** top-down / slight-oblique, same as the Outside atlas (`atlas_outside`).
  Tall objects (bookshelf, armour stand, wardrobe) draw with their base at the tile's
  bottom edge and their top overhanging upward, like the existing `pine`/`tent`.
- **Format:** transparent-background PNG, no baked shadow (the engine casts a soft
  `groundShadow` under props itself).
- **Naming:** `clh_<category>_<name>[_<variant>]` in lowerCamel-free snake, e.g.
  `clh_furn_bed_master`, `clh_wall_stone_corner_ne`. Prefix `clh_` = Cleric's House
  so a future shared interior atlas can add `inn_`, `smithy_`, etc. without clashes.
- **Variants:** where "×N" is given, provide N interchangeable takes so a room does
  not tile visibly (the engine already picks a random variant per prop).
- **Atlas:** deliver as loose PNGs **plus** one packed `atlas_interior.png` +
  `atlas_interior.json` (same rect-map format as `atlas_outside.json`: `{ name: [ {x,y,w,h}, … ] }`).

---

## EXTERIOR  (the house on the overworld)

| Asset | Name | Footprint | Variants | Notes |
|---|---|---|---|---|
| Stone cottage wall | `clh_ext_wall_stone` | 1×1 (32²) | ×3 | rough mountain granite; corner + edge pieces below |
| Wall corners | `clh_ext_wall_corner_{ne,nw,se,sw}` | 1×1 | — | 4 pieces |
| Timber gable / roof | `clh_ext_roof_slate` | 4×3 (128×96) | ×2 | steep slate roof, snow-dusted variant |
| Roof ridge + chimney | `clh_ext_chimney` | 1×2 (32×64) | — | smoking + cold variant (×2) |
| Front door (closed) | `clh_ext_door` | 1×2 (32×64) | — | heavy timber, iron studs; the entrance portal sits here |
| Windows | `clh_ext_window` | 1×1 | ×2 | shuttered + lit-from-within |
| Garden fence | `clh_ext_fence` | 1×1 | straight, corner, post | 3 pieces |
| Wayside shrine (Holy Light) | `clh_ext_shrine` | 1×2 | — | replaces the yard `statue` |
| Well | `clh_ext_well` | 2×2 (64²) | — | stone, mossed |
| Graves | `clh_ext_grave` | 1×1 | ×3 | the winter's dead |
| Path / doorstep stones | `clh_ext_path` | 1×1 | ×3 | flagstones |
| Snow / frost decal | `clh_ext_snow` | 1×1 | ×4 | overlay for the yard |
| Firewood pile, barrels | `clh_ext_woodpile`, `clh_ext_barrel` | 1×1 | ×2 each | |

## INTERIOR — STRUCTURE & FLOORING

| Asset | Name | Footprint | Variants | Notes |
|---|---|---|---|---|
| Wood plank floor | `clh_floor_wood` | 1×1 | ×4 | ground-floor living spaces |
| Stone flag floor | `clh_floor_stone` | 1×1 | ×4 | kitchen, entry, cellar |
| Wool rug | `clh_floor_rug` | 2×3, 2×2 | ×3 | red + faded; under beds and the prayer corner |
| Interior stone wall | `clh_wall_stone` | 1×1 | ×3 | + `_corner_{ne,nw,se,sw}`, `_edge_{n,s,e,w}` |
| Timber-frame wall | `clh_wall_timber` | 1×1 | ×2 | upper floor partitions |
| Interior door / doorway | `clh_door_interior` | 1×2 | open + closed | between rooms |
| Staircase (up / down) | `clh_stairs` | 2×3 (64×96) | — | replaces the `ladder` stair prop; up + down framing |
| Wall beams / joists | `clh_beam` | 1×1, 2×1 | ×2 | heavy timber ceiling beams |
| Windows (interior) | `clh_window_int` | 1×1 | day + night light | soft window-light overlay |

## INTERIOR — FURNITURE

| Asset | Name | Footprint | Variants | Notes |
|---|---|---|---|---|
| Fireplace / hearth | `clh_furn_hearth` | 2×2 | lit + banked | **animated** flame (4 frames) |
| Kitchen counter + shelves | `clh_furn_kitchen` | 2×1 | ×2 | pots, dried herbs |
| Dining table (long) | `clh_furn_table_long` | 3×1 | set + bare | |
| Small round table | `clh_furn_table_round` | 1×1 | — | |
| Chairs | `clh_furn_chair` | 1×1 | ×3 + tucked | |
| Bench / pew | `clh_furn_bench` | 2×1 | ×2 | advice room |
| Bookshelf | `clh_furn_bookshelf` | 1×2 | full + half-empty | library, study |
| Cabinet / cupboard | `clh_furn_cabinet` | 1×1 | closed + open | |
| Wardrobe | `clh_furn_wardrobe` | 1×2 | — | master bedroom |
| Storage crates + barrels + sacks | `clh_furn_crate/barrel/sack` | 1×1 | ×3 each | |
| Chest | `clh_furn_chest` | 1×1 | closed + open | Mirka's things |

## INTERIOR — BEDS & MEDICAL

| Asset | Name | Footprint | Variants | Notes |
|---|---|---|---|---|
| **Mirka's sickbed** | `clh_bed_mirka` | 2×3 (64×96) | — | occupied; Mirka pale, thin, asleep, drawn INTO the sprite so she reads at any zoom — **special, see below** |
| Master bed | `clh_bed_master` | 2×3 | made + rumpled | unslept-in |
| Bedside / nightstand | `clh_furn_nightstand` | 1×1 | — | holds the journal |
| Vigil chair | `clh_furn_chair_vigil` | 1×1 | — | worn seat, cushion |
| Medicine table | `clh_med_table` | 2×1 | — | phials, mortar & pestle, cloths |
| Medicine bottles / phials | `clh_med_phials` | 1×1 | ×3 | small overlay props |
| Water bowl + cloths | `clh_med_bowl` | 1×1 | — | |
| Herb bundles | `clh_med_herbs` | 1×1 | ×2 | drying on strings |
| Privacy screen | `clh_med_screen` | 1×2 | — | folding, by the bed |
| Wilting flowers (bedside vase) | `clh_med_flowers` | 1×1 | fresh → wilting (×3) | |

## INTERIOR — RELIGIOUS & DECOR

| Asset | Name | Footprint | Variants | Notes |
|---|---|---|---|---|
| Prayer altar / shrine | `clh_relig_altar` | 2×2 | — | the Holy Light; **animated** candle glow |
| Holy symbol (wall) | `clh_relig_symbol` | 1×1 | ×2 | sunburst of the Bringer of Dawn |
| Armour stand (empty) | `clh_relig_armourstand` | 1×2 | empty + with-plate | the empty one is the story beat |
| Weapon rack (empty) | `clh_relig_weaponrack` | 1×2 | empty + with-mace | |
| Candles / candelabra | `clh_light_candle` | 1×1 | ×3 | **animated** flicker (2 frames) |
| Wall lantern | `clh_light_lantern` | 1×1 | — | **animated** |
| Curtains | `clh_decor_curtain` | 1×1 | open + drawn | |
| Wall tapestry / painting | `clh_decor_tapestry` | 1×2 | ×2 | religious scenes |

## SPECIAL / STORY OBJECTS  (examinable — each already has interaction text)

| Asset | Name | Footprint | Notes |
|---|---|---|---|
| **The Cleric's journal** | `clh_story_journal` | 1×1 | open leather book on the nightstand; the game's chief lore source |
| **Panacea vial** | `clh_story_panacea` | 1×1 | tiny wax-sealed phial, moonlight glow (empty variant too) |
| **Wedding portrait** | `clh_story_portrait_wedding` | 1×2 | the Cleric & Mirka, gold-haired |
| Dried bouquet | `clh_story_bouquet` | 1×1 | paper-dry flowers |
| Letters from the Order | `clh_story_letters` | 1×1 | sealed + broken-seal |
| Village records / ledger | `clh_story_ledger` | 1×1 | open book, crossed-out columns |
| Sermon pages | `clh_story_sermon` | 1×1 | scattered parchment |
| Valley map | `clh_story_map` | 1×2 | pinned, hand-drawn |

## Packaging

1. Loose PNGs under `Interior/` in the folder tree implied by the names
   (`ext/`, `floor/`, `wall/`, `furn/`, `bed/`, `med/`, `relig/`, `light/`,
   `decor/`, `story/`).
2. One packed `atlas_interior.png` + `atlas_interior.json` (rect map, as
   `atlas_outside.json`). Animated assets (hearth, candles, altar, lantern) as
   horizontal strips with frame count in the JSON (`{ frames: n }`).
3. Zip/RAR the `Interior/` tree as `cleric_house_tileset.rar` for delivery.

## Wiring (when the art lands)

Each placeholder in `InteriorCatalog.BuildClericGround/Upper` maps 1:1 to an asset
above (hearth→`clh_furn_hearth`, `bench`→`clh_bed_*`, `statue`→`clh_relig_altar`,
`toolRack`→`clh_furn_bookshelf`/`clh_relig_*`). Add the interior atlas to
`atlas.js`, extend `drawProp`/`drawOutProp` to address it by `kind`, and swap the
placeholder `kind` strings in the builders. Collision, portals, lighting and every
examinable's text stay exactly as they are.
