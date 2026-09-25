"""Bring each character's separately-sized dialogue expressions onto ONE
shared canvas, modelled on tools/normalize_pc_plates.py's CLI and recipe
shape (read its header first -- this tool copies the --recipe replay
discipline and the force-a-canvas reasoning, not the geometry).

WHAT ARRIVES. `Art/Portraits/<Folder>/Processed/<Name>_<expression>.png` --
one PNG per expression per character, each already background-removed, but
NOT the same size as its sibling expressions: a raised eyebrow or an open
mouth changes the drawn figure's own bounding box, so "happy" and "sad" for
the same character can be, and are, different pixel dimensions.

WHY ONE CANVAS PER CHARACTER MATTERS. The dialogue stage swaps a character's
expression while everything else on screen -- the name plate, the other
speaker, the backdrop -- holds still. If each expression PNG kept its own
native size, the sprite's anchor point would have to compensate for a
different image size on every swap, and any anchor rule that is not "pin one
corner and let the canvas be the fixed thing" reintroduces exactly the jitter
padding onto one canvas exists to remove.

WHY BOTTOM-LEFT AND NOT CENTRED. Anchoring at the bottom-left where a
character's feet/base would be (these are bust crops, so read it as "the
bottom of the crop") is what a figure standing on a fixed floor actually
does: it does not float up and down as its own bounding box changes height
between expressions. MEASURED: bottom-left anchoring already gives silhouette
IoU 0.93-1.00 between a character's own expressions, which is the number
that says the figure barely moves at all under this anchor -- centring would
move it by half of whatever the height delta is.

NEVER RESCALED. Only transparent padding is added; a character's drawn size
does not change between expressions, which is the same "keep what was drawn
at the size it was drawn" rule normalize_pc_plates.py's heads follow.

OUTPUT. `Resources/Portraits/Dialogue/<characterId>/<expression>.png`, one
per available expression, plus `Art/Portraits/dialogue_recipe.json` recording
per-character canvas size, every source's sha256, and every output's
sha256 -- the same reproducibility bar slice_actor_sheet.py's recipes are
held to (docs/ART_PIPELINE.md, "Reproducibility is recorded").

MISSING EXPRESSIONS ARE EXPECTED, NOT AN ERROR. Today Shawn has no neutral
and Bjorn has only neutral; DialogueBust.Fallbacks (Domain/Content) is what
the runtime uses to fall back to neutral when the requested expression's
file was never written. This tool reports what it found and moves on.

NO .META FILES. Unlike normalize_pc_plates.py's Plates output, this does not
hand-write a Sprite .meta -- see PortraitImportPostprocessor's own header:
its path test is `assetPath.Contains("/Resources/Portraits/")`, which covers
this tool's Dialogue subfolder the same as the existing per-character
folders, so Unity's own import already applies the right Sprite settings the
first time it opens the project. Writing a .meta here would just be a second,
competing source of truth for a GUID Unity is about to assign anyway.

USAGE
    py tools/normalize_dialogue_busts.py
    py tools/normalize_dialogue_busts.py --recipe Assets/_Project/Art/Portraits/dialogue_recipe.json

The first writes the outputs and the recipe. The second replays the recipe
and must reproduce every output BYTE-IDENTICAL (it says so, and exits 1 if
not).
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


SOURCE_ROOT = "Assets/_Project/Art/Portraits"
OUTPUT_ROOT = "Assets/_Project/Resources/Portraits/Dialogue"
RECIPE_PATH = "Assets/_Project/Art/Portraits/dialogue_recipe.json"

# Source folder -> (characterId, name prefix on the file). The id is what
# characters.json's dialogueBustPath names (a folder under it), the name
# prefix is what the delivered filename actually starts with -- "Shawn" the
# player reads, "sheep" is what content addresses, same split
# normalize_pc_plates.py's own table makes for plateArt.
CHARACTERS = [
    ("Sheep", "sheep", "Shawn"),
    ("Owl", "owl", "Odette"),
    ("Bear", "bear", "Bjorn"),
]

# The six expressions the dialogue stage knows how to show. Order here is
# also report order, not a ranking.
EXPRESSIONS = ["neutral", "happy", "annoyed", "nervous", "sad", "surprised"]

RECIPE_VERSION = 1


def sha256_of(path):
    with open(path, "rb") as handle:
        return hashlib.sha256(handle.read()).hexdigest()


def find_expression_files(folder, name_prefix):
    """{expression: path} for every Processed/<name_prefix>_<expression>.png
    under `folder`, plus a printed warning for any PNG in that directory that
    does not match a known expression -- typo'd filenames fail loud rather
    than silently not shipping."""
    processed_dir = os.path.join(SOURCE_ROOT, folder, "Processed")
    found = {}

    if not os.path.isdir(processed_dir):
        sys.exit("missing source folder: " + processed_dir)

    prefix = name_prefix + "_"
    for filename in sorted(os.listdir(processed_dir)):
        if not filename.lower().endswith(".png"):
            continue
        if not filename.startswith(prefix):
            print(f"  WARNING: {processed_dir}/{filename} does not start with '{prefix}' -- ignored")
            continue

        stem = filename[len(prefix):-len(".png")]
        if stem not in EXPRESSIONS:
            print(f"  WARNING: {processed_dir}/{filename} names unknown expression '{stem}' -- ignored")
            continue

        found[stem] = os.path.join(processed_dir, filename)

    return found


def process(params, verify_against=None):
    os.makedirs(OUTPUT_ROOT, exist_ok=True)
    characters_out = []
    mismatches = []

    for folder, character_id, name_prefix in params["characters"]:
        print(f"{character_id} ({folder}/{name_prefix}):")
        files = find_expression_files(folder, name_prefix)

        if not files:
            sys.exit(f"{character_id}: no recognised expression files under {folder}/Processed")

        images = {expr: Image.open(path).convert("RGBA") for expr, path in files.items()}
        canvas_w = max(im.width for im in images.values())
        canvas_h = max(im.height for im in images.values())

        out_dir = os.path.join(OUTPUT_ROOT, character_id)
        os.makedirs(out_dir, exist_ok=True)

        written = []
        for expr in EXPRESSIONS:
            if expr not in images:
                continue

            im = images[expr]
            canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
            # BOTTOM-LEFT anchor: x=0 (left), y so the image's bottom edge
            # sits on the canvas's bottom edge. Never rescaled -- pasted at
            # its own native size.
            canvas.paste(im, (0, canvas_h - im.height), im)

            out_path = os.path.join(out_dir, expr + ".png")
            canvas.save(out_path, optimize=False)
            written.append({
                "expression": expr,
                "source": files[expr].replace("\\", "/"),
                "sourceSha256": sha256_of(files[expr]),
                "sourceSize": [im.width, im.height],
                "output": os.path.join(character_id, expr + ".png").replace("\\", "/"),
                "outputSha256": sha256_of(out_path),
            })

        missing = [e for e in EXPRESSIONS if e not in images]
        print(f"  canvas {canvas_w}x{canvas_h}, wrote {len(written)} file(s)"
              + (f", missing: {', '.join(missing)}" if missing else ""))

        characters_out.append({
            "id": character_id,
            "sourceFolder": folder,
            "namePrefix": name_prefix,
            "canvas": [canvas_w, canvas_h],
            "missingExpressions": missing,
            "files": written,
        })

    recipe = {
        "version": RECIPE_VERSION,
        "tool": "tools/normalize_dialogue_busts.py",
        "params": params,
        "expressions": EXPRESSIONS,
        "characters": characters_out,
    }

    if verify_against is not None:
        old_by_id = {c["id"]: c for c in verify_against.get("characters", [])}
        for new_char in characters_out:
            old_char = old_by_id.get(new_char["id"])
            if old_char is None:
                mismatches.append(f"{new_char['id']}: not present in the recipe being replayed")
                continue

            old_files = {f["expression"]: f for f in old_char.get("files", [])}
            new_files = {f["expression"]: f for f in new_char["files"]}
            for expr, new_file in new_files.items():
                old_file = old_files.get(expr)
                if old_file is None or old_file.get("outputSha256") != new_file["outputSha256"]:
                    old_sha = old_file.get("outputSha256") if old_file else "(none)"
                    mismatches.append(f"{new_char['id']}/{expr}: {old_sha} -> {new_file['outputSha256']}")

    return recipe, mismatches


def print_summary(recipe):
    print()
    print("---- summary ----")
    for char in recipe["characters"]:
        w, h = char["canvas"]
        present = [f["expression"] for f in char["files"]]
        print(f"  {char['id']:<6} canvas {w}x{h}  have: {', '.join(present) or '(none)'}"
              + (f"  missing: {', '.join(char['missingExpressions'])}" if char["missingExpressions"] else ""))
    print()


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--recipe", help="replay this recipe and prove it reproduces byte-identical")
    args = parser.parse_args()

    if args.recipe:
        with open(args.recipe, "r") as handle:
            previous = json.load(handle)
        recipe, mismatches = process(previous["params"], verify_against=previous)
        print_summary(recipe)
        if mismatches:
            print("REPLAY MISMATCH -- the recipe does not reproduce what is committed:")
            for line in mismatches:
                print("  " + line)
            sys.exit(1)
        print("REPLAY OK: every output is byte-identical to the recipe's record.")
        return

    params = {"characters": [list(c) for c in CHARACTERS]}
    recipe, _ = process(params)

    os.makedirs(os.path.dirname(RECIPE_PATH), exist_ok=True)
    with open(RECIPE_PATH, "w", newline="\n") as handle:
        json.dump(recipe, handle, indent=2, sort_keys=True)
        handle.write("\n")

    print("recipe: " + RECIPE_PATH)
    print_summary(recipe)


if __name__ == "__main__":
    main()
