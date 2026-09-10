"""Bring the owner's three per-PC leather plates to ONE canvas and ONE aspect.

WHAT ARRIVES. `Art/UI/Plates/Source/{shawn,bjorn,odette}.png` -- one wide
leather strip per playable character, a thin lighter rim tracing a rounded
rectangle, and that character's head EMBOSSED into the field at the right
end. The three were drawn separately and their alpha bounding boxes are three
different shapes (2020x360, 2091x277, 2045x313 -- 5.61:1, 7.55:1, 6.53:1).
The HUD draws all three at the same size, so they have to BE the same shape
before anything else is true of them.

WHY THE FIELD IS CUT NARROWER RATHER THAN EXTENDED TALLER. The obvious move
is to keep the widest plate's width and grow the two short ones vertically by
duplicating a band of field rows just inside the top and bottom rim, on the
theory that the head sits in a middle band and simply rides along. MEASURED,
THAT THEORY IS FALSE for this delivery: the heads span essentially the whole
plate. Bjorn's ears reach within ~5 rows of the top rim and his ruff within
~20 of the bottom, and he needs 96 rows of extra height -- there is nowhere to
insert them that is not through the bear's face.

So the normalisation runs the other way. Heights are matched (every plate is
scaled to the same canvas height), and the two plates that are then too WIDE
lose the surplus out of the empty field between the left cap and the head --
a region measured flat to within ~2 luminance levels across 3%-80% of the
width, with the rim's top and bottom strokes running dead straight through it.
The cut is a seam with a crossfade, so leather grain joins leather grain.

Nothing is scaled non-uniformly, no rim is cut, and every head keeps the size
it was drawn at relative to its own plate's height -- which is the thing a
player actually reads.

The target aspect is the REFERENCE plate's own (Shawn's, the tallest of the
three at 5.61:1). Cutting columns can only make a plate narrower, so the
target has to be the smallest aspect on offer; the reference needs no cut at
all and passes through as a pure resample.

ALSO WRITTEN: `pc_plate_glow.png`, the shared acting-highlight decal. It is
the reference silhouette's edge -- crisp at the outline, falling off outward
into a soft halo -- so the fight HUD can say "this character is acting" with
one tinted Image that follows the plate's ROUNDED corner. Four rectangular
rim Solids cannot: the corner radius measures ~14% of the plate height, which
at the HUD's 452x81 is an 11px arc with a square corner sticking out of it.

USAGE
    py tools/normalize_pc_plates.py
    py tools/normalize_pc_plates.py --recipe Assets/_Project/Art/UI/Plates/recipe.json

The first writes the outputs and the recipe. The second replays the recipe and
must reproduce every output BYTE-IDENTICAL (it says so, and exits 1 if not);
`git status --short Assets/_Project/Resources/Plates` clean after a replay is
the proof the recipe is the real one -- the same bar slice_actor_sheet.py's
recipes are held to (docs/ART_PIPELINE.md, "Reproducibility is recorded").

The recipe also carries the MEASUREMENTS the C# side pins as literals: the
plate aspect, the visible pad, the safe content inset and each plate's head
zone. Re-run and re-paste into PcPlateArt when the art is redelivered.
"""

import argparse
import hashlib
import json
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


SOURCE_DIR = "Assets/_Project/Art/UI/Plates/Source"
OUTPUT_DIR = "Assets/_Project/Resources/Plates"
RECIPE_PATH = "Assets/_Project/Art/UI/Plates/recipe.json"

# (character id, source file). The id is what characters.json's plateArt
# names, so the output filename is the id and not the character's NAME --
# "Shawn" is what a player reads, "sheep" is what content addresses.
PLATES = [
    ("sheep", "shawn.png"),
    ("bear", "bjorn.png"),
    ("owl", "odette.png"),
]

# Whose aspect every plate is brought to. Must be the plate with the SMALLEST
# width/height, since the only shaping move here removes width.
REFERENCE_ID = "sheep"

# The same threshold ContainerArt/ButtonPlateArt's own pads are measured at
# (tools/measure_ui_kit.py's header says why 32).
ALPHA_THRESHOLD = 32

