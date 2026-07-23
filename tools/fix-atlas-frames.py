#!/usr/bin/env python3
"""Repair the packed atlases' frame rectangles.

    python3 tools/fix-atlas-frames.py [--write]

The frame table says where each sprite is in the sheet. Several of the rectangles
are TOO SMALL for the art inside them, so the renderer draws a sprite with a
piece sliced off along a straight edge: the guard loses the hand holding his
shield, the old man loses his feet. One entry does not point at a character at
all — the blacksmith's left-facing frame is an anvil, so he turns into a
workbench when he walks west.

Neither is a drawing problem and neither can be fixed by drawing. The art is
whole; the numbers are wrong.

The repair is to stop trusting the numbers and measure the sheet: label every
connected island of opaque pixels, then grow each declared frame to the union of
the islands that belong to it. A frame that then disagrees violently with its own
first frame is not the same character, and is dropped — the renderer mirrors
frame 0 when there is no second frame, which is right far more often than an
anvil is.
"""
import json, os, sys
from collections import deque
from PIL import Image

ROOT = "src/AlfarQuest.Client/wwwroot/assets/Outside"
SHEETS = [("atlas_chars.png", "atlas_chars.json"),
          ("atlas_outside.png", "atlas_outside.json")]
WRITE = "--write" in sys.argv


def components(im):
    """Every connected island of opaque pixels, as (bbox, pixel count).

    Eight-connected, because a sprite's outline meets itself diagonally and
    four-connectivity splits a boot off an ankle."""
    px = im.load()
    W, H = im.size
    seen = bytearray(W * H)
    out = []
    for y in range(H):
        row = y * W
        for x in range(W):
            if px[x, y][3] <= 8 or seen[row + x]: continue
            q = deque([(x, y)]); seen[row + x] = 1
            x0 = x1 = x; y0 = y1 = y; n = 0
            while q:
                cx, cy = q.popleft(); n += 1
                if cx < x0: x0 = cx
                if cx > x1: x1 = cx
                if cy < y0: y0 = cy
                if cy > y1: y1 = cy
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < W and 0 <= ny < H and not seen[ny * W + nx] \
                           and px[nx, ny][3] > 8:
                            seen[ny * W + nx] = 1
                            q.append((nx, ny))
            out.append(((x0, y0, x1, y1), n))
    return out


def overlap(a, b):
    ax0, ay0, ax1, ay1 = a
    bx0, by0, bx1, by1 = b
    w = min(ax1, bx1) - max(ax0, bx0) + 1
    h = min(ay1, by1) - max(ay0, by0) + 1
    return max(0, w) * max(0, h)


for png, js in SHEETS:
    ppath, jpath = os.path.join(ROOT, png), os.path.join(ROOT, js)
    if not os.path.exists(ppath): continue
    im = Image.open(ppath).convert("RGBA")
    frames = json.load(open(jpath))
    comps = components(im)
    print(f"=== {png}  {im.size}  {len(comps)} islands, {sum(len(v) for v in frames.values())} frames")

    grown = dropped = 0
    fixed = {}
    for name, lst in frames.items():
        out = []
        for i, f in enumerate(lst):
            box = (f["x"], f["y"], f["x"] + f["w"] - 1, f["y"] + f["h"] - 1)
            area = f["w"] * f["h"]
            # An island belongs to this frame if most of it is inside the frame.
            # "Most of it", not "any of it": sprites are packed close, and a
            # single pixel of the neighbour clipping the corner must not drag the
            # whole neighbour in.
            mine = [c for c, n in comps
                    if overlap(c, box) >= 0.55 * ((c[2] - c[0] + 1) * (c[3] - c[1] + 1))
                    and overlap(c, box) > 0]
            if not mine:
                out.append(f); continue
            x0 = min(c[0] for c in mine); y0 = min(c[1] for c in mine)
            x1 = max(c[2] for c in mine); y1 = max(c[3] for c in mine)
            nf = {"x": x0, "y": y0, "w": x1 - x0 + 1, "h": y1 - y0 + 1}
            if (nf["x"], nf["y"], nf["w"], nf["h"]) != (f["x"], f["y"], f["w"], f["h"]):
                grown += 1
                print(f"   {name}[{i}] {f['w']}x{f['h']} -> {nf['w']}x{nf['h']}"
                      f"  (+{nf['w']-f['w']}w +{nf['h']-f['h']}h)")
            out.append(nf)

        # A second frame that is a different shape from the first is a different
        # thing. Drop it and let the renderer mirror frame 0.
        if len(out) > 1:
            h0 = out[0]["h"]
            keep = [out[0]] + [g for g in out[1:] if abs(g["h"] - h0) <= 0.30 * h0]
            if len(keep) != len(out):
                dropped += len(out) - len(keep)
                for g in out[1:]:
                    if g not in keep:
                        print(f"   {name}: dropped a frame {g['w']}x{g['h']} "
                              f"against a {out[0]['w']}x{h0} first frame — not the same subject")
            out = keep
        fixed[name] = out

    print(f"   grown {grown}, dropped {dropped}")
    if WRITE:
        json.dump(fixed, open(jpath, "w"), separators=(",", ":"))
        print(f"   wrote {jpath}")

if not WRITE:
    print("\n(dry run — pass --write to save)")
