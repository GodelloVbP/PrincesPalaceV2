"""
Removes a flat-color background from AI-generated character portraits.

Uses a flood-fill from the image border rather than a blanket color-key, so
dark features that belong to the character (a black nose, deep shadow) don't
get punched transparent just for sharing a similar color with the backdrop —
they're only removed if they're actually *connected* to the border through
matching-color pixels.

Which folders have been processed, and at what tolerance, is recorded in the
PORTRAITS manifest below rather than left to whoever last typed the command
- the same reason slice_actor_sheet.py has ACTORS. A non-default tolerance
is a real per-subject decision (how close the backdrop sits to the
character's own colours) and is exactly the kind of thing that used to
survive only in shell history.

Usage:
    python tools/remove_portrait_backgrounds.py                  # every entry in PORTRAITS
    python tools/remove_portrait_backgrounds.py --only Sheep
    python tools/remove_portrait_backgrounds.py --dir <path> [--tolerance N]   # one-off

CAUTION: this always regenerates Processed/<name>.png fresh from
<source_dir>/<name>.png. If a file in Processed/ was hand-edited or
replaced directly (rather than its un-processed source in <source_dir>),
re-running this over the whole folder will silently overwrite that edit
with a freshly-reprocessed copy of the old source. That hazard used to be
documented here and enforced nowhere; --check now reports which outputs
would change WITHOUT writing, so the batch can be run safely over a folder
someone may have touched.
"""

import argparse
import os
from collections import deque

import numpy as np
from PIL import Image, ImageFilter

DEFAULT_TOLERANCE = 35
FEATHER_RADIUS = 1.2
CROP_PADDING = 10

SOURCE_ROOT = "Assets/_Project/Art/Portraits"

# Every portrait folder that has been processed, and with what. Add an entry
# when a new character's portraits land; the default tolerance is the right
# starting point and only needs changing if the backdrop is close in colour
# to the character themselves.
#
# `reproduces` records whether a fresh run actually rebuilds what is
# committed. Sheep does NOT: the shipped Shawn_neutral.png is 1122x1402
# while this tool produces 1122x1360 from the same source at the documented
# tolerance -- a 42px difference in the crop, so the committed portraits came
# from different settings, a different version of this tool, or a hand edit.
# Nobody recorded which, and the CAUTION above was the only thing standing
# between that and a batch run silently replacing all six.
#
# Left alone deliberately rather than "fixed" by regenerating: the committed
# art is what ships and looks right. Run --check before ever running this
# over Sheep.
#
# AND SHAWN'S NEUTRAL PORTRAIT IS NO LONGER IN Processed/. The dossier loads
# portraits off Resources now (characters.json portraitPath, Resources-relative),
# so that one file moved to Assets/_Project/Resources/Portraits/sheep.png and a
# run of this tool will happily rebuild a Processed/Shawn_neutral.png that
# nothing consumes. The output that ships is the one under Resources; copy it
# there deliberately, do not assume Processed/ is what the game reads.
PORTRAITS = {
    "Sheep": {"tolerance": DEFAULT_TOLERANCE, "reproduces": False},
    "Bear": {"tolerance": DEFAULT_TOLERANCE, "reproduces": True},
    "Owl": {"tolerance": DEFAULT_TOLERANCE, "reproduces": True},
}


def flood_fill_background_mask(rgb: np.ndarray, tolerance: float) -> np.ndarray:
    h, w = rgb.shape[:2]
    bg_color = rgb[1, 1].astype(np.float32)
    diff = rgb.astype(np.float32) - bg_color
    close_to_bg = np.sqrt((diff ** 2).sum(axis=2)) < tolerance

    visited = np.zeros((h, w), dtype=bool)
    is_background = np.zeros((h, w), dtype=bool)
    queue = deque()

    def seed(y, x):
        if close_to_bg[y, x] and not visited[y, x]:
            visited[y, x] = True
            queue.append((y, x))

    for x in range(w):
        seed(0, x)
        seed(h - 1, x)
    for y in range(h):
        seed(y, 0)
        seed(y, w - 1)

    while queue:
        y, x = queue.popleft()
        is_background[y, x] = True
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx] and close_to_bg[ny, nx]:
                visited[ny, nx] = True
                queue.append((ny, nx))

    return is_background


