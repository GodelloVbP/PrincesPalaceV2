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

Each stance row also carries a REDRAW RATIO, and the run ends with the whole
roster ranked by it. It is the one number here that answers "why does this look
janky when every frame is fine": silhouette churn per step over how far the
figure's mass actually moves. Around 1 means the drawing changes about as much
as the pose does; 3 means the art is being redrawn rather than animated. A
looping stance should be the LOWEST on the sheet, since the creature is meant
to be standing still, and today all three six-frame idles are near the top. See
churn_stats for what it is measured against and why the bar differs by whether
the stance loops. --fail-over turns it into a gate; it is off by default,
because a gate that shipped switched on would refuse every build.

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


# ---------------------------------------------------------------------------
# HOW MUCH OF A STANCE IS MOTION AND HOW MUCH IS REDRAWING.
#
# THE REPORT THIS EXISTS FOR, in the words it arrived in: "still janky
# animation wise". The Forest Troll's idle steps six drawings on a cosine with
# its feet planted to within a pixel and its centre held to within one -- every
# rule the runtime enforces, enforced -- and it still read as wrong. The
# numbers say why, and neither of them is a number this tool measured before:
#
#   the figure's own mass moves 18px, 3.9% of its height, across the whole
#   cycle, while 13% of its silhouette is replaced between EACH pair of
#   adjacent frames -- 25% of it across the head and its mushrooms alone,
#   where the mushroom count itself changes frame to frame.
#
# So the creature barely moves and is heavily redrawn. That is not a fault in
# any one frame, which is why a per-frame check could never find it, and it is
# not visible in an onion skin either -- six overlaid silhouettes that differ
# in their fringes look much like six that differ in their pose.
#
# THE RATIO IS THE MEASUREMENT, not the churn. A stance is ALLOWED to redraw
# itself heavily; a lunge or a collapse has to. What it is not allowed to do is
# redraw itself heavily while standing still. Ranked over the whole roster the
# ratio sorts exactly the way the eye does -- the Rat's attack, a real lunge
# across the stage, scores 0.7; the Beetle's sealed shell, genuinely still and
# consistently drawn, scores 1.1; every six-frame idle in the game scores near
# 3.
#
# BOTTOM-CENTRE ALIGNED FIRST, because that is what the stage does
# (FightController.StageVisuals sizes each slot to the sprite's own canvas and
# stands it on its ground line). Measuring in raw canvas coordinates would
# report a creature as moving when all that moved was the crop -- which is
# precisely the state the Forest Warden's frames are in, 35 distinct canvas
# sizes across 36 frames.

# Where the bands are cut, as fractions of the figure's own height from the
# top. The feet band is the one worth having: a planted foot that gains and
# loses toes is the single most legible thing in a bad idle, and it is buried
# in a whole-figure average because the feet are a small part of the area.
BANDS = [
    ("head", 0.00, 0.25),
    ("torso", 0.25, 0.78),
    ("feet", 0.78, 1.00),
]

# A LOOPING STANCE IS HELD TO A TIGHTER BAR THAN A SWING, and the asymmetry is
# the whole rule -- the same one StanceTiming.Steady draws for the same reason.
# An idle that redraws twice as much as it moves is doing something other than
# breathing; an attack that redraws twice as much as it moves is an attack.
LOOPING_STANCES = {"idle"}
LOOPING_BARS = (1.6, 2.5)
ONE_SHOT_BARS = (2.5, 4.0)


