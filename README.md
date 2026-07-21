# Alfar Quest — Blazor + MySQL

A single-player, top-down action RPG built from the lore of the band **Alfar Quest**,
using **C# / Blazor WebAssembly** for the game and **ASP.NET Core + EF Core + MySQL**
for persistence.

> **What this is, honestly.** The full vision in the design doc (a franchise-scale
> RPG with handcrafted pixel-art regions, crafting, factions, mounts, dozens of quests,
> and full real-time combat) is a multi-year effort for a team. This repository is a
> **working foundation plus a playable vertical slice** so the project starts from real,
> runnable code instead of a blank page. It is not the finished game.

## What's playable right now

**"The Crystal Cistern"** — the reflective crystal chamber from *Story for Music*.
- The three canonical delvers — **Mage, Cleric, Thief** — as a party.
- **Switch the active hero freely** (keys 1/2/3); the other two follow and auto-fight,
  matching the design goal of recruiting existing heroes and swapping between them.
- **Real-time combat**: move (WASD), aim (mouse), attack (J / left-click),
  class **ability/ultimate** (K / right-click), and **dodge-dash** with i-frames (Shift/Space).
- **Dynamic torch/mage-light** in the dark, flickering the way the story describes.
- **Gem-riddled husks** (from the *Cistern* chapter) to clear; clearing them opens the
  way deeper.
- **Your track, "The Ballad of the Wandering," plays as the cavern theme** (looped, mutable).

The game runs fully **offline**. The API + MySQL layer adds persistent saves and serves
hero data, but the client falls back to built-in lore if the backend isn't running.

## Project layout

```
AlfarQuest.sln
 ├─ src/AlfarQuest.Shared   # DTOs shared by client + API
 ├─ src/AlfarQuest.Api      # ASP.NET Core Web API, EF Core, MySQL
 └─ src/AlfarQuest.Client   # Blazor WebAssembly game
     ├─ Game/GameEngine.cs  # all simulation (movement, combat, AI) in C#
     ├─ wwwroot/js/game.js  # canvas rendering, input, audio
     ├─ wwwroot/assets/…    # pixel-art sheets + the generated party atlas
     └─ wwwroot/audio/…     # The Ballad of the Wandering.mp3
tools/extract-sprites.mjs   # regenerates assets/atlas_party.png from the art sheet
db/schema.sql               # raw MySQL schema (alternative to EF EnsureCreated)
```

