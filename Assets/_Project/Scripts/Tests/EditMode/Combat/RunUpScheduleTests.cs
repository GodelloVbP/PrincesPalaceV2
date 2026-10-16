using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The run-up from the killing blow to the Reckoning's wipe
    // (docs/PLAN_REWARD_SCREENS.md C0), on literals. FightController plays
    // the schedule back; the PlayMode side only checks that it does.
    public class RunUpScheduleTests
    {
        // The death fade's authored hold and fade (StageDeathFade).
        private const float Hold = 0.35f;
        private const float Fade = 0.6f;

        private const float Tolerance = 0.0005f;

        // Pace is BeatSpeedMultiplier x the preset's multiplier (Display / 1.5):
        // 0.5x -> 1/3, 1x -> 2/3, 1.5x -> 1, 2x -> 4/3.
        private static readonly (float display, float pace)[] Presets =
        {
            (0.5f, 1f / 3f), (1f, 2f / 3f), (1.5f, 1f), (2f, 4f / 3f),
        };

        // ---- the stop ---------------------------------------------------------

        [Test]
        public void TheFinalBlowStopsLongerTheBiggerTheFight()
        {
            Assert.AreEqual(0.22f, HitStop.FinalBlowSeconds(EncounterClass.Normal), Tolerance);
            Assert.AreEqual(0.26f, HitStop.FinalBlowSeconds(EncounterClass.Elite), Tolerance);
            Assert.AreEqual(0.34f, HitStop.FinalBlowSeconds(EncounterClass.Boss), Tolerance);
        }

        [Test]
        public void TheFinalBlowStopsLongerThanAnyOrdinaryBlowCan()
        {
            Assert.Greater(HitStop.FinalBlowSeconds(EncounterClass.Normal), HitStop.MaxSeconds);
        }

        // ---- the clock --------------------------------------------------------

        [Test]
        public void ANormalKillRunsStopHoldAndFadeAtPaceOne()
        {
            var run = RunUpSchedule.For(EncounterClass.Normal, 1f, 1f, Hold, Fade);

            Assert.AreEqual(0.22f, run.StopEnd, Tolerance);
            Assert.AreEqual(0.57f, run.StingAt, Tolerance, "the sting lands when the fade's hold ends");
            Assert.AreEqual(0.77f, run.VoiceAt, Tolerance, "the voices follow it by 0.2 s");
            Assert.AreEqual(0.57f, run.WashStart, Tolerance);
            Assert.AreEqual(1.17f, run.WipeAt, Tolerance, "stop 0.22 + hold 0.35 + fade 0.6");
            Assert.IsTrue(run.HasFade);
            Assert.IsFalse(run.Skipped);
        }

        [Test]
        public void AnEliteAndABossStopLongerAndSoWipeLater()
        {
            Assert.AreEqual(1.21f, RunUpSchedule.For(EncounterClass.Elite, 1f, 1f, Hold, Fade).WipeAt, Tolerance);
            Assert.AreEqual(1.29f, RunUpSchedule.For(EncounterClass.Boss, 1f, 1f, Hold, Fade).WipeAt, Tolerance);
        }

        [Test]
        public void TheSurvivorsLiftOneAfterAnotherFromTheEndOfTheStop()
        {
            var run = RunUpSchedule.For(EncounterClass.Normal, 1f, 1f, Hold, Fade);

            Assert.AreEqual(0.22f, run.LiftAt(0), Tolerance);
            Assert.AreEqual(0.30f, run.LiftAt(1), Tolerance);
            Assert.AreEqual(0.38f, run.LiftAt(2), Tolerance);
            Assert.AreEqual(0.25f, run.LiftSecondsWall, Tolerance);
            Assert.AreEqual(0.4f, run.WashSecondsWall, Tolerance);
        }

        // ---- the cap ----------------------------------------------------------

        [Test]
        public void TheCapIsOnePointThreeAndHoldsForEveryClassAtEveryPreset()
        {
            Assert.AreEqual(1.3f, RunUpSchedule.CapSeconds, Tolerance);

            foreach (var (display, pace) in Presets)
            {
                foreach (EncounterClass fightClass in System.Enum.GetValues(typeof(EncounterClass)))
                {
                    var run = RunUpSchedule.For(fightClass, pace, display, Hold, Fade);

                    Assert.LessOrEqual(run.WipeAt, RunUpSchedule.CapSeconds + Tolerance, $"{fightClass} at {display}x");
                    Assert.LessOrEqual(run.StingAt, run.WipeAt + Tolerance, $"the sting never lands after the wipe, {fightClass} at {display}x");
                }
            }
        }

        [Test]
        public void ASlowFadeIsCutByTheCapAndTheStingStillLandsBeforeIt()
        {
            // 1x: pace 2/3, so stop 0.33, hold 0.525, fade 0.9 -- 1.755 s uncapped.
            var run = RunUpSchedule.For(EncounterClass.Normal, 2f / 3f, 1f, Hold, Fade);

            Assert.AreEqual(0.33f, run.StopEnd, Tolerance);
            Assert.AreEqual(0.855f, run.StingAt, Tolerance);
            Assert.AreEqual(1.3f, run.WipeAt, Tolerance);
        }

        [Test]
        public void AtHalfSpeedABossStopAndHoldOverrunTheCapAndTheStingIsClampedToTheWipe()
        {
            // 0.5x: pace 1/3. Stop 1.02, hold 1.05: the sting would land at 2.07.
            var run = RunUpSchedule.For(EncounterClass.Boss, 1f / 3f, 0.5f, Hold, Fade);

            Assert.AreEqual(1.02f, run.StopEnd, Tolerance);
            Assert.AreEqual(1.3f, run.WipeAt, Tolerance);
            Assert.AreEqual(1.3f, run.StingAt, Tolerance);
        }

        [Test]
        public void AFastPresetFinishesInsideTheCapOnItsOwn()
        {
            // 2x: pace 4/3. Stop 0.165, hold 0.2625, fade 0.45.
            var run = RunUpSchedule.For(EncounterClass.Normal, 4f / 3f, 2f, Hold, Fade);

            Assert.AreEqual(0.8775f, run.WipeAt, Tolerance);
        }

        // ---- what the run-up adds follows min(1, Default / preset) -------------

        [Test]
        public void TheAddedDurationsSpeedUpAtFastPresetsAndNeverSlowDown()
        {
            Assert.AreEqual(1f, RunUpSchedule.TimeScale(0.5f), Tolerance);
            Assert.AreEqual(1f, RunUpSchedule.TimeScale(1f), Tolerance);
            Assert.AreEqual(2f / 3f, RunUpSchedule.TimeScale(1.5f), Tolerance);
            Assert.AreEqual(0.5f, RunUpSchedule.TimeScale(2f), Tolerance);

            var half = RunUpSchedule.For(EncounterClass.Normal, 1f / 3f, 0.5f, Hold, Fade);
            Assert.AreEqual(0.25f, half.LiftSecondsWall, Tolerance, "0.5x does not slow the lift");

            var fast = RunUpSchedule.For(EncounterClass.Normal, 4f / 3f, 2f, Hold, Fade);
            Assert.AreEqual(0.125f, fast.LiftSecondsWall, Tolerance);
            Assert.AreEqual(0.04f, fast.LiftStaggerWall, Tolerance);
            Assert.AreEqual(0.2f, fast.WashSecondsWall, Tolerance);
            Assert.AreEqual(0.1f, fast.VoiceAt - fast.StingAt, Tolerance);
        }

        // ---- nothing to wait for ----------------------------------------------

        [Test]
        public void ANoFadeKillWipesWhenTheStopEndsAndStingsThen()
        {
            var run = RunUpSchedule.For(EncounterClass.Normal, 1f, 1f, 0f, 0f);

            Assert.IsFalse(run.HasFade);
            Assert.AreEqual(0.22f, run.WipeAt, Tolerance);
            Assert.AreEqual(0.22f, run.StingAt, Tolerance);
        }

        [Test]
        public void AFightWithNoFinalBlowRunsFromZero()
        {
            var run = RunUpSchedule.WithoutStop(1f, 1f, Hold, Fade);

            Assert.AreEqual(0f, run.StopEnd, Tolerance);
            Assert.AreEqual(0.35f, run.StingAt, Tolerance);
            Assert.AreEqual(0.95f, run.WipeAt, Tolerance);
        }

        [Test]
        public void ALeaderFallHasWaitedItsBodiesOutAndOnlyStingsAndWipes()
        {
            var run = RunUpSchedule.Settled(1f);

            Assert.AreEqual(0f, run.WipeAt, Tolerance);
            Assert.AreEqual(0f, run.StingAt, Tolerance);
            Assert.AreEqual(0.2f, run.VoiceAt, Tolerance);
            Assert.IsTrue(run.WipeDue(0f, fadeRunning: false));
        }

        // ---- when the wipe is due ---------------------------------------------

        [Test]
        public void TheWipeWaitsForTheFadeOrTheCapWhicheverIsFirst()
        {
            var run = RunUpSchedule.For(EncounterClass.Normal, 1f, 1f, Hold, Fade);

            Assert.IsFalse(run.WipeDue(0.1f, fadeRunning: false), "still inside the stop");
            Assert.IsFalse(run.WipeDue(0.5f, fadeRunning: true));
            Assert.IsTrue(run.WipeDue(0.6f, fadeRunning: false), "the fade finished early");
            Assert.IsTrue(run.WipeDue(1.18f, fadeRunning: true));

            var slow = RunUpSchedule.For(EncounterClass.Normal, 2f / 3f, 1f, Hold, Fade);
            Assert.IsFalse(slow.WipeDue(1.29f, fadeRunning: true));
            Assert.IsTrue(slow.WipeDue(1.3f, fadeRunning: true), "a fade still running at the cap finishes under the gloom");
        }

        // ---- a press -----------------------------------------------------------

        [Test]
        public void APressJumpsToTheWipeAndTheStingHasAlreadyLanded()
        {
            var run = RunUpSchedule.For(EncounterClass.Normal, 1f, 1f, Hold, Fade);

            var early = run.SkippedAt(0.4f);
            Assert.IsTrue(early.Skipped);
            Assert.AreEqual(0.4f, early.WipeAt, Tolerance);
            Assert.AreEqual(0.4f, early.StingAt, Tolerance, "the sting plays now rather than being dropped");
            Assert.AreEqual(0.6f, early.VoiceAt, Tolerance);
            Assert.IsTrue(early.WipeDue(0.4f, fadeRunning: true));

            var late = run.SkippedAt(5f);
            Assert.AreEqual(1.17f, late.WipeAt, Tolerance, "a press after the wipe is due changes nothing");
            Assert.AreEqual(0.57f, late.StingAt, Tolerance, "the sting keeps its time");
        }

        // ---- EndsFight: exactly one beat ---------------------------------------

        private static (FightSession session, CombatantState foe) Fight(
            int foeHealth, bool isElite = false, bool isBoss = false)
        {
            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Rat", false, foeHealth, 10, 8, 9);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null);
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("rat", "Rat", new StatBlock(), 20, 10, false,
                    DamageType.Physical, DamageType.Physical, 0), isElite);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(2), isBossFight: isBoss, isEliteFight: isElite)
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, foe);
        }

        private static List<CombatBeat> PlayOut(FightSession session, CombatantState foe)
        {
            var beats = new List<CombatBeat>();
            for (int swing = 0; swing < 60 && !session.IsOver; swing++)
            {
                session.ExecuteAttack(foe);
                beats.AddRange(session.DrainBeats());
            }

            Assert.IsTrue(session.IsOver && session.PlayerWon, "fixture: the hero wins");
            return beats;
        }

        [Test]
        public void TheLastBeatOfAWonFightEndsItAndNoOtherBeatDoes()
        {
            var (session, foe) = Fight(foeHealth: 90);

            var beats = PlayOut(session, foe);

            Assert.Greater(beats.Count, 1, "fixture: the fight took more than one beat");
            Assert.AreEqual(1, beats.Count(b => b.EndsFight));
            Assert.IsTrue(beats[beats.Count - 1].EndsFight, "and it is the last one");
            Assert.AreEqual(EncounterClass.Normal, beats[beats.Count - 1].FightClass);
        }

        [Test]
        public void TheFlagCarriesTheFightsClass()
        {
            var (elite, eliteFoe) = Fight(1, isElite: true);
            Assert.AreEqual(EncounterClass.Elite, PlayOut(elite, eliteFoe).Single(b => b.EndsFight).FightClass);

            var (boss, bossFoe) = Fight(1, isBoss: true);
            Assert.AreEqual(EncounterClass.Boss, PlayOut(boss, bossFoe).Single(b => b.EndsFight).FightClass);
        }

        [Test]
        public void AFightStillRunningHasNoBeatThatEndsIt()
        {
            var (session, foe) = Fight(foeHealth: 5000);

            session.ExecuteAttack(foe);

            Assert.IsFalse(session.IsOver);
            Assert.IsFalse(session.DrainBeats().Any(b => b.EndsFight));
        }
    }
}