# 2048 is the kit's own maxTextureSize (UiKitImportPostprocessor), so a wider
# canvas would be thrown away on import.
CANVAS_W = 2048

# Where the surplus field is taken from, as a fraction of the cropped width,
# and how wide the crossfade over the seam is. 0.40 sits in the middle of the
# empty run: clear of the left cap's rounded corner and clear of the head,
# which starts at 0.81 on the earliest of the three.
CUT_CENTRE_FRAC = 0.40
CUT_BLEND_PX = 64

# The acting-highlight decal: how far the halo reaches past the silhouette
# (fraction of canvas height), and how hard the falloff is.
GLOW_PAD_FRAC = 0.10
GLOW_BLUR_PASSES = 3

# Head detection: a column counts as head when the vertical spread of its
# luminance rises this far above the field's own.
HEAD_STD_FACTOR = 1.8
HEAD_SMOOTH_PX = 9

# Content inset: a pixel belongs to the safe field when it is fully opaque and
# its luminance is within this of the field median. The rim stroke and the
# corner arc both fail one of those, which is exactly what the inset must
# clear.
FIELD_LUM_BAND = 22.0
FIELD_ALPHA_MIN = 250

RECIPE_VERSION = 1


def sha256_of(path):
    with open(path, "rb") as handle:
        return hashlib.sha256(handle.read()).hexdigest()


def alpha_bbox(alpha, threshold):
    ys, xs = np.where(alpha > threshold)
    if len(xs) == 0:
        raise ValueError("the image is entirely transparent")
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def cut_columns(arr, cut_at, remove, blend):
    """Drop `remove` columns starting at `cut_at`, crossfading `blend` columns
    of the kept left part into the columns that follow the gap.

    The seam is a crossfade rather than a butt join because leather grain is
    stochastic: two flat-but-not-identical patches meeting on a hard line read
    as a line. Over 64 columns of a 2000-wide plate whose field varies by ~2
    luminance levels, nothing survives to be seen."""
    left = arr[:, :cut_at].astype(np.float64)
    right = arr[:, cut_at + remove:].astype(np.float64)

    if blend > 0:
        donor = arr[:, cut_at + remove - blend:cut_at + remove].astype(np.float64)
        t = ((np.arange(blend) + 0.5) / blend).reshape(1, blend, 1)
        left[:, cut_at - blend:] = left[:, cut_at - blend:] * (1.0 - t) + donor * t

    return np.concatenate([left, right], axis=1).round().clip(0, 255).astype(np.uint8)


def edge_pads(alpha, threshold):
    """(left, top, right, bottom) in px -- the same walk tools/measure_ui_kit.py
    does, restated here so the recipe records the pad it actually wrote rather
    than a number from a separate run."""
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


def head_start_column(arr):
    """The first column of the embossed head, walking right from mid-plate.

    The head is tone-on-tone -- it has no alpha and barely any hue of its own
    -- so it is found by VARIANCE: a column of empty field has almost none
    down its length, a column crossing an ear or a muzzle has a lot."""
    lum = arr[:, :, :3].astype(np.float64).mean(axis=2)
    h, w = lum.shape
    band = lum[int(h * 0.12):int(h * 0.88)]
    col_std = band.std(axis=0)
    kernel = np.ones(HEAD_SMOOTH_PX) / HEAD_SMOOTH_PX
    smooth = np.convolve(col_std, kernel, mode="same")
    field = float(np.median(smooth[int(w * 0.05):int(w * 0.6)]))

    threshold = field * HEAD_STD_FACTOR
    for x in range(int(w * 0.5), w):
        if smooth[x] > threshold:
            return x
    return w


