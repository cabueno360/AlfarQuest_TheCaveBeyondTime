namespace AlfarQuest.Client.Game;

// =====================================================================
//  The JS boundary. Field names are the wire format read by
//  wwwroot/js — renaming one is a breaking change.
// =====================================================================
public class InputState
{
    public bool up, down, left, right, attack, ability, dash, interact, potion;
    public int switchTo;            // 0 = none, 1..3 = party slot (F1-F3)
    public int cycle;               // -1 previous hero, +1 next (Tab / Shift+Tab)
    public int castSlot;            // 0 = none, 1..4 = hotbar skill
    public float mouseX, mouseY;    // screen space
    public float viewW = 1280, viewH = 720;
}

public class RenderState { public RVec cam = new(); public float chamberW, chamberH; public int level, tile, stage, rev; public bool outdoor; public string floorStyle = ""; public string mapId = ""; public List<REnt> ents = new(); public List<RLight> lights = new(); public List<RFloat> floats = new(); public RHud hud = new(); public List<RSound> sounds = new(); }

/// <summary>One sound the frame asked for: a family (a folder of interchangeable
/// takes, picked from at random on the JS side) and how loud, already faded for
/// distance. Not a file path — the engine names the event, the audio layer owns
/// which clip and how to pool it.</summary>
public class RSound { public string f = ""; public float v = 1f; }

public class RVec { public float x, y; }

public class WorldSnapshot
{
    public int rev, tile, cols, rows, stage;
    public float chamberW, chamberH;
    public List<string> map = new();
    public bool seam;          // this rev is a region seam, not a doorway
    public List<RProp> props = new();
    public List<RVec> arches = new();
    public List<RNpc> npcs = new();
    public RVec spawn = new(), exit = new();
}

public class REnt
{
    public string t = ""; public float x, y, r, f, life, flash, iframe, hp, mhp, atk, abl, s = 1, dprog;
    public string? c, a, name; public bool hero, active, dead, dying;

    /// <summary>Particles only: the fraction of life left, and whether to draw
    /// with lighter compositing. Sent rather than derived, because the client has
    /// no idea how long a mote was meant to live.</summary>
    public float t01 = 1; public bool add;
}

public class RProp { public float x, y, s; public int cx, cy, v; public string k = ""; public bool flip; public bool? paint; }
public class RNpc { public float x, y, f; public string kind = "", name = ""; public bool talking, inReach, merchant, hidden; }

public class RFloat { public float x, y, life; public string text = "", c = "#ffffff"; }
public class RLight { public float x, y, rad; public string c = "#ffffff"; }

public class RHud
{
    public string phase = ""; public int enemies, level, stage, dropsTried, dropsDelivered;
    public string objective = "", talkName = "", talkRole = "", talkLine = "", promptName = "";
    public bool atCraftsman, outdoor; public float shake, clearedFor;
    public float timeOfDay;    // world clock, 0–24 hours — drives day/night and the clock UI
    public string message = "", region = ""; public List<RHero> party = new();

    // The chamber's boss, when one is alive — drives its own health bar across the
    // top of the screen. Null when there is no boss in play.
    public RBoss? boss;

    // Progression, read by the canvas HUD. heroLevel is the steered hero's level,
    // distinct from `level` above, which is the cave depth — two different things
    // that both wanted the name "level".
    public int heroLevel = 1, xp, xpNext = 100;
    public float xpFrac, levelUpGlow;
    public string promptVerb = "";

    // Reward diagnostics, in the same spirit as dropsTried/dropsDelivered: they
    // separate "nothing was awarded" from "something was awarded and nobody
    // listened", and they show whether the one-shot rewards are staying claimed.
    public int xpAwards, xpTotal, claimsHeld, rewardsLeft;

    /// <summary>Seconds the simulation is held after an impact. Sent so the HUD
    /// could show it while tuning; the freeze itself is the engine's doing.</summary>
    public float hitStop;

    /// <summary>Live particle count, against the engine's own ceiling. A
    /// diagnostic in the same spirit as the drop counters: it separates "the
    /// effect did not fire" from "it fired and was trimmed".</summary>
    public int particles, particleCap, particlesEmitted;

