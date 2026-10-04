#!/usr/bin/env python3
"""Procedural spell pieces, for effects that are shapes rather than drawings.

Usage, from the project root:
    python tools/make_spell_fx.py                  # write every piece
    python tools/make_spell_fx.py court_price_pulse
    python tools/make_spell_fx.py --out DIR        # write under DIR instead of Resources/Spells

A piece is added here only when a spell's blocking shows it needs one and no
existing frame can stand in. Each is deterministic (no clock, no RNG), so a
rerun is byte-identical and the test pins the pixels.

court_price_pulse: a soft violet ring around a glowing core, one 256x256 frame.
    Court of Whispers' price on Shawn. Violet like the court, a ring rather than
    figures so it reads as "something was taken" and not as a second court.

mud_glob: one 256x256 frame, a lumpy matte-brown glob travelling right with a
    few drips trailing left. Mud Burst's lobbed projectile; matte on purpose
    (the old sheet's gold glints read as fire).
mud_splat_drops: four 128x128 particle frames (round, long, teardrop, crumb) in
    the same brown, for the splat's falling drops. Emitters cannot take a tint,
    so the colour is in the pixels.
mud_burst_splat: the existing mud_burst sheet's burst frames (f12-f25) with the
    long streak to the left of the burst feathered away, so the burst can land
    on the target without a rope of mud back to the caster. Derived from the
    shipped frames, nothing painted.

shackle_cinch_ring: one 256x256 frame, a thin gold-white ring with a crimson halo
    outside it. Velvet Shackles' cinch: drawn oversized for two frames and snapped
    onto the body (layer punch), so the bind reads as pulled tight.
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPELLS = os.path.join(ROOT, "Assets", "_Project", "Resources", "Spells")
SIZE = 256


def court_price_pulse():
    """RGBA uint8, SIZE x SIZE: ring at 0.62 of the radius, faint core."""
    ys, xs = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
    r = np.hypot(xs - (SIZE - 1) / 2, ys - (SIZE - 1) / 2) / (SIZE / 2)
    ring = np.exp(-((r - 0.62) / 0.09) ** 2)
    core = 0.35 * np.exp(-(r / 0.3) ** 2)
    edge = np.clip((1.0 - r) / 0.08, 0.0, 1.0)
    alpha = np.clip(ring + core, 0.0, 1.0) * edge
    # White-hot where the ring is densest, violet at its soft edge.
    heat = np.clip(ring, 0.0, 1.0) ** 2
    rgb = np.stack([
        150 + 105 * heat,
        90 + 140 * heat,
        230 + 25 * heat,
    ], axis=-1)
    out = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[..., 3] = np.round(alpha * 255).astype(np.uint8)
    return out


def shackle_cinch_ring():
    """RGBA uint8, SIZE x SIZE: ring at 0.78 of the radius, crimson halo outside."""
    ys, xs = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
    r = np.hypot(xs - (SIZE - 1) / 2, ys - (SIZE - 1) / 2) / (SIZE / 2)
    ring = np.exp(-((r - 0.78) / 0.035) ** 2)
    halo = 0.55 * np.exp(-((r - 0.84) / 0.10) ** 2)
    edge = np.clip((1.0 - r) / 0.06, 0.0, 1.0)
    alpha = np.clip(ring + halo, 0.0, 1.0) * edge
    heat = np.clip(ring, 0.0, 1.0)
    rgb = np.stack([
        205 + 50 * heat,
        40 + 196 * heat,
        62 + 108 * heat,
    ], axis=-1)
    out = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[..., 3] = np.round(alpha * 255).astype(np.uint8)
    return out


def _blob_mask(shape, circles, wobble, seed):
    """Soft 0..1 coverage of a union of (x, y, r) circles with a sinusoidal
    boundary wobble; the phases come from `seed` so a rerun is identical."""
    h, w = shape
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float64)
    rng = np.random.RandomState(seed)
    phase = rng.uniform(0, 2 * np.pi, 3)
    best = np.full(shape, -1e9)
    for cx, cy, r in circles:
        dx, dy = xs - cx, ys - cy
        ang = np.arctan2(dy, dx)
        edge = r * (1 + wobble * (np.sin(3 * ang + phase[0]) + 0.6 * np.sin(5 * ang + phase[1])
                                   + 0.4 * np.sin(7 * ang + phase[2])) / 2.0)
        best = np.maximum(best, edge - np.hypot(dx, dy))
    return np.clip(best / 1.0 + 0.5, 0.0, 1.0), best


MUD = np.array([86.0, 62.0, 44.0])


def _mud_rgba(cover, depth, size, light=(-0.5, -0.6)):
    """Matte shading: darker toward the rim and the lower right, lighter up
    left, a flat range of about +-22 so nothing specular ever reads as glow."""
    h, w = cover.shape
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float64)
    shade = np.clip(depth / max(size, 1.0), 0.0, 1.0)
    lit = ((xs - w / 2) * light[0] + (ys - h / 2) * light[1]) / (size * 1.2)
    tone = 1.0 + 0.22 * np.clip(-lit, -1, 1) - 0.25 * (1.0 - shade) ** 2
    tone = tone * np.where(depth < 3.0, 0.55, 1.0)  # a dark rim so the silhouette holds on a brown floor
    rgb = MUD[None, None, :] * tone[..., None]
    out = np.zeros((h, w, 4), dtype=np.uint8)
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[..., 3] = np.round(cover * 255).astype(np.uint8)
    return out


def mud_glob():
    """RGBA uint8, 256x256: the glob right of centre, drips trailing left."""
    circles = [(150, 128, 44), (128, 120, 30), (172, 140, 30), (140, 150, 26), (165, 108, 24),
               (92, 134, 10), (66, 126, 7), (110, 156, 8)]
    cover, depth = _blob_mask((SIZE, SIZE), circles, 0.12, 11)
    return _mud_rgba(cover, depth, 44)


def _drop_frame(circles, seed, wobble=0.1, size=128):
    cover, depth = _blob_mask((size, size), circles, wobble, seed)
    return _mud_rgba(cover, depth, 18)


def mud_drop_round():
    return _drop_frame([(64, 64, 22)], 21)


def mud_drop_long():
    return _drop_frame([(40, 64, 12), (58, 64, 14), (78, 64, 11), (92, 64, 7)], 22)


def mud_drop_tear():
    return _drop_frame([(64, 78, 18), (64, 56, 12), (64, 40, 7)], 23)


def mud_drop_crumb():
    return _drop_frame([(50, 60, 9), (72, 70, 10), (62, 48, 6), (80, 52, 5)], 24)


SPLAT_FRAMES = range(12, 26)


def _splat_frame(n):
    def make():
        src = np.asarray(Image.open(os.path.join(SPELLS, "mud_burst", f"f{n}.png")).convert("RGBA")).astype(np.float64)
        h, w = src.shape[:2]
        x = (np.arange(w) + 0.5) / w
        keep = np.clip((x - 0.40) / 0.08, 0.0, 1.0)  # feather the streak out over 0.40-0.48
        src[..., 3] *= keep[None, :]
        return np.round(src).astype(np.uint8)
    return make


PIECES = {
    "court_price_pulse": [court_price_pulse],
    "shackle_cinch_ring": [shackle_cinch_ring],
    "mud_glob": [mud_glob],
    "mud_splat_drops": [mud_drop_round, mud_drop_long, mud_drop_tear, mud_drop_crumb],
    "mud_burst_splat": [_splat_frame(n) for n in SPLAT_FRAMES],
}


def write(name, root):
    folder = os.path.join(root, name)
    os.makedirs(folder, exist_ok=True)
    for index, make in enumerate(PIECES[name]):
        Image.fromarray(make(), "RGBA").save(os.path.join(folder, f"f{index}.png"))
        print(f"wrote {os.path.join(folder, f'f{index}.png')}")


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("names", nargs="*", help="pieces to write (default all)")
    parser.add_argument("--out", default=SPELLS)
    args = parser.parse_args(argv)
    unknown = [n for n in args.names if n not in PIECES]
    if unknown:
        sys.exit(f"unknown piece(s) {unknown}; known: {sorted(PIECES)}")
    for name in args.names or sorted(PIECES):
        write(name, args.out)


if __name__ == "__main__":
    main(sys.argv[1:])
