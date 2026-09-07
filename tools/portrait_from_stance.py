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
Characters>/<id>/idle.png (produced by slice_actor_sheet.py) and crops a
head-and-shoulders slice off the top of the figure's own alpha bounding
box, scaled up to roughly the dossier portrait's own resolution. The slice
stays sized to the head band itself (see Centring below): for a stout or
wide-built figure that means the body below the head is free to clip at
the crop's left/right edges rather than the box growing to keep it in
frame, so the default `--height-fraction` may need lowering per character
to keep held weapons and feet out of shot -- see a real example in
Usage below.

Centring: a figure facing sideways (bear faces right, head sits right of
his own torso/hammer centre) has its head off-center from the full-figure
bbox, so centring the crop on that bbox clips the face. The crop is
centred on the HEAD BAND's own alpha extent instead -- the top
`height-fraction` rows of the figure, measured on their own. The band's
own width (plus a small margin) sets the crop width, and the target aspect
sets the crop height from that -- so the band is always whole and centred.
Below the band the figure is free to run wider than the crop (pauldrons,
an out-held hammer): rather than growing the box to keep chasing that
content in frame (an earlier version of this tool did, and for a
broad-built figure that convergence pulled in almost the entire pose), the
body is simply allowed to clip at the left/right edges. A dossier plate
this small reads by the face, not by whether a hammer head is intact at
the bottom corner. See `crop_box`'s docstring for the mechanics.

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
that overshoots NEITHER of the target's dimensions. That keeps every output
pixel a clean multiple of a source pixel and lands close to, not necessarily
exactly on, the target's pixel dimensions -- "same aspect", not "same
canvas size".

The aspect half of that is a contract, not an approximation, and it is the
half a crop against the edge of the still used to break: the crop box is
never clipped to the source canvas, it is PADDED with transparency, so a
head with no clear air above it in the still gets clear air in the portrait
rather than a shortened frame. See main().

Usage:
    python tools/portrait_from_stance.py --still Assets/_Project/Resources/Characters/bear/idle.png \\
        --out Assets/_Project/Resources/Portraits/bear.png --height-fraction 0.20

    # the bear needs a narrower band than the 0.45 default: at 0.45 the
    # band's own width already reaches the shoulder pauldrons' widest
    # point, and matching the portrait's aspect from that width makes the
    # crop tall enough to reach the hammer head and boots. 0.20 keeps the
    # band to head/ears/scar/scarf and lets the pauldron's top edge in
    # without pulling the hammer into frame -- see Art/Characters/bear/
    # README.md's Portrait section for how that value was picked.

    # or crop from an explicit pixel row instead of the bbox top:
    python tools/portrait_from_stance.py --still <path> --out <path> --top 20 --height-fraction 0.4
"""

import argparse
import os

import numpy as np
from PIL import Image

DEFAULT_HEIGHT_FRACTION = 0.45
DEFAULT_ASPECT_REFERENCE = "Assets/_Project/Resources/Portraits/sheep.png"
# How much clear air to leave on each side of the head band's own alpha
# width, as a fraction of that width -- not of the final crop.
HEAD_BAND_MARGIN = 0.04
# How much clear air to leave above the head band's own top row, as a
# fraction of the final crop height -- keeps the crown of the head off the
# frame edge instead of touching it exactly.
TOP_MARGIN = 0.04


def alpha_bbox(img: Image.Image) -> tuple:
    """(x0, y0, x1, y1) inclusive bounds of non-transparent pixels."""
    arr = np.array(img.convert("RGBA"))
    ys, xs = np.where(arr[:, :, 3] > 0)
    if len(ys) == 0:
        raise SystemExit("still has no opaque pixels -- nothing to crop")
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def alpha_row_span(img: Image.Image, y0: int, y1: int) -> tuple:
    """(x0, x1) inclusive horizontal bounds of non-transparent pixels within
    rows [y0, y1] only -- narrower than the full-figure bbox whenever the
    head sits off-center from the body (e.g. a figure facing sideways)."""
    arr = np.array(img.convert("RGBA"))
    band = arr[y0:y1 + 1, :, :]
    ys, xs = np.where(band[:, :, 3] > 0)
    if len(xs) == 0:
        raise SystemExit("head band has no opaque pixels -- nothing to crop")
    return int(xs.min()), int(xs.max())


def measure_target_aspect(reference_path: str) -> float:
    """width/height of the existing portrait this output should match, measured
    fresh rather than hardcoded so a future portrait delivery at a different
    canvas size doesn't leave this tool cropping to a stale ratio."""
    with Image.open(reference_path) as ref:
        w, h = ref.size
    return w / h


