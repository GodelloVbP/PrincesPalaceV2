#!/usr/bin/env python3
"""Cut a bind-pose creature sheet into a rigged part atlas + skeleton.

Companion to `slice_actor_sheet.py`: that tool turns a flat pose sheet into
independent per-stance PNGs (no shared geometry between frames). This one
turns a SINGLE bind-pose drawing into a set of movable parts with a bone
hierarchy and a triangulated, weighted mesh per part -- the input a
skeletal/2D-mesh animation runtime (Spine-, DragonBones- or Unity
2D-Animation-style) actually wants, as opposed to the pre-baked flipbook
frames `slice_actor_sheet.py` produces.

## Where this came from

The cut/rig approach below is a direct generalisation of a prototype built
against one creature (the giant rat's splayed bind pose) in
`ratcut2/{01_key,02_cut,03_qa,04_anim}.py`. That prototype proved: green-key
+ border-flood-fill background removal; leg segmentation as connected
components of the silhouette below a "belly line"; a region-based (not
polygon) tail cut, because nothing else lives in the tail's corner of the
frame; a hand-drawn head polygon; and body-as-remainder with a dilated,
nearest-colour "seam backing" so the parts can move apart without showing a
hole. All of that generalises cleanly to *any* creature's manifest entry
-- see "What's data vs what's code" below -- except the actual polygon
points and thresholds, which are drawing-specific by nature and were always
going to live in a manifest, the same way `slice_actor_sheet.py`'s pixel
grid cuts do.

### What's data vs what's code

Rejected as accidentally-hardcoded rather than genuinely rat-specific, and
promoted into the `RIGS` manifest below:
  - the source canvas size (computed from the keyed, cropped image, never
    stored) and the belly-line Y / leg search X (manifest `legs.search`);
  - "exactly 4 legs" -- generalised to `len(legs.names)`, so a 6-legged
    insect or a legless snake (`legs` entry simply omitted) is still one
    manifest entry, not a code change;
  - every polygon, pivot and threshold (head outline, tail region clauses,
    per-part pivots, z-order, bone parents) -- these ARE genuinely specific
    to one creature's drawing and belong in data, same as
    `slice_actor_sheet.py`'s grid/band cuts are specific to one sheet.

Kept as code because it is genuinely generic across any creature: green /
white-flood / alpha keying, connected-component labelling, region and
polygon rasterising, dilation-based seam backing, z-order overlap
resolution, Moore-neighbour contour tracing, Douglas-Peucker simplification,
ear-clip triangulation, rigid vertex weighting, and shelf atlas packing.

## No scipy

`slice_actor_sheet.py` deliberately reimplements connected-component
labelling without scipy (see its own comment on that function) rather than
add a dependency the rest of `tools/` doesn't take. The ratcut2 prototype
DID use `scipy.ndimage` (binary_propagation, binary_dilation,
distance_transform_edt) -- fine for a throwaway script, not for a committed
tool. Every one of those calls is reimplemented here in pure numpy: labelling
is a row-run union-find (ported from `slice_actor_sheet`'s
`largest_connected_component`, generalised to return every component rather
than just the largest); dilation is N iterations of a 4-neighbour OR; the
seam-backing "nearest opaque colour" fill is an outward colour-propagating
wavefront for exactly `backing_px` iterations, which is what bounded-radius
`distance_transform_edt` nearest-fill reduces to for this use (filling a
~16px backing ring, not a true unbounded nearest-neighbour field).

## Output

    Assets/_Project/Resources/Rigs/<root>/<id>/atlas.png   -- packed parts, RGBA, 4px pad
    Assets/_Project/Resources/Rigs/<root>/<id>/rig.json    -- schema below

    {
      "referenceHeightPx": <int>,        # whole-silhouette content height, keyed+cropped
      "bones": [
        {"name": str, "parent": str|null, "x": int, "y": int}   # source-image px, root at the feet
        ...
      ],
      "parts": [
        {
          "name": str, "bone": str, "z": int,        # z is paint order, back to front
          "rect": [x, y, w, h],                       # atlas space
          "pivot": [x, y],                             # atlas-local (relative to this part's own rect)
          "outline": [[x, y], ...],                     # simplified polygon, atlas-local, CCW
          "triangles": [i, i, i, ...],                  # indices into "outline"
          "weights": [[{"boneIndex": int, "weight": 1.0}], ...]   # one list per outline vertex
        },
        ...
      ]
    }

Rigid weighting only for this pilot: every vertex of a part is 100% bound to
that part's own bone. Multi-bone blending (needed once the tail becomes a
bone chain instead of one rigid piece, per the ratcut2 SKELETAL prototype in
04_anim.py) is a `weights` list with more than one entry per vertex, so the
schema doesn't need to change to grow into that -- only this tool's rigid
`weights_for(...)` does.

## Safety

- Refuses to read a source under `Assets/_Project/Resources/` (same guard as
  `slice_actor_sheet.py` -- processing already-processed output compounds
  resample loss).
- Output root defaults to `Assets/_Project/Resources/Rigs` and can be
  redirected wholesale with `--output-root <path>` or the
  `RIG_ACTOR_OUTPUT_ROOT` env var (CLI wins if both given) -- the escape
  hatch a draft/test run uses to never touch the real project tree.
- `--source <path>` overrides the manifest's source image for one run. It
  exists for drafting a manifest entry before its source art has been
  committed under `Assets/_Project/Art/Rigs/<id>/` -- once that file exists,
  drop the flag and let the manifest be the single source of truth, the same
  way `slice_actor_sheet.py`'s ACTORS entries are.
- Never adds/removes an output filename beyond {atlas.png, rig.json} without
  `--prune` -- same convention as `slice_actor_sheet.py`.

## Usage

    python tools/rig_actor.py rat
    python tools/rig_actor.py --all
    python tools/rig_actor.py --preview rat
    python tools/rig_actor.py rat --prune
    python tools/rig_actor.py rat --output-root "C:/scratch/rigs_test" --source "C:/path/to/bind_pose.png"
"""

import argparse
import json
import math
import os
import sys

