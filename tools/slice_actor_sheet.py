#!/usr/bin/env python3
"""Slice one Stage-1 design sheet into an actor's key-pose stance stills.

## The policy this tool implements

Every combat actor now ships as **one still drawing per stance** (idle,
attack, cast, hurt, defeated, plus any skill-specific pose), posed
procedurally at runtime. No skeletal rigs, no multi-frame animation sheets.
See docs/STANCE_SHEET_SPEC.md for the commissioning work order this tool is
the delivery step of.

The commissioning format is one 1536x1024 image, a grid of cells (3x2 by
default, `--grid` overrides), one distinct pose per cell, same creature,
faces right, identical scale. Those cells ARE the delivery: this tool cuts
them out, keys the background if needed, and composites every stance onto
ONE shared canvas per actor so a single authored ground line holds across
every pose the actor has.

## CLI

    python tools/slice_actor_sheet.py --sheet Assets/_Project/Art/Enemies/beetle/sheet_poses.png \\
        --actor Enemies/beetle \\
        --stances idle,attack,turtle_up,shell_closed,hurt,defeated

Optional:
    --grid COLSxROWS         default 3x2 (six cells, row-major top-left first)
    --key alpha|white_flood|green   default alpha (real transparency)
    --anchor ground_band|centroid   default ground_band (see below)
    --delivery-scale FLOAT    one uniform multiplier applied after native-
                               scale compositing, to restore on-screen
                               presence -- see docs/ART_PIPELINE.md's sizing
                               note. Default 1.0.
    --nudge STANCE:DX,DY      per-stance pixel offset, repeatable. Only ever
                               a reposition, never a resize -- see the
                               ground-line assertion below for why a size
                               correction is refused instead.
    --drop-far-components-px N   a neighbouring cell's stray limb/tail bled
                               across a gutter and survived as its own
                               component -- keep the largest component plus
                               anything within N px of it, zero out the rest.
                               Off by default; every clean Stage-1 sheet
                               needs nothing here.
    --out-root PATH            default Assets/_Project/Resources
    --prune                    delete stance PNGs already in the output
                               folder that this run did not (re)write
    --quiet

A stance name of "-" or "skip" leaves that grid cell out entirely (a design
sheet with a dead cell, or a stance the actor doesn't ship).

## Anchoring

"ground_band" (default): largest-connected-component (ignores detached
debris) intersected with a thin band at the creature's own ground line
(ignores a raised tail or outflung fist -- anatomy far from the ground
cannot move where the creature is anchored). "centroid": whole-mask alpha
centroid, ground line at bbox bottom -- the older, simpler behaviour, kept
for a pose whose mass legitimately touches the ground away from its feet
(a staff, a dragging tail) where ground_band would mis-anchor.

## One shared canvas, one ground line

Every stance is composited onto a canvas sized to the LARGEST stance's
content plus uniform padding, with every stance's content bottom-aligned to
the same row. `FightController.StageVisuals` sizes each combatant's slot to
its sprite's own canvas and stands it on that one authored ground line --
a per-stance canvas would make the actor visibly resize or float the moment
its stance changes. `_assert_one_ground_line` below checks the PNGs this
tool actually wrote, not the in-memory pieces, and refuses to leave a
mismatch on disk silently.

## Safety guards

- Refuses any input path under `Assets/_Project/Resources/` -- processing
  already-processed output compounds resample loss.
- Never scales an individual pose -- only one literal `--delivery-scale`
  for the whole sheet. `sqrt(opaque pixel count)` is measured per stance
  and printed, not `bbox height` -- bbox height is pose-dependent (a crouch
  is shorter than a rear-up at identical draw scale).
"""

import argparse
import os
import sys

try:
    import numpy as np
    from PIL import Image, ImageFilter
