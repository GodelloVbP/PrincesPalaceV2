"""
Crops a head-and-shoulders dossier portrait out of a character's existing
idle stance still.

Why this exists: a painted portrait is real art (see
remove_portrait_backgrounds.py, which keys a flat-color backdrop off an
AI-generated portrait sheet via border flood-fill) and someone has to
commission and key one per character. Until that happens for a given
character, giving their dossier plate someone else's face (as bear and owl
both currently do, wearing Portraits/sheep) is worse than a soft, obviously-
stand-in crop of art the character already has. This tool is that stopgap:
it takes the idle stance PNG already sitting under Resources/<Enemies|
Characters>/<id>/idle.png (produced by slice_actor_sheet.py) and crops the
top slice of the figure's own alpha bounding box, i.e. head and shoulders,
scaled up to roughly the dossier portrait's own resolution.

It does NOT key anything — the still already carries real alpha from the
slicer, so there is no backdrop to remove. remove_portrait_backgrounds.py
stays the tool for painted portrait sheets; this one is for borrowing a
crop from art that already exists, and should stop being used for a
character the moment a painted portrait is commissioned for them.

Sizing: the crop is small (a still's head is a fraction of its ~350-450px
idle height) and the dossier plate is drawn from a source several times
that size (Portraits/sheep.png is 1122x1402). Upscaling a ~140x175px crop
to that resolution with LANCZOS is a real quality loss — it will read as
soft next to a painted portrait — which is why this is a "for now" tool:
the README section it prints a reminder to add should say so plainly.
Rather than resample straight to the target's exact pixel dimensions
(a fractional, non-integer scale factor that resampling artifacts compound),
this multiplies the crop's own native size by the largest whole integer
that does not overshoot the target's height. That keeps every output pixel
a clean multiple of a source pixel and lands close to, not necessarily
exactly on, the target's pixel dimensions -- "same aspect", not "same
canvas size".

Usage:
    python tools/portrait_from_stance.py --still Assets/_Project/Resources/Characters/bear/idle.png \\
        --out Assets/_Project/Resources/Portraits/bear.png

    # override how much of the figure's height to keep, or crop from an
    # explicit pixel row instead of the bbox top:
    python tools/portrait_from_stance.py --still <path> --out <path> --height-fraction 0.5
    python tools/portrait_from_stance.py --still <path> --out <path> --top 20 --height-fraction 0.4
"""

import argparse
import os

import numpy as np
from PIL import Image

DEFAULT_HEIGHT_FRACTION = 0.45
DEFAULT_ASPECT_REFERENCE = "Assets/_Project/Resources/Portraits/sheep.png"


def alpha_bbox(img: Image.Image) -> tuple:
    """(x0, y0, x1, y1) inclusive bounds of non-transparent pixels."""
    arr = np.array(img.convert("RGBA"))
    ys, xs = np.where(arr[:, :, 3] > 0)
    if len(ys) == 0:
        raise SystemExit("still has no opaque pixels -- nothing to crop")
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def measure_target_aspect(reference_path: str) -> float:
    """width/height of the existing portrait this output should match, measured
    fresh rather than hardcoded so a future portrait delivery at a different
    canvas size doesn't leave this tool cropping to a stale ratio."""
    with Image.open(reference_path) as ref:
        w, h = ref.size
    return w / h


def crop_box(bbox: tuple, height_fraction: float, top_override: int, aspect: float) -> tuple:
    x0, y0, x1, y1 = bbox
    bbox_w = x1 - x0 + 1
    bbox_h = y1 - y0 + 1

    top = y0 if top_override is None else y0 + top_override
    crop_h = round(bbox_h * height_fraction)
    crop_w = round(crop_h * aspect)

    center_x = x0 + bbox_w / 2
    left = round(center_x - crop_w / 2)
    right = left + crop_w
    bottom = top + crop_h
    return left, top, right, bottom


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--still", required=True, help="path to the idle (or other) stance PNG to crop from")
    parser.add_argument("--out", required=True, help="path to write the portrait PNG to")
    parser.add_argument("--height-fraction", type=float, default=DEFAULT_HEIGHT_FRACTION,
                        help=f"fraction of the figure's alpha-bbox height to keep, from the top (default {DEFAULT_HEIGHT_FRACTION})")
    parser.add_argument("--top", type=int, default=None,
                        help="pixel row (relative to the bbox top) to start the crop at, instead of the bbox top itself")
    parser.add_argument("--aspect-reference", default=DEFAULT_ASPECT_REFERENCE,
                        help=f"existing portrait to measure the target aspect from (default {DEFAULT_ASPECT_REFERENCE})")
    args = parser.parse_args()

    if not os.path.isfile(args.still):
        raise SystemExit(f"no such still: {args.still}")

    still = Image.open(args.still).convert("RGBA")
    bbox = alpha_bbox(still)
    aspect = measure_target_aspect(args.aspect_reference)

    left, top, right, bottom = crop_box(bbox, args.height_fraction, args.top, aspect)
    # Clamp to the source canvas -- a --top override or a very wide/short
    # bbox could otherwise ask for pixels outside the image.
    left = max(0, left)
    top = max(0, top)
    right = min(still.width, right)
    bottom = min(still.height, bottom)

    cropped = still.crop((left, top, right, bottom))
    crop_w, crop_h = cropped.size
    print(f"crop box (source px): ({left}, {top}) - ({right}, {bottom}), size {crop_w}x{crop_h}")

    with Image.open(args.aspect_reference) as ref:
        target_w, target_h = ref.size

    if crop_h < target_h or crop_w < target_w:
        factor = max(1, min(target_w // crop_w, target_h // crop_h))
        out_size = (crop_w * factor, crop_h * factor)
        result = cropped.resize(out_size, Image.LANCZOS)
        print(f"upscaled {factor}x with LANCZOS (native {crop_w}x{crop_h} -> {out_size[0]}x{out_size[1]}); "
              f"reference portrait is {target_w}x{target_h} -- this is a crop of existing stance art, "
              f"not painted source, so it will read soft next to a painted portrait. That is the "
              f"accepted tradeoff until a real portrait is commissioned.")
    else:
        result = cropped
        print(f"crop already >= reference size ({target_w}x{target_h}); wrote at native size {crop_w}x{crop_h}, no upscale")

    os.makedirs(os.path.dirname(args.out) or ".", exist_ok=True)
    result.save(args.out)
    print(f"wrote {args.out} ({result.size[0]}x{result.size[1]})")


if __name__ == "__main__":
    main()
