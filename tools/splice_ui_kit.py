"""Slice the six-theme UI kit sheets dropped in Buttons/ (transparent grid,
NOT the white palette-grid sheets `slice_ui_palette_sheet.py` already
handles) into per-theme plates.

Those white-background sheets are RGB with no alpha yet and get keyed off
whiteness. These sheets arrive as real RGBA already -- the model painted
actual transparency around each cell -- so there is nothing to key: find the
six connected blobs of non-transparent pixels, crop each tight, and name it
by which of the six house themes (gold/crimson/violet/blue/green/silver) its
border colour matches. Matching by colour rather than by fixed grid position
means a sheet that got laid out in a different order, or with a gap moved,
still comes out right -- position is a layout accident, colour is the only
thing that actually identifies a theme.

    py tools/splice_ui_kit.py
"""

import colorsys
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

try:
    import numpy as np
except ImportError:
    sys.exit("numpy is required: pip install numpy")

try:
    from scipy import ndimage
except ImportError:
    sys.exit("scipy is required: pip install scipy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from key_green_screen import force_sprite_import

Image.MAX_IMAGE_PIXELS = None

BUTTONS_DIR = "Assets/_Project/Art/UI/Buttons"
OUT_DIR = os.path.join(BUTTONS_DIR, "Processed")

# A pixel counts as real content once alpha clears this -- same threshold
# slice_ui_palette_sheet.py uses for its own tight-bbox pass.
ALPHA_THRESHOLD = 16

# Transparent breathing room left around each trimmed cell.
MARGIN = 2

# Hue anchors (degrees, 0-360) for the five saturated house themes, measured
# by sampling the most-saturated pixel along each existing
# Processed/button_plate_<theme>.png's top border. Silver has no reliable
# hue -- it is near-neutral by design -- so it is matched on low saturation
# instead of hue distance.
THEME_HUES = {
    "gold": 35.0,
    "crimson": 3.0,
    "violet": 282.0,
    "blue": 219.0,
    "green": 132.0,
}
SILVER_MAX_SATURATION = 0.18

# Reading order (left-to-right, top-to-bottom) every existing sheet in this
# kit uses -- see slice_ui_palette_sheet.py's own COLORS constant.
COLORS = ["gold", "crimson", "violet", "blue", "green", "silver"]

# (source sheet, output name template) -- output name gets the matched theme.
SHEETS = [
    (os.path.join(BUTTONS_DIR, "button_31.png"), "button_plate_{theme}_3x1.png"),
    (os.path.join(BUTTONS_DIR, "button_51.png"), "button_plate_{theme}_5x1.png"),
    (os.path.join(BUTTONS_DIR, "row_61.png"), "row_plate_{theme}_6x1.png"),
    (os.path.join(BUTTONS_DIR, "container_32.png"), "container_{theme}_3x2.png"),
    (os.path.join(BUTTONS_DIR, "container_21.png"), "container_{theme}_2x1.png"),
]


def find_cells(rgba_array):
    """Connected components of non-transparent pixels, as (y0,y1,x0,x1) boxes.

    Labelled on a 2px-dilated copy of the alpha mask so a single-pixel gap
    in an anti-aliased border stroke (seen in practice on one cell) doesn't
    split one cell into two components; the bounding box itself is still
    read off the real, undilated mask so it stays tight.
    """
    alpha = rgba_array[:, :, 3]
    mask = alpha > ALPHA_THRESHOLD
    structure = np.ones((3, 3), dtype=int)  # 8-connectivity
    dilated = ndimage.binary_dilation(mask, structure=structure, iterations=2)
    labels, count = ndimage.label(dilated, structure=structure)

    boxes = []
    for label_id in range(1, count + 1):
        region = mask & (labels == label_id)
        ys, xs = np.where(region)
        if len(ys) < 500:  # discard speckle noise, not a real cell
            continue
        boxes.append((ys.min(), ys.max() + 1, xs.min(), xs.max() + 1))
    return boxes


def border_hue_and_saturation(rgba_array, box):
    """Median hue among the cell's own border-ring pixels.

    The dark leather-textured interior fills far more of the cell than the
    coloured frame does and carries its own faint (near-identical across
    themes) hue, so sampling the whole cell just measures the interior.
    Erode the cell's alpha mask a few px in from its outer edge and keep
    only the ring that erosion removes -- that ring is the painted border,
    which is where the theme colour actually lives.
    """
    y0, y1, x0, x1 = box
    cell_rgba = rgba_array[y0:y1, x0:x1]
    cell_mask = cell_rgba[:, :, 3] > ALPHA_THRESHOLD
    eroded = ndimage.binary_erosion(cell_mask, iterations=6, border_value=0)
    ring = cell_mask & ~eroded

    ring_pixels = cell_rgba[ring]
    opaque = ring_pixels[ring_pixels[:, 3] > 200]
    if len(opaque) == 0:
        return 0.0, 0.0

    r = opaque[:, 0] / 255.0
    g = opaque[:, 1] / 255.0
    b = opaque[:, 2] / 255.0
    maxc = np.max(opaque[:, :3], axis=1) / 255.0
    minc = np.min(opaque[:, :3], axis=1) / 255.0
    delta = maxc - minc
    sat = np.divide(delta, maxc, out=np.zeros_like(maxc, dtype=float), where=maxc != 0)

    saturated = sat > 0.3
    if not np.any(saturated):
        return 0.0, float(np.median(sat))

    hues = np.array([
        colorsys.rgb_to_hsv(rr, gg, bb)[0] * 360
        for rr, gg, bb in zip(r[saturated], g[saturated], b[saturated])
    ])
    # Circular mean (via unit vectors, not a plain arithmetic mean) so a
    # border colour that straddles the 0/360 wraparound (crimson's near-red
    # hues do) still averages to the right place.
    radians = np.deg2rad(hues)
    mean_angle = np.arctan2(np.mean(np.sin(radians)), np.mean(np.cos(radians)))
    circular_hue = np.degrees(mean_angle) % 360
    return float(circular_hue), float(np.median(sat[saturated]))


def hue_distance(a, b):
    d = abs(a - b) % 360
    return min(d, 360 - d)


def nearest_theme_by_color(hue, saturation):
    """Nearest theme by hue, silver preferred outright on low saturation.

    Used only to VERIFY the grid-position ordering below, not to assign
    themes itself -- ties between warm hues (gold vs. a washed-out crimson)
    are close enough that colour alone occasionally picks the wrong one.
    """
    if saturation <= SILVER_MAX_SATURATION:
        return "silver", 0.0
    candidates = [(theme, hue_distance(hue, anchor)) for theme, anchor in THEME_HUES.items()]
    candidates.sort(key=lambda pair: pair[1])
    return candidates[0]


def grid_order(boxes):
    """Sort cells left-to-right, top-to-bottom -- the ordering every existing
    Processed plate in this kit already uses (see slice_ui_palette_sheet.py's
    COLORS comment). Rows are found by clustering box vertical centres
    (any two centres within half a cell-height count as the same row),
    then each row is sorted left-to-right by horizontal centre.
    """
    if not boxes:
        return []
    heights = [y1 - y0 for y0, y1, x0, x1 in boxes]
    row_tol = (sum(heights) / len(heights)) / 2

    remaining = sorted(boxes, key=lambda b: (b[0] + b[1]) / 2)
    rows = []
    for box in remaining:
        cy = (box[0] + box[1]) / 2
        placed = False
        for row in rows:
            row_cy = sum((b[0] + b[1]) / 2 for b in row) / len(row)
            if abs(cy - row_cy) <= row_tol:
                row.append(box)
                placed = True
                break
        if not placed:
            rows.append([box])

    ordered = []
    for row in rows:
        row.sort(key=lambda b: (b[2] + b[3]) / 2)
        ordered.extend(row)
    return ordered


def measure_inset(rgba_array, box):
    """Border thickness on each side, as a fraction of the cell's own size.

    Same idea as ContainerArt's measurement (see its own comment and the
    a57775f commit message): walk in from each edge until the pixel colour
    stops looking like the border and starts looking like the dark interior,
    using the cell's own centre patch as the interior reference colour.

    ALPHA GATE. A pixel only counts as interior if it is actually opaque.
    Without that gate this returns 0 on every side of an already-cropped
    Processed/ PNG: those carry a transparent halo outside the paint whose
    RGB is (0,0,0), which is within 45 of the dark panel interior, so the
    very first pixel scanned "reads as interior" and the border measures
    zero. That is what produced the implausible .008/.003 raw fractions
    recorded against container_*_3x2 and _2x1 -- they were measurement
    artifacts, not thin borders.
    """
    y0, y1, x0, x1 = box
    h, w = y1 - y0, x1 - x0
    cell = rgba_array[y0:y1, x0:x1].astype(np.int32)

    cy0, cy1 = y0 + int(h * 0.4), y0 + int(h * 0.6)
    cx0, cx1 = x0 + int(w * 0.4), x0 + int(w * 0.6)
    interior = rgba_array[cy0:cy1, cx0:cx1, :3].astype(np.int32).reshape(-1, 3)
    interior_color = interior.mean(axis=0) if len(interior) else np.array([0, 0, 0])

    OPAQUE = 200

    def is_interior(px):
        return px[3] >= OPAQUE and np.abs(px[:3].astype(np.int32) - interior_color).sum() < 45

    def scan(axis_len, sample_fn):
        for i in range(axis_len // 2):
            if is_interior(sample_fn(i)):
                return i
        return axis_len // 2

    mid_y = h // 2
    mid_x = w // 2
    left = scan(w, lambda i: cell[mid_y, i])
    right = scan(w, lambda i: cell[mid_y, w - 1 - i])
    top = scan(h, lambda i: cell[i, mid_x])
    bottom = scan(h, lambda i: cell[h - 1 - i, mid_x])

    return {
        "left": left / w, "right": right / w,
        "top": top / h, "bottom": bottom / h,
        "px": (left, top, right, bottom),
        "size": (w, h),
    }


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    report = []

    for path, template in SHEETS:
        if not os.path.isfile(path):
            print(f"missing: {path} -- skipped")
            continue

        image = Image.open(path).convert("RGBA")
        arr = np.array(image)
        boxes = find_cells(arr)
        print(f"\n{os.path.basename(path)}  ({image.size[0]}x{image.size[1]}, {len(boxes)} cells found)")

        if len(boxes) != 6:
            print(f"  ** expected 6 themed cells, found {len(boxes)} -- check this sheet by hand, "
                  f"naming every cell unknown_N instead of guessing **")
            results = [(box, f"unknown_{i}", *border_hue_and_saturation(arr, box))
                       for i, box in enumerate(boxes)]
        else:
            # Primary ordering: left-to-right, top-to-bottom, same convention
            # every existing Processed plate in this kit already uses --
            # COLORS reading order (gold, crimson, violet / blue, green,
            # silver) is exactly this walk over a 3x2 (or 2x3) grid.
            ordered = grid_order(boxes)
            results = []
            for i, box in enumerate(ordered):
                hue, sat = border_hue_and_saturation(arr, box)
                theme = COLORS[i]
                # Verify: does this cell's own border colour actually look
                # like the theme its grid position implies?
                color_guess, distance = nearest_theme_by_color(hue, sat)
                if color_guess != theme:
                    print(f"  ** WARNING: cell at grid position {i} ({theme} by position) "
                          f"reads as {color_guess} by border colour "
                          f"(hue {hue:.0f} deg, sat {sat:.2f}, distance {distance:.0f}) "
                          f"-- verify by eye **")
                results.append((box, theme, hue, sat))

        for box, theme, hue, sat in results:
            y0, y1, x0, x1 = box
            y0m, y1m = max(0, y0 - MARGIN), min(arr.shape[0], y1 + MARGIN)
            x0m, x1m = max(0, x0 - MARGIN), min(arr.shape[1], x1 + MARGIN)
            cell_img = image.crop((x0m, y0m, x1m, y1m))

            out_name = template.format(theme=theme)
            out_path = os.path.join(OUT_DIR, out_name)
            cell_img.save(out_path)
            fixed = force_sprite_import(out_path)
            w, h = cell_img.size
            aspect = w / h
            print(f"  {out_name}  {w}x{h}  aspect {aspect:.3f}  "
                  f"(hue {hue:.0f} deg, sat {sat:.2f})"
                  f"{'  [import fixed to Sprite]' if fixed else ''}")

            inset = measure_inset(arr, box)
            report.append((out_name, w, h, aspect, inset))

    print("\n--- content inset (fraction of the cropped cell's own size) ---")
    for name, w, h, aspect, inset in report:
        lp, tp, rp, bp = inset["px"]
        print(f"  {name}: {w}x{h} aspect {aspect:.3f}  "
              f"border L{lp} T{tp} R{rp} B{bp}px -> "
              f"frac L{inset['left']:.3f} T{inset['top']:.3f} "
              f"R{inset['right']:.3f} B{inset['bottom']:.3f}")

    print("\nNext: rebuild the scene so any screen referencing these picks them up.")


if __name__ == "__main__":
    main()