try:
    import numpy as np
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    sys.exit("Pillow and numpy are required: pip install Pillow numpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from remove_portrait_backgrounds import flood_fill_background_mask
from sheet_slicing import ALPHA_THRESHOLD, PADDING as SHEET_PADDING  # noqa: F401  (PADDING kept for parity reference)


# ---------------------------------------------------------------------------
# Roots / paths
# ---------------------------------------------------------------------------

RIGS_OUTPUT_BASE = "Assets/_Project/Resources/Rigs"
ROOT_ENEMIES = "Enemies"
ROOT_CHARACTERS = "Characters"
SOURCE_DIR_DEFAULT = "Assets/_Project/Art/Rigs"

# Default atlas padding (px) between packed parts.
ATLAS_PADDING_DEFAULT = 4
# Simplified-outline max deviation, in source-image px.
#
# Used to be a vertex-COUNT target band, searched by adjusting epsilon until
# the count landed inside it. That was chasing the wrong variable: a count
# is only a proxy for "how far can the simplified line stray from the true
# silhouette," and a large, mostly-simple part (body) could land inside its
# target band while still bulging several px past its own true edge in one
# locally-complex spot -- invisible to the global vertex count, and to the
# fidelity check's global IoU too (a few hundred stray px is nothing against
# a 100,000+ px part), but highly visible wherever that bulge lands on top
# of a smaller, differently-coloured neighbour. Found exactly this way: body
# passed its IoU threshold at 0.9977 while still painting a visible dark
# band over far_hindleg's hip; confirmed by isolating the leg alone and
# seeing it render perfectly clean with nothing else on.
#
# epsilon is what Douglas-Peucker actually guarantees: no point on the
# simplified outline deviates from the true traced contour by more than
# epsilon, in EITHER direction (undershoot -- clipping a real feature like
# the fangs -- or overshoot -- bulging into a neighbour). Setting epsilon
# directly bounds both failure modes with one number instead of chasing them
# with two different band-aids (raising a vertex ceiling for undershoot,
# shrinking backing_px for overshoot).
SIMPLIFY_EPSILON_DEFAULT = 1.5


# ---------------------------------------------------------------------------
# RIGS manifest
# ---------------------------------------------------------------------------
#
# One entry = one creature. Everything inside is data: geometry, thresholds,
# z-order, bone parentage. See the module docstring's "What's data vs what's
# code" for what was promoted out of the ratcut2 prototype to get here.

RIGS = {
    "rat": {
        # "file" is resolved against SOURCE_DIR_DEFAULT unless --source
        # overrides it wholesale for a draft run (see module docstring).
        # Not yet committed -- this pilot's bind pose is still the reference
        # image supplied for the ratcut2 prototype, not art living in the
        # repo. Wire the real path once it is.
        "file": "rat/bind_pose.png",
        "root": ROOT_ENEMIES,
        "key": "green",
        # Padding (px) added on all four sides when cropping the keyed
        # image to its opaque content, before any part geometry below is
        # evaluated. Every point/threshold in this entry is expressed in
        # THAT cropped image's coordinate frame -- ratcut2/01_key.py's own
        # crop, reproduced exactly (see key_green()).
        "content_pad": 8,

        # Explicit per-part pivots, in the cropped source frame. Not needed
        # for "legs" (procedural rule below) or for parts with no natural
        # single point (there are none in this pilot).
        # 20, not 40 and not 16. 16 left a visible seam gap (dilate_px=4 on
        # the legs was outrunning it). 40 overcorrected: body draws ON TOP
        # of the two FAR legs (z=0,1, below body's z=3), and backing_px
        # dilates in every direction by exactly that many px -- so 40
        # reached 40px into far_hindleg's own hip territory and painted
        # body's dark fur over what should have been visible leg colour,
        # confirmed by measuring the actual mask overlap (4870px, y-extent
        # ~40px -- matches backing_px almost exactly, not a coincidence).
        # 20 nets ~16px of reach past dilate_px=4, comfortably more than
        # the few px the seam needed, without reaching deep enough into a
        # far leg's own territory to visibly paint over it.
        # 20 still overshot into the far legs (2932px overlap, still a
        # visible if smaller dark band -- confirmed against a real Unity
        # render, not just the offline coverage number, which reports
        # gap=0 at every value tried here and so cannot be used alone to
        # pick this). The "40 was needed" finding was measured under a
        # DIFFERENT dilate_px (2, then 10) earlier in this file's history;
        # never directly re-tested at the current dilate_px=4. The actual
        # render-time margin needed is sub-pixel rounding in bone/pivot
        # placement, not tens of pixels -- trying a much smaller value.
        # 10 still showed a visible (smaller) dark band -- confirmed against
        # the TRUE source art at that exact spot (source_hindleg_exact.png):
        # completely uniform fur colour there, no natural shading crease at
        # all, so this is 100% a pipeline artifact, not art being
        # faithfully reproduced. Going to 6, just 2px past dilate_px=4 --
        # deliberately close to the theoretical floor, to find where the
        # real trade-off boundary is rather than keep bisecting one step
        # at a time.
        # Still banded at backing_px=6, and the raw mask-overlap number
        # (921px, ~5px tall) never matched the rendered band's actual
        # height (~60px) -- the gap between those two numbers was the tell
        # that backing_px wasn't the real variable. body's SIMPLIFIED
        # outline could bulge past its own dilated mask by more than the
        # count-targeted simplify_closed ever bounded, independent of how
        # small backing_px got. See simplify_epsilon: bounding Douglas-
        # Peucker deviation directly is the actual fix; backing_px stays
        # at the floor found here and should be re-verified, not re-grown,
        # once epsilon-bounded output is confirmed clean.
        "body": {"pivot": (820, 760), "backing_px": 6},
        "parts": {
            "head": {
                # Traces up the near ear's outer edge to its tip, down into
                # the gap between the ears, up the far ear's outer edge to
                # its tip, THEN CONTINUES all the way around the actual face
                # -- forehead, nose bridge, nose tip, under-nose, both fang
                # tips with the gap between them, jaw line, throat -- back to
                # the near ear's base.
                #
                # The second draft closed early at (1245,80)->(1335,175)->
                # (1400,235)->(1360,300), which stops around y=235-315 --
                # short of the nose tip (~y=320-350) and WAY short of the
                # fang tips (~y=455-465). That meant the ENTIRE snout, nose,
                # jaw and both teeth were never part of head's mask at all --
                # they got claimed by body's "whatever's left" remainder
                # instead. Static bind pose hid this completely (body and
                # head sit in their correct relative positions at rest); it
                # would have shown up the instant the head bone rotated in
                # an actual animation, as the whole face staying behind with
                # the torso while an eyeless, mouthless cranium swung away.
                # No amount of outline-simplification vertex budget could
                # have fixed the teeth from the head side, because they were
                # never head's pixels to simplify -- confirmed by rendering
                # the polygon directly over the source art and watching the
                # boundary cut across the middle of the face nowhere near the
                # mouth. Verified against ratcut2/rat_grid.png for the ears
                # (near ear x=1005-1110,y=0-140; far ear x=1120-1215,y=0-105)
                # and against head_region_grid2.png (this project's own
                # bind_pose.png, gridded 1000..1460 x 0..480) for everything
                # from the forehead down through the fangs.
                "kind": "polygon",
                "points": [
                    (1078, 205), (1055, 130), (1015, 55), (1035, 5), (1078, 10),
                    (1092, 80), (1108, 25), (1150, 3), (1200, 20), (1213, 85),
                    (1245, 80), (1290, 95), (1345, 140), (1395, 220), (1440, 300),
                    (1448, 340), (1430, 375), (1415, 395), (1400, 455), (1378, 425),
                    (1355, 463), (1330, 428), (1290, 410), (1200, 398), (1100, 388),
                    (1030, 375), (1000, 340), (1030, 290), (1070, 260), (1110, 245),
                ],
                # Found by verify_rig, not by eye: a hand-traced polygon
                # sits a few px inside the true edge almost everywhere, not
                # just at one bad corner -- see the comment on this option's
                # implementation in build_rig for the two disconnected
                # slivers (far ear tip, nose bridge) this specifically
                # closed, which a bigger vertex budget could never fix
                # (trace_contour only ever follows ONE, the LARGEST,
                # connected component -- a disconnected island is
                # structurally invisible to it regardless of simplify epsilon).
                "outset_px": 8,
                "pivot": (1080, 290),
            },
            "tail": {
                # Region, not polygon: nothing else in the drawing lives in
                # the lower-left, so "left of the legs, plus the wedge above
                # the rear leg" fully separates the tail with no hand-traced
                # outline needed. `clauses` is OR-of-AND over (axis, op,
                # value) triples against the pixel coordinate grid.
                #
                # The corner (520,472) used to sit tight against the real
                # fur-to-tail transition, which is a curve, not a right
                # angle -- wherever the true silhouette crossed outside that
                # rigid rectangle, region membership just stopped, showing up
                # as a flat notched step where the tail meets the back
                # instead of a smooth continuation. Pushed out to (560,500):
                # still 16px clear of the belly line (516) where leg
                # detection starts, and 108px right of far_hindleg's own
                # search bound (x_min 452) -- legs are found strictly below
                # y=516 by connected components, so this corner never
                # actually overlaps a leg pixel even though its x-range
                # does, and the rank-based resolve (tail loses to body/near
                # legs/head, wins over far legs) is unchanged either way.
                "kind": "region",
                "clauses": [
                    [("x", "<", 450)],
                    [("x", "<", 560), ("y", "<", 500)],
                ],
                "pivot": (500, 398),
            },
            "legs": {
                # Connected components of the silhouette below the belly
                # line and right of the tail, sorted left -> right and
                # assigned these names in order. A creature with a
                # different leg count just lists a different-length
                # `names` -- nothing else about this tool changes.
                "kind": "components",
                "search": {"y_min": 516, "x_min": 452},
                "min_blob_px": 400,
                "names": ["far_hindleg", "near_hindleg", "far_foreleg", "near_foreleg"],
                # Hip pivot = mean X of the topmost `band_px` rows of the
                # blob, `dy` px below the top -- "where the leg meets the
                # body", not the blob's bounding-box corner.
                "pivot_rule": {"band_px": 12, "dy": 4},
            },
        },

        # Paint order, back to front. Doubles as the z-resolve priority for
        # pixel overlap between parts (a later entry wins the overlap) and
        # as the "z" field written per part in rig.json.
        "order": [
            "far_hindleg", "far_foreleg", "tail", "body",
            "near_hindleg", "near_foreleg", "head",
        ],

        # 2px growth so a moving part owns its own outline pixels instead of
        # ending exactly where the body silhouette does (leaves a hairline
        # gap the instant it rotates away from bind pose). Tail excluded --
        # it is not a separate silhouette carved out of the body the way a
        # limb is, so growing it would eat into the body itself.
        # Kept modest -- growing this ALSO shrinks body_mask (body is defined
        # as "whatever's left after removing dilated legs/head"), which
        # eats into body's own backing_px margin at the same rate it grows
        # the legs'. Bumping this alone doesn't close a seam; see body's
        # backing_px below, which is the side actually worth growing.
        "dilate_px": 4,
        "dilate_skip": ["tail"],

        "simplify_epsilon": SIMPLIFY_EPSILON_DEFAULT,
        "atlas_padding": ATLAS_PADDING_DEFAULT,

        # Bone parent per non-root part. Exactly one part must be absent
        # here -- that part is the root. Rigid pilot: bone name == part
        # name for every part (a part's `bone` field, only needed the day a
        # part is weighted to a bone it doesn't own, defaults to its own
        # name).
        "bone_parents": {
            "head": "body", "tail": "body",
            "far_hindleg": "body", "near_hindleg": "body",
            "far_foreleg": "body", "near_foreleg": "body",
        },
    },
}


# ---------------------------------------------------------------------------
# Connected components -- row-run union-find, no scipy. Generalises
# slice_actor_sheet.largest_connected_component to return EVERY component
# (legs needs all four blobs, not just the biggest one).
# ---------------------------------------------------------------------------

def label_components(mask_bool):
    """4-connected component labels for a boolean 2D array.

    Returns (labels int32 array, count). Label 0 is background; components
    are 1..count in no particular order.
    """
    h = mask_bool.shape[0]
    parent = []

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    provisional = np.zeros(mask_bool.shape, dtype=np.int32)
    prev_runs = []  # [((x0,x1), gid), ...] for the previous row
    for y in range(h):
        row = mask_bool[y]
        if not row.any():
            prev_runs = []
            continue
        padded = np.concatenate(([False], row, [False]))
        edges = np.flatnonzero(padded[1:] != padded[:-1])
        runs = [(int(edges[i]), int(edges[i + 1])) for i in range(0, len(edges), 2)]
        ids = []
        for (x0, x1) in runs:
            gid = len(parent)
            parent.append(gid)
            ids.append(gid)
            provisional[y, x0:x1] = gid + 1
        for (x0, x1), gid in zip(runs, ids):
            for (px0, px1), pgid in prev_runs:
                if px0 < x1 and x0 < px1:
                    union(gid, pgid)
        prev_runs = list(zip(runs, ids))

    if not parent:
        return np.zeros(mask_bool.shape, dtype=np.int32), 0

    roots = np.array([find(i) for i in range(len(parent))], dtype=np.int64)
    uniq, remap = np.unique(roots, return_inverse=True)
    lut = np.zeros(len(parent) + 1, dtype=np.int32)
    lut[1:] = remap.astype(np.int32) + 1
    labels = lut[provisional]
    return labels, int(len(uniq))


def largest_component(mask_bool):
    labels, count = label_components(mask_bool)
    if count == 0:
        return np.zeros_like(mask_bool)
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    best = int(np.argmax(sizes))
    return labels == best


# ---------------------------------------------------------------------------
# Shift / dilate / nearest-fill -- pure numpy replacements for the
# scipy.ndimage calls the ratcut2 prototype used. See module docstring.
# ---------------------------------------------------------------------------

def _shift(arr, dy, dx, pad_value):
    """arr shifted so that out[y, x] == arr[y - dy, x - dx] (0 outside)."""
    out = np.full_like(arr, pad_value)
    ys = slice(max(0, dy), arr.shape[0] + min(0, dy))
    ysrc = slice(max(0, -dy), arr.shape[0] + min(0, -dy))
    xs = slice(max(0, dx), arr.shape[1] + min(0, dx))
    xsrc = slice(max(0, -dx), arr.shape[1] + min(0, -dx))
    out[ys, xs] = arr[ysrc, xsrc]
    return out


_FOUR_NEIGHBOURS = [(1, 0), (-1, 0), (0, 1), (0, -1)]


def binary_dilation(mask_bool, iterations):
    out = mask_bool.copy()
    for _ in range(max(0, iterations)):
        grown = out.copy()
        for dy, dx in _FOUR_NEIGHBOURS:
            grown |= _shift(out, dy, dx, False)
        out = grown
    return out


def nearest_fill(rgb_u8, known_mask, iterations):
    """Propagate rgb_u8 colour outward from `known_mask` by up to
    `iterations` px (4-connected wavefront). Returns (filled_rgb,
    filled_known) -- filled_known is known_mask grown by the same wavefront,
    i.e. exactly what `iterations` rounds of binary_dilation would produce,
    so callers can intersect it with alpha the same way as a real dilation.
    """
    filled_rgb = rgb_u8.copy()
    filled_known = known_mask.copy()
    for _ in range(max(0, iterations)):
        unknown = ~filled_known
        newly = np.zeros_like(filled_known)
        for dy, dx in _FOUR_NEIGHBOURS:
            src_known = _shift(filled_known, dy, dx, False) & unknown & ~newly
            if not src_known.any():
                continue
            src_rgb = _shift(filled_rgb, dy, dx, 0)
            filled_rgb[src_known] = src_rgb[src_known]
            newly |= src_known
        if not newly.any():
            break
        filled_known = filled_known | newly
    return filled_rgb, filled_known


# ---------------------------------------------------------------------------
# Keying
# ---------------------------------------------------------------------------

def key_green(rgb_im):
    """Hue-dominance green-key + border flood-fill + spill suppression.

    Reproduces ratcut2/01_key.py exactly (vectorised, and the border flood
    fill done as "components touching the border" instead of
    scipy.ndimage.binary_propagation -- see module docstring). Kept
    separate from tools/key_green_screen.py's key_out_green(), which uses a
    different threshold/ramp: this pilot's manifest polygons were measured
    against 01_key.py's specific crop, and a differently-keyed alpha bbox
    would silently desync every hardcoded point in the RIGS entry.
    """
    rgb = np.asarray(rgb_im.convert("RGB")).astype(np.int16)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    greenish = (g - np.maximum(r, b) > 40) & (g > 90)
    border = np.zeros(greenish.shape, bool)
    border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    seed_labels, count = label_components(greenish)
    if count:
        touch = set(seed_labels[border].tolist())
        touch.discard(0)
        bg = np.isin(seed_labels, list(touch)) if touch else np.zeros_like(greenish)
    else:
        bg = np.zeros_like(greenish)
    alpha = (~bg).astype(np.uint8) * 255

    out = np.dstack([np.asarray(rgb_im.convert("RGB")), alpha]).astype(np.uint8)
    fringe = (alpha > 0) & (g - np.maximum(r, b) > 10)
    out[..., 1][fringe] = np.maximum(r, b)[fringe].astype(np.uint8)
    return out  # H,W,4 uint8


def key_source(image, mode):
    if mode == "green":
        return key_green(image)
    if mode == "white_flood":
        rgba = image.convert("RGBA")
        arr = np.array(rgba)
        is_bg = flood_fill_background_mask(arr[:, :, :3], tolerance=35.0)
        arr[:, :, 3] = np.where(is_bg, 0, 255).astype(np.uint8)
        return arr
    if mode == "alpha":
        return np.array(image.convert("RGBA"))
    raise ValueError(f"unknown key mode: {mode!r}")


# ---------------------------------------------------------------------------
# Region / polygon / component mask builders
# ---------------------------------------------------------------------------

_OPS = {
    "<": lambda a, v: a < v, "<=": lambda a, v: a <= v,
    ">": lambda a, v: a > v, ">=": lambda a, v: a >= v,
    "==": lambda a, v: a == v,
}


def eval_region(clauses, X, Y):
    result = np.zeros(X.shape, dtype=bool)
    for clause in clauses:
        cur = np.ones(X.shape, dtype=bool)
        for axis, op, val in clause:
            arr = X if axis == "x" else Y
            cur &= _OPS[op](arr, val)
        result |= cur
    return result


def rasterize_polygon(points, w, h):
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).polygon([tuple(p) for p in points], fill=255)
    return np.asarray(m) > 0


