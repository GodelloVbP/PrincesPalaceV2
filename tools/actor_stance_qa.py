#!/usr/bin/env python3
"""Visual QA contact sheets for ACTOR stance/frame art (enemies and party).

Why this exists: ScreenshotTool runs Unity in Edit Mode and explicitly cannot
capture live combat state (see its own header comment) -- there is no way to
SEE an animated actor stance play out before it ships. Every other check in
the art pipeline is stdout text. This renders one contact sheet per creature
straight from whatever PNGs are currently on disk under
Resources/Enemies/<id>/, so a scale or ground-alignment regression is visible
at a glance instead of buried in a table of numbers.

Standalone: reads existing PNGs, no dependency on slice_actor_sheet.py's
CREATURES manifest or any slicing step. That is deliberate -- it lets you
render the CURRENTLY COMMITTED art as a "before" picture, make a change, and
re-run the exact same command as "after".

Each creature's row-of-stances image shows, per frame: the canvas bounds (a
thin white rect), the canvas's own ground line (red, fixed at canvas_h -
PADDING -- the same PADDING slice_actor_sheet.py pastes with, so a frame
whose content doesn't reach it is genuinely floating, not a display
artifact), that frame's own alpha centroid (cyan tick -- where THIS frame's
mass actually sits, so drift across frames in one stance is visible without
guessing), and a sqrt(opaque-area)/creature-median caption. Area, not bbox
height, is the pose-invariant proxy for "how big is this creature drawn" --
a crouching pose has a shorter bbox than a rearing one even when the artist
drew both at the same scale (see docs/ART_PIPELINE.md and the scale-guard
test in EnemyStageTests.cs). The caption is colour-coded so an outlier reads
as red without doing the division yourself.

The rightmost cell of each stance row is an ONION SKIN: every frame of that
stance composited at alpha=1/n. A creature that stays one size and stands in
one place produces a single clean silhouette; ballooning shows as concentric
outlines and drift shows as a smear. This is the single highest-value pixel
in the whole artifact. A bottom band repeats the trick across every
non-prone stance of the creature at once.

Usage:
    python tools/actor_stance_qa.py --report Assets/_Project/Resources/Enemies
    python tools/actor_stance_qa.py --report Assets/_Project/Resources/Characters
    python tools/actor_stance_qa.py --report Assets/_Project/Resources/Enemies --only golem rat
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
# exists to catch (see AllStancesOfOneEnemy_ShareOneCanvasSize's canvas-only
# guarantee vs. the scale guarantee this adds on top).
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
    """(box, count, sqrt(count), centroid_x) over the whole alpha mask.

    Mirrors slice_actor_sheet.opaque_mask/alpha_centroid_x's own definitions
    (same ALPHA_THRESHOLD, same "weighted by opaque column count" centroid)
    but vectorised with numpy since this walks every frame of every stance
    of every creature, not one sheet at a time.
    """
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
        "mask": mask,
    }


def discover_creature(creature_dir):
    """{stance_name: [PIL.Image, ...]} -- a flat "<stance>.png" is a
    single-frame list; a "<stance>/f0.png, f1.png, ..." folder is probed in
    order until a gap, matching StanceAnimationLibrary's own convention so
    this tool sees exactly what the game would load.
    """
    stances = {}
    for entry in sorted(os.listdir(creature_dir)):
        full = os.path.join(creature_dir, entry)
        if entry.lower().endswith(".png") and os.path.isfile(full):
            stance = entry[:-4]
            stances[stance] = [Image.open(full).convert("RGBA")]
        elif os.path.isdir(full):
            frames = []
            i = 0
            while True:
                frame_path = os.path.join(full, f"f{i}.png")
                if not os.path.isfile(frame_path):
                    break
                frames.append(Image.open(frame_path).convert("RGBA"))
                i += 1
            if frames:
                stances[entry] = frames
    return stances


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


def onion_skin(frames, canvas_size):
    """Every frame composited at alpha=1/n onto one transparent canvas.

    A creature that stays one size and stands in one place produces a
    single clean silhouette here; anything else shows as concentric
    outlines (ballooning) or a smear (drift) -- see module docstring.
    """
    w, h = canvas_size
    accum = np.zeros((h, w, 4), dtype=np.float32)
    weight = 1.0 / max(1, len(frames))
    for im in frames:
        arr = np.array(im, dtype=np.float32)
        fh, fw = arr.shape[:2]
        if (fw, fh) != (w, h):
            im = im.resize((w, h), Image.LANCZOS)
            arr = np.array(im, dtype=np.float32)
        alpha = arr[:, :, 3:4] / 255.0 * weight
        accum[:, :, :3] += arr[:, :, :3] * alpha
        accum[:, :, 3:4] += alpha * 255.0
    accum[:, :, :3] = np.clip(accum[:, :, :3], 0, 255)
    accum[:, :, 3] = np.clip(accum[:, :, 3], 0, 255)
    return Image.fromarray(accum.astype(np.uint8), "RGBA")


def draw_frame_cell(base_size, thumb_h, im, stat, ratio, ground_y_full, draw_guides=True):
    """One thumbnail: the frame scaled to thumb_h tall, with canvas outline,
    ground line, centroid tick and a colour-coded scale caption.
    """
    w, h = base_size
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


def render_creature(creature_id, creature_dir, out_path):
    stances = discover_creature(creature_dir)
    if not stances:
        print(f"[{creature_id}] no stance art found in {creature_dir} -- skipped")
        return None

    all_stats = {}
    canvas_size = None
    for stance, frames in stances.items():
        for frame in frames:
            if canvas_size is None:
                canvas_size = frame.size
            elif frame.size != canvas_size:
                print(f"  WARNING: {stance} frame is {frame.size}, expected {canvas_size} -- canvas mismatch")
        all_stats[stance] = [opaque_stats(f) for f in frames]

    sqrt_values = [
        st["sqrt"]
        for stance, stats in all_stats.items()
        if stance not in PRONE_STANCES
        for st in stats
        if st is not None
    ]
    median = float(np.median(sqrt_values)) if sqrt_values else 1.0
    ground_y_full = canvas_size[1] - PADDING

    names = ordered_stance_names(stances)
    rows = []
    table_lines = [f"{creature_id}  canvas={canvas_size[0]}x{canvas_size[1]}  median sqrt(area)={median:.1f}"]

    for stance in names:
        frames = stances[stance]
        stats = all_stats[stance]
        cells = []
        for i, (frame, stat) in enumerate(zip(frames, stats)):
            ratio = (stat["sqrt"] / median) if (stat is not None and median > 0) else None
            cells.append(draw_frame_cell(canvas_size, THUMB_H, frame, stat, ratio, ground_y_full))
            if stat is not None:
                table_lines.append(
                    f"  {stance:10s} f{i}  box={stat['box'][2]-stat['box'][0]}x{stat['box'][3]-stat['box'][1]}"
                    f"  sqrt={stat['sqrt']:.1f}  ratio={ratio:.3f}"
                )
        skin = onion_skin(frames, canvas_size)
        cells.append(draw_frame_cell(canvas_size, THUMB_H, skin, None, None, ground_y_full, draw_guides=True))
        rows.append((stance, cells))

    non_prone_frames = [f for stance, frames in stances.items() if stance not in PRONE_STANCES for f in frames]
    all_skin = onion_skin(non_prone_frames, canvas_size) if non_prone_frames else None

    thumb_w = max(c.width for _, cells in rows for c in cells)
    row_h = THUMB_H + 22 + CELL_PAD
    max_cols = max(len(cells) for _, cells in rows)
    sheet_w = LABEL_W + max_cols * (thumb_w + CELL_PAD)
    sheet_h = 40 + len(rows) * row_h + (THUMB_H + 40 if all_skin else 0)

    sheet = Image.new("RGBA", (sheet_w, sheet_h), BG)
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 8), f"{creature_id}  canvas {canvas_size[0]}x{canvas_size[1]}  median sqrt(area) {median:.1f}", fill=TEXT, font=_font())

    y = 40
    for stance, cells in rows:
        draw.text((8, y + THUMB_H // 2), f"{stance}\n({len(cells) - 1} frame(s))", fill=TEXT, font=_font())
        x = LABEL_W
        for i, cell in enumerate(cells):
            sheet.paste(cell, (x, y), cell)
            x += thumb_w + CELL_PAD
        y += row_h

    if all_skin is not None:
        draw.text((8, y + THUMB_H // 2), "all stances\n(onion skin,\nexcl. defeated)", fill=TEXT, font=_font())
        cell = draw_frame_cell(canvas_size, THUMB_H, all_skin, None, None, ground_y_full)
        sheet.paste(cell, (LABEL_W, y), cell)

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sheet.convert("RGB").save(out_path)
    print(f"[{creature_id}] wrote {out_path}  ({sheet_w}x{sheet_h})")
    for line in table_lines:
        print(line)
    return out_path


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--report", required=True, help="Path to Resources/Enemies (or any folder of <id>/ subfolders)")
    ap.add_argument("--only", nargs="*", default=None, help="Limit to these creature ids")
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

    for creature_id in creature_ids:
        render_creature(creature_id, os.path.join(args.report, creature_id), os.path.join(OUT_DIR, f"{creature_id}.png"))


if __name__ == "__main__":
    main()
