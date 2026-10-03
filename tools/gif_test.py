#!/usr/bin/env python3
"""Pure checks for tools/gif.py (no Unity): python3 tools/gif_test.py"""

import os
import sys
import tempfile
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
        "status": ["Poison"], "stance": ["Enemies/rat"], "sheet": ["bolt"], "stillClasses": ["MenuCaptureTests"],
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

    def test_phase_b_kinds_are_checked_against_what_exists(self):
        self.assertEqual(self.refused({"kind": "stance", "id": "Enemies/rat"}), [])
        self.assertEqual(self.refused({"kind": "sheet", "id": "bolt"}), [])
        self.assertEqual(self.refused({"kind": "still", "id": "MenuCaptureTests/menu.png"}), [])
        self.assertIn("has no stills", self.refused({"kind": "stance", "id": "Enemies/zzz"})[0])
        self.assertIn("no frame folder", self.refused({"kind": "sheet", "id": "zzz"})[0])
        self.assertIn("not a PlayMode capture class", self.refused({"kind": "still", "id": "Nope/menu.png"})[0])
        self.assertIn("<CaptureClass>/<file>.png", self.refused({"kind": "still", "id": "MenuCaptureTests/menu"})[0])
        self.assertIn("crop is always full", self.refused({"kind": "sheet", "id": "bolt", "crop": "subject"})[0])

    def test_statuses_come_from_the_enum_source(self):
        self.assertIn("Poison", gif.status_names())


class CaptureOptionTests(unittest.TestCase):
    IDS = RefusalTests.IDS

    def norm(self, **extra):
        return gif.normalize(dict({"kind": "spell", "id": "bolt"}, **extra), SKILLS)

    def test_aspect_sets_the_size_and_a_full_frame(self):
        s = self.norm(aspect="4:3")
        self.assertEqual((s["width"], s["height"], s["crop"]), (1440, 1080, "full"))
        self.assertEqual(self.norm()["width"], 0)

    def test_a_bad_aspect_is_an_error(self):
        with self.assertRaises(ValueError):
            self.norm(aspect="wide")

    def test_defaults_leave_the_key_of_an_older_scenario_alone(self):
        key = gif.scenario_key({"kind": "spell", "id": "bolt", "crop": "stage"})
        self.assertEqual(key, gif.scenario_key({"kind": "spell", "id": "bolt", "crop": "stage",
                                                "width": 0, "height": 0, "repeat": 1, "darkness": 0.0}))
        for change in ({"aspect": "4:3"}, {"repeat": 5}, {"darkness": 0.9}):
            self.assertNotEqual(self.norm()["key"], self.norm(**change)["key"], change)

    def test_repeat_and_darkness_reach_the_request(self):
        s = self.norm(repeat=5, darkness=0.9)
        self.assertEqual((s["repeat"], s["darkness"]), (5, 0.9))

    def test_refusals(self):
        self.assertEqual(gif.refusals(self.norm(aspect="4:3", repeat=3, darkness=0.5), self.IDS), [])
        self.assertIn("full", gif.refusals(self.norm(aspect="4:3", crop="stage"), self.IDS)[0])
        self.assertIn("at least 1", gif.refusals(self.norm(repeat=-2), self.IDS)[0])
        self.assertIn("opacity", gif.refusals(self.norm(darkness=1.5), self.IDS)[0])
        relic = gif.normalize({"kind": "relic", "id": "bell", "repeat": 2}, SKILLS)
        self.assertIn("nothing to repeat", gif.refusals(relic, self.IDS)[0])