def content_inset(arr):
    """The largest axis-aligned rect of plain field, as a pad in px per side.

    Two passes: the top/bottom rim strokes are found down the plate's own
    centre column, then the left/right pads are taken as the WORST field start
    over every row inside that band -- which is what makes the rounded corner
    pay for itself instead of a label clipping it."""
    rgba = arr.astype(np.float64)
    lum = rgba[:, :, :3].mean(axis=2)
    alpha = rgba[:, :, 3]
    h, w = lum.shape

    field_median = float(np.median(lum[int(h * 0.3):int(h * 0.7), int(w * 0.15):int(w * 0.6)]))
    is_field = (alpha >= FIELD_ALPHA_MIN) & (np.abs(lum - field_median) <= FIELD_LUM_BAND)

    centre_band = is_field[:, int(w * 0.2):int(w * 0.5)]
    rows_ok = centre_band.all(axis=1)
    top = int(np.argmax(rows_ok))
    bottom = int(np.argmax(rows_ok[::-1]))

    inner = is_field[top:h - bottom]
    lefts = []
    rights = []
    for y in range(inner.shape[0]):
        cols = np.where(inner[y])[0]
        if len(cols) == 0:
            continue
        lefts.append(int(cols.min()))
        rights.append(w - 1 - int(cols.max()))

    # ROWS WHERE THE HEAD REACHES THE RIM ARE DROPPED, not maxed in. The owl's
    # ear tufts touch the right rim on a handful of rows, and on those the
    # rightmost FIELD pixel is the one left of the whole head -- a pad of
    # 2011px on a 2048px plate. Taken as a max that is the answer for every
    # row; capped, it is what it actually is, a row content never occupies
    # because the head zone already reserves that end.
    cap = int(w * 0.10)
    lefts = [v for v in lefts if v <= cap] or [0]
    rights = [v for v in rights if v <= cap] or [0]

    return max(lefts), top, max(rights), bottom


def box_blur(channel, radius):
    """Separable box blur, run GLOW_BLUR_PASSES times by the caller -- three
    boxes approximate a gaussian closely enough for a halo, and it keeps this
    tool on Pillow+numpy rather than pulling scipy in for one filter."""
    size = radius * 2 + 1
    kernel = np.ones(size) / size
    out = np.apply_along_axis(lambda row: np.convolve(row, kernel, mode="same"), 1, channel)
    return np.apply_along_axis(lambda col: np.convolve(col, kernel, mode="same"), 0, out)


