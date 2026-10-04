#!/usr/bin/env python3
"""Pure checks for tools/make_spell_cast_cues.cut (no Unity, no source packs):
python tools/make_spell_cast_cues_test.py"""

import os
import sys
import unittest

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_spell_cast_cues as cues  # noqa: E402

RATE = cues.RATE


class CutTests(unittest.TestCase):
    def test_a_window_inside_the_source_is_exactly_length(self):
        x = np.ones(RATE * 2) * 0.5
        self.assertEqual(len(cues.cut(x, 1.0, 0.3)), round(0.3 * RATE))

    def test_a_peak_earlier_than_length_pads_the_head(self):
        x = np.ones(RATE) * 0.5
        out = cues.cut(x, 0.1, 0.3)
        self.assertEqual(len(out), round(0.3 * RATE))
        self.assertEqual(out[0], 0.0)

    def test_after_past_the_source_end_pads_the_tail_to_full_length(self):
        x = np.ones(int(RATE * 0.5)) * 0.5      # source ends at 0.5 s
        out = cues.cut(x, 0.49, 0.34, after=0.03)  # window ends at 0.52 s
        self.assertEqual(len(out), round(0.34 * RATE))
        self.assertEqual(out[-1], 0.0)
        self.assertGreater(np.abs(out[len(out) // 2]), 0.0)

    def test_after_with_a_head_pad_and_tail_pad_still_exact(self):
        x = np.ones(int(RATE * 0.1)) * 0.5
        out = cues.cut(x, 0.09, 0.34, after=0.03)
        self.assertEqual(len(out), round(0.34 * RATE))


if __name__ == "__main__":
    unittest.main()
