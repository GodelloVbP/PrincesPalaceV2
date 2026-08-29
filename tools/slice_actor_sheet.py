#!/usr/bin/env python3
"""Slice ACTOR sprite sheets into per-stance/per-frame combat art.

"Actor" rather than "enemy" because the party's side of the stage is the
same thing: `Resources/Characters/sheep/idle.png` and
`Resources/Enemies/rat/idle.png` are resolved by one runtime path
(`StanceAnimationLibrary.Resolve` -> `FightController.RefreshCombatantSprite`)
and held to one set of invariants, so they are produced by one tool. The
only thing that differs is which Resources root the output lands in, which
is a per-entry `root` in the manifest.

Driven by a committed per-actor manifest (ACTORS below) instead of one-off
shell commands typed at the terminal and never recorded anywhere — which is
exactly how the rat attack sheet's irregular row layout ("manual row
split"), every creature's stance/frame naming, and the whole of Shawn's
battle art ended up living only in someone's command-line history.

## What changed and why (2026-08-03 rewrite)

A previous pass tried to make every pose "the same size" by tight-cropping
each one and scaling it so its CONTENT HEIGHT matched the tallest pose in
the set. That is wrong: bbox height is POSE-DEPENDENT (a crouch is shorter
than a rear-up even when drawn at the same scale) — sqrt(opaque pixel
count) is the pose-INVARIANT proxy for "how big is this creature drawn",
and measurement showed the raw art already held that to within +-5% per
sheet. Height-matching inverted a good invariant into a bad one, and the
creature visibly pulsed size between frames.

So: **this tool never scales an individual pose.** Every piece is pasted
at its native size (after, optionally, one literal per-SHEET correction —
see `scale` below). The only thing "auto" here is `--suggest-scales`,
which measures and prints a number; a human copies it into the manifest,
where it shows up in a diff. An automatic per-sheet scale would fail for
exactly the reason the per-pose version did: VFX/debris in a frame
contaminate the mass measurement, the suggestion moves every time a frame
is added, and it is unreviewed.

## Manifest shape

    ACTORS = {
        "<id>": {
            "root": OUTPUT_ROOT_ENEMIES | OUTPUT_ROOT_CHARACTERS,
            "sheets": [
                {
                    "file": "<name>.png",         # under Assets/_Project/Art/Enemies/
                    "key": "alpha" | "white_flood" | "green",
                    "grid": (rows, cols),          # OR "bands" below, not both
                    "bands": {"rows": [(y0,y1), ...], "cols": N},
                    "scale": 1.0,                  # literal; see module docstring
                    "names": ["idle", None, "cast/f0", ...],  # row-major,
                              # None skips that cell; "x/f0" writes an
                              # animated-stance frame at <out>/x/f0.png
                },
                ...
            ],
            "aliases": {"attack": "cast"},   # byte-for-byte copy, whole shape
            "anchor": "centroid" | "ground_band",
            "delivery_scale": 1.0,           # ONE uniform multiplier, applied
                                              # AFTER native-scale compositing,
                                              # to restore on-screen presence
            "nudge": {"cast/f3": (0, -18)},  # per-output-name 2D pixel offset
        },
    }

`bands` exists because a sheet is not always an even grid — the rat's
attack sheet has two irregularly-spaced rows with dead space between and
around them, and a nominal even split lands the cut inside a pose. `bands`
gives explicit row ranges; column cuts inside each band still get the
usual `best_cut` nudging.

## Anchoring

"centroid" is a faithful port of the tool's original behaviour (horizontal
alignment on whole-mask alpha centroid, ground line at bbox bottom) and
exists so a creature that has never been touched (bog_witch) can be
regenerated PIXEL-IDENTICAL to what already shipped, proving this rewrite
is a superset rather than a rewrite-and-hope.

"ground_band" is the improved anchor: largest-connected-component (to
ignore detached debris — a golem's flying earth shards should not drag the
horizontal anchor sideways) intersected with a thin band at the CREATURE'S
ground line (to ignore a raised tail or an outflung fist — anatomy far from
the ground cannot move where the creature is anchored, whereas a whole-mass
centroid moves with it). See `ground_band_anchor`.

## Safety guards

- Refuses any input path under `Assets/_Project/Resources/` — processing
  already-processed output and compounding resample loss is exactly how
  the previous bad pass happened.
- Refuses to write a creature whose sheets disagree >10% in corrected
  LCC-median mass unless at least one of the disagreeing sheets carries an
  explicit `scale` (i.e. a human already looked at it). Run
  `--suggest-scales <id>` to get the number to copy in.
- Warns (does not refuse) when LCC drops more than 15% of a frame's total
  opaque mass — usually means real content, not debris, got excluded.
- Never adds or removes an output filename — every name in a creature's
  manifest must already exist on disk (or you are deliberately adding a
  new stance, which also touches Unity's asset GUIDs, so do it knowingly).
  Files present in the output folder that the manifest does not produce
  are listed, not deleted, unless `--prune` is passed.

Usage:
    python tools/slice_actor_sheet.py bog_witch
    python tools/slice_actor_sheet.py golem rat
    python tools/slice_actor_sheet.py --all
    python tools/slice_actor_sheet.py --suggest-scales rat
    python tools/slice_actor_sheet.py golem --prune
"""

import argparse
import os
import sys

try:
    import numpy as np
    from PIL import Image
