using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // THE TRANSFORM AND ONE OF ITS HITS, EVERY FRAME OF THEM.
    //
    // The same shape as SpellRuntimeCaptureTests and for the same reason: four
    // sampled stills cannot answer "does the change read as an event" or "does
    // the ram's blow land harder than Shawn's", because both are about how one
    // instant follows another. Time.captureFramerate makes a frame index an
    // instant, so 60 of these ARE one second of the game.
    //
    // NOT A GATE. run_tests_parallel passes -nographics, where
    // CanvasCapture.IsSupported is false and ReadPixels returns garbage, so
    // this ignores itself there and names the command that does run it:
    //   tools/screenshot.ps1 -Runtime -RuntimeFilter BlackRamRuntimeCaptureTests
    // What the SUITE checks about the same two moments is
    // TransformFlashTests, which needs no graphics at all.
    public class BlackRamRuntimeCaptureTests
    {
        private const int CaptureFps = 60;
        private const string TransformSkill = "black_ram_mode";

        // 1.5s at 60fps. The transform beat is a wind-up plus a 0.21s flash
        // plus its settle; this holds the whole of it with room either side.
        private const int TransformFrames = 90;

        // 1.75s: the ram's own swing, its contact effect and its recoil.
        private const int HitFrames = 105;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        private FightController _fight;
        private FightBeatPlayer _beats;
        private Canvas _canvas;

        // The same debt TransformFlashTests.Restore pays and for the same
        // reason (AUDIT #106): this capture stops mid-round, so the flush that
        // tearing its scene down triggers has to happen here, inside the
        // tolerance, rather than inside whichever fixture loads a scene next.
        [UnityTearDown]
        public IEnumerator Restore()
        {
            LogAssert.ignoreFailingMessages = true;
            if (_beats != null) _beats.EndFight();
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            Time.captureFramerate = 0;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        [UnityTest]
        public IEnumerator TheTransformAndTheRamsFirstBlowRecordAsAFrameSeries()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime " +
                              "-RuntimeFilter BlackRamRuntimeCaptureTests");
            }

            yield return StandTheFightUp();

            Directory.CreateDirectory(OutputDir);

            _fight.ForceFirstAction(TransformSkill);
            yield return WaitForBusy();

            for (int frame = 0; frame < TransformFrames; frame++)
            {
                CanvasCapture.RenderToFile(_canvas,
                    Path.Combine(OutputDir, string.Format("black_ram_become_f{0:00}.png", frame)));
                yield return null;
            }

            // BACK TO THE PLAYER'S TURN before the second half: the ram's own
            // swing is what the hit presentation is authored for, and it has to
            // be pressed rather than waited for.
            float deadline = Time.realtimeSinceStartup + 20f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            var attack = Named("Verb0");
            var plate = Named("EnemyPlate0");
            Assert.IsNotNull(attack, "no ATTACK verb in the Fight scene");
            Assert.IsNotNull(plate, "no enemy plate in the Fight scene");

            attack.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            plate.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitForBusy();

            for (int frame = 0; frame < HitFrames; frame++)
            {
                CanvasCapture.RenderToFile(_canvas,
                    Path.Combine(OutputDir, string.Format("black_ram_hit_f{0:00}.png", frame)));
                yield return null;
            }

            Debug.Log("[BlackRam] wrote " + TransformFrames + " become-frames and " + HitFrames +
                      " hit-frames to " + OutputDir);
        }

        // ---- fixture --------------------------------------------------------

        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private IEnumerator WaitForBusy()
        {
            float armed = Time.realtimeSinceStartup;
            while (!_fight.IsBusy && Time.realtimeSinceStartup - armed < 20f) yield return null;
            Assert.IsTrue(_fight.IsBusy, "nothing was ever pressed -- the log says what was refused");
        }

        private IEnumerator StandTheFightUp()
        {
            var plan = PreviewFight.ForSpell(TransformSkill);
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            Time.captureFramerate = CaptureFps;

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

            var fielded = PreviewFight.EnemiesWithArt(1);
            Assert.IsNotEmpty(fielded, "no enemies in content to stand a fight up against");

            var built = FightEncounterAdapter.Build(
                new List<string> { plan.CasterId },
                fielded,
                new Domain.Rng.SeededRandom(20260909),
                relicIds: null,
                depthStep: 0,
                previewExtraSkillIds: new List<string> { TransformSkill });

            Assert.IsNotNull(built?.Session, TransformSkill + " could not be built into an encounter");

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
    }
}
