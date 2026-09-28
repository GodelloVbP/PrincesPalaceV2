using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE M5 CAPTURE (docs/PLAN_BELLWETHER_KIT.md M5): the real Bellwether's
    // kit, played on the real Fight scene, photographed at instants inside its
    // beats -- the toll ripple with the rally badge, the chains in flight and
    // binding, the knell telegraph with its floor figures (front, then after a
    // step to the middle), the knell landing, the Scratch rake, and the Bleed
    // badge after it. A picture, not an assertion: the behaviour is pinned by
    // BellwetherKitContentTests, BellwetherPresentationTests and
    // KnellFloorFigureTests.
    //
    // Frame-stepped (Time.captureFramerate), so a frame index is an instant of
    // the beat's own clock; time is frozen while a picture is taken, so the
    // 16:9 and 4:3 frames of one instant are the same instant.
    //
    // Graphics device only, on the hidden desktop:
    //   tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.BellwetherKitCaptureTests
    // PNGs land in tools/screenshots/runtime/kit_m5/ in the runner copy; the
    // set worth keeping is copied to docs/captures/kit-m5/. NEEDS BUILT CONTENT.
    public class BellwetherKitCaptureTests
    {
        private const int Fps = 60;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime", "kit_m5"));

        private static readonly (string Name, UiVec Frame)[] Aspects =
        {
            ("16x9", UiFrames.Reference),
            ("4x3", UiFrames.FourThree),
        };

        private Canvas _canvas;
        private FightController _fight;
        private FightBeatPlayer _player;

        [TearDown]
        public void Restore()
        {
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            TestGlobals.ResetAll();
            ContentDatabase.Reset();
        }

        [UnityTest]
        public IEnumerator CaptureTheBellwethersKit()
        {
            if (!CanvasCapture.IsSupported)
                Assert.Ignore("No graphics device. Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.BellwetherKitCaptureTests");

            if (Directory.Exists(OutputDir))
            {
                foreach (var old in Directory.GetFiles(OutputDir, "*.png")) File.Delete(old);
            }

            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _fight = UnityEngine.Object.FindAnyObjectByType<FightController>();
            _player = UnityEngine.Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_fight);
            Assert.IsNotNull(_player);

            var built = FightEncounterAdapter.Build(new List<string> { "sheep" }, new List<string> { "bellwether" },
                new SeededRandom(21), previewExtraSkillIds: new List<string> { "palace_passage" });
            Assert.IsNotNull(built?.Session);
            var session = built.Session;
            var shawn = built.Party[0];
            var bell = session.Encounter.Enemies[0];

            // Durable enough to live through the kit; a round limit so the
            // toll's round beat is the one the Bell in the Fog records.
            shawn.MaxHealth = shawn.CurrentHealth = 4000;
            bell.MaxHealth = bell.CurrentHealth = 9999;
            session.DamageVarianceRange = 0f;
            session.RoundLimit = 30;
            Assert.AreEqual(PlaceOutcome.Placed, session.Encounter.PlaceAt(shawn, 2, out _), "Shawn opens at the rear");
            session.Begin();

            _fight.Bind(session, Domain.Rewards.EncounterClass.Normal);
            _fight.BindPartyArt(built.Party.Take(1).ToList(), built.PartyArt.Take(1).ToList());
            yield return KnellBadgeFixture.Settle(_fight);
            _canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).First(c => c.isRootCanvas);
            yield return WaitReal(0.4f);

            Time.captureFramerate = Fps;

            // 1. The chains are telegraphed (Pull icon, "Death Knell next").
            yield return Shoot("a_chains_telegraph");

            // 2. Shawn swings; the chains fly and bind (and the round's toll).
            session.ExecuteAttack(bell);
            yield return PlayAndShoot(session, b => b.IsAction && b.Actor == bell,
                "b_chains", 0.3f, 0.6f, 0.8f, 0.95f, 1.1f, 1.3f, 1.5f);

            // 3. The knell committed, Shawn at the front: the lethal telegraph.
            Assert.AreEqual(0, session.Encounter.SeatOf(shawn), "the chains pulled him to the front");
            _fight.RefreshUi();
            yield return null;
            yield return Shoot("c_knell_telegraph_front");

            // 4. A free Palace Passage to the middle: the stage moves him, the
            // badge and the emphasised floor figure follow.
            yield return KnellBadgeFixture.PassageTo(_fight, session, shawn, 1);
            Assert.AreEqual(1, session.Encounter.SeatOf(shawn));
            yield return Shoot("d_knell_telegraph_middle");

            // 5. The knell lands on the middle seat.
            session.ExecuteAttack(bell);
            yield return PlayAndShoot(session, b => b.IsAction && b.Actor == bell,
                "e_knell", 0.3f, 0.5f, 0.7f, 0.8f, 0.9f, 1.1f);

            // 6. Scratch telegraphed (Bleed icon), then the rake, then Bleed on him.
            _fight.RefreshUi();
            yield return null;
            yield return Shoot("f_scratch_telegraph");
            for (int guard = 0; guard < 6; guard++)
            {
                session.ExecuteAttack(bell);
                var beats = session.DrainBeats();
                bool scratches = beats.Any(b => b.IsAction && b.Actor == bell);
                yield return PlayAndShoot(beats, scratches ? b => b.IsAction && b.Actor == bell : (Func<CombatBeat, bool>)null,
                    "g_scratch", 0.25f, 0.35f, 0.42f, 0.48f, 0.55f, 0.7f);
                if (scratches) break;
            }

            _fight.RefreshUi();
            yield return null;
            yield return Shoot("h_after_scratch_bleed");

            // 7. A later toll, the rally badge well into its count.
            for (int guard = 0; guard < 12; guard++)
            {
                session.Encounter.PlaceAt(shawn, 2, out _);
                session.ExecuteAttack(bell);
                var beats = session.DrainBeats();
                bool tolls = beats.Any(b => b.Cause == BeatCause.RoundStart && b.Actor == bell)
                             && session.RallyStacksOf(bell) >= 3;
                yield return PlayAndShoot(beats,
                    tolls ? b => b.Cause == BeatCause.RoundStart && b.Actor == bell : (Func<CombatBeat, bool>)null,
                    "i_toll", 0.1f, 0.25f, 0.4f, 0.55f);
                if (tolls) break;
            }
        }

        private IEnumerator PlayAndShoot(FightSession session, Func<CombatBeat, bool> pick, string name,
            params float[] instants) =>
            PlayAndShoot(session.DrainBeats(), pick, name, instants);

        // Plays the beats through the scene's own player and photographs the
        // FIRST beat `pick` matches at each instant (seconds after it opens).
        private IEnumerator PlayAndShoot(IReadOnlyList<CombatBeat> beats, Func<CombatBeat, bool> pick, string name,
            params float[] instants)
        {
            int wanted = -1;
            if (pick != null)
            {
                for (int i = 0; i < beats.Count; i++)
                {
                    if (beats[i] != null && pick(beats[i])) { wanted = i; break; }
                }
            }

            float startedAt = -1f;
            bool finished = false;
            Action<int, float> started = (index, _) => { if (index == wanted) startedAt = Time.time; };
            var previous = _player.BeatStarted;
            _player.BeatStarted = (index, speed) => { previous?.Invoke(index, speed); started(index, speed); };

            // Busy while it plays, as AfterResolution holds it: the telegraph
            // (badge, callout, floor figures) is down during playback.
            var busy = typeof(FightController).GetField("_isBusy",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            busy.SetValue(_fight, true);
            _fight.RefreshUi();

            if (beats.Count == 0) finished = true;
            else _player.Play(beats, () => finished = true);

            int shot = 0;
            for (int frame = 0; !finished && frame < 60 * Fps; frame++)
            {
                // Beat time, not frames: time is frozen while a picture is
                // taken, so later instants do not drift by the shots before.
                if (startedAt >= 0f && shot < instants.Length
                    && Time.time - startedAt >= instants[shot] - 0.001f)
                {
                    yield return Shoot($"{name}_{Mathf.RoundToInt(instants[shot] * 100):000}");
                    shot++;
                    continue;
                }

                yield return null;
            }

            _player.BeatStarted = previous;
            busy.SetValue(_fight, false);
            Assert.IsTrue(finished, $"{name}: playback never finished");
            _fight.RefreshUi();
            yield return null;
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private IEnumerator Shoot(string state)
        {
            float scale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                foreach (var (aspect, frame) in Aspects)
                {
                    int width = (int)frame.X;
                    int height = (int)frame.Y;

                    var rig = StageCaptureRig.FullFrame(_canvas, width, height);
                    try
                    {
                        yield return null;
                        yield return null;
                        yield return null;
                    }
                    finally
                    {
                        rig.Restore();
                    }

                    Directory.CreateDirectory(OutputDir);
                    string path = Path.Combine(OutputDir, $"bell_{state}_{aspect}.png");
                    CanvasCapture.RenderToFile(_canvas, path, width, height);
                    FileAssert.Exists(path);
                    Debug.Log($"[BellwetherKitCapture] wrote {path}");
                }
            }
            finally
            {
                Time.timeScale = scale;
            }
        }
    }
}
