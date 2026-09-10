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
    --recipe PATH            replay the recipe.json a previous run wrote --
                             every other cut-affecting argument comes out of
                             the file, so what runs is the invocation that
                             produced the committed art rather than a
                             reconstruction of it. --out-root/--quiet may
                             still be given explicitly alongside --recipe
                             (they choose where the replay writes, not what
                             it cuts); any other flag given alongside
                             --recipe is refused. See "Recipes" below.
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
    --max-ground-spread PX     allow the stances' lowest rows to differ by up to
                               PX (default 6). Only for a deliberate hover, with
                               --nudge lifting the airborne stances and defeated
                               left on the floor.
    --pocket-max-area N        white_flood only: key enclosed neutral-bright
                               pockets up to N px (default 200). Raise it for a
                               checkerboard sheet whose art encloses a real hole.
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

## More than one sheet, one actor

An actor's stances do not have to come from one sheet. Bjorn's Slam kit was
commissioned as a second sheet after his original six stances shipped, and a
second AI generation of the same character is never pixel-identical to the
first at the same nominal size -- there is no scale to hold constant across
sheets except by measuring one. **`--sheet`, `--grid`, `--stances`, `--key`,
`--pocket-max-area` and `--delivery-scale` may each be repeated, once per
sheet**, in the same relative order:

    python tools/slice_actor_sheet.py --actor Characters/bear \\
        --sheet Assets/_Project/Art/Characters/bear/sheet_poses.png \\
        --grid 3x2 --stances idle,attack,cast,hurt,defeated,victory \\
        --key white_flood --pocket-max-area 4000 --delivery-scale 0.8632286995515696 \\
        --sheet Assets/_Project/Art/Characters/bear/sheet_slam.png \\
        --grid 2x2 --stances rush,-,overhead,slam \\
        --key white_flood --pocket-max-area 4000 --delivery-scale 0.503 \\
        --prune

A cell a later sheet doesn't use is skipped the same way any sheet skips a
cell: name it `-` (or `skip`) in that sheet's own `--stances`. There is no
separate cell selector, because the stance list already says which cells are
used and in what name/order.

Rules that do not change per sheet, because they describe the ACTOR rather
than any one source image: `--actor`, `--anchor`, `--nudge`, `--max-ground-spread`,
`--drop-far-components-px`, `--out-root`, `--prune`. Give each of those once,
same as always.

`--grid`/`--key`/`--pocket-max-area`/`--delivery-scale` may also be given
**once** with more than one `--sheet` present, in which case that one value
applies to every sheet -- most multi-sheet actors will still want one `--key`
and one `--pocket-max-area` for every sheet and only the per-sheet scale
genuinely differs. Give each exactly once per `--sheet`, or exactly once for
all of them; any other count is refused. `--stances` has no shared default
(the cells differ every time) and must be given exactly once per `--sheet`.

**A stance name may not repeat across sheets.** Two sheets each naming a cell
`idle` would silently let whichever sheet is processed second win, which is
exactly the kind of collision `slice_spell_sheet.py`'s multi-source `sources`
list already refuses for spell frames -- this is the same rule for actor
stances.

The union canvas, its one ground line, and every downstream assertion
(`_assert_one_ground_line`, `ActorArtAssertions.AssertOneCanvasSize`) do not
know or care how many sheets a stance came from: every named cell from every
sheet is gathered into one flat list of pieces before the canvas is ever
sized, so a two-sheet actor is composited exactly like a one-sheet actor with
more cells.

## Recipes

Every run against the real `Resources` tree writes
`Assets/_Project/Art/<Enemies|Characters>/<id>/recipe.json`: the source sheet,
the FULL argument list with every default made explicit, this file's sha256,
the Pillow / numpy / Python versions, the output folder, the ground line it
measured, and when. `--recipe <path>` replays it.

