#!/usr/bin/env python3
"""Turns one PlayMode frame-series capture into things a person can judge --
a stamped contact strip, a real-time GIF, and a before/after comparison.

WHY THIS EXISTS AND rig_clip_qa.py DOES NOT COVER IT. That tool assembles a
RIG's clips and is shaped by that: it walks <root>/<actor>/<stance>/ and takes
its pacing from the actor's own animations.json, because a rig clip's authored
duration is the truth about how fast it should play. A static-pilot capture has
neither shape -- it is one folder per LABEL ("before", "after"), and its pacing
is a fixed sampling interval recorded by the capture itself in timing.json. The
frame discovery is shared rather than rewritten (see the import below); what is
new here is the stamping, the impact marker, and the two-label comparison, none
of which a rig clip has any use for.

Reads StaticPilotStageCaptureTests' output:

    tools/screenshots/runtime/static_pilot/<label>/f0..fN.png
    tools/screenshots/runtime/static_pilot/<label>/timing.json

Writes, per label:

    strip.png      every frame tiled in rows, each stamped with its index and
                   the millisecond it represents; the impact frame is ringed
    playback.gif   the same frames at the interval they were sampled at, which
                   for a capture at BeatSpeedMultiplier 1 is real time

and once both "before" and "after" exist:

    before_vs_after.png   the two strips stacked, frame-aligned

Usage:
    python tools/capture_strip.py
    python tools/capture_strip.py --label before
    python tools/capture_strip.py --root tools/screenshots/runtime/static_pilot
"""

import argparse
import json
import os
import sys

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rig_clip_qa import discover_frames  # reuse, not a second implementation

TOOLS = os.path.dirname(os.path.abspath(__file__))
DEFAULT_ROOT = os.path.join(TOOLS, "screenshots", "runtime", "static_pilot")

# Wide enough that a figure is still readable at a glance, narrow enough that a
# 42-frame beat fits on one screen at six per row.
CELL_WIDTH = 320
COLUMNS = 6

# The GIF is for watching the timing, not for reading detail, so it is halved
# again -- a 42-frame GIF at full capture size runs to tens of megabytes.
GIF_WIDTH = 480

BACKDROP = (24, 20, 28)
STAMP = (235, 230, 225)
IMPACT = (255, 96, 96)


def load_font(size):
    """A real font if this machine has one, PIL's bitmap default otherwise.
    The stamp is the only thing on the strip that has to be READ, so it is
    worth trying; a missing font is not worth failing the run over.
    """
    for candidate in ("arial.ttf", "DejaVuSans.ttf", "segoeui.ttf"):
        try:
            return ImageFont.truetype(candidate, size)
        except (OSError, IOError):
            continue
    return ImageFont.load_default()


def read_timing(label_dir):
    path = os.path.join(label_dir, "timing.json")
    if not os.path.isfile(path):
        return {}
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def build_strip(frames, timing, title):
    """Every frame, tiled, each stamped with its index and its millisecond."""
    interval = float(timing.get("captureIntervalMs", 1000.0 / 30.0))
    impact = timing.get("impactFrame", -1)
    settled = timing.get("settledFrame", -1)

    scale = CELL_WIDTH / float(frames[0].size[0])
    cell_h = int(round(frames[0].size[1] * scale))
    header = 26
    rows = (len(frames) + COLUMNS - 1) // COLUMNS

    strip = Image.new("RGB", (CELL_WIDTH * COLUMNS, header + rows * (cell_h + header)), BACKDROP)
    draw = ImageDraw.Draw(strip)
    font = load_font(15)

    draw.text((8, 5), title, fill=STAMP, font=font)

    for i, frame in enumerate(frames):
        col = i % COLUMNS
        row = i // COLUMNS
        x = col * CELL_WIDTH
        y = header + row * (cell_h + header)

        shrunk = frame.convert("RGB").resize((CELL_WIDTH, cell_h), Image.LANCZOS)
        strip.paste(shrunk, (x, y))

        stamp = "%d  %dms" % (i, round(i * interval))
        colour = STAMP
        if i == impact:
            stamp += "  IMPACT"
            colour = IMPACT
            draw.rectangle([x, y, x + CELL_WIDTH - 1, y + cell_h - 1], outline=IMPACT, width=3)
        elif i == settled:
            stamp += "  SETTLED"

        draw.text((x + 6, y + cell_h + 4), stamp, fill=colour, font=font)

    return strip


