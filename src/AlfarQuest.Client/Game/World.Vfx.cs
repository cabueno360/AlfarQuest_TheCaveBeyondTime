using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  Effects.
//
//  One entry point — Play(id, at) — and a catalogue of what each id
//  means. Nothing here knows what a critical hit or a chest is; it
//  knows how to turn a VfxSpec into motes, shake and a held frame.
//
//  That separation is the point. An action that wants feedback names
//  an effect; an effect that wants tuning is edited in one row of the
//  catalogue and changes everywhere it is used.
// =====================================================================
public partial class World
{
    /// <summary>How many motes may exist at once.
    ///
    /// A hard ceiling rather than a quality setting: the outdoor map can have a
    /// dozen fights and an ambient emitter running together, and an unbounded
    /// list would degrade the frame rate exactly when the screen is busiest — the
    /// moment the effects are meant to be read.</summary>
    const int MaxParticles = 900;

    /// <summary>Seconds the simulation is held still after an impact.
    ///
    /// Hit-stop: the frame freezes for a fraction of a second so a blow lands
    /// rather than passing through. Rendering continues, so it reads as weight
    /// and not as a stutter.</summary>
    public float HitStop { get; private set; }

    /// <summary>Every mote ever emitted, never reset.
    ///
    /// The live count cannot answer "did that action produce an effect?" — a
    /// footfall's four motes vanish inside the twenty the ambient emitter is
    /// already drifting. A total that only goes up can.</summary>
    public int FxEmitted { get; private set; }

    /// <summary>Plays an effect at a point.</summary>
    /// <param name="towards">Direction for the cone shapes — the way the blow was
    /// travelling. Ignored by the others.</param>
    /// <param name="scale">Multiplies count and speed. For the same effect at a
    /// different weight, rather than a second near-identical catalogue row.</param>
    public void Play(string id, Vec at, Vec? towards = null, float scale = 1f)
    {
        if (VfxSpec.Find(id) is not { } spec) return;

        Emit(spec, at, towards, scale);

        if (spec.Shake > 0) Shake = MathF.Max(Shake, spec.Shake * scale);
        // Taken, not added: two hits in one frame should not stack into a
        // noticeable freeze.
        if (spec.HitStop > 0) HitStop = MathF.Max(HitStop, spec.HitStop);
    }

    void Emit(VfxSpec spec, Vec at, Vec? towards, float scale)
    {
        var count = Math.Max(1, (int)MathF.Round(spec.Count * scale));

        // Trimmed rather than dropped when the budget is tight: an effect that
        // vanished entirely under load would leave the player with no feedback
        // at the busiest moment. Fewer motes still reads.
        var room = MaxParticles - Fx.Count;
        if (room <= 0) return;
        if (count > room) count = room;

        var facing = towards is { } t && t.Len() > 0.01f
            ? MathF.Atan2(t.Y, t.X)
            : (float)(_rng.NextDouble() * Math.Tau);

        for (var i = 0; i < count; i++)
        {
            var angle = spec.Shape switch
            {
                // Even spacing, not random: a ring made of random angles has gaps
                // and clumps, and reads as a burst that failed.
                Emission.Ring => i / (float)count * MathF.Tau,
                Emission.Cone => facing + ((float)_rng.NextDouble() - 0.5f) * spec.Spread,
                Emission.Fountain => -MathF.PI / 2 + ((float)_rng.NextDouble() - 0.5f) * spec.Spread,
                Emission.Fall => MathF.PI / 2 + ((float)_rng.NextDouble() - 0.5f) * spec.Spread,
                _ => (float)(_rng.NextDouble() * Math.Tau),
            };

            var speed = spec.Speed * scale * Vary(spec.Vary);
            var life = spec.Life * Vary(spec.Vary * 0.6f);
            var size = spec.Size * Vary(spec.Vary * 0.5f);
            var colour = spec.Colours[_rng.Next(spec.Colours.Length)];

            Fx.Add(new Particle(
                at,
                new Vec(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
                colour, life, size, spec.Gravity, spec.Drag, spec.Additive));
            FxEmitted++;
        }
    }

    /// <summary>A multiplier around 1, spread by <paramref name="amount"/>.
    /// Floored so a large spread cannot produce a mote with no speed or no life
    /// at all.</summary>
    float Vary(float amount) =>
        MathF.Max(0.15f, 1f + ((float)_rng.NextDouble() - 0.5f) * 2f * amount);

    /// <summary>Steps every mote. Each carries its own gravity and drag, so this
    /// is the same loop for a rock fragment and a rising soul.</summary>
    void UpdateParticles(float dt)
    {
        foreach (var p in Fx)
        {
            p.Vel = new Vec(p.Vel.X, p.Vel.Y + p.Gravity * dt);
            p.Vel *= MathF.Pow(p.Drag, dt * 60f);      // frame-rate independent
            p.Pos += p.Vel * dt;
            p.Life -= dt;
        }
        Fx.RemoveAll(p => p.Life <= 0);
    }

    /// <summary>Drifting motes, so a still screen is not a dead one. Emitted
    /// around the camera rather than across the map: only what can be seen is
    /// worth spending motes on.</summary>
    void UpdateAmbient(float dt)
    {
        _ambient -= dt;
        if (_ambient > 0) return;
        _ambient = 0.14f;

        // The cave's motes drift in torchlight; the wood's catch the sun.
        var id = Stage == 1 ? "motes_forest" : "motes_cave";
        var x = Camera.X + ((float)_rng.NextDouble() - 0.5f) * ViewW;
        var y = Camera.Y - ViewH * 0.55f + (float)_rng.NextDouble() * ViewH * 0.2f;
        Play(id, new Vec(x, y));
    }

    float _ambient;
}
