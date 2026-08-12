"""Does the fight stage fit between the HUD below it and the plates above it?

The question no test in this project can answer. UiAudit works on DECLARED
geometry, and a stage slot's declared 320x200 is a placeholder the runtime
replaces with the real sprite canvas -- so the audit is measuring a box that
never appears on screen. This measures the art.

It reads its constants OUT OF THE SOURCE rather than restating them, which is
the whole point: a tool carrying its own copy of Near.Y is a second place for
the number to be wrong, and this file exists because the first place was.

    python tools/measure_stage.py

Exits non-zero if a front-row figure's ground contact is behind an
always-visible panel, or if the tallest actor's head is inside the enemy plates.
"""
import json
import re
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("needs Pillow: python -m pip install pillow")

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "Assets/_Project/Resources"
ANCHORS = ROOT / "Assets/_Project/Scripts/Domain/Stage/FightStageAnchors.cs"
STAGE_LAYOUT = ROOT / "Assets/_Project/Scripts/Domain/Stage/StageLayout.cs"
FIGHT_SCREEN = ROOT / "Assets/_Project/Scripts/Domain/UiKit/Screens/FightScreen.cs"
SUBMENU = ROOT / "Assets/_Project/Scripts/Domain/UiKit/FightSubmenuLayout.cs"

# The contact ring straddles the ground line, hanging this far below it.
RING_DROP = 8.0

# Clearance that reads as clearance rather than as touching.
MARGIN = 12.0

SLOTS = 3


def read(path):
    return path.read_text(encoding="utf-8", errors="replace")


def grab(text, pattern, what):
    match = re.search(pattern, text)
    if not match:
        sys.exit(f"could not find {what} - has the source moved? pattern: {pattern}")
    return float(match.group(1))


def constants():
    a, s, f, m = read(ANCHORS), read(STAGE_LAYOUT), read(FIGHT_SCREEN), read(SUBMENU)

    c = {}
    c["near_x"] = grab(a, r"Near\s*=\s*new UiVec\(\s*(-?[\d.]+)f", "Near.X")
    c["near_y"] = grab(a, r"Near\s*=\s*new UiVec\(\s*-?[\d.]+f\s*,\s*(-?[\d.]+)f", "Near.Y")
    c["far_x"] = grab(a, r"Far\s*=\s*new UiVec\(\s*(-?[\d.]+)f", "Far.X")
    c["far_y"] = grab(a, r"Far\s*=\s*new UiVec\(\s*-?[\d.]+f\s*,\s*(-?[\d.]+)f", "Far.Y")
    c["sprite_scale"] = grab(a, r"SpriteScale\s*=\s*(-?[\d.]+)f", "SpriteScale")

    c["near_scale"] = grab(s, r"NearScale\s*=\s*(-?[\d.]+)f", "NearScale")
    c["far_scale"] = grab(s, r"FarScale\s*=\s*(-?[\d.]+)f", "FarScale")

    c["plate_first_y"] = grab(f, r"PlateFirstY\s*=\s*(-?[\d.]+)f", "PlateFirstY")
    c["plate_h"] = grab(f, r"PlateH\s*=\s*(-?[\d.]+)f", "PlateH")
    c["plate_x"] = grab(f, r"PlateX\s*=\s*(-?[\d.]+)f", "PlateX")
    c["plate_w"] = grab(f, r"PlateW\s*=\s*(-?[\d.]+)f", "PlateW")
    c["verb_x"] = grab(f, r"VerbColumnX\s*=\s*(-?[\d.]+)f", "VerbColumnX")
    c["verb_w"] = grab(f, r"VerbRowW\s*=\s*(-?[\d.]+)f", "VerbRowW")
    c["verb_h"] = grab(f, r"VerbRowH\s*=\s*(-?[\d.]+)f", "VerbRowH")
    c["verb_pitch"] = grab(f, r"VerbPitch\s*=\s*(-?[\d.]+)f", "VerbPitch")

    c["command_bottom"] = grab(m, r"CommandBottom\s*=\s*(-?[\d.]+)f", "CommandBottom")

    # PlatePitch is PlateH + 12 in source; derive rather than re-type it.
    c["plate_pitch"] = c["plate_h"] + 12.0
    return c


def lerp(a, b, t):
    return a + (b - a) * t


