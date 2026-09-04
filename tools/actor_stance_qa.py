#!/usr/bin/env python3
"""Visual QA contact sheets for ACTOR stance STILLS (enemies and party).

Why this exists: ScreenshotTool runs Unity in Edit Mode and cannot capture
live combat state, so there is no other way to SEE a sliced stance before it
ships. Every combat actor now ships one still drawing per stance (see
docs/STANCE_SHEET_SPEC.md) -- this renders one row per actor, one cell per
stance, straight from whatever PNGs are currently on disk under
Resources/Enemies/<id>/ (or Resources/Characters/<id>/).

Standalone: reads existing PNGs, no dependency on slice_actor_sheet.py or
any slicing step. That is deliberate -- it lets you render the CURRENTLY
COMMITTED art as a "before" picture, make a change, and re-run the exact
same command as "after".

Each stance's cell shows the canvas bounds (a thin white rect), the canvas's
own ground line (red, fixed at canvas_h - PADDING -- the same PADDING
slice_actor_sheet.py pastes with, so content that doesn't reach it is
genuinely floating, not a display artifact), that stance's own alpha
centroid (cyan tick), and a sqrt(opaque-area)/actor-median caption. Area,
not bbox height, is the pose-invariant proxy for "how big is this creature
drawn" -- a crouching pose has a shorter bbox than a rearing one even at
the same draw scale (see docs/ART_PIPELINE.md and
ActorArtAssertions.AssertOneDrawScale). The caption is colour-coded so an
outlier reads as red without doing the division yourself.

A creature folder that still holds old <stance>/f0..fN frame folders (the
pre-stills format, mid-migration) is SKIPPED with a one-line notice rather
than treated as a stance -- there is nothing to compare a frame sequence
against here, and guessing which frame is "the" still would misreport it.

Usage:
    python tools/actor_stance_qa.py --report Assets/_Project/Resources/Enemies
    python tools/actor_stance_qa.py --report Assets/_Project/Resources/Characters
    python tools/actor_stance_qa.py --report Assets/_Project/Resources/Enemies --only beetle treant
"""

import argparse
import math
import os
import sys

try:
    import numpy as np
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    sys.exit("Pillow and numpy are required: pip install Pillow numpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sheet_slicing import ALPHA_THRESHOLD, PADDING

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "screenshots", "actor_qa")

# Prone/downed poses legitimately carry less mass than a standing pose --
# forcing them into the same scale band would be exactly the bug this tool
# exists to catch.
PRONE_STANCES = {"defeated"}

# Display order only; anything not listed here still gets discovered and
# shown, just after these.
STANCE_ORDER = ["idle", "cast", "attack", "hurt", "guard", "extra", "taunt", "victory", "defeated"]

THUMB_H = 220
LABEL_W = 150
CELL_PAD = 10
GREEN = (70, 210, 90)
AMBER = (235, 175, 40)
RED = (235, 70, 70)
WHITE = (235, 235, 235)
CYAN = (60, 220, 235)
BG = (40, 40, 44)
TEXT = (225, 225, 225)


def _font():
    return ImageFont.load_default()


def opaque_stats(im):
    """(box, count, sqrt(count), centroid_x) over the whole alpha mask."""
    arr = np.array(im.convert("RGBA"))
    mask = arr[:, :, 3] > ALPHA_THRESHOLD
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return None
    box = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
    count = int(mask.sum())
    return {
        "box": box,
        "count": count,
        "sqrt": math.sqrt(count),
        "cx": float(xs.mean()),
    }


def discover_creature(creature_dir):
    """{stance_name: PIL.Image} -- flat "<stance>.png" files only. A
    "<stance>/" subfolder is the old multi-frame format; it is reported and
    skipped rather than guessed at (see module docstring)."""
    stances = {}
    skipped_folders = []
    for entry in sorted(os.listdir(creature_dir)):
        full = os.path.join(creature_dir, entry)
        if entry.lower().endswith(".png") and os.path.isfile(full):
            stances[entry[:-4]] = Image.open(full).convert("RGBA")
        elif os.path.isdir(full):
            skipped_folders.append(entry)
    return stances, skipped_folders


def ordered_stance_names(stances):
    known = [s for s in STANCE_ORDER if s in stances]
    unknown = sorted(s for s in stances if s not in STANCE_ORDER)
    return known + unknown


def caption_colour(ratio):
    if 0.90 <= ratio <= 1.10:
        return GREEN
    if 0.75 <= ratio <= 1.30:
        return AMBER
    return RED


def draw_stance_cell(canvas_size, thumb_h, im, stat, ratio, ground_y_full, draw_guides=True):
    """One thumbnail: the stance scaled to thumb_h tall, with canvas
    outline, ground line, centroid tick and a colour-coded scale caption."""
    w, h = canvas_size
    scale = thumb_h / h
    thumb_w = max(1, round(w * scale))
    thumb = Image.new("RGBA", (thumb_w, thumb_h + 22), (0, 0, 0, 0))
    resized = im.resize((thumb_w, thumb_h), Image.LANCZOS)
    thumb.paste(resized, (0, 0), resized)

    draw = ImageDraw.Draw(thumb)
    draw.rectangle([0, 0, thumb_w - 1, thumb_h - 1], outline=WHITE, width=1)
    if draw_guides:
        gy = round(ground_y_full * scale)
        draw.line([(0, gy), (thumb_w, gy)], fill=RED, width=1)
        if stat is not None:
            cx = round(stat["cx"] * scale)
            draw.line([(cx, 0), (cx, thumb_h)], fill=CYAN, width=1)

    label = f"{ratio:.2f}" if ratio is not None else "n/a"
    colour = caption_colour(ratio) if ratio is not None else TEXT
    draw.text((2, thumb_h + 2), label, fill=colour, font=_font())
    return thumb


