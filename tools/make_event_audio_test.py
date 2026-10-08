#!/usr/bin/env python3
"""Pure checks for tools/make_court_of_whispers_impact.py and tools/make_flock_charge.py
(no Unity): python tools/make_event_audio_test.py"""

import os
import sys
import tempfile
import unittest

import numpy as np
from scipy.signal import butter, sosfilt

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_court_of_whispers_impact as court  # noqa: E402
import make_flock_charge as flock  # noqa: E402

RATE = 44100


def db(x):
    return 20.0 * np.log10(np.sqrt(np.mean(np.square(x))) + 1e-12)


class CourtOfWhispersTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.clip = court.build()
        cls.dry = court.read_wav(os.path.join(court.SOURCE_DIR, "dry_impact.wav"))

    def test_length_is_between_two_and_two_and_a_half_seconds(self):
        self.assertGreaterEqual(len(self.clip) / RATE, 2.0)
        self.assertLessEqual(len(self.clip) / RATE, 2.5)

    def test_the_first_40ms_is_the_dry_source_untouched(self):
        n = int(court.HEAD_EXACT * RATE)
        np.testing.assert_allclose(self.clip[:n], self.dry[:n], atol=1e-9)

    def test_the_head_follows_the_dry_source_until_the_whispers_enter(self):
        n = int(court.BED_ENTRY * RATE)
        r = np.corrcoef(self.clip[:n], self.dry[:n])[0, 1]
        self.assertGreater(r, 0.88)

    def test_the_tail_decays_below_minus_60_db_by_the_end(self):
        peak_db = 20.0 * np.log10(np.max(np.abs(self.clip)))
        self.assertLess(db(self.clip[-int(0.05 * RATE):]), peak_db - 60.0)

    def test_there_is_a_tail_past_the_dry_source(self):
        after = self.clip[int(1.3 * RATE):int(1.5 * RATE)]
        self.assertGreater(db(after), -40.0)

    def test_the_whisper_bed_has_no_voicing_left(self):
        # Voiced speech is strongly periodic at 80-300 Hz; a whisper's autocorrelation is flat.
        line = court.read_wav(os.path.join(court.SOURCE_DIR, "lines", "miscellaneous_9_alex.wav"))
        w = court.whisperize(line, 1)
        seg = w[int(0.5 * RATE):int(1.5 * RATE)]
        ref = line[int(0.5 * RATE):int(1.5 * RATE)]

        def periodicity(x):
            x = x - x.mean()
            ac = np.correlate(x, x, "full")[len(x) - 1:]
            lo, hi = int(RATE / 300), int(RATE / 80)
            return ac[lo:hi].max() / ac[0]

        self.assertLess(periodicity(seg), periodicity(ref))

    def test_rerun_is_byte_identical(self):
        with tempfile.TemporaryDirectory() as d:
            a, b = os.path.join(d, "a.wav"), os.path.join(d, "b.wav")
            court.write_wav(a, court.build())
            court.write_wav(b, court.build())
            with open(a, "rb") as fa, open(b, "rb") as fb:
                self.assertEqual(fa.read(), fb.read())


class FlockChargeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.clip = flock.master(flock.flock_charge(), flock.PEAK)

    def test_length_is_5_5_seconds(self):
        self.assertEqual(len(self.clip), int(RATE * 5.5))

    def test_the_strike_lands_at_the_hit_cue(self):
        low = sosfilt(butter(4, 300 / (RATE / 2), "low", output="sos"), self.clip)[: int(1.5 * RATE)]
        k = int(0.01 * RATE)
        env = np.convolve(np.abs(low), np.ones(k) / k, "same")
        onset = np.argmax(env > 0.1 * env.max()) / RATE
        # relics.json toll_of_the_flock hitCueSeconds is 0.55
        self.assertGreaterEqual(onset, 0.55)
        self.assertLessEqual(onset, 0.60)

    def test_nothing_audible_before_the_rush_begins(self):
        self.assertLess(db(self.clip[: int(0.15 * RATE)]), -60.0)

    def test_no_hoof_beats_between_strike_and_end(self):
        # The rush is smooth noise: its 50 ms levels fall steadily after the bell has decayed.
        k = int(0.05 * RATE)
        levels = [db(self.clip[i:i + k]) for i in range(int(2.0 * RATE), int(5.0 * RATE), k)]
        rises = [b - a for a, b in zip(levels, levels[1:])]
        self.assertLess(max(rises), 6.0)

    def test_rerun_is_byte_identical(self):
        with tempfile.TemporaryDirectory() as d:
            a, b = os.path.join(d, "a.wav"), os.path.join(d, "b.wav")
            flock.write_wav(a, flock.master(flock.flock_charge(), flock.PEAK))
            flock.write_wav(b, flock.master(flock.flock_charge(), flock.PEAK))
            with open(a, "rb") as fa, open(b, "rb") as fb:
                self.assertEqual(fa.read(), fb.read())

    def test_shipped_wav_matches_the_generator(self):
        with tempfile.TemporaryDirectory() as d:
            p = os.path.join(d, "f.wav")
            flock.write_wav(p, flock.master(flock.flock_charge(), flock.PEAK))
            with open(p, "rb") as f, open(flock.OUT_DEFAULT, "rb") as g:
                self.assertEqual(f.read(), g.read())


class CourtShippedTests(unittest.TestCase):
    def test_shipped_wav_matches_the_generator(self):
        with tempfile.TemporaryDirectory() as d:
            p = os.path.join(d, "c.wav")
            court.write_wav(p, court.build())
            with open(p, "rb") as f, open(court.OUT_DEFAULT, "rb") as g:
                self.assertEqual(f.read(), g.read())


if __name__ == "__main__":
    unittest.main()
