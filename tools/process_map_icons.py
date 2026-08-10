"""Cuts the run-map's tree backdrop and per-room-type icons to transparent,
content-cropped sprites, ready for DescentMapView to draw.

Two different background removals, because the two art batches were
generated two different ways:

  - The tree cluster (map_forest_room.png) is on flat WHITE, so it goes
    through the flood-fill-from-border approach remove_portrait_backgrounds.py
    already uses for portraits -- connectivity-aware, so the tree's own dark
    trunk shadow and the black outline strokes don't get punched through just
    for sharing a colour with a nearby edge pixel.

  - The room-type icons (mob/mob_elite/mob_boss/rest/event/chest) are on
    flat GREEN, so they go through key_green_screen.py's hue-dominance keying
    instead -- brightness-keying a near-black boar's-head icon would eat the
    subject alive.

Both existing scripts are reused directly (imported, not reimplemented) so
this does not risk drifting from an already-proven keying algorithm.

Usage:
    py tools/process_map_icons.py

Writes to Assets/_Project/Art/Backgrounds/Processed/, matching the existing
Portraits/Processed convention -- same filenames as the sources, now
transparent and cropped to content.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(__file__))

from PIL import Image

from key_green_screen import content_bounds, force_sprite_import, key_out_green
from remove_portrait_backgrounds import flood_fill_background_mask
import numpy as np
from PIL import ImageFilter

SOURCE_DIR = "Assets/_Project/Art/Backgrounds"
OUTPUT_DIR = "Assets/_Project/Art/Backgrounds/Processed"

# On flat white -- the tree cluster tile backdrop.
WHITE_BG_SOURCES = ["map_forest_room.png"]

# On flat green -- one icon per room type that has art so far. Room types
# without an entry here (Shop, Unknown, ItemSpawn, Entry) are simply not
# processed; DescentMapView falls back to no overlay for those until their
# art arrives, the same graceful-degradation posture the rest of this
# project takes toward missing content.
GREEN_BG_SOURCES = [
    "map_forest_mob.png",
    "map_forest_mob_elite.png",
    "map_forest_mob_boss.png",
    "map_forest_rest.png",
    "map_forest_event.png",
    "forest_map_chest.png",
]

FLOOD_FILL_TOLERANCE = 35
FEATHER_RADIUS = 5.0
CROP_PADDING = 10

# The tree cluster reads as a flat cutout sticker on the map — a crisp,
# AI-generated silhouette pasted straight onto the panel's dark backdrop
# with nothing to soften the join. Two separate treatments, stacked:
# FEATHER_RADIUS above blurs the cutout's OWN edge (so it's not a hard
# pixel-perfect line); the glow below is a SEPARATE, much softer, tinted
# copy of the silhouette sitting behind it, bleeding a few extra pixels
# past the tree's own edge so it looks gently lit rather than glued down.
GLOW_RADIUS = 14
GLOW_OPACITY = 0.5
GLOW_COLOR = (255, 241, 204)  # warm candlelight cream, not a cold white
GLOW_CROP_PADDING = 24


def process_white_bg(path, out_path):
    img = Image.open(path).convert("RGBA")
    arr = np.array(img)
    rgb = arr[:, :, :3]

    is_background = flood_fill_background_mask(rgb, FLOOD_FILL_TOLERANCE)
    alpha_mask = Image.fromarray(np.where(is_background, 0, 255).astype(np.uint8), "L")

    feathered = np.array(alpha_mask.filter(ImageFilter.GaussianBlur(FEATHER_RADIUS)))
    arr[:, :, 3] = feathered
    subject = Image.fromarray(arr, "RGBA")

    glow_alpha = alpha_mask.filter(ImageFilter.GaussianBlur(GLOW_RADIUS))
    glow_arr = np.zeros_like(arr)
    glow_arr[:, :, 0] = GLOW_COLOR[0]
    glow_arr[:, :, 1] = GLOW_COLOR[1]
    glow_arr[:, :, 2] = GLOW_COLOR[2]
    glow_arr[:, :, 3] = (np.array(glow_alpha).astype(np.float32) * GLOW_OPACITY).astype(np.uint8)
    glow = Image.fromarray(glow_arr, "RGBA")

    composed = Image.alpha_composite(glow, subject)

    # Cropped to the GLOW's own bounds, not the sharp subject's — the glow
    # is deliberately the wider of the two, and cropping to the tighter
    # box would clip the halo it exists to show.
    glow_box = glow.getchannel("A").getbbox()
    result = composed
    if glow_box is not None:
        h, w = rgb.shape[:2]
        left = max(0, glow_box[0] - GLOW_CROP_PADDING)
        top = max(0, glow_box[1] - GLOW_CROP_PADDING)
        right = min(w, glow_box[2] + GLOW_CROP_PADDING)
        bottom = min(h, glow_box[3] + GLOW_CROP_PADDING)
        result = composed.crop((left, top, right, bottom))

    result.save(out_path)
    return result.size


def process_green_bg(path, out_path):
    keyed = key_out_green(Image.open(path))
    left, top, right, bottom = content_bounds(keyed)
    cropped = keyed.crop((
        max(0, left - CROP_PADDING),
        max(0, top - CROP_PADDING),
        min(keyed.size[0], right + CROP_PADDING),
        min(keyed.size[1], bottom + CROP_PADDING),
    ))
    cropped.save(out_path)
    return cropped.size


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    for name in WHITE_BG_SOURCES:
        src = os.path.join(SOURCE_DIR, name)
        if not os.path.exists(src):
            print(f"SKIP (not found): {src}")
            continue
        dst = os.path.join(OUTPUT_DIR, name)
        size = process_white_bg(src, dst)
        fixed = force_sprite_import(dst)
        print(f"{name} [white-bg flood-fill] -> Processed/{name} ({size[0]}x{size[1]})"
              f"{'  [import fixed to Sprite]' if fixed else ''}")

    for name in GREEN_BG_SOURCES:
        src = os.path.join(SOURCE_DIR, name)
        if not os.path.exists(src):
            print(f"SKIP (not found): {src}")
            continue
        dst = os.path.join(OUTPUT_DIR, name)
        size = process_green_bg(src, dst)
        fixed = force_sprite_import(dst)
        print(f"{name} [green-screen key] -> Processed/{name} ({size[0]}x{size[1]})"
              f"{'  [import fixed to Sprite]' if fixed else ''}")

    print("\nIf any '[import fixed to Sprite]' did not print, Unity has not imported "
          "these yet: let it import once, re-run this, and the import settings will "
          "be corrected.")


if __name__ == "__main__":
    main()
