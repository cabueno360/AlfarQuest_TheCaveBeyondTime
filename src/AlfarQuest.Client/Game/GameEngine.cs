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
