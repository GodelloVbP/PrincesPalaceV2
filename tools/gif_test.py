#!/usr/bin/env python3
"""Pure checks for tools/gif.py (no Unity): python3 tools/gif_test.py"""

import contextlib
import io
import os
import sys
import tempfile
import threading
import types
import unittest
import unittest.mock

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

    def test_fightend_is_a_full_frame_film_of_one_fight_class(self):
        s = gif.normalize({"kind": "fightend", "id": "boss"}, SKILLS)
        self.assertEqual((s["crop"], s["seconds"], s["formation"]), ("full", 0.0, "lone"))
        self.assertIn("fightend", gif.UNITY_KINDS)
        self.assertEqual(gif.refusals(s, {"enemies": ["rat"], "status": []}), [])
        bad = gif.normalize({"kind": "fightend", "id": "huge"}, SKILLS)
        self.assertIn("not one of normal, elite, boss", gif.refusals(bad, {"enemies": ["rat"], "status": []})[0])

    def test_reveal_is_a_full_frame_film_of_one_r0_page(self):
        s = gif.normalize({"kind": "reveal", "id": "cachetop"}, SKILLS)
        self.assertEqual((s["crop"], s["seconds"]), ("full", 0.0))
        self.assertIn("reveal", gif.UNITY_KINDS)
        self.assertEqual(gif.refusals(s, {"enemies": ["rat"], "status": []}), [])
        bad = gif.normalize({"kind": "reveal", "id": "huge"}, SKILLS)
        self.assertIn("not one of nocache, cachetop, ghosts", gif.refusals(bad, {"enemies": ["rat"], "status": []})[0])

    def test_key_is_stable_and_differs_on_any_field(self):
        base = gif.normalize({"kind": "spell", "id": "bolt"}, SKILLS)
        self.assertEqual(base["key"], gif.normalize({"kind": "spell", "id": "bolt"}, SKILLS)["key"])
        self.assertEqual(len(base["key"]), 10)
        for change in ({"vs": "treant"}, {"speed": 1.5}, {"formation": "full"}, {"preStatus": "Poison"}, {"crop": "full"}):
            other = gif.normalize(dict({"kind": "spell", "id": "bolt"}, **change), SKILLS)
            self.assertNotEqual(base["key"], other["key"], change)


class SpellSecondsTests(unittest.TestCase):
    def test_a_spell_keeps_its_seconds_and_a_cast_without_them_stays_zero(self):
        held = gif.normalize({"kind": "spell", "id": "bolt", "seconds": 25}, SKILLS)
        plain = gif.normalize({"kind": "spell", "id": "bolt"}, SKILLS)
        self.assertEqual((held["seconds"], plain["seconds"]), (25.0, 0.0))
        self.assertNotEqual(held["key"], plain["key"])

    def test_an_attack_still_drops_seconds(self):
        self.assertEqual(gif.normalize({"kind": "attack", "id": "rat", "seconds": 9}, SKILLS)["seconds"], 0.0)


