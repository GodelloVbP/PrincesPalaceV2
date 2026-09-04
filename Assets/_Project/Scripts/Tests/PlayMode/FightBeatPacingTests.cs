using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.PlayModeTests
{
    // How long a blow is given to land.
    //
    // THE BUG THIS EXISTS FOR: the settle after a stance was whatever was LEFT
    // of BeatHoldSeconds once the stance had been paid for, and back when a
    // stance was six drawings at 0.08s that remainder was negative -- so
    // `remaining > 0f` was never true and a blow got no pause at all. The last
    // frame of the swing ran straight into the return to idle and the next
    // beat.
    //
    // Nothing could see it. "No pause" and "a pause of zero" are the same code
    // path, the beat ordering tests still passed because the ORDER was right,
    // and the only symptom was that the fight felt fast and twitchy. The sheets
    // are gone; the same subtraction still runs, and the hit-stop now takes out
    // of it what the frames used to.
    //
    // Most of these are plain [Test]s rather than [UnityTest]s: the arithmetic
    // is the thing that was wrong, and it needs no scene to state.
    public class FightBeatPacingTests
    {
        // Anticipation 0.07 plus the 0.055s outbound tween.
        //
        // PINNED AS A LITERAL rather than added up from the two constants it
        // comes from -- derived, this would pass whatever those constants
        // became, which is the tautology CLAUDE.md's standing rule on formula
        // tests names. The impact instant is timed off exactly this number:
        // shorten it and the flash, the recoil and the damage number fire
        // while the attacker is still crossing, which is the defect StaticSwing
        // exists to fix.
        private const float SwingSeconds = 0.125f;

        [Test]
        public void TheSwingWindupIsTheAnticipationAndTheTravelTogether()
        {
            Assert.AreEqual(SwingSeconds, StaticSwing.WindupSeconds, 0.0001f,
                "the crouch and the cross are one number precisely so the impact instant cannot " +
                "drift apart from the travel it waits on");
        }

        // ---- a charge's impact waits for the charger to arrive (AUDIT.md #59) --

        // THE FLOOR under a plain rush's out-tween, pinned as a literal rather
        // than read off FightBeatPlayer's own private constant -- the private
        // copy is what this test exists to hold honest, so reading it back
        // would make the assertion a tautology (docs/CODE_STANDARDS.md #8).
        private const float ChargeWindupFloorSeconds = 0.18f;

        // MEASUREMENT SLOP, not a loosened floor. WaitForSeconds resolves on
        // the frame where accumulated deltaTime first reaches its target, and
        // that comparison can land inside a millisecond of the target rather
        // than exactly on it -- observed here as low as 0.1795s against a
        // 0.18s wait. Smaller than a single 60fps frame (~16.7ms), so a
        // regression that skipped the wait entirely (firing on frame one)
        // still fails this assertion by two orders of magnitude.
        private const float TimingSlop = 0.005f;

        // THE BUG THIS PINS: a flat-art Charge fired its impact instant (the
        // flash, the recoil, the damage number) on the very frame the beat
        // opened, while its own out-tween took ChargeWindupFloorSeconds to
        // cross the stage -- so the target reacted a beat before the charger
        // arrived. FightBeatPlayer now waits out the charge's own travel time
        // before firing the impact, the same shape StaticSwing already gives
        // a Lunge.
        //
        // RUN AT REAL SPEED (BeatSpeedMultiplier = 1), deliberately unlike
        // every other test in this file: the floor is 0.18s of WALL-CLOCK
        // time, and scaling it down to milliseconds would put it under a
        // single frame's own length, at which point any measured elapsed time
        // -- however short the true wait -- clears the assertion and the test
        // stops being able to catch the regression it exists for.
        [UnityTest]
        public IEnumerator AChargeWaitsOutItsOwnTravelBeforeTheImpactInstant()
        {
            var player = NewPlayer();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            var beat = ChargeBeat();
            float fireTime = -1f;
            player.WireContactFxForTest(b => { if (fireTime < 0f) fireTime = Time.realtimeSinceStartup; });

            float start = Time.realtimeSinceStartup;
            bool finished = false;
            player.Play(new List<CombatBeat> { beat }, () => finished = true);

            float deadline = start + 5f;
            while (fireTime < 0f && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.Greater(fireTime, 0f,
                "the charge never fired its contact effect, so the timing below means nothing");
            Assert.GreaterOrEqual(fireTime - start, ChargeWindupFloorSeconds - TimingSlop,
                "a charge's impact instant fired before its own floor travel time had elapsed -- " +
                "the target flashed and recoiled before the charger crossed the stage");

            while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(finished, "the charge beat never finished");
        }

        private static CombatantState Fighter(string name, bool playerSide) =>
            new CombatantState(name, playerSide, 30, 10, 5, 5);

        private static CombatBeat ChargeBeat()
        {
            var beat = new CombatBeat
            {
                Actor = Fighter("Charger", true),
                Target = Fighter("Victim", false),
                Amount = 7,
                Approach = StageApproach.Charge,
            };

            beat.Stances[beat.Actor] = FightSession.Stances.Attack;
            return beat;
        }

        // ---- what the hit-stop takes out of the beat ---------------------------

        // THE FREEZE IS SUBTRACTED, NOT ADDED. Hit-stop pauses the beat at the
        // moment of contact, and the settle that follows gives back exactly
        // what the pause took -- so a beat costs what it always cost.
        //
        // This is the assertion the whole effect hangs on. A stop that simply
        // appeared would stretch every landed blow by up to a tenth of a
        // second, which is invisible on one beat and puts the log a full
        // exchange ahead of the stage over a round. Same failure mode
        // SettleAfter's own comment records going unnoticed once already.
        [Test]
        public void TheHitStopComesOutOfTheSettleRatherThanLengtheningTheBeat()
        {
            float stance = FightBeatPlayer.StillPoseSeconds;
            float stop = HitStop.MaxSeconds;

            float without = FightBeatPlayer.SettleAfter(stance);
            float with = FightBeatPlayer.SettleAfter(stance + stop);

            Assert.AreEqual(without - stop, with, 0.0001f,
                "the settle did not give back what the freeze took, so every landed blow now runs long");
            Assert.AreEqual(stance + without, stance + stop + with, 0.0001f,
                "and a beat's total length has to be identical with the freeze and without it");
        }

        // THE FLOOR IS STILL LOAD-BEARING, and this is the case that keeps it
        // so. The heaviest blow in the game is a swing: 0.125s of crouch and
        // cross plus HitStop.MaxSeconds leaves 0.145s of a 0.45s beat, which is
        // under MinSettleSeconds -- so the biggest hits are exactly the ones
        // that would have nothing left to read the damage number against.
        [Test]
        public void TheHeaviestSwingStillClampsToTheSettleFloor()
        {
            float bare = FightBeatPlayer.BeatHoldSeconds - (SwingSeconds + HitStop.MaxSeconds);

            Assert.Less(bare, FightBeatPlayer.MinSettleSeconds,
                "this test's premise has gone: the heaviest swing now fits inside the beat budget " +
                "with room to spare, so the floor it checks is doing nothing");

            Assert.AreEqual(FightBeatPlayer.MinSettleSeconds,
                FightBeatPlayer.SettleAfter(SwingSeconds + HitStop.MaxSeconds), 0.0001f,
                "the heaviest swing fell through the settle floor and runs into the next beat");
        }

        // The floor must not quietly become the answer for everything: a still
        // pose that is not a swing still has most of the beat left over, and
        // shortening that would be rushing every cast to fix the swings.
        [Test]
        public void AStillPoseKeepsTheBeatItAlreadyHad()
        {
            float stance = FightBeatPlayer.StillPoseSeconds;

            Assert.AreEqual(FightBeatPlayer.BeatHoldSeconds - stance,
                FightBeatPlayer.SettleAfter(stance), 0.0001f,
                "a still pose's hold changed; the floor was only supposed to catch the case where " +
                "the stance outran the budget");
        }

        // ---- a stopped playback still reports -----------------------------------
        //
        // THE BUG THESE EXIST FOR: FightController sets _isBusy before Play and
        // clears it ONLY in the finished callback, and _isBusy is half of
        // CanAct. Flush stopped the coroutine without ever calling that
        // callback, so any playback that was stopped rather than allowed to
        // finish left the controller busy for the rest of the fight -- every
        // verb disabled, every click ignored, and nothing on screen saying why.
        [UnityTest]
        public IEnumerator AStoppedPlaybackTellsWhoeverWasWaiting()
        {
            var player = NewPlayer();
            bool finished = false;

            player.Play(OneBeat(), () => finished = true);
            yield return null;

            player.Flush();

            Assert.IsTrue(finished,
                "the playback was stopped and nobody was told, so the fight stays busy forever");
        }

        // The other half, and it is what stops the fix above from being worse
        // than the bug: starting a NEW round must not fire the old round's
        // callback, or the controller would clear the busy flag one frame after
        // setting it and let the player act in the middle of the round.
        [UnityTest]
        public IEnumerator StartingANewRoundDoesNotFireTheOldRoundsCallback()
        {
            var player = NewPlayer();
            bool first = false;

            player.Play(OneBeat(), () => first = true);
            yield return null;

            player.Play(OneBeat(), () => { });
            yield return null;

            Assert.IsFalse(first,
                "starting a round fired the previous round's finished callback, which clears the " +
                "busy flag while the new round is still playing");
        }


        // ---- fixture -------------------------------------------------------------

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void CleanUp()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        // A bare player on its own object. No scene needed: these are about the
        // callback contract, not about anything being drawn.
        private FightBeatPlayer NewPlayer()
        {
            var go = new GameObject("BeatPlayerUnderTest");
            _spawned.Add(go);

            var player = go.AddComponent<FightBeatPlayer>();

            // Fast, so a beat's waits do not make the test wait them out.
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            return player;
        }

        private static IReadOnlyList<CombatBeat> OneBeat()
        {
            return new List<CombatBeat> { new CombatBeat() };
        }

    }
}
