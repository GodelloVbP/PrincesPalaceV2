#!/usr/bin/env python3
"""Slice a grid item sheet into one PNG per item level.

The sheets are authored as a grid reading left to right, top to bottom: on a
5x2 sheet the top row is levels 1-5 and the bottom row 6-10, ascending in
quality. Output is `level_1.png` .. `level_N.png` in a folder named after the
sheet, which is what itemsets.json points a piece's `iconSheet` at.

Three things this does that a plain NxM crop does not:

1. KEYS THE BACKGROUND WHEN THERE IS ONE. The sheets arrive in two states and
   it is not visible from a thumbnail which is which: `helmets_str.png` and
   `staff_sheet.png` already carry real alpha, while the four leather sheets
   are 24-bit with an opaque near-white backdrop. Rather than take a flag for
   it, this measures -- a sheet whose pixels are already mostly transparent is
   left alone, and one that is fully opaque is keyed. Keying is a FLOOD FILL
   FROM THE BORDER rather than a brightness threshold, because a brightness
   threshold also punches through the white highlights on a steel buckle,
   which are just as bright as the paper behind it and are not behind
   anything. Reachability from the edge is what distinguishes them.

2. FINDS THE ITEMS RATHER THAN ASSUMING A GRID. Within a row it segments on
   the empty columns between items and only falls back to evenly spaced cuts
   (nudged to the emptiest nearby column, as slice_enemy_sheet.py does) when
   segmenting does not find the expected count. This is not belt-and-braces:
   `staff_sheet.png` is a single row of ten UNEVENLY spaced staves, and an
   even 10-way cut put two of them in one cell and left the neighbouring cell
   empty. Gaps are where the sheet actually says one item ends.

3. A SHARED CANVAS PER SHEET, with every item composited AT ITS NATIVE SIZE and
   centred on its own bounding box. Uniform so ten icons drop into a UI slot at
   one scale; native size rather than each one blown up to fill, because a
   level-10 breastplate being visibly bigger than a level-1 jerkin is the
   sheet's own statement about progression and normalising each cell would
   throw it away.

Usage:
    py tools/slice_item_sheet.py                      # every known sheet
    py tools/slice_item_sheet.py --only staff_sheet
"""

import argparse
import os
import sys
from collections import deque

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
# Cell-cutting geometry, shared with slice_enemy_sheet.py -- see that module's
# own header for why it exists.
from sheet_slicing import (
    ALPHA_THRESHOLD,
    SEARCH_FRACTION,
    PADDING,
    opaque_mask,
    column_weights,
    row_weights,
    best_cut,
)

Image.MAX_IMAGE_PIXELS = None

SOURCE_DIR = "Assets/_Project/Art/Items"

# rows x cols per sheet. Everything is 5x2 except the staffs, which are laid
# out as a single row of ten -- the reading order is the same either way, so
# level numbering does not care, but the CUTS very much do.
SHEETS = {
    "armor_leather_sheet": (2, 5),
    "headwear_leather_sheet": (2, 5),
    "leggings_leather_sheet": (2, 5),
    "gloves_leather_sheet": (2, 5),
    "helmets_str": (2, 5),
    "staff_sheet": (1, 10),
    "boots_silk_sheet": (2, 5),
    "gloves_silk_sheet": (2, 5),
    "rob_bottom_silk_sheet": (2, 5),
    "robe_top_silk_sheet": (2, 5),
    "daggers": (2, 5),
    "longswords": (2, 5),
}

# Longest edge of a delivered icon. The sources are ~300x500 per cell, far more
# than a 96px inventory slot needs; this keeps them comfortably sharp on a 4K
# screen without committing six sheets' worth of full-resolution PNGs.
DELIVERY_LONG_EDGE = 384

# What counts as the paper a sheet was drawn on: bright, and close to
# neutral. Both conditions matter -- brightness alone would swallow the pale
# fur trim on the level-7 coif, and neutrality alone would swallow grey steel.
BACKGROUND_MIN_LUMA = 200
BACKGROUND_MAX_CHROMA = 26

# A sheet is taken to already have alpha if at least this fraction of it is
# transparent. Well above what a stray transparent pixel could produce and well
# below the ~70% empty space a real cut-out sheet shows.
ALREADY_KEYED_FRACTION = 0.15


def looks_like_background(pixel):
    r, g, b = pixel[0], pixel[1], pixel[2]
    return min(r, g, b) >= BACKGROUND_MIN_LUMA and (max(r, g, b) - min(r, g, b)) <= BACKGROUND_MAX_CHROMA