class TrimFramesTests(unittest.TestCase):
    NOW = 2_000_000_000.0
    OLD = NOW - 3600

    def make_run(self, root, name, scenario="abc", frames=3, finished=True, at=None):
        """A run directory; `finished` writes the scenario.json finish leaves
        behind, `at` is the mtime every file of it carries."""
        at = self.OLD if at is None else at
        run = os.path.join(root, name)
        sdir = os.path.join(run, scenario)
        os.makedirs(os.path.join(sdir, "frames"))
        for i in range(frames):
            with open(os.path.join(sdir, "frames", "f%04d.png" % i), "wb") as f:
                f.write(b"x" * 10)
        keeps = ["anim.gif", "strip.png"] + (["scenario.json"] if finished else [])
        for keep in keeps:
            with open(os.path.join(sdir, keep), "wb") as f:
                f.write(b"k")
        with open(os.path.join(run, "request.json"), "w") as f:
            f.write('{"scenarios": [{"key": "%s"}]}' % scenario)
        for path in (os.path.join(run, "request.json"), os.path.join(sdir, "scenario.json")):
            if os.path.exists(path):
                os.utime(path, (at, at))

    def has_frames(self, root, name):
        return os.path.isdir(os.path.join(root, name, "abc", "frames"))

    def test_only_the_newest_runs_keep_frames_and_everything_else_stays(self):
        with tempfile.TemporaryDirectory() as root:
            for name in ("20261001T000000Z_a", "20261002T000000Z_b", "20261003T000000Z_c"):
                self.make_run(root, name)
            trimmed, freed = gif.trim_frames(root, keep=2, now=self.NOW)
            self.assertEqual((trimmed, freed), (1, 30))
            self.assertFalse(self.has_frames(root, "20261001T000000Z_a"))
            self.assertTrue(self.has_frames(root, "20261002T000000Z_b"))
            self.assertTrue(self.has_frames(root, "20261003T000000Z_c"))
            self.assertTrue(os.path.isfile(os.path.join(root, "20261001T000000Z_a", "abc", "anim.gif")))

    def test_a_second_trim_and_a_missing_root_are_no_ops(self):
        with tempfile.TemporaryDirectory() as root:
            self.make_run(root, "20261001T000000Z_a")
            self.make_run(root, "20261002T000000Z_b")
            self.assertEqual(gif.trim_frames(root, keep=1, now=self.NOW)[0], 1)
            self.assertEqual(gif.trim_frames(root, keep=1, now=self.NOW), (0, 0))
            self.assertEqual(gif.trim_frames(os.path.join(root, "nope")), (0, 0))

    def test_a_run_still_recording_is_never_trimmed(self):
        with tempfile.TemporaryDirectory() as root:
            self.make_run(root, "20261001T000000Z_a", finished=False)
            self.make_run(root, "20261002T000000Z_b")
            self.make_run(root, "20261003T000000Z_c")
            self.assertEqual(gif.trim_frames(root, keep=1, now=self.NOW)[0], 1)
            self.assertTrue(self.has_frames(root, "20261001T000000Z_a"))
            self.assertFalse(self.has_frames(root, "20261002T000000Z_b"))

    def test_an_unfinished_run_is_trimmed_once_it_is_a_day_stale(self):
        with tempfile.TemporaryDirectory() as root:
            self.make_run(root, "20261001T000000Z_a", finished=False, at=self.NOW - 2 * 86400)
            self.make_run(root, "20261002T000000Z_b")
            self.assertEqual(gif.trim_frames(root, keep=1, now=self.NOW)[0], 1)
            self.assertFalse(self.has_frames(root, "20261001T000000Z_a"))

    def test_a_run_younger_than_the_grace_period_keeps_its_frames(self):
        with tempfile.TemporaryDirectory() as root:
            self.make_run(root, "20261001T000000Z_a", at=self.NOW - 60)
            self.make_run(root, "20261002T000000Z_b")
            self.assertEqual(gif.trim_frames(root, keep=1, now=self.NOW), (0, 0))
            self.assertTrue(self.has_frames(root, "20261001T000000Z_a"))

    def test_runs_without_frames_do_not_use_up_the_kept_slots(self):
        with tempfile.TemporaryDirectory() as root:
            self.make_run(root, "20261001T000000Z_a")
            self.make_run(root, "20261002T000000Z_b")
            os.makedirs(os.path.join(root, "20261003T000000Z_c", "abc"))
            os.makedirs(os.path.join(root, "20261004T000000Z_d", "abc"))
            self.assertEqual(gif.trim_frames(root, keep=2, now=self.NOW), (0, 0))
            self.assertTrue(self.has_frames(root, "20261001T000000Z_a"))

    def test_a_run_marked_keep_frames_is_left_for_a_later_compare(self):
        with tempfile.TemporaryDirectory() as root:
            self.make_run(root, "20261001T000000Z_a")
            open(os.path.join(root, "20261001T000000Z_a", gif.KEEP_FRAMES_MARKER), "w").close()
            self.make_run(root, "20261002T000000Z_b")
            self.assertEqual(gif.trim_frames(root, keep=1, now=self.NOW), (0, 0))
            self.assertTrue(self.has_frames(root, "20261001T000000Z_a"))

    def test_a_folder_that_cannot_be_removed_is_reported_and_skipped(self):
        import unittest.mock as mock
        with tempfile.TemporaryDirectory() as root:
            self.make_run(root, "20261001T000000Z_a")
            self.make_run(root, "20261002T000000Z_b")
            with mock.patch.object(gif.shutil, "rmtree", side_effect=PermissionError("locked")):
                self.assertEqual(gif.trim_frames(root, keep=1, now=self.NOW), (0, 0))
            self.assertTrue(self.has_frames(root, "20261001T000000Z_a"))


