"""Measure the transparent halo the UI kit's PNGs carry on every edge.

WHY THIS EXISTS. Every Processed/ PNG's declared rect (the size a screen
passes to Ui.Container/Ui.Button) is NOT where the painted art actually
stops -- each file carries a few pixels of transparent padding on all four
sides, left over from the sheet-splicing pass. A screen that flushes two
rects' BOTTOM EDGES against each other (FightSubmenuLayout.CommandBottom,
the party plate) therefore reads as misaligned even though the rects agree
exactly, because the visible paint stops short of the rect on both sides by
a different amount. This script measures that pad so ContainerArt/
ButtonPlateArt can carry it as a VisiblePad fraction, the same way they
already carry the content Inset.

HOW A PAD IS MEASURED. For each edge, walk in from that edge one row/column
at a time; a row/column counts as "still padding" while EVERY pixel in it is
at or below the alpha threshold. The distance walked before the first pixel
clears the threshold is the pad, in pixels. Three thresholds are printed
(8/32/128) because the kit's edges are not a hard cutoff -- a feathered edge
can leave single-digit alpha several pixels past the actual paint, the same
reason PngAlpha.LowestOpaqueRow does not default to zero. 32 is the
threshold ContainerArt/ButtonPlateArt actually use (decided on the main
tree): a uniform halo means the count agrees with 8 and 128 almost exactly,
which is itself part of what makes 32 safe to build on.

GROUPING. Files are grouped by (stem, shape suffix) -- the theme token
removed from the filename -- so "button_plate_gold_5x1.png" and
"button_plate_violet_5x1.png" land in the same group as the six themes of
one shape. A file with no recognisable theme token (tab_plate.png,
continue_arrow.png) is not part of any six-theme kit and is skipped, named,
rather than silently guessed into a group of one.

USAGE
    py tools/measure_ui_kit.py

Prints, per file, the four edges' pad in px and as a fraction of that file's
own width (left/right) or height (top/bottom) at all three thresholds; per
group, whether the six themes agree within 1px at threshold 32 (exits 1
naming the file if not); and a C#-pasteable block of the threshold-32
numbers, averaged across each group's six themes, in the shape
ContainerArt.Specs/ButtonPlateArt already read literals in.

A GROUP THAT FAILS THE AGREEMENT CHECK GETS NO PASTEABLE NUMBER, only the
reason. Averaging six themes that disagree by 9px produces a figure that is
wrong for all six rather than right for the middle one, and printing it
under a heading reading "C#-PASTEABLE" -- with the warning further down the
output -- asks to be read bottom-up.
"""

import os
import re
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

try:
    import numpy as np
except ImportError:
    sys.exit("numpy is required: pip install numpy")

PROCESSED_DIR = "Assets/_Project/Art/UI/Buttons/Processed"

# The per-PC fight-HUD plates (tools/normalize_pc_plates.py). NOT a six-theme
# kit -- one file per character, so the agreement check below has nothing to
# compare and each file is its own group. Measured here anyway because
# UiKitVisiblePadTests pins these pads exactly like the kit's, and a second
# script with a second copy of edge_pads is how the two would drift.
PLATE_DIR = "Assets/_Project/Resources/Plates"
PLATE_LABEL = "PcPlateArt: the per-PC fight-HUD plate"

THEMES = ["blue", "crimson", "gold", "green", "silver", "violet"]
THRESHOLDS = (8, 32, 128)
PIN_THRESHOLD = 32
AGREEMENT_BAND_PX = 1

# filename -> (stem, theme, suffix), suffix includes its leading underscore
# ("" for the legacy button plate, which carries no suffix at all).
NAME_RE = re.compile(
    r"^(?P<stem>[a-z_]+?)_(?P<theme>" + "|".join(THEMES) + r")(?P<suffix>_[a-z0-9]+)?\.png$"
)

