#!/usr/bin/env python3
"""Slice a spell VFX sheet into one transparent PNG per animation frame.

Deliberately NOT slice_enemy_sheet.py, and the difference matters.

That tool re-anchors every pose onto a common baseline, because a character
whose feet sit at different heights visibly jumps when the pose changes. For a
VFX sheet the opposite is true: where the effect sits INSIDE its cell IS the
animation. Frame 1 is sparks gathering at the top with nothing at the bottom
at all, so baseline-aligning it would shove it down the screen to meet a
ground line it does not have. So this cuts a plain fixed grid and preserves
each cell exactly as drawn.

Two things it does do:

1. KEYS THE BLACK BACKGROUND TO ALPHA, using luminance. A glow has no clean
   edge to cut around - the whole point is that it falls off softly - so a
   hard threshold leaves a visible rectangle of not-quite-black around every
   bolt. Alpha = luminance means the falloff becomes the transparency, which
   is also what an additively-blended effect would do anyway.

   RGB is left alone. Only the alpha channel is synthesised.

2. ERASES FULL-WIDTH / FULL-HEIGHT LIGHT LINES. One of the delivered sheets
   came back with a white frame around the image and a white divider along
   the row boundary, despite being asked for neither. Keyed naively those
   become fully opaque white bars welded to the edge of four frames. A line
   that spans essentially the entire image is never art, so it is removed
   before keying.

Usage:
    python tools/slice_spell_sheet.py                 # every entry in VFX
    python tools/slice_spell_sheet.py frost_flare     # one entry
    python tools/slice_spell_sheet.py --sheet <p.png> --out <dir> \\
        --rows 2 --cols 3 --names f0 f1 f2 f3 f4 f5   # one-off, unrecorded
"""

import argparse
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

SOURCE_DIR = "Assets/_Project/Art/Sheets"
OUTPUT_ROOT = "Assets/_Project/Resources/Spells"

# Which sheet each shipped VFX came from, and how it was cut.
#
# Before this existed the answer lived only in shell history: every
# parameter, including the frame names, came from argv. Both entries below
# are verified -- re-running them regenerates the committed PNGs
# BYTE-IDENTICAL, which is the proof the recipe is the real one rather than
# a plausible-looking guess.
VFX = {
    "frost_flare": {
        "sheet": "frost_flare.png",
        "grid": (2, 3),
        "names": ["f0", "f1", "f2", "f3", "f4", "f5"],
    },
    "lightning_bolt": {
        "sheet": "lightning_bolt.png",
        "grid": (2, 3),
        "names": ["f0", "f1", "f2", "f3", "f4", "f5"],
    },
}

# VFX that exist on disk but that this tool did NOT produce and cannot
# reproduce. Recorded rather than omitted: "we don't know" is worth
# committing, and a silent gap in the manifest reads as "nothing to see".
#
# golem_boulder is a hand-assembled sequence, and the evidence is in the
# frames themselves: f2 and f3 are byte-identical (a held peak), f4 and f5
# are different content at max alpha 178 then 76 (a two-step fade-out), and
# f0 is fully transparent (a wind-up beat before the rocks burst, which
# lines up with the golem's vfxImpactFrame: 3 -- 1-based, so impact lands on
# the f2 peak). No keyer produces a held duplicate or a stepped alpha ramp.
#
# Its apparent source, Art/Enemies/golem_sheet_attack_rock.png, is a
# WHITE-backed painted sheet whose 2x3 cells are 724x362 -- the shipped
# frames are 598x433, so they were not cut from it on that grid either.
# Reconstructing the real recipe needs the author, not more measurement.
HAND_ASSEMBLED = {
    "golem_boulder": (
        "Hand-assembled, not tool-produced. f2==f3 (held peak), f4/f5 are a "
        "stepped alpha fade-out, f0 is an intentional blank wind-up frame. "
        "Do not regenerate from golem_sheet_attack_rock.png -- the grid does "
        "not match and the post-steps are not reproducible here."
    ),
}

