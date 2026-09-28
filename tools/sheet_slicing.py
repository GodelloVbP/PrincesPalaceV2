#!/usr/bin/env python3
"""Geometry primitives shared by the sprite-sheet slicers.

`slice_actor_sheet.py` and `slice_item_sheet.py` solve different problems --
one composites poses onto a shared anchored canvas, the other cuts an item
grid into per-level icons -- but they cut cells out of a sheet the same way,
and until this module existed they did it with four byte-identical copies of
the same functions plus three redeclared constants. That is the setup for a
silent drift: a fix to `best_cut`'s tie-breaking in one slicer would leave the
other on the old behaviour, with nothing to say so.

Per `docs/CODE_STANDARDS.md` "Reuse", the second copy-paste of a pattern is
the signal to promote it. This is that promotion, for the cell-cutting layer
only -- anchoring, keying and canvas composition stay with whichever slicer
owns them, because those genuinely differ.

Nothing here loads or writes a file; it is pure measurement over a PIL image
and plain Python lists, which is what makes it safe to share.
"""

# A pixel counts as opaque above this alpha. Deliberately above zero: keying
# leaves a fringe of near-transparent pixels around a cut-out edge, and
# treating those as content pulls every bounding box a few pixels wide.
ALPHA_THRESHOLD = 16

# How far from a nominal cut we may search for a gap, as a fraction of cell
# size. Wide enough to clear real bleed, narrow enough that a cut can never
# wander into the neighbouring figure.
SEARCH_FRACTION = 0.22

# Transparent breathing room around the tallest/widest piece, so a sprite
# never sits flush against its own texture edge.
PADDING = 8


def opaque_mask(image):
    """1-bit-ish mask (0 or 255) of everything above ALPHA_THRESHOLD."""
    return image.getchannel("A").point(lambda v: 255 if v > ALPHA_THRESHOLD else 0)


def column_weights(mask, x0, x1, y0, y1):
    """Opaque pixel count for each column in [x0,x1) within rows [y0,y1)."""
    region = mask.crop((x0, y0, x1, y1))
    w, h = region.size
    px = region.load()
    return [sum(1 for y in range(h) if px[x, y]) for x in range(w)]


def row_weights(mask, x0, x1, y0, y1):
    """Opaque pixel count for each row in [y0,y1) within columns [x0,x1)."""
    region = mask.crop((x0, y0, x1, y1))
    w, h = region.size
    px = region.load()
    return [sum(1 for x in range(w) if px[x, y]) for y in range(h)]


def best_cut(weights, offset, nominal, search):
    """Pick the emptiest position near `nominal`, ties breaking toward it.

    A nominal even split lands wherever the arithmetic says, which on a
    hand-drawn sheet is regularly a few pixels inside a figure. Searching a
    bounded window for the emptiest column/row moves the cut into the gutter
    the artist actually left, and the distance tie-break keeps it from
    drifting off to a marginally emptier position further away.
    """
    lo = max(0, nominal - search - offset)
    hi = min(len(weights) - 1, nominal + search - offset)
    if lo > hi:
        return nominal
    best_i, best_key = None, None
    for i in range(lo, hi + 1):
        key = (weights[i], abs((offset + i) - nominal))
        if best_key is None or key < best_key:
            best_i, best_key = i, key
    return offset + best_i
