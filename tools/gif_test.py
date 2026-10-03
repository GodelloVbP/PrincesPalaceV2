#!/usr/bin/env python3
"""Pure checks for tools/gif.py (no Unity): python3 tools/gif_test.py"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gif  # noqa: E402

SKILLS = {"nova": {"targeting": "AllEnemies"}, "bolt": {"targeting": "SingleEnemy"}}


class NormalizeTests(unittest.TestCase):
    def test_defaults_are_filled_and_pinned(self):
        s = gif.normalize({"kind": "spell", "id": "bolt"}, SKILLS)
        self.assertEqual(
            (s["vs"], s["formation"], s["speed"], s["crop"], s["seconds"], s["skill"], s["preStatus"]),
            ("rat", "lone", 1.0, "stage", 0.0, "", ""))

    def test_an_all_target_spell_defaults_to_a_rack(self):
        self.assertEqual(gif.normalize({"kind": "spell", "id": "nova"}, SKILLS)["formation"], "full")

    def test_status_defaults_to_two_seconds_and_a_subject_crop(self):
        s = gif.normalize({"kind": "status", "id": "Poison"}, SKILLS)
        self.assertEqual((s["seconds"], s["crop"]), (2.0, "subject"))

    def test_key_is_stable_and_differs_on_any_field(self):
        base = gif.normalize({"kind": "spell", "id": "bolt"}, SKILLS)
        self.assertEqual(base["key"], gif.normalize({"kind": "spell", "id": "bolt"}, SKILLS)["key"])
        self.assertEqual(len(base["key"]), 10)
        for change in ({"vs": "treant"}, {"speed": 1.5}, {"formation": "full"}, {"preStatus": "Poison"}, {"crop": "full"}):
            other = gif.normalize(dict({"kind": "spell", "id": "bolt"}, **change), SKILLS)
            self.assertNotEqual(base["key"], other["key"], change)


class RefusalTests(unittest.TestCase):
    IDS = {
        "spell": ["bolt"], "allSkills": ["bolt", "plain"], "attack": ["rat"], "enemies": ["rat", "wolf"],
        "enemyAbilities": {"rat": [], "wolf": []}, "relic": ["bell"], "allRelics": ["bell", "ring"],
        "status": ["Poison"],
    }

    def refused(self, raw):
        return gif.refusals(gif.normalize(raw, SKILLS), self.IDS)

    def test_known_ids_pass(self):
        for raw in ({"kind": "spell", "id": "bolt"}, {"kind": "attack", "id": "rat"},
                    {"kind": "relic", "id": "bell"}, {"kind": "status", "id": "Poison"}):
            self.assertEqual(self.refused(raw), [], raw)

    def test_unknown_and_artless_ids_are_refused_by_name(self):
        self.assertIn("not in skills.json", self.refused({"kind": "spell", "id": "zzz"})[0])
        self.assertIn("no vfx", self.refused({"kind": "spell", "id": "plain"})[0])
        self.assertIn("no art", self.refused({"kind": "attack", "id": "wolf"})[0])
        self.assertIn("relics with vfx: bell", self.refused({"kind": "relic", "id": "ring"})[0])
        self.assertIn("not a StatusEffectType", self.refused({"kind": "status", "id": "Nope"})[0])

    def test_phase_b_kinds_are_not_launchable(self):
        self.assertIn("phase B", self.refused({"kind": "stance", "id": "rat"})[0])

    def test_statuses_come_from_the_enum_source(self):
        self.assertIn("Poison", gif.status_names())


class IdentityTests(unittest.TestCase):
    def test_manifest_diff_honours_globs_and_one_sided_paths(self):
        a = {"x/a.txt": "1", "gen/b.asset": "2", "only_a": "3"}
        b = {"x/a.txt": "1", "gen/b.asset": "9"}
        self.assertEqual(gif.manifest_diff(a, b), ["gen/b.asset", "only_a"])
        self.assertEqual(gif.manifest_diff(a, b, ["gen/**"]), ["only_a"])

    def test_fingerprint_depends_on_every_hash(self):
        self.assertNotEqual(gif.fingerprint({"a": "1"}), gif.fingerprint({"a": "2"}))
        self.assertEqual(gif.fingerprint({"a": "1", "b": "2"}), gif.fingerprint({"b": "2", "a": "1"}))


class CropTests(unittest.TestCase):
    def test_stage_is_the_centred_1700_by_600_box(self):
        self.assertEqual(gif.crop_box("stage", (1920, 1080)), (110, 240, 1810, 840))

    def test_full_keeps_the_frame_and_subject_pads_the_motion_inside_the_stage(self):
        self.assertEqual(gif.crop_box("full", (1920, 1080)), (0, 0, 1920, 1080))
        self.assertEqual(gif.crop_box("subject", (1920, 1080), (500, 400, 700, 600)), (440, 340, 760, 660))
        self.assertEqual(gif.crop_box("subject", (1920, 1080), (120, 250, 200, 300)), (110, 240, 260, 360))


if __name__ == "__main__":
    unittest.main()
