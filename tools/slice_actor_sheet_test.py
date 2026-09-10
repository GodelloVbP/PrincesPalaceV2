#!/usr/bin/env python3
"""Tests for the multi-sheet extension to tools/slice_actor_sheet.py.

    python tools/slice_actor_sheet_test.py

SYNTHETIC DATA ONLY. Resources/Characters/bear is real, delivered art -- a
destructive tool's first run belongs on a fixture, never on the one copy of
something nobody can remake. Every sheet here is a tiny generated PNG with a
solid rectangle standing in for a figure, built fresh under a temp directory
per test.

WHY A SUBPROCESS, AND WHY A FAKE `Assets/` TREE UNDER THE TEMP DIR. `main()`
is what a real invocation runs, including the parts a function-level test
would skip over: argument parsing, the `--recipe` replay path, and the
`out_root == DEFAULT_OUT_ROOT` gate that decides whether a recipe.json and a
StanceManifest.json update are written at all. `DEFAULT_OUT_ROOT` and
`ART_ROOT` are relative paths ("Assets/_Project/Resources",
"Assets/_Project/Art"), so running the tool with its CWD set to a temp
directory that has its own `Assets/_Project/...` underneath exercises that
exact gate -- recipe writing, replay, and the StanceManifest.json no-op path
(there is deliberately no manifest file in the fixture tree, and the tool's
own code prints a NOTE and moves on) -- without the run ever resolving to a
path under the real project.

THE THREE CONTRACTS THIS FILE CHECKS, matching the job's own requirements:
  1. the shared canvas is sized over the UNION of every sheet's stances, and
     every stance still shares one ground line, even though they were cut
     from different sheets;
  2. a multi-sheet recipe.json replays byte-identical;
  3. a one-sheet recipe (the shape committed for bear/owl/treant/the ram
     already) keeps replaying through the exact same code, unchanged.
"""

import hashlib
import os
import subprocess
import sys
import tempfile
import unittest

from PIL import Image, ImageDraw

TOOL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "slice_actor_sheet.py")

# sheet_slicing.PADDING -- imported for the expected canvas math, not to
# duplicate its definition.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sheet_slicing import PADDING  # noqa: E402


def _blob_sheet(path, width, height, box):
    """An RGBA PNG, transparent except for one solid opaque rectangle."""
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    ImageDraw.Draw(img).rectangle(box, fill=(160, 90, 40, 255))
    img.save(path)


def _sha256(path):
    with open(path, "rb") as handle:
        return hashlib.sha256(handle.read()).hexdigest()


def _lowest_opaque_row(path):
    with Image.open(path) as img:
        alpha = img.convert("RGBA").getchannel("A")
        bbox = alpha.point(lambda v: 255 if v > 16 else 0).getbbox()
    return None if bbox is None else bbox[3] - 1  # bbox is (l, t, r, b) exclusive


class _FixtureTree(unittest.TestCase):
    """A throwaway Assets/_Project/{Art,Resources} tree under a temp dir, so
    the tool's own relative DEFAULT_OUT_ROOT/ART_ROOT resolve inside it."""

    def setUp(self):
        self.root = tempfile.mkdtemp()
        self.art_dir = os.path.join(self.root, "Assets", "_Project", "Art", "Characters", "testbear")
        os.makedirs(self.art_dir, exist_ok=True)

    def tearDown(self):
        import shutil
        shutil.rmtree(self.root, ignore_errors=True)

    def run_tool(self, argv, expect_ok=True):
        result = subprocess.run(
            [sys.executable, TOOL] + argv,
            cwd=self.root, capture_output=True, text=True)
        if expect_ok:
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        return result

    def out_png(self, name):
        return os.path.join(self.root, "Assets", "_Project", "Resources",
                             "Characters", "testbear", f"{name}.png")

    def recipe_path(self):
        return os.path.join(self.art_dir, "recipe.json")