def build_glow(reference_alpha, canvas_w, canvas_h, pad):
    """The acting-highlight decal: white, alpha = the silhouette's own edge
    blurred outward, on a canvas `pad` bigger on every side.

    White so the HUD can tint it to any PC colour with an Image tint (a
    multiply), and edge-only so the part of it that lands BEHIND the opaque
    plate costs nothing."""
    full = np.zeros((canvas_h + pad * 2, canvas_w + pad * 2), dtype=np.float64)
    full[pad:pad + canvas_h, pad:pad + canvas_w] = reference_alpha.astype(np.float64) / 255.0

    blurred = full
    for _ in range(GLOW_BLUR_PASSES):
        blurred = box_blur(blurred, max(1, pad // 2))

    # Outward only: inside the silhouette the plate is opaque anyway, and
    # leaving it in would make the decal a slab rather than a rim.
    halo = np.clip(blurred - full, 0.0, 1.0)
    halo = halo / max(halo.max(), 1e-6)

    # The crisp edge itself: one hard ring at the silhouette so an acting
    # plate has an actual OUTLINE and not only a smudge around it.
    edge = np.clip(full - np.clip(box_blur(full, 2) * 1.0 - 0.02, 0, 1), 0.0, 1.0)
    alpha = np.clip(np.maximum(halo * 0.85, edge), 0.0, 1.0)

    rgba = np.zeros((canvas_h + pad * 2, canvas_w + pad * 2, 4), dtype=np.uint8)
    rgba[:, :, 0:3] = 255
    rgba[:, :, 3] = (alpha * 255.0).round().astype(np.uint8)
    return rgba


def force_sprite_meta(png_path):
    """Give a new PNG a .meta that imports it as a Sprite.

    key_green_screen.py's own force_sprite_import says why this is not
    optional: a PNG Unity has never seen imports as a plain Texture,
    Resources.Load<Sprite> on a plain Texture returns null with no error, and
    the slot renders nothing. Only WRITES a file that is not there -- an
    existing .meta keeps its GUID, which is the whole reason a .meta is
    committed with its asset."""
    meta_path = png_path + ".meta"
    if os.path.exists(meta_path):
        return False

    guid = hashlib.md5(png_path.replace("\\", "/").encode("utf-8")).hexdigest()
    with open(meta_path, "w", newline="\n") as handle:
        handle.write(
            "fileFormatVersion: 2\n"
            "guid: %s\n"
            "TextureImporter:\n"
            "  serializedVersion: 13\n"
            "  textureType: 8\n"
            "  spriteImportMode: 1\n"
            "  alphaIsTransparency: 1\n"
            "  mipmaps:\n"
            "    mipMapMode: 0\n"
            "    enableMipMap: 1\n"
            "  textureSettings:\n"
            "    serializedVersion: 2\n"
            "    filterMode: 2\n"
            "  textureCompression: 2\n"
            "  spritePixelsToUnits: 100\n"
            "  spriteMeshType: 1\n" % guid
        )
    return True


def process(params, verify_against=None):
    reference = None
    entries = []
    crops = {}

    # Pass one: crop every source, so the reference aspect is known before any
    # plate is shaped against it.
    for plate_id, source_name in params["plates"]:
        path = os.path.join(SOURCE_DIR, source_name)
        if not os.path.exists(path):
            sys.exit("missing source: " + path)

        image = Image.open(path).convert("RGBA")
        arr = np.array(image)
        box = alpha_bbox(arr[:, :, 3], params["alphaThreshold"])
        crop = arr[box[1]:box[3], box[0]:box[2]]
        crops[plate_id] = (source_name, path, box, crop)

        if plate_id == params["referenceId"]:
            reference = crop

    if reference is None:
        sys.exit("reference plate '%s' is not in the plate list" % params["referenceId"])

    ref_h, ref_w = reference.shape[:2]
    target_aspect = ref_w / ref_h
    canvas_w = params["canvasW"]
    canvas_h = int(round(canvas_w / target_aspect))

    os.makedirs(OUTPUT_DIR, exist_ok=True)
    written = []
    mismatches = []

    for plate_id, _ in params["plates"]:
        source_name, source_path, box, crop = crops[plate_id]
        h, w = crop.shape[:2]

        want_w = int(round(h * target_aspect))
        remove = w - want_w
        if remove < 0:
            sys.exit(
                "%s is TALLER than the reference (%.4f vs %.4f). The reference has to be the "
                "smallest aspect on offer -- this tool only ever removes width." % (
                    plate_id, w / h, target_aspect))

        cut_at = 0
        blend = 0
        if remove > 0:
            cut_at = int(round(w * params["cutCentreFrac"]))
            blend = min(params["cutBlendPx"], cut_at)
            head = head_start_column(crop)
            if cut_at + remove + blend > head:
                sys.exit(
                    "%s: the %dpx cut at column %d would reach the head (starts at %d). "
                    "Move cutCentreFrac left." % (plate_id, remove, cut_at, head))
            shaped = cut_columns(crop, cut_at, remove, blend)
        else:
            shaped = crop

        scaled = Image.fromarray(shaped, "RGBA").resize((canvas_w, canvas_h), Image.LANCZOS)
        out_path = os.path.join(OUTPUT_DIR, "pc_%s.png" % plate_id)
        scaled.save(out_path, optimize=False)

        final = np.array(scaled)
        pad_l, pad_t, pad_r, pad_b = edge_pads(final[:, :, 3], params["alphaThreshold"])
        ins_l, ins_t, ins_r, ins_b = content_inset(final)
        head_col = head_start_column(final)

        entries.append({
            "id": plate_id,
            "source": source_name,
            "sourceSha256": sha256_of(source_path),
            "cropBox": list(box),
            "cropSize": [w, h],
            "removedColumns": remove,
            "cutAtColumn": cut_at,
            "blendColumns": blend,
            "output": os.path.basename(out_path),
            "outputSha256": sha256_of(out_path),
            "visiblePadPx": [pad_l, pad_t, pad_r, pad_b],
            "contentInsetPx": [ins_l, ins_t, ins_r, ins_b],
            "headStartColumn": head_col,
            "headZoneFrac": round((canvas_w - head_col) / canvas_w, 4),
        })
        written.append(out_path)

    glow_pad = int(round(canvas_h * params["glowPadFrac"]))
    ref_scaled = np.array(
        Image.fromarray(crops[params["referenceId"]][3], "RGBA").resize((canvas_w, canvas_h), Image.LANCZOS))
    glow = build_glow(ref_scaled[:, :, 3], canvas_w, canvas_h, glow_pad)
    glow_path = os.path.join(OUTPUT_DIR, "pc_plate_glow.png")
    Image.fromarray(glow, "RGBA").save(glow_path, optimize=False)
    written.append(glow_path)

    recipe = {
        "version": RECIPE_VERSION,
        "tool": "tools/normalize_pc_plates.py",
        "params": params,
        "canvas": [canvas_w, canvas_h],
        "targetAspect": round(target_aspect, 6),
        "glow": {
            "output": os.path.basename(glow_path),
            "padPx": glow_pad,
            "sha256": sha256_of(glow_path),
        },
        "plates": entries,
    }

    if verify_against is not None:
        for old, new in zip(verify_against.get("plates", []), entries):
            if old.get("outputSha256") != new["outputSha256"]:
                mismatches.append("%s: %s -> %s" % (new["id"], old.get("outputSha256"), new["outputSha256"]))
        old_glow = verify_against.get("glow", {}).get("sha256")
        if old_glow != recipe["glow"]["sha256"]:
            mismatches.append("glow: %s -> %s" % (old_glow, recipe["glow"]["sha256"]))

    for path in written:
        if force_sprite_meta(path):
            print("wrote .meta for " + path)

    return recipe, mismatches


def print_measurements(recipe):
    canvas_w, canvas_h = recipe["canvas"]
    print()
    print("---- C#-PASTEABLE, PcPlateArt ----")
    print("  // canvas %dx%d, aspect %.6f" % (canvas_w, canvas_h, recipe["targetAspect"]))
    print("  internal const float PlateAspect = %.4ff;" % recipe["targetAspect"])
    worst = [0, 0, 0, 0]
    head = 0.0
    for entry in recipe["plates"]:
        pad = entry["visiblePadPx"]
        ins = entry["contentInsetPx"]
        head = max(head, entry["headZoneFrac"])
        for i in range(4):
            worst[i] = max(worst[i], ins[i])
        print("  // %-6s visible pad px L%d T%d R%d B%d   inset px L%d T%d R%d B%d   head %.4f" % (
            entry["id"], pad[0], pad[1], pad[2], pad[3], ins[0], ins[1], ins[2], ins[3],
            entry["headZoneFrac"]))
    print("  // WORST inset per side across the three plates, as a fraction:")
    print("  new ContentInsetFrac(left: %.4ff, right: %.4ff, top: %.4ff, bottom: %.4ff),"
          % (worst[0] / canvas_w, worst[2] / canvas_w, worst[1] / canvas_h, worst[3] / canvas_h))
    print("  internal const float HeadZoneFrac = %.4ff;" % head)
    print()


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--recipe", help="replay this recipe and prove it reproduces byte-identical")
    args = parser.parse_args()

    if args.recipe:
        with open(args.recipe, "r") as handle:
            previous = json.load(handle)
        recipe, mismatches = process(previous["params"], verify_against=previous)
        print_measurements(recipe)
        if mismatches:
            print("REPLAY MISMATCH -- the recipe does not reproduce what is committed:")
            for line in mismatches:
                print("  " + line)
            sys.exit(1)
        print("REPLAY OK: every output is byte-identical to the recipe's record.")
        return

    params = {
        "plates": [list(p) for p in PLATES],
        "referenceId": REFERENCE_ID,
        "alphaThreshold": ALPHA_THRESHOLD,
        "canvasW": CANVAS_W,
        "cutCentreFrac": CUT_CENTRE_FRAC,
        "cutBlendPx": CUT_BLEND_PX,
        "glowPadFrac": GLOW_PAD_FRAC,
    }
    recipe, _ = process(params)

    os.makedirs(os.path.dirname(RECIPE_PATH), exist_ok=True)
    with open(RECIPE_PATH, "w", newline="\n") as handle:
        json.dump(recipe, handle, indent=2, sort_keys=True)
        handle.write("\n")

    print("wrote %d plates + the glow decal to %s" % (len(recipe["plates"]), OUTPUT_DIR))
    print("recipe: " + RECIPE_PATH)
    print_measurements(recipe)


if __name__ == "__main__":
    main()
