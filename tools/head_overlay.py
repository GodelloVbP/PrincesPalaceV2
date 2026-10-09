#!/usr/bin/env python3
"""Head and body points, drawn: one proof per monster, on its idle still.

    python tools/head_overlay.py --out scratch/heads.png

Draws every enemy in enemies.json that has a spritePath, and every transform
form in skills.json that has one, as a grid cell: its idle still, a yellow
crosshair on the head point with a red box of headSize, a green ring on the
body point, and its id. For batch review of hundreds of monsters, and for the
one-off look at a new one (`slice_actor_sheet.py` writes the same picture per
actor as head_check.png through `draw_head_check` below).

ENEMIES.JSON (and a skill's `transform` block, for a form) IS THE ONLY HOME of
headX/headY/headSize/bodyX/bodyY; nothing here or in the slicer stores a
second copy. The fractions are of the idle drawing's OPAQUE box (alpha above
the stage's 0.02 floor, the same box Core measures), X from its left edge as
drawn, Y from its bottom edge, size = head height over box height. The body
point is the torso aim point body-aimed effects land on. A placeholder with no
spritePath has no drawing and is skipped.
"""

import argparse
import json
import os
import sys

try:
    from PIL import Image, ImageDraw
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

ENEMIES_JSON = "Assets/_Project/ContentData/enemies.json"
SKILLS_JSON = "Assets/_Project/ContentData/skills.json"
RESOURCES = "Assets/_Project/Resources"
# FightController.StageVisuals.AlphaFloorByte: a pixel counts when alpha is above this.
ALPHA_FLOOR = 5
MARK_KEYS = ("headX", "headY", "headSize", "bodyX", "bodyY")


def load_enemies(path=ENEMIES_JSON):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle).get("enemies", [])


def load_forms(path=SKILLS_JSON):
    """Every transform form that has its own sprite, shaped like an enemy
    entry: id, spritePath and the five mark keys read off the `transform` block."""
    with open(path, encoding="utf-8") as handle:
        skills = json.load(handle).get("skills", [])
    forms = []
    for skill in skills:
        block = skill.get("transform") or {}
        if (block.get("spritePath") or "").strip():
            forms.append(dict(block, id=f"{skill.get('id')} (form)"))
    return forms


def marks_of(entry):
    """(headX, headY, headSize, bodyX, bodyY) or None when any is unauthored (-1/absent)."""
    values = [entry.get(key, -1) for key in MARK_KEYS]
    return None if any(v is None or v < 0 for v in values) else tuple(values)


def opaque_box(image):
    """(left, top, right, bottom) in image pixels, right/bottom exclusive, or None."""
    alpha = image.convert("RGBA").getchannel("A")
    return alpha.point(lambda v: 255 if v > ALPHA_FLOOR else 0).getbbox()


def draw_head_check(idle_path, marks, label=None):
    """The idle still with a crosshair at the head, a box of headSize and a
    ring at the body, as an RGBA image. `marks` is marks_of()'s tuple. Raises
    ValueError when the still is empty."""
    with Image.open(idle_path) as src:
        image = src.convert("RGBA")
    box = opaque_box(image)
    if box is None:
        raise ValueError(f"{idle_path} has no opaque pixels")
    left, top, right, bottom = box
    width, height = right - left, bottom - top
    hx, hy, size, bx, by = marks
    cx = left + hx * width
    cy = bottom - hy * height
    half = size * height / 2.0

    backdrop = Image.new("RGBA", image.size, (40, 40, 48, 255))
    backdrop.alpha_composite(image)
    draw = ImageDraw.Draw(backdrop)
    draw.rectangle(box, outline=(120, 200, 255, 255))  # the opaque box the fractions are of
    draw.rectangle((cx - half, cy - half, cx + half, cy + half), outline=(255, 60, 60, 255), width=2)
    arm = max(8, half)
    draw.line((cx - arm, cy, cx + arm, cy), fill=(255, 255, 0, 255), width=2)
    draw.line((cx, cy - arm, cx, cy + arm), fill=(255, 255, 0, 255), width=2)
    body_x = left + bx * width
    body_y = bottom - by * height
    ring = max(6, size * height / 3.0)
    draw.ellipse((body_x - ring, body_y - ring, body_x + ring, body_y + ring), outline=(60, 255, 90, 255), width=3)
    if label:
        draw.text((4, 4), label, fill=(255, 255, 255, 255))
    return backdrop


def idle_path_of(entry, resources=RESOURCES):
    return os.path.join(resources, entry["spritePath"], "idle.png")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", required=True, help="PNG to write.")
    ap.add_argument("--enemies", default=ENEMIES_JSON)
    ap.add_argument("--skills", default=SKILLS_JSON)
    ap.add_argument("--resources", default=RESOURCES)
    ap.add_argument("--columns", type=int, default=4)
    ap.add_argument("--cell", type=int, default=360, help="Cell height in px.")
    args = ap.parse_args()

    cells, problems = [], []
    for entry in load_enemies(args.enemies) + load_forms(args.skills):
        if not (entry.get("spritePath") or "").strip():
            continue
        marks = marks_of(entry)
        if marks is None:
            problems.append(f"{entry['id']}: head/body unauthored")
            continue
        path = idle_path_of(entry, args.resources)
        if not os.path.isfile(path):
            problems.append(f"{entry['id']}: no idle still at {path}")
            continue
        cells.append(draw_head_check(path, marks, entry["id"]))

    if not cells:
        sys.exit("no drawn enemies or forms with head and body points to show" + (": " + "; ".join(problems) if problems else ""))

    scaled = []
    for img in cells:
        factor = args.cell / img.height
        scaled.append(img.resize((max(1, round(img.width * factor)), args.cell), Image.LANCZOS))
    cols = max(1, min(args.columns, len(scaled)))
    rows = [scaled[i:i + cols] for i in range(0, len(scaled), cols)]
    col_w = max(img.width for img in scaled)
    sheet = Image.new("RGBA", (col_w * cols, args.cell * len(rows)), (24, 24, 30, 255))
    for r, row in enumerate(rows):
        for c, img in enumerate(row):
            sheet.paste(img, (c * col_w + (col_w - img.width) // 2, r * args.cell))

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    sheet.save(args.out)
    print(f"wrote {args.out} ({len(cells)} enemies and forms)")
    for problem in problems:
        print(f"  SKIPPED {problem}")
    if problems:
        sys.exit(1)


if __name__ == "__main__":
    main()
