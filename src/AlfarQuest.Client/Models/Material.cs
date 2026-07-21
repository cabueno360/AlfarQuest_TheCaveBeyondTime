namespace AlfarQuest.Client.Models;

/// <summary>A crafting resource. Deliberately not an <see cref="Item"/>: materials
/// stack, have no slot and are never worn, so forcing them through the equipment
/// model would mean a nullable Slot on every piece of gear to serve a case that
/// is not equipment at all.</summary>
public sealed record Material(string Id, string Name, string Icon, string Colour)
{
    public static readonly IReadOnlyList<Material> All =
    [
        new("Stone",         "Stone",         "◼", "#b8b6c8"),
        new("Hide",          "Hide",          "▧", "#c9a06a"),
        new("Spider Silk",   "Spider Silk",   "≋", "#e7e0ff"),
        new("Bat Wing",      "Bat Wing",      "𝇋", "#c98fff"),
        new("Herbs",         "Herbs",         "❦", "#7fd694"),
        new("Small Crystal", "Small Crystal", "◆", "#9fe4ff"),
    ];

    public static Material? Find(string id) => All.FirstOrDefault(m => m.Id == id);
}

/// <summary>What the party has gathered. Unknown ids read as zero, so a drop
/// added in a later version does not break an older pouch.</summary>
public sealed class Satchel
{
    private readonly Dictionary<string, int> _counts = [];

    public int this[string id] => _counts.GetValueOrDefault(id);
    /// <summary>Negative amounts spend. Clamped at zero so a miscounted
    /// recipe can never leave a pouch owing materials.</summary>
    public void Add(string id, int n = 1) => _counts[id] = Math.Max(0, this[id] + n);

    public void Clear() => _counts.Clear();

    public IEnumerable<(Material Material, int Count)> Held() =>
        Material.All.Select(m => (m, this[m.Id])).Where(x => x.Item2 > 0);
}