def write_gif(frames, timing, out_path):
    interval = float(timing.get("captureIntervalMs", 1000.0 / 30.0))
    scale = GIF_WIDTH / float(frames[0].size[0])
    size = (GIF_WIDTH, int(round(frames[0].size[1] * scale)))

    shots = [f.convert("RGB").resize(size, Image.LANCZOS).convert("P", palette=Image.ADAPTIVE)
             for f in frames]

    # imageio is not installed in this project's toolchain, so the GIF is
    # written by PIL directly. loop=0 means forever, which is what a one-beat
    # loop wants.
    #
    # PER-FRAME DURATIONS THAT ACCUMULATE CORRECTLY, not one repeated value. A
    # GIF stores delays in CENTIseconds, so a 33.33ms sample cannot be written
    # at all: the nearest representable delay is 30ms, and 42 frames of that is
    # a beat played 10% fast -- which is exactly the thing this capture exists
    # to measure. Rounding the RUNNING total instead spends the error frame to
    # frame (30, 30, 40, ...) and lands the last frame on the real elapsed time.
    durations = []
    written = 0.0
    for i in range(len(shots)):
        want = (i + 1) * interval
        step = max(10, int(round((want - written) / 10.0)) * 10)
        durations.append(step)
        written += step

    shots[0].save(out_path, save_all=True, append_images=shots[1:],
                  duration=durations, loop=0, disposal=2)


def process(label_dir, label):
    frames = discover_frames(label_dir)
    if not frames:
        print("  %s: no f0.png -- skipped" % label)
        return None

    timing = read_timing(label_dir)
    impact = timing.get("impactFrame", -1)
    interval = float(timing.get("captureIntervalMs", 1000.0 / 30.0))
    title = "%s | %d frames @ %.1fms | impact f%s | settled f%s" % (
        label, len(frames), interval, impact, timing.get("settledFrame", "?"))

    strip = build_strip(frames, timing, title)
    strip_path = os.path.join(label_dir, "strip.png")
    strip.save(strip_path)

    gif_path = os.path.join(label_dir, "playback.gif")
    write_gif(frames, timing, gif_path)

    print("  %s: %d frames -> %s, %s" % (label, len(frames), strip_path, gif_path))
    return strip


def write_comparison(root, strips):
    """The two labels stacked. Frame-aligned for free: both strips were tiled
    at the same cell size and column count, so row N of one is row N of the
    other as long as the captures are the same length -- which is the point of
    the capture being a fixed frame count rather than a wall-clock duration.
    """
    gap = 12
    width = max(s.size[0] for s in strips.values())
    height = sum(s.size[1] for s in strips.values()) + gap

    sheet = Image.new("RGB", (width, height), BACKDROP)
    y = 0
    for label in ("before", "after"):
        sheet.paste(strips[label], (0, y))
        y += strips[label].size[1] + gap

    out = os.path.join(root, "before_vs_after.png")
    sheet.save(out)
    print("  comparison -> %s" % out)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default=DEFAULT_ROOT,
                    help="Capture tree written by StaticPilotStageCaptureTests. "
                         "Defaults to tools/screenshots/runtime/static_pilot.")
    ap.add_argument("--label", nargs="*", default=None,
                    help="Limit to these labels (default: every subdirectory).")
    args = ap.parse_args()

    if not os.path.isdir(args.root):
        sys.exit("Not a directory: %s\n"
                 "Run tools/graphics_tests.ps1 -Filter "
                 "PrincesPalace.PlayModeTests.StaticPilotStageCaptureTests first -- this "
                 "reads its output, it does not render anything itself." % args.root)

    strips = {}
    for label in sorted(os.listdir(args.root)):
        label_dir = os.path.join(args.root, label)
        if not os.path.isdir(label_dir):
            continue
        if args.label and label not in args.label:
            continue

        strip = process(label_dir, label)
        if strip is not None:
            strips[label] = strip

    if not strips:
        sys.exit("No captured frames found under %s." % args.root)

    if "before" in strips and "after" in strips:
        write_comparison(args.root, strips)


if __name__ == "__main__":
    main()
