namespace AlfarQuest.Client.Models;

/// <summary>How an effect's particles leave their origin.</summary>
public enum Emission
{
    /// <summary>Every direction at once. Impacts and bursts.</summary>
    Burst,
    /// <summary>An even ring. Reads as something rising out of a point rather
    /// than an explosion landing on it.</summary>
    Ring,
    /// <summary>A cone about a given angle. Sparks off a struck edge.</summary>
    Cone,
    /// <summary>Upward and outward, then falling. Coins, dust from a footfall.</summary>
    Fountain,
    /// <summary>Downward from above. Falling dust, drips, leaves.</summary>
    Fall,
}

/// <summary>One visual effect, as data.
///
/// This is the system the brief asks for: colour, size, lifetime, count, blend
/// mode, emission direction, screen shake and hit-stop are all fields, so an
/// effect is authored rather than coded. Nothing in the engine switches on an
/// effect id — <c>Play("hit_crystal", at)</c> looks the row up and spawns what it
/// says. Adding an effect is a row here and a call.
///
/// <see cref="Sound"/> names a sound family — a folder of interchangeable takes
/// the audio layer picks from at random — played wherever the effect is. Left
/// null for effects that either want no sound (the silent ambient motes) or whose
/// sound depends on something the catalogue cannot see, like the terrain under a
/// footfall, which the call site decides instead.</summary>
public sealed record VfxSpec(
    string Id,
    string[] Colours,
    int Count,
    float Speed,
    float Life,
    float Size = 3f,
    Emission Shape = Emission.Burst,
    float Spread = MathF.Tau,
    float Gravity = 0f,
    float Drag = 1f,
    bool Additive = false,
    float Shake = 0f,
    float HitStop = 0f,
    float Vary = 0.5f,
    string? Sound = null)
{
    public static readonly IReadOnlyList<VfxSpec> All =
    [
        // ---- what a hit is made of -----------------------------------
        // One effect per material, because a sword landing on crystal should not
        // look like the same sword landing on a body.
        new("hit_flesh",   ["#a8323c", "#7d2630", "#c0555c"], 7,  120, 0.35f, 3f,
            Emission.Cone, 1.5f, Gravity: 260f, Drag: 0.88f, Shake: 0.14f, HitStop: 0.035f, Sound: "hit"),

        new("hit_stone",   ["#b8b6c8", "#8e8ba0", "#e0dcc8"], 9,  150, 0.40f, 2.5f,
            Emission.Cone, 1.4f, Gravity: 420f, Drag: 0.9f, Shake: 0.18f, HitStop: 0.04f, Sound: "hit"),

        new("hit_metal",   ["#fff4d0", "#ffd77a", "#ffae3a"], 12, 220, 0.28f, 2f,
            Emission.Cone, 1.1f, Gravity: 320f, Drag: 0.86f, Additive: true, Shake: 0.2f, HitStop: 0.045f, Sound: "hit"),

        new("hit_crystal", ["#9fe4ff", "#d6f2ff", "#5aa8d8"], 11, 180, 0.45f, 2.5f,
            Emission.Cone, 1.3f, Gravity: 300f, Drag: 0.9f, Additive: true, Shake: 0.16f, HitStop: 0.04f, Sound: "hit"),

        new("hit_wood",    ["#c9a06a", "#8a6a42", "#e0c9a0"], 8,  140, 0.42f, 2.5f,
            Emission.Cone, 1.4f, Gravity: 380f, Drag: 0.9f, Shake: 0.12f, HitStop: 0.03f, Sound: "hit"),

        // ---- the critical: the same blow, louder ---------------------
        // Longer hit-stop than any ordinary hit. The pause is most of what makes
        // a critical feel heavy — more particles alone read as noise.
        new("crit",        ["#ffd77a", "#fff4d0", "#ff9a3c"], 20, 260, 0.5f, 3.5f,
            Emission.Burst, MathF.Tau, Gravity: 180f, Drag: 0.88f, Additive: true,
            Shake: 0.55f, HitStop: 0.11f, Sound: "crit"),

        // ---- dying ---------------------------------------------------
        new("death_dust",  ["#6b6478", "#9a95b6", "#4a4458"], 16, 90, 0.85f, 4f,
            Emission.Burst, MathF.Tau, Gravity: -30f, Drag: 0.93f),

        new("death_magic", ["#c98fff", "#9fe4ff", "#e7ccff"], 22, 70, 1.1f, 3f,
            Emission.Ring, MathF.Tau, Gravity: -70f, Drag: 0.95f, Additive: true, Sound: "spell_impact"),

        // ---- movement ------------------------------------------------
        new("footfall",    ["#c9b48a", "#a8946e"], 3, 26, 0.4f, 2f,
            Emission.Fountain, 1.2f, Gravity: 60f, Drag: 0.9f, Vary: 0.8f),

        new("dash_dust",   ["#d8c9a8", "#b8a684"], 10, 90, 0.5f, 3f,
            Emission.Cone, 1.8f, Gravity: 40f, Drag: 0.88f, Sound: "dash"),

        new("water_step",  ["#9fd6ff", "#d6f2ff", "#6fa8d8"], 6, 70, 0.45f, 2.5f,
            Emission.Fountain, 1.4f, Gravity: 300f, Drag: 0.9f, Additive: true, Sound: "step_water"),

        // ---- magic, by school ----------------------------------------
        new("fire",        ["#ff8a3c", "#ffd06b", "#c0392b"], 26, 150, 0.6f, 4f,
            Emission.Ring, MathF.Tau, Gravity: -120f, Drag: 0.9f, Additive: true, Shake: 0.5f, Sound: "magic_fire"),

        new("holy",        ["#f0d99a", "#fff6d8", "#ffd77a"], 24, 110, 0.8f, 3.5f,
            Emission.Ring, MathF.Tau, Gravity: -90f, Drag: 0.94f, Additive: true, Shake: 0.4f, Sound: "magic_holy"),

        new("frost",       ["#bfe9ff", "#e8f7ff", "#7fb6ff"], 24, 130, 0.7f, 3f,
            Emission.Ring, MathF.Tau, Gravity: 60f, Drag: 0.92f, Additive: true, Shake: 0.4f, Sound: "magic_frost"),

        // ---- the world -----------------------------------------------
        new("levelup",     ["#f0d99a", "#e7ccff", "#fff6d8"], 30, 95, 1.2f, 3.5f,
            Emission.Ring, MathF.Tau, Gravity: -110f, Drag: 0.95f, Additive: true, Shake: 0.7f),

        new("chest_open",  ["#f0d99a", "#ffd77a", "#fff4d0"], 14, 110, 0.7f, 3f,
            Emission.Fountain, 1.6f, Gravity: 280f, Drag: 0.9f, Additive: true, Shake: 0.12f, Sound: "chest"),

        new("chest_rare",  ["#f0d99a", "#fff6d8", "#c98fff"], 30, 170, 1.0f, 4f,
            Emission.Ring, MathF.Tau, Gravity: -60f, Drag: 0.93f, Additive: true, Shake: 0.45f, Sound: "chest_rare"),

        new("mine",        ["#b8b6c8", "#8e8ba0", "#c9a06a"], 12, 160, 0.5f, 2.5f,
            Emission.Cone, 1.6f, Gravity: 460f, Drag: 0.9f, Shake: 0.2f, HitStop: 0.03f, Sound: "mine"),

        new("gather",      ["#7fd694", "#a8f0be", "#4e9c5e"], 10, 80, 0.6f, 2.5f,
            Emission.Fountain, 1.8f, Gravity: 120f, Drag: 0.92f, Sound: "chop"),

        new("coins",       ["#f0d99a", "#d8b45a"], 8, 130, 0.75f, 2.5f,
            Emission.Fountain, 1.1f, Gravity: 520f, Drag: 0.95f, Additive: true),

        new("discovery",   ["#a8f0de", "#d6f2ff", "#9fe4ff"], 18, 60, 1.3f, 3f,
            Emission.Ring, MathF.Tau, Gravity: -50f, Drag: 0.96f, Additive: true),

        new("talk",        ["#f0d99a", "#fff6d8"], 6, 40, 0.6f, 2f,
            Emission.Fountain, 0.9f, Gravity: -60f, Drag: 0.94f, Additive: true),

        new("block",       ["#cfd6ff", "#ffffff"], 8, 130, 0.3f, 2.5f,
            Emission.Cone, 1.2f, Gravity: 200f, Drag: 0.88f, Additive: true, Shake: 0.1f, Sound: "block"),

        // ---- ambient -------------------------------------------------
        // Emitted continuously rather than on an action. Low counts on purpose:
        // this runs every frame across the whole visible field.
        new("motes_forest", ["#d8e8b0", "#c9d89a", "#f0e8c0"], 1, 12, 3.2f, 2f,
            Emission.Fall, 0.6f, Gravity: 8f, Drag: 0.995f, Vary: 1f),

        new("motes_cave",   ["#8a92b8", "#6b7398", "#a8b0d0"], 1, 8, 3.6f, 2f,
            Emission.Fall, 0.4f, Gravity: 14f, Drag: 0.995f, Additive: true, Vary: 1f),
    ];

    private static readonly Dictionary<string, VfxSpec> ById = All.ToDictionary(v => v.Id);

    public static VfxSpec? Find(string id) => ById.GetValueOrDefault(id);
}
