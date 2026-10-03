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
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Preview;
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
    //
    // THE FIGHT SET-UP AND THE STOP RULE ARE NOT HERE: they live in
    // Shared/CaptureScenarioRunner, which tools/gif.ps1's cold batch
    // (GifBatchCaptureTests) calls too, so a GIF and this series are recordings
    // of the same fight stopped by the same rule.
    public class SpellRuntimeCaptureTests
    {
        private const int CaptureFps = CaptureScenarioRunner.Fps;

        // The spell whose composition this exists to show, from
        // PP_RUNTIME_SPELL when tools/screenshot.ps1 -Runtime -Spell <id> set
        // one, defaulting to the pilot so today's -RuntimeFilter
        // SpellRuntimeCaptureTests behaviour is unchanged when nothing is
        // passed. Still not open to just anything: ForSpell below owns the
        // caster resolution and refusals, the same way tools/preview.ps1
        // -Spell does, and an id it cannot stand a fight up for is refused
        // there by name rather than approximated here.
        private static readonly string SpellId = ResolveSpellId();

        // Read directly here rather than through PreviewCaptureTests' own
        // env-var family: PreviewEnvironmentLintTests requires that family's
        // prefix appear in exactly one file (PreviewCaptureTests.cs, which is
        // [Explicit] and never runs in the gate), because a second fixture
        // reading one would test a different subset depending on the shell it
        // was started from. This capture is driven by tools/screenshot.ps1,
        // not tools/preview.ps1, so it gets its own variable rather than
        // reaching into that reserved family.
        private const string SpellVariable = "PP_RUNTIME_SPELL";
        private const string VersusVariable = "PP_RUNTIME_VERSUS";

        // Battle speed the cast is recorded at (tools/spell_feel.ps1 -Speed),
        // as FightBeatPlayer.BeatSpeedMultiplier. 1 when unset or unparsable.
        private const string SpeedVariable = "PP_RUNTIME_SPEED";

        // A status every fielded enemy already carries when the cast starts
        // (StatusEffectType name), so a spell that spends one -- Viper's Bite
        // detonating Poison -- is recorded doing so. Empty means none.
        private const string PreStatusVariable = "PP_RUNTIME_PRESTATUS";

        private static float ResolveSpeed()
        {
            string raw = System.Environment.GetEnvironmentVariable(SpeedVariable);
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float speed) && speed > 0f
                ? speed
                : 1f;
        }

        private static string ResolveSpellId()
        {
            string raw = System.Environment.GetEnvironmentVariable(SpellVariable);
            return string.IsNullOrWhiteSpace(raw) ? PilotSpellId : raw.Trim();
        }

        // THE WATER PILOT, which the two M8 overlap tests below are written
        // against and nothing else. Their frame numbers (SecondCastFrame,
        // WaterOverFaultFrame) and their premise (a tail of droplets still
        // alive half a second after release) are the pilot's own timings; with
        // PP_RUNTIME_SPELL naming winters_rebuke or blackglass_spear the
        // first cast's particles are gone before frame 30 and the overlap
        // assertion fails for a reason that says nothing about the spell being
        // captured. So they Ignore themselves for any other spell rather than
        // retime themselves per spell -- the frame series above is the part
        // that is general.
        private const string PilotSpellId = "prismatic_orb";

        private static void OnlyForThePilot(string what)
        {
            if (SpellId == PilotSpellId) return;

            Assert.Ignore(what + " is timed against the " + PilotSpellId + " pilot's own tail, and " +
                          SpellVariable + " names '" + SpellId + "'. Run it with no -Spell (or -Spell " +
                          PilotSpellId + ") to check the overlap; the frame series for '" + SpellId +
                          "' is TheWaterPilotRecordsAsAFrameSeries.");
        }

        // 1.25s at 60fps, which holds one whole pilot cast (0.63s) plus a
        // second one opened half a second in.
        private const int OverlapFrames = 75;

        // 0.50s from the first release: past the arrival and the cue, inside
        // the tail. Chosen against the beat's own hold (0.45s), so the overlap
        // recorded is the one two casts in a real round already produce.
        private const int SecondCastFrame = 30;

        // 0.33s into the fault, which runs 0.78s -- the plumes open at 0.2167s
        // (frame 13), so the water lands while every one is still erupting.
        private const int WaterOverFaultFrame = 20;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [TearDown]
        public void Restore()
        {
            CaptureScenarioRunner.Restore();
        }

        private readonly CaptureScenarioRunner _runner = new CaptureScenarioRunner();
        private FightController _fight;
        private Canvas _canvas;

        // The spell's own fight, from the PP_RUNTIME_* variables. `enemies` null
        // fields what the PLAN says (PreviewFight.EnemyCountFor over its
        // formation), which is what tools/preview.ps1 fields for the same spell;
        // a number overrides it -- see the overlap tests.
        private IEnumerator StandTheFightUp(int? enemies)
        {
            yield return _runner.StandTheFightUp(new CaptureStandRequest
            {
                SpellId = SpellId,
                Versus = System.Environment.GetEnvironmentVariable(VersusVariable),
                Speed = ResolveSpeed(),
                PreStatus = System.Environment.GetEnvironmentVariable(PreStatusVariable),
                Enemies = enemies,
            });

            _fight = _runner.Fight;
            _canvas = _runner.Canvas;
        }

        [UnityTest]
        public IEnumerator TheWaterPilotRecordsAsAFrameSeries()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/screenshot.ps1 -Runtime " +
                              "-RuntimeFilter SpellRuntimeCaptureTests");
            }

            // THE PLAN'S FORMATION, which is what tools/preview.ps1
            // photographs for the same spell: one enemy for a single-target
            // cast (the pilot, an Afflict), three for an all-target one.
            yield return StandTheFightUp(null);

            Directory.CreateDirectory(OutputDir);

            var outcome = new CaptureOutcome();
            yield return _runner.RecordCast(SpellId, relic: false, ResolveSpeed(), new CaptureSink
            {
                FramePath = frame => Path.Combine(OutputDir, string.Format("spell_{0}_f{1:00}.png", SpellId, frame)),
                TimingPath = Path.Combine(OutputDir, string.Format("spell_{0}_timing.json", SpellId)),
            }, outcome);

            Debug.Log("[SpellRuntime] wrote " + outcome.Frames + " frames to " + OutputDir +
                      ". ffmpeg -framerate " + CaptureFps + " -i spell_" + SpellId +
                      "_f%02d.png out.mp4 makes it a video.");
        }


        // ---- M8: two consecutive casts, the first's droplets still alive ---------

        // THE CASE THE POOLING EXISTS FOR, and the one a single-cast recording
        // cannot show: a second cast opening over a tail that has not finished.
        // The pilot's last droplet clears 0.63s after its release and a beat's
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
            OnlyForThePilot("The two-cast overlap");

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
                "pilot's shed and spray clear at 0.63s and the second opens at " +
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
            OnlyForThePilot("The cinderfault-and-water overlap");

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
        private static CombatBeat WaterAt(CombatantState caster, CombatantState target) => new CombatBeat
        {
            Actor = caster,
            Target = target,
            Vfx = CaptureScenarioRunner.ShippedVfx(SpellId),
        };

        private static CombatBeat CinderfaultOn(CombatantState caster, List<CombatantState> struck) =>
            new CombatBeat
            {
                Actor = caster,
                Target = struck[0],
                SplashTargets = struck.Skip(1).ToList(),
                Vfx = CaptureScenarioRunner.ShippedVfx("cinderfault"),
            };
    }
}
