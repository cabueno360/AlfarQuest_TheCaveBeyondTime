using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Status effects.
//
//  The runtime half of the architecture the catalogue describes: apply
//  one to a creature or a hero, tick it down, and let its behaviour —
//  damage over time, a hold in place — do its work. Two are wired now
//  (stun from the hammer, venom from the spider); the rest are a row of
//  wiring away, which is the point of building it this way.
// =====================================================================
public partial class World
{
    /// <summary>Puts an effect on a creature, or refreshes it if it already has
    /// one of the same kind — a second stun should extend the first, not stack
    /// into a lock. Announces it with a floating word, so the player sees why the
    /// thing stopped moving.</summary>
    void ApplyEffect(Husk k, StatusEffectKind kind, float seconds, float magnitude, string? source)
    {
        Refresh(k.Effects, kind, seconds, magnitude, source);
        var info = StatusEffectInfo.Of(kind);
        Floaters.Add(new FloatText(k.Pos + new Vec(0, -k.R - 8), info.Name.ToUpperInvariant(), info.Colour));
    }

    void ApplyEffect(Hero h, StatusEffectKind kind, float seconds, float magnitude, string? source)
    {
        Refresh(h.Effects, kind, seconds, magnitude, source);
        var info = StatusEffectInfo.Of(kind);
        Floaters.Add(new FloatText(h.Pos + new Vec(0, -26), info.Name.ToUpperInvariant(), info.Colour));
    }

    static void Refresh(List<ActiveEffect> effects, StatusEffectKind kind, float seconds, float magnitude, string? source)
    {
        foreach (var e in effects)
            if (e.Kind == kind) { e.Remaining = MathF.Max(e.Remaining, seconds); return; }
        effects.Add(new ActiveEffect(kind, seconds, magnitude, source));
    }

    /// <summary>Steps a creature's effects. Damage-over-time gnaws its health and
    /// is credited to whoever applied it, so a kill by lingering venom is still
    /// individual. Returns nothing — expiry and the immobilise check are read off
    /// the list.</summary>
    void TickHuskEffects(Husk k, float dt)
    {
        if (k.Effects.Count == 0) return;
        foreach (var e in k.Effects)
        {
            e.Remaining -= dt;
            if (e.Behaviour != StatusBehaviour.DamageOverTime) continue;

            e.TickPool += e.Magnitude * dt;
            if (e.TickPool < 1f) continue;
            var chunk = (int)MathF.Floor(e.TickPool);
            e.TickPool -= chunk;
            k.Hp -= chunk;
            if (e.Source is { } src)
            {
                k.LastHitBy = src;
                StatBridge.Record(src, HeroStats.Kind.DamageDealt, chunk);
            }
            var info = StatusEffectInfo.Of(e.Kind);
            Floaters.Add(new FloatText(k.Pos + new Vec(0, -k.R), $"-{chunk}", info.Colour));
        }
        k.Effects.RemoveAll(e => e.Remaining <= 0);
    }

    /// <summary>Steps a hero's effects — venom that gnaws while it lasts.</summary>
    void TickHeroEffects(Hero h, float dt)
    {
        if (h.Effects.Count == 0) return;
        foreach (var e in h.Effects)
        {
            e.Remaining -= dt;
            if (e.Behaviour != StatusBehaviour.DamageOverTime) continue;

            e.TickPool += e.Magnitude * dt;
            if (e.TickPool < 1f) continue;
            var chunk = (int)MathF.Floor(e.TickPool);
            e.TickPool -= chunk;
            h.Hp -= chunk;
            StatBridge.Record(h.Def.Key, HeroStats.Kind.DamageTaken, chunk);
            var info = StatusEffectInfo.Of(e.Kind);
            Floaters.Add(new FloatText(h.Pos + new Vec(0, -24), $"-{chunk}", info.Colour));
        }
        h.Effects.RemoveAll(e => e.Remaining <= 0);
    }
}
