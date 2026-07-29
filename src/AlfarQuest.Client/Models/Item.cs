namespace AlfarQuest.Client.Models;

/// <summary>Every place an item can be worn.
///
/// Wider than the game currently has gear for. That is deliberate: the paper doll
/// is meant to show the shape of a whole character, and an empty Cape slot tells a
/// player something to look for, whereas a doll with six slots tells them the game
/// is small. Nothing here is persisted by name — a save stores item ids — so this
/// list can grow without touching anyone's save.</summary>
public enum Slot
{
    Helmet, Shoulders, Cape, Chest, Gloves, Belt,
    Necklace, Ring1, Ring2, Pants, Boots, Artifact,
    MainHand, OffHand, Shield,
    Pet, Mount,

    /// <summary>Not worn anywhere — a potion, a bundle of herbs, a ration. Carried
    /// in the pack and traded, but never equipped, so the paper doll ignores it.</summary>
    None,
}

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythic }

/// <summary>What kind of thing an item is, for the shop's filters and for deciding
/// which merchant will handle it. Derived from the slot when an item does not say,
/// so ordinary gear needs no annotation.</summary>
public enum ItemClass
{
    Weapon, Armor, Accessory, Consumable, Material, Quest, Magic, Food, Book, Tool,
}

/// <summary>Presentation for a category — the label and icon the shop's filter
/// tabs use. Beside the enum so a new category is one row.</summary>
public sealed record CategoryInfo(ItemClass Category, string Name, string Icon)
{
    public static readonly IReadOnlyList<CategoryInfo> All =
    [
        new(ItemClass.Weapon,     "Weapons",     "⚔"),
        new(ItemClass.Armor,      "Armor",       "🛡"),
        new(ItemClass.Accessory,  "Trinkets",    "◈"),
        new(ItemClass.Consumable, "Consumables", "🧪"),
        new(ItemClass.Material,   "Materials",   "◆"),
        new(ItemClass.Magic,      "Magic",       "✦"),
        new(ItemClass.Food,       "Food",        "🍞"),
        new(ItemClass.Book,       "Books",       "📖"),
        new(ItemClass.Tool,       "Tools",       "🔧"),
        new(ItemClass.Quest,      "Quest",       "❗"),
    ];

    public static CategoryInfo Of(ItemClass c) => All.First(x => x.Category == c);
}

/// <summary>Presentation for a rarity, beside the enum so a new tier is one row
/// and no component changes.</summary>
public sealed record RarityInfo(Rarity Rarity, string Name, string Colour)
{
    public static readonly IReadOnlyList<RarityInfo> All =
    [
        new(Rarity.Common,    "Common",    "#b8b6c8"),
        new(Rarity.Uncommon,  "Uncommon",  "#7fd694"),
        new(Rarity.Rare,      "Rare",      "#7fb6ff"),
        new(Rarity.Epic,      "Epic",      "#c98fff"),
        new(Rarity.Legendary, "Legendary", "#f0d99a"),
        new(Rarity.Mythic,    "Mythic",    "#ff7a6b"),
    ];

    public static RarityInfo Of(Rarity r) => All.First(x => x.Rarity == r);
}

/// <summary>Which column of the paper doll a slot stands in.</summary>
public enum DollColumn { Left, Right, Hands, Future }

