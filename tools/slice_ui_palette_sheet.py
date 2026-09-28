"""Slice a colour-palette grid sheet into one sprite per cell.

The button and container art comes back from the model as a single sheet --
N colour variants of the same plate laid out on a grid -- because generating
them one at a time loses the shared design. `key_green_screen.py`'s "direct"
mode does not fit: it is one-file-in-one-file-out, and would either key a
sheet that already has real alpha (the containers) or leave a flat white
background untouched (the buttons). This is the grid-cutting step those
sheets still need before `ScreenRegistry` can reference an individual
colour.

Two sheet shapes show up in practice, and both are handled the same way
once each has an alpha channel:

- **Already RGBA** (the container sheets): the model delivered real alpha,
  cell backgrounds read 0 and plate interiors read 255. Cut, don't key.
- **Flat RGB on white** (the button sheet): no alpha exists yet, so a
  near-white pixel is keyed to transparent with the same soft-ramp idea
  `key_green_screen.py` uses for its green band, just measured as distance
  from white instead of green dominance.

Reuses `force_sprite_import` from `key_green_screen.py` rather than a second
copy -- see docs/CODE_STANDARDS.md "Reuse" on promoting the second
copy-paste, except here it's avoided instead of committed.
"""

import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from key_green_screen import force_sprite_import

Image.MAX_IMAGE_PIXELS = None

# How far (per channel, worst-case) a pixel can sit from pure white and still
# count as background, and the ramp down to fully-opaque -- mirrors
# key_green_screen's FULLY_TRANSPARENT_ABOVE / FULLY_OPAQUE_BELOW, just
# measured as whiteness instead of greenness.
FULLY_TRANSPARENT_ABOVE = 8
FULLY_OPAQUE_BELOW = 40

# Alpha level a pixel must clear to count as real content when tightening a
# cell's crop -- same reasoning as sheet_slicing.ALPHA_THRESHOLD.
ALPHA_THRESHOLD = 16

# Transparent breathing room left around each trimmed cell.
PADDING = 12

# Six colour swatches, reading the grid left-to-right, top-to-bottom. Every
# sheet in SHEETS below uses this same layout and palette.
COLORS = ["gold", "crimson", "violet", "blue", "green", "silver"]


def whiteness_distance(r, g, b):
    """How far this pixel is from pure white, as the worst single channel."""
    return max(255 - r, 255 - g, 255 - b)


def key_out_white(image):
    """RGB on flat white -> RGBA, with a soft edge at the plate boundary."""
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    span = FULLY_OPAQUE_BELOW - FULLY_TRANSPARENT_ABOVE

    for y in range(height):
        for x in range(width):
            r, g, b, _ = pixels[x, y]
            distance = whiteness_distance(r, g, b)

            if distance <= FULLY_TRANSPARENT_ABOVE:
                pixels[x, y] = (255, 255, 255, 0)
                continue

            if distance >= FULLY_OPAQUE_BELOW:
                alpha = 255
            else:
                alpha = int(255 * (distance - FULLY_TRANSPARENT_ABOVE) / span)

            pixels[x, y] = (r, g, b, alpha)

    return rgba


def tight_bbox(rgba, x0, y0, x1, y1):
    """Bounding box of real content inside [x0,x1)x[y0,y1), padded."""
    region = rgba.crop((x0, y0, x1, y1))
    box = region.getchannel("A").point(lambda a: 255 if a > ALPHA_THRESHOLD else 0).getbbox()
    if box is None:
        return (x0, y0, x1, y1)

    left, top, right, bottom = box
    return (
        max(x0, x0 + left - PADDING),
        max(y0, y0 + top - PADDING),
        min(x1, x0 + right + PADDING),
        min(y1, y0 + bottom + PADDING),
    )


def slice_sheet(path, cols, rows, out_dir, name_fn):
    image = Image.open(path)
    rgba = image if image.mode == "RGBA" else key_out_white(image)
    width, height = rgba.size
    cell_w, cell_h = width // cols, height // rows

    os.makedirs(out_dir, exist_ok=True)
    print(f"{os.path.basename(path)}  ({width}x{height}, {cols}x{rows} grid)")

    index = 0
    for row in range(rows):
        for col in range(cols):
            x0, y0 = col * cell_w, row * cell_h
            x1 = width if col == cols - 1 else x0 + cell_w
            y1 = height if row == rows - 1 else y0 + cell_h

            box = tight_bbox(rgba, x0, y0, x1, y1)
            cell = rgba.crop(box)

            out_name = name_fn(index)
            out_path = os.path.join(out_dir, out_name)
            cell.save(out_path)
            fixed = force_sprite_import(out_path)
            print(f"  {out_name}  {cell.size[0]}x{cell.size[1]}"
                  f"{'  [import fixed to Sprite]' if fixed else ''}")
            index += 1


BUTTONS_DIR = "Assets/_Project/Art/UI/Buttons"
OUT_DIR = os.path.join(BUTTONS_DIR, "Processed")

# (source file, cols, rows, output name template) -- name template gets the
# colour at that grid index via COLORS.
SHEETS = [
    (os.path.join(BUTTONS_DIR, "button_palette_grid_white.png"), 3, 2, "button_plate_{color}.png"),
    (os.path.join(BUTTONS_DIR, "Container 34.png"), 3, 2, "container_{color}_3x4.png"),
    (os.path.join(BUTTONS_DIR, "container 916.png"), 3, 2, "container_{color}_9x16.png"),
    (os.path.join(BUTTONS_DIR, "banner_flag_34.png"), 3, 2, "banner_flag_{color}_3x4.png"),
    (os.path.join(BUTTONS_DIR, "banner_flag_916.png"), 3, 2, "banner_flag_{color}_9x16.png"),
]


def main():
    for path, cols, rows, template in SHEETS:
        if not os.path.isfile(path):
            print(f"missing: {path} -- skipped")
            continue

        def name_fn(i, template=template):
            return template.format(color=COLORS[i])

        slice_sheet(path, cols, rows, OUT_DIR, name_fn)

    print("\nNext: rebuild the scene so any screen referencing these picks them up.")


if __name__ == "__main__":
    main()
