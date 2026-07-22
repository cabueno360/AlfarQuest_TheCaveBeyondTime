using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  The party: the hero the player steers, the two that follow, and
//  the separation pass that keeps them from stacking.
// =====================================================================
public partial class World
{
    /// <summary>Distance walked since the last puff of dust.</summary>
    float _stride;

    void UpdateActiveHero(Hero h, float dt, InputState input, Vec worldAim)
    {
        // Mid-sentence the hero stands still, so the balloon stays over the
        // villager it belongs to.
        if (IsTalking) { h.Facing = MathF.Atan2(worldAim.Y - h.Pos.Y, worldAim.X - h.Pos.X); return; }

        var move = new Vec(
            (input.right ? 1 : 0) - (input.left ? 1 : 0),
            (input.down ? 1 : 0) - (input.up ? 1 : 0)).Norm();

        float speed = h.Speed;
        // Stamina is checked as part of the same condition, so a dash that cannot
        // be paid for never starts its cooldown either — being out of breath must
        // not also cost you the next dodge.
        if (input.dash && h.DashCool <= 0 && move.Len() > 0.1f && h.SpendStamina(ResourceCosts.Dash))
        {
            h.DashVel = move * h.Def.Speed * 3.4f;
            h.DashCool = h.DashCooldown;
            h.IFrames = 0.28f;               // dodge i-frames
            // No water branch: water is impassable, so a hero never wades. The
            // catalogue keeps a water_step effect for when a shallow tile exists,
            // but nothing calls it and pretending otherwise would be a dead one.
            Play("dash_dust", h.Pos, move * -1f);
        }
        h.DashVel *= 0.82f;
        h.Pos = MoveBlocked(h.Pos, move * speed * dt + h.DashVel * dt, 14f);

        // Dust off the heels, paced by distance rather than by time so it does
        // not thicken when the hero is standing still turning on the spot.
        if (move.Len() > 0.1f)
        {
            _stride += speed * dt;
            if (_stride > 26f)
            {
                _stride = 0;
                Play("footfall", h.Pos + new Vec(0, 6), move * -1f, 0.6f);
                // The footstep's sound is the one thing about it the catalogue
                // cannot choose: it depends on the ground. Stone underfoot in the
                // cave, dirt on the approach. Kept low — a step is not an event.
                PlaySound(Stage == 2 ? "step_stone" : "step_dirt", h.Pos, 0.4f);
            }
        }
        if (move.Len() > 0.1f) h.Facing = (float)Math.Atan2(move.Y, move.X);

        var aimDir = (worldAim - h.Pos).Norm();
        if (aimDir.Len() > 0.01f) h.Facing = (float)Math.Atan2(aimDir.Y, aimDir.X);

        // Skills aim where the mouse is; held here so a cast reads the same aim
        // the swing does.
        _castAim = worldAim;

        if (input.attack && h.Cool <= 0) DoAttack(h, aimDir);
        // 1–4 cast the hotbar skills; right mouse is a quick second cast of slot 1.
        if (input.castSlot is >= 1 and <= 4) CastSkill(h, input.castSlot);
        else if (input.ability) CastSkill(h, 1);
        if (input.potion) UsePotion(h);
    }

    // Where each companion tries to stand, relative to the leader. Without
    // distinct slots every companion chased the leader's exact position, so they
    // converged on the same spot and ended up drawn on top of each other.
    static readonly Vec[] CompanionSlots = { new(-58f, 24f), new(58f, 24f), new(0f, 62f) };

    // Formation slots aim companions apart, but crystals, walls and the chamber
    // clamp can still squeeze two heroes together. This is the backstop that
    // guarantees they never render stacked.
    void SeparateParty()
    {
        const float minGap = 34f;
        for (int a = 0; a < Party.Count; a++)
        {
            for (int b = a + 1; b < Party.Count; b++)
            {
                Hero A = Party[a], B = Party[b];
                if (!A.Alive || !B.Alive) continue;
                var d = B.Pos - A.Pos;
                float len = d.Len();
                if (len >= minGap) continue;

                // Exactly coincident: Norm() would return zero and leave them
                // welded together, so pick a deterministic direction instead.
                var dir = len < 1e-4f ? new Vec(a % 2 == 0 ? 1f : -1f, 0f) : d.Norm();
                float push = (minGap - len) * 0.5f;

                // Never shove the hero the player is steering — move the other one.
                if (a == Active) B.Pos = MoveBlocked(B.Pos, dir * (push * 2f), 14f);
                else if (b == Active) A.Pos = MoveBlocked(A.Pos, dir * -(push * 2f), 14f);
                else { A.Pos = MoveBlocked(A.Pos, dir * -push, 14f); B.Pos = MoveBlocked(B.Pos, dir * push, 14f); }

                A.Pos = Clamp(ResolveCrystalCollision(A.Pos), 40);
                B.Pos = Clamp(ResolveCrystalCollision(B.Pos), 40);
            }
        }
    }

