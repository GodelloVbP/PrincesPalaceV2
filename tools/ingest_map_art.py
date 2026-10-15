#!/usr/bin/env python3
"""Ingest the run-map art batch (docs/prompts/2026-10-15_run_map_art.md).

    py tools/ingest_map_art.py [--src DIR] [--out DIR] [--only medallions|chips|strip|heart]

Reads Art/Incoming/run_map/ and writes Assets/_Project/Art/Backgrounds/Processed/:

  map_medallions.png (3x3, #00FF00)  -> map_node_<type>.png, nine of them
  map_chips.png      (5x1, #00FF00)  -> map_chip_<name>.png, five of them
  realm_hush_found.png / _eased.png  -> same names, width 1920, vertical seam tile
  realm_hush_heart.png               -> same name, width 1920, top edge faded to alpha 0

A missing source is skipped, so the sheets can arrive one at a time.

Keying reuses key_green_screen.key_out_green (hue dominance, so dark painted
rims survive). The sheet is then cut into its separate icons by connectivity
(the generator lets tall icons spill past their grid cell). Each icon is
trimmed to its content and padded with the same margin on all four sides, so every icon sits centred in a
canvas sized to its own shape and the layout code's preserveAspect fit centres
them alike.

The strip is made to tile by crossfading its bottom band into its top band and
dropping the band, so the last row continues into the first with no cut. The
found and eased strips go through the same arithmetic on the same dimensions,
which is what keeps them pixel-aligned for the eased overlay.
"""

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from scipy import ndimage
from PIL import Image

from key_green_screen import content_bounds, force_sprite_import, key_out_green

Image.MAX_IMAGE_PIXELS = None

DEFAULT_SRC = "Art/Incoming/run_map"
DEFAULT_OUT = "Assets/_Project/Art/Backgrounds/Processed"

# Row-major order of the prompt's 3x3 and 5x1 sheets.
MEDALLIONS = ["fight", "elite", "boss", "rest", "event", "shop", "treasure", "cache", "entry"]
CHIPS = ["gold", "potion", "gear", "relic", "book"]

STRIP_WIDTH = 1920
SEAM_BAND = 0.12        # fraction of the strip height crossfaded away
DILATE = 6              # px; joins an icon's detached bits, not neighbouring icons
HEART_FADE = 0.22       # fraction of the lair's height that fades in from the top
MARGIN = 0.06           # trim margin, fraction of the longer content side


def split_pieces(keyed, count, cols):
    """The `count` largest separate pieces of a keyed sheet, in reading order.

    Cut by connectivity, not on an equal grid: the generator does not keep each
    icon inside its own cell (a tall badge spills into the row below), so an
    equal cut slices icons in half. A small dilation joins an icon's detached
    studs and finials to it without bridging the gutters between icons.
    """
    alpha = np.asarray(keyed.getchannel("A")) > 8
    grown = ndimage.binary_dilation(alpha, iterations=DILATE)
    labels, n = ndimage.label(grown)
    if n < count:
        raise SystemExit(f"expected {count} pieces on the sheet, found {n}")
    sizes = ndimage.sum(alpha, labels, range(1, n + 1))
    keep = sorted(np.argsort(sizes)[::-1][:count] + 1)
    centres = {k: ndimage.center_of_mass(labels == k) for k in keep}
    rows = count // cols
    by_y = sorted(keep, key=lambda k: centres[k][0])
    order = []
    for r in range(rows):
        row = by_y[r * cols:(r + 1) * cols]
        order += sorted(row, key=lambda k: centres[k][1])
    pixels = np.asarray(keyed).copy()
    pieces = []
    for k in order:
        piece = pixels.copy()
        piece[labels != k, 3] = 0
        pieces.append(Image.fromarray(piece, "RGBA"))
    return pieces


