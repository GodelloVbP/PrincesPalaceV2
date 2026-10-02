#!/usr/bin/env python3
"""Tests for the curve math and warning thresholds in tools/spell_feel.py, on
small synthetic frames and curves. Standard library plus numpy and Pillow.

Run: python tools/spell_feel_test.py
"""

import json
import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))

import spell_feel  # noqa: E402

FPS = 60


def curve(n=100, floor=1.0):
    return [floor] * n


def cast(gather=36, travel=6, spike=20.0, tail=0, tail_level=0.0, n=100):
    """Flat 3.0 gather, a release rise, travel at 5.0, a spike at contact, an
    optional tail, then a clean floor of 1.0."""
    a = [3.0] * n
    release = gather
    hit = gather + travel
    for i in range(release, hit):
        a[i] = 5.0
    for i in range(hit, hit + 5):
        a[i] = spike
    for i in range(hit + 5, hit + 5 + tail):
        a[i] = tail_level
    for i in range(hit + 5 + tail, n):
        a[i] = 1.0
    return a, hit, release


class FrameMathTests(unittest.TestCase):
    def test_activity_is_mean_abs_diff_from_last_frame_over_mask(self):
        black = np.zeros((4, 4, 3), dtype=np.uint8)
        lit = black.copy()
        lit[:2, :, :] = 10
        mask = np.ones((4, 4), dtype=bool)
        mask[:, 3] = False
        self.assertEqual(spell_feel.activity_curve([lit, black], mask), [5.0, 0.0])

    def test_masked_out_pixels_do_not_count(self):
        black = np.zeros((4, 4, 3), dtype=np.uint8)
        lit = black.copy()
        lit[0, 0, :] = 200
        mask = np.ones((4, 4), dtype=bool)
        mask[0, 0] = False
        self.assertEqual(spell_feel.activity_curve([lit, black], mask), [0.0, 0.0])

    def test_hot_counts_pixels_with_all_channels_at_level(self):
        frame = np.zeros((2, 2, 3), dtype=np.uint8)
        frame[0, 0] = (255, 255, 255)
        frame[0, 1] = (255, 255, 200)
        mask = np.ones((2, 2), dtype=bool)
        self.assertEqual(spell_feel.hot_curve([frame], mask), [0.25])

    def test_stage_mask_removes_hud_and_scales_with_frame_size(self):
        for shape in ((270, 480), (1080, 1920)):
            mask = spell_feel.stage_mask(shape)
            h, w = shape
            self.assertFalse(mask[0, 0])
            self.assertFalse(mask[h - 1, w // 2])
            self.assertTrue(mask[h // 2, w // 2])
            self.assertTrue(0.55 < mask.mean() < 0.8)


class BeatTimingTests(unittest.TestCase):
    def test_release_gather_travel_come_from_the_rises(self):
        a, hit, release = cast(gather=36, travel=6)
        r = spell_feel.analyse(a, [0.0] * len(a), FPS, hit)
        self.assertEqual(r["release_frame"], 36)
        self.assertAlmostEqual(r["gather_s"], 0.6)
        self.assertAlmostEqual(r["travel_s"], 0.1)
        self.assertAlmostEqual(r["gather_to_travel"], 6.0)
        self.assertEqual(r["visual_contact_frame"], hit)

    def test_clean_frame_is_first_frame_that_stays_at_floor(self):
        a, hit, _ = cast(spike=20.0)
        r = spell_feel.analyse(a, [0.0] * len(a), FPS, hit)
        self.assertEqual(r["clean_frame"], hit + 5)
        self.assertAlmostEqual(r["floor"], 1.0)

    def test_hot_peak_frame(self):
        a, hit, _ = cast()
        hot = [0.0] * len(a)
        hot[44] = 0.02
        self.assertEqual(spell_feel.analyse(a, hot, FPS, hit)["hot_peak_frame"], 44)


class WarningTests(unittest.TestCase):
    def analyse(self, a, hit, cue=None, **kw):
        return spell_feel.analyse(a, [0.0] * len(a), FPS, hit, cue, **kw)

    def test_clean_cast_has_no_warnings(self):
        a, hit, _ = cast()
        self.assertEqual(self.analyse(a, hit, 0.7)["warnings"], [])

    def test_residue_is_measured_to_the_last_frame_above_a_quarter_of_peak(self):
        a, hit, _ = cast(spike=21.0, tail=12, tail_level=6.0)  # 1 + 0.25 * 20 = 6.0 is not above
        self.assertAlmostEqual(self.analyse(a, hit)["residue_s"], 4 / FPS)
        a, hit, _ = cast(spike=21.0, tail=12, tail_level=6.5)
        self.assertAlmostEqual(self.analyse(a, hit)["residue_s"], 16 / FPS)

    def test_long_tail_warns_and_the_limit_is_a_parameter(self):
        a, hit, _ = cast(spike=21.0, tail=40, tail_level=8.0)
        r = self.analyse(a, hit)
        self.assertTrue(any(w.startswith("residue tail") for w in r["warnings"]))
        r = self.analyse(a, hit, residue_limit=1.0)
        self.assertFalse(any(w.startswith("residue tail") for w in r["warnings"]))

    def test_short_tail_does_not_warn(self):
        a, hit, _ = cast(spike=21.0, tail=10, tail_level=8.0)
        self.assertEqual(self.analyse(a, hit)["warnings"], [])

    def test_flat_curve_has_no_accent(self):
        a = [4.0] * 100
        for i in range(90, 100):
            a[i] = 1.0
        a[50] = 5.0
        r = self.analyse(a, None)
        self.assertTrue(any(w.startswith("no accent") for w in r["warnings"]))

    def test_two_beat_cast_still_has_an_accent(self):
        a, hit, _ = cast(spike=20.0)
        a[36] = 12.0  # release flash nearly as high as contact
        self.assertFalse(any(w.startswith("no accent") for w in self.analyse(a, hit)["warnings"]))

    def test_contact_desync_beyond_two_frames_warns(self):
        a, hit, _ = cast(gather=36, travel=6)
        self.assertEqual(self.analyse(a, hit, 0.7 + 2 / FPS)["warnings"], [])
        warnings = self.analyse(a, hit, 0.7 + 4 / FPS)["warnings"]
        self.assertTrue(any(w.startswith("contact desync") for w in warnings))
        self.assertFalse(any(w.startswith("contact desync")
                             for w in self.analyse(a, hit, 0.7 + 4 / FPS, desync_frames=5)["warnings"]))

    def test_missing_hit_frame_skips_timings_and_desync(self):
        a, _, _ = cast()
        r = self.analyse(a, None, 0.7)
        self.assertIsNone(r["gather_s"])
        self.assertFalse(any(w.startswith("contact desync") for w in r["warnings"]))


class RitualAndWindowTests(unittest.TestCase):
    def test_ritual_uses_frame_zero_and_the_recorded_hit_with_no_false_desync(self):
        # A ritual: starts at frame 0, hit cue at 18, then a later hurt flash
        # at 28 that is the biggest rise after the cast's own first rise.
        a = [1.0] * 60
        for i in range(0, 18):
            a[i] = 3.0
        a[18] = 12.0
        for i in range(28, 33):
            a[i] = 15.0
        plain = spell_feel.analyse(a, [0.0] * 60, FPS, 19, 18 / FPS)
        self.assertTrue(any(w.startswith("contact desync") for w in plain["warnings"]))
        r = spell_feel.analyse(a, [0.0] * 60, FPS, 19, 18 / FPS, ritual=True)
        self.assertEqual(r["release_frame"], 0)
        self.assertEqual(r["hit_frame"], 19)
        self.assertIsNone(r["visual_contact_frame"])
        self.assertAlmostEqual(r["gather_s"], 0.0)
        self.assertFalse(any(w.startswith("contact desync") for w in r["warnings"]))

    def test_ritual_accepts_a_hit_on_frame_zero(self):
        a = [5.0, 4.0] + [1.0] * 40
        r = spell_feel.analyse(a, [0.0] * 42, FPS, 0, 0.0, ritual=True)
        self.assertEqual(r["hit_frame"], 0)

    def test_is_ritual_needs_a_release_layer_with_nothing_delayed(self):
        self.assertTrue(spell_feel.is_ritual({"layers": [{"at": "release"}, {"at": "hit"}]}))
        self.assertFalse(spell_feel.is_ritual(
            {"layers": [{"at": "release"}, {"at": "release", "offset": 0.5}]}))
        self.assertFalse(spell_feel.is_ritual({"layers": [{"at": "release", "travelSeconds": 0.1}]}))
        self.assertTrue(spell_feel.is_ritual({"layers": [{"at": "hit"}]}))
        self.assertFalse(spell_feel.is_ritual({"layers": [{"at": "start"}]}))
        self.assertFalse(spell_feel.is_ritual(None))

    def test_window_ends_at_cleared_time_so_a_later_tick_is_not_measured(self):
        self.assertEqual(spell_feel.window_end(138, FPS, 0.9), 61)
        self.assertEqual(spell_feel.window_end(50, FPS, 2.0), 50)
        self.assertEqual(spell_feel.window_end(138, FPS, None), 138)
        a = [1.0] * 138
        a[30] = 10.0
        for i in range(97, 102):
            a[i] = 20.0  # the poison tick on the next turn
        end = spell_feel.window_end(len(a), FPS, 0.9)
        r = spell_feel.analyse(a[:end], [0.0] * end, FPS, 30, None)
        self.assertEqual(r["peak_frame"], 30)
        self.assertLess(r["residue_s"], 0.5)

    def test_authored_seconds_are_divided_by_speed(self):
        authored = {"hitCueSeconds": 0.9, "release_s": 0.6, "travel_s": 0.3, "layers": []}
        scaled = spell_feel.scale_authored(authored, 1.5)
        self.assertAlmostEqual(scaled["hitCueSeconds"], 0.6)
        self.assertAlmostEqual(scaled["release_s"], 0.4)
        self.assertAlmostEqual(scaled["travel_s"], 0.2)
        self.assertEqual(spell_feel.scale_authored(authored, 1), authored)


class AuthoredTests(unittest.TestCase):
    def test_blackglass_authored_values_come_from_skills_json(self):
        authored = spell_feel.authored_values("blackglass_spear")
        self.assertEqual(authored["hitCueSeconds"], 0.7)
        self.assertEqual(authored["release_s"], 0.6)
        self.assertEqual(authored["travel_s"], 0.1)

    def test_unknown_spell_has_no_authored_values(self):
        self.assertIsNone(spell_feel.authored_values("no_such_spell"))

    def test_disagreement_is_a_note_beyond_two_frames(self):
        authored = {"release_s": 0.6, "travel_s": 0.1}
        measured = {"gather_s": 0.62, "travel_s": 0.2}
        notes = spell_feel.disagreements(measured, authored, FPS)
        self.assertEqual(len(notes), 1)
        self.assertIn("travel", notes[0])


if __name__ == "__main__":
    unittest.main()
