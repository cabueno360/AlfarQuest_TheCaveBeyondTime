namespace AlfarQuest.Client.Models;

/// <summary>What has happened to one container, in a form that survives the game
/// being closed.
///
/// Not just "opened": what is still inside, too. A chest you took the coin from
/// and walked away from has to still hold the rest when you come back, and the
/// only way to promise that is to write the remainder down. Recording opened-ness
/// alone would either give the rest away for free on the next visit or lose it.
///
/// <see cref="OpenedAt"/> is a UTC stamp rather than elapsed play time, because a
/// barrel that refills in half an hour should refill while the game is shut.</summary>
public sealed record ContainerSave(
    string Key,
    DateTime OpenedAt,
    int Coin,
    IReadOnlyList<MaterialStack> Materials,
    IReadOnlyList<string> Items)
{
    /// <summary>Reads the remainder back into a stack the world can use.</summary>
    public LootStack ToStack() =>
        new(Coin, [.. Materials.Select(m => (m.Id, m.Count))], [.. Items]);

    public static ContainerSave From(string key, DateTime openedAt, LootStack stack) =>
        new(key, openedAt, stack.Coin,
            [.. stack.Materials.Select(m => new MaterialStack(m.Id, m.Count))],
            [.. stack.Items]);

    /// <summary>Whether this container has been closed long enough to refill.
    /// Kinds that never come back answer false however long it has been.</summary>
    public bool HasRespawned(ContainerKind kind, DateTime now) =>
        kind.RespawnHours is { } hours && (now - OpenedAt).TotalHours >= hours;
}

/// <param name="Id">A material id. A named record rather than a tuple because it
/// crosses a serialisation boundary, and a tuple's Item1/Item2 would become the
/// field names on the wire.</param>
public sealed record MaterialStack(string Id, int Count);