class UnionCanvasAndOneGroundLineTests(_FixtureTree):
    def setUp(self):
        super().setUp()
        # Sheet A: a short blob, one cell, named "idle".
        self.sheet_a = os.path.join(self.art_dir, "sheet_a.png")
        _blob_sheet(self.sheet_a, 60, 60, (10, 30, 29, 49))  # 20x20

        # Sheet B: a TALL blob (stands in for the overhead-hammer pose that
        # forces the bear's real canvas to grow upward), one cell, named
        # "tall". Same width as A so only the height axis is under test.
        self.sheet_b = os.path.join(self.art_dir, "sheet_b.png")
        _blob_sheet(self.sheet_b, 60, 90, (10, 10, 29, 69))  # 20x60

    def test_union_canvas_height_covers_the_tallest_stance_from_either_sheet(self):
        self.run_tool([
            "--actor", "Characters/testbear",
            "--sheet", self.sheet_a, "--grid", "1x1", "--stances", "idle",
            "--sheet", self.sheet_b, "--grid", "1x1", "--stances", "tall",
        ])

        with Image.open(self.out_png("idle")) as idle, Image.open(self.out_png("tall")) as tall:
            idle_size, tall_size = idle.size, tall.size

        # Both stances share ONE canvas -- not each sized to its own content.
        self.assertEqual(idle_size, tall_size,
                          "idle and tall were composited onto different-sized canvases")

        # The canvas is tall enough for the TALLEST piece (60px blob) plus
        # padding on both sides -- the short blob from sheet A must not have
        # capped the union.
        self.assertEqual(tall_size[1], 60 + 2 * PADDING,
                          "canvas height does not cover sheet B's tall stance")

    def test_one_ground_line_across_stances_cut_from_different_sheets(self):
        self.run_tool([
            "--actor", "Characters/testbear",
            "--sheet", self.sheet_a, "--grid", "1x1", "--stances", "idle",
            "--sheet", self.sheet_b, "--grid", "1x1", "--stances", "tall",
        ])

        idle_foot = _lowest_opaque_row(self.out_png("idle"))
        tall_foot = _lowest_opaque_row(self.out_png("tall"))
        self.assertEqual(idle_foot, tall_foot,
                          f"idle's feet (row {idle_foot}) and tall's feet (row {tall_foot}) "
                          "do not sit on the same ground row, though both blobs touch their "
                          "own cell's bottom edge and should bottom-align identically")

    def test_a_dash_named_cell_is_skipped_the_same_as_a_one_sheet_run(self):
        # Sheet C has two cells; the second is unused, the way cell 2 of
        # Bjorn's real slam sheet is disregarded entirely.
        sheet_c = os.path.join(self.art_dir, "sheet_c.png")
        img = Image.new("RGBA", (120, 60), (0, 0, 0, 0))
        ImageDraw.Draw(img).rectangle((10, 30, 29, 49), fill=(160, 90, 40, 255))
        ImageDraw.Draw(img).rectangle((70, 30, 89, 49), fill=(90, 160, 40, 255))
        img.save(sheet_c)

        self.run_tool([
            "--actor", "Characters/testbear",
            "--sheet", self.sheet_a, "--grid", "1x1", "--stances", "idle",
            "--sheet", sheet_c, "--grid", "2x1", "--stances", "used,-",
        ])

        self.assertTrue(os.path.isfile(self.out_png("used")))
        self.assertFalse(os.path.isfile(self.out_png("-")),
                          "the dash-named cell should never be written as its own stance")