class RunsCapTests(unittest.TestCase):
    NOW = 2_000_000_000.0

    def make_run(self, root, name, size=100, finished=True, at=None, marker=False):
        at = self.NOW - 3600 if at is None else at
        run = os.path.join(root, name)
        os.makedirs(os.path.join(run, "abc"))
        with open(os.path.join(run, "abc", "anim.gif"), "wb") as f:
            f.write(b"x" * size)
        with open(os.path.join(run, "request.json"), "w") as f:
            f.write('{"scenarios": [{"key": "abc"}]}')
        os.utime(os.path.join(run, "request.json"), (at, at))
        if finished:
            record = os.path.join(run, "abc", "scenario.json")
            open(record, "w").close()
            os.utime(record, (at, at))
        if marker:
            open(os.path.join(run, gif.KEEP_FRAMES_MARKER), "w").close()

    def names(self, root):
        return sorted(os.listdir(root))

    def test_oldest_runs_go_first_until_under_the_cap(self):
        with tempfile.TemporaryDirectory() as root:
            for n in ("20261001_a", "20261002_b", "20261003_c", "20261004_d"):
                self.make_run(root, n)
            removed, freed = gif.enforce_runs_cap(root, cap_bytes=300, now=self.NOW)
            self.assertEqual(removed, ["20261001_a", "20261002_b"])
            self.assertGreater(freed, 250)
            self.assertEqual(self.names(root), ["20261003_c", "20261004_d"])

    def test_nothing_is_removed_under_the_cap_or_without_a_folder(self):
        with tempfile.TemporaryDirectory() as root:
            self.make_run(root, "20261001_a")
            self.assertEqual(gif.enforce_runs_cap(root, cap_bytes=10**9, now=self.NOW), ([], 0))
            self.assertEqual(gif.enforce_runs_cap(os.path.join(root, "nope"), cap_bytes=1), ([], 0))

    def test_protected_unfinished_young_and_kept_runs_stay(self):
        with tempfile.TemporaryDirectory() as root:
            self.make_run(root, "20261001_just", size=1000)
            self.make_run(root, "20261002_open", size=1000, finished=False)
            self.make_run(root, "20261003_young", size=1000, at=self.NOW - 60)
            self.make_run(root, "20261004_kept", size=1000, marker=True)
            self.make_run(root, "20261005_old", size=1000)
            removed, _ = gif.enforce_runs_cap(root, cap_bytes=1, protect=("20261001_just",), now=self.NOW)
            self.assertEqual(removed, ["20261005_old"])
            self.assertEqual(len(self.names(root)), 4)

    def test_cap_comes_from_the_flag_then_the_environment_then_five_gb(self):
        with unittest.mock.patch.dict(os.environ, {}, clear=False):
            os.environ.pop(gif.RUNS_CAP_ENV, None)
            self.assertEqual(gif.runs_cap_bytes(), 5_000_000_000)
            os.environ[gif.RUNS_CAP_ENV] = "2"
            self.assertEqual(gif.runs_cap_bytes(), 2_000_000_000)
            self.assertEqual(gif.runs_cap_bytes(1.5), 1_500_000_000)
            os.environ[gif.RUNS_CAP_ENV] = "junk"
            self.assertEqual(gif.runs_cap_bytes(), 5_000_000_000)


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

    def test_aspect_leaves_a_stance_and_a_sheet_alone(self):
        for kind, crop in (("stance", "subject"), ("sheet", "full")):
            s = gif.normalize({"kind": kind, "id": "x", "aspect": "4:3"}, SKILLS)
            self.assertEqual((s["width"], s["height"], s["crop"]), (0, 0, crop), kind)

    def test_a_bad_aspect_in_a_manifest_is_a_refusal_not_a_traceback(self):
        import io
        import contextlib
        spec = {"name": "m", "scenarios": [{"kind": "spell", "id": "bolt", "aspect": "wide"}]}
        with contextlib.redirect_stderr(io.StringIO()) as err:
            with unittest.mock.patch.object(gif, "load_manifest", return_value=spec), \
                    unittest.mock.patch.object(gif, "known_ids", return_value=RefusalTests.IDS):
                code = gif.cmd_prepare(types.SimpleNamespace(manifest="m"))
        self.assertEqual(code, 2)
        self.assertIn("REFUSED manifest", err.getvalue())

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


