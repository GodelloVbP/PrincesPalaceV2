"""Cut a green screen off generated art and emit game-ready sprites.

The Hub buildings (and now the Relic icons and Talent Tree kit) are generated
on flat green rather than on transparency, because
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
the same name out, written to a `Processed/` (or `Resources/`) sibling of the
source folder -- the same raw-folder-plus-output-subfolder convention
`Portraits/` and `Backgrounds/` already use for editor-time-loaded sprites.
Most direct-mode kits also skip resampling entirely (`default_delivery_size:
None`): these are baked into the scene once by SceneBuilder's own
`LoadSprite`, never `Resources.Load`ed at runtime, so the source resolution
IS the delivery resolution, and several pieces (the talent tree's branch
segment) are deliberately non-square -- forcing a square resample would
distort them. Ask for exactly the delivery resolution in the prompt instead
(see each kit's README).

A direct-mode kit CAN still set a `default_delivery_size` (the `status` kit
does, at 256) when its output IS `Resources.Load`ed at runtime and therefore
does need a fixed delivery size -- "direct" only ever meant "no grouping into
animation frames", never "no resize". What direct mode skips either way is
the re-crop/re-centre step: a resized direct-mode file keeps its full canvas,
letters and background alike, so a badge glyph stays registered inside its
own 1024/256 frame the way the game's layout code expects.

Any resize -- grouped or direct -- runs on the ALREADY-KEYED image and uses
premultiplied alpha, not a plain RGBA resample: a plain resize blends a
transparent edge pixel's leftover colour (background bleed the keyer didn't
fully suppress) into its opaque neighbours at full weight, which shows up as
a faint dark ring. Premultiplying first makes a fully transparent pixel
contribute pure black at weight zero instead.
"""

