#!/usr/bin/env python3
"""Run: python tools/make_spell_fx_test.py"""
import hashlib
import os
import sys
import tempfile
import unittest

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_spell_fx as fx  # noqa: E402


class PricePulse(unittest.TestCase):
    def test_deterministic_and_shaped(self):
        a, b = fx.court_price_pulse(), fx.court_price_pulse()
        self.assertEqual(hashlib.sha256(a.tobytes()).hexdigest(),
                         hashlib.sha256(b.tobytes()).hexdigest())
        self.assertEqual(a.shape, (256, 256, 4))

    def test_ring_not_a_disc(self):
        a = fx.court_price_pulse()
        centre, ring, corner = int(a[128, 128, 3]), int(a[128, 128 + 79, 3]), int(a[2, 2, 3])
        self.assertEqual(corner, 0)
        self.assertGreater(ring, 240)
        self.assertLess(centre, 110)
        self.assertGreater(centre, 60)

    def test_violet(self):
        a = fx.court_price_pulse()
        r, g, b = (int(v) for v in a[128, 128 + 40, :3])
        self.assertGreater(b, r)
        self.assertGreater(r, g)

    def test_writes_where_asked(self):
        with tempfile.TemporaryDirectory() as d:
            fx.write("court_price_pulse", d)
            self.assertTrue(os.path.isfile(os.path.join(d, "court_price_pulse", "f0.png")))


class CinchRing(unittest.TestCase):
    def test_deterministic_thin_gold_ring_with_crimson_halo(self):
        a, b = fx.shackle_cinch_ring(), fx.shackle_cinch_ring()
        self.assertEqual(hashlib.sha256(a.tobytes()).hexdigest(),
                         hashlib.sha256(b.tobytes()).hexdigest())
        self.assertEqual(a.shape, (256, 256, 4))
        self.assertEqual(int(a[2, 2, 3]), 0)
        self.assertEqual(int(a[128, 128, 3]), 0, "a ring, not a disc")
        peak = a[128, 128 + 100]
        self.assertGreater(int(peak[3]), 240)
        self.assertGreater(int(peak[1]), 200, "gold-white where it is densest")
        halo = a[128, 128 + 108]
        self.assertGreater(int(halo[0]), int(halo[1]) + 80, "crimson in the halo")


class AegisMote(unittest.TestCase):
    def test_deterministic_gold_orb_with_a_tail_behind_it(self):
        a, b = fx.aegis_mote(), fx.aegis_mote()
        self.assertEqual(hashlib.sha256(a.tobytes()).hexdigest(),
                         hashlib.sha256(b.tobytes()).hexdigest())
        self.assertEqual(a.shape, (256, 256, 4))
        self.assertEqual(int(a[2, 2, 3]), 0)
        self.assertGreater(int(a[128, 170, 3]), 250, "the orb")
        core = [int(v) for v in a[128, 170, :3]]
        self.assertGreater(min(core), 190, "white-gold at the core")
        tail = a[128, 100]
        self.assertGreater(int(tail[3]), 40, "a tail trails to the left")
        self.assertLess(int(a[128, 240, 3]), 8, "only the faint halo ahead of the orb")
        self.assertGreater(int(tail[0]), int(tail[1]), "gold, never blue or green")
        self.assertGreater(int(tail[1]), int(tail[2]))


class MudPieces(unittest.TestCase):
    def test_deterministic(self):
        for name in ("mud_glob", "mud_splat_drops", "mud_burst_splat"):
            a = [hashlib.sha256(f().tobytes()).hexdigest() for f in fx.PIECES[name]]
            b = [hashlib.sha256(f().tobytes()).hexdigest() for f in fx.PIECES[name]]
            self.assertEqual(a, b, name)

    def test_glob_is_matte_brown_with_drips_behind_it(self):
        a = fx.mud_glob()
        self.assertEqual(a.shape, (256, 256, 4))
        solid = a[..., 3] > 200
        rgb = a[solid][:, :3].astype(int)
        self.assertGreater(len(rgb), 5000)
        self.assertLess(int(rgb.max()), 120, "matte: nothing bright enough to bloom or read as a highlight")
        self.assertTrue(((rgb[:, 0] > rgb[:, 1]) & (rgb[:, 1] > rgb[:, 2])).all(), "brown, never grey or green")
        xs = np.nonzero(solid)[1]
        self.assertLess(int(xs.min()), 90, "drips trail to the left")
        self.assertGreater(int(xs.max()), 170)
        self.assertEqual(int(a[2, 2, 3]), 0)

    def test_drop_frames(self):
        frames = [f() for f in fx.PIECES["mud_splat_drops"]]
        self.assertEqual([f.shape for f in frames], [(128, 128, 4)] * 4)
        for f in frames:
            self.assertGreater(int((f[..., 3] > 200).sum()), 150)
            self.assertEqual(int(f[1, 1, 3]), 0)

    def test_splat_keeps_the_burst_and_drops_the_streak(self):
        frames = [f() for f in fx.PIECES["mud_burst_splat"]]
        self.assertEqual(len(frames), 14)
        first = frames[0]
        w = first.shape[1]
        self.assertEqual(int(first[:, : int(w * 0.38), 3].max()), 0, "the streak back to the caster is gone")
        self.assertGreater(int(first[:, int(w * 0.55):, 3].max()), 200, "the burst is whole")


if __name__ == "__main__":
    unittest.main()