class MultiSheetRecipeReplayTests(_FixtureTree):
    def setUp(self):
        super().setUp()
        self.sheet_a = os.path.join(self.art_dir, "sheet_a.png")
        _blob_sheet(self.sheet_a, 60, 60, (10, 30, 29, 49))
        self.sheet_b = os.path.join(self.art_dir, "sheet_b.png")
        _blob_sheet(self.sheet_b, 60, 90, (10, 10, 29, 69))

        self.argv = [
            "--actor", "Characters/testbear",
            "--sheet", self.sheet_a, "--grid", "1x1", "--stances", "idle",
            "--sheet", self.sheet_b, "--grid", "1x1", "--stances", "tall",
        ]

    def test_recipe_gains_a_sheets_list_for_two_sources(self):
        self.run_tool(self.argv)
        import json
        with open(self.recipe_path(), encoding="utf-8") as handle:
            recipe = json.load(handle)

        self.assertIn("sheets", recipe, "a two-source recipe should record a 'sheets' list")
        self.assertEqual(2, len(recipe["sheets"]))
        self.assertNotIn("sheet", recipe,
                          "a multi-sheet recipe should not also carry the one-sheet 'sheet' key")
        self.assertIn("argv", recipe, "replay reads argv regardless of shape")

    def test_replay_reproduces_every_still_byte_identical(self):
        self.run_tool(self.argv)
        before = {name: _sha256(self.out_png(name)) for name in ("idle", "tall")}
        recipe_before = _sha256(self.recipe_path())

        for name in before:
            os.remove(self.out_png(name))

        self.run_tool(["--recipe", self.recipe_path()])

        after = {name: _sha256(self.out_png(name)) for name in ("idle", "tall")}
        self.assertEqual(before, after, "replay did not reproduce the committed stills byte for byte")
        self.assertEqual(recipe_before, _sha256(self.recipe_path()),
                          "a replay must never rewrite the recipe it just read")

    def test_a_stance_name_repeated_across_sheets_is_refused(self):
        result = self.run_tool([
            "--actor", "Characters/testbear",
            "--sheet", self.sheet_a, "--grid", "1x1", "--stances", "idle",
            "--sheet", self.sheet_b, "--grid", "1x1", "--stances", "idle",
        ], expect_ok=False)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("more than one sheet", result.stdout + result.stderr)


class OneSheetRecipeShapeStillReplaysTests(_FixtureTree):
    """The single-sheet actors committed today (bear, owl, treant, the ram)
    must keep replaying unchanged. This proves the n=1 case of the new code
    is the same code as before, not a parallel path that happens to agree."""

    def setUp(self):
        super().setUp()
        self.sheet = os.path.join(self.art_dir, "sheet_a.png")
        _blob_sheet(self.sheet, 60, 60, (10, 30, 29, 49))
        self.argv = [
            "--actor", "Characters/testbear",
            "--sheet", self.sheet, "--grid", "1x1", "--stances", "idle",
        ]

    def test_a_freshly_written_one_sheet_recipe_keeps_the_old_top_level_shape(self):
        self.run_tool(self.argv)
        import json
        with open(self.recipe_path(), encoding="utf-8") as handle:
            recipe = json.load(handle)

        self.assertIn("sheet", recipe, "a one-sheet recipe should still carry the old 'sheet' key")
        self.assertEqual(self.sheet.replace("\\", "/"), recipe["sheet"])
        self.assertNotIn("sheets", recipe,
                          "a one-sheet recipe should not gain the multi-sheet 'sheets' list")

    def test_replay_of_a_one_sheet_recipe_matches_the_original_run_byte_identical(self):
        self.run_tool(self.argv)
        original = _sha256(self.out_png("idle"))
        os.remove(self.out_png("idle"))

        self.run_tool(["--recipe", self.recipe_path()])
        self.assertEqual(original, _sha256(self.out_png("idle")))

    def test_a_hand_authored_pre_existing_old_shape_recipe_still_replays(self):
        # Simulates a recipe committed BEFORE this file supported more than
        # one sheet: single "sheet" string, argv with exactly one occurrence
        # of each per-sheet flag -- the exact shape recorded in
        # Assets/_Project/Art/Characters/{bear,owl,sheep_black_ram}/recipe.json
        # and Assets/_Project/Art/Enemies/treant/recipe.json today.
        import json
        old_style = {
            "tool": "tools/slice_actor_sheet.py",
            "sheet": self.sheet.replace("\\", "/"),
            "argv": [
                "--sheet", self.sheet.replace("\\", "/"),
                "--actor", "Characters/testbear",
                "--stances", "idle",
                "--grid", "1x1",
                "--key", "alpha",
                "--anchor", "ground_band",
                "--delivery-scale", "1.0",
                "--pocket-max-area", "200",
                "--max-ground-spread", "6",
            ],
        }
        with open(self.recipe_path(), "w", encoding="utf-8") as handle:
            json.dump(old_style, handle)

        self.run_tool(["--recipe", self.recipe_path()])
        self.assertTrue(os.path.isfile(self.out_png("idle")))