class RunnerAlignmentTests(unittest.TestCase):
    CHARS = {"sheep": "sheep", "owl": "owl"}
    IDS = dict(FormationAndPartyTests.IDS, casters={"bolt": "sheep", "free": "owl", "aegis": ""},
               targeting={"bolt": "SingleEnemy", "free": "SingleEnemy", "aegis": "SingleAlly"},
               needsDepth={"bolt": True}, spell=["bolt", "free", "aegis"], allSkills=["bolt", "free", "aegis"])

    def r(self, sid, **kw):
        return gif.refusals(gif.normalize(dict({"kind": "spell", "id": sid}, **kw), SKILLS, self.CHARS), self.IDS)

    def test_a_skill_with_no_authored_owner_still_has_a_lead_caster(self):
        self.assertIn("leads with its caster", self.r("free", party="sheep,owl")[0])
        self.assertEqual(self.r("free", party="owl,sheep"), [])

    def test_a_skill_that_needs_depth_has_a_rack_of_three_in_a_lone_formation(self):
        self.assertEqual(self.r("bolt", target="3"), [])
        self.assertIn("slot 1..1", self.r("free", target="3")[0])

    def test_an_ally_target_with_no_party_is_not_refused_for_an_unresolved_caster(self):
        self.assertEqual(self.r("aegis", target="owl"), [])
        self.assertIn("not a character", self.r("aegis", target="nobody")[0])

    def test_caster_of_walks_the_preview_ladder(self):
        chars = [{"id": "a", "battleSpritePath": ""}, {"id": "b", "battleSpritePath": "x", "signatureId": "wool"},
                 {"id": "c", "signatureId": "ember"}]
        self.assertEqual(gif.caster_of({"characterId": "c"}, chars), "c")
        self.assertEqual(gif.caster_of({"characterId": "zz"}, chars), "b")
        self.assertEqual(gif.caster_of({"resourceCost": 2, "characterId": "c"}, chars), "c")
        self.assertEqual(gif.caster_of({"resourceCost": 2}, chars), "b")
        self.assertEqual(gif.caster_of({}, chars), "b")
        self.assertEqual(gif.caster_of({"resourceCost": 1}, [{"id": "a"}]), "")
        self.assertTrue(gif.needs_depth({"reachSlots": [2, 3]}))
        self.assertFalse(gif.needs_depth({"reachSlots": [1, 2]}))
        self.assertFalse(gif.needs_depth({}))


class IdentityTests(unittest.TestCase):
    def test_manifest_diff_honours_globs_and_one_sided_paths(self):
        a = {"x/a.txt": "1", "gen/b.asset": "2", "only_a": "3"}
        b = {"x/a.txt": "1", "gen/b.asset": "9"}
        self.assertEqual(gif.manifest_diff(a, b), ["gen/b.asset", "only_a"])
        self.assertEqual(gif.manifest_diff(a, b, ["gen/**"]), ["only_a"])

    def test_fingerprint_depends_on_every_hash(self):
        self.assertNotEqual(gif.fingerprint({"a": "1"}), gif.fingerprint({"a": "2"}))
        self.assertEqual(gif.fingerprint({"a": "1", "b": "2"}), gif.fingerprint({"b": "2", "a": "1"}))