# (stem, suffix) -> a human label and the C# target this group fills in.
GROUP_LABELS = {
    ("button_plate", ""): "ButtonPlateArt: ButtonPlateShape.Legacy",
    ("button_plate", "_3x1"): "ButtonPlateArt: ButtonPlateShape.ThreeByOne",
    ("button_plate", "_5x1"): "ButtonPlateArt: ButtonPlateShape.FiveByOne",
    ("row_plate", "_6x1"): "ButtonPlateArt: ButtonPlateShape.Row6x1",
    ("container", "_3x4"): "ContainerArt: (ContainerKind.Container, ContainerRatio.ThreeByFour)",
    ("container", "_9x16"): "ContainerArt: (ContainerKind.Container, ContainerRatio.NineBySixteen)",
    ("container", "_3x2"): "ContainerArt: (ContainerKind.Container, ContainerRatio.ThreeByTwo)",
    ("container", "_2x1"): "ContainerArt: (ContainerKind.Container, ContainerRatio.TwoByOne)",
    ("container", "_5x1"): "ContainerArt: (ContainerKind.Container, ContainerRatio.FiveByOne)",
    ("banner_flag", "_3x4"): "ContainerArt: (ContainerKind.FlagBanner, ContainerRatio.ThreeByFour)",
    ("banner_flag", "_9x16"): "ContainerArt: (ContainerKind.FlagBanner, ContainerRatio.NineBySixteen)",
}


def edge_pads(alpha, threshold):
    """(left, top, right, bottom) pad in px: how many rows/cols from that
    edge are ENTIRELY at or below `threshold` before the first pixel clears
    it."""
    h, w = alpha.shape
    col_max = alpha.max(axis=0)
    row_max = alpha.max(axis=1)

    left = 0
    while left < w and col_max[left] <= threshold:
        left += 1
    right = 0
    while right < w and col_max[w - 1 - right] <= threshold:
        right += 1
    top = 0
    while top < h and row_max[top] <= threshold:
        top += 1
    bottom = 0
    while bottom < h and row_max[h - 1 - bottom] <= threshold:
        bottom += 1

    return left, top, right, bottom


def measure_pc_plates():
    """The per-PC plates, each its own group. Returns C#-pasteable lines.

    Kept in this script rather than only in normalize_pc_plates.py because
    this is the script ContainerArt/ButtonPlateArt's own pins are re-measured
    with, and PcPlateArt pins the same kind of number -- one walk of one edge
    algorithm, so the plates cannot end up measured by a second definition of
    "where the paint stops"."""
    if not os.path.isdir(PLATE_DIR):
        print(f"== {PLATE_DIR} is not there -- no PC plates to measure (run tools/normalize_pc_plates.py)")
        print()
        return []

    files = sorted(f for f in os.listdir(PLATE_DIR) if f.endswith(".png"))
    if not files:
        return []

    print(f"== pc plates  ->  {PLATE_LABEL}  ({len(files)} files)")
    lines = [f"  // {PLATE_LABEL}"]
    for name in files:
        path = os.path.join(PLATE_DIR, name)
        image = Image.open(path).convert("RGBA")
        alpha = np.array(image)[:, :, 3]
        h, w = alpha.shape
        for t in THRESHOLDS:
            l, top, r, b = edge_pads(alpha, t)
            print(
                f"  {name:24s} {w}x{h}  thr{t:>3d}  "
                f"L{l:>3d}({l / w:.4f}) T{top:>3d}({top / h:.4f}) "
                f"R{r:>3d}({r / w:.4f}) B{b:>3d}({b / h:.4f})"
            )
        l, top, r, b = edge_pads(alpha, PIN_THRESHOLD)
        lines.append(
            f"  // {name}: left {l / w:.4f}  top {top / h:.4f}  right {r / w:.4f}  bottom {b / h:.4f}")
    print()
    return ["\n".join(lines)]