def render_creature(creature_id, creature_dir, out_path, draw_guides=True):
    stances, skipped_folders = discover_creature(creature_dir)
    for folder in skipped_folders:
        print(f"[{creature_id}] '{folder}/' is a multi-frame folder, not a still -- skipped "
              f"(not yet converted to the stills pipeline)")

    if not stances:
        print(f"[{creature_id}] no stance stills found in {creature_dir} -- skipped")
        return []

    canvas_size = None
    stats = {}
    for stance, im in stances.items():
        if canvas_size is None:
            canvas_size = im.size
        elif im.size != canvas_size:
            print(f"  WARNING: {stance} is {im.size}, expected {canvas_size} -- canvas mismatch "
                  f"(every stance must share one canvas, see docs/STANCE_SHEET_SPEC.md)")
        stats[stance] = opaque_stats(im)

    sqrt_values = [
        st["sqrt"] for stance, st in stats.items()
        if stance not in PRONE_STANCES and st is not None
    ]
    median = float(np.median(sqrt_values)) if sqrt_values else 1.0
    ground_y_full = canvas_size[1] - PADDING

    names = ordered_stance_names(stances)
    cells = []
    table_lines = [f"{creature_id}  canvas={canvas_size[0]}x{canvas_size[1]}  median sqrt(area)={median:.1f}"]
    for stance in names:
        im = stances[stance]
        stat = stats[stance]
        ratio = (stat["sqrt"] / median) if (stat is not None and median > 0) else None
        cells.append((stance, draw_stance_cell(canvas_size, THUMB_H, im, stat, ratio, ground_y_full, draw_guides)))
        if stat is not None:
            table_lines.append(
                f"  {stance:12s} box={stat['box'][2]-stat['box'][0]}x{stat['box'][3]-stat['box'][1]}"
                f"  sqrt={stat['sqrt']:.1f}  ratio={ratio:.3f}"
            )

    thumb_w = max(c.width for _, c in cells)
    row_h = THUMB_H + 22 + CELL_PAD
    sheet_w = LABEL_W + len(cells) * (thumb_w + CELL_PAD)
    sheet_h = 40 + row_h

    sheet = Image.new("RGBA", (sheet_w, sheet_h), BG)
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 8), f"{creature_id}  canvas {canvas_size[0]}x{canvas_size[1]}  median sqrt(area) {median:.1f}",
              fill=TEXT, font=_font())
    draw.text((8, 40 + THUMB_H // 2), creature_id, fill=TEXT, font=_font())

    x = LABEL_W
    for stance, cell in cells:
        sheet.paste(cell, (x, 40), cell)
        draw.text((x, 40 + THUMB_H + 4), stance, fill=TEXT, font=_font())
        x += thumb_w + CELL_PAD

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sheet.convert("RGB").save(out_path)
    print(f"[{creature_id}] wrote {out_path}  ({sheet_w}x{sheet_h})")
    for line in table_lines:
        print(line)

    return [
        (creature_id, stance, stats[stance]["sqrt"] / median if stats[stance] and median > 0 else None)
        for stance in names
    ]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--report", required=True, help="Path to Resources/Enemies (or any folder of <id>/ subfolders)")
    ap.add_argument("--only", nargs="*", default=None, help="Limit to these creature ids")
    ap.add_argument("--out-dir", default=OUT_DIR, help="Where to write <id>.png sheets")
    ap.add_argument("--no-guides", action="store_true",
                    help="Suppress the ground line and per-stance scale caption.")
    args = ap.parse_args()

    if not os.path.isdir(args.report):
        sys.exit(f"Not a directory: {args.report}")

    creature_ids = sorted(
        d for d in os.listdir(args.report)
        if os.path.isdir(os.path.join(args.report, d))
    )
    if args.only:
        creature_ids = [c for c in creature_ids if c in args.only]

    if not creature_ids:
        sys.exit("No creature folders found.")

    findings = []
    for creature_id in creature_ids:
        findings += render_creature(creature_id, os.path.join(args.report, creature_id),
                                    os.path.join(args.out_dir, f"{creature_id}.png"),
                                    draw_guides=not args.no_guides)

    outliers = [(c, s, r) for c, s, r in findings if r is not None and not (0.75 <= r <= 1.30)]
    if outliers:
        print()
        print("SCALE OUTLIERS -- sqrt(area) ratio outside 0.75-1.30 of the actor's own median "
              "(excluding defeated):")
        for c, s, r in outliers:
            print(f"  {c}/{s}: {r:.2f}")


if __name__ == "__main__":
    main()
