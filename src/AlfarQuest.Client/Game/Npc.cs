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

    /// <summary>The first thing they say when the conversation opens, above the list
    /// of questions. Empty falls back to the first of <see cref="Lines"/>.</summary>
    public string Greeting { get; init; } = "";

    /// <summary>A branching conversation: the questions you may put to them, each
    /// with its own answer. Empty means they only cycle <see cref="Lines"/> in a
    /// balloon; non-empty opens the question menu (DialogueWindow) instead.</summary>
    public IReadOnlyList<DialogueTopic> Topics { get; init; } = [];

    /// <summary>Whether this NPC answers a menu of questions rather than cycling
    /// one-liners — the tell that [E] should open the dialogue window.</summary>
    public bool HasDialogue => Topics.Count > 0;

    /// <summary>Keeping to their bed, so the renderer draws no standing sprite for
    /// them — they are already painted into the furniture (Mirka, in the prop cut
    /// from the second-floor plan). They stay in the world and stay talkable; only
    /// the upright figure is suppressed, since the engine has no lying pose.</summary>
    public bool Bedridden { get; init; }

    public NpcServices Services { get; init; } = NpcServices.None;
}

/// <summary>One question a traveller may ask and the answer it draws. The answer is
/// a list of pages so a long reply turns a leaf at a time, like a letter.
///
/// A topic may depend on quest state through the flag set (the persisted
/// ClaimedRewards). This is how NPCs give hints that change as the story moves and
/// how talking advances a quest — without it, every villager says the same thing
/// forever.</summary>
public sealed record DialogueTopic(string Q, params string[] A)
{
    /// <summary>Shown only once this flag is set — a topic that makes no sense until
    /// the player has reached a point in the story. Empty = always shown.</summary>
    public string ShowWhen { get; init; } = "";

    /// <summary>Hidden once this flag is set — a topic spent, or superseded by a
    /// later one. Empty = never hidden.</summary>
    public string HideWhen { get; init; } = "";

    /// <summary>A flag raised the first time this topic is asked — how a conversation
    /// advances a quest (learning of the Cave sets <c>quest_cave_learned</c>). Empty
    /// = the topic sets nothing. The flag persists, so the step stays done.</summary>
    public string SetsFlag { get; init; } = "";

    /// <summary>The id of a quest this topic OFFERS. Asking it does not start the
    /// quest outright — it raises the offer panel (terms and reward shown up front),
    /// and only Accept sets the quest's own StartFlag. Empty = the topic offers no
    /// quest. Use this instead of SetsFlag for the start of a takeable side quest.</summary>
    public string OffersQuest { get; init; } = "";

    /// <summary>Whether this topic should appear, given the flags set so far.</summary>
    public bool Visible(IReadOnlyCollection<string> flags) =>
        (ShowWhen.Length == 0 || flags.Contains(ShowWhen)) &&
        (HideWhen.Length == 0 || !flags.Contains(HideWhen));
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
