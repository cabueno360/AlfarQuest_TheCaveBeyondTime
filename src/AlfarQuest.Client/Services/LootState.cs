using AlfarQuest.Client.Game;
using AlfarQuest.Client.Models;
using AlfarQuest.Client.Services.Character;

namespace AlfarQuest.Client.Services;

/// <summary>The container the player currently has open, and taking from it.
///
/// It holds the engine's own <see cref="LootStack"/> by reference rather than a
/// copy, which is what makes "take one thing and walk away" work: the world's
/// chest and the window are the same object, so whatever is left is still in the
/// chest and is still there when the game is saved.
///
/// This is also the one place that knows the party's pack can be full. A take
/// that cannot be delivered must not empty the container — losing an item to a
/// full bag is the kind of thing players do not forgive.</summary>
public sealed class LootState(PartyState party)
{
    public OpenedContainer? Open { get; private set; }

    public event Action? Changed;

    /// <summary>Registers with the engine. Called once at start-up, so the
    /// simulation has somewhere to send an opened container and no component has
    /// to remember to wire it.</summary>
    public void Attach()
    {
        ContainerBridge.OnOpened = container =>
        {
            Open = container;
            Changed?.Invoke();
            return true;
        };

        // The pouch is where a key would be, so the pouch is what answers.
        ContainerBridge.HasKey = key => party.Pouch[key] > 0;
        ContainerBridge.ConsumeKey = key => party.Pouch.Add(key, -1);
    }

    public void Close()
    {
        Open = null;
        Changed?.Invoke();
    }

    public void TakeCoin()
    {
        if (Open is not { } c || c.Contents.Coin <= 0) return;
        party.Purse.Add("gold", c.Contents.Coin);
        c.Contents.TakeCoin();
        Announce();
    }

    public void TakeMaterial(int index)
    {
        if (Open is not { } c || index < 0 || index >= c.Contents.Materials.Count) return;
        var (id, n) = c.Contents.Materials[index];
        party.Pouch.Add(id, n);
        c.Contents.TakeMaterial(index);
        Announce();
    }

    public void TakeItem(int index)
    {
        if (Open is not { } c || index < 0 || index >= c.Contents.Items.Count) return;

        var item = ItemCatalog.Find(c.Contents.Items[index]);
        // An id the game no longer defines is dropped rather than left jamming
        // the container forever.
        if (item is null) { c.Contents.TakeItem(index); Announce(); return; }

        if (!party.Bag.Add(item)) { Full = true; Changed?.Invoke(); return; }
        c.Contents.TakeItem(index);
        Announce();
    }

    /// <summary>Everything that will fit. Coin and materials always fit — they are
    /// counted, not carried one by one — so only the gear can be refused, and
    /// what is refused stays in the container.</summary>
    public void TakeAll()
    {
        if (Open is not { } c) return;

        TakeCoin();
        while (c.Contents.Materials.Count > 0) TakeMaterial(0);

        var blocked = 0;
        while (c.Contents.Items.Count > blocked)
        {
            var before = c.Contents.Items.Count;
            TakeItem(blocked);
            // Nothing moved: the pack is full and this one stays. Step past it so
            // the loop still ends rather than retrying the same item forever.
            if (c.Contents.Items.Count == before) blocked++;
        }

        if (c.Contents.IsEmpty) Close();
    }

    /// <summary>Set when a take was refused for want of room. Cleared as soon as
    /// anything succeeds, so the warning is about now and not about earlier.</summary>
    public bool Full { get; private set; }

    private void Announce()
    {
        Full = false;
        Record();
        party.Notify();
        Changed?.Invoke();
    }

    /// <summary>Writes down what is left in the open container.
    ///
    /// After every take, not on closing: a player who takes a sword and then
    /// closes the tab has taken the sword, and the chest should know it. Waiting
    /// for a tidy close is how a crash becomes a duplicated item.</summary>
    private void Record()
    {
        if (Open is not { } c) return;

        // The opening time is kept, not refreshed. Stamping "now" on every take
        // would push a respawning container's clock forward each time the player
        // reached into it, so a barrel emptied slowly would never come back.
        var openedAt = party.Containers.TryGetValue(c.Key, out var prior)
            ? prior.OpenedAt
            : DateTime.UtcNow;

        RewardBridge.SaveContainer(ContainerSave.From(c.Key, openedAt, c.Contents));
    }
}
