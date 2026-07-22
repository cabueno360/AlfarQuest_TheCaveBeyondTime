namespace AlfarQuest.Client.Game;

// =====================================================================
//  What a creature leaves behind.
// =====================================================================
public partial class World
{
    /// <summary>Text that floats up from a drop. Purely presentational, but it
    /// lives here because the engine is what knows a kill happened.</summary>
    public List<FloatText> Floaters { get; } = [];

    void RollLoot(Husk k)
    {
        var (luck, yield) = CharacterStats.PartyFortune?.Invoke() ?? (0f, 0f);

        foreach (var (item, chance) in k.Def.Loot)
        {
            if (_rng.NextDouble() > chance * (1f + luck)) continue;

            // Coins are money; everything else is a material. Prospecting only
            // multiplies materials — it is a miner's eye, not a purse.
            var count = item == "Coins"
                ? 3 + _rng.Next(9)
                : 1 + (int)MathF.Floor(yield + (float)_rng.NextDouble() * yield);
            // To whoever is being steered — the one who walked up to the kill and
            // its spoils. XP goes to the killer; the loot goes to the one holding
            // the reins, which is usually but not always the same hero.
            LootBridge.Drop(item, count, SteeredKey);
            Floaters.Add(new FloatText(k.Pos, item == "Coins" ? $"+{count} coins" : $"+{item}",
                                       item == "Coins" ? "#f0d99a" : "#9fe4ff"));
        }
    }

    void UpdateFloaters(float dt)
    {
        foreach (var f in Floaters) { f.Life -= dt; f.Pos += new Vec(0, -18f * dt); }
        Floaters.RemoveAll(f => f.Life <= 0);
    }
}

public sealed class FloatText(Vec pos, string text, string colour)
{
    public Vec Pos = pos;
    public string Text { get; } = text;
    public string Colour { get; } = colour;
    public float Life = 1.6f;
}