# A pixel this bright counts as "light" when hunting for drawn grid lines.
LIGHT_THRESHOLD = 200

# What fraction of a row/column must be light before it is treated as a drawn
# line rather than as art. Real art never spans a whole edge uniformly.
LINE_COVERAGE = 0.90

# Below this luminance a pixel is background and gets alpha 0 outright. Keeps
# the near-black noise floor of a JPEG-ish render from becoming a grey haze
# over the whole frame.
BLACK_FLOOR = 10


def luminance(pixel):
    r, g, b = pixel[0], pixel[1], pixel[2]
    # Perceptual weights: a saturated blue bolt should not key out darker than
    # a white core of the same visual brightness.
    return (r * 299 + g * 587 + b * 114) // 1000


def erase_drawn_lines(image):
    """Blank any row or column that is almost entirely light pixels."""
    width, height = image.size
    pixels = image.load()
    removed = 0

    for y in range(height):
        light = sum(1 for x in range(0, width, 4) if luminance(pixels[x, y]) >= LIGHT_THRESHOLD)
        if light >= (width // 4) * LINE_COVERAGE:
            for x in range(width):
                pixels[x, y] = (0, 0, 0)
            removed += 1

    for x in range(width):
        light = sum(1 for y in range(0, height, 4) if luminance(pixels[x, y]) >= LIGHT_THRESHOLD)
        if light >= (height // 4) * LINE_COVERAGE:
            for y in range(height):
                pixels[x, y] = (0, 0, 0)
            removed += 1

    return removed


# A row/column of a FINISHED frame this covered counts as a drawn border
# rather than as art.
FRAME_BORDER_COVERAGE = 0.85

# ...and only if the art two pixels away is this sparse. A real effect that
# genuinely spans the frame (a bolt from ceiling to floor) has neighbours;
# a drawn border sits alone with nothing beside it.
FRAME_BORDER_ISOLATION = 0.35


def erase_frame_borders(frame):
    """Blank rows/columns that span a CROPPED frame edge to edge.

    erase_drawn_lines only sees lines that cross the whole sheet, which is
    the wrong shape for the common case: a generator that draws a box around
    every cell. Those borders are one cell wide, so they never reach the
    sheet-wide coverage threshold and survive the cut intact — which is
    exactly how Frost Flare ended up playing a rectangle around its target.
    """
    pixels = frame.load()
    width, height = frame.size

    rows = [sum(1 for x in range(width) if pixels[x, y][3] > 0) / width for y in range(height)]
    cols = [sum(1 for y in range(height) if pixels[x, y][3] > 0) / height for x in range(width)]

    def isolated(profile, i):
        before = profile[i - 2] if i >= 2 else 0.0
        after = profile[i + 2] if i + 2 < len(profile) else 0.0
        return before < FRAME_BORDER_ISOLATION and after < FRAME_BORDER_ISOLATION

    removed = 0
    for y, coverage in enumerate(rows):
        if coverage >= FRAME_BORDER_COVERAGE and isolated(rows, y):
            for x in range(width):
                r, g, b, _ = pixels[x, y]
                pixels[x, y] = (r, g, b, 0)
            removed += 1

    for x, coverage in enumerate(cols):
        if coverage >= FRAME_BORDER_COVERAGE and isolated(cols, x):
            for y in range(height):
                r, g, b, _ = pixels[x, y]
                pixels[x, y] = (r, g, b, 0)
            removed += 1

    return removed


def key_to_alpha(image):
    """RGB on black -> RGBA, with alpha taken from luminance."""
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size

    for y in range(height):
        for x in range(width):
            r, g, b, _ = pixels[x, y]
            lum = luminance((r, g, b))
            pixels[x, y] = (r, g, b, 0 if lum <= BLACK_FLOOR else min(255, lum))

    return rgba


def slice_sheet(sheet_path, out_dir, rows, cols, names):
    expected = rows * cols
    if len(names) != expected:
        sys.exit(f"Need exactly {expected} names for a {rows}x{cols} sheet, got {len(names)}.")

    # Same guard slice_actor_sheet.py carries: re-processing already-keyed
    # output compounds resample loss, and is how a previous art pass went
    # wrong.
    if "/Resources/" in os.path.abspath(sheet_path).replace("\\", "/"):
        sys.exit(f"Refusing to read from a Resources/ path (would compound a previous pass): {sheet_path}")
    if not os.path.isfile(sheet_path):
        sys.exit(f"Missing source sheet: {sheet_path}")

    sheet = Image.open(sheet_path).convert("RGB")
    width, height = sheet.size
    if width % cols or height % rows:
        print(f"warning: {width}x{height} does not divide evenly into {cols}x{rows}; "
              f"cells will be truncated by up to a pixel.")

    lines = erase_drawn_lines(sheet)
    if lines:
        print(f"erased {lines} drawn grid line(s)")

    keyed = key_to_alpha(sheet)
    os.makedirs(out_dir, exist_ok=True)

    cell_w = width // cols
    cell_h = height // rows

    blanks = []
    for index, name in enumerate(names):
        row, col = divmod(index, cols)
        box = (col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h)
        frame = keyed.crop(box)

        borders = erase_frame_borders(frame)
        if borders:
            print(f"  {name}: erased {borders} cell border line(s)")

        opaque = sum(1 for a in frame.getchannel("A").getdata() if a > 8)
        coverage = opaque * 100 // (cell_w * cell_h)
        if opaque == 0:
            blanks.append(index)

        out_path = os.path.join(out_dir, f"{name}.png")
        frame.save(out_path)
        print(f"  {name}.png  {cell_w}x{cell_h}  {coverage}% visible")

    # A blank frame is legitimate ONLY as a lead-in beat (see the
    # golem_boulder note by HAND_ASSEMBLED). One in the middle of a sequence
    # means the key ate a cell, which is invisible until someone watches the
    # effect play.
    for index in blanks:
        if index > 0:
            print(f"  WARNING: '{names[index]}' (frame {index}) is fully transparent. A blank frame is only "
                  f"expected as a lead-in at frame 0 -- this one is mid-sequence, which usually means the "
                  f"luminance key erased the cell.")

    print(f"wrote {len(names)} frames to {out_dir}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("ids", nargs="*", help="VFX ids to process (default: every entry in VFX)")
    parser.add_argument("--sheet", help="One-off mode: slice this sheet instead of a manifest entry")
    parser.add_argument("--out", help="One-off mode: output directory")
    parser.add_argument("--rows", type=int, default=2)
    parser.add_argument("--cols", type=int, default=3)
    parser.add_argument("--names", nargs="+")
    args = parser.parse_args()

    if args.sheet:
        if not args.out or not args.names:
            parser.error("--sheet also needs --out and --names")
        slice_sheet(args.sheet, args.out, args.rows, args.cols, args.names)
        print("NOTE: one-off mode wrote nothing to the VFX manifest. If this output is going to "
              "ship, add an entry so the recipe survives.")
        return

    ids = args.ids or list(VFX)
    for vfx_id in ids:
        if vfx_id in HAND_ASSEMBLED:
            sys.exit(f"'{vfx_id}' is hand-assembled and must not be regenerated: {HAND_ASSEMBLED[vfx_id]}")
        if vfx_id not in VFX:
            sys.exit(f"Unknown VFX '{vfx_id}'. Known: {', '.join(sorted(VFX))}")
        spec = VFX[vfx_id]
        rows, cols = spec["grid"]
        print(f"[{vfx_id}] {spec['sheet']} {rows}x{cols}")
        slice_sheet(os.path.join(SOURCE_DIR, spec["sheet"]),
                    os.path.join(OUTPUT_ROOT, vfx_id), rows, cols, spec["names"])


if __name__ == "__main__":
    main()