def process_image(path: str, out_path: str, tolerance: float) -> tuple:
    img = Image.open(path).convert("RGBA")
    arr = np.array(img)
    rgb = arr[:, :, :3]

    is_background = flood_fill_background_mask(rgb, tolerance)

    # Binary alpha from connectivity, then a small blur purely to soften the
    # cutout edge — this only smooths a boundary that's already been decided
    # correctly, so it can't leak into unrelated dark interior pixels.
    alpha = np.where(is_background, 0, 255).astype(np.uint8)
    alpha = np.array(Image.fromarray(alpha, "L").filter(ImageFilter.GaussianBlur(FEATHER_RADIUS)))
    arr[:, :, 3] = alpha

    # Crop to the actual character bounds using the *pre-blur* mask, not
    # PIL's getbbox() — that checks all four RGBA channels for non-zero,
    # and our "removed" pixels keep their original (non-zero) RGB, only
    # alpha goes to 0, so getbbox() alone would not crop correctly.
    ys, xs = np.where(~is_background)
    result = Image.fromarray(arr, "RGBA")
    if len(ys) > 0:
        top, bottom = int(ys.min()), int(ys.max())
        left, right = int(xs.min()), int(xs.max())
        h, w = rgb.shape[:2]
        top = max(0, top - CROP_PADDING)
        left = max(0, left - CROP_PADDING)
        bottom = min(h - 1, bottom + CROP_PADDING)
        right = min(w - 1, right + CROP_PADDING)
        result = result.crop((left, top, right + 1, bottom + 1))

    result.save(out_path)
    return result.size


def process_folder(source_dir, tolerance, check_only=False):
    if not os.path.isdir(source_dir):
        raise SystemExit(f"No such portrait folder: {source_dir}")

    out_dir = os.path.join(source_dir, "Processed")
    os.makedirs(out_dir, exist_ok=True)

    changed = []
    for fname in sorted(os.listdir(source_dir)):
        if not fname.lower().endswith(".png"):
            continue
        src = os.path.join(source_dir, fname)
        dst = os.path.join(out_dir, fname)

        if check_only:
            # Render to a scratch path and compare, so an output that a human
            # replaced by hand shows up BEFORE this overwrites it.
            # Keeps the .png extension: PIL picks its encoder from the
            # extension, and anything else raises rather than writing.
            scratch = dst + ".check.png"
            process_image(src, scratch, tolerance)
            existing = open(dst, "rb").read() if os.path.exists(dst) else None
            fresh = open(scratch, "rb").read()
            os.remove(scratch)
            if existing != fresh:
                changed.append(fname)
                print(f"  WOULD CHANGE  {fname}" + ("" if existing else "  (no output yet)"))
            continue

        size = process_image(src, dst, tolerance)
        print(f"  {fname} -> Processed/{fname} ({size[0]}x{size[1]})")

    if check_only:
        print(f"  {len(changed)} of the outputs in {out_dir} would change." if changed
              else f"  every output in {out_dir} already matches a fresh run.")
    return changed


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", help="process just this manifest entry")
    parser.add_argument("--dir", help="one-off: process this folder instead of a manifest entry")
    parser.add_argument("--tolerance", type=float, default=DEFAULT_TOLERANCE)
    parser.add_argument("--check", action="store_true",
                        help="report which outputs a run WOULD change, and write nothing")
    args = parser.parse_args()

    if args.dir:
        process_folder(args.dir, args.tolerance, args.check)
        if not args.check:
            print("NOTE: one-off mode wrote nothing to the PORTRAITS manifest. If this output is "
                  "going to ship, add an entry so the tolerance survives.")
        return

    names = [args.only] if args.only else sorted(PORTRAITS)
    for name in names:
        if name not in PORTRAITS:
            raise SystemExit(f"'{name}' is not in PORTRAITS. Known: {', '.join(sorted(PORTRAITS))}")
        entry = PORTRAITS[name]
        tolerance = entry.get("tolerance", DEFAULT_TOLERANCE)

        # Refuse to overwrite art this tool is not known to reproduce. The
        # committed output would be REPLACED by something visibly different,
        # and the only prior protection was a comment nobody had to read.
        if not entry.get("reproduces", True) and not args.check:
            raise SystemExit(
                f"'{name}' is marked reproduces=False: a fresh run does NOT rebuild the committed "
                f"Processed/ art, so this would overwrite what ships with something different. Run "
                f"--check to see the damage, or --dir to force a one-off if you genuinely mean it.")

        print(f"[{name}] tolerance={tolerance}")
        process_folder(os.path.join(SOURCE_ROOT, name), tolerance, args.check)


if __name__ == "__main__":
    main()