import argparse
import io
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
    {
        # Status-effect badge glyphs. Painted objects on flat green like every other kit here, keyed
        # on hue-dominance -- these are small icons with genuine dark detail
        # (outlines, shadowed folds) that brightness-keying would eat.
        # Unlike the other direct-mode kits, this one DOES resize: the output
        # lands under Resources/ and is Resources.Load<Sprite>'d at runtime
        # (FightHudModel), not baked into a scene once, so a fixed delivery
        # size matters. 1024px masters are keyed first, THEN downsampled to
        # 256 -- see the module docstring for why that resize is alpha-aware
        # rather than a plain RGBA resample.
        #
        # force_sprite_import only patches a .meta that already exists. A
        # brand-new PNG has none yet, so the FIRST run after dropping a new
        # file here will report nothing fixed; let Unity import it once, then
        # re-run to get the Sprite/alphaIsTransparency fix-up. The Relics
        # README (Assets/_Project/Art/Items/Relics/README.md) hit the same
        # gotcha on its own first delivery batch.
        "name": "status",
        "source": "Assets/_Project/Art/UI/Status/Raw",
        "output": "Assets/_Project/Resources/Status",
        "grouped": False,
        "delivery_size": {},
        "default_delivery_size": 256,
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
    return g - np.maximum(r, b)


def key_out_green(image):
    """RGB on flat green -> RGBA, with a soft edge and the spill suppressed.

    Output alpha is min(input alpha, computed key alpha). A source delivered
    on flat green with no real alpha channel comes in fully opaque (255)
    everywhere, so the min is a no-op there and this behaves exactly as
    before for every existing kit. A source delivered WITH real alpha (the
    status masters) already carries (0,0,0,0) under its transparent corners;
    those pixels have dominance 0 (black is nobody's green) and would
    otherwise key to alpha 255, painting the transparent corners solid
    black. Taking the min keeps them at 0.

    VECTORISED with numpy (S13's review) -- the per-pixel Python loop this
    replaced took noticeably longer per master the bigger the source got,
    for arithmetic that has no actual per-pixel dependency (every pixel's
    result depends only on that same pixel's own r/g/b/a). Equivalence
    against the old loop is checked directly, not just argued: run both
    against a flat-green master with an outlined shape and a real-alpha
    master and diff the output arrays -- see the S13 commit message for
    that result. Every rule the loop enforced is preserved exactly:
    FULLY_OPAQUE_BELOW/FULLY_TRANSPARENT_ABOVE, the same linear ramp
    (computed the same order -- 255 * numerator, THEN divided, THEN
    truncated -- so no float rounding differs from the original int(...)
    call), the min(input_alpha, keyed_alpha) rule, and spill suppression
    (g = max(r, b) wherever dominance > 0) applied before the fully-
    transparent pixels are forced back to (0, 0, 0, 0) -- the loop's own
    `continue` skipped spill suppression for those pixels, but their RGB is
    thrown away by the zeroing either way, so the order cannot be observed.
    """
    rgba = image.convert("RGBA")
    arr = np.asarray(rgba).astype(np.int32)
    r, g, b, input_alpha = arr[..., 0], arr[..., 1], arr[..., 2], arr[..., 3]

    span = FULLY_TRANSPARENT_ABOVE - FULLY_OPAQUE_BELOW
    dominance = greenness(r, g, b)

    # Linear ramp across the band, so antialiased edges keep a soft falloff
    # instead of turning into a hard jagged cut. Computed for every pixel
    # (including ones the two np.where calls below will overwrite) since
    # that is cheaper than masking the array twice.
    ramp = 255 - np.trunc(255 * (dominance - FULLY_OPAQUE_BELOW) / span).astype(np.int32)
    keyed_alpha = np.where(dominance <= FULLY_OPAQUE_BELOW, 255, ramp)

    alpha = np.minimum(input_alpha, keyed_alpha)

    # Spill suppression. Any green still dominating on a kept pixel is
    # backdrop bounced onto the subject's edge -- pulling it down to its own
    # red/blue removes the lime fringe that would otherwise glow against a
    # near-black nebula.
    suppressed_g = np.where(dominance > 0, np.maximum(r, b), g)

    transparent = dominance >= FULLY_TRANSPARENT_ABOVE
    out_r = np.where(transparent, 0, r)
    out_g = np.where(transparent, 0, suppressed_g)
    out_b = np.where(transparent, 0, b)
    out_a = np.where(transparent, 0, alpha)

    out = np.stack([out_r, out_g, out_b, out_a], axis=-1).astype(np.uint8)
    return Image.fromarray(out, mode="RGBA")


# Moved here VERBATIM from tools/slice_actor_sheet.py (~508, S13's review),
# under its own existing name -- it duplicated this module's own (now
# deleted) resize_alpha_aware almost line for line, and slice_actor_sheet.py
# already imports key_out_green from this module, so this is the one shared
# home rather than two hand-copies drifting apart. slice_actor_sheet.py's
# own byte-identical output (HandAssembledArtTests, the recipe replay) is
# unaffected: it is the exact same code, only relocated, and only imported
# back where it used to be defined.
def resize_premultiplied(image, new_size):
    """LANCZOS resize with alpha premultiplied first, so a transparent
    pixel's own RGB cannot bleed a dark fringe into the resized edge."""
    if image.size == tuple(new_size):
        return image
    arr = np.asarray(image.convert("RGBA"), dtype=np.float32)
    alpha = arr[:, :, 3:4]
    premult_rgb = arr[:, :, :3] * (alpha / 255.0)
    premult_img = Image.fromarray(np.concatenate([premult_rgb, alpha], axis=2).astype(np.uint8), "RGBA")
    resized = np.asarray(premult_img.resize(tuple(new_size), Image.LANCZOS), dtype=np.float32)
    out_alpha = resized[:, :, 3:4]
    safe_alpha = np.where(out_alpha > 0.5, out_alpha, 1.0)
    out_rgb = np.clip(resized[:, :, :3] * 255.0 / safe_alpha, 0, 255)
    out_rgb = np.where(out_alpha > 0.5, out_rgb, 0)
    out = np.concatenate([out_rgb, out_alpha], axis=2).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


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


def process_kit(kit, only=None):
    source_dir = kit["source"]
    if not os.path.isdir(source_dir):
        print(f"[{kit['name']}] no source directory at {source_dir} -- skipped")
        return

    groups = group_sources(source_dir) if kit["grouped"] else direct_sources(source_dir)

    if only is not None:
        groups = {item: paths for item, paths in groups.items() if item == only}
        if not groups:
            print(f"[{kit['name']}] --only {only!r} matched nothing in {source_dir} -- skipped")
            return

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

            # NOT re-cropped or re-centred, even when resized -- see the
            # module docstring: a badge/building's canvas has to stay
            # registered the way the layout code that loads it expects.
            output = resize_premultiplied(keyed, (size, size)) if size else keyed
            out_name = f"f{index}.png" if kit["grouped"] else f"{item}.png"
            out_path = os.path.join(out_dir, out_name)
            output.save(out_path)
            fixed = force_sprite_import(out_path)

            print(f"    {out_name}  {visible:2d}% visible  "
                  f"content box {right - left}x{bottom - top} of {keyed.size[0]}x{keyed.size[1]}"
                  f"{'  [import fixed to Sprite]' if fixed else ''}")


def build_arg_parser():
    parser = argparse.ArgumentParser(
        description="Key green-screen source art into game-ready sprites, "
                     "one kit (or all of KITS) at a time.",
    )
    parser.add_argument(
        "--kit", metavar="NAME",
        help="Process only the kit with this 'name' from KITS instead of all "
             "of them. Required alongside --source/--output.",
    )
    parser.add_argument(
        "--only", metavar="ITEM",
        help="Within the kit(s) processed, key only this one item -- the "
             "group name for a grouped kit, or the filename without its "
             "extension for a direct kit (e.g. 'chilled') -- instead of "
             "every file present. Lets a single file be re-keyed without "
             "touching the rest.",
    )
    parser.add_argument(
        "--source", metavar="PATH",
        help="Override the kit's source directory for this run. Requires "
             "--kit, so it is unambiguous which kit's path is being "
             "redirected. Meant for pointing a test fixture at the keyer "
             "without touching the real Art/ tree.",
    )
    parser.add_argument(
        "--output", metavar="PATH",
        help="Override the kit's output directory for this run. Requires "
             "--kit, same reasoning as --source.",
    )
    return parser


def main():
    args = build_arg_parser().parse_args()

    if (args.source or args.output) and not args.kit:
        sys.exit("--source/--output need --kit to say which kit's path they override")

    kits = KITS
    if args.kit:
        kits = [kit for kit in KITS if kit["name"] == args.kit]
        if not kits:
            known = ", ".join(kit["name"] for kit in KITS)
            sys.exit(f"no kit named {args.kit!r} -- known kits: {known}")

    for kit in kits:
        run_kit = dict(kit)
        if args.source:
            run_kit["source"] = args.source
        if args.output:
            run_kit["output"] = args.output
        process_kit(run_kit, only=args.only)

    print("\nIf any .meta was missing above, Unity has not imported these yet: "
          "let it import once, re-run this, and the import settings will be corrected.")
    print("Next: rebuild the scene so SceneBuilder/ContentBuilder pick up the new files.")


if __name__ == "__main__":
    main()