class FormationAndPartyTests(unittest.TestCase):
    CHARS = {"sheep": "sheep", "shawn": "sheep", "owl": "owl", "odette": "owl", "bear": "bear", "bjorn": "bear"}
    IDS = dict(RefusalTests.IDS, characters=["sheep", "owl", "bear"], enemies=["rat", "treant", "beetle"], spell=["bolt", "aegis", "wave"], allSkills=["bolt", "aegis", "wave"],
               casters={"bolt": "sheep", "aegis": "sheep"},
               targeting={"bolt": "", "aegis": "SingleAlly", "wave": "AllEnemies"})

    def norm(self, **extra):
        return gif.normalize(dict({"kind": "spell", "id": "bolt"}, **extra), SKILLS, self.CHARS)

    def test_names_resolve_to_ids_and_a_list_is_a_formation(self):
        s = self.norm(party="Shawn, odette bjorn", target="Odette", vs="rat,treant beetle")
        self.assertEqual((s["party"], s["target"], s["vs"], s["formation"]),
                         ("sheep,owl,bear", "owl", "rat,treant,beetle", "full"))

    def test_defaults_leave_old_keys_alone_and_new_fields_change_them(self):
        base = self.norm()
        self.assertEqual((base["party"], base["target"], base["vs"]), ("", "", "rat"))
        for change in ({"party": "shawn,odette"}, {"target": "2"}, {"vs": "rat,treant"}):
            self.assertNotEqual(base["key"], self.norm(**change)["key"], change)

    def test_refusals(self):
        r = lambda **kw: gif.refusals(self.norm(**kw), self.IDS)
        self.assertEqual(r(vs="rat,treant,beetle", target="3"), [])
        self.assertIn("slot", r(vs="rat,treant", target="3")[0])
        self.assertIn("rack holds", r(vs="rat,rat,rat,rat")[0])
        self.assertIn("not a lone", r(vs="rat,treant", formation="lone")[0])
        self.assertIn("leads with its caster", r(party="odette,shawn")[0])
        self.assertIn("not a character", r(party="shawn,nobody")[0])
        self.assertIn("twice", r(party="shawn,shawn")[0])
        aegis = lambda **kw: gif.refusals(gif.normalize(dict({"kind": "spell", "id": "aegis"}, **kw), SKILLS, self.CHARS), self.IDS)
        self.assertEqual(aegis(party="shawn,odette,bear", target="odette"), [])
        self.assertIn("not in the party", aegis(party="shawn,odette", target="bjorn")[0])
        wave = gif.normalize({"kind": "spell", "id": "wave", "target": "1"}, SKILLS, self.CHARS)
        self.assertIn("no target to pick", gif.refusals(wave, self.IDS)[0])
        relic = gif.normalize({"kind": "relic", "id": "bell", "vs": "rat,treant"}, SKILLS, self.CHARS)
        self.assertIn("vs list", gif.refusals(relic, self.IDS)[0])


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


class NormalizeBKindsTests(unittest.TestCase):
    def test_fight_fields_are_pinned_so_the_key_depends_on_the_subject_only(self):
        a = gif.normalize({"kind": "sheet", "id": "bolt", "vs": "treant", "speed": 1.5, "formation": "full"}, SKILLS)
        b = gif.normalize({"kind": "sheet", "id": "bolt"}, SKILLS)
        self.assertEqual(a["key"], b["key"])
        self.assertEqual((b["vs"], b["formation"], b["speed"], b["crop"]), ("", "", 1.0, "full"))

    def test_default_crops(self):
        self.assertEqual(gif.normalize({"kind": "stance", "id": "Enemies/rat"}, SKILLS)["crop"], "subject")
        self.assertEqual(gif.normalize({"kind": "still", "id": "A/b.png"}, SKILLS)["crop"], "full")


def write_png(path, size, box=None, colour=(200, 40, 40, 255)):
    from PIL import Image
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    if box:
        image.paste(Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), colour), (box[0], box[1]))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)


def gif_durations(path):
    from PIL import Image, ImageSequence
    with Image.open(path) as image:
        return [frame.info.get("duration") for frame in ImageSequence.Iterator(image)]


class StanceTests(unittest.TestCase):
    def test_order_is_the_fight_order_then_the_rest_by_name(self):
        self.assertEqual(gif.stance_order(["victory", "zeta", "attack", "idle", "hurt", "alpha"]),
                         ["idle", "attack", "hurt", "victory", "alpha", "zeta"])

    def test_one_loop_400_ms_a_still_cropped_to_the_union_of_every_pose(self):
        with tempfile.TemporaryDirectory() as root:
            actor = os.path.join(root, "Enemies", "bug")
            write_png(os.path.join(actor, "idle.png"), (200, 100), (50, 40, 90, 90))
            write_png(os.path.join(actor, "attack.png"), (200, 100), (60, 30, 150, 90))
            write_png(os.path.join(actor, "defeated.png"), (200, 100), (50, 70, 100, 90))
            out = os.path.join(root, "out")
            result = gif.render_stance({"id": "Enemies/bug", "crop": "subject"}, out, resources=root)
            self.assertEqual(result["order"], ["idle", "attack", "defeated"])
            self.assertEqual(result["cropBox"], [26, 6, 174, 100])
            self.assertEqual(gif_durations(os.path.join(out, "anim.gif")), [400, 400, 400])
            self.assertEqual(sorted(os.listdir(os.path.join(out, "frames"))), ["f0000.png", "f0001.png", "f0002.png"])

    def test_full_crop_keeps_the_canvas(self):
        with tempfile.TemporaryDirectory() as root:
            actor = os.path.join(root, "Characters", "hero")
            write_png(os.path.join(actor, "idle.png"), (120, 80), (40, 20, 80, 70))
            result = gif.render_stance({"id": "Characters/hero", "crop": "full"}, os.path.join(root, "o"), resources=root)
            self.assertEqual(result["cropBox"], [0, 0, 120, 80])

    def test_stances_on_different_canvases_are_refused(self):
        with tempfile.TemporaryDirectory() as root:
            actor = os.path.join(root, "Enemies", "bug")
            write_png(os.path.join(actor, "idle.png"), (200, 100), (50, 40, 90, 90))
            write_png(os.path.join(actor, "attack.png"), (210, 100), (50, 40, 90, 90))
            with self.assertRaisesRegex(ValueError, "shared canvas"):
                gif.render_stance({"id": "Enemies/bug", "crop": "subject"}, os.path.join(root, "o"), resources=root)