def detect_legs(alpha, X, Y, spec, rig_id):
    y_min = spec["search"]["y_min"]
    x_min = spec["search"]["x_min"]
    region = alpha & (Y > y_min) & (X > x_min)
    labels, count = label_components(region)
    blobs = []
    for i in range(1, count + 1):
        comp = labels == i
        n = int(comp.sum())
        if n < spec.get("min_blob_px", 1):
            continue
        blobs.append((float(X[comp].mean()), comp))
    blobs.sort(key=lambda b: b[0])

    names = spec["names"]
    if len(blobs) != len(names):
        sizes = ", ".join(str(int(c.sum())) for _, c in blobs)
        sys.exit(
            f"[{rig_id}] leg detection: expected {len(names)} blob(s) {names}, "
            f"found {len(blobs)} (sizes: {sizes}). Adjust parts.legs.search / "
            f"min_blob_px in the manifest."
        )

    band_px = spec["pivot_rule"]["band_px"]
    dy = spec["pivot_rule"]["dy"]
    masks, pivots = {}, {}
    for name, (_cx, comp) in zip(names, blobs):
        masks[name] = comp
        ys, xs = Y[comp], X[comp]
        top = int(ys.min())
        band = ys < top + band_px
        pivots[name] = (int(round(float(xs[band].mean()))), top + dy)
    return masks, pivots