/// <summary>Where a slot appears on the paper doll and what it looks like empty.
///
/// The layout lives here rather than in CSS so adding a slot is one row and the
/// doll rearranges itself. Icon is the silhouette shown while the slot is empty:
/// an outline of what belongs there reads as "nothing yet", where a blank square
/// reads as a rendering fault.</summary>
public sealed record SlotInfo(Slot Slot, string Name, string Icon, DollColumn Column, bool Available = true)
{
    public static readonly IReadOnlyList<SlotInfo> All =
    [
        new(Slot.Helmet,    "Helmet",    "⛑", DollColumn.Left),
        new(Slot.Shoulders, "Shoulders", "⩕", DollColumn.Left),
        new(Slot.Cape,      "Cape",      "🜲", DollColumn.Left),
        new(Slot.Chest,     "Armor",     "🛡", DollColumn.Left),
        new(Slot.Gloves,    "Gloves",    "🧤", DollColumn.Left),
        new(Slot.Belt,      "Belt",      "⌒", DollColumn.Left),

        new(Slot.Necklace,  "Necklace",  "◈", DollColumn.Right),
        new(Slot.Ring1,     "Ring",      "○", DollColumn.Right),
        new(Slot.Ring2,     "Ring",      "○", DollColumn.Right),
        new(Slot.Pants,     "Legs",      "👖", DollColumn.Right),
        new(Slot.Boots,     "Boots",     "🥾", DollColumn.Right),
        new(Slot.Artifact,  "Artifact",  "✧", DollColumn.Right),

        new(Slot.MainHand,  "Main Hand", "⚔", DollColumn.Hands),
        new(Slot.OffHand,   "Off Hand",  "🗡", DollColumn.Hands),
        new(Slot.Shield,    "Shield",    "🛆", DollColumn.Hands),

        // Shown because the brief names them, disabled because neither system
        // exists. A slot that quietly accepted an item it could do nothing with
        // would be worse than one that says it is not ready.
        new(Slot.Pet,       "Pet",       "🐾", DollColumn.Future, Available: false),
        new(Slot.Mount,     "Mount",     "🐎", DollColumn.Future, Available: false),
    ];

    public static SlotInfo Of(Slot s) => All.First(x => x.Slot == s);

    public static IEnumerable<SlotInfo> In(DollColumn column) => All.Where(x => x.Column == column);
}