class SheetTests(unittest.TestCase):
    SKILLS_JSON = [{"id": "bolt", "vfx": {"layers": [
        {"id": "a", "render": "still", "path": "Spells/x"},
        {"id": "b", "render": "sprite", "path": "Spells/x", "fps": 24},
        {"id": "c", "render": "sprite", "path": "Spells/y", "fps": 0, "seconds": 0.5}]}}]

    def test_fps_is_the_sprite_layers_fps_else_frames_over_seconds_else_a_warned_default(self):
        self.assertEqual(gif.sheet_fps("x", 8, self.SKILLS_JSON, [])[0], 24.0)
        self.assertEqual(gif.sheet_fps("y", 10, self.SKILLS_JSON, [])[0], 20.0)
        fps, source, warning = gif.sheet_fps("z", 10, self.SKILLS_JSON, [])
        self.assertEqual((fps, source), (12.0, "default"))
        self.assertIn("no sprite layer", warning)

    def test_frames_play_in_numeric_order_at_the_layer_rate(self):
        with tempfile.TemporaryDirectory() as root:
            folder = os.path.join(root, "Spells", "x")
            for n in (0, 1, 2, 10):
                write_png(os.path.join(folder, "f%d.png" % n), (64, 64), (8 + n, 8, 40, 40))
            out = os.path.join(root, "o")
            result = gif.render_sheet({"id": "x"}, out, resources=root, skills=self.SKILLS_JSON, relics=[])
            self.assertEqual(result["order"], ["f0.png", "f1.png", "f2.png", "f10.png"])
            self.assertEqual(result["frames"], 4)
            # 24 fps is 41.67 ms a frame; the GIF stores centiseconds, so the
            # running total is rounded rather than each delay.
            self.assertEqual(gif_durations(os.path.join(out, "anim.gif")), [40, 40, 40, 50])


class StillTests(unittest.TestCase):
    def test_the_png_is_found_by_name_anywhere_under_still_raw_and_becomes_frame_zero(self):
        with tempfile.TemporaryDirectory() as root:
            write_png(os.path.join(root, "still_raw", "main_menu", "Menu.png"), (32, 32), (4, 4, 20, 20))
            scenario = gif.normalize({"kind": "still", "id": "MenuCaptureTests/menu.png"}, SKILLS)
            sdir = os.path.join(root, scenario["key"])
            result = gif.collect_still(root, scenario, sdir)
            self.assertEqual(result["label"], "still/MenuCaptureTests/menu.png")
            self.assertTrue(os.path.isfile(os.path.join(sdir, "frames", "f0000.png")))
            self.assertTrue(os.path.isfile(os.path.join(sdir, "still.png")))

    def test_a_png_the_class_did_not_write_is_a_failed_row_not_a_crash(self):
        with tempfile.TemporaryDirectory() as root:
            scenario = gif.normalize({"kind": "still", "id": "MenuCaptureTests/gone.png"}, SKILLS)
            sdir = os.path.join(root, scenario["key"])
            result = gif.run_python_scenario(root, scenario, sdir)
            self.assertFalse(result["ok"])
            self.assertIn("wrote no gone.png", result["error"])