    /// <summary>How near a threat must be to the leader before a companion will
    /// bother with it. Companions assist; they do not go adventuring off across
    /// the map after something that wandered past.</summary>
    const float AssistTiles = 5f;

    /// <summary>A companion's turn. The point of all the timers and jitter below
    /// is that they are not a second player — they hang back, react a beat late,
    /// miss sometimes, and drift out of formation rather than holding a rigid
    /// triangle. A perfect auto-fighter made the leader feel like a passenger.</summary>
    void UpdateCompanion(Hero h, float dt, int slot)
    {
        var lead = Party[Active];
        h.ReactCool = MathF.Max(0, h.ReactCool - dt);

        // Re-roll the loose formation offset every few seconds — sometimes left,
        // sometimes right, sometimes a little behind — so spacing looks alive.
        h.SlotDriftCool -= dt;
        if (h.SlotDriftCool <= 0)
        {
            h.SlotDriftCool = 2.5f + (float)_rng.NextDouble() * 3f;
            float a = (float)_rng.NextDouble() * MathF.Tau;
            float r = (float)_rng.NextDouble() * 26f;
            h.SlotDrift = new Vec(MathF.Cos(a) * r, MathF.Sin(a) * r);
        }
        var slotPos = lead.Pos + CompanionSlots[Math.Min(slot, CompanionSlots.Length - 1)] + h.SlotDrift;

        // Only a threat near the party counts. One that has wandered off is left
        // alone, and the companion just keeps up.
        var k = NearestHusk(h.Pos);
        bool near = k is not null && (k.Pos - lead.Pos).Len() < AssistTiles * TILE;
        if (!near)
        {
            // Nothing worth fighting: hold the drifting slot, and keep a fresh
            // reaction primed so the next threat still costs a beat.
            h.ReactCool = MathF.Max(h.ReactCool, 0.4f + (float)_rng.NextDouble() * 1.6f);
            MoveTowardSlot(h, slotPos, dt);
            return;
        }

        var threat = k!;
        float d = (threat.Pos - h.Pos).Len();

        // Out of reach: close some of the gap — but not at a sprint, and not
        // past the assist leash, which the `near` check already bounds.
        if (d > h.Range * 0.9f)
        {
            var approach = (threat.Pos - h.Pos).Norm();
            h.Pos = MoveBlocked(h.Pos, approach * h.Speed * 0.7f * dt, 14f);
            return;
        }

        // In reach, but a human does not swing the instant a target is in range.
        // While the reaction is running, reposition instead of attacking.
        if (h.ReactCool > 0 || h.Cool > 0) { MoveTowardSlot(h, slotPos, dt); return; }

        // Sometimes step aside rather than commit — the pause that makes the
        // rhythm feel unscripted.
        if (_rng.NextDouble() < 0.15) { MoveTowardSlot(h, slotPos, dt); h.ReactCool = 0.3f; return; }

        // Attack, imperfectly. The aim is nudged off-true, so the swing sometimes
        // lands wide or the bolt sails past a target that moved — a companion that
        // never missed read as a machine. Then a fresh reaction beat before the
        // next one.
        var aim = Jitter((threat.Pos - h.Pos).Norm(), 0.2f);
        h.Facing = MathF.Atan2(aim.Y, aim.X);
        DoAttack(h, aim);
        h.ReactCool = 0.4f + (float)_rng.NextDouble() * 1.6f;
    }

    /// <summary>Eases a hero toward a point with a dead zone, so companions settle
    /// instead of jittering on the spot or snapping into formation.</summary>
    void MoveTowardSlot(Hero h, Vec slotPos, float dt)
    {
        var toSlot = slotPos - h.Pos;
        float dist = toSlot.Len();
        if (dist <= 14f) return;
        float speed = h.Speed * 0.9f * Math.Min(1f, dist / 46f);
        h.Pos = MoveBlocked(h.Pos, toSlot.Norm() * speed * dt, 14f);
    }

    // ---- helpers ----
    Hero? NearestHero(Vec p) => Party.Where(h => h.Alive).OrderBy(h => (h.Pos - p).Len()).FirstOrDefault();

    Husk? NearestHusk(Vec p) => Husks.OrderBy(k => (k.Pos - p).Len()).FirstOrDefault();
}
