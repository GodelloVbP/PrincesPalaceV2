using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // G5, docs/PLAN_BATTLE_SPEED.md (mandatory): playable evidence that "1x"
    // answers "fights read as too fast" -- a plain melee beat and a real
    // Water (prismatic_orb) cast at the three most legible rows on the table
    // (0.5x, 1x, 2x -- skipping 1.5x, today's relabelled pace, since the
    // point is showing the NEW range), plus one sequence with a live Water
    // tail carried across a pause-step-resume from 0.5x to 2x.
    //
    // SAME PATTERN AS SpellRuntimeCaptureTests: numbered frames written at
    // Time.captureFramerate, assembled OUTSIDE Unity into a playable file
    // (a GIF at 60fps via PIL, this session's own tooling). The preset is
    // burned into the frames themselves via FightController.
    // PushLogLineForTest, onto the same visible bark banner a real fight
    // uses for combat messages -- a viewer scrubbing the assembled GIF can
    // read which preset is playing without a caption added after the fact.
    //
    // NOT A GATE. tools/run_tests_parallel.ps1 passes -nographics, where
    // CanvasCapture.IsSupported is false; every test here Ignores itself
    // there, same guard SpellRuntimeCaptureTests' own header carries.
    // Driven by tools/screenshot.ps1 -Runtime -RuntimeFilter
    // BattleSpeedRuntimeCaptureTests.
    public class BattleSpeedRuntimeCaptureTests
    {
        private const int CaptureFps = 60;
        private const string SpellId = "prismatic_orb";

        // FLAT, not a subfolder -- tools/screenshot.ps1's own copy-back check
        // (Get-ChildItem $runnerOut -Filter *.png, no -Recurse) only looks at
        // this directory's own top level, the same convention every other
        // *CaptureTests fixture in this project already follows for exactly
        // that reason.
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        private FightController _fight;
        private FightBeatPlayer _beats;
        private Canvas _canvas;

        // Set by StandTheFightUp from the plan it resolved, so a caller that
        // asked for an element (RecordWaterAt, the pause-step-resume test)
        // presses the SAME one on ForceFirstAction rather than letting the
        // forced path fall back to "the first that draws" -- which is Earth,
        // authored first among prismatic_orb's four elements. Before this
        // field existed, every "water" capture in this fixture cast
        // unelemented and silently recorded Earth's art under a Water
        // filename. 2026-09-08.
        private string _planElement;

        [TearDown]
        public void Restore()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            TestGlobals.ResetAll();
        }

        private IEnumerator StandTheFightUp(int enemies, string element = null)
        {
            var plan = PreviewFight.ForSpell(SpellId, element);
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            _planElement = plan.Element;

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.captureFramerate = CaptureFps;

            // Same tolerated-teardown-log window SpellRuntimeCaptureTests'
            // own StandTheFightUp carries (AUDIT.md #106) -- a second test in
            // this class loading Fight over a live one.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the fight scene has no controller");
            _beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_beats, "the fight scene has no beat player");
            _beats.Flush();
            yield return null;
            yield return null;

            var fielded = PreviewFight.EnemiesWithArt(enemies);
            Assert.IsNotEmpty(fielded, "no enemies in content to cast at");

            var built = FightEncounterAdapter.Build(
                new List<string> { plan.CasterId },
                fielded,
                new Domain.Rng.SeededRandom(20260908),
                relicIds: null,
                depthStep: 0,
                previewExtraSkillIds: new List<string> { SpellId });

            Assert.IsNotNull(built?.Session, "'" + SpellId + "' could not be built into an encounter");

            PreviewFight.Prepare(built, plan);
            built.Session.Begin();
            _fight.Bind(built.Session, EncounterClass.Normal);
            _fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return null;

            _canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(_canvas, "the fight scene has no root canvas");
        }

        // Adopts immediately -- the caller decides when, which is what makes
        // the pause-step-resume sequence possible: this is called again mid-
        // capture, after the source has already changed, exactly like
        // AdoptPlayerSpeedForTest everywhere else in this plan's tests.
        private static void SetPreset(float display)
        {
            float multiplier = BattleSpeed.Nearest(display).Multiplier;
            FightBeatPlayer.PlayerSpeedSource = () => multiplier;
            FightBeatPlayer.AdoptPlayerSpeedForTest();
        }

        private static string PresetLabel(float display) => BattleSpeed.Nearest(display).DisplayNumber + "x";

        private IEnumerator RecordFrames(string filePrefix, int frameCount, int startAt = 0)
        {
            Directory.CreateDirectory(OutputDir);
            for (int i = 0; i < frameCount; i++)
            {
                string path = Path.Combine(OutputDir, $"{filePrefix}_f{(startAt + i):000}.png");
                CanvasCapture.RenderToFile(_canvas, path);
                FileAssert.Exists(path);
                yield return null;
            }
        }

        private static void RefuseWithoutGraphics()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter " +
                              "BattleSpeedRuntimeCaptureTests");
            }
        }

        // ---- the plain melee beat --------------------------------------------------

        [UnityTest]
        public IEnumerator APlainMeleeBeatAtDefaultPace()
        {
            RefuseWithoutGraphics();
            yield return StandTheFightUp(1);

            SetPreset(1f);
            _fight.PushLogLineForTest("Battle speed " + PresetLabel(1f));

            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(e => e != null && e.IsAlive);

            bool finished = false;
            _beats.Play(new List<CombatBeat>
            {
                new CombatBeat { Actor = hero, Target = foe, Amount = 12, DamageType = DamageType.Physical },
            }, () => finished = true);

            yield return RecordFrames("battlespeed_melee_1x", 80);

            Debug.Log("[BattleSpeedRuntime] wrote battlespeed_melee_1x frames to " + OutputDir +
                      " (playback finished: " + finished + ")");
        }

        // ---- Water at the three legible rows ---------------------------------------

        private IEnumerator RecordWaterAt(float display, int frameCount)
        {
            RefuseWithoutGraphics();
            yield return StandTheFightUp(1, "Water");

            SetPreset(display);
            string label = PresetLabel(display);
            _fight.PushLogLineForTest("Battle speed " + label);

            _fight.ForceFirstAction(SpellId, _planElement);

            float armed = Time.realtimeSinceStartup;
            while (!_fight.IsBusy && Time.realtimeSinceStartup - armed < 15f) yield return null;
            Assert.IsTrue(_fight.IsBusy, "'" + SpellId + "' was never cast -- the log says what was refused");

            string prefix = "battlespeed_water_" + label;
            yield return RecordFrames(prefix, frameCount);

            Debug.Log("[BattleSpeedRuntime] wrote " + prefix + " frames to " + OutputDir);
        }

        // 0.5x (multiplier 1/3): the pilot's 0.69s authored cast is ~2.07s of
        // engine time, ~124 frames at 60fps -- 140 covers it with a tail.
        [UnityTest]
        public IEnumerator WaterAtHalfSpeed()
        {
            yield return RecordWaterAt(0.5f, 140);
        }

        // 1x (multiplier 2/3): ~1.04s engine, ~62 frames -- 75 covers it.
        [UnityTest]
        public IEnumerator WaterAtDefaultSpeed()
        {
            yield return RecordWaterAt(1f, 75);
        }

        // 2x (multiplier 4/3): ~0.52s engine, ~31 frames -- 45 covers it.
        [UnityTest]
        public IEnumerator WaterAtDoubleSpeed()
        {
            yield return RecordWaterAt(2f, 45);
        }

        // ---- the pause-step-resume sequence -----------------------------------------

        // Contract 4 as PLAYABLE evidence rather than as BattleSpeedPausedMenuTests'
        // structural assertion: a live Water tail, opened at 0.5x, held
        // through a real Time.timeScale = 0 pause (SystemMenuController's own
        // mechanism), stepped to 2x while still paused, then resumed. The
        // tail must visibly hold still through the pause and visibly speed
        // up only after the resume, never mid-pause.
        [UnityTest]
        public IEnumerator AWaterTailLivesAcrossAPauseStepResumeFromHalfToDoubleSpeed()
        {
            RefuseWithoutGraphics();
            yield return StandTheFightUp(1, "Water");

            SetPreset(0.5f);
            _fight.PushLogLineForTest("Battle speed 0.5x");

            _fight.ForceFirstAction(SpellId, _planElement);
            float armed = Time.realtimeSinceStartup;
            while (!_fight.IsBusy && Time.realtimeSinceStartup - armed < 15f) yield return null;
            Assert.IsTrue(_fight.IsBusy, "'" + SpellId + "' was never cast -- the log says what was refused");

            string prefix = "battlespeed_water_pause_step_resume";
            int frame = 0;

            // SLOW HALF: the tail opens and draws at 0.5x.
            yield return RecordFrames(prefix, 40, frame);
            frame += 40;

            // PAUSE: Time.timeScale = 0, the exact mechanism
            // SystemMenuController.Pause uses (SystemMenuController.cs:207-227)
            // -- held for a visible run of frames so the assembled GIF shows a
            // genuine freeze rather than one skipped tick.
            Time.timeScale = 0f;
            _fight.PushLogLineForTest("Paused");
            yield return RecordFrames(prefix, 15, frame);
            frame += 15;

            // STEP: to display 2 (2x) while STILL paused -- contract 4's own
            // case (BattleSpeedPausedMenuTests proves this structurally; this
            // is the same claim, watched rather than asserted).
            SetPreset(2f);
            _fight.PushLogLineForTest("Battle speed 2x");
            yield return RecordFrames(prefix, 5, frame);
            frame += 5;

            // RESUME.
            Time.timeScale = 1f;
            yield return RecordFrames(prefix, 100, frame);

            Debug.Log("[BattleSpeedRuntime] wrote " + prefix + " frames to " + OutputDir);
        }
    }
}
