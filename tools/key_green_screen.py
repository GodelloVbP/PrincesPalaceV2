"""Cut a green screen off generated art and emit game-ready sprites.

The Hub buildings (and now the Relic icons and Talent Tree kit) are generated
on flat green (see docs/archive/2026-08-01_HUB_ART_PROMPTS.md) rather than on transparency, because
image generators are unreliable at real alpha and the obvious alternative --
keying alpha from BRIGHTNESS, which is how the spell frames are cut -- is
actively wrong for painted objects with dark shadow or a deliberately dark
void that has to stay OPAQUE. Brightness-keying would punch straight through
all of it. Green appears nowhere in this project's palette, so keying on hue
removes the background and touches nothing else.

Everything below is one keyer shared by every "kit" -- a source folder of raw
green-screen generations and the Resources folder its keyed output lands in.
Each kit is independent; a missing source folder just skips that kit instead
of aborting the run, so you can drop art for one kit at a time.

    py tools/key_green_screen.py

## The two grouping modes

**Hub uses "grouped" mode**, unchanged from before this file supported more
than one kit: sources are grouped by name with any trailing `_<number>`
stripped -- `talents_1.png`, `talents_2.png`, `talents_3.png` become one
`talents` building with three animation frames (`f0.png`, `f1.png`, ...),
resampled to that building's DELIVERY_SIZE. That grouping is what lets
HubBuildingAnimator find extra frames to cycle at runtime.

**Every other kit uses "direct" mode**: one source file in, one keyed file of
the same name out, no resampling, written to a `Processed/` sibling of the
source folder -- the same raw-folder-plus-Processed-subfolder convention
`Portraits/` and `Backgrounds/` already use for editor-time-loaded sprites
(these are baked into the scene once by SceneBuilder's own `LoadSprite`,
never `Resources.Load`ed at runtime, so they don't belong under
`Resources/` the way the animated Hub buildings do). Several of these
pieces (the talent tree's branch segment) are deliberately non-square --
forcing a square resample would distort them. Ask for exactly the delivery
resolution in the prompt instead (see each kit's README) and the source IS
the output size.
"""

import io
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

Image.MAX_IMAGE_PIXELS = None

