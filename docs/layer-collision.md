# Layer collision — what blocks the party (authoring cheat-sheet)

Collision is decided by **layer precedence**, never by tile id, so a map can be
repainted with any tileset and play the same. The engine reads a handful of tile
layers per cell and resolves one terrain type; only two of them stop a body.

Source of truth: `Game/Overworld.Tmx.cs` `ReadTerrain` (overworld),
`BuildInteriorFromTmx` (interiors), `World.Cave.cs` `BuildCaveFromTmx` (cave);
the block test is `World.Collision.cs` `IsWallAt` → `t == ROCK || t == WATER`.

## The rule

```
cell = Cliffs OR Walls OR Buildings OR Trees(1-6)  → ROCK   (blocks)
     : Bridges                                     → BRIDGE (walkable — beats water)
     : Water                                       → WATER  (blocks)
     : Roads                                       → PATH   (walkable)
     : else Ground                                 → FLOOR  (walkable)
```

Any of a cell's 2×2 map tiles painted counts as painted (the map is 16px, the
engine cell is 32px = a 2×2 block).

## Layers that BLOCK the party

| Layer | Why | Use it for |
|---|---|---|
| **`Cliffs`** | ROCK | cliff tops / high rock |
| **`Walls`** | ROCK | stone, fences, any impassable wall |
| **`Buildings`** | ROCK | **a house is solid to its full drawn extent** (roof included) |
| **`Trees`, `Trees2`…`Trees6`** | ROCK | a tree is a thing you walk around; a dense wood is a wall |
| **`Water`** | WATER | rivers, ponds, sea — **unless a `Bridges` tile sits on the same cell** |

## Layers that DO NOT block (walkable / decoration)

`Bridges` (walkable deck) · `Roads` · `Ground` · `GroundDetails` · `Shore` ·
`CliffFace` · `Objects` · `AbovePlayer` · `Shadows`

## Traps

1. **`Buildings` blocks its whole footprint now** — roof and eaves included, so
   there is no "walk under the eaves". A door's threshold you want to step onto
   must be on **`Roads`/`Ground`, not `Buildings`** (a warp in front of the house
   is reached over open ground regardless).
2. **`Trees` block their whole footprint, canopy included.** In a HAND-authored
   map you place trees deliberately, so this is a natural wall you route the road
   around. In the auto-dressed regions (R2/R3/R4) the woods are DENSE, so blocking
   trees fences off forest-interior clearings — the main road path stays open
   (verified: spawn → seams → cave mouth reachable in every region) but pockets
   inside the wood become unreachable.
3. **A bridge over water needs the bridge tile.** `Water` blocks; painting a
   `Bridges` tile on those same cells makes them a walkable deck (Bridges beats
   Water). If a ford reads as solid, its `Water` was never covered by `Bridges`.

## Also blocking (not a tile layer)

Props on an **object layer** with `Solid = true` block by a round `Radius` around
their base — independent of the tile layers above.
