using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.PlayModeTests
{
    // How long a blow is given to land.
    //
    // THE BUG THIS EXISTS FOR: the settle after a stance was whatever was LEFT
    // of BeatHoldSeconds once the stance had been paid for, and for every
    // animated actor in the game that remainder was negative. Six frames at the
    // manifest's 0.08s is 0.48s against a 0.45s budget, so `remaining > 0f` was
    // never true and an animated blow got no pause at all -- the last frame of
    // the swing ran straight into the return to idle and the next beat.
    //
    // Nothing could see it. "No pause" and "a pause of zero" are the same code
    // path, the beat ordering tests still passed because the ORDER was right,
    // and the only symptom was that the fight felt fast and twitchy.
    //
    // These are plain [Test]s rather than [UnityTest]s: the arithmetic is the
    // thing that was wrong, and it needs no scene to state.
    public class FightBeatPacingTests
    {
        // What every animated stance in the game currently costs. Not read off
        // the manifest on purpose -- pinning the literal is what makes this
        // test notice that the shipped shape changed, rather than moving with
        // it and continuing to pass.
        private const int ShippedFrameCount = 6;
        private const float ShippedSecondsPerFrame = 0.08f;

        [Test]
        public void AnAnimatedBlowIsGivenTimeToLand()
        {
            float stance = ShippedSecondsPerFrame * ShippedFrameCount;

            Assert.Greater(stance, FightBeatPlayer.BeatHoldSeconds,
                "this test's premise has gone: the stance now fits inside the beat budget, so the " +
                "subtraction below cannot go negative and the floor it checks is doing nothing");

            Assert.GreaterOrEqual(FightBeatPlayer.SettleAfter(stance), FightBeatPlayer.MinSettleSeconds,
                "an animated blow gets no pause after it, so it runs straight into the next beat and " +
                "the damage number has no still frame to be read against");
        }

        // The floor must not quietly become the answer for everything: a flat
        // one-frame pose still has most of the beat left over, and shortening
        // that would be fixing the animated actors by rushing the others.
        [Test]
        public void AFlatPoseKeepsTheBeatItAlreadyHad()
        {
            float stance = ShippedSecondsPerFrame * 1;

            Assert.AreEqual(FightBeatPlayer.BeatHoldSeconds - stance,
                FightBeatPlayer.SettleAfter(stance), 0.0001f,
                "a single-frame pose's hold changed; the floor was only supposed to catch the case " +
                "where the stance outran the budget");
        }

        // The curve normalises to exactly secondsPerFrame x frameCount, and the
        // settle arithmetic subtracts exactly that product. If the curve ever
        // stopped preserving the total, the beat budget would be computed
        // against a duration the stance does not actually take.
        [Test]
        public void TheHoldCurveStillSpendsExactlyWhatItWasGiven()
        {
            var holds = FrameHoldCurve.Holds(ShippedFrameCount, impactFrame: 3, ShippedSecondsPerFrame);

            float total = 0f;
            foreach (float h in holds) total += h;

            Assert.AreEqual(ShippedSecondsPerFrame * ShippedFrameCount, total, 0.001f,
                "the frame-hold curve no longer preserves the stance's total duration, so the beat " +
                "budget is being computed against a length the stance does not take");
        }

        // Every drawing has to be on screen long enough to be a drawing. The
        // curve's own floor is 55ms; this is what stops a future pace change
        // from quietly dropping under it.
        [Test]
        public void NoDrawingIsHeldForLessThanTheFloor()
        {
            var holds = FrameHoldCurve.Holds(ShippedFrameCount, impactFrame: 3, ShippedSecondsPerFrame);

            foreach (float h in holds)
            {
                Assert.GreaterOrEqual(h, FrameHoldCurve.MinimumHoldSeconds - 0.0001f,
                    $"a frame is held for {h * 1000f:F0}ms, under the {FrameHoldCurve.MinimumHoldSeconds * 1000f:F0}ms " +
                    "floor - it reads as a flicker between the drawings either side of it");
            }
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
