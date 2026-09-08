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
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // A CAST, EVERY FRAME OF IT, IN REAL TIME.
    //
    // The brief asks for real-time in-game playback as evidence and says
    // screenshots supplement it. Four sampled stills cannot answer the two
    // questions the pilot is actually judged on -- does the ball read as heavy,
    // and does the splash arrive WITH the blow -- because both are about how
    // one instant follows another. This writes the whole cast as a frame
    // series; `ffmpeg -framerate 60 -i spell_prismatic_orb_f%02d.png out.mp4`
    // makes it a video, and the series is the thing this can produce.
    //
    // WHAT MAKES IT MEAN ANYTHING IS M4'S CLOCK. The renderer used to read
    // realtimeSinceStartup while the beat waited on scaled time, so a capture
    // sampled the spell at wall speed while frames advanced at a fixed step and
    // the recording showed a spell running at a different rate from the fight
    // around it. Time.time and Time.captureFramerate move together, so 60
    // frames of this series ARE one second of the game.
    //
    // NOT A GATE, and it cannot be: run_tests_parallel passes -nographics,
    // where CanvasCapture.IsSupported is false and ReadPixels returns garbage,
    // so this Ignores itself there -- the same guard RuntimeScreenshotTests
    // carries, and for the same reason it is a guard rather than [Explicit]:
    // the ignore MESSAGE names the command that does run it, which an
    // Explicit's silence does not. Driven by tools/screenshot.ps1 -Runtime
    // -RuntimeFilter SpellRuntimeCaptureTests.
    //
    // M8 EXTENDS THIS RATHER THAN REPLACING IT: a second cast opened while the
    // first cast's droplets are still alive, with the fixture asserting on the
    // frame that second cast opens that the first still owns a particle. This
    // is the one-cast half, which is what M5 owes.
    public class SpellRuntimeCaptureTests
    {
        // 60, matching RuntimeScreenshotTests. With captureFramerate set, time
        // advances by exactly 1/fps per frame however long the frame really
        // took, so a frame index is an instant and the series plays back at the
        // rate it was authored at.
        private const int CaptureFps = 60;

        // 0.75s at 60fps. The pilot's last droplet clears at 0.69s (the spray
        // bursts at the 0.35s cue and lives up to 0.34s), so this is the whole
        // cast plus a few frames of empty stage to prove it ended.
        private const int Frames = 45;

        // The spell whose composition this exists to show. Not a parameter:
        // this fixture is the pilot's evidence, and a filter that could point
        // it anywhere would need the caster resolution and refusals
        // PreviewFight already owns -- which is what tools/preview.ps1 -Spell
        // is for.
        private const string SpellId = "prismatic_orb";

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [TearDown]
        public void Restore()
        {
            Time.captureFramerate = 0;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        [UnityTest]
        public IEnumerator TheWaterPilotRecordsAsAFrameSeries()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime " +
                              "-RuntimeFilter SpellRuntimeCaptureTests");
            }

            var plan = PreviewFight.ForSpell(SpellId);
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Debug.Log("[SpellRuntime] " + PreviewFight.Describe(plan));

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.captureFramerate = CaptureFps;

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the fight scene has no controller");

            var beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(beats, "the fight scene has no beat player");

            // FightBootstrap's own placeholder fight is mid-beat and holds the
            // controller busy, so the forced press would be swallowed. Same
            // move PreviewCaptureTests and CinderfaultSpellCaptureTests make.
            beats.Flush();
            yield return null;
            yield return null;

            var enemies = PreviewFight.EnemiesWithArt(PreviewFight.EnemyCountFor(plan.Formation, 3));
            Assert.IsNotEmpty(enemies, "no enemies in content to cast at");

            var built = FightEncounterAdapter.Build(
                new List<string> { plan.CasterId },
                enemies,
                new Domain.Rng.SeededRandom(20260908),
                relicIds: null,
                depthStep: 0,
                previewExtraSkillIds: new List<string> { SpellId });

            Assert.IsNotNull(built?.Session, "'" + SpellId + "' could not be built into an encounter");

            PreviewFight.Prepare(built, plan);
            built.Session.Begin();
            fight.Bind(built.Session, EncounterClass.Normal);
            fight.BindPartyArt(built.Party, built.PartyArt);
            yield return null;
            yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "the fight scene has no root canvas");

            fight.ForceFirstAction(SpellId);

            float armed = Time.realtimeSinceStartup;
            while (!fight.IsBusy && Time.realtimeSinceStartup - armed < 15f) yield return null;
            Assert.IsTrue(fight.IsBusy, "'" + SpellId + "' was never cast -- the log says what was refused");

            // FRAME ZERO IS THE FRAME THE CAST FIRST DRAWS, so the series opens
            // on the ball leaving rather than on however many frames of wind-up
            // the beat happened to take.
            int waited = 0;
            while (!AnythingDrawn(fight) && waited < 240)
            {
                waited++;
                yield return null;
            }

            Assert.Less(waited, 240, "the cast never drew anything, so there is no recording to make");

            Directory.CreateDirectory(OutputDir);

            int lit = 0;
            for (int frame = 0; frame < Frames; frame++)
            {
                if (AnythingDrawn(fight)) lit++;

                string path = Path.Combine(OutputDir,
                    string.Format("spell_{0}_f{1:00}.png", SpellId, frame));
                CanvasCapture.RenderToFile(canvas, path);
                FileAssert.Exists(path);

                yield return null;
            }

            // THE SERIES IS OF SOMETHING. A recording of an empty stage is the
            // failure this cannot see by eye -- and the one the pilot actually
            // produced twice before the particle pool's members were being
            // activated at all.
            Assert.Greater(lit, 20,
                "only " + lit + " of " + Frames + " recorded frames had anything drawn on them. The " +
                "pilot's layers span 0.69s, which is 41 frames at " + CaptureFps + "fps, so a series " +
                "this empty is a recording of the stage rather than of the cast.");

            Debug.Log("[SpellRuntime] wrote " + Frames + " frames to " + OutputDir + " (" + lit +
                      " of them with the cast drawing; frame 0 is the release, " + waited +
                      " frames after the press). ffmpeg -framerate " + CaptureFps + " -i spell_" +
                      SpellId + "_f%02d.png out.mp4 makes it a video.");
        }

        private static bool AnythingDrawn(FightController fight) =>
            fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Any(p => p != null && p.Image != null && p.Image.enabled) ||
            AnyParticleDrawn(fight);

        private static bool AnyParticleDrawn(FightController fight)
        {
            var pool = fight.GetComponentInChildren<SpellParticleRenderer>(includeInactive: true);
            if (pool == null) return false;

            for (int i = 0; i < pool.Capacity; i++)
            {
                if (pool.IsDrawing(i)) return true;
            }

            return false;
        }
    }
}