def crop_box(still: Image.Image, bbox: tuple, height_fraction: float, top_override: int,
             aspect: float) -> tuple:
    """Crop box sized and centred on the HEAD BAND's own width, not the
    full-figure bbox's -- a figure facing sideways (bear faces right, head
    sits right of the body's centre) has its head off-center from its
    feet/hammer, so centring on the full bbox clips the face. The band is
    the top `height_fraction` slice of the figure's alpha bbox; its own
    alpha extent within those rows gives the head's true horizontal
    position and width.

    Width comes straight from that band: the band's own alpha width plus a
    small margin on each side (HEAD_BAND_MARGIN). Height then comes from
    width via the reference portrait's aspect ratio, and the box sits with
    a small margin of clear air above the band's top row (TOP_MARGIN). No
    widening or growing: whatever of the figure falls below the band --
    pauldrons, a raised or out-held hammer -- is left to clip at the crop's
    left/right edges rather than pulling the box wider to keep it in frame.
    The head band itself is always whole and centred; the body below it is
    not guaranteed to be."""
    x0, y0, x1, y1 = bbox
    bbox_h = y1 - y0 + 1

    band_top = y0 if top_override is None else y0 + top_override
    band_bottom = min(y1, band_top + round(bbox_h * height_fraction) - 1)

    span_x0, span_x1 = alpha_row_span(still, band_top, band_bottom)
    span_w = span_x1 - span_x0 + 1
    center_x = span_x0 + span_w / 2

    crop_w = round(span_w * (1 + 2 * HEAD_BAND_MARGIN))
    crop_h = round(crop_w / aspect)

    left = round(center_x - crop_w / 2)
    right = left + crop_w
    top = band_top - round(crop_h * TOP_MARGIN)
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

    left, top, right, bottom = crop_box(still, bbox, args.height_fraction, args.top, aspect)

    # NOT CLAMPED TO THE SOURCE CANVAS, deliberately. PIL fills an
    # out-of-bounds RGBA crop with transparency, which is exactly the right
    # answer here: the box asked for clear air around the head, and clear air
    # is what padding gives it.
    #
    # Clamping was the first version and it lost the tool's one stated
    # contract -- "same aspect", not "same canvas size". A clamp shortens ONE
    # side of the box, so the output silently comes out at a ratio the dossier
    # plate does not draw at, and nothing says so: the PNG looks fine on its
    # own and is stretched only once something puts it in the frame. It bit at
    # both severities. Portraits/bear.png shipped 1010x1250 rather than
    # 1010x1260 -- the top margin clamped away, so the crown of his head
    # touches the frame edge, which is the one thing TOP_MARGIN exists to
    # prevent. And an owl-shaped still (a wide head band on a canvas barely
    # taller than the band is wide) asked for 460 rows from a 366-row canvas
    # and got a near-SQUARE portrait, 25% off a plate drawn at 4:5.
    cropped = still.crop((left, top, right, bottom))
    crop_w, crop_h = cropped.size
    print(f"crop box (source px): ({left}, {top}) - ({right}, {bottom}), size {crop_w}x{crop_h}")

    padded = [name for name, outside in (
        ("left", left < 0), ("top", top < 0),
        ("right", right > still.width), ("bottom", bottom > still.height)) if outside]
    if padded:
        # Said out loud rather than left to be noticed. Transparent margin on
        # a portrait is fine; a LOT of it means the still had nowhere near
        # enough room around the head, and --height-fraction is the dial.
        print(f"  padded with transparency on the {', '.join(padded)} -- the still's canvas "
              f"ran out before the crop did. The aspect is kept; lower --height-fraction if "
              f"the margin is more than the plate should carry.")

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