def aligned_masks(frames):
    """Every frame's alpha mask on one canvas, stood on a common floor.

    Bottom-centre, matching the stage: slot sized to the sprite's canvas,
    pivot (0.5, 0). Frames that already share a canvas are unaffected.
    """
    width = max(f.width for f in frames)
    height = max(f.height for f in frames)

    masks = []
    for frame in frames:
        canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        canvas.paste(frame, ((width - frame.width) // 2, height - frame.height))
        masks.append(np.array(canvas)[:, :, 3] > ALPHA_THRESHOLD)

    return masks


def churn_stats(frames):
    """How much of this stance is motion and how much is redrawing.

    None for a single-frame stance, which cannot churn by construction.
    """
    if len(frames) < 2:
        return None

    masks = aligned_masks(frames)
    if any(not m.any() for m in masks):
        return None

    # CHURN IS PER STEP, not across the whole stance. The question is what the
    # eye sees at each drawing change, and averaging over the pairs answers it;
    # comparing first against last would report a stance that returns to its
    # opening pose as perfectly clean.
    steps = [
        float((masks[i] ^ masks[i + 1]).sum()) / max(int((masks[i] | masks[i + 1]).sum()), 1)
        for i in range(len(masks) - 1)
    ]
    churn = float(np.mean(steps))

    # TRAVEL IS THE CENTROID'S, not the bounding box's. A box grows when a
    # branch is redrawn a little wider, and counting that as travel would
    # credit the redrawing with being motion -- letting a stance excuse its own
    # churn. Mass only moves when mass moves.
    centres, heights = [], []
    for mask in masks:
        ys, xs = np.nonzero(mask)
        centres.append((float(xs.mean()), float(ys.mean())))
        heights.append(float(ys.max() - ys.min() + 1))

    height = float(np.mean(heights))
    travel = max(
        math.hypot(a[0] - b[0], a[1] - b[1])
        for a in centres
        for b in centres
    )
    travel_pct = 100.0 * travel / height if height > 0 else 0.0

    # FLOORED, and the floor is doing real work. A stance whose mass is
    # genuinely motionless divides churn by nearly nothing, and an infinite
    # ratio is not more informative than a large one -- 0.15% of a figure's
    # height is a third of a pixel on the tallest actor on the roster, which is
    # below anything anybody could see.
    ratio = (100.0 * churn) / max(travel_pct, 0.15)

    bands = {}
    top = min(int(np.nonzero(m)[0].min()) for m in masks)
    bottom = max(int(np.nonzero(m)[0].max()) for m in masks)
    span = max(bottom - top, 1)
    for name, start, end in BANDS:
        a, b = top + int(span * start), top + int(span * end) + 1
        changed = sum(int((masks[i][a:b] ^ masks[i + 1][a:b]).sum()) for i in range(len(masks) - 1))
        union = sum(int((masks[i][a:b] | masks[i + 1][a:b]).sum()) for i in range(len(masks) - 1))
        bands[name] = 100.0 * changed / max(union, 1)

    return {
        "churn": 100.0 * churn,
        "travel_px": travel,
        "travel": travel_pct,
        "ratio": ratio,
        "bands": bands,
    }


def redraw_colour(stance, ratio):
    green, amber = LOOPING_BARS if stance in LOOPING_STANCES else ONE_SHOT_BARS
    if ratio < green:
        return GREEN
    return AMBER if ratio < amber else RED


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


def render_creature(creature_id, creature_dir, out_path, draw_guides=True):
    stances = discover_creature(creature_dir)
    if not stances:
        print(f"[{creature_id}] no stance art found in {creature_dir} -- skipped")
        return []

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

    churn = {stance: churn_stats(stances[stance]) for stance in names}

    for stance in names:
        frames = stances[stance]
        stats = all_stats[stance]
        cells = []
        for i, (frame, stat) in enumerate(zip(frames, stats)):
            ratio = (stat["sqrt"] / median) if (stat is not None and median > 0) else None
            cells.append(draw_frame_cell(canvas_size, THUMB_H, frame, stat, ratio, ground_y_full, draw_guides))
            if stat is not None:
                table_lines.append(
                    f"  {stance:10s} f{i}  box={stat['box'][2]-stat['box'][0]}x{stat['box'][3]-stat['box'][1]}"
                    f"  sqrt={stat['sqrt']:.1f}  ratio={ratio:.3f}"
                )
        ch = churn[stance]
        if ch is not None:
            table_lines.append(
                f"  {stance:10s} ..  redraw={ch['ratio']:.1f}  churn={ch['churn']:.1f}%/step"
                f"  travel={ch['travel']:.1f}%  "
                + "  ".join(f"{n}={ch['bands'][n]:.0f}%" for n, _, _ in BANDS)
            )

        skin = onion_skin(frames, canvas_size)
        cells.append(draw_frame_cell(canvas_size, THUMB_H, skin, None, None, ground_y_full, draw_guides))
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

        # Beside the onion skin on purpose: the skin is the picture of this
        # number, and a reader who does not believe the ratio can look straight
        # at the smear that produced it.
        ch = churn[stance]
        if ch is not None:
            draw.text((8, y + THUMB_H // 2 + 26),
                      f"redraw {ch['ratio']:.1f}\nchurn {ch['churn']:.0f}%\nmoves {ch['travel']:.0f}%\n"
                      f"feet {ch['bands']['feet']:.0f}%",
                      fill=redraw_colour(stance, ch["ratio"]), font=_font())

        x = LABEL_W
        for i, cell in enumerate(cells):
            sheet.paste(cell, (x, y), cell)
            x += thumb_w + CELL_PAD
        y += row_h

    if all_skin is not None:
        draw.text((8, y + THUMB_H // 2), "all stances\n(onion skin,\nexcl. defeated)", fill=TEXT, font=_font())
        cell = draw_frame_cell(canvas_size, THUMB_H, all_skin, None, None, ground_y_full, draw_guides)
        sheet.paste(cell, (LABEL_W, y), cell)

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sheet.convert("RGB").save(out_path)
    print(f"[{creature_id}] wrote {out_path}  ({sheet_w}x{sheet_h})")
    for line in table_lines:
        print(line)

    return [
        (creature_id, stance, churn[stance])
        for stance in names
        if churn[stance] is not None
    ]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--report", required=True, help="Path to Resources/Enemies (or any folder of <id>/ subfolders)")
    ap.add_argument("--only", nargs="*", default=None, help="Limit to these creature ids")
    ap.add_argument("--out-dir", default=OUT_DIR,
                    help="Where to write <id>.png sheets. Defaults to tools/screenshots/actor_qa -- "
                         "override this for a --report directory that is NOT Resources/Enemies "
                         "(e.g. tools/screenshots/rigs/Enemies), or a rig capture with the same "
                         "creature id would silently overwrite that creature's frame-sheet sheet.")
    ap.add_argument("--no-guides", action="store_true",
                    help="Suppress the ground line and per-frame scale caption. Both are measured "
                         "against PADDING/the roster median, which assume a sliced frame-sheet "
                         "canvas -- meaningless (not wrong, just noise) on camera-rendered rig "
                         "frames, whose canvas is a fixed orthographic capture size. The redraw "
                         "ratio and onion skin are unaffected either way.")
    ap.add_argument("--fail-over", type=float, default=None, metavar="RATIO",
                    help="Exit non-zero if any stance's redraw ratio reaches RATIO. "
                         "Off by default: this is a report, and every six-frame idle on the "
                         "roster is currently over 2.5, so a gate that shipped switched on "
                         "would refuse every build until the art was re-cut.")
    ap.add_argument("--fail-bars", action="store_true",
                    help="Exit non-zero if any stance is at or over its OWN class's red bar "
                         "-- LOOPING_BARS[1] for a looping stance, ONE_SHOT_BARS[1] otherwise "
                         "-- rather than one fixed RATIO for every stance. This is the form "
                         "meant to be wired into a commit gate once the roster passes it: see "
                         "docs/STANCE_SHEET_SPEC.md section 9. Off by default for the same "
                         "reason --fail-over is.")
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

    # ---- the roster ranked by how much of it is redrawing --------------------
    #
    # ONE TABLE ACROSS EVERY CREATURE, which is the half a per-creature sheet
    # cannot show. A stance's ratio means little alone and a great deal beside
    # its neighbours: the point is not that the Forest Warden's idle scores 3.3,
    # it is that a real lunge scores 0.7 and a genuinely still pose scores 1.1,
    # so 3.3 is three times what standing still costs.
    if findings:
        print()
        print("REDRAW RATIO -- silhouette churn per step, over how far the figure's mass moves.")
        print("Around 1 means the drawing changes about as much as the pose does. Higher means")
        print("the art is being redrawn rather than animated; a LOOPING stance should be lowest")
        print("of all, because the creature is supposed to be standing still.")
        print()
        print(f"  {'actor':<15}{'stance':<15}{'redraw':>7}{'churn':>8}{'moves':>8}   bands (head/torso/feet)")

        for creature_id, stance, ch in sorted(findings, key=lambda f: -f[2]["ratio"]):
            green, amber = LOOPING_BARS if stance in LOOPING_STANCES else ONE_SHOT_BARS
            flag = "     " if ch["ratio"] < green else ("  ~  " if ch["ratio"] < amber else "  !  ")
            bands = "/".join(f"{ch['bands'][n]:.0f}%" for n, _, _ in BANDS)
            loop = " (loops)" if stance in LOOPING_STANCES else ""
            print(f"{flag}{creature_id:<15}{stance:<15}{ch['ratio']:>7.1f}"
                  f"{ch['churn']:>7.0f}%{ch['travel']:>7.1f}%   {bands}{loop}")

        if args.fail_over is not None:
            over = [f for f in findings if f[2]["ratio"] >= args.fail_over]
            if over:
                print()
                sys.exit(f"{len(over)} stance(s) at or over a redraw ratio of {args.fail_over}: "
                         + ", ".join(f"{c}/{s}" for c, s, _ in over))

        # PER-STANCE BARS, not one fixed number -- a looping stance and a
        # one-shot are held to different classes (LOOPING_BARS vs
        # ONE_SHOT_BARS) everywhere else in this file, and a gate that ignored
        # that split would either pass a boiling idle at the one-shot bar or
        # fail a legitimately busy attack at the idle one.
        if args.fail_bars:
            over = [
                (c, s, ch) for c, s, ch in findings
                if ch["ratio"] >= (LOOPING_BARS[1] if s in LOOPING_STANCES else ONE_SHOT_BARS[1])
            ]
            if over:
                print()
                sys.exit(f"{len(over)} stance(s) at or over their class's red bar: "
                         + ", ".join(f"{c}/{s} ({ch['ratio']:.1f})" for c, s, ch in over))


if __name__ == "__main__":
    main()
