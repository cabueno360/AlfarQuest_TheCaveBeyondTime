using System.Text.Json;
using Microsoft.JSInterop;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Alfar Quest — vertical-slice engine ("The Crystal Cistern")
//  All simulation runs here in C#. The JS layer (game.js) only captures
//  input, calls Tick() once per frame, and paints the returned state.
// =====================================================================
public static class GameEngine
{
    private static World? _world;
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };

    // Called once by game.js when the scene starts.
    // heroKeysCsv: comma separated hero keys chosen on the select screen.
    [JSInvokable]
    public static void Init(string heroKeysCsv, double viewW, double viewH)
    {
        var keys = heroKeysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (keys.Length == 0) keys = new[] { "mage", "cleric", "thief" };
        _world = new World(keys, (float)viewW, (float)viewH);
    }

    // Fetched by game.js whenever RenderState.rev changes — see WorldSnapshot.
    [JSInvokable]
    public static string Snapshot()
    {
        if (_world is null) return "{}";
        return JsonSerializer.Serialize(_world.TakeSnapshot(), Json);
    }

    // The creature catalogue, as data, so a test can assert the bestiary is
    // varied and lore-placed without spawning anything. No world needed — it is
    // static.
    [JSInvokable]
    public static string Bestiary() => JsonSerializer.Serialize(
        CreatureCatalog.All.Select(c => new
        {
            id = c.Id, name = c.Name, biome = c.Biome.ToString(),
            demeanor = c.Demeanor.ToString(), ability = c.Ability.ToString(),
            fleeBelow = c.FleeBelow, protective = c.Protective, xp = c.Xp,
        }), Json);

    // A test seam: stage a controlled encounter on open ground. Never called from
    // play — see World.Debug.
    [JSInvokable]
    public static void DebugEncounter() => _world?.DebugEncounter();

    // A test seam: one immortal, evasive target to accumulate attack rolls on.
    [JSInvokable]
    public static void DebugTrainingDummy() => _world?.DebugTrainingDummy();

    // The combat model as data, so a test can assert the weapon types differ, the
    // damage schools exist, the status architecture names them all, and the
    // resistances land on the right types — none of which needs the world running.
    [JSInvokable]
    public static string CombatFacts() => JsonSerializer.Serialize(new
    {
        weapons = Models.WeaponClass.All.Select(w => new
        {
            type = w.Type.ToString(), name = w.Name, damage = w.Damage.ToString(),
            speed = w.SpeedFactor, crit = w.CritBonus, reach = w.Reach, stun = w.StunChance,
        }),
        damageTypes = Models.DamageTypeInfo.All.Select(d => new
        {
            name = d.Name, category = d.Category.ToString(),
        }),
        statusEffects = Models.StatusEffectInfo.All.Select(e => new
        {
            name = e.Name, behaviour = e.Behaviour.ToString(),
        }),
        resistances = new
        {
            slimePiercing = CreatureCatalog.Of("slime").Resistance(Models.DamageType.Piercing),
            slimeBlunt = CreatureCatalog.Of("slime").Resistance(Models.DamageType.Blunt),
            wormSlashing = CreatureCatalog.Of("worm").Resistance(Models.DamageType.Slashing),
            wormBlunt = CreatureCatalog.Of("worm").Resistance(Models.DamageType.Blunt),
        },
    }, Json);

    // Called every animation frame. inputJson is an InputState.
    // Returns a serialized RenderState for game.js to draw.
    [JSInvokable]
    public static string Tick(double dtMs, string inputJson)
    {
        if (_world is null) return "{}";
        var input = JsonSerializer.Deserialize<InputState>(inputJson, Json) ?? new InputState();
        float dt = Math.Min(0.05f, (float)dtMs / 1000f); // clamp to avoid tunnelling on lag
        _world.Update(dt, input);
        return JsonSerializer.Serialize(_world.Render(), Json);
    }
}
