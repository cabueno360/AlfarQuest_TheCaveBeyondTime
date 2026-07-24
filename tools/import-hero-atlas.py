#!/usr/bin/env python3
"""Cut a generated character sheet into the nine-frame strip the engine animates.

    python3 tools/import-hero-atlas.py <hero> <source.png> [--preview]

A generated sheet (Gemini and the like) is NOT a clean grid like the Mage's was:
it is a light background with figures of different sizes strewn across it, the
weapon on the attack frames sticking far out to one side. So this cannot index a
fixed grid — it SEGMENTS. It keys the background out by flooding in from the
edges (which leaves an interior white tabard alone), labels every figure as a
connected island, and then the caller names which islands are the idle, the walk,
the swing and the channel.

Each chosen figure is placed into one cell aligned by its FEET and by the centre
of its LEGS — never its bounding box, because the sword throws the bounding box
sideways and would shove the body off centre. The legs are always the body, so
the body stays put and only the sword reaches out of frame, which is exactly what
an attack frame should do.

The result is atlas_<hero>.png, one row of nine 32-tall cells in the engine's
column order (idle+walk 0-5, attack 6-7, channel 8) — the same contract
atlas_party and atlas_mage answer to — plus the measured ground line to set on
the atlas.
"""
from PIL import Image
from collections import deque
import sys, os

HERO = sys.argv[1] if len(sys.argv) > 1 else "cleric"
SRC = sys.argv[2] if len(sys.argv) > 2 else ""
PREVIEW = "--preview" in sys.argv
ASSETS = "src/AlfarQuest.Client/wwwroot/assets"

# Which segmented figures become which frames, per hero. Indices are the
# segmentation order (row-major, top-left first) — run with --preview once to
# see them numbered, then fill these in.
FRAMES = {
    # 0-5 idle+walk, 6-7 attack, 8 channel
    "cleric": [0, 37, 39, 41, 43, 45, 50, 61, 66],
    "thief":  [0, 41, 43, 45, 47, 49, 51, 55, 61],
}

CELL_H = 56          # the strip's cell height in pixels; the body is scaled to fit


