#!/usr/bin/env python3
"""Run: python tools/make_spell_fx_test.py"""
import hashlib
import os
import sys
import tempfile
import unittest

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


if __name__ == "__main__":
    unittest.main()
