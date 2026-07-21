namespace AlfarQuest.Client.Models;

/// <summary>A currency the party can hold. Data-driven on purpose: adding Soul
/// Fragments or Ancient Coins is one entry in <see cref="All"/>, and every panel
/// that shows money picks it up without changing.</summary>
public sealed record Currency(string Key, string Name, string Symbol, string Colour)
{
    public static readonly IReadOnlyList<Currency> All =
    [
        new("gold",   "Gold",           "◈", "#f0d99a"),
        new("silver", "Silver",         "◇", "#cfd6ff"),
        new("shards", "Soul Fragments", "✦", "#9fe4ff"),
    ];

    public static Currency? Find(string key) => All.FirstOrDefault(c => c.Key == key);
}

/// <summary>What the party is carrying, keyed by currency. Unknown keys simply
/// read as zero, so a save written before a currency existed still loads.</summary>
public sealed class Wallet
{
    private readonly Dictionary<string, long> _amounts = [];

    public long this[string key] => _amounts.GetValueOrDefault(key);
    public void Add(string key, long amount) => _amounts[key] = this[key] + amount;
    public void Set(string key, long amount) => _amounts[key] = amount;

    public void Clear() => _amounts.Clear();

    public IEnumerable<(Currency Currency, long Amount)> Held() =>
        Currency.All.Select(c => (c, this[c.Key])).Where(x => x.Item2 > 0);
}