def already_keyed(image):
    px = image.load()
    w, h = image.size
    step = 7
    sampled = transparent = 0
    for y in range(0, h, step):
        for x in range(0, w, step):
            sampled += 1
            if px[x, y][3] <= ALPHA_THRESHOLD:
                transparent += 1
    return sampled and transparent / sampled >= ALREADY_KEYED_FRACTION


def key_background(image):
    """Flood the backdrop away from the sheet's edges.

    Only pixels REACHABLE from the border are cleared, so an enclosed white --
    the glint on a buckle, the pale lining inside a hood -- survives even though
    it is the same colour as the paper.
    """
    w, h = image.size
    px = image.load()
    background = bytearray(w * h)
    queue = deque()

    def push(x, y):
        if 0 <= x < w and 0 <= y < h and not background[y * w + x] and looks_like_background(px[x, y]):
            background[y * w + x] = 1
            queue.append((x, y))

    for x in range(w):
        push(x, 0)
        push(x, h - 1)
    for y in range(h):
        push(0, y)
        push(w - 1, y)

    while queue:
        x, y = queue.popleft()
        push(x + 1, y)
        push(x - 1, y)
        push(x, y + 1)
        push(x, y - 1)

    # The halo pass. A cut against an anti-aliased edge leaves a rim of
    # part-paper pixels that are still bright enough to read as a white
    # outline once the icon sits on a dark panel. Anything still bright and
    # neutral that TOUCHES cleared background goes too, one ring deep.
    rim = []
    for y in range(h):
        row = y * w
        for x in range(w):
            if background[row + x] or not looks_like_background(px[x, y]):
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and background[ny * w + nx]:
                    rim.append(row + x)
                    break
    for index in rim:
        background[index] = 1

    for y in range(h):
        row = y * w
        for x in range(w):
            if background[row + x]:
                r, g, b, _ = px[x, y]
                px[x, y] = (r, g, b, 0)

    return image


# How much ink a column needs before it counts as an item's BODY, and how
# narrow a body may be before it is discarded as a speck the keying left
# behind. Twenty is high on purpose: the last two staves overlap at the leaves,
# and anything below about twenty pixels of ink lets that fringe bridge them
# into a single run of nine items instead of ten.
MIN_COLUMN_INK = 20
MIN_RUN_FRACTION = 0.12


def column_runs(weights, expected, cell_w):
    """Split a row band into its items by the empty columns between them.

    Two passes, and the second is the one that matters. Bodies are found at a
    high ink threshold so overlapping fringes cannot fuse two items; each body
    is then GROWN back out over any column with ink at all, stopping at the
    midpoint of the gap to its neighbour. Without the second pass the high
    threshold would shave the thin outer columns off a wide item -- a glove's
    fingertips are only a few pixels of ink per column and would be cropped
    away by exactly the rule that keeps the staves apart.

    Returns `expected` spans, or None when the sheet does not segment that
    cleanly, which hands the decision back to evenly spaced cuts.
    """
    runs = []
    start = None
    for x, weight in enumerate(weights):
        if weight >= MIN_COLUMN_INK:
            if start is None:
                start = x
        elif start is not None:
            runs.append((start, x))
            start = None
    if start is not None:
        runs.append((start, len(weights)))

    minimum = max(1, int(cell_w * MIN_RUN_FRACTION))
    runs = [r for r in runs if r[1] - r[0] >= minimum]
    if len(runs) != expected:
        return None

    grown = []
    for i, (start, end) in enumerate(runs):
        left_limit = 0 if i == 0 else (runs[i - 1][1] + start) // 2
        right_limit = len(weights) if i == len(runs) - 1 else (end + runs[i + 1][0]) // 2
        while start > left_limit and weights[start - 1] > 0:
            start -= 1
        while end < right_limit and weights[end] > 0:
            end += 1
        grown.append((start, end))

    return grown


