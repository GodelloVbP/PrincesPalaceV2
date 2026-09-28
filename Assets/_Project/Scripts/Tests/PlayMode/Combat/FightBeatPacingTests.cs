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

        // MEASURED ON THE BEAT'S OWN CLOCK, at a pinned frame length. The
        // charge's wait is a WaitForSeconds -- scaled game time -- so that is
        // the clock this reads (Time.timeAsDouble), with Time.captureDeltaTime
        // fixing every frame at SampleSeconds so the number cannot depend on
        // how fast the machine produces frames.
        //
        // It used to read Time.realtimeSinceStartup, and that failed ~1 run in
        // 9 under a loaded sharded gate (0.170s against the 0.175s floor).
        // Not a slow wait: WaitForSeconds counts from the Time.time of the
        // frame it started in, which is stamped at the TOP of that frame, and
        // the real-time start was read partway through it, after however much
        // of the frame the machine had already spent. Under load that gap
        // grew past the slop and the real interval came out SHORTER than the
        // game-time wait it was measuring.
        private const float SampleSeconds = 0.01f;

        // Half a sample: WaitForSeconds resolves on the first frame whose
        // accumulated time reaches its target, and float accumulation can put
        // that a hair under 0.18. A regression that skipped the wait (firing
        // on the frame the beat opened) reads 0 and still fails by 0.175s.
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
        // every other test in this file: the floor is 0.18s of beat time, and
        // scaling it down to milliseconds would put it under a single frame's
        // own length, at which point any measured elapsed time -- however
        // short the true wait -- clears the assertion and the test stops
        // being able to catch the regression it exists for. At SampleSeconds
        // it is 18 frames long.
        [UnityTest]
        public IEnumerator AChargeWaitsOutItsOwnTravelBeforeTheImpactInstant()
        {
            var player = NewPlayer();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            // From the NEXT frame on; the frame Play runs in keeps whatever
            // length it already had, which is fine -- the wait is stamped from
            // that frame's start and every frame after it is pinned.
            Time.captureDeltaTime = SampleSeconds;

            var beat = ChargeBeat();
            double fireTime = -1.0;
            player.WireContactFxForTest(b => { if (fireTime < 0.0) fireTime = Time.timeAsDouble; });

            double start = Time.timeAsDouble;
            bool finished = false;
            player.Play(new List<CombatBeat> { beat }, () => finished = true);

            // A hang guard in frames, not a measurement: five seconds of beat
            // time at SampleSeconds.
            const int FrameBudget = 500;
            int frames = 0;
            while (fireTime < 0.0 && frames++ < FrameBudget) yield return null;

            Assert.Greater(fireTime, 0.0,
                "the charge never fired its contact effect, so the timing below means nothing");
            Assert.GreaterOrEqual(fireTime - start, ChargeWindupFloorSeconds - TimingSlop,
                "a charge's impact instant fired before its own floor travel time had elapsed -- " +
                "the target flashed and recoiled before the charger crossed the stage");

            while (!finished && frames++ < FrameBudget) yield return null;
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

        // ---- how long a struck figure reels (owner 2026-09-19) ------------------
        //
        // "Reeling happens way too fast: it should happen 4x as slow." The
        // reel is three legs: StageActorAnimator's 0.055s push out, the dwell
        // at full extent, and the spring back. The push out does NOT move --
        // it is what puts the body where the flash and the damage number
        // already are, and stretching it slides the impact frame off the one
        // those two land on. The other two are each four times what they
        // were, because "too fast" is a complaint about velocity and the
        // spring back is the leg the eye reads as the flinch easing off.
        //
        // PINNED AS LITERALS, for the reason SwingSeconds above is: derived
        // from the beat budget and the slowdown factor, these would agree
        // with whatever those became, and what the owner asked for was two
        // lengths.

        private const float ReelPushOutSeconds = 0.055f;   // StageActorAnimator.LungeSeconds
        private const float ReelDwellSeconds = 0.81f;      // was 0.2025
        private const float ReelReturnSeconds = 0.64f;     // was 0.16

        [Test]
        public void TheReelDwellsForFourTimesAsLongAsItUsedTo()
        {
            Assert.AreEqual(ReelDwellSeconds, FightBeatPlayer.RecoilDwellSeconds, 0.001f);
        }

        [Test]
        public void TheReelSpringsBackFourTimesAsSlowlyAsItUsedTo()
        {
            // THE LEG THE FIRST ATTEMPT AT THIS LEFT ALONE, which is why that
            // attempt made the reel four times longer without making anything
            // about it four times slower: all of its extra time was a figure
            // standing still.
            Assert.AreEqual(ReelReturnSeconds, FightBeatPlayer.RecoilReturnSeconds, 0.001f);
        }

        [Test]
        public void BothReelLegsAreStatedInBeatsSoThePresetAndTheBudgetCarryThem()
        {
            // The dwell was 0.45 of a beat and the spring back 0.3556 of one;
            // four times each is what these two factors are. Stated in beats
            // rather than in seconds so a battle-speed preset scales them
            // exactly as it scales the beat they belong to -- the property a
            // flat 1.45f seconds constant quietly gave up.
            Assert.AreEqual(1.8f, FightBeatPlayer.RecoilDwellBeats, 0.001f);
            Assert.AreEqual(1.4222f, FightBeatPlayer.RecoilReturnBeats, 0.001f);

            Assert.AreEqual(FightBeatPlayer.RecoilDwellSeconds,
                FightBeatPlayer.BeatHoldSeconds * FightBeatPlayer.RecoilDwellBeats, 0.0001f);
            Assert.AreEqual(FightBeatPlayer.RecoilReturnSeconds,
                FightBeatPlayer.BeatHoldSeconds * FightBeatPlayer.RecoilReturnBeats, 0.0001f);
        }

        [Test]
        public void TheWholeReelIsFourTimesTheOneTheOwnerCalledTooFast()
        {
            // The number the owner actually asked about: the reel end to end.
            // The push out is deliberately not part of the multiplication, so
            // this is 4x the old 0.4175s LESS the 3x the push out did not get
            // -- 1.505 rather than 1.67, and the 0.165s difference is the
            // impact frame staying exactly where it was.
            float whole = ReelPushOutSeconds + FightBeatPlayer.RecoilDwellSeconds
                          + FightBeatPlayer.RecoilReturnSeconds;

            Assert.AreEqual(1.505f, whole, 0.001f);
        }

        [Test]
        public void TheReelDeliberatelyOutlivesTheBeatThatCausedIt()
        {
            // NOT A DEFECT, AND WRITTEN DOWN SO IT CANNOT BE MISTAKEN FOR ONE.
            // Every other timing in this file is sized to fit inside the beat
            // budget; the reel is four times one that already nearly filled
            // it, so a figure struck on one beat is still coming home during
            // the next. That is what "4x as slow" costs and it is visible on
            // purpose.
            //
            // WHAT STOPS IT BEING A BUG is FightBeatPlayer's own settle gate:
            // a beat whose actor or target is still reeling waits for it
            // before measuring a stand-off against their marks. So the reel
            // overlaps beats it has nothing to do with, and never the one
            // that is about to land on it.
            Assert.Greater(FightBeatPlayer.RecoilDwellSeconds + FightBeatPlayer.RecoilReturnSeconds,
                FightBeatPlayer.BeatHoldSeconds + FightBeatPlayer.BeatGapSeconds,
                "the reel now fits inside its own beat; either the beat grew or the reel was " +
                "shortened, and the comment above is stale either way");
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
            Time.captureDeltaTime = 0f;
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

            // No scene here, so FightBootstrap
            // never runs to install anything -- but this file's own tests
            // (AChargeWaitsOutItsOwnTravelBeforeTheImpactInstant) locally set
            // BeatSpeedMultiplier back to 1 and measure elapsed beat time
            // against it, and PlayerSpeedSource is a process-wide static a
            // leaked value from elsewhere in the same batch could still
            // leave non-default. Pinned here, at the one place every test in
            // this file starts from.
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            return player;
        }

        private static IReadOnlyList<CombatBeat> OneBeat()
        {
            return new List<CombatBeat> { new CombatBeat() };
        }

    }
}