class StampAndLatestTests(unittest.TestCase):
    def make_project(self, root, body):
        os.makedirs(os.path.join(root, "Assets"))
        os.makedirs(os.path.join(root, "ProjectSettings"))
        with open(os.path.join(root, "ProjectSettings", "ProjectVersion.txt"), "w") as handle:
            handle.write("m_EditorVersion: 6000.6.2f1" + chr(10))
        with open(os.path.join(root, "Assets", "a.txt"), "w") as handle:
            handle.write(body)

    def test_stamp_fingerprints_the_tree_that_was_rendered_not_the_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            one, two, run = (os.path.join(tmp, n) for n in ("one", "two", "run"))
            self.make_project(one, "same")
            self.make_project(two, "different")
            os.makedirs(run)
            gif.cmd_stamp(types.SimpleNamespace(run_dir=run, root=one))
            first = gif.read_json(os.path.join(run, "identity.json"))
            gif.cmd_stamp(types.SimpleNamespace(run_dir=run, root=two))
            second = gif.read_json(os.path.join(run, "identity.json"))
            self.assertNotEqual(first["fingerprint"], second["fingerprint"])
            self.assertEqual(second["files"], 2)
            self.assertIn("Assets/a.txt", gif.read_json(os.path.join(run, "manifest.json")))

    def test_a_run_with_no_capture_copy_cannot_be_compared(self):
        with tempfile.TemporaryDirectory() as tmp:
            run_a, run_b = os.path.join(tmp, "a"), os.path.join(tmp, "b")
            for run in (run_a, run_b):
                os.makedirs(run)
            gif.write_json_atomic(os.path.join(run_a, "identity.json"), {"fingerprint": "abc", "files": 1})
            gif.write_json_atomic(os.path.join(run_b, "identity.json"), {"fingerprint": "", "files": 0})
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                code = gif.cmd_compare(types.SimpleNamespace(a=run_a, b=run_b, allow_diff=""))
            self.assertEqual(code, 2)
            self.assertIn("no content fingerprint", out.getvalue())

    def test_concurrent_latest_updates_keep_every_entry(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "latest.json")
            gif.write_json_atomic(path, {"old": "x"})
            threads = [threading.Thread(target=gif.update_json_locked, args=(path, {"k%d" % i: "run%d" % i}))
                       for i in range(24)]
            for t in threads:
                t.start()
            for t in threads:
                t.join()
            data = gif.read_json(path)
            self.assertEqual(len(data), 25)
            self.assertFalse(os.path.exists(path + ".lock"))

    def test_a_stale_lock_from_a_crashed_session_is_taken_over(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "latest.json")
            open(path + ".lock", "w").close()
            os.utime(path + ".lock", (0, 0))
            gif.update_json_locked(path, {"a": "1"})
            self.assertEqual(gif.read_json(path), {"a": "1"})


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


class PreviewTests(unittest.TestCase):
    def noisy_frames(self, count, size=(960, 540)):
        import random
        from PIL import Image
        rng = random.Random(7)
        frames = []
        for i in range(count):
            image = Image.new("RGB", size, (20, 20, 30))
            for _ in range(400):
                x, y = rng.randrange(size[0] - 40), rng.randrange(size[1] - 40)
                image.paste((rng.randrange(256), rng.randrange(256), rng.randrange(256)), (x, y, x + 40, y + 40))
            frames.append(image)
        return frames

    def test_the_preview_is_at_most_480_wide_under_the_byte_limit_and_keeps_the_runtime(self):
        from PIL import Image
        frames = self.noisy_frames(30)
        with tempfile.TemporaryDirectory() as root:
            out = os.path.join(root, "anim_preview.gif")
            size = gif.write_preview(frames, 1000.0 / 30.0, out, max_bytes=gif.PREVIEW_MAX_BYTES)
            self.assertTrue(os.path.isfile(out))
            self.assertEqual(size, os.path.getsize(out))
            self.assertLess(size, gif.PREVIEW_MAX_BYTES)
            with Image.open(out) as image:
                self.assertLessEqual(image.size[0], 480)
            self.assertAlmostEqual(sum(gif_durations(out)), 1000.0, delta=60)

    def test_a_tight_limit_steps_down_the_ladder_to_a_smaller_file(self):
        frames = self.noisy_frames(30)
        with tempfile.TemporaryDirectory() as root:
            first = gif.write_preview(frames, 33.3, os.path.join(root, "a.gif"), max_bytes=10 ** 9)
            tight = gif.write_preview(frames, 33.3, os.path.join(root, "b.gif"), max_bytes=1)
            self.assertLess(tight, first)

    def test_stance_and_sheet_series_write_a_preview_with_every_frame(self):
        with tempfile.TemporaryDirectory() as root:
            shown = [image.copy() for image in self.noisy_frames(4, (640, 360))]
            gif.write_series(root, shown, 400, "t")
            self.assertEqual(len(gif_durations(os.path.join(root, "anim_preview.gif"))), 4)

    def test_preview_can_be_skipped(self):
        with tempfile.TemporaryDirectory() as root:
            shown = [image.copy() for image in self.noisy_frames(2, (640, 360))]
            gif.write_series(root, shown, 400, "t", preview=False)
            self.assertFalse(os.path.exists(os.path.join(root, "anim_preview.gif")))


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