**Why this split?** A real-time action game needs its loop running locally, so the game
lives in **Blazor WASM** (C# logic + a thin JS canvas layer, driven frame-by-frame through
in-process interop — no server round-trips). MySQL is reached through a small **Web API**,
which is the correct pattern for WASM (the browser can't open a DB socket directly).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — all three projects target `net10.0`
- MySQL 8.x or 9.x (only needed for saves; skip to just play) — `brew install mysql`

`global.json` pins the SDK to 10.0.x, so an older `dotnet` gives a clear
"compatible SDK not found" error instead of a confusing target-framework one.

> **If the SDK lives in `~/.dotnet`** (the no-sudo install location used here), either put
> it first on your PATH — `export PATH="$HOME/.dotnet:$PATH"` — or invoke it explicitly as
> `~/.dotnet/dotnet build`. A system-wide install under `/usr/local/share/dotnet` needs no
> such step.

## Run it

**1. Play the game (no database needed)**
```bash
cd src/AlfarQuest.Client
dotnet run
```
Open <http://localhost:5173>, pick a lead hero, and descend.

**2. Add persistence (MySQL)**
```bash
brew services start mysql                # or your own MySQL server

# create the database + seed heroes
mysql -u root < db/schema.sql            # add -p if your root has a password

# check the connection string matches your server
#   src/AlfarQuest.Api/appsettings.json  ->  ConnectionStrings:MySql
cd src/AlfarQuest.Api
dotnet run                               # serves http://localhost:5080 + Swagger
```
Run both at once (two terminals). The client reads its backend URL from
`src/AlfarQuest.Client/wwwroot/appsettings.json` (`ApiBaseUrl`, default
`http://localhost:5080/`). With the API up, the title screen shows
*"Connected to the MySQL chronicle."*; without it, the game still plays using
built-in lore.

> First `dotnet run` restores NuGet packages (Pomelo MySQL, Blazor WASM), so you need
> an internet connection once.

### Why HTTP and not HTTPS?

Both projects are pinned to plain HTTP dev ports on purpose. ASP.NET's HTTPS dev
certificate (`dotnet dev-certs https --trust`) needs a keychain/admin prompt, and if the
client is served over HTTPS while the API is not, the browser blocks the API calls as
mixed content. HTTP on both sides removes both problems for local development. To use
HTTPS instead, trust the dev cert, re-add an `Https` endpoint under `Kestrel:Endpoints`
in the API's `appsettings.json`, and update `ApiBaseUrl` to match.

## Progression

Experience, levels and attributes, wired end to end: the world pays, the sheet
receives, and the numbers reach the fight.

**What pays.** Kills (per species — a swarm is 8, a crystal worm 40), and two
kinds of one-shot reward. A *Discovery* pays for arriving somewhere and fires as
you walk in — the seven zones of the Approach, plus the waterfall cache and the
forest hollow. An *Interactable* pays for doing something and needs `[E]`:
chests, ore seams, relics and tablets, each sitting on a prop already standing in
the world. Values live in one table in `Models/Rewards.cs`.

Both kinds are one-shot, and **claimed rewards are saved**. Without that, leaving
and re-entering rebuilt the world with every chest refilled — the front door as
an infinite XP source. Overworld claims persist; the cave's do not, because it is
regenerated on every descent.

**The curve.** 100 / 180 / 300 / 500 for the first four levels, written out by
hand because that is where pacing matters most and a formula is least
trustworthy. Beyond them a power curve continues from the last value, rounded so
the HUD never reads "1,247 to next". Five attribute points a level. Everything is
a constant in `Services/Character/Progression.cs`.

**Levelling.** The world flashes, the camera shakes, a ring of light rises off
the hero, `LEVEL UP!` floats up, and a chime plays — synthesised in a dozen lines
rather than shipped as an asset. The game freezes, the window opens, and
*Continue* hands off to the character sheet with the points waiting, because
making the player go and find the sheet is how points go unspent.

The party levels together but only one window opens, for the hero on screen,
naming the others. Three popups in a row turned the reward into a chore.

**Attributes.** Strength, Dexterity, Agility, Vitality, Intelligence, Wisdom,
Defense, Luck. Adding one is a property, two cases and a row in
`AttributeInfo.All` — every panel is driven off that list. What the compiler
cannot check is the part that matters: an attribute nothing derives from is
decorative, so a new one needs a term in `StatCalculator` as well.

Wiring these up exposed three that already were decorative: **Luck** described
better loot and only the Delver's Luck *skill* reached the loot roll; **Block**
was computed for the sheet but only skills reached the engine; and every creature
dealt a hard-coded 8 damage, so `CreatureType.Damage` was never read and no
amount of armour changed anything. All three now reach the simulation.

**Mana and stamina.** Abilities cost mana; dashing costs stamina. Both pools are
owned by the simulation and refill continuously — a pool that only refilled while
idle would make standing still the optimal play. An ultimate that cannot be paid
for spends no cooldown and says why, because a key that does nothing at all reads
as a broken game.

This is what makes **Wisdom** real: it drives mana regeneration, and after the
opening burst it is regeneration — not the cooldown — that decides how often the
ultimate comes back. Wisdom's other half, magic resistance, now has something to
resist: crystal swarms and gem-riddled husks strike magically, so a party built
purely for armour has a genuine weakness.

Costs and pool sizes live in `Models/ResourceCosts.cs`, apart from
`StatCalculator` so that retuning how often an ultimate fires never risks
touching how much damage it does. The first tuning pass was wrong in a way only
measurement showed — regeneration over one cooldown nearly paid for the next
cast, so a full pool lasted eleven casts and mana never bound in a real fight.

Companions auto-attack but never cast or dash, so their pools sit full. That is
honest rather than hidden: they are not spending anything.

**What is saved.** Level, XP, unspent points, *spent* points per attribute,
learned skills, worn gear, the pack, coin, gathered materials, and the rewards
already claimed. Items are stored by id, not by copy — the same reason spent
points are stored rather than totals: a rebalance should reach saved gear instead
of being pocketed by it.

```bash
node tools/progression-probe.mjs   # 74 checks: the world pays → the sheet
                                   # receives → the window opens → the points buy
                                   # something → it survives leaving, and saving
                                   # twice replaces rather than accumulates
```

## Accounts

The game is behind a sign-in. Anonymous visitors get the landing page, sign-in,
sign-up, password recovery, privacy and terms — nothing else. `/heroes`, `/play`,
`/profile` and `/settings` carry `[Authorize]`, and the game module, the party and
every asset are loaded from `OnAfterRenderAsync`, which never runs on a route the
guard redirected.

That guard governs what is **shown**. A WebAssembly client is rewritable by whoever
runs it, so it is not the security boundary: every endpoint behind it checks the
token again, server-side.

**How a session works.** Passwords are stored as PBKDF2-HMAC-SHA256, 210 000
iterations, per-password salt, in a self-describing format so the cost can be
raised without invalidating anyone. Signing in issues 256 bits of CSPRNG output;
only its SHA-256 is written to the database. A JWT would have been the reflex
choice, but a signed token cannot be withdrawn — and "Log out" has to mean
something, so sessions are rows and sign-out revokes one.

The token is kept in `localStorage` when "remember me" is ticked and
`sessionStorage` otherwise. Web Storage is readable by script, so a cross-site
scripting hole here would expose it; an HttpOnly cookie would not, but needs the
API and client on one origin. The trade is deliberate, and the mitigation is that
sessions are short and revocable server-side.

Changing a password revokes **every** session, this one included — the usual
reason to change a password is that it may be known.

**Endpoints**

| | |
|---|---|
| `POST /api/auth/signup` | register, sign in, return the profile |
| `POST /api/auth/signin` | `rememberMe` picks a 30-day session over 12 hours |
| `POST /api/auth/signout` | revokes the presented token |
| `GET  /api/auth/me` | confirms a stored token is still good |
| `POST /api/auth/forgot-password` | always 202, whether or not the address exists |
| `GET/PUT /api/profile` | the caller's own profile |
| `POST /api/profile/password` | needs the current password |
| `POST /api/profile/stats` | progress from a play session |
| `POST/DELETE /api/avatars` | upload or clear a portrait |
| `GET  /api/avatars/{playerId}` | serves it — anonymous, so players can see each other |
| `GET  /api/heroes` | roster (seeded from the novel) |
| `GET  /api/saves/{id}`, `/api/saves/mine`, `POST /api/saves` | scoped to the caller |

Sign-up, sign-in and password recovery are rate limited (`Auth:*` in
`appsettings.json`; 8 attempts per 5 minutes per address by default). Deliberately
**not** applied to `/api/auth/me`, which runs on every page load.

Behind a reverse proxy, add `UseForwardedHeaders` — otherwise every caller shares
one rate-limit bucket.

**Avatars** are sniffed by magic bytes, decoded, and re-encoded to a 256px PNG
before storage. Re-encoding is the point: nothing that was not a drawable pixel
survives the round trip. The bytes live in MySQL rather than on disk, which
removes upload paths, filename sanitising and traversal from the problem entirely.

**Verifying it.** Two probes, both run against a live API and MySQL:

```bash
node tools/auth-probe.mjs      # 48 API checks — hashing, revocation, IDOR, uploads, throttling
node tools/auth-ui-probe.mjs   # 72 browser checks in headless Chrome — the whole flow
node tools/css-collisions.mjs  # no CSS module silently overriding another
```

`auth-probe.mjs` makes about twenty credential calls, so start the API with
`Auth__SignInAttemptsPerWindow=40` for it; the last check then proves the limiter
still bites.

## Art pipeline

The heroes, husks and crystal spires are **pixel-art sprites**, drawn from the sheets in
`src/AlfarQuest.Client/wwwroot/assets/`:

| Sheet | Used for | Notes |
|---|---|---|
| `Characters_.png` | Mage, Cleric, Thief (Exploradora), Crystal Husk | Labelled contact sheet — **not** directly indexable |
| `Miner_Ores.png` | Crystal formations in the cistern | Clean 128px grid, already transparent — indexed directly |
| `Miner_Decorations.png` | Scenery: shrines, ruins, spoil heaps | Clean 128px grid (10×15), already transparent |
| `Caves/MainLev2.0.png` | Cave floor and worn paths | **32px** grid (51×48) |

`Characters_.png` is a presentation sheet with section titles and irregular spacing, and it
has **no alpha channel** (the backdrop is opaque `rgb(17,19,16)`). So the frames the game
needs are pre-extracted into a uniform atlas rather than sliced at runtime:

```bash
cd tools && npm install        # pngjs; the game itself ships zero JS dependencies
npm run sprites                # -> wwwroot/assets/atlas_party.png
```

`tools/extract-sprites.mjs` keys out the backdrop with a **flood fill seeded from each
frame's border** — not a colour threshold, which would eat the dark outlines *inside* the
sprites — then trims to the true bounding box and packs everything into uniform cells, one
row per character, feet resting on the cell floor. Frame boxes live in the `SPECS` table at
the top of that file; adjust them there to add poses.

Atlas layout — cells are 51×63 with the ground line at row 61, one row per character:

| Row | Character | Columns 0–5 | Columns 6–7 | Column 8 |
|-----|-----------|-------------|-------------|----------|
| 0 | Mage | idle / walk | cast | hellfire channel |
| 1 | Cleric | idle / walk | windup, mace thrust | holy nova channel |
| 2 | Thief (Exploradora) | idle / walk | draw, loose | crescent burst |
| 3 | Crystal Husk | 3 frames | — | — |

The tool prints `cell = 51x63, ground = 61` on every run; **those three numbers must
match `ATLAS.party` in `game.js`**. Change a frame box and the cell can resize, so re-read
that line after editing `SPECS`.

### Why frames are aligned to the idle pose

Ability frames bundle the character *with* a ground effect — the mage's magic circle, the
cleric's swirl — and those effects hang **below the boots**. Bottom-aligning each frame's
own bounding box therefore rests the *effect* on the floor and leaves the hero hovering
above it.

So the packer anchors every frame to its row's **frame 0**, the plain idle pose whose
lowest pixel is the feet by definition, and exports that as `ground`. `drawSprite` lands
`ground` on the hero's foot position instead of the cell bottom, which lets effects spill
below the floor line while the hero stays planted on it.

> When editing `SPECS`, note that the sheet interleaves **poses** with **detached effect
> sprites** (the cleric's golden nova at x≈486, the thief's green crescent). Those are
> effects, not frames — the game already draws its own slash/nova, so picking one as a
> pose gives a hero who fires a stray starburst.

Animation is driven by **distance travelled**, not a wall clock ([`animFrame` in
`game.js`](src/AlfarQuest.Client/wwwroot/js/game.js)) — the engine sends positions rather
than a "moving" flag, so the walk cycle stays in step at any speed and settles into a slow
sway when standing still. Sprites mirror horizontally with facing, and flash white on hit
via a pre-tinted copy of the atlas.

The attack pose is driven by `Hero.AttackAnim`, set to `0.22s` in `DoAttack` and counted
down each tick; `game.js` maps that window onto columns 6–7. It is deliberately shorter
than the fastest cooldown (the Thief's `0.30s`) so the pose always resolves back to the
walk cycle instead of latching on during sustained fire. `Hero.AbilityAnim` works the same
way for column 8 at `0.45s` — slightly longer than the `0.4s` nova it spawns, so the
channel pose outlasts its own shockwave. Ability outranks attack when both are live.

### Floor and paths

The cave floor is tiled from `Caves/MainLev2.0.png`. Its **32px** grid isn't documented
anywhere in the pack — it was established from the sheet's own transparent cut-outs, which
land exactly on 32px boundaries (a 32×64 hole at x=64, y=992). Autocorrelation was
misleading here: it favours ever-smaller periods, because in pixel art neighbouring rows
always resemble one another.

`FLOOR_TILES` lists only **centre-fill** tiles — ones that repeat without a seam, verified
by tiling each candidate 5×5 and looking. Most of that sheet is autotile edges and corners,
which show hard diagonal seams the moment they're used as fill.

Paths are drawn over the base with the worn-dirt tile. Routes are polylines from the spawn
to the exit plus spurs to crystal clusters, bowed sideways by a sine that peaks mid-route
and returns to zero at both ends — so the path wanders like something worn by feet, yet
still actually reaches the points it connects. Tiles within the core radius get the path at
full strength, and a fading band outside it feathers the edge instead of ending on a hard
tile boundary.

The whole chamber floor is painted **once** into an offscreen canvas. Tiling it live would
be ~2000 `drawImage` calls per frame; this way each frame costs a single blit. Measured at
60 fps with the floor in.

### Scenery

The cistern is furnished at **render time only** — `PROPS`/`DECALS` in `game.js` name cells
of `Miner_Decorations.png`, and `buildScenery()` scatters them once on the first frame.
Nothing about them exists in the simulation, so they cost the engine nothing and never
block a hero.

Placement is a seeded `mulberry32`, not `Math.random`: the chamber itself is fixed (`World`
seeds its own `Random` with 42), and scenery that reshuffled on every reload would feel
wrong. Props reject positions that collide with a crystal, with each other, or with the
260-unit circle around the party's spawn, so the opening shot stays clean. If the chamber
is too crowded to fit them all, the dropped count is logged rather than silently thinned.

Landmarks (`n: 1` — the crystal arch, the altars, the gargoyle well) stay unique; mining
debris repeats, so the gallery reads as worked-over rather than decorated. Props are drawn
in the crystals' depth tier, i.e. as one static layer behind the actors — the same
simplification the crystals already used. Decals are painted flat under everything.

The chamber's size comes from `RenderState.chamberW/H` rather than a copy of
`World.ChamberW/H` in JS, so resizing the room can't silently strand the scenery.

## The descent

Clearing a chamber is no longer the end of the slice. To advance:

1. **Kill every husk** — the phase flips to `cleared` and the exit mouth lights up at the
   top of the chamber.
2. **Walk the active hero into the mouth** (within 62 units). A 0.8s grace period stops the
   descent firing on the same frame the last husk dies while a movement key is still held.

`World.Descend()` then increments `Level`, reseeds its `Random`, rebuilds the chamber,
respawns the party at the new entrance and grants **+45 HP** as a breather — the fallen stay
fallen. Each level spawns `9 + level*2` husks, so the delve gets steadily worse.

Level 1 keeps its **handcrafted** crystal ring: it is the chamber from the novel. Levels 2+
are generated, with pillars biased toward the walls, a clear centre, and a guaranteed gap
around both the exit mouth and the party's entrance so a descent can never strand you
inside rock. Regions are named (`RegionNames`) and the HUD shows the name and depth.

The client caches the floor and scenery, both keyed to the crystal layout, so `game.js`
watches `RenderState.level` and throws both away on a change — and seeds them off the level
number, so each region looks different.

## Party formation

Companions steer to **distinct slots** offset from the leader (`CompanionSlots` in
`GameEngine.cs`), not to the leader's exact position — chasing the same point made them
converge and render stacked on top of one another. The slot is derived from party index
rather than a running count, so nobody hops to the other side when a third hero dies.

`SeparateParty()` is the backstop: after everyone has moved, any two heroes closer than 34
units are pushed apart, and the hero the player is steering is never shoved. Slots alone
aren't enough because crystals, walls and the chamber clamp can still squeeze two
companions together.

## Input

Discrete actions (hero switch `1`/`2`/`3`, ability `K`, dash `Shift`/`Space`) are **latched
on keydown** and cleared once the engine has consumed them. Input is otherwise sampled once
per frame, so a quick tap whose keydown *and* keyup both land between two frames used to be
dropped entirely — switching heroes with a fast tap failed intermittently. Held state is
still OR-ed in, so holding a key keeps re-triggering on cooldown as before. Attack (`J`)
stays level-triggered on purpose: hold to keep firing.

## Lore mapping

| Game element        | Source in *Story for Music*                                   |
|---------------------|---------------------------------------------------------------|
| The Cistern chamber | The crystal gallery with mirror-reflections and trapped souls |
| The husks           | The gem-riddled semi-undead the party fights in *Cistern*     |
| Mage's ultimate     | The caged fire-demon bound in his heart                        |
| Cleric's holy nova  | His blessed plate, gilded mace, and pleas to the Holy Light    |
| Thief's shard-arm   | The stolen crystal fused into his arm after his crew vanished  |

Hero **display names are placeholders** — in the novel the three are only ever "The Mage,"
"The Cleric," and "The Thief," so name them in canon and update `Lore.cs` + `schema.sql`.

## Suggested next steps

1. **The golden "ascended" forms** — the sheet has a fully gilded ultimate figure for the
   Mage and the Cleric (source x≈565–594). The Thief has none, so this needs a design call
   before it can be a uniform mechanic.
2. **Make the scenery matter** — props are pure decoration today. The obelisks and toppled
   pillars are the obvious candidates for collision (`ResolveCrystalCollision` already does
   this for crystals), and the braziers for real light sources.
3. **Persist the descent** — `Level`/`Region` are runtime-only; wire them through
   `SaveGameDto` so a delve can be resumed, and stream a region-specific track per level.
4. **Per-hero quests, dialogue, and endings** — the three delvers each have a full backstory
   chapter that maps cleanly onto branching hero-specific questlines.
5. **Save/load UI** on top of the existing `SavesController`.
6. **Region themes** — one uploaded track per major region, keyed off `Region`.

## Notes / caveats

The solution builds clean on .NET 10 (zero warnings) and has been run end-to-end against
MySQL 9.x — heroes served from the database, and saves creating, updating, and
cascade-replacing their party rows through `POST`/`GET /api/saves`.

### MySQL provider: Oracle, not Pomelo

The EF Core provider is **`MySql.EntityFrameworkCore`** (Oracle's official one), not
Pomelo. Pomelo has no EF Core 10 release — its latest is 9.0.0 — so staying on it would
have pinned the data layer to EF Core 9. The swap costs one line in `Program.cs`:

```csharp
opt.UseMySQL(conn)   // Oracle: detects server capabilities itself
// was: opt.UseMySql(conn, new MySqlServerVersion(new Version(8, 0, 36)))   // Pomelo
```

Note the capitalisation — Oracle's extension is `UseMySQL`, Pomelo's is `UseMySql`.
`GameDbContext` and the entities are plain EF Core and needed no changes. If Pomelo ships
an EF Core 10 provider later, reverting is just that one line plus the package reference.

### HUD: two layers, one element per corner

The HUD is split across two layers that cannot see each other — the canvas (`drawHud` in
`game.js`) and DOM overlays (`Play.razor` + `app.css`). Nothing reconciles their layout, so
anchoring both to the same corner silently draws one through the other. That is exactly
what happened: the party panel ran through the controls legend, and the husk counter sat
under the "Abandon Delve" button.

The rule now is **one element per corner**:

| Corner | Element | Layer |
|---|---|---|
| top-left | husks remaining / "Chamber cleared" | canvas |
| top-centre | now playing | DOM |
| top-right | Abandon Delve | DOM |
| bottom-left | party panel | canvas |
| bottom-right | controls legend | DOM |

Below 720px wide there isn't room to keep both bottom corners apart, so the legend hides.
When adding to the HUD, pick a free corner — or move an existing element deliberately.

## Credits and licensing

The code is Apache 2.0 (see `LICENSE`).

**Art.** The cave tiles, decorations and character sheets under
`RPGW_Caves_v2.1/` and `src/AlfarQuest.Client/wwwroot/assets/` are a purchased
asset pack. Its licence (`RPGW_Caves_v2.1/_license.txt`) permits use and
modification for personal and commercial work, and publication on sites about
games — which is what this repository is. It does **not** permit reselling the
assets, original or modified, or using them in a logo or trademark. Credit is
not required by the licence but is given here because it should be.

**Music.** *The Ballad of the Wandering* and *Crystal Deep* are the band's own
tracks, included here as the game's soundtrack.

**Story.** The setting, the three delvers and the Cave Beyond Time come from
*Story for Music* by Alfar Quest.
