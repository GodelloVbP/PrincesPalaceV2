#!/usr/bin/env python3
"""Tests for tools/process_reward_art.py (synthetic images only)."""
import os
import sys
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import process_reward_art as pra  # noqa: E402


class TrimWispsTests(unittest.TestCase):
    def test_a_thin_thread_past_the_edge_goes_and_the_body_stays(self):
        arr = np.zeros((80, 80, 4), np.uint8)
        arr[20:60, 20:60] = (90, 60, 120, 255)
        arr[38:41, 12:20] = (200, 160, 60, 255)  # 3 px thread poking out the left
        out = np.asarray(pra.trim_wisps(Image.fromarray(arr), 6))
        self.assertEqual(out[39, 14, 3], 0)
        self.assertEqual(out[40, 40, 3], 255)


class GradeTests(unittest.TestCase):
    def test_grade_darkens_and_desaturates(self):
        arr = np.zeros((1, 1, 4), np.uint8)
        arr[0, 0] = (160, 100, 120, 255)
        out = np.asarray(pra.grade(Image.fromarray(arr), 0.2, 0.5))[0, 0]
        self.assertLess(int(out[:3].max()), 90)
        self.assertLess(int(out[:3].max()) - int(out[:3].min()), 20)
        self.assertEqual(out[3], 255)


class RestoreOpaqueTests(unittest.TestCase):
    def test_violet_cloth_keeps_its_colour_and_the_backdrop_goes(self):
        arr = np.zeros((40, 40, 3), np.uint8)
        arr[:] = (240, 10, 245)                 # magenta backdrop
        arr[10:30, 10:30] = (90, 50, 85)        # cloth, magentaness 35
        src = Image.fromarray(arr, "RGB")
        keyed = pra.key_out(src, "magenta", 55, 150)
        out = np.asarray(pra.restore_opaque(src, keyed, "magenta", 55))
        self.assertEqual(tuple(out[20, 20]), (90, 50, 85, 255))
        self.assertEqual(out[2, 2, 3], 0)


if __name__ == "__main__":
    unittest.main()