# ---------------------------------------------------------------------------
# Moore-neighbour contour tracing + Douglas-Peucker simplification
# ---------------------------------------------------------------------------

_MOORE_DIRS = [(-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1)]


def trace_contour(mask_bool):
    """Outer boundary of `mask_bool`'s largest component, as an ordered
    list of (x, y) pixel centres (clockwise). None if mask is empty."""
    core = largest_component(mask_bool)
    ys, xs = np.nonzero(core)
    if len(xs) == 0:
        return None
    h, w = core.shape

    def get(y, x):
        return 0 <= y < h and 0 <= x < w and core[y, x]

    start_y = int(ys.min())
    start_x = int(xs[ys == start_y].min())
    start = (start_y, start_x)
    boundary = [start]
    backtrack = (0, -1)  # the pixel west of `start` is guaranteed background
    cur = start
    limit = 4 * (h + w) * 8 + 64  # generous bound on perimeter length
    while len(boundary) < limit:
        bi = _MOORE_DIRS.index(backtrack)
        found = None
        for k in range(1, 9):
            dy, dx = _MOORE_DIRS[(bi + k) % 8]
            ny, nx = cur[0] + dy, cur[1] + dx
            if get(ny, nx):
                found = (ny, nx)
                backtrack = (-dy, -dx)
                break
        if found is None:
            break
        if found == start and len(boundary) > 1:
            break
        boundary.append(found)
        cur = found
    return [(x, y) for (y, x) in boundary]


def _point_seg_distance(p, a, b):
    (px, py), (ax, ay), (bx, by) = p, a, b
    dx, dy = bx - ax, by - ay
    if dx == 0 and dy == 0:
        return math.hypot(px - ax, py - ay)
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    cx, cy = ax + t * dx, ay + t * dy
    return math.hypot(px - cx, py - cy)


def _rdp(points, epsilon):
    if len(points) < 3:
        return list(points)
    a, b = points[0], points[-1]
    dmax, idx = -1.0, -1
    for i in range(1, len(points) - 1):
        d = _point_seg_distance(points[i], a, b)
        if d > dmax:
            dmax, idx = d, i
    if dmax > epsilon:
        left = _rdp(points[: idx + 1], epsilon)
        right = _rdp(points[idx:], epsilon)
        return left[:-1] + right
    return [a, b]


