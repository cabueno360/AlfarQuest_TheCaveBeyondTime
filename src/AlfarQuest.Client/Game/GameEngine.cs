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

    /// <summary>Where the running world would resume from — read by the campaign
    /// save on the way out. Null before Init, which the saver falls back from.</summary>
    public static (string Region, float X, float Y)? ResumePoint => _world?.ResumePoint();

    // Fetched by game.js whenever RenderState.rev changes — see WorldSnapshot.
    [JSInvokable]
    public static string Snapshot()
    {
        // Captured once: Init() can replace the static mid-call (a New Game
        // over a running session), and a torn read here serialized half of one
        // world and half of the next.
        if (_world is not { } w) return "{}";
        return JsonSerializer.Serialize(w.TakeSnapshot(), Json);
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

    // The map-migration seam: load a building's interior so it can be exported the
    // same way Stage 1 was. Never called from play — see World.Interiors.
    [JSInvokable]
    public static void DebugLoadInterior(string id) => _world?.EnterInterior(id, default);

    [JSInvokable]
    public static void DebugEnterCave() => _world?.DebugEnterCave();

    [JSInvokable]
    public static void DebugLeaveCave() => _world?.DebugLeaveCave();

    [JSInvokable]
    public static void DebugDive() => _world?.DebugDive();

    [JSInvokable]
    public static void DebugGrantXp(int xp) => _world?.DebugGrantXp(xp);

    // Set a quest/progress flag directly, so a test can drive the quest chain
    // without playing every step — see World.Debug. Never called from play.
    [JSInvokable]
    public static void DebugClaim(string flag) => _world?.DebugClaim(flag);

    // Set the world clock to an hour, so a test can see any time of day at once.
    [JSInvokable]
    public static void DebugSetTime(double hour) => _world?.DebugSetTime((float)hour);

    // Drop the steered hero beside the chamber's boss, so a test can watch the
    // fight without walking to it. Never called from play — see World.Debug.
    [JSInvokable]
    public static void DebugWarpToBoss() => _world?.DebugWarpToBoss();

    // Stands up a hand-authored overworld region, so one can be walked and judged
    // before the ring is closed and Stage 1 moves onto it. Never called from play.
    [JSInvokable]
    public static bool DebugLoadRegion(string id) => _world?.LoadRegion(id) ?? false;

    // The map-migration seam: the built world as plain data, so it can be written
    // out as a Tiled .tmx. Never called from play — see World.Debug.
    [JSInvokable]
    public static string DebugExportMap() =>
        _world is null ? "{}" : JsonSerializer.Serialize(_world.ExportMap(), Json);

    // A test seam: stage a controlled encounter on open ground. Never called from
    // play — see World.Debug.
    [JSInvokable]
    public static void DebugEncounter() => _world?.DebugEncounter();

    // A test seam: one immortal, evasive target to accumulate attack rolls on.
    [JSInvokable]
    public static void DebugTrainingDummy() => _world?.DebugTrainingDummy();

    // A test seam: open a named merchant's shop as the steered hero, through the
    // same [E] path as play — see World.Debug.
    [JSInvokable]
    public static void DebugTradeWith(string npcId) => _world?.DebugTradeWith(npcId);

    // Open a named NPC's dialogue (and any quest offer it carries) as the steered
    // hero, without walking there. Never called from play — see World.Debug.
    [JSInvokable]
    public static void DebugTalkTo(string npcId) => _world?.DebugTalkTo(npcId);

    // A test seam: drop the steered hero on a tile (e.g. beside a door).
    [JSInvokable]
    public static void DebugWarp(double tx, double ty) => _world?.DebugWarp((float)tx, (float)ty);

    // The trading model as data, so a test can assert who keeps a shop, that a
    // merchant sells dearer than they buy, and that each shelf is stocked — none
    // of which needs the world running. Prices are read through ShopEconomy, so
    // this also proves the economy seam returns list price with its levers off.
    [JSInvokable]
    public static string MerchantFacts() => JsonSerializer.Serialize(new
    {
        merchants = MerchantCatalog.All.Select(m =>
        {
            var npc = NpcCatalog.Find(m.NpcId);
            var first = m.Stock
                .Select(s => Services.Character.ItemCatalog.Find(s.ItemId))
                .FirstOrDefault(i => i is not null);
            return new
            {
                npcId = m.NpcId,
                name = npc?.Name ?? m.NpcId,
                role = npc?.Role ?? "",
                isShopNpc = npc?.Services.HasFlag(NpcServices.Shop) ?? false,
                special = m.Special,
                restock = m.Restock.ToString(),
                stockLines = m.Stock.Count,
                buysAnything = m.Buys.Count == 0,
                buys = m.Buys.Select(b => b.ToString()),
                sample = first is null ? null : new
                {
                    id = first.Id, name = first.Name,
                    buy = ShopEconomy.BuyPrice(first, null, m),
                    sell = ShopEconomy.SellPrice(first, null, m),
                },
            };
        }),
    }, Json);

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
        // One capture for the whole frame: if Init() swaps the world mid-tick,
        // this frame finishes on the world it started with rather than
        // updating one and rendering the other.
        if (_world is not { } w) return "{}";
        var input = JsonSerializer.Deserialize<InputState>(inputJson, Json) ?? new InputState();
        float dt = Math.Min(0.05f, (float)dtMs / 1000f); // clamp to avoid tunnelling on lag
        w.Update(dt, input);
        return JsonSerializer.Serialize(w.Render(), Json);
    }
}