/// <summary>A piece of equipment. Immutable: items are looked at far more often
/// than they change, and an equipped item is shared by the panel, the tooltip
/// and the stat calculation at once.</summary>
public sealed record Item
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Slot Slot { get; init; }
    public Rarity Rarity { get; init; } = Rarity.Common;
    public string Icon { get; init; } = "◆";
    public string Flavour { get; init; } = "";

    /// <summary>Attribute bonuses, folded into the wearer's totals.</summary>
    public Attributes Attributes { get; init; } = new();

    /// <summary>Flat combat bonuses that do not come from an attribute. For a
    /// weapon <see cref="Damage"/> is left zero — its damage is a rolled range,
    /// not a flat add — so this stays the province of rings and trinkets.</summary>
    public float Damage { get; init; }
    public float Armour { get; init; }
    public float CritChance { get; init; }

    /// <summary>What kind of weapon this is, if it is one. Null for armour and
    /// trinkets. Sets the whole feel: speed, reach, crit, stun, damage type.</summary>
    public WeaponType? Weapon { get; init; }

    /// <summary>The weapon's damage range, rolled fresh on every swing — the
    /// brief's first rule, that no attack deals a fixed number. Zero on anything
    /// that is not a weapon.</summary>
    public int DamageMin { get; init; }
    public int DamageMax { get; init; }

    /// <summary>Overrides the weapon type's default damage type — a flaming sword
    /// that deals Fire rather than Slashing. Null means "whatever the type
    /// deals".</summary>
    public DamageType? DamageKind { get; init; }

    /// <summary>What a merchant charges to sell this. The base value everything
    /// else prices off — a merchant buys it back for a fraction. Zero means it is
    /// not for sale.</summary>
    public int Value { get; init; }

    /// <summary>Overrides the category derived from the slot — a wand that is a
    /// Magic item rather than a Weapon, a ration that is Food. Null derives it, so
    /// ordinary gear needs no annotation.</summary>
    public ItemClass? Category { get; init; }

    /// <summary>What kind of thing this is, for the shop's filters. Weapons and
    /// armour come from the slot; anything else says so with <see cref="Category"/>.</summary>
    public ItemClass Kind => Category ?? Slot switch
    {
        Slot.MainHand or Slot.OffHand => ItemClass.Weapon,
        Slot.Necklace or Slot.Ring1 or Slot.Ring2 or Slot.Artifact => ItemClass.Accessory,
        Slot.None => ItemClass.Consumable,
        _ => ItemClass.Armor,
    };

    /// <summary>Worn on the body, so the paper doll and equip both apply. False for
    /// consumables and goods, which are carried but never equipped.</summary>
    public bool IsEquippable => Slot != Slot.None;

    /// <summary>The price to work from — the authored <see cref="Value"/> when it
    /// has one, otherwise a figure derived from rarity and what the item does. The
    /// fallback means every item can be sold for something sensible without each
    /// one being priced by hand; shop stock sets an explicit value for the numbers
    /// that matter.</summary>
    public int Worth => Value > 0 ? Value : DerivedValue();

    private int DerivedValue()
    {
        int baseByRarity = Rarity switch
        {
            Rarity.Common => 15, Rarity.Uncommon => 45, Rarity.Rare => 120,
            Rarity.Epic => 320, Rarity.Legendary => 900, Rarity.Mythic => 2500, _ => 15,
        };
        int fromStats = (int)(Damage * 4 + Armour * 3 + (DamageMin + DamageMax) * 2.5f + CritChance * 200);
        return baseByRarity + fromStats;
    }

    /// <summary>What the player pays a merchant to buy it, and gets selling it back.
    /// A merchant sells dear and buys cheap; the gap is where its living is.</summary>
    public int BuyPrice => Worth;
    public int SellPrice => Math.Max(1, (int)MathF.Round(Worth * 0.45f));

    public bool IsWeapon => Weapon is not null;
    public WeaponClass? WeaponInfo => Weapon is { } w ? WeaponClass.Of(w) : null;
    public DamageType EffectiveDamage => DamageKind ?? WeaponInfo?.Damage ?? DamageType.Slashing;

    /// <summary>The lines a tooltip shows. Built here so every surface that
    /// describes an item words it identically.
    ///
    /// The caller supplies the wording. These lines used to be interpolated
    /// strings, which baked English word order into the model — "18–24 Fire
    /// Damage" cannot be reordered into "18–24 de Dano de Fogo" once it is one
    /// finished string. Each line is now a format looked up by its English text,
    /// with the numbers passed in, so a translation is free to put them
    /// elsewhere in the sentence. With no formatter this yields the English,
    /// which keeps the model usable on its own.</summary>
    /// <param name="phrase">Translate a bare phrase (a damage type, an attribute).</param>
    /// <param name="format">Translate a format and fill it in.</param>
    public IEnumerable<string> Bonuses(Func<string, string>? phrase = null,
                                       Func<string, object?[], string>? format = null)
    {
        string P(string english) => phrase is null ? english : phrase(english);
        string F(string english, params object?[] args) =>
            format is null ? string.Format(english, args) : format(english, args);

        if (IsWeapon && DamageMax > 0)
        {
            var d = DamageTypeInfo.Of(EffectiveDamage);
            yield return F("{0}–{1} {2} Damage", DamageMin, DamageMax, P(d.Name));
            yield return F("{0} — {1} attack", P(WeaponInfo!.Name), P(Speed(WeaponInfo.SpeedFactor)));
            if (WeaponInfo.CritBonus > 0)
                yield return F("+{0}% Critical Chance", (WeaponInfo.CritBonus * 100).ToString("0"));
            if (WeaponInfo.StunChance > 0)
                yield return F("{0}% chance to stun", (WeaponInfo.StunChance * 100).ToString("0"));
        }
        if (Damage != 0) yield return F("+{0} Physical Damage", Damage.ToString("0"));
        if (Armour != 0) yield return F("+{0} Armour", Armour.ToString("0"));
        if (CritChance != 0) yield return F("+{0}% Critical Chance", (CritChance * 100).ToString("0.0"));
        foreach (var a in AttributeInfo.All)
        {
            var v = Attributes[a.Kind];
            if (v != 0) yield return F("+{0} {1}", v, P(a.Name));
        }
    }

    /// <summary>Turns a speed factor into a word a player reads faster than a
    /// number — the tooltip does not need two decimal places to say "slow".</summary>
    private static string Speed(float factor) => factor switch
    {
        <= 0.75f => "very fast",
        <= 0.95f => "fast",
        < 1.10f => "balanced",
        < 1.45f => "slow",
        _ => "very slow",
    };
}
