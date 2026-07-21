namespace AlfarQuest.Client.Game;

/// <summary>Who an NPC is. Static data, shared by every instance of that role.
///
/// Services is a flags enum rather than separate lists so a shopkeeper who also
/// gives quests is one definition, and so adding Crafting or Reputation later is
/// a new flag instead of a new parallel structure.</summary>
public sealed record NpcDefinition
{
    public required string Id { get; init; }
    public required string Kind { get; init; }      // sprite key in atlas_chars.json
    public required string Name { get; init; }
    public required string Role { get; init; }

    /// <summary>What they say. A list, so a future dialogue tree can replace the
    /// rotation without changing anything that reads it.</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    public NpcServices Services { get; init; } = NpcServices.None;
}

[Flags]
public enum NpcServices
{
    None = 0,
    Shop = 1,
    Quest = 2,
    Crafting = 4,
    Training = 8,
}

/// <summary>An NPC standing in the world.</summary>
public sealed class Npc(NpcDefinition def, Vec pos)
{
    public NpcDefinition Def { get; } = def;
    public Vec Pos { get; } = pos;

    /// <summary>Which way they are looking. Turned toward whoever is talking to
    /// them, and left there afterwards.</summary>
    public float Facing { get; set; } = MathF.PI / 2;

    /// <summary>Which line comes next, so repeated talks rotate rather than
    /// repeating the same sentence.</summary>
    public int LineIndex { get; set; }

    public string CurrentLine => Def.Lines.Count == 0 ? "" : Def.Lines[LineIndex % Def.Lines.Count];
}