def actors():
    """Every actor's worst-case extent above and below its own ground line."""
    manifest = json.loads(read(RES / "StanceManifest.json"))
    out = []

    for actor in manifest["actors"]:
        folder = actor["spritePath"]
        ground = float(actor["groundLine"])
        frames = sorted((RES / folder).rglob("*.png")) if (RES / folder).exists() else []
        if not frames:
            continue

        above = below = 0.0
        width = 0.0
        canvas = None
        for frame in frames:
            with Image.open(frame) as im:
                im = im.convert("RGBA")
                box = im.getbbox()
                if box is None:
                    continue
                w, h = im.size
                canvas = (w, h)
                x0, y0, x1, y1 = box
                # PIL measures y down from the top; the manifest measures the
                # ground line up from the bottom.
                above = max(above, (h - y0) - ground)
                below = max(below, ground - (h - y1))
                width = max(width, x1 - x0)

        out.append({"name": folder, "ground": ground, "canvas": canvas,
                    "above": above, "below": below, "width": width})
    return out


def main():
    c = constants()
    people = actors()
    if not people:
        sys.exit("no actor art found - has Resources moved?")

    tallest = max(people, key=lambda a: a["above"])
    widest = max(people, key=lambda a: a["width"])

    print("Constants, read from source:")
    print(f"  Near ({c['near_x']:.0f}, {c['near_y']:.0f})   Far ({c['far_x']:.0f}, {c['far_y']:.0f})"
          f"   SpriteScale {c['sprite_scale']}")
    print(f"  PlateFirstY {c['plate_first_y']:.0f}   CommandBottom {c['command_bottom']:.0f}")
    print()

    print("Actors, measured off the delivered art (opaque box, not canvas):")
    for a in people:
        print(f"  {a['name']:24s} canvas {a['canvas'][0]:4d}x{a['canvas'][1]:<4d} "
              f"ground {a['ground']:5.1f}  above {a['above']:6.1f}  below {a['below']:5.1f}")
    print(f"  tallest: {tallest['name']} at {tallest['above']:.0f} above its ground line")
    print()

    # ---- the floor: always-visible panels the front row stands in ----------
    #
    # The topmost always-visible verb row. Verb 4 starts inactive, so the
    # highest one actually on screen is index 3.
    verb_top = c["command_bottom"] + 26.0 + 3 * c["verb_pitch"] + c["verb_h"] / 2.0

    # The bottom enemy plate's lower edge: three plates stepping down.
    plate_bottom = c["plate_first_y"] - (SLOTS - 1) * c["plate_pitch"] - c["plate_h"] / 2.0

    print(f"Floor  (topmost always-visible verb row) y {verb_top:7.1f}")
    print(f"Ceiling(bottom enemy plate, lower edge)  y {plate_bottom:7.1f}")
    print()

    failures = []

    print("Per slot:")
    for i in range(SLOTS):
        depth = i / (SLOTS - 1)
        scale = lerp(c["near_scale"], c["far_scale"], depth) * c["sprite_scale"]
        ground = lerp(c["near_y"], c["far_y"], depth)
        x = lerp(c["near_x"], c["far_x"], depth)

        ring = ground - RING_DROP
        head = ground + tallest["above"] * scale
        half = widest["canvas"][0] * scale / 2.0

        print(f"  slot {i}: x {x:6.1f} +/-{half:5.1f}  ground {ground:7.1f}  "
              f"ring {ring:7.1f}  tallest head {head:7.1f}  scale {scale:.3f}")

        # Feet: only the party side sits over the verb column, and only the
        # front slot reaches it -- but check every slot rather than assume.
        if ring < verb_top + MARGIN:
            failures.append(f"slot {i}'s contact ring at {ring:.0f} is inside the verb column "
                            f"(top {verb_top:.0f}, want {MARGIN:.0f} of daylight)")

        # Head: only slots whose art actually reaches the plates in x.
        plate_left = c["plate_x"] - c["plate_w"] / 2.0
        if x + half > plate_left and head > plate_bottom - MARGIN:
            failures.append(f"slot {i}'s tallest head at {head:.0f} is inside the enemy plates "
                            f"(lower edge {plate_bottom:.0f}, want {MARGIN:.0f} of daylight)")

    print()
    for failure in failures:
        print("FAIL: " + failure)

    if failures:
        return 1

    print("OK - the stage clears the HUD below it and the plates above it.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
