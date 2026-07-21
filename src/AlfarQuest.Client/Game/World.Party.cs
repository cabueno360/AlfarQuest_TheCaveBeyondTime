using AlfarQuest.Client.Models;

namespace AlfarQuest.Client.Game;

// =====================================================================
//  The party: the hero the player steers, the two that follow, and
//  the separation pass that keeps them from stacking.
// =====================================================================
public partial class World
{
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
            Burst(h.Pos, "#ffffff", 8);
        }
        h.DashVel *= 0.82f;
        h.Pos = MoveBlocked(h.Pos, move * speed * dt + h.DashVel * dt, 14f);
        if (move.Len() > 0.1f) h.Facing = (float)Math.Atan2(move.Y, move.X);

        var aimDir = (worldAim - h.Pos).Norm();
        if (aimDir.Len() > 0.01f) h.Facing = (float)Math.Atan2(aimDir.Y, aimDir.X);

        if (input.attack && h.Cool <= 0) DoAttack(h, aimDir);
        if (input.ability && h.AbilityCool <= 0) TryAbility(h);
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

    void UpdateCompanion(Hero h, float dt, int slot)
    {
        var lead = Party[Active];
        var target = lead.Pos + CompanionSlots[Math.Min(slot, CompanionSlots.Length - 1)];
        var toSlot = target - h.Pos;
        float dist = toSlot.Len();
        // Dead zone stops them jittering on the spot; easing in over the last
        // stretch keeps them from snapping into formation.
        if (dist > 14f)
        {
            float speed = h.Speed * 0.9f * Math.Min(1f, dist / 46f);
            h.Pos = MoveBlocked(h.Pos, toSlot.Norm() * speed * dt, 14f);
        }

        // auto-fight the nearest husk in range
        var k = NearestHusk(h.Pos);
        if (k is not null)
        {
            var dir = (k.Pos - h.Pos).Norm();
            float d = (k.Pos - h.Pos).Len();
            if (d < h.Def.Range && h.Cool <= 0) { h.Facing = (float)Math.Atan2(dir.Y, dir.X); DoAttack(h, dir); }
        }
    }

    // ---- helpers ----
    Hero? NearestHero(Vec p) => Party.Where(h => h.Alive).OrderBy(h => (h.Pos - p).Len()).FirstOrDefault();

    Husk? NearestHusk(Vec p) => Husks.OrderBy(k => (k.Pos - p).Len()).FirstOrDefault();
}