def key_and_segment(im):
    """Transparent-background copy + the figures as (x0,y0,x1,y1) boxes, in
    reading order. Background is whatever floods in from the border."""
    W, H = im.size
    px = im.load()

    def isbg(p):
        # Background is any LOW-SATURATION MID-GREY: it catches both the flat
        # backdrop and the baked drop-shadow the generator paints under the feet,
        # which is a darker grey than the backdrop and would otherwise survive as
        # a dirty smudge doubling the engine's own ground shadow. Saturated colour
        # (cloak, boots, hair, skin), very dark pixels (black leggings) and
        # near-white (a Cleric's tabard) all fail the test and are kept. And it is
        # only ever applied by a flood inward from the border, so an interior grey
        # — a dagger's steel — is never reached and never keyed.
        r, g, bl = p[0], p[1], p[2]          # p is RGBA — never let alpha into sat
        sat = max(r, g, bl) - min(r, g, bl)
        b = (r + g + bl) // 3
        return sat < 22 and 100 < b < 246

    bg = bytearray(W * H)
    q = deque()
    for x in range(W):
        for y in (0, H - 1):
            if isbg(px[x, y]) and not bg[y * W + x]:
                bg[y * W + x] = 1; q.append((x, y))
    for y in range(H):
        for x in (0, W - 1):
            if isbg(px[x, y]) and not bg[y * W + x]:
                bg[y * W + x] = 1; q.append((x, y))
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < W and 0 <= ny < H and not bg[ny * W + nx] and isbg(px[nx, ny]):
                bg[ny * W + nx] = 1; q.append((nx, ny))

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    op = out.load()
    for y in range(H):
        for x in range(W):
            if not bg[y * W + x]:
                op[x, y] = px[x, y]

    seen = bytearray(W * H); comps = []
    for y in range(H):
        for x in range(W):
            if bg[y * W + x] or seen[y * W + x]: continue
            Q = deque([(x, y)]); seen[y * W + x] = 1
            x0 = x1 = x; y0 = y1 = y; n = 0
            while Q:
                cx, cy = Q.popleft(); n += 1
                if cx < x0: x0 = cx
                if cx > x1: x1 = cx
                if cy < y0: y0 = cy
                if cy > y1: y1 = cy
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1),
                               (1, 1), (1, -1), (-1, 1), (-1, -1)):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < W and 0 <= ny < H and not bg[ny * W + nx] and not seen[ny * W + nx]:
                        seen[ny * W + nx] = 1; Q.append((nx, ny))
            if n > 1500: comps.append((x0, y0, x1, y1))
    comps.sort(key=lambda c: ((c[1] + c[3]) // 2 // 200, c[0]))
    return out, comps


def leg_centre(keyed, box):
    """The x of the body's legs — the mean x of opaque pixels in the bottom
    two-fifths of the figure. Stable across idle, walk and swing, unlike the
    bounding box, which the sword drags sideways."""
    x0, y0, x1, y1 = box
    px = keyed.load()
    lo = y1 - int((y1 - y0) * 0.4)
    xs = [x for y in range(lo, y1 + 1) for x in range(x0, x1 + 1)
          if px[x, y][3] > 16]
    return sum(xs) / len(xs) if xs else (x0 + x1) / 2


def build(hero, src):
    im = Image.open(src).convert("RGBA")
    keyed, comps = key_and_segment(im)
    print(f"{os.path.basename(src)}: {im.size}, {len(comps)} figures")

    if PREVIEW:
        from PIL import ImageDraw
        ov = im.copy(); d = ImageDraw.Draw(ov)
        for i, (x0, y0, x1, y1) in enumerate(comps):
            d.rectangle([x0, y0, x1, y1], outline=(255, 0, 0), width=3)
            d.text((x0 + 2, y0 + 2), str(i), fill=(255, 255, 0))
        p = "/tmp/hero_seg.png"; ov.resize((1024, 1024)).save(p)
        print(f"  wrote {p} — numbered figures; put the 9 you want in FRAMES['{hero}']")
        return

    idx = FRAMES[hero]
    figs = []
    for i in idx:
        x0, y0, x1, y1 = comps[i]
        cx = leg_centre(keyed, comps[i])
        figs.append((keyed.crop((x0, y0, x1 + 1, y1 + 1)),
                     cx - x0, y1 - y0))          # sprite, legs-x within it, foot-y

    # The cell must hold the widest reach either side of the leg centre and the
    # tallest figure. Everything is scaled so the tallest BODY fits CELL_H.
    body_h = max(h for _, _, h in figs)
    scale = (CELL_H - 2) / body_h
    left = max(lx for _, lx, _ in figs)
    right = max(s.width - lx for s, lx, _ in figs)
    cw = int((left + right) * scale) + 2
    ch = CELL_H
    cx0 = int(left * scale) + 1

    strip = Image.new("RGBA", (cw * 9, ch), (0, 0, 0, 0))
    for col, (s, lx, fy) in enumerate(figs):
        sw, sh = int(s.width * scale), int(s.height * scale)
        r = s.resize((max(1, sw), max(1, sh)), Image.LANCZOS)
        ox = col * cw + cx0 - int(lx * scale)
        oy = ch - 1 - int(fy * scale)            # feet on the cell's floor line
        strip.alpha_composite(r, (ox, max(0, oy)))

    os.makedirs(ASSETS, exist_ok=True)
    out = os.path.join(ASSETS, f"atlas_{hero}.png")
    strip.save(out)
    px = strip.load()
    foot = max((y for y in range(ch) for x in range(strip.width) if px[x, y][3] > 8),
               default=ch - 1)
    print(f"wrote {out}  {strip.width}x{ch} — 9 cells of {cw}x{ch}")
    print(f"  cell {cw}x{ch}, ground line y={foot}")
    print(f"  ATLAS.{hero}: cw {cw}, ch {ch}, ground {foot}, scale ~{54/CELL_H:.2f}")


build(HERO, SRC)
