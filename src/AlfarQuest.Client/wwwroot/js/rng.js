// Deterministic PRNG. The world is generated from a seeded Random on the C#
// side, so anything the client scatters must be reproducible too — scenery that
// reshuffled on every reload would make the same chamber feel different.
export function mulberry32(a) {
    return function () {
        a |= 0; a = (a + 0x6D2B79F5) | 0;
        let t = Math.imul(a ^ (a >>> 15), 1 | a);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

// A route that wanders instead of running straight. The lateral offset peaks
// mid-way and returns to zero at both ends, so the path still reaches the points
// it is meant to connect.
export function route(ax, ay, bx, by, rnd, wobble = 90) {
    const pts = [{ x: ax, y: ay }];
    const steps = Math.max(3, Math.round(Math.hypot(bx - ax, by - ay) / 150));
    for (let i = 1; i < steps; i++) {
        const t = i / steps;
        const nx = -(by - ay), ny = bx - ax;
        const len = Math.hypot(nx, ny) || 1;
        const bow = Math.sin(t * Math.PI) * (rnd() - 0.5) * 2 * wobble;
        pts.push({ x: ax + (bx - ax) * t + (nx / len) * bow,
                   y: ay + (by - ay) * t + (ny / len) * bow });
    }
    pts.push({ x: bx, y: by });
    return pts;
}

export function distToRoutes(px, py, routes) {
    let best = Infinity;
    for (const pts of routes)
        for (let i = 1; i < pts.length; i++) {
            const a = pts[i - 1], b = pts[i];
            const dx = b.x - a.x, dy = b.y - a.y;
            const l2 = dx * dx + dy * dy;
            const t = Math.max(0, Math.min(1, l2 ? ((px - a.x) * dx + (py - a.y) * dy) / l2 : 0));
            const d = Math.hypot(px - (a.x + dx * t), py - (a.y + dy * t));
            if (d < best) best = d;
        }
    return best;
}