def slice_sheet(sheet_path, out_dir, rows, cols, prune=False):
    # Same guard slice_actor_sheet.py carries: re-processing already-keyed
    # output compounds resample loss, and is how a previous art pass went
    # wrong.
    if "/Resources/" in os.path.abspath(sheet_path).replace("\\", "/"):
        sys.exit(f"Refusing to read from a Resources/ path (would compound a previous pass): {sheet_path}")

    image = Image.open(sheet_path).convert("RGBA")

    if already_keyed(image):
        print("  background: already transparent, left alone")
    else:
        print("  background: opaque, keying from the border")
        image = key_background(image)

    mask = opaque_mask(image)
    sheet_w, sheet_h = image.size
    cell_w, cell_h = sheet_w // cols, sheet_h // rows
    x_search = int(cell_w * SEARCH_FRACTION)
    y_search = int(cell_h * SEARCH_FRACTION)

    y_cuts = [0]
    for r in range(1, rows):
        y_cuts.append(best_cut(row_weights(mask, 0, sheet_w, 0, sheet_h), 0, r * cell_h, y_search))
    y_cuts.append(sheet_h)

    cells = []
    for r in range(rows):
        y0, y1 = y_cuts[r], y_cuts[r + 1]
        weights = column_weights(mask, 0, sheet_w, y0, y1)

        spans = column_runs(weights, cols, cell_w)
        if spans is None:
            print(f"  row {r}: did not segment into {cols} items, falling back to even cuts")
            x_cuts = [0]
            for c in range(1, cols):
                x_cuts.append(best_cut(weights, 0, c * cell_w, x_search))
            x_cuts.append(sheet_w)
            spans = [(x_cuts[c], x_cuts[c + 1]) for c in range(cols)]

        for c, (x0, x1) in enumerate(spans):
            box = (x0, y0, x1, y1)
            sub = mask.crop(box).getbbox()
            if sub is None:
                cells.append(None)
                print(f"  r{r}c{c}: EMPTY - no opaque pixels, skipped")
                continue
            cells.append((box[0] + sub[0], box[1] + sub[1], box[0] + sub[2], box[1] + sub[3]))

    present = [b for b in cells if b is not None]
    if not present:
        sys.exit(f"{sheet_path}: no opaque content found at all.")

    canvas_w = max(b[2] - b[0] for b in present) + PADDING * 2
    canvas_h = max(b[3] - b[1] for b in present) + PADDING * 2
    scale = min(1.0, DELIVERY_LONG_EDGE / max(canvas_w, canvas_h))
    final_w, final_h = max(1, round(canvas_w * scale)), max(1, round(canvas_h * scale))

    os.makedirs(out_dir, exist_ok=True)
    written_names = set()
    for i, box in enumerate(cells):
        if box is None:
            continue
        canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        piece = image.crop(box)
        canvas.paste(piece, ((canvas_w - piece.width) // 2, (canvas_h - piece.height) // 2), piece)
        if scale < 1.0:
            canvas = canvas.resize((final_w, final_h), Image.LANCZOS)
        name = f"level_{i + 1}.png"
        canvas.save(os.path.join(out_dir, name))
        written_names.add(name)

    print(f"  wrote {len(written_names)} icons at {final_w}x{final_h} into {out_dir}")
    _report_stray_files(out_dir, written_names, prune)
    return len(written_names)


def _report_stray_files(out_dir, written_names, prune):
    """List (or with --prune, delete) level_*.png this run did not produce.

    A sheet that SHRINKS is the case this exists for: re-slicing a 10-level
    sheet down to 8 leaves level_9/level_10 on disk, still referenced by
    ItemSetEntryResolver's {iconSheet}/level_{N} derivation, and nothing
    says so. Listed rather than deleted by default because removing an
    output also removes its .meta, which is the one operation that touches
    Unity GUIDs (CLAUDE.md gotcha 2).
    """
    stray = sorted(
        f for f in os.listdir(out_dir)
        if f.startswith("level_") and f.endswith(".png") and f not in written_names
    )
    if not stray:
        return

    if not prune:
        print(f"  NOTE: {len(stray)} file(s) here were not produced by this run "
              f"(pass --prune to remove): {', '.join(stray)}")
        return

    for name in stray:
        os.remove(os.path.join(out_dir, name))
        meta = os.path.join(out_dir, name + ".meta")
        if os.path.exists(meta):
            os.remove(meta)
    print(f"  pruned {len(stray)} stale file(s): {', '.join(stray)}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", help="slice just this sheet (name without .png)")
    ap.add_argument("--prune", action="store_true",
                    help="delete level_*.png files this run did not produce (a shrunk sheet leaves stale ones)")
    args = ap.parse_args()

    names = [args.only] if args.only else sorted(SHEETS)
    for name in names:
        if name not in SHEETS:
            sys.exit(f"'{name}' is not a known sheet. Known: {', '.join(sorted(SHEETS))}")
        rows, cols = SHEETS[name]
        sheet_path = os.path.join(SOURCE_DIR, f"{name}.png")
        if not os.path.exists(sheet_path):
            sys.exit(f"No sheet at {sheet_path}")
        print(f"Slicing {name} ({rows}x{cols})")
        slice_sheet(sheet_path, os.path.join(SOURCE_DIR, name), rows, cols, prune=args.prune)


if __name__ == "__main__":
    main()
