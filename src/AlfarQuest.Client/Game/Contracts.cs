namespace AlfarQuest.Client.Game;

// =====================================================================
//  The JS boundary. Field names are the wire format read by
//  wwwroot/js — renaming one is a breaking change.
// =====================================================================
public class InputState
{
    public bool up, down, left, right, attack, ability, dash, interact;
    public int switchTo;            // 0 = none, 1..3 = party slot
    public float mouseX, mouseY;    // screen space
    public float viewW = 1280, viewH = 720;
}

public class RenderState { public RVec cam = new(); public float chamberW, chamberH; public int level, tile, stage, rev; public List<REnt> ents = new(); public List<RLight> lights = new(); public List<RFloat> floats = new(); public RHud hud = new(); public List<RSound> sounds = new(); }

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
    public List<RProp> props = new();
    public List<RVec> arches = new();
    public List<RNpc> npcs = new();
    public RVec spawn = new(), exit = new();
}

public class REnt
{
    public string t = ""; public float x, y, r, f, life, flash, iframe, hp, mhp, atk, abl, s = 1;
    public string? c, a, name; public bool hero, active, dead;

    /// <summary>Particles only: the fraction of life left, and whether to draw
    /// with lighter compositing. Sent rather than derived, because the client has
    /// no idea how long a mote was meant to live.</summary>
    public float t01 = 1; public bool add;
}

public class RProp { public float x, y, s; public int cx, cy, v; public string k = ""; public bool flip; }
public class RNpc { public float x, y, f; public string kind = "", name = ""; public bool talking, inReach; }

public class RFloat { public float x, y, life; public string text = "", c = "#ffffff"; }
public class RLight { public float x, y, rad; public string c = "#ffffff"; }

public class RHud
{
    public string phase = ""; public int enemies, level, stage, dropsTried, dropsDelivered;
    public string objective = "", talkName = "", talkRole = "", talkLine = "", promptName = "";
    public bool atCraftsman; public float shake, clearedFor;
    public string message = "", region = ""; public List<RHero> party = new();

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
}

public class RHero
{
    public string key = "", name = "", cls = "", color = "";
    public float hp, mhp, mana, mmana, stam, mstam;
    public bool active, dead, abilityReady;
    /// <summary>False when the ultimate is off cooldown but unaffordable, so the
    /// HUD can say "waiting on mana" rather than showing a ready prompt that does
    /// nothing when pressed.</summary>
    public bool abilityAffordable;
    public int slot;
}
