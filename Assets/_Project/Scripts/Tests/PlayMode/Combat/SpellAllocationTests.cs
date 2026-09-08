using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

// THE ALLOCATION CONSTRAINT LIVES IN TWO PLACES AT ONCE: the extension method
// AllocatingGCMemory hangs off UnityEngine.TestTools.Constraints, whose own `Is`
// then collides with NUnit's. Aliased rather than fully qualified at the call
// site, because the alias is one line and the qualification would be three.
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PrincesPalace.PlayModeTests
{
    // WHAT A CAST COSTS A FRAME, MEASURED RATHER THAN ASSERTED.
    //
    // "The tick path allocates nothing" was a design property with no test
    // behind it from M4 until here -- the tick was WRITTEN for it
    // (SpellSchedule.Crossed fills a caller's buffer rather than returning a
    // collection; the emitter is a closed form over its inputs), but an
    // intention is not a measurement and this pool exists precisely so a
    // sixty-four-droplet spell does not hand the collector a kilobyte a frame.
    //
    // PER-FRAME BYTES, NOT RETAINED BYTES. An earlier revision of the plan
    // measured GC.GetTotalMemory(false) across ten casts, which reads whatever
    // the collector happened not to have swept: a tick allocating a kilobyte
    // every frame passes that whenever a collection lands inside the window. A
    // test that quietly measures the collector's mood instead of the code is
    // worse than no test.
    //
    // TWO ASSERTIONS, AND ONLY ONE OF THEM IS "ZERO".
    //
    //   Zero, on the module's own tick, through the test framework's own
    //   allocation constraint -- which measures the delegate rather than the
    //   frame. That is the one that carries the meaning: no new list, array,
    //   closure or boxed enumerator per tick.
    //
    //   A DIFFERENCE of zero, on the whole frame. A Unity frame in this scene
    //   is not allocation-free and never was -- FightBeatPlayer news a
    //   WaitForSeconds per wait, StruckBy is an iterator, Canvas and TMP
    //   rebuild on their own schedule -- so the recorder measures the same
    //   scene twice for the same number of frames, idle and during an
    //   overlapping cast, and compares the two MEDIANS. Comparing against
    //   itself rather than against a made-up ceiling is what keeps the number
    //   from being a constant somebody re-measured once and then defended.
    //
    // MEDIAN AND NOT SUM, deliberately: beginning a cast allocates its own
    // per-instance arrays once, which is a cost of casting rather than of
    // ticking, and a sum would report those few frames as if every frame paid
    // them.
    public class SpellAllocationTests
    {
        private const int CaptureFps = 60;

        // 120 frames at 60fps is two seconds of game time, which is long enough
        // to hold several whole casts and short enough that a batchmode run
        // does not notice.
        private const int MeasuredFrames = 120;

        // Point 4 of the plan's warm-up definition: five frames discarded after
        // everything else has happened once.
        private const int DiscardedFrames = 5;

        private FightController _fight;
        private FightBeatPlayer _beats;

        [TearDown]
        public void Restore()
        {
            Time.captureFramerate = 0;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            SpellPerformancePlayer.ClockOverride = null;
        }

        private IEnumerator LoadFight()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_beats, "the Fight scene has no FightBeatPlayer");

            // The bootstrap's own fight is mid-beat and would be casting inside
            // the measurement window.
            _beats.EndFight();
            yield return null;

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = Enumerable.Range(0, FightHudSpec.StageSlotsPerSide)
                .Select(i => new CombatantState("Foe" + i, false, 5000, 10, 8, 4))
                .ToArray();
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var enemyKits = foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name,
                    new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false))
                .ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                enemyKits, new SeededRandom(20260908));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private CombatantState Hero => _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);

        private List<SpellVfxPlayer> Effects =>
            _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Where(p => !_fight.GroundVfxPlayersForTest.Contains(p))
                .ToList();

        private List<CombatantState> Foes =>
            _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();

        // THE SHIPPED PRESENTATIONS, not hand-built copies of them. What this
        // measures has to be what the game plays: the pilot's five layers with
        // both emitters, and the fault plus three plumes it overlaps.
        private static SpellPresentation ShippedVfx(string skillId)
        {
            var definition = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == skillId);
            Assert.IsNotNull(definition, "'" + skillId + "' is not in the content database");

            var vfx = PreviewFight.PreviewPresentationOf(definition.Data);
            Assert.IsTrue(vfx != null && vfx.HasArt, "'" + skillId + "' resolves to a presentation with no art");
            return vfx;
        }

        private CombatBeat WaterAt(CombatantState target) => new CombatBeat
        {
            Actor = Hero,
            Target = target,
            Vfx = ShippedVfx("prismatic_orb"),
        };

        private CombatBeat CinderfaultOn(List<CombatantState> struck) => new CombatBeat
        {
            Actor = Hero,
            Target = struck[0],
            SplashTargets = struck.Skip(1).ToList(),
            Vfx = ShippedVfx("cinderfault"),
        };

        // ---- the measurement ------------------------------------------------------

        [UnityTest]
        public IEnumerator TheTickPathAllocatesNothingAndACastCostsTheFrameTheSameAsIdle()
        {
            yield return LoadFight();

            var module = _fight.PerformancePlayerForTest;
            Time.captureFramerate = CaptureFps;

            // ---- warm-up, all four parts of it --------------------------------
            //
            // 1. one full cast of the heaviest performance begun, ticked to its
            //    last layer's end and released -- which also satisfies
            // 2. FrameSequenceLoader holding a cache entry for every folder both
            //    performances touch (it caches on first probe, and caches an
            //    empty result too, so the first draw of any folder is a one-off
            //    disk cost belonging to no frame under test), and
            // 3. both pool nodes having been activated once.
            var warmUp = _fight.PlaySpellVfxForTest(CinderfaultOn(Foes));
            var warmWater = _fight.PlaySpellVfxForTest(WaterAt(Foes[0]));
            Assert.IsTrue(module.IsLive(warmUp), "the warm-up cast obtained nothing");
            Assert.IsTrue(module.IsLive(warmWater), "the warm-up water cast obtained nothing");

            while (module.IsLive(warmUp) || module.IsLive(warmWater)) yield return null;

            // 4. five further frames, discarded.
            for (int i = 0; i < DiscardedFrames; i++) yield return null;

            // ---- the recorder -------------------------------------------------

            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                // A RECORDER THAT NEVER STARTED WOULD MAKE EVERY ASSERTION
                // BELOW READ ZERO, which is the most convincing possible way to
                // pass a test that measured nothing.
                yield return null;
                Assert.IsTrue(recorder.Valid,
                    "ProfilerRecorder(Memory, \"GC Allocated In Frame\") is not valid in this PlayMode " +
                    "host, so nothing below would be measuring anything. It is a UnityEngine core module " +
                    "counter on Unity 6 and needs no package.");

                var idle = new List<long>(MeasuredFrames);
                for (int frame = 0; frame < MeasuredFrames; frame++)
                {
                    yield return null;
                    idle.Add(recorder.LastValue);
                }

                // ---- zero, on the tick itself ---------------------------------
                //
                // MEASURED WITH THE HEAVY CASE LIVE and with the clock moving,
                // so the delegate is doing the work rather than returning early:
                // a cinderfault's fault and three plumes, a water cast's three
                // sprite layers and two emitters, all mid-flight.
                var cinder = _fight.PlaySpellVfxForTest(CinderfaultOn(Foes));
                var water = _fight.PlaySpellVfxForTest(WaterAt(Foes[0]));
                Assert.IsTrue(module.IsLive(cinder) && module.IsLive(water),
                    "the overlapping pair did not both open, so the tick under test is not the heavy one");

                yield return null;

                float now = Time.time;

                // THE DELEGATE IS RUN ONCE BEFORE IT IS MEASURED. The
                // constraint measures a single invocation, and the first
                // invocation of a fresh delegate pays for JIT of whatever it
                // reaches that has not run yet -- which reads as an allocation
                // the tick does not make. Warming it is not the assertion
                // getting easier: the second call runs exactly the same code.
                TestDelegate tick = () =>
                {
                    now += 1f / CaptureFps;
                    module.Tick(now);
                };

                tick();

                Assert.That(tick,
                    Is.Not.AllocatingGCMemory(),
                    "the module's tick allocated. Every per-frame path in it is written not to: the " +
                    "schedule fills a caller's buffer rather than returning a collection, the emitter is " +
                    "a closed form over its inputs, and every renderer comes from a pool.");

                // ---- a difference of zero, on the whole frame -----------------

                var during = new List<long>(MeasuredFrames);
                for (int frame = 0; frame < MeasuredFrames; frame++)
                {
                    // KEPT ALIVE ACROSS THE WHOLE WINDOW. Both casts are under a
                    // second and the window is two, so without this the second
                    // half of "during" would be another idle run wearing its
                    // name.
                    if (!module.IsLive(cinder)) cinder = _fight.PlaySpellVfxForTest(CinderfaultOn(Foes));
                    if (!module.IsLive(water)) water = _fight.PlaySpellVfxForTest(WaterAt(Foes[0]));

                    yield return null;
                    during.Add(recorder.LastValue);
                }

                module.CancelAll();

                long idleMedian = Median(idle);
                long duringMedian = Median(during);
                long worst = Worst(during, idleMedian);

                Assert.AreEqual(idleMedian, duringMedian,
                    "a frame with two overlapping casts on it allocates more than the same scene idle. " +
                    "idle median " + idleMedian + " bytes, casting median " + duringMedian +
                    " bytes, worst casting frame " + worst + " bytes over the idle median. " +
                    "The pools exist so that difference is nothing.");

                Debug.Log("[SpellAllocation] idle median " + idleMedian + " B/frame, casting median " +
                          duringMedian + " B/frame over " + MeasuredFrames + " frames at " + CaptureFps +
                          "fps; worst casting frame " + worst + " B over idle.");
            }
        }

        // THE PLAIN MELEE CASE (finding review, FightController.SpellVfx.cs
        // PlayContactFx/ContactPresentation): every swing that authors no
        // spell of its own reaches this, so it is the single most-called path
        // in this file. ContactPresentation used to build a fresh
        // List<SpellLayer> plus one or two SpellLayer objects on every call
        // even though there are only ever two possible outputs -- now cached
        // as two static templates and picked between.
        //
        // NOT A LITERAL ZERO, and this says why rather than silently
        // widening the assertion: SpellPerformance.Resolve still fans out a
        // fresh SpellLayerInstance per layer (Box/To/From/pool-member state
        // is per-cast by necessity -- two overlapping blows must not share a
        // renderer) and SpellPerformancePlayer.Begin allocates that cast's
        // own small Members/Seeds/Drops arrays. Both are the same
        // architecture every OTHER cast in the game already pays, measured
        // and accepted by the test above (its own "zero" is on the TICK, not
        // on beginning a cast) -- so this asks the same question that test's
        // second half does: does a frame that plays the house's own contact
        // fx cost any more than the same scene idle.
        [UnityTest]
        public IEnumerator APlainMeleeBlowsContactFxCostsNoMoreThanIdle()
        {
            yield return LoadFight();

            var module = _fight.PerformancePlayerForTest;
            Time.captureFramerate = CaptureFps;

            var beat = new CombatBeat
            {
                Actor = Hero, Target = Foes[0], Amount = 5, DamageType = DamageType.Physical,
            };

            // Warm-up: one full call so FrameSequenceLoader has cached the
            // contact sheets' folders (a cold folder probe is a one-off disk
            // cost belonging to no frame under test) and the pool nodes have
            // been activated once.
            _fight.PlayContactFxForTest(beat);

            // A generous, fixed wait rather than polling FreeEffectRenderers
            // back to its starting count: the contact arc/burst pair is well
            // under a second (ContactCues.SlashSeconds/BurstSeconds), and 30
            // frames at 60fps is half a second of real time on top of that.
            for (int i = 0; i < 30; i++) yield return null;

            for (int i = 0; i < DiscardedFrames; i++) yield return null;

            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                yield return null;
                Assert.IsTrue(recorder.Valid,
                    "ProfilerRecorder(Memory, \"GC Allocated In Frame\") is not valid in this PlayMode host.");

                var idle = new List<long>(MeasuredFrames);
                for (int frame = 0; frame < MeasuredFrames; frame++)
                {
                    yield return null;
                    idle.Add(recorder.LastValue);
                }

                // RE-TRIGGERED WHEN THE EFFECTS BAND IS FULLY FREE AGAIN, not
                // every single frame. A real melee beat opens one contact fx
                // and lets it run its own ~0.2-0.3s life before the next
                // blow can land; firing a fresh cast on every frame of a
                // 60fps window is a scenario no beat pacer ever produces and
                // would measure a stress case this fix was never meant to
                // cover.
                int idleFreeEffectRenderers = module.FreeEffectRenderers;
                var during = new List<long>(MeasuredFrames);
                for (int frame = 0; frame < MeasuredFrames; frame++)
                {
                    if (module.FreeEffectRenderers == idleFreeEffectRenderers)
                    {
                        _fight.PlayContactFxForTest(beat);
                    }

                    yield return null;
                    during.Add(recorder.LastValue);
                }

                module.CancelAll();

                long idleMedian = Median(idle);
                long duringMedian = Median(during);
                long worst = Worst(during, idleMedian);

                Assert.AreEqual(idleMedian, duringMedian,
                    "a frame playing the house's own contact fx allocates more than the same scene idle. " +
                    "idle median " + idleMedian + " bytes, contact-fx median " + duringMedian +
                    " bytes, worst frame " + worst + " bytes over the idle median. The cached templates " +
                    "and the pools exist so that difference is nothing.");

                Debug.Log("[SpellAllocation] contact fx: idle median " + idleMedian + " B/frame, during " +
                          "median " + duringMedian + " B/frame over " + MeasuredFrames + " frames at " +
                          CaptureFps + "fps; worst frame " + worst + " B over idle.");
            }
        }

        private static long Median(List<long> samples)
        {
            var sorted = samples.OrderBy(v => v).ToList();
            return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
        }

        private static long Worst(List<long> samples, long baseline)
        {
            long worst = 0;
            foreach (long sample in samples)
            {
                if (sample - baseline > worst) worst = sample - baseline;
            }

            return worst;
        }
    }
}