class ReplayOutRootOverrideTests(_FixtureTree):
    """Finding: `--recipe <path> --out-root <scratch>` used to silently drop
    the `--out-root` and write the real Resources tree instead. A replay's
    `--out-root` (and `--quiet`) must be honoured; every other flag given
    alongside `--recipe` must be refused rather than silently discarded."""

    def setUp(self):
        super().setUp()
        self.sheet = os.path.join(self.art_dir, "sheet_a.png")
        _blob_sheet(self.sheet, 60, 60, (10, 30, 29, 49))
        self.argv = [
            "--actor", "Characters/testbear",
            "--sheet", self.sheet, "--grid", "1x1", "--stances", "idle",
        ]

    def test_replay_with_an_explicit_out_root_writes_scratch_not_the_real_tree(self):
        # Initial run at the DEFAULT out-root -- this is the only shape that
        # writes a recipe (see the module's out_root == DEFAULT_OUT_ROOT gate).
        self.run_tool(self.argv)
        self.assertTrue(os.path.isfile(self.out_png("idle")))
        os.remove(self.out_png("idle"))

        scratch = os.path.join(self.root, "scratch2")
        self.run_tool(["--recipe", self.recipe_path(), "--out-root", scratch])

        self.assertFalse(
            os.path.isfile(self.out_png("idle")),
            "a replay with an explicit --out-root must not touch the real Resources tree")
        scratch_png = os.path.join(scratch, "Characters", "testbear", "idle.png")
        self.assertTrue(
            os.path.isfile(scratch_png),
            "a replay's explicit --out-root should have been honoured, not discarded")

    def test_replay_never_dirties_the_real_tree_git_status(self):
        # git-status guard named in the brief: run the whole sequence inside
        # a real git repo rooted at the fixture, and confirm the scratch
        # replay leaves Assets/_Project/Resources untouched.
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        self.run_tool(self.argv)
        subprocess.run(["git", "add", "-A"], cwd=self.root, check=True)
        subprocess.run(["git", "commit", "-q", "-m", "initial"], cwd=self.root, check=True)

        scratch = os.path.join(self.root, "scratch2")
        self.run_tool(["--recipe", self.recipe_path(), "--out-root", scratch])

        status = subprocess.run(
            ["git", "status", "--short", "Assets/_Project/Resources"],
            cwd=self.root, capture_output=True, text=True, check=True)
        self.assertEqual("", status.stdout.strip(),
                          "replay with --out-root scratch dirtied the real Resources tree: "
                          + status.stdout)

    def test_replay_refuses_a_flag_that_would_change_the_cut(self):
        self.run_tool(self.argv)
        result = self.run_tool(
            ["--recipe", self.recipe_path(), "--anchor", "centroid"], expect_ok=False)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("--anchor", result.stdout + result.stderr)
        self.assertIn("cannot be combined with --recipe", result.stdout + result.stderr)

    def test_replay_still_honours_quiet(self):
        self.run_tool(self.argv)
        result = self.run_tool(["--recipe", self.recipe_path(), "--quiet"])
        self.assertNotIn("sqrt(LCC-mass)", result.stdout,
                          "--quiet given alongside --recipe should still suppress the verbose "
                          "per-stance line")


if __name__ == "__main__":
    unittest.main(verbosity=2)