def simplify_closed(contour, max_epsilon):
    """Douglas-Peucker on a closed contour, epsilon applied DIRECTLY rather
    than searched-for via a target vertex-count range.

    That used to be backwards, and it hid a real bug: a target COUNT is
    only ever a proxy for "how far can the simplified line stray from the
    true silhouette" -- and the two are not the same question. epsilon IS
    that distance, directly (DP's actual guarantee: no simplified point
    ever deviates from the true contour by more than epsilon, in EITHER
    direction). Chasing a vertex count instead meant a part with a large,
    mostly-simple silhouette (body) could land inside its target band
    while still bulging several px past its own true edge in one small
    spot where the contour happened to be locally complex -- invisible to
    the fidelity check's global IoU (a few hundred stray px is nothing
    against a 100,000+ px part) but highly visible where that bulge lands
    on top of a much smaller, differently-coloured neighbour (found this
    exact way: body at IoU 0.9977 -- passing -- was still painting a
    visible dark band over far_hindleg's hip, confirmed by isolating the
    leg alone and seeing it render perfectly clean with nothing else on).

    Splits the loop at the point farthest from contour[0] so each half is
    simplified as an open polyline with fixed endpoints, then rejoins --
    the standard trick for running an open-polyline algorithm on a closed
    one."""
    n = len(contour)
    if n <= 3:
        return list(contour)
    arr = np.asarray(contour, dtype=float)
    d = np.hypot(arr[:, 0] - arr[0, 0], arr[:, 1] - arr[0, 1])
    i1 = int(np.argmax(d))
    if i1 == 0:
        i1 = n // 2
    chain_a = contour[: i1 + 1]
    chain_b = contour[i1:] + [contour[0]]
    return _rdp(chain_a, max_epsilon)[:-1] + _rdp(chain_b, max_epsilon)[:-1]


# ---------------------------------------------------------------------------
# Ear-clip triangulation
# ---------------------------------------------------------------------------

def _signed_area2(poly):
    s = 0.0
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        s += x1 * y2 - x2 * y1
    return s


