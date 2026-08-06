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

    /// <summary>Gathered crafting materials — kept shared on purpose. Crafting is a
    /// party workbench, and splitting the ore between three packs would mean
    /// shuffling it back together before anything could be made. Inventory and
    /// gold are individual (on each <see cref="Models.Character"/>); materials are
    /// not.</summary>
    public Satchel Pouch { get; } = new();

    /// <summary>A store neither hero owns, that any of them can reach — the
    /// architecture the brief asks for. Off by default: nothing routes here and no
    /// window shows it yet, but the container exists so turning it on is a feature
    /// flag, not a rewrite.</summary>
    public Inventory Stash { get; } = new(48);

    /// <summary>When true, every hero draws from one purse instead of their own —
    /// the optional shared-gold mode. Off by default: gold is individual. Flipping
    /// it changes where <see cref="PurseFor"/> points, and nothing else.</summary>
    public bool SharedGold { get; set; }

    /// <summary>The one purse used when <see cref="SharedGold"/> is on.</summary>
    public Wallet SharedPurse { get; } = new();

    /// <summary>Which purse a hero spends from and earns into — their own, or the
    /// shared pool when that mode is on. Every gold path goes through here so the
    /// two modes never disagree.</summary>
    public Wallet PurseFor(Models.Character c) => SharedGold ? SharedPurse : c.Purse;

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

    /// <summary>Fires with the quest the instant a newly-set flag completes it, once
    /// per quest — the quest-complete toast listens here for its title and payout.</summary>
    public event Action<QuestDef>? QuestCompleted;

    /// <summary>The one door every flag goes through, whether the engine set it
    /// (a descent, a discovery) or a conversation did. Adds it to the claimed set,
    /// and if it just carried any quest over the line, PAYS that quest's reward and
    /// announces it — so a completion is granted and toasted exactly once, never
    /// missed and never doubled.</summary>
    private bool AddFlag(string key)
    {
        if (!ClaimedRewards.Add(key)) return false;
        foreach (var q in QuestCatalog.CompletedBy(key, ClaimedRewards))
        {
            GrantReward(q.Reward);
            QuestCompleted?.Invoke(q);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Pays a finished quest's reward to the party's leader — XP to them,
    /// coin to their purse, the rare story items into their pack. The leader, not
    /// "everyone", because the pack and the purse are individual now, like every
    /// other drop. XP may level them up; GainXp raises that on its own.</summary>
    private void GrantReward(QuestReward r)
    {
        if (r.IsEmpty) return;
        var owner = Selected ?? Members.FirstOrDefault();
        if (owner is null) return;

        if (r.Xp > 0) GainXp(r.Xp, owner.Key);
        if (r.Gold > 0)
        {
            PurseFor(owner).Add("gold", r.Gold);
            owner.Stats.Add(HeroStats.Kind.GoldEarned, r.Gold);
        }
        foreach (var id in r.ItemIds)
            if (ItemCatalog.Find(id) is { } item && owner.Bag.Add(item))
                owner.Stats.Add(HeroStats.Kind.ItemsCollected, 1);
    }

    public PartyState()
    {
        // Publish attribute-derived modifiers to the simulation. Registered once,
        // here, so nothing else has to remember to keep the two in step.
        // Drops arrive from the simulation and are sorted here: coin to the purse,
        // everything else to the pouch.
        // A drop names its owner — whoever was being steered when it fell. Coin
        // and gear go to that hero (their purse, their pack); materials go to the
        // shared pouch, because crafting is a party thing. An owner the party does
        // not know falls back to the selected hero, so a headless run still lands
        // its loot somewhere real.
        LootBridge.OnDrop = (item, count, ownerKey) =>
        {
            var owner = Find(ownerKey) ?? Selected ?? Members.FirstOrDefault();
            if (item == "Coins")
            {
                if (owner is not null) { PurseFor(owner).Add("gold", count); owner.Stats.Add(HeroStats.Kind.GoldEarned, count); }
            }
            else if (Material.Find(item) is not null)
            {
                Pouch.Add(item, count);      // materials and keys are shared
            }
            else if (ItemCatalog.Find(item) is { } gear && owner is not null)
            {
                for (var i = 0; i < count; i++) if (owner.Bag.Add(gear)) owner.Stats.Add(HeroStats.Kind.ItemsCollected, 1);
            }
            Changed?.Invoke();
        };

        // Deeds counted against the hero who did them — the engine names the hero
        // and the statistic, this files it. Individual, never shared.
        StatBridge.OnStat = (heroKey, kind, amount) =>
        {
            if (Find(heroKey) is { } c) { c.Stats.Add(kind, amount); Changed?.Invoke(); }
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
        RewardBridge.OnClaimed = key => AddFlag(key);

        // The Cleric joining at the Cave: the engine adds him to its own party and
        // calls here so he gets a sheet, gear and a purse and lands in the save,
        // exactly like a hero chosen at the start.
        PartyBridge.OnRecruit = Recruit;

        // The engine asking after (and spending) a carried story item — the
        // Panacea at Mirka's bedside. Packs first, then the shared stash.
        PartyBridge.HasItem = id =>
            Members.Any(m => m.Bag.Items.Any(i => i.Id == id)) || Stash.Items.Any(i => i.Id == id);
        PartyBridge.TakeItem = id =>
        {
            foreach (var m in Members)
                if (m.Bag.Items.FirstOrDefault(i => i.Id == id) is { } it && m.Bag.Remove(it))
                { Changed?.Invoke(); return true; }
            if (Stash.Items.FirstOrDefault(i => i.Id == id) is { } st && Stash.Remove(st))
            { Changed?.Invoke(); return true; }
            return false;
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
            var weapon = StatCalculator.Weapon(c);
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
                CritChance: StatCalculator.CritChance(c.Total) + c.Gear.Total().CritChance + weapon.CritBonus,
                CritDamage: StatCalculator.CritDamage(c.Total),
                MaxMana: StatCalculator.MaxMana(c.Total),
                MaxStamina: StatCalculator.MaxStamina(c.Total),
                ManaRegen: StatCalculator.ManaRegen(c.Total),
                StaminaRegen: StatCalculator.StaminaRegen(c.Total),
                // The weapon the engine actually swings, and the defensive rolls
                // that used to be sheet decoration.
                WeaponMin: weapon.Min,
                WeaponMax: weapon.Max,
                WeaponStunChance: weapon.StunChance,
                AttackReach: weapon.Reach,
                WeaponSpeedFactor: weapon.SpeedFactor,
                WeaponDamage: weapon.Damage,
                Accuracy: StatCalculator.Accuracy(c.Total),
                DodgeChance: StatCalculator.Dodge(c.Total),
                SpellPower: c.Total.Intelligence * 1.4f,
                // The fate-dice bonuses. The prime attribute is the class's own
                // (the arm, the mind, the hand, the faith); Luck is everyone's
                // fortune at a chest. (attr−10)/2, clamped so a die stays a die.
                FateMod: Math.Clamp((c.Class switch
                {
                    "Mage" => c.Total.Intelligence,
                    "Thief" => c.Total.Dexterity,
                    "Cleric" => c.Total.Wisdom,
                    _ => c.Total.Strength,
                } - 10) / 2, 0, 5),
                LuckMod: Math.Clamp((c.Total.Luck - 10) / 2, 0, 5),
                IntMod: Math.Clamp((c.Total.Intelligence - 10) / 2, 0, 5),
                StrMod: Math.Clamp((c.Total.Strength - 10) / 2, 0, 5),
                WisMod: Math.Clamp((c.Total.Wisdom - 10) / 2, 0, 5),
                VitMod: Math.Clamp((c.Total.Vitality - 10) / 2, 0, 5),
                DexMod: Math.Clamp((c.Total.Dexterity - 10) / 2, 0, 5));
        };
    }

    public void Load(IEnumerable<string> heroKeys)
    {
        Members = [.. heroKeys.Select(k => _byKey.TryGetValue(k, out var c) ? c : Create(k))];
        Selected ??= Members.FirstOrDefault();
        Changed?.Invoke();
    }

    /// <summary>Forgets everything — sheets, claims, containers, belongings. The
    /// play screen calls this on the way in, BEFORE Load and the campaign read,
    /// so no progress from a previous slot ever bleeds into the one being played:
    /// without it, a hero cached from save A kept their old levels when save B
    /// (or a brand-new game) recruited them.</summary>
    public void StartFresh()
    {
        _byKey.Clear();
        Members = [];
        Selected = null;
        ClaimedRewards.Clear();
        Containers.Clear();
        Pouch.Clear();
        Stash.Clear();
        SharedPurse.Clear();
        PendingLevelUps.Clear();
        Changed?.Invoke();
    }

    /// <summary>Adds a hero to the party mid-campaign — the Cleric joining at the
    /// Cave — with a full sheet, starting gear and a purse, exactly like a hero
    /// chosen at the start. Idempotent: a hero already in the party is left as they
    /// are, so a reloaded save that already lists him neither duplicates nor resets
    /// him. Wired to <see cref="PartyBridge"/> so the simulation can call it.</summary>
    public void Recruit(string key)
    {
        var c = _byKey.TryGetValue(key, out var e) ? e : Create(key);
        if (Members.Contains(c)) return;
        Members = [.. Members, c];
        Changed?.Invoke();
    }

    /// <summary>Raises a persisted story/quest flag. It rides the same
    /// <see cref="ClaimedRewards"/> set one-shot rewards do, so it saves with the
    /// campaign for free and the engine reads it through RewardBridge on the next
    /// build. Conversations call this to advance a quest when a topic is asked.</summary>
    public void ClaimFlag(string key) => AddFlag(key);

    private Models.Character Create(string key)
    {
        var c = new Models.Character(Lore.ByKey(key));
        foreach (var item in StartingGear.For(c.Class)) c.Gear.Equip(item);
        // Each hero sets out with their own purse and their own loose gear — the
        // pack and the coin are individual now, so the starting kit is too. A save
        // that loads afterwards replaces all of it.
        c.Purse.Set("gold", 120);
        foreach (var item in StartingGear.Spares) c.Bag.Add(item);
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

    /// <summary>Moves an item from the hero's own pack onto them. Whatever was in
    /// that slot goes back into that same pack rather than vanishing — a hero
    /// equips from and unequips to their own inventory, never a shared one.</summary>
    public void Equip(Models.Character c, Item item)
    {
        if (!item.IsEquippable) return;      // a potion or a ration is carried, never worn
        if (!c.Bag.Remove(item)) return;
        var displaced = c.Gear.Equip(item);
        if (displaced is not null && !c.Bag.Add(displaced))
        {
            // Pack is full: put it back rather than destroy the player's gear.
            c.Gear.Equip(displaced);
            c.Bag.Add(item);
            return;
        }
        Changed?.Invoke();
    }

    public void Unequip(Models.Character c, Slot slot)
    {
        if (c.Gear[slot] is not { } item) return;
        if (!c.Bag.Add(item)) return;            // no room: leave it worn
        c.Gear.Unequip(slot);
        Changed?.Invoke();
    }

    /// <summary>Spends the shared materials and the crafter's coin, and puts the
    /// result in the crafter's pack. The crafter is the hero whose sheet is open.
    /// Returns false and changes nothing if anything is short — a partial craft
    /// that ate the materials and produced nothing would be the worst outcome.</summary>
    public bool Craft(Recipe r)
    {
        if (Selected is not { } crafter) return false;
        var purse = PurseFor(crafter);
        if (!r.Affordable(Pouch, purse)) return false;
        if (crafter.Bag.IsFull) return false;

        foreach (var (mat, n) in r.Cost) Pouch.Add(mat, -n);
        purse.Add("gold", -r.CoinCost);
        crafter.Bag.Add(r.Output);
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