KITS = [
    {
        "name": "hub",
        "source": "Assets/_Project/Art/UI/Hub",
        "output": "Assets/_Project/Resources/Hub",
        "grouped": True,
        # Display size of each building in SceneBuilder, doubled -- the brief
        # asks for 2x so the art stays sharp on a 4K screen. A building not
        # listed here is still keyed, at the default, rather than skipped.
        "delivery_size": {
            "gate": 1120,
            "talents": 720,
            "principality": 720,
            "character_sheet": 680,
            "empty_plot": 680,
        },
        "default_delivery_size": 720,
    },
    {
        "name": "relics",
        "source": "Assets/_Project/Art/Items/Relics",
        "output": "Assets/_Project/Art/Items/Relics/Processed",
        "grouped": False,
        "delivery_size": {},
        "default_delivery_size": None,
    },
    {
        "name": "talent_tree",
        "source": "Assets/_Project/Art/UI/TalentTree",
        "output": "Assets/_Project/Art/UI/TalentTree/Processed",
        "grouped": False,
        "delivery_size": {},
        "default_delivery_size": None,
    },
    {
        # The Reckoning's painted container. Its OWN folder rather than
        # Art/UI/Panels, because that kit is deliberately absent from this list
        # -- those panels are opaque stretched rectangles that need no keying,
        # and adding them here would mint six Processed/ duplicates nothing
        # references.
        "name": "reckoning",
        "source": "Assets/_Project/Art/UI/Reckoning",
        "output": "Assets/_Project/Art/UI/Reckoning/Processed",
        "grouped": False,
        "delivery_size": {},
        "default_delivery_size": None,
    },
    {
        # The painted buttons: the pointed Continue banner and the sleek tab
        # plate. Both are shapes with real alpha at their edges, unlike the
        # rectangles in Panels/.
        "name": "ui_buttons",
        "source": "Assets/_Project/Art/UI/Buttons",
        "output": "Assets/_Project/Art/UI/Buttons/Processed",
        "grouped": False,
        "delivery_size": {},
        "default_delivery_size": None,
    },
    {
        # The rarity burst. White-on-green, so the keyer's greenness measure
        # (g - max(r, b)) reads a white ray as fully opaque and the backdrop as
        # fully transparent -- which is exactly the case it was built for.
        "name": "ui_effects",
        "source": "Assets/_Project/Art/UI/Effects",
        "output": "Assets/_Project/Art/UI/Effects/Processed",
        "grouped": False,
        "delivery_size": {},
        "default_delivery_size": None,
    },
    {
        # The armour stand the character overlay hangs its eight slot cells
        # on. One large figure rather than a sheet, so direct mode.
        "name": "character_overlay",
        "source": "Assets/_Project/Art/UI/CharacterOverlay",
        "output": "Assets/_Project/Art/UI/CharacterOverlay/Processed",
        "grouped": False,
        "delivery_size": {},
        "default_delivery_size": None,
    },
    {
        # Consumable icons. Every other item icon comes off a sliced sheet;
        # potions are authored one at a time because there are only two and
        # they share no visual family with a weapon strip.
        "name": "potions",
        "source": "Assets/_Project/Art/Items/Potions",
        "output": "Assets/_Project/Art/Items/Potions/Processed",
        "grouped": False,
        "delivery_size": {},
        "default_delivery_size": None,
    },
    {
        # The two painted verb-button plaques (ATTACK / SKILL+ITEM tiers) --
        # generated with pointed diamond accents that stick out past a plain
        # rectangle, so unlike the Panels/ kit (opaque stretched rectangles,
        # no keying needed) these need real alpha at the corners or the
        # points would show as a green box.
        "name": "fight_buttons",
        "source": "Assets/_Project/Art/UI/FightButtons",
        "output": "Assets/_Project/Art/UI/FightButtons/Processed",
        "grouped": False,
        "delivery_size": {},
        "default_delivery_size": None,
    },
]

# How green a pixel has to be, relative to its own red and blue, before it
# counts as background at all. Measured as g - max(r, b), so it is a measure
# of how much the green channel DOMINATES rather than of absolute brightness
# -- which is what keeps a warmly-lit brown branch (high green, higher red)
# from being mistaken for the backdrop.
FULLY_TRANSPARENT_ABOVE = 60
FULLY_OPAQUE_BELOW = 18


def greenness(r, g, b):
    return g - max(r, b)


def key_out_green(image):
    """RGB on flat green -> RGBA, with a soft edge and the spill suppressed."""
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    span = FULLY_TRANSPARENT_ABOVE - FULLY_OPAQUE_BELOW

    for y in range(height):
        for x in range(width):
            r, g, b, _ = pixels[x, y]
            dominance = greenness(r, g, b)

            if dominance >= FULLY_TRANSPARENT_ABOVE:
                pixels[x, y] = (0, 0, 0, 0)
                continue

            if dominance <= FULLY_OPAQUE_BELOW:
                alpha = 255
            else:
                # Linear ramp across the band, so antialiased edges keep a
                # soft falloff instead of turning into a hard jagged cut.
                alpha = 255 - int(255 * (dominance - FULLY_OPAQUE_BELOW) / span)

            # Spill suppression. Any green still dominating on a kept pixel is
            # backdrop bounced onto the subject's edge -- pulling it down to
            # its own red/blue removes the lime fringe that would otherwise
            # glow against a near-black nebula.
            if dominance > 0:
                g = max(r, b)

            pixels[x, y] = (r, g, b, alpha)

    return rgba


def content_bounds(rgba):
    box = rgba.getchannel("A").getbbox()
    return box if box else (0, 0, rgba.size[0], rgba.size[1])


def group_sources(source_dir):
    """{building: [path, ...]} keyed by name with any trailing _<n> removed."""
    groups = {}
    for name in sorted(os.listdir(source_dir)):
        if not name.lower().endswith(".png"):
            continue

        stem = os.path.splitext(name)[0]
        parts = stem.rsplit("_", 1)
        building = parts[0] if len(parts) == 2 and parts[1].isdigit() else stem
        groups.setdefault(building, []).append(os.path.join(source_dir, name))

    return groups