def _cross(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _point_in_tri(p, a, b, c):
    d1 = _cross(a, b, p)
    d2 = _cross(b, c, p)
    d3 = _cross(c, a, p)
    neg = (d1 < 0) or (d2 < 0) or (d3 < 0)
    pos = (d1 > 0) or (d2 > 0) or (d3 > 0)
    return not (neg and pos)


def ear_clip(polygon):
    """Simple-polygon ear clipping. Returns (points_ccw, triangles) where
    `triangles` is a flat [i,i,i,...] index list into `points_ccw`, and
    every triangle is guaranteed non-degenerate (area > 1 px^2)."""
    pts = [tuple(p) for p in polygon]
    if len(pts) >= 2 and pts[0] == pts[-1]:
        pts = pts[:-1]
    if _signed_area2(pts) < 0:
        pts.reverse()
    idxs = list(range(len(pts)))
    tris = []
    guard = 0
    while len(idxs) > 3 and guard < 5000:
        guard += 1
        clipped = False
        m = len(idxs)
        for i in range(m):
            ip, ic, inx = idxs[(i - 1) % m], idxs[i], idxs[(i + 1) % m]
            a, b, c = pts[ip], pts[ic], pts[inx]
            if _cross(a, b, c) <= 1e-9:
                continue  # reflex or degenerate, not an ear
            if any(
                j not in (ip, ic, inx) and _point_in_tri(pts[j], a, b, c)
                for j in idxs
            ):
                continue
            tris.append((ip, ic, inx))
            idxs.pop(i)
            clipped = True
            break
        if not clipped:
            # Numerically stuck (near-collinear residue) -- drop the
            # thinnest remaining vertex rather than looping forever.
            idxs.pop(0)
    if len(idxs) == 3:
        tris.append((idxs[0], idxs[1], idxs[2]))

    good = []
    for (ia, ib, ic) in tris:
        area2 = abs(_cross(pts[ia], pts[ib], pts[ic]))
        if area2 > 1.0:
            good.append((ia, ib, ic))
    if not good:
        sys.exit("ear_clip produced no non-degenerate triangle for a part outline")
    flat = [i for tri in good for i in tri]
    return pts, flat


# ---------------------------------------------------------------------------
# Atlas packing -- shelf packer, 4px padding, verified non-overlapping.
# ---------------------------------------------------------------------------

def pack_atlas(tiles, padding):
    """tiles: [(name, rgba uint8 HxWx4), ...]. Returns (atlas_rgba, rects)
    where rects[name] = (x, y, w, h) in atlas space."""
    items = sorted(tiles, key=lambda t: -t[1].shape[0])
    total_area = sum((im.shape[1] + padding) * (im.shape[0] + padding) for _, im in items)
    max_w = max(im.shape[1] for _, im in items)
    atlas_w = max(max_w + 2 * padding, int(math.sqrt(total_area) * 1.15) + padding)

    x = padding
    y = padding
    shelf_h = 0
    rects = {}
    for name, im in items:
        h, w = im.shape[:2]
        if x + w + padding > atlas_w:
            x = padding
            y += shelf_h + padding
            shelf_h = 0
        rects[name] = (x, y, w, h)
        x += w + padding
        shelf_h = max(shelf_h, h)
    atlas_h = y + shelf_h + padding

    canvas = np.zeros((atlas_h, atlas_w, 4), dtype=np.uint8)
    for name, im in items:
        x, y, w, h = rects[name]
        canvas[y:y + h, x:x + w] = im
    return canvas, rects


def assert_no_overlap(rects):
    names = list(rects)
    for i in range(len(names)):
        ax, ay, aw, ah = rects[names[i]]
        for j in range(i + 1, len(names)):
            bx, by, bw, bh = rects[names[j]]
            if ax < bx + bw and bx < ax + aw and ay < by + bh and by < ay + ah:
                sys.exit(f"atlas packer produced overlapping rects: {names[i]} / {names[j]}")


# ---------------------------------------------------------------------------
# Bone tree validation
# ---------------------------------------------------------------------------

def validate_bone_tree(bones):
    names = [b["name"] for b in bones]
    if len(set(names)) != len(names):
        sys.exit(f"duplicate bone name(s) in {names}")
    parent_of = {b["name"]: b["parent"] for b in bones}
    roots = [n for n, p in parent_of.items() if p is None]
    if len(roots) != 1:
        sys.exit(f"bone tree must have exactly one root (parent=null), found {roots}")
    for b in bones:
        seen = {b["name"]}
        cur = b["parent"]
        while cur is not None:
            if cur in seen:
                sys.exit(f"cycle in bone tree reaching '{b['name']}' via '{cur}'")
            seen.add(cur)
            cur = parent_of.get(cur)
            if cur is not None and cur not in parent_of:
                sys.exit(f"bone '{b['name']}' has unknown parent '{cur}'")
    return roots[0]


# ---------------------------------------------------------------------------
# Main pipeline
# ---------------------------------------------------------------------------

class RigBuild:
    """Everything a build produced, so --preview can render without
    re-running the pipeline and the writer/verifier can share one result."""

    def __init__(self, rig_id, cropped_rgb, alpha, masks, pivots, order, bone_parents,
                 atlas, rects, rig_json, tile_origins):
        self.rig_id = rig_id
        self.cropped_rgb = cropped_rgb   # H,W,3 uint8 -- keyed+cropped source
        self.alpha = alpha               # H,W bool -- TRUE silhouette, ground truth for verify_rig
        self.masks = masks               # name -> bool mask, full cropped-canvas size (post resolve+dilate; body = backed)
        self.pivots = pivots             # name -> (x,y) in cropped-canvas space
        self.order = order
        self.bone_parents = bone_parents
        self.atlas = atlas               # H,W,4 uint8
        self.rects = rects               # name -> (x,y,w,h) in atlas space
        self.rig_json = rig_json
        self.tile_origins = tile_origins  # name -> (x0,y0) bbox origin in cropped-canvas space


def _resolve_source_path(spec, source_override):
    if source_override:
        return source_override
    path = os.path.join(SOURCE_DIR_DEFAULT, spec["file"])
    return path


def build_rig(rig_id, source_override=None):
    if rig_id not in RIGS:
        sys.exit(f"Unknown rig '{rig_id}'. Known: {', '.join(sorted(RIGS))}")
    spec = RIGS[rig_id]

    src_path = _resolve_source_path(spec, source_override)
    if not os.path.isfile(src_path):
        sys.exit(f"[{rig_id}] missing source image: {src_path}")
    if "/Resources/" in os.path.abspath(src_path).replace("\\", "/"):
        sys.exit(f"[{rig_id}] refusing to read a source under Resources/: {src_path}")

    image = Image.open(src_path)
    keyed = key_source(image, spec["key"])  # H,W,4 uint8
    alpha_full = keyed[..., 3] > 0
    ys, xs = np.nonzero(alpha_full)
    if len(xs) == 0:
        sys.exit(f"[{rig_id}] keyed source has no opaque content: {src_path}")
    pad = spec.get("content_pad", 8)
    H0, W0 = keyed.shape[:2]
    x0 = max(0, int(xs.min()) - pad)
    y0 = max(0, int(ys.min()) - pad)
    x1 = min(W0, int(xs.max()) + pad + 1)
    y1 = min(H0, int(ys.max()) + pad + 1)
    crop = keyed[y0:y1, x0:x1]
    rgb = crop[..., :3].copy()
    alpha = crop[..., 3] > ALPHA_THRESHOLD
    H, W = alpha.shape
    Y, X = np.indices((H, W))

    reference_height_px = int(ys.max() - ys.min()) + 1

    # ---- part masks -------------------------------------------------
    parts_spec = spec["parts"]
    masks = {}
    pivots = {}
    for name, pspec in parts_spec.items():
        if pspec["kind"] == "polygon":
            poly_mask = rasterize_polygon(pspec["points"], W, H)
            # A hand-drawn polygon systematically undershoots the true edge
            # by a few px almost everywhere (found by verify_rig: two
            # disconnected slivers at the far ear tip and the nose bridge,
            # plus a dozen 5-18px ones along individual fur-spike tips --
            # the same failure mode repeated, not one bad corner). Chasing
            # each one by hand doesn't generalise; dilating the RASTERIZED
            # mask by a uniform margin does, and it's the same primitive
            # already used for every other part's seam margin.
            outset_px = pspec.get("outset_px", 0)
            if outset_px:
                poly_mask = binary_dilation(poly_mask, outset_px)
            masks[name] = poly_mask & alpha
            pivots[name] = tuple(pspec["pivot"])
        elif pspec["kind"] == "region":
            masks[name] = eval_region(pspec["clauses"], X, Y) & alpha
            pivots[name] = tuple(pspec["pivot"])
        elif pspec["kind"] == "components":
            leg_masks, leg_pivots = detect_legs(alpha, X, Y, pspec, rig_id)
            masks.update(leg_masks)
            pivots.update(leg_pivots)
        else:
            sys.exit(f"[{rig_id}] unknown part kind '{pspec['kind']}' for '{name}'")

    order = spec["order"]
    rank = {name: i for i, name in enumerate(order)}
    for a in list(masks):
        for b in masks:
            if a != b and rank.get(b, -1) > rank.get(a, -1):
                masks[a] = masks[a] & ~masks[b]

    dilate_px = spec.get("dilate_px", 0)
    dilate_skip = set(spec.get("dilate_skip", []))
    for name in masks:
        if name in dilate_skip or dilate_px == 0:
            continue
        masks[name] = binary_dilation(masks[name], dilate_px) & alpha

    moving = np.zeros((H, W), dtype=bool)
    for m in masks.values():
        moving |= m
    body_mask = alpha & ~moving

    body_spec = spec["body"]
    backing_px = body_spec.get("backing_px", 0)
    filled_rgb, _ = nearest_fill(rgb, body_mask, backing_px)
    back = binary_dilation(body_mask, backing_px) & alpha
    body_rgb = rgb.copy()
    extra = back & ~body_mask
    body_rgb[extra] = filled_rgb[extra]
    masks["body"] = back
    pivots["body"] = tuple(body_spec["pivot"])

    if set(masks) != set(order):
        sys.exit(f"[{rig_id}] part set {sorted(masks)} != manifest 'order' {sorted(order)}")

    # ---- per-part RGBA tiles -----------------------------------------
    bone_parents = spec.get("bone_parents", {})
    simplify_epsilon = spec.get("simplify_epsilon", SIMPLIFY_EPSILON_DEFAULT)
    atlas_padding = spec.get("atlas_padding", ATLAS_PADDING_DEFAULT)

    tiles = []
    tile_origins = {}
    parts_json = []
    for name in order:
        mask = masks[name]
        part_rgb = body_rgb if name == "body" else rgb
        m_ys, m_xs = np.nonzero(mask)
        if len(m_xs) == 0:
            sys.exit(f"[{rig_id}] part '{name}' is empty after masking/dilation")
        tx0, ty0 = int(m_xs.min()), int(m_ys.min())
        tx1, ty1 = int(m_xs.max()) + 1, int(m_ys.max()) + 1
        tile = np.zeros((ty1 - ty0, tx1 - tx0, 4), dtype=np.uint8)
        tile[..., :3] = part_rgb[ty0:ty1, tx0:tx1]
        tile[..., 3] = np.where(mask[ty0:ty1, tx0:tx1], 255, 0).astype(np.uint8)
        tiles.append((name, tile))
        tile_origins[name] = (tx0, ty0)

        contour = trace_contour(mask)
        if contour is None:
            sys.exit(f"[{rig_id}] part '{name}' produced no contour")
        simplified = simplify_closed(contour, simplify_epsilon)
        local = [(px - tx0, py - ty0) for (px, py) in simplified]
        outline_pts, triangles = ear_clip(local)

        pivot_full = pivots[name]
        pivot_local = (pivot_full[0] - tx0, pivot_full[1] - ty0)

        bone_name = parts_spec.get(name, {}).get("bone", name)
        parts_json.append({
            "name": name,
            "bone": bone_name,
            "z": rank[name] if name in rank else order.index(name),
            "rect": None,  # filled in after packing
            "pivot": [int(round(pivot_local[0])), int(round(pivot_local[1]))],
            "outline": [[int(round(px)), int(round(py))] for px, py in outline_pts],
            "triangles": triangles,
            "weights": [[{"boneIndex": None, "weight": 1.0}] for _ in outline_pts],  # bone index filled after bones built
            "_bone_name": bone_name,
        })

    atlas, rects = pack_atlas(tiles, atlas_padding)
    assert_no_overlap(rects)
    for p in parts_json:
        x, y, w, h = rects[p["name"]]
        p["rect"] = [int(x), int(y), int(w), int(h)]

    # ---- bones ----------------------------------------------------
    all_names = order
    non_root = set(bone_parents)
    roots = [n for n in all_names if n not in non_root]
    if len(roots) != 1:
        sys.exit(f"[{rig_id}] bone_parents must leave exactly one part unparented (root), got roots={roots}")
    root_name = roots[0]
    bones = [{"name": n, "parent": bone_parents.get(n), "x": int(round(pivots[n][0])), "y": int(round(pivots[n][1]))}
             for n in [root_name] + [n for n in all_names if n != root_name]]
    validate_bone_tree(bones)
    bone_index = {b["name"]: i for i, b in enumerate(bones)}
    for p in parts_json:
        idx = bone_index[p["_bone_name"]]
        for w in p["weights"]:
            w[0]["boneIndex"] = idx
        del p["_bone_name"]

    rig_json = {
        "referenceHeightPx": reference_height_px,
        "bones": bones,
        "parts": parts_json,
    }

    return RigBuild(rig_id, rgb, alpha, masks, pivots, order, bone_parents, atlas, rects, rig_json, tile_origins)


# ---------------------------------------------------------------------------
# Writing
# ---------------------------------------------------------------------------

def output_dir_for(rig_id, output_base):
    spec = RIGS[rig_id]
    return os.path.join(output_base, spec.get("root", ROOT_ENEMIES), rig_id)


def write_rig(build, output_base, verbose=True):
    out_dir = output_dir_for(build.rig_id, output_base)
    os.makedirs(out_dir, exist_ok=True)
    atlas_path = os.path.join(out_dir, "atlas.png")
    rig_path = os.path.join(out_dir, "rig.json")
    Image.fromarray(build.atlas, "RGBA").save(atlas_path)
    with open(rig_path, "w") as f:
        json.dump(build.rig_json, f, indent=2)
    if verbose:
        aw, ah = build.atlas.shape[1], build.atlas.shape[0]
        print(f"[{build.rig_id}] wrote {atlas_path} ({aw}x{ah}) and {rig_path} "
              f"({len(build.rig_json['parts'])} parts, {len(build.rig_json['bones'])} bones)")
    return {"atlas.png", "rig.json"}


def report_stray_files(rig_id, output_base, written, prune, verbose=True):
    out_dir = output_dir_for(rig_id, output_base)
    if not os.path.isdir(out_dir):
        return
    existing = set(os.listdir(out_dir))
    # Unity owns each output file's .meta (that's where its GUID lives --
    # deleting one silently reassigns the GUID on next import and orphans
    # every reference to it, CLAUDE.md gotcha #2). This tool never writes
    # .meta files itself, so one sitting next to a file it DOES write is
    # Unity's, not a stray -- spare it from the same prune that clears out
    # genuinely obsolete output. Bit ourselves on this: --prune deleted
    # atlas.png.meta/rig.json.meta out from under a live rebuild before
    # this exclusion existed.
    owned_metas = {name + ".meta" for name in written}
    stray = sorted(existing - written - owned_metas)
    if not stray:
        return
    if prune:
        for name in stray:
            os.remove(os.path.join(out_dir, name))
        if verbose:
            print(f"  pruned {len(stray)} stray file(s): {', '.join(stray)}")
    else:
        print(f"  NOTE: {len(stray)} file(s) in {out_dir} not produced by this run "
              f"(pass --prune to remove): {', '.join(stray)}")


# ---------------------------------------------------------------------------
# Preview
# ---------------------------------------------------------------------------

_PREVIEW_TINTS = {
    "body": (255, 255, 255), "head": (255, 120, 120), "tail": (120, 200, 255),
    "near_foreleg": (140, 255, 140), "far_foreleg": (80, 180, 80),
    "near_hindleg": (255, 210, 120), "far_hindleg": (210, 150, 60),
}


def _checker(w, h, s=24):
    idx = (np.indices((h, w)).sum(0) // s) % 2
    return np.repeat(np.where(idx[..., None] == 0, 205, 232).astype(np.uint8), 3, axis=2)


def render_preview(build):
    H, W = build.cropped_rgb.shape[:2]
    silhouette = np.zeros((H, W), dtype=bool)
    for mask in build.masks.values():
        silhouette |= mask

    base_rgb = np.where(silhouette[..., None], build.cropped_rgb, _checker(W, H))
    base = Image.fromarray(base_rgb.astype(np.uint8), "RGB").convert("RGBA")
    overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ov_arr = np.array(overlay)
    for name in build.order:
        mask = build.masks[name]
        tint = _PREVIEW_TINTS.get(name, (255, 0, 255))
        ov_arr[mask, 0] = tint[0]
        ov_arr[mask, 1] = tint[1]
        ov_arr[mask, 2] = tint[2]
        ov_arr[mask, 3] = 110
    overlay = Image.fromarray(ov_arr, "RGBA")
    comp = Image.alpha_composite(base, overlay)

    d = ImageDraw.Draw(comp)
    try:
        font = ImageFont.truetype("arialbd.ttf", 14)
    except Exception:
        font = ImageFont.load_default()

    bone_pos = {b["name"]: (b["x"], b["y"]) for b in build.rig_json["bones"]}
    for b in build.rig_json["bones"]:
        if b["parent"] is not None:
            px, py = bone_pos[b["parent"]]
            d.line([(px, py), (b["x"], b["y"])], fill=(255, 255, 0, 255), width=2)
    for b in build.rig_json["bones"]:
        x, y = b["x"], b["y"]
        r = 5
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 40, 40, 255), outline=(255, 255, 255, 255))
        d.text((x + 7, y - 7), b["name"], fill=(255, 255, 255, 255), font=font)

    return comp.convert("RGB")


