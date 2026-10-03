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


PIECES = {"court_price_pulse": [court_price_pulse]}


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