except ImportError:
    sys.exit("Pillow and numpy are required: pip install Pillow numpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from remove_portrait_backgrounds import flood_fill_background_mask
from key_green_screen import key_out_green
# Cell-cutting geometry, shared with slice_item_sheet.py -- see that module's
# own header for why it exists.
from sheet_slicing import (
    ALPHA_THRESHOLD,
    SEARCH_FRACTION,
    PADDING,
    opaque_mask,
    column_weights,
    row_weights,
    best_cut,
)

# Ground-contact band size, as a fraction of the creature's own shared
# canvas height (NOT of any one pose's own bbox — a fraction of the pose's
# own height would be pose-dependent by construction, the exact error
# class this anchor exists to avoid).
GROUND_BAND_FRACTION = 0.08
GROUND_BAND_MAX_FRACTION = 0.24
GROUND_BAND_STEP = 0.04
# A band holding less than this fraction of the core's own pixels is
# "thin contact" (one toe) -- widen before trusting it.
GROUND_BAND_MIN_MASS_FRACTION = 0.005

# Two sheets of one creature disagreeing by more than this in corrected
# LCC-median mass, with neither carrying an explicit `scale`, is refused
# rather than silently written mismatched.
SHEET_SCALE_MISMATCH_GUARD = 0.10
# A frame where LCC excludes more than this fraction of total opaque mass
# is worth a human glance -- likely real content, not debris.
LCC_DROP_WARN_FRACTION = 0.15

# Sheets live under one of two source folders depending on who authored
# them; an entry names its own via "source_dir" when it is not the default.
SOURCE_DIR = "Assets/_Project/Art/Enemies"
SOURCE_DIR_SHEETS = "Assets/_Project/Art/Sheets"

# Both roots are Resources-relative at runtime ("Enemies/rat",
# "Characters/sheep") and are what EnemyDefinition.spritePath /
# CharacterDefinition.battleSpritePath point at.
OUTPUT_ROOT_ENEMIES = "Assets/_Project/Resources/Enemies"
OUTPUT_ROOT_CHARACTERS = "Assets/_Project/Resources/Characters"


ACTORS = {
    "bog_witch": {
        # Untouched by the bad pass and already alpha-keyed -- the control
        # this whole rewrite is validated against (see module docstring).
        "sheets": [
            {
                "file": "bog_witch_sheet.png",
                "key": "alpha",
                "grid": (2, 3),
                "scale": 1.0,
                "names": ["idle", "cast", "attack", "hurt", "defeated", "taunt"],
            },
        ],
        # Deliberately left on "centroid" -- this creature was never broken
        # (see module docstring) and the parity proof above already covers
        # it. ground_band was validated against it directly during
        # development: comparable onion-skin tightness, canvas ~10% wider
        # (bog_witch's "attack" pose has her staff touching the ground,
        # which ground_band correctly treats as ground contact) -- not
        # worse, just a different, equally defensible read. Not worth
        # shipping a change to art nobody reported a problem with.
        "aliases": {},
        "anchor": "centroid",
        "delivery_scale": 1.0,
        "nudge": {},
    },

    "golem": {
        "sheets": [
            {
                # Cells 1 and 2 (cast, attack) are superseded by the
                # dedicated attack sheet below -- golem_sheet.png's own
                # cast/attack poses were single static frames, replaced
                # once the 6-frame earth-shard sequence was authored.
                "file": "golem_sheet.png",
                "key": "alpha",
                "grid": (2, 3),
                "scale": 1.0,
                "names": ["idle", None, None, "hurt", "defeated", "guard"],
            },
            {
                "file": "golem_sheet_attack.png",
                "key": "alpha",
                "grid": (2, 3),
                "scale": 1.0,  # measured ratio to golem_sheet.png: 0.988 -- inside the 10% guard
                "names": ["cast/f0", "cast/f1", "cast/f2", "cast/f3", "cast/f4", "cast/f5"],
            },
        ],
        # "attack" reuses the earth-shard slam wholesale -- see
        # TheGolemsAttackAnimation_ReusesTheEarthShardSequence.
        "aliases": {"attack": "cast"},
        "anchor": "ground_band",
        "delivery_scale": 1.0,
        "nudge": {},
    },

    "rat": {
        "sheets": [
            {
                # RGB, ZERO alpha -- has always been white-backed (single
                # commit in its whole git history). There is no committed
                # keyed intermediate to reproduce; this key is a genuine
                # re-derivation, not a reproduction (see the risk table in
                # the design plan). Cell r0c2 (484x266) carries no pose
                # any shipped output uses and is intentionally skipped.
                #
                # Cell 0 (idle) is superseded by the dedicated 12-frame idle
                # sheet below -- same relationship golem's base sheet has
                # with its earth-shard cast sequence -- because this flat
                # single-drawing idle is what the rig pipeline (ART_PIPELINE
                # #9) was built to replace in the first place. Kept here as
                # None, not deleted, per that section's "keep a creature's
                # old art even after it ships a rig" rule -- the rig is
                # currently disabled (mid-overhaul on another rig, see
                # RigLibrary), so this flat art is exactly the fallback that
                # rule exists for; the new animated idle just fell in ahead
                # of it because it happened to ship first.
                "file": "Giant_rat_sheet.png",
                "key": "white_flood",
                "grid": (2, 3),
                "scale": 1.0,  # the reference sheet for this creature
                "names": [None, "cast", None, "hurt", "defeated", "extra"],
            },
            {
                # Poses occupy y=112..489 and y=560..880 -- NOT a nominal
                # even 2-row split (that lands at y=750, inside a pose).
                "file": "giant_rat_attack_sheet.png",
                "key": "alpha",
                "bands": {"rows": [(112, 490), (560, 881)], "cols": 3},
                "scale": 0.766,  # drawn ~1.306x larger (linear) than the base
                                 # sheet; always scale the LARGER sheet down.
                "names": ["attack/f0", "attack/f1", "attack/f2", "attack/f3", "attack/f4", "attack/f5"],
            },
            {
                # New 12-frame idle (2026-08-29), flat cel style, actual
                # green (~hue-dominant, not a literal #00FF00 flood) rather
                # than this creature's usual white-backed sheets -- "green"
                # key mode (key_out_green) rather than "white_flood".
                # Even 2x6 grid, cells cleanly gapped -- no bands needed.
                "file": "Giant_rat_idle_sheet_12_frame.png",
                "key": "green",
                "grid": (2, 6),
                "scale": 1.208,  # measured via --suggest-scales rat (median
                                 # sqrt-mass 189.5 vs the reference sheet's 228.9)
                                 # -- this sheet is drawn SMALLER than the other two,
                                 # so matching it means upscaling rather than the
                                 # module docstring's usual "always shrink the
                                 # larger sheet" (Giant_rat_sheet.png is fixed at
                                 # 1.0 as every other rat stance's anchor; rescaling
                                 # IT down would move cast/hurt/defeated/extra,
                                 # which nobody asked to change). A mild 1.2x
                                 # softens the upscaled sheet slightly -- acceptable
                                 # for this flat cel style; revisit if idle reads
                                 # noticeably softer than the rat's other stances.
                "names": ["idle/f0", "idle/f1", "idle/f2", "idle/f3", "idle/f4", "idle/f5",
                          "idle/f6", "idle/f7", "idle/f8", "idle/f9", "idle/f10", "idle/f11"],
            },
        ],
        "aliases": {"guard": "extra"},
        "anchor": "ground_band",
        "delivery_scale": 1.0,
        "nudge": {},
    },

    # --- Regenerated under docs/STANCE_SHEET_SPEC.md -----------------------
    #
    # PLACEHOLDERS. Every "sheets" entry below names a file that does not
    # exist yet -- these three actors are mid-regeneration (see the spec doc's
    # own work-order table, section 2). `scale` and `delivery_scale` are left
    # at 1.0 until Protocol B's delivery step (spec section 8) measures the
    # real ones; do not guess ahead of that measurement.
    #
    # Six SEPARATE sheets each, one per stance -- unlike bog_witch/golem/rat's
    # single multi-pose sheet, because the spec commissions one full
    # 3x2-of-six-frames animation sheet PER STANCE (section 5), not six poses
    # sharing one sheet. `names` is a plain list because every cell in a
    # Stage-2 sheet belongs to the same stance; there is no `None` skip and no
    # cross-sheet alias to make here.
    "beetle": {
        "sheets": [
            {"file": "beetle/sheet_idle.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["idle/f0", "idle/f1", "idle/f2", "idle/f3", "idle/f4", "idle/f5"]},
            {"file": "beetle/sheet_attack.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["attack/f0", "attack/f1", "attack/f2", "attack/f3", "attack/f4", "attack/f5"]},
            {"file": "beetle/sheet_turtle_up.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["turtle_up/f0", "turtle_up/f1", "turtle_up/f2", "turtle_up/f3", "turtle_up/f4", "turtle_up/f5"]},
            {"file": "beetle/sheet_shell_closed.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["shell_closed/f0", "shell_closed/f1", "shell_closed/f2", "shell_closed/f3", "shell_closed/f4", "shell_closed/f5"]},
            {"file": "beetle/sheet_hurt.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["hurt/f0", "hurt/f1", "hurt/f2", "hurt/f3", "hurt/f4", "hurt/f5"]},
            {"file": "beetle/sheet_defeated.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["defeated/f0", "defeated/f1", "defeated/f2", "defeated/f3", "defeated/f4", "defeated/f5"]},
        ],
        "aliases": {},
        "anchor": "ground_band",
        "delivery_scale": 0.972,  # 243 / measured idle f0 height 250px, per spec section 8
        "nudge": {},
    },

    "treant": {
        "sheets": [
            {"file": "treant/sheet_idle.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["idle/f0", "idle/f1", "idle/f2", "idle/f3", "idle/f4", "idle/f5"]},
            {"file": "treant/sheet_attack.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["attack/f0", "attack/f1", "attack/f2", "attack/f3", "attack/f4", "attack/f5"]},
            {"file": "treant/sheet_trunk_slam.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["trunk_slam/f0", "trunk_slam/f1", "trunk_slam/f2", "trunk_slam/f3", "trunk_slam/f4", "trunk_slam/f5"]},
            {"file": "treant/sheet_cast.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["cast/f0", "cast/f1", "cast/f2", "cast/f3", "cast/f4", "cast/f5"]},
            {"file": "treant/sheet_hurt.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["hurt/f0", "hurt/f1", "hurt/f2", "hurt/f3", "hurt/f4", "hurt/f5"]},
            {"file": "treant/sheet_defeated.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["defeated/f0", "defeated/f1", "defeated/f2", "defeated/f3", "defeated/f4", "defeated/f5"]},
        ],
        "aliases": {},
        "anchor": "ground_band",
        "delivery_scale": 1.0,  # measure per spec section 8; target idle f0 height 441px
        "nudge": {},
    },

    # Delivered id is "forest_warden" -- the content id enemies.json has
    # always used -- but the art folder is "forest_troll" and has never been
    # renamed to match. Not a typo: SOURCE_DIR-relative "file" paths below
    # point into the folder that actually exists.
    "forest_warden": {
        "sheets": [
            {"file": "forest_troll/sheet_idle.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["idle/f0", "idle/f1", "idle/f2", "idle/f3", "idle/f4", "idle/f5"]},
            {"file": "forest_troll/sheet_attack.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["attack/f0", "attack/f1", "attack/f2", "attack/f3", "attack/f4", "attack/f5"]},
            {"file": "forest_troll/sheet_attack_roar.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["attack_roar/f0", "attack_roar/f1", "attack_roar/f2", "attack_roar/f3", "attack_roar/f4", "attack_roar/f5"]},
            {"file": "forest_troll/sheet_attack_charge.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["attack_charge/f0", "attack_charge/f1", "attack_charge/f2", "attack_charge/f3", "attack_charge/f4", "attack_charge/f5"]},
            {"file": "forest_troll/sheet_hurt.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["hurt/f0", "hurt/f1", "hurt/f2", "hurt/f3", "hurt/f4", "hurt/f5"]},
            {"file": "forest_troll/sheet_defeated.png", "key": "alpha", "grid": (2, 3), "scale": 1.0,
             "names": ["defeated/f0", "defeated/f1", "defeated/f2", "defeated/f3", "defeated/f4", "defeated/f5"]},
        ],
        "aliases": {},
        "anchor": "ground_band",
        "delivery_scale": 1.0,  # measure per spec section 8; target idle f0 height 473px
        "nudge": {},
    },

    # --- Party side ------------------------------------------------------
    # Shawn is the one PC with battle art. Until this entry existed, no
    # committed tool wrote Resources/Characters at ALL -- his six stances
    # were produced by an invocation that survives nowhere, which made the
    # most-seen sprite in the game the least reproducible thing in it.
    "sheep": {
        "root": OUTPUT_ROOT_CHARACTERS,
        "source_dir": SOURCE_DIR_SHEETS,
        "sheets": [
            {
                # Already alpha-keyed, and an honest even grid: the two rows
                # of figures occupy y=104..452 and y=555..851, so the
                # nominal split at y=512 lands in the empty band between
                # them and needs no `bands` override.
                #
                # NOT switched to explicit bands, even though they would be
                # tidier. There is a 4px sliver of stray alpha at
                # y=450..454 -- keying noise in the source -- which the
                # even grid sweeps into the top row and which is therefore
                # baked into the shipped art: it is what makes idle's
                # canvas 366px rather than 364. Bands that exclude it
                # produce cleaner output that is NOT what shipped, and this
                # entry's job is to reproduce. See the note in
                # docs/ART_PIPELINE.md before "fixing" it.
                #
                # Cell order was reverse-engineered by silhouette IoU
                # against the six shipped PNGs -- every cell matched its
                # pose at 0.95-0.99 with the runner-up below 0.72. Worth
                # recording because the middle of the top row is ATTACK,
                # not cast: the alphabetical-looking guess is wrong, and a
                # transposition here shows up only as the wrong pose
                # playing, never as a test failure.
                "file": "shawn.png",
                "key": "alpha",
                "grid": (2, 3),
                "scale": 1.0,
                # Cell 1 (the flat attack pose) is superseded by the
                # dedicated attack sheet below -- same relationship the
                # golem's base sheet has with its earth-shard sequence.
                "names": ["idle", None, "cast", "hurt", "defeated", "victory"],
            },
            {
                # Authored alongside shawn.png and then never wired up:
                # until this entry existed the party was the only thing on
                # the stage that could not animate, while every enemy could.
                #
                # Rows sit at y=164..440 and y=608..877, so the nominal even
                # split at y=512 falls in the empty band between them and a
                # plain grid is correct here.
                #
                # scale 1.0 is measured, not assumed: this sheet's six cells
                # span sqrt(area) 219.8-235.1 against the base sheet's idle
                # at 222.9 -- a 1.07 ratio, comfortably inside the tool's own
                # 10% cross-sheet guard, so no correction is wanted. (The rat
                # needed 0.766 for the same comparison; Shawn does not.)
                "file": "Shawn_attack_sheet.png",
                "key": "alpha",
                "grid": (2, 3),
                "scale": 1.0,
                "names": ["attack/f0", "attack/f1", "attack/f2", "attack/f3", "attack/f4", "attack/f5"],
            },
        ],
        # Started on "centroid", which reproduced the six shipped flat
        # stances byte-identical (the parity proof that this entry describes
        # what actually made the art). Moved to "ground_band" only when the
        # attack ANIMATION was added, for the same reason golem and rat
        # moved: a whole-mass centroid tracks the staff, so the frames where
        # Shawn thrusts furthest (f1, f4) pushed his body left to compensate
        # and he slid along the ground mid-swing. Measured ground-contact x
        # across all ten non-prone frames: centroid spread 32.7px,
        # ground_band 3.8px.
        #
        # This is why the flat stances' canvas is 525x366 rather than the
        # 470x366 that shipped -- the shared canvas has to fit the widest
        # pose, and the lunge is wider than anything in the base sheet.
        "aliases": {},
        "anchor": "ground_band",
        "delivery_scale": 1.0,
        "nudge": {},
    },
}


# ---------------------------------------------------------------------------
# Legacy math -- unchanged shapes/behaviour from the original tool. The cell-
# cutting primitives it used to declare here (opaque_mask, column_weights,
# row_weights, best_cut) now live in sheet_slicing.py and are imported above;
# only the "centroid" anchor, which nothing else shares, remains local.
# ---------------------------------------------------------------------------

def alpha_centroid_x(mask, box):
    """Horizontal centre of alpha mass within box, in ABSOLUTE coordinates."""
    region = mask.crop(box)
    w, h = region.size
    px = region.load()
    total = 0
    weighted = 0
    for x in range(w):
        col = sum(1 for y in range(h) if px[x, y])
        total += col
        weighted += col * x
    if total == 0:
        return box[0] + (box[2] - box[0]) // 2
    return box[0] + weighted / total


def cut_cells(mask, sheet_w, sheet_h, cols, rows=None, bands=None):
    """Row-major list of tight ABSOLUTE boxes (or None for an empty cell).

    Either `rows` (nominal even grid, each row cut nudged to the emptiest
    gutter) or `bands` (explicit [(y0,y1), ...] row ranges, used verbatim)
    must be given. Column cuts are always computed PER ROW/BAND and always
    nudged -- bleed on one row says nothing about another.
    """
    if bands is not None:
        row_ranges = list(bands)
    else:
        cell_h = sheet_h // rows
        y_search = int(cell_h * SEARCH_FRACTION)
        y_cuts = [0]
        weights = row_weights(mask, 0, sheet_w, 0, sheet_h)
        for r in range(1, rows):
            y_cuts.append(best_cut(weights, 0, r * cell_h, y_search))
        y_cuts.append(sheet_h)
        row_ranges = [(y_cuts[r], y_cuts[r + 1]) for r in range(rows)]

    cell_w = sheet_w // cols
    x_search = int(cell_w * SEARCH_FRACTION)

    cells = []
    for (y0, y1) in row_ranges:
        weights = column_weights(mask, 0, sheet_w, y0, y1)
        x_cuts = [0]
        for c in range(1, cols):
            x_cuts.append(best_cut(weights, 0, c * cell_w, x_search))
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
        from PIL import ImageFilter
        rgba = image.convert("RGBA")
        arr = np.array(rgba)
        is_bg = flood_fill_background_mask(arr[:, :, :3], tolerance=35.0)
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

def largest_connected_component(mask_bool):
    """4-connected LCC of a boolean 2D numpy array. O(number of runs), not
    per-pixel BFS -- a per-row run extraction plus union-find against the
    previous row's overlapping runs.
    """
    h = mask_bool.shape[0]
    parent = []
    size = []

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

    row_runs = []
    row_ids = []
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

    if not parent:
        return np.zeros_like(mask_bool, dtype=bool)

    best_root = max((find(g) for g in range(len(parent))), key=lambda r: size[r])

    out = np.zeros_like(mask_bool, dtype=bool)
    for y, (runs, ids) in enumerate(zip(row_runs, row_ids)):
        for (x0, x1), gid in zip(runs, ids):
            if find(gid) == best_root:
                out[y, x0:x1] = True
    return out


def ground_band_anchor(mask_bool, canvas_h):
    """(anchor_x, core_mask, path) in the LOCAL coordinate frame of
    mask_bool (caller passes an already-tight-cropped piece's mask).

    `path` is one of "ground_band", "widened_band" or "core_centroid" --
    logged by the caller so a degenerate frame is visible, not silent.
    Returns None if the mask is entirely empty (caller falls back further).
    """
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


# ---------------------------------------------------------------------------
# Resampling
# ---------------------------------------------------------------------------

def resize_premultiplied(image, new_size):
    """LANCZOS resize with alpha premultiplied first, so a transparent
    pixel's own RGB (often black) cannot bleed a dark fringe into the
    resized edge.
    """
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
# Per-creature processing
# ---------------------------------------------------------------------------

def _cells_for_sheet(mask, sheet_w, sheet_h, sheet_spec):
    if "bands" in sheet_spec:
        return cut_cells(mask, sheet_w, sheet_h, cols=sheet_spec["bands"]["cols"], bands=sheet_spec["bands"]["rows"])
    rows, cols = sheet_spec["grid"]
    return cut_cells(mask, sheet_w, sheet_h, cols=cols, rows=rows)


def _load_and_key(sheet_spec, source_dir=SOURCE_DIR):
    path = os.path.join(source_dir, sheet_spec["file"])
    if not os.path.isfile(path):
        sys.exit(f"Missing source sheet: {path}")
    if "/Resources/" in os.path.abspath(path).replace("\\", "/"):
        sys.exit(f"Refusing to read from a Resources/ path (would compound a previous pass): {path}")
    image = Image.open(path)
    return key_sheet(image, sheet_spec["key"])


def _sheet_pieces(sheet_spec, verbose, source_dir=SOURCE_DIR):
    """[(output_name, cropped_native_piece_RGBA), ...] for one sheet, at its
    OWN native size (grid-cut, tight-cropped) -- no scale applied yet."""
    keyed = _load_and_key(sheet_spec, source_dir)
    mask = opaque_mask(keyed)
    boxes = _cells_for_sheet(mask, keyed.width, keyed.height, sheet_spec)
    names = sheet_spec["names"]
    pieces = []
    for i, box in enumerate(boxes):
        name = names[i] if i < len(names) else None
        if box is None:
            if name is not None and verbose:
                print(f"  WARNING: '{name}' cell is empty (no opaque pixels) -- skipped")
            continue
        if name is None:
            continue
        piece = keyed.crop(box)
        pieces.append((name, piece))
    return pieces


def _sqrt_lcc_mass(piece):
    mask = np.array(opaque_mask(piece)) > 0
    core = largest_connected_component(mask)
    return float(np.sqrt(core.sum())) if core.any() else 0.0


def suggest_scales(actor_id):
    spec = ACTORS[actor_id]
    source_dir = spec.get("source_dir", SOURCE_DIR)
    print(f"[{actor_id}] measured per-sheet median sqrt(LCC-mass), native scale:")
    medians = []
    for sheet_spec in spec["sheets"]:
        pieces = _sheet_pieces(sheet_spec, verbose=False, source_dir=source_dir)
        masses = sorted(_sqrt_lcc_mass(p) for _, p in pieces)
        median = masses[len(masses) // 2] if masses else 0.0
        medians.append((sheet_spec["file"], median))
        print(f"  {sheet_spec['file']:30s} median={median:7.1f}  (declared scale: {sheet_spec.get('scale', 1.0)})")

    if len(medians) >= 2:
        reference = medians[0][1]
        print(f"  Suggested 'scale' relative to '{medians[0][0]}' (the first sheet listed):")
        for name, median in medians[1:]:
            if median > 0:
                print(f"    {name}: {reference / median:.3f}")


def _guard_cross_sheet_scale(actor_id, spec, sheet_pieces_by_index):
    if len(spec["sheets"]) < 2:
        return
    correcteds = []
    for sheet_spec, pieces in zip(spec["sheets"], sheet_pieces_by_index):
        masses = sorted(_sqrt_lcc_mass(p) for _, p in pieces)
        median = masses[len(masses) // 2] if masses else 0.0
        scale = sheet_spec.get("scale", 1.0)
        correcteds.append((sheet_spec, median * scale))

    for i in range(len(correcteds)):
        for j in range(i + 1, len(correcteds)):
            spec_a, mass_a = correcteds[i]
            spec_b, mass_b = correcteds[j]
            if mass_a <= 0 or mass_b <= 0:
                continue
            ratio = max(mass_a, mass_b) / min(mass_a, mass_b)
            both_default = "scale" not in spec_a and "scale" not in spec_b
            if ratio - 1.0 > SHEET_SCALE_MISMATCH_GUARD and both_default:
                sys.exit(
                    f"[{actor_id}] '{spec_a['file']}' and '{spec_b['file']}' disagree by "
                    f"{(ratio - 1.0) * 100:.0f}% in corrected mass and neither carries an explicit "
                    f"'scale'. Run --suggest-scales {actor_id} and add one before writing."
                )


def process_actor(actor_id, verbose=True, prune=False):
    if actor_id not in ACTORS:
        sys.exit(f"Unknown actor '{actor_id}'. Known: {', '.join(sorted(ACTORS))}")
    spec = ACTORS[actor_id]
    out_dir = os.path.join(spec.get("root", OUTPUT_ROOT_ENEMIES), actor_id)
    source_dir = spec.get("source_dir", SOURCE_DIR)
    anchor_mode = spec.get("anchor", "centroid")
    delivery_scale = spec.get("delivery_scale", 1.0)
    nudges = spec.get("nudge", {})

    print(f"[{actor_id}] anchor={anchor_mode} delivery_scale={delivery_scale} -> {out_dir}")

    # Pass 0: native-scale pieces per sheet (for the cross-sheet guard,
    # which must see UNCORRECTED mass to judge whether a correction was
    # actually needed).
    per_sheet_pieces = [_sheet_pieces(s, verbose, source_dir) for s in spec["sheets"]]
    _guard_cross_sheet_scale(actor_id, spec, per_sheet_pieces)

    # Pass 1: apply each sheet's own scale + the creature's delivery_scale,
    # producing the pieces that will actually be composited. Also recompute
    # each piece's tight bbox post-resize (LANCZOS can leave a few
    # near-transparent border pixels).
    pieces = []  # [(name, piece_image, local_mask_bool, local_bbox)]
    for sheet_spec, raw_pieces in zip(spec["sheets"], per_sheet_pieces):
        total_scale = sheet_spec.get("scale", 1.0) * delivery_scale
        for name, piece in raw_pieces:
            if total_scale != 1.0:
                new_size = (max(1, round(piece.width * total_scale)), max(1, round(piece.height * total_scale)))
                piece = resize_premultiplied(piece, new_size)
            mask_img = opaque_mask(piece)
            bbox = mask_img.getbbox()
            if bbox is None:
                if verbose:
                    print(f"  WARNING: '{name}' has no opaque pixels after scaling -- skipped")
                continue
            piece = piece.crop(bbox)
            mask_bool = np.array(opaque_mask(piece)) > 0
            pieces.append((name, piece, mask_bool))

    if not pieces:
        sys.exit(f"[{actor_id}]: no pieces produced at all.")

    # Pass 2: where each piece's FEET are, in its own local rows.
    #
    # Not the same thing as the bottom of its bounding box, and conflating the
    # two is what made the golem appear to fly. Its slam frames throw dust and
    # gravel that settles 30-55px BELOW the creature's own feet, with a band of
    # completely empty rows in between. Landing the bbox bottom on the ground
    # line therefore lands the DUST on the ground line and hoists the golem
    # into the air -- by 54px on its recovery frames, against an idle frame
    # whose feet sit right on the line. Switching pose then read as taking off.
    #
    # The largest connected component already answers this correctly (dust is
    # a separate component; the creature is the big one) and was already being
    # computed for the horizontal anchor -- it had simply never been consulted
    # for the vertical one.
    #
    # "centroid" mode keeps using the bbox bottom, deliberately: it is the
    # faithful port of the original behaviour and bog_witch rides on it as the
    # untouched control. The arithmetic below is arranged so that mode comes
    # out byte-identical (foot_y = height-1 makes max_below 0, which collapses
    # every formula here back to what it was).
    footed = []  # [(name, piece, foot_y)]
    for name, piece, mask_bool in pieces:
        if anchor_mode == "ground_band":
            core = largest_connected_component(mask_bool)
            core_rows = np.nonzero(core)[0]
            foot_y = int(core_rows.max()) if len(core_rows) else piece.height - 1
        else:
            foot_y = piece.height - 1
        footed.append((name, piece, foot_y))

    # The canvas has to hold the tallest piece measured UP from its feet, plus
    # the deepest anything hangs BELOW its feet -- otherwise the dust the fix
    # above stops standing on would simply fall off the bottom edge instead.
    max_above = max(foot_y + 1 for _, _, foot_y in footed)
    max_below = max(p.height - 1 - foot_y for _, p, foot_y in footed)
    canvas_h = int(max_above + max_below) + PADDING * 2
    # One past the foot row, matching the convention the old bbox-bottom
    # placement used, so "centroid" pieces land on exactly the pixel they did
    # before.
    ground_y = PADDING + int(max_above)
    foot_of = {name: foot_y for name, _, foot_y in footed}

    # THE NUMBER StanceManifest.json's per-actor "groundLine" WANTS: pixels
    # from the canvas's bottom edge up to the ground every frame is placed on.
    # canvas_h - ground_y rather than a second constant, so it can never drift
    # from the canvas this function just built -- it is PADDING for anchor
    # modes that place every foot on the same row (which is every mode here),
    # printed rather than assumed because a future anchor mode could vary it.
    if verbose:
        print(f"  groundLine {canvas_h - ground_y}  (StanceManifest.json wants that number)")

    # Pass 3: per-piece anchor_x in LOCAL (post-crop) coordinates.
    anchored = []
    for name, piece, mask_bool in pieces:
        if anchor_mode == "ground_band":
            result = ground_band_anchor(mask_bool, canvas_h)
            if result is None:
                cx = piece.width / 2.0
                path = "empty_fallback"
            else:
                cx, core, path = result
                core_frac = core.sum() / max(1, mask_bool.sum())
                if core_frac < 1.0 - LCC_DROP_WARN_FRACTION:
                    print(f"  WARNING: '{name}' -- largest connected component covers only "
                          f"{core_frac * 100:.0f}% of opaque mass (debris or a detached part?)")
            if path != "ground_band" and verbose:
                print(f"  '{name}': anchor path = {path}")
        else:
            local_mask_img = Image.fromarray((mask_bool * 255).astype(np.uint8), "L")
            cx = alpha_centroid_x(local_mask_img, (0, 0, piece.width, piece.height))
        anchored.append((name, piece, cx))

    max_left = max(cx for _, piece, cx in anchored)
    max_right = max(piece.width - cx for _, piece, cx in anchored)
    canvas_w = int(max_left + max_right) + PADDING * 2
    anchor_x = int(max_left) + PADDING

    os.makedirs(out_dir, exist_ok=True)
    written_relpaths = set()
    for name, piece, cx in anchored:
        canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        dx, dy = nudges.get(name, (0, 0))
        paste_x = int(round(anchor_x - cx)) + dx
        # Aligned on the FEET, not the bounding box -- see Pass 2. Reduces to
        # the old `ground_y - piece.height` whenever foot_y is the last row,
        # which is every "centroid" piece.
        paste_y = int(ground_y - 1 - foot_of[name]) + dy
        canvas.paste(piece, (paste_x, paste_y), piece)

        rel_path = f"{name}.png"
        full_path = os.path.join(out_dir, rel_path)
        os.makedirs(os.path.dirname(full_path), exist_ok=True)
        canvas.save(full_path)
        written_relpaths.add(rel_path.replace("\\", "/"))
        if verbose:
            print(f"  {rel_path}  {canvas_w}x{canvas_h}")

    # Aliases: byte-for-byte copies, preserving shape (flat file vs frame folder).
    for alias_name, target_name in spec.get("aliases", {}).items():
        flat_target = f"{target_name}.png"
        if flat_target.replace("\\", "/") in written_relpaths:
            src = os.path.join(out_dir, flat_target)
            dst = os.path.join(out_dir, f"{alias_name}.png")
            _copy_file(src, dst)
            written_relpaths.add(f"{alias_name}.png")
            if verbose:
                print(f"  {alias_name}.png = alias of {flat_target}")
            continue

        folder_prefix = f"{target_name}/"
        matched = [r for r in written_relpaths if r.startswith(folder_prefix)]
        if not matched:
            print(f"  WARNING: alias '{alias_name}' -> '{target_name}' has no target output to copy")
            continue
        for rel in matched:
            frame_name = rel[len(folder_prefix):]
            src = os.path.join(out_dir, target_name, frame_name)
            dst_dir = os.path.join(out_dir, alias_name)
            os.makedirs(dst_dir, exist_ok=True)
            dst = os.path.join(dst_dir, frame_name)
            _copy_file(src, dst)
            written_relpaths.add(f"{alias_name}/{frame_name}")
        if verbose:
            print(f"  {alias_name}/ = alias of {target_name}/ ({len(matched)} frame(s))")

    _assert_one_ground_line(actor_id, out_dir, written_relpaths, verbose)
    _report_stray_files(out_dir, written_relpaths, prune, verbose)
    return written_relpaths


# Every pose an actor ships has to STAND in the same place.
#
# The stage pins each actor's canvas bottom to the ground line, so a pose whose
# figure sits higher inside its own canvas visibly takes off the moment the
# stance changes. The golem shipped exactly that: its slam frames throw dust
# that settles 30-55px below its feet, the old placement aligned the bottom of
# the whole BOUNDING BOX (dust included), and the creature got hoisted 54px
# into the air against an idle frame standing right on the line. Shawn had the
# same defect at 34px. In game both read as the figure launching upward.
#
# Verified against the PNGs actually WRITTEN rather than against the in-memory
# pieces, so it also covers nudges, aliases, and anything a future change does
# between placement and disk.
#
# This lives in the tool and not in a PlayMode test on purpose, for the same
# reason the cross-sheet scale guard does. A C# test reads the sprite Unity
# imported, and these textures import COMPRESSED (BC3 quantises alpha in 4x4
# blocks) -- measured through that, a prone pose's largest component moved by
# over 100px, which is noise swamping the 6px signal. The tool reads the source
# pixels and can be exact.
def _assert_one_ground_line(actor_id, out_dir, written_relpaths, verbose):
    MAX_SPREAD_PX = 6

    feet = []
    for rel in sorted(written_relpaths):
        path = os.path.join(out_dir, rel.replace("/", os.sep))
        if not os.path.exists(path):
            continue
        with Image.open(path) as img:
            mask = np.array(img.convert("RGBA"))[:, :, 3] > ALPHA_THRESHOLD
        if not mask.any():
            continue  # a legitimately empty frame has no footing to compare
        core = largest_connected_component(mask)
        rows = np.nonzero(core)[0]
        if not len(rows):
            continue
        feet.append((rel, int(rows.max())))

    if len(feet) < 2:
        return

    lowest = min(f for _, f in feet)
    highest = max(f for _, f in feet)
    spread = highest - lowest
    if spread <= MAX_SPREAD_PX:
        if verbose:
            print(f"  ground line: all {len(feet)} pose(s) within {spread}px")
        return

    table = ", ".join(f"{rel}={row}" for rel, row in sorted(feet, key=lambda t: t[1]))
    sys.exit(
        f"[{actor_id}]: poses do not share one ground line -- they span {spread}px "
        f"(max {MAX_SPREAD_PX}). The figure will appear to fly when its stance changes. "
        f"Rows are measured from the top, so a SMALLER number means the figure stands "
        f"HIGHER in its canvas: {table}"
    )


def _copy_file(src, dst):
    with open(src, "rb") as f:
        data = f.read()
    with open(dst, "wb") as f:
        f.write(data)


def _report_stray_files(out_dir, written_relpaths, prune, verbose):
    existing = []
    for root, _dirs, files in os.walk(out_dir):
        for fname in files:
            if not fname.lower().endswith(".png"):
                continue
            rel = os.path.relpath(os.path.join(root, fname), out_dir).replace("\\", "/")
            existing.append(rel)

    stray = sorted(set(existing) - written_relpaths)
    if not stray:
        return
    if prune:
        for rel in stray:
            os.remove(os.path.join(out_dir, rel))
            meta = os.path.join(out_dir, rel + ".meta")
            if os.path.exists(meta):
                os.remove(meta)
        if verbose:
            print(f"  pruned {len(stray)} file(s) the manifest did not produce: {', '.join(stray)}")
    else:
        print(f"  NOTE: {len(stray)} file(s) in {out_dir} were not produced by this run "
              f"(pass --prune to remove): {', '.join(stray)}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("creatures", nargs="*", help="Creature ids to process (default: all)")
    ap.add_argument("--all", action="store_true", help="Process every creature in ACTORS")
    ap.add_argument("--suggest-scales", metavar="CREATURE", help="Print measured per-sheet scale ratios and stop")
    ap.add_argument("--prune", action="store_true", help="Delete output files the manifest no longer produces")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    if args.suggest_scales:
        suggest_scales(args.suggest_scales)
        return

    ids = list(ACTORS.keys()) if args.all else args.creatures
    if not ids:
        ap.error("Give one or more creature ids, or --all, or --suggest-scales <id>")

    for actor_id in ids:
        process_actor(actor_id, verbose=not args.quiet, prune=args.prune)


if __name__ == "__main__":
    main()