class ExportTests(unittest.TestCase):
    def scenario(self, **kw):
        return dict({"id": "vipers_bite", "key": "k1", "vs": "rat", "speed": 1.0, "formation": "lone", "preStatus": ""}, **kw)

    def test_names_carry_target_then_speed_formation_status_only_when_not_default(self):
        self.assertEqual(gif.export_stem(self.scenario(), "vipers_bite_eyes2"), "vipers_bite_eyes2_rat")
        self.assertEqual(gif.export_stem(self.scenario(vs="rat,treant", speed=1.5, formation="full", preStatus="Poison")),
                         "vipers_bite_rat-treant_1.5x_full_poison")

    def test_copies_the_four_files_under_the_stem_and_skips_missing_ones(self):
        with tempfile.TemporaryDirectory() as root:
            run, out = os.path.join(root, "run"), os.path.join(root, "notes")
            sdir = os.path.join(run, "k1")
            os.makedirs(sdir)
            gif.write_json_atomic(os.path.join(run, "request.json"), {"scenarios": [self.scenario(speed=1.5)]})
            for name in ("anim.gif", "anim_preview.gif", "strip.png", "timing.json"):
                with open(os.path.join(sdir, name), "w") as f:
                    f.write(name)
            os.remove(os.path.join(sdir, "timing.json"))
            gif.export_run(run, out, "vb")
            self.assertEqual(sorted(os.listdir(out)),
                             ["vb_rat_1.5x_full.gif", "vb_rat_1.5x_preview.gif", "vb_rat_1.5x_strip.png"])
            with open(os.path.join(out, "vb_rat_1.5x_preview.gif")) as f:
                self.assertEqual(f.read(), "anim_preview.gif")


class SheetTests60(unittest.TestCase):
    COLOURS = [(255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 0)]

    def make_gif(self, path):
        from PIL import Image
        frames = []
        for c in self.COLOURS:
            im = Image.new("RGB", (100, 40), (10, 10, 10))
            im.paste(c, (60, 0, 100, 40))  # the right 40% carries the frame colour
            frames.append(im)
        frames[0].save(path, save_all=True, append_images=frames[1:], duration=33, loop=0)

    def test_sixty_fps_frames_map_to_every_second_gif_frame(self):
        self.assertEqual([gif.sheet_gif_index(n) for n in (0, 1, 2, 5, 6)], [0, 0, 1, 2, 3])

    def test_a_row_per_input_a_tile_per_frame_cropped_to_the_right(self):
        with tempfile.TemporaryDirectory() as root:
            a = os.path.join(root, "a.gif")
            self.make_gif(a)
            sheet = gif.build_sheet([("rat", a), ("treant", a)], [0, 2, 99], crop=(0.55, 1.0), tile_width=50)
            self.assertEqual(sheet.size[0], 150)
            row_h = sheet.size[1] // 2
            # the tile body (below the label strip) is solid frame colour; f99 clamps to the last frame
            for col, want in enumerate([self.COLOURS[0], self.COLOURS[1], self.COLOURS[3]]):
                px = sheet.getpixel((col * 50 + 25, row_h - 4))
                self.assertTrue(all(abs(p - w) < 8 for p, w in zip(px, want)), (col, px, want))

    def test_run_folder_resolves_to_its_single_scenario_and_several_are_refused(self):
        with tempfile.TemporaryDirectory() as root:
            for k in ("k1", "k2"):
                os.makedirs(os.path.join(root, k))
            open(os.path.join(root, "k1", "anim.gif"), "w").close()
            self.assertEqual(gif.resolve_sheet_input(root), os.path.join(root, "k1", "anim.gif"))
            open(os.path.join(root, "k2", "anim.gif"), "w").close()
            with self.assertRaises(ValueError):
                gif.resolve_sheet_input(root)

    def test_crop_must_be_an_ordered_fraction_pair(self):
        self.assertEqual(gif.parse_crop("0.5,1"), (0.5, 1.0))
        with self.assertRaises(ValueError):
            gif.parse_crop("0.8,0.2")


if __name__ == "__main__":
    unittest.main()