WHY A FILE AND NOT THE README. The README is prose a person wrote and is
still where the reasoning belongs -- but the treant's said `delivery_scale
1.05`, `white_flood`, and "a raised pocket_max_area", and reconstructing the
run from it meant guessing the raised value and testing three candidates
against the committed bytes. A recipe is the argv, so there is nothing to
reconstruct.

**The recipe describes how the frames are PRODUCED. Content describes how
they PLAY.** Which stance a skill poses, how long a beat takes, how big the
figure stands on the stage -- all of that is `ContentData/`, and content may
override a recipe's intent deliberately. The same split
`slice_spell_sheet.py`'s recipes have with the `vfx` block they are
deliberately not allowed to own.

A replay never rewrites the recipe: the argv it would write is the argv it
just read, so the only thing that could change is the timestamp, and a
verification run that dirties the tree is a verification nobody runs twice. It
reports whether this file's hash still matches the one recorded, which is the
warning that a byte-identical result is no longer guaranteed.

A replay honours an explicit `--out-root`/`--quiet` typed alongside
`--recipe` -- those choose WHERE the replay writes, never WHAT it cuts, so
`--recipe <path> --out-root scratch/verify` verifies into scratch instead of
overwriting the real delivered PNGs. Any other flag typed alongside
`--recipe` (a different `--sheet`, `--grid`, `--key`, `--anchor`, `--nudge`,
...) would change the cut the recipe exists to pin, so it is refused rather
than silently discarded.

**A one-sheet recipe's top level is unchanged**: `"sheet"` is a single path
and `"argv"` has one `--sheet`. A **multi-sheet** recipe additionally carries
a `"sheets"` array -- one entry per source, each naming its own sheet, grid,
stance list, key mode, pocket-max-area and delivery scale -- purely for a
person reading the file; replay never looks at it. `"argv"` is the one thing
`--recipe` actually reads, for both shapes: it is the repeated
`--sheet`/`--grid`/`--stances`/`--key`/`--pocket-max-area`/`--delivery-scale`
flags described in "More than one sheet, one actor" above, so a two-sheet
recipe replays through the exact same argument parser as a one-sheet one, one
`--sheet` occurrence per source. This is why a one-sheet recipe never had to
change shape to make room for the two-sheet case: `"argv"` already said
everything a replay needs, and it still does.

It never writes `.meta` files either. Unity generates those on import and
`Editor/StanceSpriteImporter.cs` sets the importer settings a stance PNG
needs; a tool copying a `.meta` alongside a regenerated PNG is how a duplicated
asset ends up with a duplicated GUID (measured, 2026-09-05 baseline, E1).

## Who owns the ground line

This tool MEASURES a ground line and `Resources/StanceManifest.json` AUTHORS
one, and before `groundLineSource` existed the handover between those two was
a human copying a number off a terminal (measured: 2026-09-05 baseline, E1).
Every actor entry now says which of the two owns its number:

    "groundLineSource": "slicer"     this tool wrote it and may rewrite it
    "groundLineSource": "authored"   a person decided it; the tool must not

**Absent means "authored"**, so nothing written before the field existed
changes meaning by gaining it. A new actor's entry is created by the tool as
`"slicer"`, because a number nobody has looked at yet is the tool's.

When the entry is authored and the measurement disagrees, the run prints the
computed value and the delta and leaves the file alone -- the disagreement is
the point (the golem's slam erupts below its feet; Shawn's idle plants a
staff), and silently overwriting an override is how those figures ended up
floating in the first place. `breath` and `hover` are never written by any
tool: how hard a creature breathes and whether it flies are judgements about
the art, not measurements of it.

Nothing is recorded when `--out-root` points somewhere other than the real
`Resources` tree: art written to scratch must not record a ground line for
the actor that ships.

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
import collections
import datetime
import hashlib
import json
import os
import platform
import sys

try:
    import numpy as np
    from PIL import Image, ImageFilter
except ImportError:
    sys.exit("Pillow and numpy are required: pip install Pillow numpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from remove_portrait_backgrounds import flood_fill_background_mask
from key_green_screen import key_out_green, resize_premultiplied
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
STANCE_MANIFEST = "Assets/_Project/Resources/StanceManifest.json"

# The two values groundLineSource may take. Absent is "authored" -- see the
# module docstring's "Who owns the ground line".
SOURCE_SLICER = "slicer"
SOURCE_AUTHORED = "authored"

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

def key_sheet(image, mode, pocket_max_area=POCKET_MAX_AREA):
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
                if comp.sum() <= pocket_max_area:
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


# resize_premultiplied moved to key_green_screen.py (S13's review): it
# duplicated that module's own resize_alpha_aware almost line for line, and
# key_green_screen.py already supplies key_out_green to this file, so it is
# the one shared home now. Imported above, verbatim, under its own name.


# ---------------------------------------------------------------------------
# Ground-line consistency, checked against what was actually WRITTEN
# ---------------------------------------------------------------------------

def _assert_one_ground_line(actor_label, out_dir, written_names, verbose, max_spread_px=6):
    """Every pose an actor ships has to STAND in the same place -- the stage
    pins each actor's canvas bottom to the ground line, so a pose sitting
    higher in its own canvas visibly takes off the moment the stance
    changes. Reads the PNGs actually written, not the in-memory pieces, so
    it also covers nudges and anything a future change does between
    placement and disk.
    """
    MAX_SPREAD_PX = max_spread_px
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


# ---------------------------------------------------------------------------
# Recipes -- the argv that made the art, beside the art
# ---------------------------------------------------------------------------

ART_ROOT = "Assets/_Project/Art"
RECIPE_NAME = "recipe.json"


def recipe_path(actor_arg):
    """Beside the actor's own source material, by DELIVERED id.

    The art folder and the content id can disagree -- the troll's sheet lives
    in Art/Enemies/forest_troll/ and ships as `forest_warden` -- and the id is
    the half everything else keys on, so it is the half this is filed under.
    """
    parts = actor_arg.replace("\\", "/").strip("/").split("/")
    return os.path.join(ART_ROOT, parts[0], parts[1], RECIPE_NAME)


def canonical_argv(sheet_specs, actor, anchor, max_ground_spread, nudges, drop_far_components_px, prune):
    """The run, with every default made explicit.

    EXPLICIT DEFAULTS ON PURPOSE. A recipe recording only what was typed would
    change meaning the day a default changes -- silently, and only for the
    actors sliced before the change. Writing them all out costs eight lines of
    JSON and makes the file a description of the run rather than of the
    keystrokes.

    ONE `--sheet` GROUP PER ENTRY IN `sheet_specs`, IN ORDER. A one-sheet actor
    (`len(sheet_specs) == 1`) emits exactly the flags this function has always
    emitted for it -- same flags, same values -- so an existing single-sheet
    recipe's shape is not something this had to special-case, it falls out of
    the general case at n=1.
    """
    argv = []
    for spec in sheet_specs:
        argv += [
            "--sheet", spec["sheet"].replace("\\", "/"),
            "--grid", spec["grid"],
            "--stances", spec["stances"],
            "--key", spec["key"],
            "--pocket-max-area", str(spec["pocket_max_area"]),
            "--delivery-scale", repr(float(spec["delivery_scale"])),
        ]
    argv += [
        "--actor", actor,
        "--anchor", anchor,
        "--max-ground-spread", str(max_ground_spread),
    ]
    for nudge in nudges or []:
        argv += ["--nudge", nudge]
    if drop_far_components_px is not None:
        argv += ["--drop-far-components-px", str(drop_far_components_px)]
    if prune:
        argv.append("--prune")
    return argv


def _sha256(path):
    with open(path, "rb") as handle:
        return hashlib.sha256(handle.read()).hexdigest()


def _versions():
    try:
        import PIL
        pillow = PIL.__version__
    except Exception:  # pragma: no cover - a run without Pillow never gets here
        pillow = "unknown"
    return collections.OrderedDict([
        ("python", platform.python_version()),
        ("pillow", pillow),
        ("numpy", np.__version__),
    ])


def write_recipe(sheet_specs, actor, anchor, max_ground_spread, nudges, drop_far_components_px,
                  prune, ground_line, out_dir):
    """`sheet_specs` is the ordered list built in `main()` -- see its docstring
    there. ONE SHEET keeps the exact old top-level shape (`"sheet"`: a single
    path, next to `"argv"`) so a one-sheet actor's recipe is unchanged from
    before this file supported more than one. MORE THAN ONE additionally
    writes a `"sheets"` array for a person reading the file; `"argv"` is what
    `load_recipe`/`--recipe` actually replay, for both shapes alike.
    """
    path = recipe_path(actor)
    os.makedirs(os.path.dirname(path), exist_ok=True)

    recipe = collections.OrderedDict()
    recipe["_readme"] = (
        "How this actor's stance stills were produced. Replay with "
        "`python tools/slice_actor_sheet.py --recipe " + path.replace("\\", "/") + "`; "
        "a replay that does not reproduce the committed PNGs byte for byte means the "
        "tool, its libraries or the source sheet moved under it. The recipe describes "
        "how the frames are MADE -- how they are used is content, in ContentData/."
    )
    recipe["tool"] = "tools/slice_actor_sheet.py"
    recipe["toolSha256"] = _sha256(os.path.abspath(__file__))
    if len(sheet_specs) == 1:
        recipe["sheet"] = sheet_specs[0]["sheet"].replace("\\", "/")
    else:
        recipe["sheets"] = [
            collections.OrderedDict([
                ("sheet", spec["sheet"].replace("\\", "/")),
                ("grid", spec["grid"]),
                ("stances", spec["stances"]),
                ("key", spec["key"]),
                ("pocket_max_area", spec["pocket_max_area"]),
                ("delivery_scale", spec["delivery_scale"]),
            ])
            for spec in sheet_specs
        ]
    recipe["argv"] = canonical_argv(sheet_specs, actor, anchor, max_ground_spread,
                                     nudges, drop_far_components_px, prune)
    recipe["outputFolder"] = out_dir.replace("\\", "/")
    recipe["groundLine"] = ground_line
    recipe["versions"] = _versions()
    recipe["generatedAt"] = datetime.datetime.now().astimezone().replace(microsecond=0).isoformat()

    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(json.dumps(recipe, indent=2, ensure_ascii=False) + "\n")
    print(f"  recipe: {path}")
    return path


def load_recipe(path):
    """Returns (argv, recipe). Refuses a recipe whose source sheet is gone."""
    if not os.path.isfile(path):
        sys.exit(f"No recipe at {path}")

    try:
        with open(path, encoding="utf-8") as handle:
            recipe = json.load(handle)
    except ValueError as problem:
        sys.exit(f"cannot read recipe {path}: {problem}")

    argv = recipe.get("argv")
    if not argv:
        sys.exit(f"{path} has no argv -- there is nothing to replay.")

    # Checked against EVERY source: a multi-sheet recipe's "sheets" array
    # names them all, a one-sheet recipe's "sheet" names the one.
    if "sheets" in recipe:
        sheets = [entry.get("sheet", "") for entry in recipe["sheets"]]
    else:
        sheets = [recipe.get("sheet", "")]
    for sheet in sheets:
        if not os.path.isfile(sheet):
            sys.exit(
                f"{path} was cut from '{sheet}', and that file is not there. "
                "A recipe without its source sheet cannot be replayed -- recover the sheet "
                "from git rather than slicing something else under the same id."
            )

    recorded = recipe.get("toolSha256")
    if recorded and recorded != _sha256(os.path.abspath(__file__)):
        print(f"  NOTE: {os.path.basename(path)} was written by a different version of this tool "
              f"({recorded[:12]} vs {_sha256(os.path.abspath(__file__))[:12]}). "
              "A byte-identical result is no longer guaranteed.")

    return argv, recipe


# ---------------------------------------------------------------------------
# The ground line, handed over instead of read off a terminal
# ---------------------------------------------------------------------------

def _normalise_actor_path(path):
    """The same comparison Domain/Stage/StanceManifest.cs makes: stray slashes
    and casing are the difference between a path typed into JSON and one built
    by concatenation, and neither decides whether a golem stands on the floor.
    """
    return path.replace("\\", "/").strip().strip("/").lower()


def update_stance_manifest(actor_label, ground_line, manifest_path=STANCE_MANIFEST, verbose=True):
    """Record the measured ground line, or explain why it was not recorded.

    Returns one of "written", "created", "unchanged", "authored", "absent" --
    for the caller's report and for tests.

    READ-MODIFY-WRITE WITH THE KEY ORDER KEPT. The file carries a long
    `_comment` and per-actor `_groundLineNote`s that are the whole reason
    anybody trusts the numbers; a rewrite that reordered or dropped them would
    make every run of this tool a diff nobody wants to read. Entries are
    mutated in place inside an OrderedDict, so an unchanged actor is
    byte-unchanged, and a run that changes nothing does not open the file for
    writing at all.
    """
    if not os.path.isfile(manifest_path):
        print(f"  NOTE: no stance manifest at {manifest_path} -- groundLine {ground_line} not recorded")
        return "absent"

    with open(manifest_path, encoding="utf-8") as handle:
        manifest = json.load(handle, object_pairs_hook=collections.OrderedDict)

    wanted = _normalise_actor_path(actor_label)
    entry = next(
        (a for a in manifest.get("actors", [])
         if _normalise_actor_path(str(a.get("spritePath", ""))) == wanted),
        None,
    )

    if entry is None:
        manifest.setdefault("actors", []).append(collections.OrderedDict([
            ("spritePath", actor_label),
            ("groundLine", ground_line),
            ("groundLineSource", SOURCE_SLICER),
        ]))
        _write_manifest(manifest_path, manifest)
        print(f"  StanceManifest.json: added {actor_label} groundLine {ground_line} (groundLineSource slicer)")
        return "created"

    source = str(entry.get("groundLineSource", SOURCE_AUTHORED)).strip().lower()
    previous = entry.get("groundLine", 0)

    if source != SOURCE_SLICER:
        delta = ground_line - previous
        print(f"  StanceManifest.json: LEFT ALONE. {actor_label} authors groundLine {previous}; "
              f"this run measures {ground_line} (delta {delta:+}). groundLineSource is "
              f"'{source}', so the authored number stands -- change it by hand, or set "
              f"groundLineSource to 'slicer' if the measurement should own it.")
        return "authored"

    if previous == ground_line:
        if verbose:
            print(f"  StanceManifest.json: {actor_label} groundLine {ground_line} unchanged")
        return "unchanged"

    entry["groundLine"] = ground_line
    _write_manifest(manifest_path, manifest)
    print(f"  StanceManifest.json: {actor_label} groundLine {previous} -> {ground_line}")
    return "written"


def _write_manifest(manifest_path, manifest):
    with open(manifest_path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")


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


def _cut_one_sheet(spec, label, drop_far_px, verbose, seen_names):
    """One sheet's tight-cropped native-size pieces per named cell -- the old
    single-sheet Pass 0, unchanged in what it does per cell. `seen_names` is
    shared across every sheet an actor draws from, so a stance name repeated
    on a second sheet is caught here rather than silently overwriting the
    first sheet's piece of that name later.

    Returns [(name, piece_RGBA, mask_bool), ...].
    """
    sheet_path = spec["sheet"]
    if "/Resources/" in os.path.abspath(sheet_path).replace("\\", "/"):
        sys.exit(f"Refusing to read from a Resources/ path (would compound a previous pass): {sheet_path}")
    if not os.path.isfile(sheet_path):
        sys.exit(f"Missing source sheet: {sheet_path}")

    grid_arg = spec["grid"]
    key_mode = spec["key"]
    pocket_max_area = spec["pocket_max_area"]
    delivery_scale = spec["delivery_scale"]
    rows, cols = parse_grid(grid_arg)
    names = [s.strip() for s in spec["stances"].split(",")]
    if len(names) != rows * cols:
        sys.exit(f"--stances has {len(names)} name(s) but the {grid_arg} grid has {rows * cols} cell(s) "
                  f"(sheet={sheet_path})")

    print(f"[{label}] sheet={sheet_path} grid={grid_arg} key={key_mode} "
          f"delivery_scale={delivery_scale}")

    keyed = key_sheet(Image.open(sheet_path), key_mode, pocket_max_area)
    mask = opaque_mask(keyed)
    boxes = cut_cells(mask, keyed.width, keyed.height, rows, cols)

    pieces = []  # [(name, piece_RGBA, mask_bool)]
    for i, box in enumerate(boxes):
        name = names[i] if i < len(names) else None
        if name is None or name.lower() in SKIP_TOKENS:
            continue
        if name in seen_names:
            sys.exit(f"[{label}]: stance '{name}' is named by more than one sheet -- "
                      "names must be unique across every --sheet an actor draws from.")
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

        if verbose:
            core = largest_connected_component(mask_bool)
            print(f"  '{name}': content {piece.width}x{piece.height}  "
                  f"sqrt(LCC-mass)={np.sqrt(core.sum()):.1f}")
        seen_names.add(name)
        pieces.append((name, piece, mask_bool))

    return pieces


def process(sheet_specs, actor_arg, anchor_mode, nudges, drop_far_px, out_root, prune, verbose,
            max_ground_spread=6):
    """`sheet_specs`: ordered list of dicts, each `{sheet, grid, stances, key,
    pocket_max_area, delivery_scale}` -- one entry per `--sheet` on the
    command line, in order. A one-sheet actor passes a one-entry list; this
    function does not otherwise know or care how many sheets an actor has,
    because everything past Pass 0 already worked over a flat list of named
    pieces regardless of which sheet cut them.
    """
    out_dir, label = parse_actor(actor_arg, out_root)
    print(f"[{label}] anchor={anchor_mode} -> {out_dir}")

    # Pass 0: every sheet's named cells, gathered into one flat list. Order
    # across sheets does not matter to anything downstream -- canvas size,
    # ground line and anchor are all computed as a max/median over the whole
    # list, not sheet by sheet.
    seen_names = set()
    pieces = []  # [(name, piece_RGBA, mask_bool)]
    for spec in sheet_specs:
        pieces.extend(_cut_one_sheet(spec, label, drop_far_px, verbose, seen_names))

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
    # A stance nudged UP (negative dy -- a deliberate hover) still needs
    # its full height above the ground row, or the canvas clips its crown
    # by the nudge. Room is made here so the printed groundLine stays what
    # it would be without the hover: canvas bottom to the ground row.
    max_above += max([0] + [-nudges.get(name, (0, 0))[1] for name, _, _, _ in footed])
    max_below = max(p.height - 1 - foot_y for _, p, _, foot_y in footed)
    canvas_h = int(max_above + max_below) + PADDING * 2
    ground_y = PADDING + int(max_above)
    ground_line = canvas_h - ground_y
    print(f"  groundLine {ground_line}")

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

    _assert_one_ground_line(label, out_dir, written, verbose, max_ground_spread)
    _report_stray_files(out_dir, written, prune, verbose)

    # AFTER the art is on disk and the one-ground-line check has passed, not
    # before: a run that dies partway must not leave a ground line recorded for
    # frames that were never written.
    if os.path.normpath(out_root) == os.path.normpath(DEFAULT_OUT_ROOT):
        update_stance_manifest(label, ground_line, verbose=verbose)
    elif verbose:
        print(f"  (--out-root is not {DEFAULT_OUT_ROOT}; groundLine {ground_line} not recorded)")

    return written, ground_line, out_dir


def _expand_per_sheet(values, default, n, flag_name):
    """`values` is what argparse collected for a flag that MAY be repeated
    once per `--sheet`. Zero occurrences means "use the default for every
    sheet" (the one-sheet-actor behaviour, unchanged); one occurrence means
    "use this value for every sheet" (most multi-sheet actors want one
    `--key`/`--pocket-max-area` even with two sheets); `n` occurrences means
    one value per sheet, in order. Anything else is ambiguous and refused
    rather than guessed at.
    """
    if not values:
        return [default] * n
    if len(values) == 1:
        return values * n
    if len(values) == n:
        return list(values)
    sys.exit(f"--{flag_name} was given {len(values)} time(s) but there are {n} --sheet "
              f"entries: give it once (applies to every sheet) or once per --sheet.")


# Flags a replay may still honour explicitly. Both change WHERE the run
# writes, never WHAT it cuts -- `--out-root` is the scratch-vs-real switch
# this module's "Nothing is recorded when --out-root points somewhere other
# than the real Resources tree" guard depends on, and `--quiet` is output
# verbosity. Every other flag is part of the recorded cut (sheet, grid,
# stances, key, anchor, delivery-scale, nudges, drop-far-components-px,
# max-ground-spread, pocket-max-area, prune) and is refused alongside
# --recipe rather than silently discarded -- see the replay block in main().
REPLAY_OVERRIDABLE_FLAGS = {"--out-root", "--quiet"}


def _explicit_flags(argv_tokens):
    """The `--flag` names actually typed in `argv_tokens` (not their values).

    Covers both `--foo value` and `--foo=value` spellings. Used only to see
    what a human typed on the real command line -- never to parse a recipe's
    own recorded argv, which is trusted wholesale.
    """
    return {tok.split("=", 1)[0] for tok in argv_tokens if tok.startswith("--")}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--recipe", default=None, metavar="PATH",
                    help="Replay a recipe.json this tool wrote. Every other argument comes from "
                         "the file, so what runs is the invocation that produced the committed "
                         "art rather than a reconstruction of it. Refuses if the source sheet "
                         "named in the recipe is missing.")
    ap.add_argument("--sheet", action="append", default=[],
                    help="Path to a Stage-1 design sheet. Repeatable -- see 'More than one sheet, "
                         "one actor' in this file's module docstring -- with --grid/--stances/--key/"
                         "--pocket-max-area/--delivery-scale each given once per --sheet, or once for "
                         "all of them.")
    ap.add_argument("--actor", help="'Enemies/<id>' or 'Characters/<id>'. One per actor, whatever "
                                    "the number of sheets.")
    ap.add_argument("--stances", action="append", default=[],
                    help="Comma-separated stance names for one --sheet, row-major, one per grid "
                         "cell. Use '-' to skip a cell. Exactly one --stances per --sheet -- there "
                         "is no default, because the cells differ every time.")
    ap.add_argument("--grid", action="append", default=[],
                    help="COLSxROWS for one --sheet (default 3x2 if omitted entirely). Once per "
                         "--sheet, or once for all of them.")
    ap.add_argument("--key", action="append", default=[], choices=("alpha", "white_flood", "green"),
                    help="Keying mode for one --sheet (default alpha if omitted entirely). Once per "
                         "--sheet, or once for all of them.")
    ap.add_argument("--anchor", default="ground_band", choices=("ground_band", "centroid"),
                    help="Per actor, not per sheet -- how every stance from every sheet is anchored.")
    ap.add_argument("--delivery-scale", action="append", default=[], type=float,
                    help="Uniform multiplier for one --sheet's pieces, applied after native-scale "
                         "cropping (default 1.0 if omitted entirely). THE reason a second sheet needs "
                         "its own value: a second AI generation of the same subject is drawn at its "
                         "own pixel size, never the first sheet's. Once per --sheet, or once for all "
                         "of them -- rarely useful shared, since this is the one thing that usually "
                         "differs sheet to sheet.")
    ap.add_argument("--nudge", action="append", metavar="STANCE:DX,DY", default=[],
                    help="Per actor -- a stance name is unique across every sheet, so one flat list "
                         "covers all of them.")
    ap.add_argument("--drop-far-components-px", type=int, default=None,
                    help="Per actor, applied to every sheet's pieces alike.")
    ap.add_argument("--max-ground-spread", type=int, default=6, metavar="PX",
                    help="How far apart the stances' lowest rows may sit before the one-ground-line "
                         "check refuses the output (default 6). Raise it ONLY for a deliberate hover: an "
                         "actor whose airborne stances are nudged up while its defeated pose stays on the "
                         "floor. The accidental float the default catches is still caught for everyone else.")
    ap.add_argument("--pocket-max-area", action="append", default=[], type=int,
                    help="white_flood only: largest enclosed neutral-bright pocket (px) still keyed "
                         "to transparent for one --sheet (default 200 if omitted entirely). The 200px "
                         "default catches specks; a sheet whose art closes around a real hole of "
                         "checkerboard (the treant's roots enclose ~1500px) needs this raised for that "
                         "one sheet. Once per --sheet, or once for all of them.")
    ap.add_argument("--out-root", default=DEFAULT_OUT_ROOT)
    ap.add_argument("--prune", action="store_true")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    # A REPLAY IS THE RECORDED RUN, not a run that borrows some of it. Every
    # cut-affecting argument is re-parsed out of the recipe, so nothing typed
    # alongside --recipe can quietly change what the recipe claims to
    # reproduce. This is the ONE code path for both a one-sheet and a
    # multi-sheet recipe: a recipe's "argv" is always the full repeated
    # --sheet/--grid/--stances/--key/--pocket-max-area/--delivery-scale
    # flags, one --sheet occurrence per source, so parsing it here needs no
    # shape-specific branch at all.
    #
    # --out-root/--quiet are the exception: they choose WHERE the replay
    # writes, not WHAT it cuts, so an explicit one typed alongside --recipe
    # is honoured rather than discarded -- a verification run needs to be
    # able to land in scratch. Anything else typed alongside --recipe would
    # silently change the cut a moment ago, so it is refused instead.
    replaying = False
    if args.recipe:
        typed_out_root, typed_quiet = args.out_root, args.quiet
        disallowed = _explicit_flags(sys.argv[1:]) - {"--recipe"} - REPLAY_OVERRIDABLE_FLAGS
        if disallowed:
            ap.error(
                "--recipe replays the exact run that produced the committed art; "
                + ", ".join(sorted(disallowed)) + " would change the CUT, not just "
                "where it lands, and cannot be combined with --recipe. Only "
                + "/".join(sorted(REPLAY_OVERRIDABLE_FLAGS)) + " (output location/verbosity) "
                "may be overridden on replay. Drop --recipe to run with new arguments instead."
            )
        argv, _ = load_recipe(args.recipe)
        print(f"[replay] {args.recipe}")
        args = ap.parse_args(argv)
        # Explicit CLI overrides win; otherwise these are just the parser's
        # own defaults, which is what the recipe implies anyway, so
        # restoring them here is always safe -- never a change from what a
        # replay with no --out-root/--quiet already did.
        args.out_root, args.quiet = typed_out_root, typed_quiet
        replaying = True

    if not args.sheet:
        ap.error("--sheet is required (at least one; or give --recipe, which carries it)")
    if not args.actor:
        ap.error("--actor is required (or give --recipe, which carries it)")

    n = len(args.sheet)
    if len(args.stances) != n:
        ap.error(f"--stances must be given exactly once per --sheet ({n} --sheet entries, "
                  f"got {len(args.stances)} --stances)")

    grids = _expand_per_sheet(args.grid, "3x2", n, "grid")
    keys = _expand_per_sheet(args.key, "alpha", n, "key")
    pocket_max_areas = _expand_per_sheet(args.pocket_max_area, POCKET_MAX_AREA, n, "pocket-max-area")
    delivery_scales = _expand_per_sheet(args.delivery_scale, 1.0, n, "delivery-scale")

    sheet_specs = [
        {
            "sheet": args.sheet[i],
            "grid": grids[i],
            "stances": args.stances[i],
            "key": keys[i],
            "pocket_max_area": pocket_max_areas[i],
            "delivery_scale": delivery_scales[i],
        }
        for i in range(n)
    ]

    _, ground_line, out_dir = process(
        sheet_specs=sheet_specs,
        actor_arg=args.actor,
        anchor_mode=args.anchor,
        nudges=parse_nudges(args.nudge),
        drop_far_px=args.drop_far_components_px,
        max_ground_spread=args.max_ground_spread,
        out_root=args.out_root,
        prune=args.prune,
        verbose=not args.quiet,
    )

    # Not on a replay (the argv it would write is the argv it just read, so the
    # only change would be the timestamp -- a verification run that dirties the
    # tree is one nobody runs twice), and not for art written to scratch.
    if replaying:
        print("  recipe: unchanged (this was a replay)")
    elif os.path.normpath(args.out_root) == os.path.normpath(DEFAULT_OUT_ROOT):
        write_recipe(sheet_specs, args.actor, args.anchor, args.max_ground_spread,
                     args.nudge, args.drop_far_components_px, args.prune, ground_line, out_dir)
    else:
        print(f"  (--out-root is not {DEFAULT_OUT_ROOT}; no recipe written)")


if __name__ == "__main__":
    main()
