#!/usr/bin/env python3
"""Key, trim, grade, slice and scale the reward-screen sheets from a recipe.

    py tools/process_reward_art.py [--only reward_chain.png]

Reads Assets/_Project/Art/UI/Reckoning/reward_recipe.json and writes each named
cell to Reckoning/Processed/<name>.png. The keying is `key_green_screen.key_out`
(not a second keyer); this file adds only what a recipe needs around it: a
wisp trim, a colour grade, cell cutting and one shared canvas per sheet.
Output .meta files are patched to Sprite by `force_sprite_import`.
"""
import argparse
import colorsys
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from key_green_screen import key_out, resize_premultiplied, force_sprite_import, KEY_COLOURS  # noqa: E402

ALPHA_VISIBLE = 16
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FOLDER = os.path.join(ROOT, "Assets", "_Project", "Art", "UI", "Reckoning")


def disc(radius):
    y, x = np.ogrid[-radius:radius + 1, -radius:radius + 1]
    return x * x + y * y <= radius * radius


def trim_wisps(rgba, radius):
    """Drop anything thinner than the disc: stray thread ends past the silhouette.

    The opened mask is regrown by 1 px so the card's own outline keeps its
    antialiased rim; alpha outside it goes to zero.
    """
    arr = np.asarray(rgba).copy()
    solid = arr[..., 3] > 128
    body = ndimage.binary_opening(solid, structure=disc(radius))
    keep = ndimage.binary_dilation(body, structure=disc(1))
    arr[..., 3] = np.where(keep, arr[..., 3], 0)
    arr[..., :3] = np.where(keep[..., None], arr[..., :3], 0)
    return Image.fromarray(arr, "RGBA")


def drop_specks(rgba, min_pixels):
    """Delete connected opaque blobs smaller than `min_pixels` (keying leaves stray dots)."""
    arr = np.asarray(rgba).copy()
    labels, count = ndimage.label(arr[..., 3] > ALPHA_VISIBLE, structure=np.ones((3, 3)))
    sizes = ndimage.sum(np.ones_like(labels), labels, index=np.arange(1, count + 1))
    small = np.isin(labels, np.nonzero(sizes < min_pixels)[0] + 1)
    arr[small] = 0
    return Image.fromarray(arr, "RGBA")


def grade(rgba, saturation, value, cool=0.0):
    """Scale HSV saturation and value, then lean the result toward blue by `cool`."""
    arr = np.asarray(rgba).astype(np.float32) / 255.0
    rgb = arr[..., :3]
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    # grey reference at the same value, then lerp toward the original by `saturation`
    grey = np.repeat(mx[..., None], 3, -1)
    out = grey + (rgb - grey) * saturation
    out = out * value
    out[..., 2] = out[..., 2] + cool * mx
    out[..., 0] = out[..., 0] - cool * 0.5 * mx
    out = np.clip(out, 0, 1)
    res = np.concatenate([out, arr[..., 3:]], -1)
    return Image.fromarray((res * 255 + 0.5).astype(np.uint8), "RGBA")


def restore_opaque(source, keyed, key, opaque_below, rim=3):
    """Give pixels that sit inside the opaque band their source colour back.

    key_out lowers the backdrop channels of every pixel with positive dominance,
    which is right for a fringe and wrong for a subject whose own colour leans
    toward the key (violet cloth against magenta, dominance 25-45 on a backdrop
    near 240). With the band moved above the subject, those pixels are fully
    opaque anyway, so they keep the painted colour; only the band's fringe
    pixels and the `rim` px next to the cut-out still get the spill pull.
    """
    measure, _ = KEY_COLOURS[key]
    src = np.asarray(source.convert("RGB")).astype(np.int32)
    solid = measure(src[..., 0], src[..., 1], src[..., 2]) <= opaque_below
    # Within `rim` px of the cut-out the colour is an outline blended with the
    # backdrop, not subject colour: leave those to the spill pull.
    gone = np.asarray(keyed)[..., 3] == 0
    solid &= ~ndimage.binary_dilation(gone, structure=disc(1), iterations=rim)
    out = np.asarray(keyed).copy()
    out[..., :3] = np.where(solid[..., None], src.astype(np.uint8), out[..., :3])
    return Image.fromarray(out, "RGBA")