def direct_sources(source_dir):
    """{stem: [path]} -- one file, kept under its own name, no grouping."""
    groups = {}
    for name in sorted(os.listdir(source_dir)):
        if not name.lower().endswith(".png"):
            continue
        stem = os.path.splitext(name)[0]
        groups[stem] = [os.path.join(source_dir, name)]
    return groups


def force_sprite_import(png_path):
    """Make Unity import this PNG as a Sprite with real alpha.

    A PNG that Unity has never seen has no .meta, and the one it generates
    defaults to textureType 0 (plain Texture) with alphaIsTransparency off.
    Resources.Load<Sprite> on a plain Texture returns NULL -- no error, no
    warning, the asset just draws nothing. That has now cost two separate
    debugging sessions (the spell frames first, then the Hub buildings), so
    the tool that writes the PNG also writes its import settings.

    Only touches a .meta that already exists and is wrong; a correct one is
    left alone so its GUID survives (CLAUDE.md gotcha #2 -- a regenerated GUID
    orphans every asset that referenced it).
    """
    meta_path = png_path + ".meta"
    if not os.path.exists(meta_path):
        return False

    original = io.open(meta_path, encoding="utf-8").read()
    patched = original
    for key, wanted in (("textureType", "8"), ("spriteMode", "1"), ("alphaIsTransparency", "1")):
        lines = []
        for line in patched.splitlines(True):
            stripped = line.strip()
            if stripped.startswith(key + ":"):
                indent = line[: len(line) - len(line.lstrip())]
                line = f"{indent}{key}: {wanted}\n"
            lines.append(line)
        patched = "".join(lines)

    if patched == original:
        return False

    io.open(meta_path, "w", encoding="utf-8", newline="").write(patched)
    return True


def process_kit(kit):
    source_dir = kit["source"]
    if not os.path.isdir(source_dir):
        print(f"[{kit['name']}] no source directory at {source_dir} -- skipped")
        return

    groups = group_sources(source_dir) if kit["grouped"] else direct_sources(source_dir)
    if not groups:
        print(f"[{kit['name']}] no PNGs in {source_dir} -- skipped")
        return

    print(f"[{kit['name']}]")
    for item, paths in sorted(groups.items()):
        size = kit["delivery_size"].get(item, kit["default_delivery_size"])
        out_dir = kit["output"] if not kit["grouped"] else os.path.join(kit["output"], item)
        os.makedirs(out_dir, exist_ok=True)

        label = f"{size}x{size}" if size else "no resize"
        print(f"  {item}: {len(paths)} frame(s) -> {label}")
        for index, path in enumerate(paths):
            keyed = key_out_green(Image.open(path))

            left, top, right, bottom = content_bounds(keyed)
            coverage = (keyed.getchannel("A").resize((64, 64)).point(lambda a: 255 if a > 8 else 0)
                        .convert("L").getdata())
            visible = sum(1 for a in coverage if a) * 100 // (64 * 64)

            # NOT re-cropped or re-centred -- see the module docstring on why
            # a forced resize is skipped entirely for non-grouped kits.
            output = keyed.resize((size, size), Image.LANCZOS) if size else keyed
            out_name = f"f{index}.png" if kit["grouped"] else f"{item}.png"
            out_path = os.path.join(out_dir, out_name)
            output.save(out_path)
            fixed = force_sprite_import(out_path)

            print(f"    {out_name}  {visible:2d}% visible  "
                  f"content box {right - left}x{bottom - top} of {keyed.size[0]}x{keyed.size[1]}"
                  f"{'  [import fixed to Sprite]' if fixed else ''}")


def main():
    for kit in KITS:
        process_kit(kit)

    print("\nIf any .meta was missing above, Unity has not imported these yet: "
          "let it import once, re-run this, and the import settings will be corrected.")
    print("Next: rebuild the scene so SceneBuilder/ContentBuilder pick up the new files.")


if __name__ == "__main__":
    main()
