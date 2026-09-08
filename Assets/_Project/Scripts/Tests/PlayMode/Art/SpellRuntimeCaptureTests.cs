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
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
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

        // 0.75s at 60fps. The pilot's last droplet clears at 0.667s (the spray
        // bursts at the 0.327s cue -- was 0.35s, pulled tighter 2026-09-08 --
        // and lives up to 0.34s), so this is the whole cast plus a few frames
        // of empty stage to prove it ended.
        private const int Frames = 45;

        // The spell whose composition this exists to show. Not a parameter:
        // this fixture is the pilot's evidence, and a filter that could point
        // it anywhere would need the caster resolution and refusals
        // PreviewFight already owns -- which is what tools/preview.ps1 -Spell
        // is for.
        private const string SpellId = "prismatic_orb";

        // 1.25s at 60fps, which holds one whole pilot cast (0.667s) plus a
        // second one opened half a second in.
        private const int OverlapFrames = 75;

        // 0.50s from the first release: past the arrival and the cue, inside
        // the tail. Chosen against the beat's own hold (0.45s), so the overlap
        // recorded is the one two casts in a real round already produce.
        private const int SecondCastFrame = 30;

        // 0.20s into the fault, which runs 0.78s -- the water lands while every
        // plume is still erupting.
        private const int WaterOverFaultFrame = 12;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [TearDown]
        public void Restore()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.captureFramerate = 0;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        private FightController _fight;
        private FightBeatPlayer _beats;
        private Canvas _canvas;

        // THE PILOT'S OWN FIGHT, stood up the way tools/preview.ps1 stands one
        // up: PreviewFight chooses the caster and the formation, so a recording
        // and a preview are photographs of the same encounter rather than of two
        // fixtures that drifted.
        private IEnumerator StandTheFightUp(int enemies)
        {
            var plan = PreviewFight.ForSpell(SpellId);
            Assert.IsTrue(plan.Ok, PreviewFight.Describe(plan));
            Debug.Log("[SpellRuntime] " + PreviewFight.Describe(plan));

            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.captureFramerate = CaptureFps;

            // A KNOWN TEARDOWN LOG, TOLERATED ACROSS THE SCENE SWAP AND
            // NOWHERE ELSE. Loading Fight over a Fight disables the outgoing
            // one, and FightBeatPlayer.OnDisable -> EndFight -> Flush ->
            // OnPlaybackFinished -> RefreshUi reaches
            // FightController.BeginAppearancePop (FightController.Hud.cs:1218),
            // which calls StartCoroutine on a panel that is already inactive
            // and logs an error. It is a real defect and it is recorded as
            // AUDIT.md #106; it belongs to fight-HUD teardown rather than to
            // anything this fixture is recording, and a second test in this
            // class is simply the first thing in the suite to load a fight
            // scene over a live one. The tolerance is lifted before the cast.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the fight scene has no controller");

            _beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_beats, "the fight scene has no beat player");

            // FightBootstrap's own placeholder fight is mid-beat and holds the
            // controller busy, so the forced press would be swallowed. Same
            // move PreviewCaptureTests and CinderfaultSpellCaptureTests make.
            _beats.Flush();
            yield return null;
            yield return null;

            // THE COUNT IS THE CALLER'S, not the plan's. PreviewFight.
            // EnemyCountFor answers for the SPELL being previewed, and the
            // pilot is single-target, so it says one however many are asked
            // for -- which is right for a recording of the pilot alone and
            // wrong for the overlap below, where a cinderfault needs a
            // formation to open under.
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

        [UnityTest]
        public IEnumerator TheWaterPilotRecordsAsAFrameSeries()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime " +
                              "-RuntimeFilter SpellRuntimeCaptureTests");
            }

            // ONE ENEMY, which is what PreviewFight's own formation rule gives
            // a single-target spell and therefore what tools/preview.ps1
            // photographs.
            yield return StandTheFightUp(1);

            var fight = _fight;
            var canvas = _canvas;

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
                "pilot's layers span 0.667s, which is 40 frames at " + CaptureFps + "fps, so a series " +
                "this empty is a recording of the stage rather than of the cast.");

            Debug.Log("[SpellRuntime] wrote " + Frames + " frames to " + OutputDir + " (" + lit +
                      " of them with the cast drawing; frame 0 is the release, " + waited +
                      " frames after the press). ffmpeg -framerate " + CaptureFps + " -i spell_" +
                      SpellId + "_f%02d.png out.mp4 makes it a video.");
        }

        // ---- M8: two consecutive casts, the first's droplets still alive ---------

        // THE CASE THE POOLING EXISTS FOR, and the one a single-cast recording
        // cannot show: a second cast opening over a tail that has not finished.
        // The pilot's last droplet clears 0.667s after its release and a beat's
        // hold is 0.45s, so this is not a contrived overlap -- it is what two
        // party members casting in one round already produce.
        //
        // THE ASSERTION IS THE POINT AND THE PICTURES ARE THE EVIDENCE. On the
        // frame the second cast opens, the first cast's HANDLE still owns at
        // least one particle -- a fact about ownership rather than about what a
        // human thought they saw in a PNG. The frames are then written for the
        // human, and only when there is a graphics device; the assertion runs
        // in the commit gate either way.
        //
        // DRIVEN THROUGH THE MODULE RATHER THAN THROUGH TWO BEATS, and that is
        // a deviation worth naming: the beat player is sequential, so a second
        // BEAT arrives when its own hold, hit-stop, settle and gap say it does,
        // which is not a time this fixture can choose. Opening the second cast
        // on a chosen frame is the only way to record a NAMED overlap; what it
        // gives up is the damage popup, which is not what this records.
        [UnityTest]
        public IEnumerator TwoConsecutiveCastsOverlapWithTheFirstsDropletsStillOwned()
        {
            yield return StandTheFightUp(1);

            var module = _fight.PerformancePlayerForTest;
            var caster = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(e => e != null && e.IsAlive);

            var first = _fight.PlaySpellVfxForTest(WaterAt(caster, foe));
            Assert.IsTrue(module.IsLive(first), "the first cast obtained nothing");

            bool haveGraphics = CanvasCapture.IsSupported;
            if (haveGraphics) Directory.CreateDirectory(OutputDir);

            int owned = -1;
            var second = CastHandle.None;

            for (int frame = 0; frame < OverlapFrames; frame++)
            {
                if (frame == SecondCastFrame)
                {
                    // READ BEFORE THE SECOND CAST OPENS, so the number is the
                    // FIRST cast's own and cannot be the second's.
                    owned = module.ParticlesOwnedBy(first);
                    second = _fight.PlaySpellVfxForTest(WaterAt(caster, foe));
                }

                if (haveGraphics)
                {
                    string path = Path.Combine(OutputDir,
                        string.Format("spell_{0}_two_f{1:00}.png", SpellId, frame));
                    CanvasCapture.RenderToFile(_canvas, path);
                }

                yield return null;
            }

            Assert.Greater(owned, 0,
                "on the frame the second cast opened, the first cast owned no particles at all -- so " +
                "this recording is of two casts in a row rather than of two casts overlapping. The " +
                "pilot's shed and spray clear at 0.667s and the second opens at " +
                (SecondCastFrame / (float)CaptureFps).ToString("F2") + "s.");

            Assert.IsTrue(second.IsLive, "the second cast never opened");

            Debug.Log("[SpellRuntime] second cast opened on frame " + SecondCastFrame + " with " + owned +
                      " droplets of the first still owned" +
                      (haveGraphics ? "; wrote " + OverlapFrames + " frames to " + OutputDir : ""));
        }

        // ---- M8: a cinderfault and a water tail on one target --------------------

        // TWO CASTS, TWO BANDS, ONE TARGET. The fault is a cast-level ground
        // layer under the whole rack; the water lands on one of the three
        // enemies standing in it while every plume is still erupting. The
        // assertion is that nothing is shared: two casts, disjoint members, and
        // the ground band held by the one that authored a ground layer.
        [UnityTest]
        public IEnumerator ACinderfaultAndAWaterTailOverlapOnOneTarget()
        {
            yield return StandTheFightUp(3);

            var module = _fight.PerformancePlayerForTest;
            var caster = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foes = _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();
            Assert.GreaterOrEqual(foes.Count, 2, "the overlap wants a formation, not a lone enemy");

            var fault = _fight.PlaySpellVfxForTest(CinderfaultOn(caster, foes));
            Assert.IsTrue(module.IsLive(fault), "the cinderfault obtained nothing");

            bool haveGraphics = CanvasCapture.IsSupported;
            if (haveGraphics) Directory.CreateDirectory(OutputDir);

            var water = CastHandle.None;
            bool checkedOverlap = false;

            for (int frame = 0; frame < OverlapFrames; frame++)
            {
                if (frame == WaterOverFaultFrame)
                {
                    water = _fight.PlaySpellVfxForTest(WaterAt(caster, foes[0]));
                }

                if (frame == WaterOverFaultFrame + 1)
                {
                    Assert.IsTrue(module.IsLive(fault) && module.IsLive(water),
                        "the two casts are not both alive, so there is no overlap to record");

                    // THE FAULT IS IN THE GROUND BAND AND THE WATER IS NOT -- a
                    // member index only means something inside its own band.
                    Assert.IsTrue(module.DrawsInGroundBand(fault, 0),
                        "the cinderfault's first layer is not drawing behind the racks");

                    var faultMembers = MembersOf(module, fault);
                    var waterMembers = MembersOf(module, water);

                    Assert.IsNotEmpty(faultMembers, "the cinderfault's plumes drew nothing");
                    Assert.IsNotEmpty(waterMembers, "the water cast drew no sprite layer");
                    CollectionAssert.IsEmpty(faultMembers.Intersect(waterMembers).ToList(),
                        "the two casts are sharing an effects renderer, so one is drawing over the " +
                        "other's member rather than beside it");

                    checkedOverlap = true;
                }

                if (haveGraphics)
                {
                    string path = Path.Combine(OutputDir, string.Format("spell_overlap_f{0:00}.png", frame));
                    CanvasCapture.RenderToFile(_canvas, path);
                }

                yield return null;
            }

            Assert.IsTrue(checkedOverlap, "the overlap frame was never reached");

            Debug.Log("[SpellRuntime] cinderfault plus a water cast on one of its targets, water opening " +
                      "on frame " + WaterOverFaultFrame +
                      (haveGraphics ? "; wrote " + OverlapFrames + " frames to " + OutputDir : ""));
        }

        // Every EFFECTS-band member a live cast holds, which is the list two
        // overlapping casts have to keep disjoint. Sixteen is past any cast the
        // pool can serve, so the walk ends before the instances do only if a
        // spell ever authors more layers than the band has members.
        private static List<int> MembersOf(SpellPerformancePlayer module, CastHandle handle)
        {
            var held = new List<int>();
            for (int instance = 0; instance < 16; instance++)
            {
                int member = module.MemberFor(handle, instance);
                if (member < 0) continue;
                if (module.DrawsInGroundBand(handle, instance)) continue;
                held.Add(member);
            }

            return held;
        }

        private static SpellPresentation ShippedVfx(string skillId)
        {
            var definition = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == skillId);
            Assert.IsNotNull(definition, "'" + skillId + "' is not in the content database");

            var vfx = PreviewFight.PreviewPresentationOf(definition.Data);
            Assert.IsTrue(vfx != null && vfx.HasArt, "'" + skillId + "' resolves to a presentation with no art");
            return vfx;
        }

        private static CombatBeat WaterAt(CombatantState caster, CombatantState target) => new CombatBeat
        {
            Actor = caster,
            Target = target,
            Vfx = ShippedVfx(SpellId),
        };

        private static CombatBeat CinderfaultOn(CombatantState caster, List<CombatantState> struck) =>
            new CombatBeat
            {
                Actor = caster,
                Target = struck[0],
                SplashTargets = struck.Skip(1).ToList(),
                Vfx = ShippedVfx("cinderfault"),
            };

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