# ---------------------------------------------------------------------------
# Verification -- three checks, because "pixel perfect" turned out to be
# three different questions and the earlier tool only ever answered the
# easy one (is the mesh well-formed: non-degenerate triangles, no atlas
# overlap). None of that checks whether the cut is CORRECT. Found by hand,
# on the rat, after it had already shipped once: a seam gap (coverage), a
# clipped ear-notch and clipped teeth (simplification losing a thin/pointy
# feature), and a head polygon that didn't reach the jaw at all -- so the
# whole snout/nose/mouth was silently owned by body's "whatever's left"
# remainder instead of head. That last one is the one worth naming loudly:
# a coverage check passes it (every pixel is still owned by SOME part), a
# fidelity check passes it (the wrong-but-internally-consistent head
# polygon simplifies fine), and it never shows up until something actually
# MOVES the part that should carry those pixels and doesn't. Three
# different failure classes, three different checks:
#
#   1. COVERAGE   -- does every true-alpha pixel belong to at least one
#                     final part mask? A gap here is a seam.
#   2. FIDELITY    -- does each part's simplified outline still match its
#                     OWN pre-simplification mask closely? A miss here is
#                     Douglas-Peucker cutting across a thin feature that
#                     WAS correctly owned but got smoothed away.
#   3. WIGGLE      -- rotate each non-root part about its own bone, one at
#                     a time, and look. This is the only one of the three
#                     that can catch an OWNERSHIP bug (pixels that are
#                     present, and correctly shaped, but attached to the
#                     wrong part) -- coverage and fidelity are both
#                     mathematically blind to "whose bone should this
#                     move with," because that question is anatomical, not
#                     geometric. Not a numeric gate: a rendered contact
#                     sheet, meant to actually be looked at, every joint,
#                     every time, before a cut is called done.
# ---------------------------------------------------------------------------