def cell_rect(sheet_size, spec, grid):
    if "rect" in spec:
        return tuple(spec["rect"])
    cols, rows = grid
    w, h = sheet_size
    x0, x1 = round(spec["col"] * w / cols), round((spec["col"] + 1) * w / cols)
    y0, y1 = round(spec["row"] * h / rows), round((spec["row"] + 1) * h / rows)
    return (x0, y0, x1 - x0, y1 - y0)


def process_sheet(sheet, out_dir):
    src = os.path.join(FOLDER, sheet["sheet"])
    image = Image.open(src).convert("RGB")
    opaque_below = sheet.get("opaque_below", 18)
    keyed = key_out(image, sheet["key"], opaque_below, sheet.get("transparent_above", 60))
    if sheet.get("restore_opaque"):
        keyed = restore_opaque(image, keyed, sheet["key"], opaque_below)
    if sheet.get("trim_wisps"):
        keyed = trim_wisps(keyed, sheet["trim_wisps"])
    if sheet.get("grade"):
        keyed = grade(keyed, **sheet["grade"])

    keyed = drop_specks(keyed, sheet.get("min_blob", 200))

    grid = sheet.get("grid")
    pieces = []
    for spec in sheet["cells"]:
        x, y, w, h = cell_rect(keyed.size, spec, grid)
        cell = keyed.crop((x, y, x + w, y + h))
        bbox = cell.getchannel("A").point(lambda v: 255 if v > ALPHA_VISIBLE else 0).getbbox()
        if bbox is None:
            sys.exit(f"{spec['name']}: no opaque pixels in {x},{y},{w},{h}")
        # no_trim keeps the cell's own frame (a sprite that must fill its container).
        pieces.append((spec["name"], cell if sheet.get("no_trim") else cell.crop(bbox)))

    pad = 0 if sheet.get("no_trim") else sheet.get("pad", 8)
    if sheet.get("shared_canvas"):
        cw = max(p.width for _, p in pieces) + pad * 2
        ch = max(p.height for _, p in pieces) + pad * 2
        sizes = {name: (cw, ch) for name, _ in pieces}
    else:
        sizes = {name: (p.width + pad * 2, p.height + pad * 2) for name, p in pieces}

    if sheet.get("square"):
        side = max(max(s) for s in sizes.values())
        sizes = {name: (side, side) for name in sizes}

    longest = max(max(s) for s in sizes.values())
    scale = min(1.0, sheet["long_edge"] / longest)
    os.makedirs(out_dir, exist_ok=True)
    for name, piece in pieces:
        w, h = sizes[name]
        canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        top = h - pad - piece.height if sheet.get("align") == "bottom" else (h - piece.height) // 2
        canvas.paste(piece, ((w - piece.width) // 2, top))
        if scale < 1.0:
            canvas = resize_premultiplied(canvas, (max(1, round(w * scale)), max(1, round(h * scale))))
        path = os.path.join(out_dir, name + ".png")
        canvas.save(path)
        fixed = force_sprite_import(path)
        print(f"  {name}.png  {canvas.width}x{canvas.height}{'  [import fixed]' if fixed else ''}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", help="process just this sheet filename")
    ap.add_argument("--out", default=os.path.join(FOLDER, "Processed"))
    args = ap.parse_args()
    with open(os.path.join(FOLDER, "reward_recipe.json"), encoding="utf-8") as f:
        recipe = json.load(f)
    for sheet in recipe["sheets"]:
        if args.only and sheet["sheet"] != args.only:
            continue
        print(sheet["sheet"])
        process_sheet(sheet, args.out)


if __name__ == "__main__":
    main()
