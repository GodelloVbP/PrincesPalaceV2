#!/usr/bin/env python3
"""Tests for tools/portrait_from_stance.py. Stdlib unittest, PIL, temp dirs.

    python tools/portrait_from_stance_test.py

END TO END THROUGH THE SCRIPT, not over crop_box(). The geometry in crop_box
was never the part that was wrong -- what was wrong sat in main(), between a
correct box and the file on disk, and a test over the function alone would
have watched the bug go past.

THE ONE CONTRACT THESE ALL CHECK: the output has the reference portrait's
ASPECT. The tool's own docstring puts it as "same aspect, not same canvas
size", and the dossier plate is drawn at a fixed ratio, so an output at the
wrong ratio is either stretched or letterboxed by whatever draws it -- a
defect that survives every look at the PNG on its own, because on its own it
looks fine.

The stills are synthesised rather than checked in. Each one is a shape chosen
to put the crop box somewhere specific relative to the canvas edge, and a
committed PNG that has to keep a particular alpha silhouette to keep meaning
that is a PNG that quietly stops meaning it.
"""

import os
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

TOOL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "portrait_from_stance.py")

# The shipped dossier portrait's ratio, which is what the tool measures its
# target aspect from: Portraits/sheep.png is 1122x1402.
REFERENCE_SIZE = (1122, 1402)
REFERENCE_ASPECT = REFERENCE_SIZE[0] / REFERENCE_SIZE[1]


def still_with(width, height, blobs):
    """A transparent canvas with opaque rectangles at (x0, y0, x1, y1) inclusive."""
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for x0, y0, x1, y1 in blobs:
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                img.putpixel((x, y), (200, 120, 80, 255))
    return img


class OutputKeepsTheReferenceAspectTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.reference = os.path.join(self.dir, "reference.png")
        Image.new("RGBA", REFERENCE_SIZE, (0, 0, 0, 255)).save(self.reference)

    def tearDown(self):
        import shutil

        shutil.rmtree(self.dir, ignore_errors=True)

    def crop(self, still, height_fraction=0.45):
        still_path = os.path.join(self.dir, "idle.png")
        out_path = os.path.join(self.dir, "out.png")
        still.save(still_path)

        result = subprocess.run(
            [sys.executable, TOOL,
             "--still", still_path,
             "--out", out_path,
             "--aspect-reference", self.reference,
             "--height-fraction", str(height_fraction)],
            capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

        with Image.open(out_path) as out:
            return out.size

    def assertReferenceAspect(self, size):
        aspect = size[0] / size[1]
        # One pixel of integer rounding on each axis, no more. The failure
        # this guards was 25% out, so the tolerance is not what decides it.
        self.assertAlmostEqual(
            REFERENCE_ASPECT, aspect, delta=0.01,
            msg="output is {}x{} (aspect {:.4f}); the reference is {:.4f}".format(
                size[0], size[1], aspect, REFERENCE_ASPECT))

    def test_a_figure_sitting_clear_of_every_edge(self):
        # The control: the crop box falls entirely inside the canvas, so
        # nothing is asked of the source that it does not have.
        still = still_with(400, 600, [(160, 200, 240, 280), (140, 281, 260, 500)])

        self.assertReferenceAspect(self.crop(still))

    def test_a_head_touching_the_top_of_the_canvas(self):
        # The crop wants clear air above the crown (TOP_MARGIN) and the
        # canvas has none: the box starts above row 0. Clamping it to row 0
        # is what shortened the shipped bear portrait and tilted its ratio.
        still = still_with(400, 600, [(160, 0, 240, 80), (140, 81, 260, 500)])

        self.assertReferenceAspect(self.crop(still))

    def test_a_head_band_wider_than_the_canvas_is_tall(self):
        # The severe one, and the shape a real actor has: a wide head band
        # (ears, a hood, a bird's head against its shoulders) on a canvas
        # that is not much taller than it is wide. Width comes from the
        # band, height comes from width via the aspect -- and that height
        # can exceed the whole canvas, so the bottom was being cut off by a
        # quarter with nothing said about it.
        still = still_with(600, 360, [(100, 10, 500, 120), (250, 121, 350, 350)])

        self.assertReferenceAspect(self.crop(still))

    def test_a_figure_hard_against_the_left_edge(self):
        # Same failure on the other axis: the box wants margin to the left
        # of a head that has none.
        still = still_with(400, 600, [(0, 40, 90, 120), (0, 121, 120, 500)])

        self.assertReferenceAspect(self.crop(still))


class TheHeadBandSurvivesThePadTests(unittest.TestCase):
    """Padding must add clear air, never move or eat the head.

    The whole reason the crop is centred on the head band rather than on the
    figure's bbox is that the face has to be whole and centred; a fix for the
    aspect that achieved it by shifting the box would have given up the thing
    the box is for.
    """

    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.reference = os.path.join(self.dir, "reference.png")
        Image.new("RGBA", REFERENCE_SIZE, (0, 0, 0, 255)).save(self.reference)

    def tearDown(self):
        import shutil

        shutil.rmtree(self.dir, ignore_errors=True)

    def test_every_opaque_pixel_of_the_head_band_is_still_in_frame(self):
        still = still_with(600, 360, [(100, 10, 500, 120), (250, 121, 350, 350)])
        still_path = os.path.join(self.dir, "idle.png")
        out_path = os.path.join(self.dir, "out.png")
        still.save(still_path)

        subprocess.run(
            [sys.executable, TOOL, "--still", still_path, "--out", out_path,
             "--aspect-reference", self.reference, "--height-fraction", "0.45"],
            capture_output=True, text=True, check=True)

        with Image.open(out_path) as out:
            width, height = out.size
            alpha = out.convert("RGBA").getchannel("A")

        # The band is 400px of the 600px-wide source. Whatever scale the
        # output landed at, the opaque run across the head's own rows must
        # not be touching either side of the frame -- if it were, the band
        # had been cut rather than padded.
        row = alpha.crop((0, 0, width, max(1, height // 8)))
        columns = [x for x in range(width)
                   if any(row.getpixel((x, y)) > 0 for y in range(row.height))]
        self.assertTrue(columns, "the head band vanished from the crop entirely")
        self.assertGreater(min(columns), 0, "the head band is cut off at the left edge")
        self.assertLess(max(columns), width - 1, "the head band is cut off at the right edge")


if __name__ == "__main__":
    unittest.main(verbosity=2)