except ImportError:
    sys.exit("Pillow and numpy are required: pip install Pillow numpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from remove_portrait_backgrounds import flood_fill_background_mask
from key_green_screen import key_out_green
# Cell-cutting geometry, shared with slice_item_sheet.py -- see that
# module's own header for why it exists.
from sheet_slicing import (
    ALPHA_THRESHOLD,
    SEARCH_FRACTION,
    PADDING,
    opaque_mask,
    column_weights,
    row_weights,
    best_cut,
)

# Ground-contact band size, as a fraction of the CREATURE'S shared canvas
# height (not any one pose's own bbox -- that would be pose-dependent by
# construction, the exact error this anchor exists to avoid).
GROUND_BAND_FRACTION = 0.08
GROUND_BAND_MAX_FRACTION = 0.24
GROUND_BAND_STEP = 0.04
# A band holding less than this fraction of the core's own pixels is "thin
# contact" (one toe) -- widen before trusting it.
GROUND_BAND_MIN_MASS_FRACTION = 0.005

# Enclosed background pockets (see key_sheet's "white_flood" branch). A
# pocket is NEUTRAL (channels within this many levels of each other) --
# BRIGHT, and SMALL, since a pocket trapped inside a silhouette cannot be
# large without being a real hole in the drawing.
NEUTRAL_SATURATION_MAX = 12
POCKET_MIN_BRIGHTNESS = 195
POCKET_MAX_AREA = 200

# Resample ringing (see despeckle_resample_ringing). A pixel must be this
# bright, this much brighter than its own neighbourhood, and essentially
# alone among bright pixels, before it is pulled back.
RINGING_MIN_BRIGHTNESS = 195
RINGING_MIN_EXCESS = 40

DEFAULT_OUT_ROOT = "Assets/_Project/Resources"

# Poses whose feet legitimately do not share the standing ground line get
# no special case here -- everything the actor ships stands on one line by
# construction (see the module docstring). A defeated/prone pose is still
# checked; if it fails, it needs a nudge like anything else.


# ---------------------------------------------------------------------------
# Cell cutting
# ---------------------------------------------------------------------------

def cut_cells(mask, sheet_w, sheet_h, rows, cols):
    """Row-major list of tight ABSOLUTE boxes (or None for an empty cell).

    Nominal even grid, each cut nudged to the emptiest nearby gutter via
    `best_cut` -- the generator does not lay figures out on an exact grid,
    and a plain even split has amputated limbs on real sheets. See
    docs/ART_PIPELINE.md's slicing note.
    """
    cell_h = sheet_h // rows
    y_search = int(cell_h * SEARCH_FRACTION)
    y_weights = row_weights(mask, 0, sheet_w, 0, sheet_h)
    y_cuts = [0]
    for r in range(1, rows):
        y_cuts.append(best_cut(y_weights, 0, r * cell_h, y_search))
    y_cuts.append(sheet_h)

    cell_w = sheet_w // cols
    x_search = int(cell_w * SEARCH_FRACTION)

    cells = []
    for r in range(rows):
        y0, y1 = y_cuts[r], y_cuts[r + 1]
        x_weights = column_weights(mask, 0, sheet_w, y0, y1)
        x_cuts = [0]
        for c in range(1, cols):
            x_cuts.append(best_cut(x_weights, 0, c * cell_w, x_search))
        x_cuts.append(sheet_w)
        for c in range(cols):
            box = (x_cuts[c], y0, x_cuts[c + 1], y1)
            sub = mask.crop(box).getbbox()
            if sub is None:
                cells.append(None)
                continue
            abs_box = (box[0] + sub[0], box[1] + sub[1], box[0] + sub[2], box[1] + sub[3])
            cells.append(abs_box)
    return cells


# ---------------------------------------------------------------------------
# Keying
# ---------------------------------------------------------------------------

def key_sheet(image, mode):
    if mode == "alpha":
        return image.convert("RGBA")
    if mode == "white_flood":
        rgba = image.convert("RGBA")
        arr = np.array(rgba)
        is_bg = flood_fill_background_mask(arr[:, :, :3], tolerance=35.0)

        # Clear background POCKETS the border flood cannot reach -- a pocket
        # enclosed by the art stays opaque and renders as a light speck.
        # Distinguished from the art's own light pixels by SATURATION, not
        # brightness (a background is neutral; a cream highlight is warm).
        rgb_i = arr[:, :, :3].astype(np.int16)
        neutral = (rgb_i.max(axis=2) - rgb_i.min(axis=2)) < NEUTRAL_SATURATION_MAX
        pocket = (rgb_i.min(axis=2) > POCKET_MIN_BRIGHTNESS) & neutral & (~is_bg)
        if pocket.any():
            for comp in _all_components(pocket):
                if comp.sum() <= POCKET_MAX_AREA:
                    is_bg = is_bg | comp

        # Bleed the sprite's own colour outward before feathering the alpha,
        # so a softened edge pixel carries the creature's outline colour
        # rather than the white background -- otherwise every feathered edge
        # composites as a pale halo over the dark stage.
        rgb = arr[:, :, :3].astype(np.float32)
        known = ~is_bg
        for _ in range(2):
            if known.all():
                break
            acc = np.zeros_like(rgb)
            cnt = np.zeros(known.shape, np.float32)
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                shifted_rgb = np.roll(rgb, (dy, dx), axis=(0, 1))
                shifted_known = np.roll(known, (dy, dx), axis=(0, 1))
                acc += shifted_rgb * shifted_known[:, :, None]
                cnt += shifted_known
            fill = (~known) & (cnt > 0)
            rgb[fill] = acc[fill] / cnt[fill][:, None]
            known = known | fill
        arr[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)

        alpha = np.where(is_bg, 0, 255).astype(np.uint8)
        alpha = np.array(Image.fromarray(alpha, "L").filter(ImageFilter.GaussianBlur(1.2)))
        arr[:, :, 3] = alpha
        return Image.fromarray(arr, "RGBA")
    if mode == "green":
        return key_out_green(image)
    raise ValueError(f"unknown key mode: {mode!r}")


# ---------------------------------------------------------------------------
# Largest connected component -- row-run union-find, no scipy dependency.
# ---------------------------------------------------------------------------

def _label_components(mask_bool):
    h = mask_bool.shape[0]
    parent, size = [], []

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra == rb:
            return
        if size[ra] < size[rb]:
            ra, rb = rb, ra
        parent[rb] = ra
        size[ra] += size[rb]

    row_runs, row_ids = [], []
    for y in range(h):
        row = mask_bool[y].astype(np.int8)
        if not row.any():
            row_runs.append([])
            row_ids.append([])
            continue
        padded = np.concatenate(([0], row, [0]))
        edges = np.flatnonzero(np.diff(padded))
        runs = [(int(edges[i]), int(edges[i + 1])) for i in range(0, len(edges), 2)]
        ids = []
        for (x0, x1) in runs:
            gid = len(parent)
            parent.append(gid)
            size.append(x1 - x0)
            ids.append(gid)
        if y > 0:
            for i, (x0, x1) in enumerate(runs):
                for j, (px0, px1) in enumerate(row_runs[y - 1]):
                    if px0 < x1 and x0 < px1:
                        union(ids[i], row_ids[y - 1][j])
        row_runs.append(runs)
        row_ids.append(ids)

    return row_runs, row_ids, find, size


def largest_connected_component(mask_bool):
    row_runs, row_ids, find, size = _label_components(mask_bool)
    if not size:
        return np.zeros_like(mask_bool, dtype=bool)
    roots = set(find(g) for g in range(len(size)))
    best_root = max(roots, key=lambda r: size[r])
    out = np.zeros_like(mask_bool, dtype=bool)
    for y, (runs, ids) in enumerate(zip(row_runs, row_ids)):
        for (x0, x1), gid in zip(runs, ids):
            if find(gid) == best_root:
                out[y, x0:x1] = True
    return out


def _all_components(mask_bool):
    """Every 4-connected component as its own boolean mask, largest first."""
    row_runs, row_ids, find, size = _label_components(mask_bool)
    if not size:
        return []
    root_total = {}
    for y, (runs, ids) in enumerate(zip(row_runs, row_ids)):
        for (x0, x1), gid in zip(runs, ids):
            root_total[find(gid)] = root_total.get(find(gid), 0) + (x1 - x0)
    masks = {r: np.zeros_like(mask_bool, dtype=bool) for r in root_total}
    for y, (runs, ids) in enumerate(zip(row_runs, row_ids)):
        for (x0, x1), gid in zip(runs, ids):
            masks[find(gid)][y, x0:x1] = True
    ordered = sorted(root_total.items(), key=lambda kv: kv[1], reverse=True)
    return [masks[r] for r, _ in ordered]


def _boundary_points(mask_bool):
    interior = np.ones_like(mask_bool)
    interior[:-1, :] &= mask_bool[1:, :]
    interior[1:, :] &= mask_bool[:-1, :]
    interior[:, :-1] &= mask_bool[:, 1:]
    interior[:, 1:] &= mask_bool[:, :-1]
    boundary = mask_bool & ~interior
    ys, xs = np.nonzero(boundary)
    return np.stack([ys, xs], axis=1).astype(np.float32)


def _nearest_gap(mask_a, mask_b):
    pa, pb = _boundary_points(mask_a), _boundary_points(mask_b)
    if len(pa) == 0 or len(pb) == 0:
        return float("inf")
    d2 = ((pa[:, None, :] - pb[None, :, :]) ** 2).sum(axis=2)
    return float(np.sqrt(d2.min()))


def drop_far_components(mask_bool, threshold_px):
    """Keeps the largest component plus any other within threshold_px of it
    (nearest-pixel distance); zeroes every component further away. The tell
    for contamination bleeding across a gutter is DISTANCE from the main
    silhouette, not size -- see the module docstring.
    """
    components = _all_components(mask_bool)
    if len(components) <= 1:
        return mask_bool
    main = components[0]
    kept = main.copy()
    for extra in components[1:]:
        if _nearest_gap(main, extra) <= threshold_px:
            kept |= extra
    return kept


def ground_band_anchor(mask_bool, canvas_h):
    """(anchor_x, core_mask, path) in mask_bool's own LOCAL coordinates.
    None if the mask is entirely empty."""
    core = largest_connected_component(mask_bool)
    ys, xs = np.nonzero(core)
    if len(xs) == 0:
        return None
    core_count = len(xs)
    bottom = int(ys.max())
    fraction = GROUND_BAND_FRACTION
    widened = False
    while True:
        band_px = max(1, round(fraction * canvas_h))
        band_top = max(0, bottom - band_px + 1)
        in_band = ys >= band_top
        if in_band.sum() >= max(1, core_count * GROUND_BAND_MIN_MASS_FRACTION):
            anchor_x = float(xs[in_band].mean())
            return anchor_x, core, ("widened_band" if widened else "ground_band")
        fraction += GROUND_BAND_STEP
        widened = True
        if fraction > GROUND_BAND_MAX_FRACTION:
            break
    return float(xs.mean()), core, "core_centroid_fallback"


def alpha_centroid_x(mask, box):
    region = mask.crop(box)
    w, h = region.size
    px = region.load()
    total, weighted = 0, 0
    for x in range(w):
        col = sum(1 for y in range(h) if px[x, y])
        total += col
        weighted += col * x
    if total == 0:
        return box[0] + (box[2] - box[0]) // 2
    return box[0] + weighted / total


# ---------------------------------------------------------------------------
# Resampling (only exercised when --delivery-scale != 1.0)
# ---------------------------------------------------------------------------

def despeckle_resample_ringing(image):
    """Darken isolated bright pixels a sharp LANCZOS resize overshot into --
    see the original tool's rationale, unchanged: only a pixel both much
    brighter than its neighbourhood and neutral (no hue) is pulled back."""
    arr = np.asarray(image.convert("RGBA")).copy()
    rgb = arr[:, :, :3].astype(np.int16)
    alpha = arr[:, :, 3]
    bright = rgb.min(axis=2) > RINGING_MIN_BRIGHTNESS
    neutral = (rgb.max(axis=2) - rgb.min(axis=2)) < NEUTRAL_SATURATION_MAX
    inside = alpha > 200
    candidate = bright & neutral & inside
    if not candidate.any():
        return image
    h, w = alpha.shape
    stack = []
    bright_neighbours = np.zeros((h, w), np.int16)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy == 0 and dx == 0:
                continue
            stack.append(np.roll(np.roll(rgb, dy, axis=0), dx, axis=1))
            bright_neighbours += np.roll(np.roll(bright, dy, axis=0), dx, axis=1)
    local_median = np.median(np.stack(stack, axis=0), axis=0)
    overshoot = candidate & (bright_neighbours <= 1)
    overshoot &= (rgb.min(axis=2) - local_median.min(axis=2)) > RINGING_MIN_EXCESS
    if not overshoot.any():
        return image
    arr[:, :, :3][overshoot] = local_median[overshoot].astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


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


# ---------------------------------------------------------------------------
# Ground-line consistency, checked against what was actually WRITTEN
# ---------------------------------------------------------------------------

def _assert_one_ground_line(actor_label, out_dir, written_names, verbose):
    """Every pose an actor ships has to STAND in the same place -- the stage
    pins each actor's canvas bottom to the ground line, so a pose sitting
    higher in its own canvas visibly takes off the moment the stance
    changes. Reads the PNGs actually written, not the in-memory pieces, so
    it also covers nudges and anything a future change does between
    placement and disk.
    """
    MAX_SPREAD_PX = 6
    feet = []
    for name in sorted(written_names):
        path = os.path.join(out_dir, f"{name}.png")
        if not os.path.exists(path):
            continue
        with Image.open(path) as img:
            mask = np.array(img.convert("RGBA"))[:, :, 3] > ALPHA_THRESHOLD
        if not mask.any():
            continue
        core = largest_connected_component(mask)
        rows = np.nonzero(core)[0]
        if not len(rows):
            continue
        feet.append((name, int(rows.max())))

    if len(feet) < 2:
        return
    lowest = min(f for _, f in feet)
    highest = max(f for _, f in feet)
    spread = highest - lowest
    if spread <= MAX_SPREAD_PX:
        if verbose:
            print(f"  ground line: all {len(feet)} stance(s) within {spread}px")
        return
    table = ", ".join(f"{n}={row}" for n, row in sorted(feet, key=lambda t: t[1]))
    sys.exit(
        f"[{actor_label}]: stances do not share one ground line -- they span {spread}px "
        f"(max {MAX_SPREAD_PX}). The figure will appear to fly when its stance changes. "
        f"Rows are measured from the top, so a SMALLER number means the figure stands "
        f"HIGHER in its canvas: {table}"
    )


def _report_stray_files(out_dir, written_names, prune, verbose):
    existing = [f[:-4] for f in os.listdir(out_dir) if f.lower().endswith(".png")] if os.path.isdir(out_dir) else []
    stray = sorted(set(existing) - written_names)
    if not stray:
        return
    if prune:
        for name in stray:
            for suffix in (".png", ".png.meta"):
                p = os.path.join(out_dir, name + suffix)
                if os.path.exists(p):
                    os.remove(p)
        if verbose:
            print(f"  pruned {len(stray)} file(s) this run did not (re)write: {', '.join(stray)}")
    else:
        print(f"  NOTE: {len(stray)} file(s) in {out_dir} were not written by this run "
              f"(pass --prune to remove): {', '.join(stray)}")


# ---------------------------------------------------------------------------
# CLI plumbing
# ---------------------------------------------------------------------------

def parse_actor(actor_arg, out_root):
    """"Enemies/beetle" -> (out_dir, label). Also accepts "Characters/sheep".
    "Actor", not "enemy": a party member's stance art
    (Resources/Characters/<id>/) and a monster's (Resources/Enemies/<id>/)
    are resolved by one runtime path and held to one set of invariants."""
    parts = actor_arg.replace("\\", "/").strip("/").split("/")
    if len(parts) != 2 or parts[0] not in ("Enemies", "Characters"):
        sys.exit(f"--actor must be 'Enemies/<id>' or 'Characters/<id>', got {actor_arg!r}")
    root_kind, actor_id = parts
    out_dir = os.path.join(out_root, root_kind, actor_id)
    return out_dir, f"{root_kind}/{actor_id}"


def parse_grid(grid_arg):
    cols_s, _, rows_s = grid_arg.lower().partition("x")
    try:
        cols, rows = int(cols_s), int(rows_s)
    except ValueError:
        sys.exit(f"--grid must be COLSxROWS, e.g. 3x2, got {grid_arg!r}")
    if cols < 1 or rows < 1:
        sys.exit(f"--grid must have at least one row and column, got {grid_arg!r}")
    return rows, cols


def parse_nudges(nudge_args):
    nudges = {}
    for raw in nudge_args or []:
        name, _, rest = raw.partition(":")
        dx_s, _, dy_s = rest.partition(",")
        try:
            nudges[name] = (int(dx_s), int(dy_s))
        except ValueError:
            sys.exit(f"--nudge must be STANCE:DX,DY, e.g. idle:-3,0, got {raw!r}")
    return nudges


SKIP_TOKENS = {"-", "skip", "none", ""}


def process(sheet_path, actor_arg, stances_arg, grid_arg, key_mode, anchor_mode,
            delivery_scale, nudges, drop_far_px, out_root, prune, verbose):
    if "/Resources/" in os.path.abspath(sheet_path).replace("\\", "/"):
        sys.exit(f"Refusing to read from a Resources/ path (would compound a previous pass): {sheet_path}")
    if not os.path.isfile(sheet_path):
        sys.exit(f"Missing source sheet: {sheet_path}")

    rows, cols = parse_grid(grid_arg)
    out_dir, label = parse_actor(actor_arg, out_root)
    names = [s.strip() for s in stances_arg.split(",")]
    if len(names) != rows * cols:
        sys.exit(f"--stances has {len(names)} name(s) but the {grid_arg} grid has {rows * cols} cell(s)")

    print(f"[{label}] sheet={sheet_path} grid={grid_arg} key={key_mode} anchor={anchor_mode} "
          f"delivery_scale={delivery_scale} -> {out_dir}")

    keyed = key_sheet(Image.open(sheet_path), key_mode)
    mask = opaque_mask(keyed)
    boxes = cut_cells(mask, keyed.width, keyed.height, rows, cols)

    # Pass 0: tight-cropped native-size pieces per named cell.
    pieces = []  # [(name, piece_RGBA, mask_bool)]
    for i, box in enumerate(boxes):
        name = names[i] if i < len(names) else None
        if name is None or name.lower() in SKIP_TOKENS:
            continue
        if box is None:
            print(f"  WARNING: '{name}' cell is empty (no opaque pixels) -- skipped")
            continue
        piece = keyed.crop(box)

        if delivery_scale != 1.0:
            new_size = (max(1, round(piece.width * delivery_scale)), max(1, round(piece.height * delivery_scale)))
            piece = resize_premultiplied(piece, new_size)
            piece = despeckle_resample_ringing(piece)
            bbox = opaque_mask(piece).getbbox()
            if bbox is None:
                print(f"  WARNING: '{name}' has no opaque pixels after scaling -- skipped")
                continue
            piece = piece.crop(bbox)

        mask_bool = np.array(opaque_mask(piece)) > 0

        if drop_far_px is not None:
            cleaned = drop_far_components(mask_bool, drop_far_px)
            if not np.array_equal(cleaned, mask_bool):
                arr = np.asarray(piece.convert("RGBA")).copy()
                arr[~cleaned, 3] = 0
                piece = Image.fromarray(arr, "RGBA")
                tight = opaque_mask(piece).getbbox()
                piece = piece.crop(tight)
                mask_bool = np.array(opaque_mask(piece)) > 0
                if verbose:
                    print(f"  '{name}': dropped a stray component beyond {drop_far_px}px")

        content_h = mask_bool.shape[0]
        if verbose:
            core = largest_connected_component(mask_bool)
            print(f"  '{name}': content {piece.width}x{piece.height}  "
                  f"sqrt(LCC-mass)={np.sqrt(core.sum()):.1f}")
        pieces.append((name, piece, mask_bool))

    if not pieces:
        sys.exit(f"[{label}]: no stances produced at all.")

    # Pass 1: per-piece foot row, in its own local coordinates.
    footed = []
    for name, piece, mask_bool in pieces:
        if anchor_mode == "ground_band":
            core = largest_connected_component(mask_bool)
            core_rows = np.nonzero(core)[0]
            foot_y = int(core_rows.max()) if len(core_rows) else piece.height - 1
        else:
            foot_y = piece.height - 1
        footed.append((name, piece, mask_bool, foot_y))

    max_above = max(foot_y + 1 for _, _, _, foot_y in footed)
    max_below = max(p.height - 1 - foot_y for _, p, _, foot_y in footed)
    canvas_h = int(max_above + max_below) + PADDING * 2
    ground_y = PADDING + int(max_above)
    print(f"  groundLine {canvas_h - ground_y}  (StanceManifest.json wants that number)")

    # Pass 2: per-piece horizontal anchor.
    anchored = []
    for name, piece, mask_bool, foot_y in footed:
        if anchor_mode == "ground_band":
            result = ground_band_anchor(mask_bool, canvas_h)
            if result is None:
                cx, path = piece.width / 2.0, "empty_fallback"
            else:
                cx, core, path = result
                core_frac = core.sum() / max(1, mask_bool.sum())
                if core_frac < 0.85:
                    print(f"  WARNING: '{name}' -- largest connected component covers only "
                          f"{core_frac * 100:.0f}% of opaque mass (debris or a detached part?)")
            if path != "ground_band" and verbose:
                print(f"  '{name}': anchor path = {path}")
        else:
            local_mask_img = Image.fromarray((mask_bool * 255).astype(np.uint8), "L")
            cx = alpha_centroid_x(local_mask_img, (0, 0, piece.width, piece.height))
        anchored.append((name, piece, cx, foot_y))

    max_left = max(cx for _, _, cx, _ in anchored)
    max_right = max(piece.width - cx for _, piece, cx, _ in anchored)
    canvas_w = int(max_left + max_right) + PADDING * 2
    anchor_x = int(max_left) + PADDING

    os.makedirs(out_dir, exist_ok=True)
    written = set()
    for name, piece, cx, foot_y in anchored:
        canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        dx, dy = nudges.get(name, (0, 0))
        paste_x = int(round(anchor_x - cx)) + dx
        paste_y = int(ground_y - 1 - foot_y) + dy
        canvas.paste(piece, (paste_x, paste_y), piece)
        canvas.save(os.path.join(out_dir, f"{name}.png"))
        written.add(name)
        if verbose:
            print(f"  {name}.png  {canvas_w}x{canvas_h}")

    _assert_one_ground_line(label, out_dir, written, verbose)
    _report_stray_files(out_dir, written, prune, verbose)
    return written


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--sheet", required=True, help="Path to the Stage-1 design sheet")
    ap.add_argument("--actor", required=True, help="'Enemies/<id>' or 'Characters/<id>'")
    ap.add_argument("--stances", required=True,
                    help="Comma-separated stance names, row-major, one per grid cell. "
                         "Use '-' to skip a cell.")
    ap.add_argument("--grid", default="3x2", help="COLSxROWS, default 3x2 (six cells)")
    ap.add_argument("--key", default="alpha", choices=("alpha", "white_flood", "green"))
    ap.add_argument("--anchor", default="ground_band", choices=("ground_band", "centroid"))
    ap.add_argument("--delivery-scale", type=float, default=1.0)
    ap.add_argument("--nudge", action="append", metavar="STANCE:DX,DY", default=[])
    ap.add_argument("--drop-far-components-px", type=int, default=None)
    ap.add_argument("--out-root", default=DEFAULT_OUT_ROOT)
    ap.add_argument("--prune", action="store_true")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    process(
        sheet_path=args.sheet,
        actor_arg=args.actor,
        stances_arg=args.stances,
        grid_arg=args.grid,
        key_mode=args.key,
        anchor_mode=args.anchor,
        delivery_scale=args.delivery_scale,
        nudges=parse_nudges(args.nudge),
        drop_far_px=args.drop_far_components_px,
        out_root=args.out_root,
        prune=args.prune,
        verbose=not args.quiet,
    )


if __name__ == "__main__":
    main()
