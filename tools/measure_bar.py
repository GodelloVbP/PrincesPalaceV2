"""Does the Reckoning's experience track read as a groove, or as a hole?

The bar is drawn by TWO things that multiply: ProceduralSpriteBaker's baked
`bar_track` shading, and the tint ReckoningScreen applies over it. Neither
number means anything alone, and no EditMode test can see their product -- the
audit knows a node's box, not what colour it ends up.

It has now been wrong in both directions. Shipped, the two darknesses compounded
to 0.20x the luminance of the panel the bar sits on, which reads as a hole
punched through. The first fix raised both halves and landed at 0.85x, where the
channel disappears into the panel instead. This measures the product against the
painted panel itself and fails outside the band between those two mistakes.

    python tools/measure_bar.py
"""
import math
import re
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("needs Pillow: python -m pip install pillow")

ROOT = Path(__file__).resolve().parent.parent
TRACK = ROOT / "Assets/_Project/Art/Generated/bar_track.png"
FILL = ROOT / "Assets/_Project/Art/Generated/bar_fill.png"
FRAME = ROOT / "Assets/_Project/Art/UI/Reckoning/Processed/reckoning_frame.png"
RECKONING = ROOT / "Assets/_Project/Scripts/Domain/UiKit/Screens/ReckoningScreen.cs"


def tint(name):
    """Read a tint out of ReckoningScreen rather than keeping a copy of it.

    The first version of this file hardcoded all three, which lasted exactly
    until the fill changed from #FFE9A8 to an amber -- at which point the tool
    would have gone on cheerfully measuring a colour the game no longer used.
    """
    source = RECKONING.read_text(encoding="utf-8", errors="replace")
    match = re.search(name + r'\s*=\s*"#([0-9A-Fa-f]{6})', source)
    if not match:
        sys.exit(f"could not read {name} from ReckoningScreen - has it been renamed?")
    hexa = match.group(1)
    return tuple(int(hexa[i:i + 2], 16) for i in (0, 2, 4))


TINT = tint("BarTrackTint")

# The two tints the SAME baked fill wears: the resting violet for what was
# already earned, and the amber for what this fight paid.
BEFORE_TINT = tint("BarBeforeTint")
EARNED_TINT = tint("BarFillTint")

# The whole reason CharacterReward carries a before as well as an after is that
# the player can see what THIS fight was worth. Both segments therefore have to
# be legible against the channel, and against each other.
SEGMENT_MIN = 1.4

# A recessed channel is DARKER than the surface it is cut into, and not by so
# much that it stops being a surface. Both bounds are failures this project has
# actually shipped rather than round numbers.
BODY_MIN, BODY_MAX = 0.35, 0.75

# The lit lip is the only part that is brighter than the panel. Without it the
# bar is a dark rectangle rather than an edge catching light.
LIP_MIN = 1.15

# Row 0's track sits 84px below the centre of an 896-tall panel.
TRACK_OFFSET_Y = 84.0
PANEL_HEIGHT = 896.0


def luminance(colour):
    return (0.2126 * colour[0] + 0.7152 * colour[1] + 0.0722 * colour[2]) / 255.0


def baked_profile(path, weight_by_alpha=False):
    """The baked strip is uniform across x, so one column is the whole design."""
    image = Image.open(path).convert("RGBA")
    width, height = image.size
    column = []
    for y in range(height):
        pixel = image.getpixel((width // 2, y))
        value = pixel[0] / 255.0
        # The fill feathers its ALPHA at both edges, so a pixel there covers
        # the channel only partly. Ignoring that would credit the fill with
        # brightness it never actually puts on screen.
        if weight_by_alpha:
            value *= pixel[3] / 255.0
        column.append(value)
    # Row 0 of a PNG is the TOP; the baker's y runs 0 at the bottom.
    return column[::-1]


def panel_behind_the_bar():
    frame = Image.open(FRAME).convert("RGBA")
    width, height = frame.size
    x = width // 2
    y = int(height * (0.5 + TRACK_OFFSET_Y / PANEL_HEIGHT))

    patch = [frame.getpixel((x + dx, y + dy))
             for dx in range(-40, 41, 8)
             for dy in range(-6, 7, 3)]
    opaque = [p for p in patch if p[3] > 200]
    if not opaque:
        sys.exit("the sample point on the painted frame is transparent - has the art moved?")

    return tuple(sum(p[i] for p in opaque) // len(opaque) for i in range(3))


def composite(value, tint=TINT):
    return tuple(int(round(value * channel)) for channel in tint)


def main():
    profile = baked_profile(TRACK)
    body = composite(profile[len(profile) // 2])
    lip = composite(max(profile))
    panel = panel_behind_the_bar()

    panel_lum = luminance(panel)
    body_ratio = luminance(body) / panel_lum
    lip_ratio = luminance(lip) / panel_lum

    def hexof(c):
        return "#%02X%02X%02X" % c

    print("panel behind the bar  %s  L %.3f" % (hexof(panel), panel_lum))
    print("channel body          %s  L %.3f  %.2fx the panel" % (hexof(body), luminance(body), body_ratio))
    print("lit lip               %s  L %.3f  %.2fx the panel" % (hexof(lip), luminance(lip), lip_ratio))

    failures = []
    if body_ratio < BODY_MIN:
        failures.append("body is %.2fx the panel (min %.2f) - that reads as a hole, not a groove"
                        % (body_ratio, BODY_MIN))
    if body_ratio > BODY_MAX:
        failures.append("body is %.2fx the panel (max %.2f) - the channel vanishes into the surface"
                        % (body_ratio, BODY_MAX))
    if lip_ratio < LIP_MIN:
        failures.append("lit lip is %.2fx the panel (min %.2f) - the cut edge catches no light"
                        % (lip_ratio, LIP_MIN))

    # The two segments IN the channel. Averaged over the full height rather
    # than sampled at the bloom's core, because what has to be legible is the
    # segment as a whole, not its brightest row.
    fill = baked_profile(FILL, weight_by_alpha=True)
    mean = sum(fill) / len(fill)

    before = composite(mean, BEFORE_TINT)
    earned = composite(mean, EARNED_TINT)
    before_ratio = luminance(before) / luminance(body)
    earned_ratio = luminance(earned) / luminance(before)

    print()
    print("already earned        %s  L %.3f  %.2fx the channel" % (hexof(before), luminance(before), before_ratio))
    print("this fight paid       %s  L %.3f  %.2fx the resting segment" % (hexof(earned), luminance(earned), earned_ratio))

    if before_ratio < SEGMENT_MIN:
        failures.append("the resting segment is %.2fx the channel (min %.2f) - the bar will look empty "
                        "when it is not" % (before_ratio, SEGMENT_MIN))
    if earned_ratio < SEGMENT_MIN:
        failures.append("what this fight paid is %.2fx the resting segment (min %.2f) - the two run "
                        "together and the reward stops being visible" % (earned_ratio, SEGMENT_MIN))

    for failure in failures:
        print("FAIL: " + failure)

    if failures:
        return 1

    print("\nOK - the track reads as a channel, and both segments read inside it.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
