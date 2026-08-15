"""Measures the painted clearings in the descent map's backdrop.

The numbers in MapLayout.ClearingColumnX / ClearingRowY are measured off
forest_map_background.png rather than chosen, and until now the scan that
produced them existed only in a commit message. This is that scan, kept, so the
next person to touch the map's geometry can re-run it instead of trusting a
comment -- and so a repaint of the backdrop is checkable against the constants
it would invalidate.

Reads the constants out of the C# rather than restating them, same as
measure_stage.py and measure_scrim.py: a tool that carries its own copy of the
numbers it is checking cannot report a disagreement.

    py tools/measure_clearing.py
"""

import re
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
LAYOUT = ROOT / "Assets/_Project/Scripts/Domain/UiKit/MapLayout.cs"
ART = ROOT / "Assets/_Project/Art/Backgrounds/forest_map_background.png"

# The canvas the backdrop is drawn into. MapScreen sizes each tile 1920x1080.
CANVAS_WIDTH = 1920.0


def floats_of(name, source):
    """Pulls `public static readonly float[] <name> = { a, b, c };` out of the C#."""
    match = re.search(
        r"float\[\]\s+" + name + r"\s*=\s*\{([^}]*)\}", source, re.S)
    if not match:
        sys.exit(f"{LAYOUT.name} has no float[] {name} -- has the layout been refactored?")
    return [float(v.strip().rstrip("f")) for v in match.group(1).split(",") if v.strip()]


def is_clearing(pixel):
    """Sandy/tan floor against dark green canopy."""
    r, g, b = pixel
    return r > 140 and g > 100 and b < 130 and r > b + 45


def extent(pixels, size, x, y):
    """The clearing's width and height through the point (x, y)."""
    width, height = size

    x0 = x
    while x0 > 0 and is_clearing(pixels[x0 - 1, y]):
        x0 -= 1
    x1 = x
    while x1 < width - 1 and is_clearing(pixels[x1 + 1, y]):
        x1 += 1

    y0 = y
    while y0 > 0 and is_clearing(pixels[x, y0 - 1]):
        y0 -= 1
    y1 = y
    while y1 < height - 1 and is_clearing(pixels[x, y1 + 1]):
        y1 += 1

    return (x0, x1, y0, y1)


def main():
    source = LAYOUT.read_text(encoding="utf-8")
    column_x = floats_of("ClearingColumnX", source)
    row_y = floats_of("ClearingRowY", source)

    image = Image.open(ART).convert("RGB")
    pixels = image.load()
    width, height = image.size
    scale = CANVAS_WIDTH / width

    print(f"{ART.name}: {width}x{height} source, drawn at {CANVAS_WIDTH:.0f} wide (scale {scale:.4f})")
    print(f"{LAYOUT.name} says columns {column_x}, rows {row_y}\n")

    worst = 0.0
    for row_index, declared_y in enumerate(row_y):
        for col_index, declared_x in enumerate(column_x):
            # Declared content coordinates back into source pixels. Content y is
            # +up from the canvas centre; image y is +down from the top.
            sx = int(round(declared_x / scale))
            sy = int(round((1080.0 / 2.0 - declared_y) / scale))

            if not (0 <= sx < width and 0 <= sy < height):
                print(f"  column {col_index} row {row_index}: declared position is off the art")
                continue

            if not is_clearing(pixels[sx, sy]):
                print(f"  column {col_index} row {row_index}: NOT ON A CLEARING "
                      f"(source pixel {sx},{sy} is {pixels[sx, sy]})")
                worst = max(worst, 999.0)
                continue

            x0, x1, y0, y1 = extent(pixels, image.size, sx, sy)
            centre_x = (x0 + x1) / 2.0 * scale
            centre_y = 1080.0 / 2.0 - (y0 + y1) / 2.0 * scale
            drift = max(abs(centre_x - declared_x), abs(centre_y - declared_y))
            worst = max(worst, drift)

            print(f"  column {col_index} row {row_index}: "
                  f"{(x1 - x0) * scale:6.1f} x {(y1 - y0) * scale:6.1f} content units, "
                  f"centre ({centre_x:7.1f}, {centre_y:7.1f}), "
                  f"declared ({declared_x:7.1f}, {declared_y:7.1f}), drift {drift:5.1f}")

    print()
    print(f"Worst drift between a declared position and the clearing it names: {worst:.1f} units.")

    # A tile is 100-142 wide, so a room can be a good 20 units off centre and
    # still plainly stand in its clearing. Past that it starts climbing the
    # canopy on one side.
    if worst > 20.0:
        print("TOO FAR. The constants and the painting disagree about where a room stands.")
        return 1

    print("Within tolerance: every declared position lands on the clearing it claims.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