    /// <summary>A live census of the creatures on the field, so a test can see the
    /// AI working without pixels: how many sleep, how many hunt, how many hostile
    /// bolts are in the air, and which species are present.</summary>
    public int asleep, chasing, fleeing, bolts;
    public string enemyKinds = "";

    /// <summary>The nearest creature to the steered hero — how far in tiles, the
    /// unit direction to it, and what it is. Purely a seam so a test can walk
    /// toward a fight rather than wander into a cliff; the game never reads these.</summary>
    public float nearestTiles = 999f, nearDx, nearDy;
    public string nearKind = "", nearState = "", nearAbility = "";

    /// <summary>The nearest creature that casts a ranged bolt (slime, swarm,
    /// spider) — distance in tiles and the way to it. Lets a test provoke a
    /// caster specifically rather than wander onto a bruiser.</summary>
    public float casterTiles = 999f, casterDx, casterDy;

    /// <summary>Whether the steered hero is standing where creatures will not
    /// follow. A test that finds itself in one knows to walk out before expecting
    /// a fight.</summary>
    public bool inSafeZone;

    /// <summary>The steered hero's tile, so a test can tell "not moving" from
    /// "moving but hemmed in".</summary>
    public int heroTx, heroTy;

    /// <summary>Cumulative counts, never reset: every monster ability performed and
    /// every hostile bolt loosed this session. A live count misses an instant bolt;
    /// a running total does not.</summary>
    public int castsFired, boltsFired, husksSummoned;

    /// <summary>Cumulative combat tallies — criticals, misses, blocks, dodges,
    /// stuns — and the last few damage numbers dealt, so a test can see the weapon
    /// rolls a range. The live count of stunned creatures rides alongside.</summary>
    public int crits, misses, blocks, dodges, stuns, stunnedNow;
    public int[] recentHits = [];

    /// <summary>The active hero's action hotbar — the four skills, each with its
    /// cooldown and whether it can be cast right now — plus the potion. Rebuilt for
    /// whoever is being steered, so switching heroes changes the whole bar.</summary>
    public List<RSkill> hotbar = new();
    public float potionCdFrac;
    public bool potionReady;
}

/// <summary>A boss on the field, for its own health bar: its name, how much of its
/// health is left, and whether it has crossed into its enrage.</summary>
public class RBoss
{
    public string name = "";
    public float hp, mhp;
    public bool enraged;
}

/// <summary>One hotbar slot as the HUD needs it: what it is, its shortcut, how
/// much of its cooldown is left (1 = just cast, 0 = ready), and whether it can be
/// pressed — learned yet, off cooldown, and affordable.</summary>
public class RSkill
{
    public int slot;
    public string name = "", icon = "", shortcut = "", desc = "";
    public float cdFrac;
    public int manaCost, unlockLevel;
    public bool unlocked, affordable, ready;
}

public class RHero
{
    public string key = "", name = "", cls = "", color = "";
    public float hp, mhp, mana, mmana, stam, mstam;
    /// <summary>This hero's own level and experience. Individual now — the party
    /// no longer shares a level — so each row carries its own, and the HUD shows
    /// whichever hero is being steered.</summary>
    public int level = 1, xp, xpNext = 100;

    /// <summary>The equipped weapon as combat sees it — its rolled damage range,
    /// damage type, how fast it swings (1 is a plain sword), its stun chance — and
    /// the wearer's dodge and accuracy. Enough for a test to tell a hammer from a
    /// dagger without opening the sheet.</summary>
    public int dmgMin, dmgMax;
    public string dmgType = "";
    public float weaponSpeed = 1f, stunChance, dodge, accuracy;

    public bool active, dead, abilityReady;
    /// <summary>False when the ultimate is off cooldown but unaffordable, so the
    /// HUD can say "waiting on mana" rather than showing a ready prompt that does
    /// nothing when pressed.</summary>
    public bool abilityAffordable;
    public int slot;
}
