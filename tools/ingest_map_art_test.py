#!/usr/bin/env python3
"""Tests for tools/ingest_map_art.py. Synthetic images only.

    python tools/ingest_map_art_test.py
"""
import os
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ingest_map_art as ima  # noqa: E402

GREEN = (0, 255, 0)


def sheet(cols, rows, cell=60):
    """Green sheet; each cell holds a red block of a different width, off-centre."""
    arr = np.zeros((rows * cell, cols * cell, 3), np.uint8)
    arr[...] = GREEN
    for i in range(cols * rows):
        r, c = divmod(i, cols)
        w = 10 + i * 2
        x = c * cell + 5
        y = r * cell + 8
        arr[y:y + 20, x:x + w] = (200, 40, 40)
    return Image.fromarray(arr)


class IngestTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.src = os.path.join(self.tmp.name, "src")
        self.out = os.path.join(self.tmp.name, "out")
        os.makedirs(self.src)

    def tearDown(self):
        self.tmp.cleanup()

    def test_medallions_become_nine_keyed_trimmed_files(self):
        sheet(3, 3).save(os.path.join(self.src, "map_medallions.png"))
        ima.run(self.src, self.out, "medallions")
        for name in ima.MEDALLIONS:
            img = Image.open(os.path.join(self.out, f"map_node_{name}.png"))
            self.assertEqual(img.mode, "RGBA")
            a = np.asarray(img)[..., 3]
            self.assertEqual(a[0, 0], 0)
            ys, xs = np.nonzero(a)
            left, right = xs.min(), img.width - 1 - xs.max()
            top, bottom = ys.min(), img.height - 1 - ys.max()
            self.assertEqual(left, right)
            self.assertEqual(top, bottom)
            self.assertEqual(left, top)

    def test_chips_slice_five_by_one(self):
        sheet(5, 1).save(os.path.join(self.src, "map_chips.png"))
        wrote = ima.run(self.src, self.out, "chips")
        self.assertEqual(len(wrote), 5)
        self.assertTrue(os.path.exists(os.path.join(self.out, "map_chip_gold.png")))
        self.assertTrue(os.path.exists(os.path.join(self.out, "map_chip_book.png")))

    def test_missing_sources_are_skipped(self):
        self.assertEqual(ima.run(self.src, self.out), [])

    def test_strip_is_scaled_and_tiles_vertically(self):
        # Vertical ramp: top row dark, bottom row bright, so an uncrossfaded
        # wrap would jump by about 255 at the seam.
        ramp = np.linspace(0, 255, 100, dtype=np.float32)[:, None, None] * np.ones((1, 40, 3), np.float32)
        Image.fromarray(ramp.astype(np.uint8)).save(os.path.join(self.src, "realm_hush_found.png"))
        ima.run(self.src, self.out, "strip")
        out = np.asarray(Image.open(os.path.join(self.out, "realm_hush_found.png")), dtype=np.int32)
        self.assertEqual(out.shape[1], 1920)
        self.assertEqual(out.shape[0], ima.seam_height(1920 * 100 // 40))
        self.assertLess(abs(out[-1] - out[0]).mean(), 20)

    def test_heart_is_scaled_and_fades_in_from_the_top(self):
        Image.fromarray(np.full((1080, 1920, 3), 120, np.uint8)).save(os.path.join(self.src, "realm_hush_heart.png"))
        ima.run(self.src, self.out, "heart")
        img = Image.open(os.path.join(self.out, "realm_hush_heart.png"))
        self.assertEqual(img.size, (1920, 1080))
        a = np.asarray(img)[..., 3]
        self.assertLess(a[0, 0], 8)
        self.assertEqual(a[-1, 0], 255)

    def test_an_icon_spilling_past_its_grid_cell_stays_whole(self):
        arr = np.zeros((120, 60, 3), np.uint8)
        arr[...] = GREEN
        arr[10:80, 10:50] = (200, 40, 40)    # 70 tall in a 60-tall cell
        arr[100:120, 10:50] = (40, 40, 200)
        Image.fromarray(arr).save(os.path.join(self.src, "map_chips.png"))
        pieces = ima.split_pieces(ima.key_out_green(Image.open(os.path.join(self.src, "map_chips.png")).convert("RGB")), 2, 1)
        heights = [p.getchannel("A").getbbox()[3] - p.getchannel("A").getbbox()[1] for p in pieces]
        self.assertEqual(sorted(heights), [20, 70])

    def test_found_and_eased_share_dimensions(self):
        for name in ("realm_hush_found.png", "realm_hush_eased.png"):
            Image.fromarray(np.full((200, 80, 3), 90, np.uint8)).save(os.path.join(self.src, name))
        ima.run(self.src, self.out, "strip")
        a = Image.open(os.path.join(self.out, "realm_hush_found.png")).size
        b = Image.open(os.path.join(self.out, "realm_hush_eased.png")).size
        self.assertEqual(a, b)


if __name__ == "__main__":
    unittest.main()