def verify_rig(build, iou_threshold=0.985):
    """Runs checks 1 and 2 (hard, numeric). Returns (ok, report_lines)."""
    ok = True
    report = []
    H, W = build.alpha.shape

    union = np.zeros((H, W), dtype=bool)
    for m in build.masks.values():
        union |= m
    gap = build.alpha & ~union
    gap_px = int(gap.sum())
    if gap_px > 0:
        ok = False
        ys, xs = np.where(gap)
        report.append(
            f"COVERAGE FAIL: {gap_px}px of true silhouette not covered by any part "
            f"(bbox x={int(xs.min())}-{int(xs.max())} y={int(ys.min())}-{int(ys.max())}) -- a seam gap."
        )
    else:
        report.append("COVERAGE OK: every silhouette pixel is owned by at least one part.")

    for p in build.rig_json["parts"]:
        name = p["name"]
        tx0, ty0 = build.tile_origins[name]
        poly_full = [(px + tx0, py + ty0) for px, py in p["outline"]]
        poly_mask = rasterize_polygon(poly_full, W, H)
        true_mask = build.masks[name]
        inter = int((poly_mask & true_mask).sum())
        union_px = int((poly_mask | true_mask).sum())
        iou = inter / union_px if union_px else 1.0
        if iou < iou_threshold:
            ok = False
            lost_px = int((true_mask & ~poly_mask).sum())
            report.append(
                f"FIDELITY FAIL: '{name}' outline IoU={iou:.4f} < {iou_threshold} -- "
                f"simplification lost {lost_px}px this part should own "
                f"(a thin/pointy feature likely got cut across)."
            )
        else:
            report.append(f"FIDELITY OK: '{name}' outline IoU={iou:.4f}")

    return ok, report


def render_wiggle(build, test_angle_deg=20):
    """One row per non-root part: rest pose beside that part's bone rotated
    by test_angle_deg about its own pivot, everything else held still.
    Pure PIL/numpy -- no Unity round trip, so this is cheap enough to run
    (and to actually look at) every single time, not just when something
    already looks wrong."""
    H, W = build.cropped_rgb.shape[:2]
    root_name = build.rig_json["bones"][0]["name"]

    def composite(rotated_name, angle):
        canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        for name in build.order:
            mask = build.masks[name]
            layer_rgba = np.dstack([build.cropped_rgb, np.where(mask, 255, 0).astype(np.uint8)])
            layer = Image.fromarray(layer_rgba, "RGBA")
            if name == rotated_name and angle:
                px, py = build.pivots[name]
                layer = layer.rotate(-angle, center=(px, py), resample=Image.BICUBIC)
            canvas.alpha_composite(layer)
        bg = Image.new("RGB", (W, H), (210, 213, 217))
        bg.paste(canvas, (0, 0), canvas)
        return bg

    rest = composite(None, 0)
    rows = []
    for name in build.order:
        if name == root_name:
            continue  # rotating the root moves everything -- not an isolation test
        wiggled = composite(name, test_angle_deg)
        pair = Image.new("RGB", (W * 2 + 10, H + 22), (25, 25, 25))
        pair.paste(rest, (0, 20))
        pair.paste(wiggled, (W + 10, 20))
        d = ImageDraw.Draw(pair)
        try:
            font = ImageFont.truetype("arialbd.ttf", 15)
        except Exception:
            font = ImageFont.load_default()
        d.text((4, 2), f"{name}: rest  |  +{test_angle_deg} deg about its own bone", fill=(255, 255, 0), font=font)
        rows.append(pair)

    sheet_w = rows[0].width
    sheet = Image.new("RGB", (sheet_w, sum(r.height for r in rows) + 4 * (len(rows) + 1)), (15, 15, 15))
    y = 4
    for r in rows:
        sheet.paste(r, (0, y))
        y += r.height + 4
    return sheet


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("creatures", nargs="*", help="Rig ids to process (default: all)")
    ap.add_argument("--all", action="store_true", help="Process every rig in RIGS")
    ap.add_argument("--preview", metavar="ID", help="Render cut+bone overlay for ID and stop (no atlas/rig.json write)")
    ap.add_argument("--prune", action="store_true", help="Delete output files the manifest no longer produces")
    ap.add_argument("--output-root", metavar="PATH",
                     help="Override the output base (default Assets/_Project/Resources/Rigs, or "
                          "$RIG_ACTOR_OUTPUT_ROOT). Use for draft/test runs.")
    ap.add_argument("--source", metavar="PATH",
                     help="Override the manifest source image for this run (single-id runs only). "
                          "Draft/test use only -- see module docstring.")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    output_base = args.output_root or os.environ.get("RIG_ACTOR_OUTPUT_ROOT") or RIGS_OUTPUT_BASE

    if args.preview:
        build = build_rig(args.preview, source_override=args.source)
        out_dir = output_dir_for(args.preview, output_base)
        os.makedirs(out_dir, exist_ok=True)
        preview_path = os.path.join(out_dir, "preview.png")
        render_preview(build).save(preview_path)
        print(f"[{args.preview}] preview -> {preview_path}")

        # Non-fatal here (nothing's being written to Resources/ yet), but
        # printed every time -- --preview is exactly the moment to catch a
        # coverage/fidelity problem, before it's ever synced into Unity.
        ok, report = verify_rig(build)
        for line in report:
            print(f"[{args.preview}] {line}")
        wiggle_path = os.path.join(out_dir, "wiggle.png")
        render_wiggle(build).save(wiggle_path)
        print(f"[{args.preview}] wiggle -> {wiggle_path}  (look at every row before trusting this cut)")
        return

    ids = list(RIGS.keys()) if args.all else args.creatures
    if not ids:
        ap.error("Give one or more rig ids, or --all, or --preview <id>")
    if args.source and len(ids) != 1:
        ap.error("--source only applies to a single rig id")

    for rig_id in ids:
        build = build_rig(rig_id, source_override=args.source)

        # Hard gate, always on -- not behind a flag, so it cannot be
        # forgotten the way a one-off --preview glance can. Refuses to
        # write atlas.png/rig.json at all on a coverage or fidelity
        # failure, matching UiAudit's "refuse rather than warn" posture.
        ok, report = verify_rig(build)
        for line in report:
            print(f"[{rig_id}] {line}")
        if not ok:
            sys.exit(f"[{rig_id}] verify FAILED -- fix the manifest entry before this can write. "
                      f"See the FAIL line(s) above.")

        written = write_rig(build, output_base, verbose=not args.quiet)
        report_stray_files(rig_id, output_base, written, args.prune, verbose=not args.quiet)

        # The wiggle sheet is not a numeric gate (whether a rotated part
        # "looks anatomically right" isn't reliably a number) -- it is the
        # mandatory review artifact instead. Written every run, same as
        # atlas.png, so there is always a fresh one to actually look at
        # before calling a cut done.
        out_dir = output_dir_for(rig_id, output_base)
        wiggle_path = os.path.join(out_dir, "wiggle.png")
        render_wiggle(build).save(wiggle_path)
        print(f"[{rig_id}] wiggle -> {wiggle_path}  (look at every row before calling this done)")


if __name__ == "__main__":
    main()
