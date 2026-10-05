#!/usr/bin/env python3
"""Tests for the backdrop-colour keying model in tools/key_green_screen.py.

    python tools/key_green_screen_test.py

Synthetic images only.
"""
import os
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import key_green_screen as kg  # noqa: E402
import slice_spell_sheet as sss  # noqa: E402


def magenta_sheet():
    """32x32 on #FF00FF: a venom-green disc, a spilled backdrop pixel, a reddish blot."""
    arr = np.zeros((32, 32, 3), np.uint8)
    arr[...] = (255, 0, 255)
    arr[8:24, 8:24] = (30, 200, 60)
    arr[2, 2] = (255, 40, 255)
    arr[28:31, 28:31] = (200, 60, 50)
    return arr


class MagentaKeyTests(unittest.TestCase):
    def test_green_subject_survives_and_backdrop_clears(self):
        out = np.asarray(kg.key_out(Image.fromarray(magenta_sheet()), "magenta"))
        self.assertEqual(tuple(out[16, 16]), (30, 200, 60, 255))
        self.assertEqual(out[0, 0, 3], 0)
        self.assertEqual(out[2, 2, 3], 0)

    def test_magenta_lean_on_a_kept_pixel_is_pulled_toward_neutral(self):
        arr = magenta_sheet()
        arr[16, 16] = (120, 100, 130)  # dominance 20: kept, partly transparent
        out = np.asarray(kg.key_out(Image.fromarray(arr), "magenta"))
        r, g, b, a = (int(v) for v in out[16, 16])
        self.assertGreater(a, 0)
        self.assertEqual((r, g, b), (100, 100, 110))

    def test_a_reddish_subject_keeps_its_hue(self):
        out = np.asarray(kg.key_out(Image.fromarray(magenta_sheet()), "magenta"))
        self.assertEqual(tuple(out[29, 29]), (200, 60, 50, 255))

    def test_sliced_sheet_has_no_pink_fringe(self):
        with tempfile.TemporaryDirectory() as root:
            path = os.path.join(root, "venom_sheet.png")
            Image.fromarray(magenta_sheet()).save(path)
            cells = sss.cut_cells(path, 1, 1, ["f0"], keyed="magenta")
            arr = np.asarray(cells["f0"]).astype(int)
            self.assertEqual(int(arr[0, 0, 3]), 0)
            pink = (arr[..., 3] > 0) & (np.minimum(arr[..., 0], arr[..., 2]) - arr[..., 1] > 0)
            self.assertFalse(pink.any())
            self.assertEqual(tuple(arr[16, 16]), (30, 200, 60, 255))


class GreenKeyUnchangedTests(unittest.TestCase):
    def test_green_path_matches_the_pinned_formulas(self):
        rng = np.random.default_rng(7)
        arr = rng.integers(0, 256, (24, 24, 3), dtype=np.uint8)
        out = np.asarray(kg.key_out_green(Image.fromarray(arr))).astype(int)
        r, g, b = (arr[..., i].astype(int) for i in range(3))
        dom = g - np.maximum(r, b)
        keep = dom < kg.FULLY_TRANSPARENT_ABOVE
        want_g = np.where(dom > 0, np.maximum(r, b), g)
        self.assertTrue((out[..., 1][keep] == want_g[keep]).all())
        self.assertTrue((out[..., 3][~keep] == 0).all())


if __name__ == "__main__":
    unittest.main()
