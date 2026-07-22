using AlfarQuest.Client.Game;
using AlfarQuest.Client.Models;
using AlfarQuest.Client.Services.Character;

namespace AlfarQuest.Client.Services;

/// <summary>The party's character sheets — the single source of truth shared by
/// the UI and the simulation. The engine reads attribute-derived numbers from
/// here; the character window writes to it. Neither owns a second copy.</summary>
public sealed class PartyState
{
    private readonly Dictionary<string, Models.Character> _byKey = [];

    public IReadOnlyList<Models.Character> Members { get; private set; } = [];
    /// <summary>Shared across the party — coin is not carried per hero.</summary>
    public Wallet Purse { get; } = new();

    /// <summary>Likewise the pack: one party, one set of saddlebags.</summary>
    public Inventory Bag { get; } = new();

    /// <summary>Gathered materials, separate from worn gear.</summary>
    public Satchel Pouch { get; } = new();
    public Models.Character? Selected { get; private set; }

    /// <summary>Names of the one-shot rewards this player has taken in the
    /// overworld. A set, so claiming twice is harmless — which matters because
    /// the world can be rebuilt while the same party is loaded.</summary>
    public HashSet<string> ClaimedRewards { get; } = [];

    /// <summary>What has happened to each container the party has opened, keyed by
    /// its name — including what is still inside it. Written by the engine when
    /// one is opened and by the loot window after every take.</summary>
    public Dictionary<string, ContainerSave> Containers { get; } = [];

    public event Action? Changed;

    public PartyState()
    {
        // Publish attribute-derived modifiers to the simulation. Registered once,
        // here, so nothing else has to remember to keep the two in step.
        // Drops arrive from the simulation and are sorted here: coin to the purse,
        // everything else to the pouch.
        LootBridge.OnDrop = (item, count) =>
        {
            if (item == "Coins") Purse.Add("gold", count);
            else Pouch.Add(item, count);
            Changed?.Invoke();
        };

        CharacterStats.PartyFortune = () =>
        {
            float chance = 0, yield = 0;
            foreach (var c in Members)
            {
                var sk = c.SkillEffects();
                // Luck the attribute and Delver's Luck the skill both count. Only
                // the skill did before, which left the attribute describing a
                // benefit it never delivered.
                chance = Math.Max(chance, sk.LootChance + StatCalculator.LootChance(c.Total));
                yield = Math.Max(yield, sk.MaterialYield);
            }
            return (chance, yield);
        };

        // What the canvas HUD draws above the party panel.
        CharacterStats.Progress = key => Find(key) is { } c
            ? new HeroProgress(c.Level, c.Xp, Progression.XpForNext(c.Level))
            : HeroProgress.None;

        // One-shot rewards already taken, so re-entering the world does not
        // refill every chest. Read on world build, written as each is claimed.
        RewardBridge.ClaimedRewards = () => ClaimedRewards;
        RewardBridge.OnClaimed = key =>
        {
            if (ClaimedRewards.Add(key)) Changed?.Invoke();
        };

        RewardBridge.ContainerStates = () => Containers;
        RewardBridge.OnContainerSaved = state =>
        {
            Containers[state.Key] = state;
            Changed?.Invoke();
        };

        // Experience arrives from the simulation, credited to one hero — whoever
        // landed the killing blow, or whoever picked up the loot. Progression is
        // individual now: each hero grows at their own pace, and switching leaders
        // never syncs their levels.
        RewardBridge.OnXp = (xp, _, earner) => GainXp(xp, earner);

        CharacterStats.Lookup = key =>
        {
            if (Find(key) is not { } c) return HeroModifiers.None;
            var sk = c.SkillEffects();
            return new HeroModifiers(
                BonusDamage: StatCalculator.PhysicalDamage(c.Def.Damage, c.Total) - c.Def.Damage + c.Gear.Total().Damage,
                BonusMaxHp: StatCalculator.MaxHp(c.Def.BaseHp, c.Total) - c.Def.BaseHp,
                BonusSpeed: StatCalculator.MoveSpeed(c.Def.Speed, c.Total) - c.Def.Speed,
                CooldownReduction: c.Total.Dexterity * 0.006f,
                DamageMultiplier: sk.Damage,
                AbilityDamageMultiplier: sk.AbilityDamage,
                AbilityRadiusBonus: sk.AbilityRadius,
                DashCooldownMultiplier: sk.DashCooldown,
                AttackSpeedMultiplier: sk.AttackSpeed,
                HealthRegen: sk.HealthRegen,
                // Defense feeds this as well as Steady Guard. Skills alone reached
                // the engine before, so the attribute moved a label and nothing else.
                BlockChance: Math.Min(0.9f, StatCalculator.Block(c.Total) + sk.BlockChance),
                LootChance: sk.LootChance,
                MaterialYield: sk.MaterialYield,
                DamageReduction: StatCalculator.DamageReduction(
                    StatCalculator.PhysicalDefense(c.Total) + c.Gear.Total().Armour),
                MagicResistance: StatCalculator.MagicResistance(c.Total),
                CritChance: StatCalculator.CritChance(c.Total) + c.Gear.Total().CritChance,
                CritDamage: StatCalculator.CritDamage(c.Total),
                MaxMana: StatCalculator.MaxMana(c.Total),
                MaxStamina: StatCalculator.MaxStamina(c.Total),
                ManaRegen: StatCalculator.ManaRegen(c.Total),
                StaminaRegen: StatCalculator.StaminaRegen(c.Total));
        };
    }