class ManifestTests(unittest.TestCase):
    IDS = {"playerSpell": ["bolt", "nova", "aegis"],
           "targeting": {"bolt": "SingleEnemy", "nova": "AllEnemies", "aegis": "SingleAlly"},
           "attack": ["rat"], "relic": ["bell"], "status": ["Poison"], "stance": ["Enemies/rat"], "sheet": ["bolt"]}

    def test_spell_matrix_skips_what_targeting_makes_meaningless(self):
        args = (("rat", "treant"), ("lone", "full"), (1.0, 1.5))
        kept, skipped = gif.spell_matrix("bolt", "SingleEnemy", *args)
        self.assertEqual((len(kept), len(skipped)), (8, 0))
        kept, skipped = gif.spell_matrix("nova", "AllEnemies", *args)
        self.assertEqual((len(kept), len(skipped)), (4, 4))
        self.assertTrue(all(c["formation"] == "full" for c in kept))
        self.assertEqual({c["reason"] for c in skipped}, {"an all-enemy spell needs a rack"})
        kept, skipped = gif.spell_matrix("aegis", "SingleAlly", *args)
        self.assertEqual([(c["vs"], c["formation"], c["speed"]) for c in kept], [("rat", "lone", 1.0), ("rat", "lone", 1.5)])
        self.assertEqual(len(skipped), 6)

    def test_expansion_counts_every_dimension_and_passes_the_gaps_through(self):
        spec = {"spells": {"ids": "all", "vs": ["rat", "treant"], "formations": ["lone", "full"], "speeds": [1, 1.5]},
                "attacks": "all", "relics": "all", "statuses": ["Poison"], "stances": "all", "sheets": "all",
                "stills": [{"class": "MenuCaptureTests", "files": ["a.png", "b.png"]}],
                "notCovered": [{"name": "Outcome form", "why": "not built"}]}
        skills = {"bolt": {"targeting": "SingleEnemy"}, "nova": {"targeting": "AllEnemies"}, "aegis": {"targeting": "SingleAlly"}}
        out = gif.expand_manifest(spec, self.IDS, skills)
        kinds = {}
        for s in out["scenarios"]:
            kinds[s["kind"]] = kinds.get(s["kind"], 0) + 1
        self.assertEqual(kinds, {"spell": 8 + 4 + 2, "attack": 1, "relic": 1, "status": 1, "stance": 1, "sheet": 1, "still": 2})
        self.assertEqual(len(out["skipped"]), 4 + 6)
        self.assertEqual(out["gaps"], [{"name": "Outcome form", "why": "not built"}])
        self.assertEqual(len({s["key"] for s in out["scenarios"]}), len(out["scenarios"]))

    def test_abilities_expand_to_one_attack_per_enemy_ability_with_vfx(self):
        ids = dict(self.IDS, abilityPairs=[["golem", "slam"], ["treant", "spore"]])
        out = gif.expand_manifest({"abilities": "all"}, ids, {})
        self.assertEqual([(s["kind"], s["id"], s["skill"]) for s in out["scenarios"]],
                         [("attack", "golem", "slam"), ("attack", "treant", "spore")])
        self.assertEqual(gif.expand_manifest({"attacks": ["rat"]}, ids, {})["scenarios"][0]["skill"], "")

    def test_real_content_gives_the_seven_enemy_abilities_and_the_baseline_records_them(self):
        ids = gif.known_ids()
        self.assertEqual(sorted(k for _e, k in ids["abilityPairs"]), ["bellwether_scratch", "bog_mud_burst", "boulder_slam",
                         "dark_chains", "death_knell", "spore_cloud", "trunk_slam"])
        base = gif.expand_manifest(gif.load_manifest("baseline"), ids)["scenarios"]
        self.assertEqual(sum(1 for s in base if s["kind"] == "attack" and s["skill"]), 7)

    def test_the_committed_manifests_expand_with_no_refusal_and_cover_every_kind(self):
        ids = gif.known_ids()
        for name in ("baseline", "smoke"):
            out = gif.expand_manifest(gif.load_manifest(name), ids)
            self.assertEqual([m for s in out["scenarios"] for m in gif.refusals(s, ids)], [], name)
            self.assertEqual({s["kind"] for s in out["scenarios"]}, set(gif.KINDS), name)
        baseline = gif.expand_manifest(gif.load_manifest("baseline"), ids)
        self.assertEqual([g["name"] for g in baseline["gaps"]], [])
        receipts = [s for s in baseline["scenarios"] if s["kind"] == "still" and "RelicReceiptCaptureTests" in str(s)]
        self.assertEqual(len(receipts), 7, "the P4 capture cases are stills in the baseline")


if __name__ == "__main__":
    unittest.main()