def trim_even(rgba, margin=MARGIN):
    """Crop to content, then pad equally on every side."""
    box = content_bounds(rgba)
    cropped = rgba.crop(box)
    pad = max(1, round(max(cropped.size) * margin))
    out = Image.new("RGBA", (cropped.width + 2 * pad, cropped.height + 2 * pad), (0, 0, 0, 0))
    out.paste(cropped, (pad, pad))
    return out


def key_and_slice(path, cols, rows, names):
    sheet = Image.open(path).convert("RGB")
    pieces = split_pieces(key_out_green(sheet), cols * rows, cols)
    return {name: trim_even(piece) for name, piece in zip(names, pieces)}


def seam_height(height, band=SEAM_BAND):
    return height - max(1, int(round(height * band)))


def seam_tile(image, band=SEAM_BAND):
    """Make `image` tile vertically: bottom band crossfades into the top band."""
    arr = np.asarray(image.convert("RGB"), dtype=np.float32)
    h = arr.shape[0]
    b = h - seam_height(h, band)
    out = arr[: h - b].copy()
    w = ((np.arange(b, dtype=np.float32) + 0.5) / b)[:, None, None]
    out[:b] = (1.0 - w) * arr[h - b:] + w * arr[:b]
    return Image.fromarray(np.clip(out + 0.5, 0, 255).astype(np.uint8), "RGB")


def scaled_height(image, width=STRIP_WIDTH):
    return max(1, round(image.height * width / image.width))


def scale_to_width(image, width=STRIP_WIDTH):
    if image.width == width:
        return image
    return image.resize((width, scaled_height(image, width)), Image.LANCZOS)


def fade_top(image, fraction=HEART_FADE):
    """RGBA copy whose top `fraction` ramps from transparent to opaque.

    The prompt asks the generator for a top edge that blends into the strip,
    but a painted blend is still opaque and shows as a band over a strip that
    differs from the one the painter saw. Alpha is the only blend that is
    exact.
    """
    rgba = np.asarray(image.convert("RGBA")).copy()
    rows = max(1, int(round(rgba.shape[0] * fraction)))
    ramp = (np.arange(rows, dtype=np.float32) + 0.5) / rows
    ramp = ramp * ramp * (3.0 - 2.0 * ramp)
    rgba[:rows, :, 3] = np.round(rgba[:rows, :, 3] * ramp[:, None]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def save(image, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)
    force_sprite_import(path)
    print(f"wrote {path} {image.size[0]}x{image.size[1]}")


def run(src, out, only=None):
    wrote = []

    def sheet(name, cols, rows, names, prefix):
        path = os.path.join(src, name)
        if not os.path.exists(path):
            print(f"skip {path} (not delivered)")
            return
        for key, img in key_and_slice(path, cols, rows, names).items():
            target = os.path.join(out, f"{prefix}{key}.png")
            save(img, target)
            wrote.append(target)

    if only in (None, "medallions"):
        sheet("map_medallions.png", 3, 3, MEDALLIONS, "map_node_")
    if only in (None, "chips"):
        sheet("map_chips.png", 5, 1, CHIPS, "map_chip_")
    if only in (None, "strip"):
        for name in ("realm_hush_found.png", "realm_hush_eased.png"):
            path = os.path.join(src, name)
            if not os.path.exists(path):
                print(f"skip {path} (not delivered)")
                continue
            target = os.path.join(out, name)
            save(seam_tile(scale_to_width(Image.open(path))), target)
            wrote.append(target)
    if only in (None, "heart"):
        path = os.path.join(src, "realm_hush_heart.png")
        if os.path.exists(path):
            target = os.path.join(out, "realm_hush_heart.png")
            save(fade_top(scale_to_width(Image.open(path).convert("RGB"))), target)
            wrote.append(target)
        else:
            print(f"skip {path} (not delivered)")
    return wrote


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--src", default=DEFAULT_SRC)
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--only", choices=["medallions", "chips", "strip", "heart"])
    args = parser.parse_args()
    run(args.src, args.out, args.only)


if __name__ == "__main__":
    sys.exit(main())