    public void Load(IEnumerable<string> heroKeys)
    {
        Members = [.. heroKeys.Select(k => _byKey.TryGetValue(k, out var c) ? c : Create(k))];
        Selected ??= Members.FirstOrDefault();
        if (Purse["gold"] == 0)
        {
            Purse.Set("gold", 120);              // a delver's starting purse
            foreach (var item in StartingGear.Spares) Bag.Add(item);
        }
        Changed?.Invoke();
    }

    private Models.Character Create(string key)
    {
        var c = new Models.Character(Lore.ByKey(key));
        foreach (var item in StartingGear.For(c.Class)) c.Gear.Equip(item);
        _byKey[key] = c;
        return c;
    }

    public Models.Character? Find(string key) => _byKey.GetValueOrDefault(key);

    public void Select(string key)
    {
        if (_byKey.TryGetValue(key, out var c)) { Selected = c; Changed?.Invoke(); }
    }

    /// <summary>Raises an attribute and tells everyone, so every derived number on
    /// screen recomputes from the one change.</summary>
    public bool Raise(Models.Character c, AttributeKind kind)
    {
        if (!c.Raise(kind)) return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Copies live pools in from the simulation, which owns them. Called
    /// when a menu opens — the game is paused by then, so one read is enough and
    /// there is no per-frame interop cost.</summary>
    public void SyncVitals(IEnumerable<HeroVitals> vitals)
    {
        foreach (var v in vitals)
        {
            if (Find(v.Key) is not { } c) continue;
            // All four pools now come from the simulation, which owns them.
            // Mana and stamina used to be pinned full here because nothing spent
            // them — the sheet was reporting a resource that did not exist.
            c.Hp = v.Hp;
            c.MaxHp = v.MaxHp;
            c.Mana = v.Mana;
            c.MaxMana = v.MaxMana;
            c.Stamina = v.Stamina;
            c.MaxStamina = v.MaxStamina;
        }
        Changed?.Invoke();
    }

    /// <summary>Level-ups waiting to be shown, oldest first.
    ///
    /// A queue rather than a bare event because a single large award can cross
    /// two thresholds, and because XP can land while the previous window is still
    /// open — an event would show the last one and lose the rest.</summary>
    public Queue<LevelUp> PendingLevelUps { get; } = new();

    /// <summary>Raised when the queue goes from empty to non-empty, so the game
    /// page can pause and open the window. Not raised per level: the page drains
    /// the queue itself.</summary>
    public event Action? LevelledUp;

    /// <summary>The fraction of a kill's XP that companions who did not land it
    /// still earn. Zero disables assist experience entirely, which is the default:
    /// a kill belongs to whoever finished it. The architecture is here so a future
    /// setting can turn it on — Leader 100%, Companions 30% — without touching the
    /// award path.</summary>
    public static float AssistShare = 0f;

    /// <summary>Awards experience to the hero who earned it, and records who grew.
    ///
    /// Individual progression: the earner gets it in full, and companions get only
    /// the assist share (zero by default). The party used to share every award,
    /// which kept levels in lock-step and made switching heroes weightless — the
    /// opposite of what the game now wants.</summary>
    /// <param name="earnerKey">Whoever landed the killing blow, or picked up the
    /// reward. Their level-up is the one that gets the window.</param>
    /// <returns>How many heroes levelled — the engine uses it to decide whether
    /// to celebrate, and knows nothing else about progression.</returns>
    public int GainXp(int xp, string earnerKey = "")
    {
        if (xp <= 0 || Members.Count == 0) return 0;

        var earner = Find(earnerKey) ?? Selected ?? Members[0];
        var grew = new List<Models.Character>();
        var fromLevel = new Dictionary<string, int>();

        void Give(Models.Character c, int amount)
        {
            if (amount <= 0) return;
            var before = c.Level;
            c.GainXp(amount);
            if (c.Level == before) return;
            grew.Add(c);
            fromLevel[c.Key] = before;
        }

        Give(earner, xp);
        // The assist share, off by default. When enabled, everyone else on the
        // roster earns a slice — the disabled-but-supported system the spec asks
        // for. Rounded down so a 30% share of a small kill is not free levels.
        if (AssistShare > 0f)
            foreach (var c in Members)
                if (c != earner) Give(c, (int)MathF.Floor(xp * AssistShare));

        if (grew.Count > 0)
        {
            // The earner if they grew, otherwise whoever did — with assist off this
            // is always the earner, but the fallback keeps the window honest if a
            // future assist share levels a companion and not the lead.
            var star = grew.FirstOrDefault(c => c.Key == earner.Key) ?? grew[0];
            var from = fromLevel[star.Key];
            var levels = star.Level - from;

            PendingLevelUps.Enqueue(new LevelUp(
                star, from, star.Level,
                levels * Progression.AttributePointsPerLevel,
                levels * Progression.SkillPointsPerLevel,
                Companions: [.. grew.Where(c => c != star).Select(c => c.Name)]));
        }

        Changed?.Invoke();
        if (grew.Count > 0) LevelledUp?.Invoke();
        return grew.Count;
    }

    public bool Learn(Models.Character c, Skill s)
    {
        if (!c.Learn(s)) return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Moves an item from the pack onto the character. Whatever was in
    /// that slot goes back into the pack rather than vanishing.</summary>
    public void Equip(Models.Character c, Item item)
    {
        if (!Bag.Remove(item)) return;
        var displaced = c.Gear.Equip(item);
        if (displaced is not null && !Bag.Add(displaced))
        {
            // Pack is full: put it back rather than destroy the player's gear.
            c.Gear.Equip(displaced);
            Bag.Add(item);
            return;
        }
        Changed?.Invoke();
    }

    public void Unequip(Models.Character c, Slot slot)
    {
        if (c.Gear[slot] is not { } item) return;
        if (!Bag.Add(item)) return;              // no room: leave it worn
        c.Gear.Unequip(slot);
        Changed?.Invoke();
    }

    /// <summary>Spends the materials and coin, and puts the result in the pack.
    /// Returns false and changes nothing if anything is short — a partial craft
    /// that ate the materials and produced nothing would be the worst outcome.</summary>
    public bool Craft(Recipe r)
    {
        if (!r.Affordable(Pouch, Purse)) return false;
        if (Bag.IsFull) return false;

        foreach (var (mat, n) in r.Cost) Pouch.Add(mat, -n);
        Purse.Add("gold", -r.CoinCost);
        Bag.Add(r.Output);
        Changed?.Invoke();
        return true;
    }

    public void Notify() => Changed?.Invoke();
}

/// <summary>A hero's live pools, as reported by the simulation.</summary>
public sealed record HeroVitals(
    string Key, float Hp, float MaxHp, float Mana, float MaxMana, float Stamina, float MaxStamina);

/// <summary>One hero crossing one or more levels. Carries where they came from
/// as well as where they landed, because "Level 4 reached" means more beside the
/// 3 it replaced.</summary>
/// <param name="Companions">Others who levelled on the same award. Named in the
/// window rather than given their own, so the party's progress is visible without
/// three popups in a row.</param>
public sealed record LevelUp(
    Models.Character Hero, int FromLevel, int ToLevel, int AttributePoints, int SkillPoints,
    IReadOnlyList<string> Companions);