def main():
    if not os.path.isdir(PROCESSED_DIR):
        sys.exit(f"no such directory: {PROCESSED_DIR}")

    files = sorted(f for f in os.listdir(PROCESSED_DIR) if f.endswith(".png"))
    if not files:
        sys.exit(f"no PNGs found under {PROCESSED_DIR}")

    groups = {}  # (stem, suffix) -> list of (theme, path, w, h, {threshold: (l,t,r,b)})
    skipped = []

    for name in files:
        m = NAME_RE.match(name)
        if not m:
            skipped.append(name)
            continue

        stem = m.group("stem")
        theme = m.group("theme")
        suffix = m.group("suffix") or ""

        path = os.path.join(PROCESSED_DIR, name)
        image = Image.open(path).convert("RGBA")
        arr = np.array(image)
        alpha = arr[:, :, 3]
        h, w = alpha.shape

        pads = {t: edge_pads(alpha, t) for t in THRESHOLDS}
        groups.setdefault((stem, suffix), []).append((theme, name, w, h, pads))

    if skipped:
        print("SKIPPED (no theme token, not part of a six-theme kit):")
        for name in skipped:
            print(f"  {name}")
        print()

    failures = []
    csharp_blocks = []

    for key in sorted(groups):
        stem, suffix = key
        entries = sorted(groups[key], key=lambda e: e[0])
        label = GROUP_LABELS.get(key, f"UNKNOWN GROUP {stem}{suffix}")
        print(f"== {stem}{suffix or ' (no suffix)'}  ->  {label}  ({len(entries)} themes)")

        for theme, name, w, h, pads in entries:
            for t in THRESHOLDS:
                l, top, r, b = pads[t]
                print(
                    f"  {name:32s} {w}x{h}  thr{t:>3d}  "
                    f"L{l:>3d}({l / w:.4f}) T{top:>3d}({top / h:.4f}) "
                    f"R{r:>3d}({r / w:.4f}) B{b:>3d}({b / h:.4f})"
                )

        # Agreement check at the pin threshold: every theme's px pad, per
        # edge, within AGREEMENT_BAND_PX of every other theme's.
        pin_pads = [(theme, name, pads[PIN_THRESHOLD]) for theme, name, w, h, pads in entries]
        disagreed = []
        for edge_index, edge_name in enumerate(("left", "top", "right", "bottom")):
            values = [p[2][edge_index] for p in pin_pads]
            spread = max(values) - min(values)
            if spread > AGREEMENT_BAND_PX:
                worst = max(pin_pads, key=lambda p: p[2][edge_index])
                disagreed.append(f"{edge_name} (spread {spread}px, worst {worst[1]} at {worst[2][edge_index]}px)")
                failures.append(
                    f"{stem}{suffix} edge={edge_name}: spread {spread}px across themes at threshold "
                    f"{PIN_THRESHOLD} (> {AGREEMENT_BAND_PX}px band) -- worst file {worst[1]} "
                    f"({worst[2][edge_index]}px). Re-measure by hand before pinning this group."
                )

        # NO PASTEABLE LINE FOR A GROUP THAT JUST FAILED ITS OWN CHECK. The
        # number would be a mean over a spread the tool has this second
        # declared untrustworthy -- for container_3x4's 9px left spread that
        # mean is wrong for every one of the six themes, not right for the
        # middle one -- and it was being printed under a heading that says
        # "C#-PASTEABLE" with the reason not to paste it further down the
        # output. Anything that has to be read bottom-up to be read correctly
        # gets read wrong.
        if disagreed:
            csharp_blocks.append(
                f"  // {label}\n"
                f"  // REFUSED: the six themes disagree on {', '.join(disagreed)} at threshold "
                f"{PIN_THRESHOLD}.\n"
                f"  // An average across a spread that wide is wrong for every theme in it. Fix the\n"
                f"  // art or re-measure by hand; see the FAILURES list below."
            )
            print()
            continue

        # C# paste: the fraction average across the group's six themes, at
        # the pin threshold, one fraction per edge.
        avg_l = sum(pads[PIN_THRESHOLD][0] / w for _, _, w, h, pads in entries) / len(entries)
        avg_t = sum(pads[PIN_THRESHOLD][1] / h for _, _, w, h, pads in entries) / len(entries)
        avg_r = sum(pads[PIN_THRESHOLD][2] / w for _, _, w, h, pads in entries) / len(entries)
        avg_b = sum(pads[PIN_THRESHOLD][3] / h for _, _, w, h, pads in entries) / len(entries)
        csharp_blocks.append(
            f"  // {label}\n"
            f"  // left {avg_l:.4f}  top {avg_t:.4f}  right {avg_r:.4f}  bottom {avg_b:.4f}\n"
            f"  new ContentInsetFrac(left: {avg_l:.4f}f, right: {avg_r:.4f}f, "
            f"top: {avg_t:.4f}f, bottom: {avg_b:.4f}f),"
        )
        print()

    csharp_blocks.extend(measure_pc_plates())

    print(f"---- C#-PASTEABLE, threshold {PIN_THRESHOLD}, fraction averaged across each group's six themes ----")
    print()
    for block in csharp_blocks:
        print(block)
    print()

    if failures:
        print("FAILURES -- themes disagree by more than the allowed band:")
        for f in failures:
            print(f"  {f}")
        sys.exit(1)

    print("All groups agree within the 1px band at threshold 32.")


if __name__ == "__main__":
    main()
