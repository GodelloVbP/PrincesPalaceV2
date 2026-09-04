using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stage;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // The IStancePlayback seam FightBeatPlayer now drives instead of
    // StanceStepper/FrameHoldCurve directly. Lives in PlayMode rather than
    // EditMode because FrameStancePlayback is a Core type
    // (StanceAnimation itself references UnityEngine.Sprite), which the
    // EditMode assembly's own asmdef does not reference at all.
    //
    // THE POINT OF THIS FILE: the refactor that introduced this seam must
    // not have changed what a frame-sheet actor's timing actually is. Every
    // number here is pinned as a LITERAL, hand-derived against
    // FrameHoldCurve's own published algorithm -- recomputing the formula
    // under test would make these tautologies (see CLAUDE.md's standing
    // rule on formula tests).
    public class StancePerformanceTests
    {
        private static StanceAnimation TwoFrameAnimation(float secondsPerFrame = 0.1f, int impactFrame = 1,
            bool returnsToStart = false) =>
            new StanceAnimation(new Sprite[] { null, null }, secondsPerFrame, impactFrame, impactFrame,
                returnsToStart: returnsToStart);

        [Test]
        public void WindupSecondsMatchesTheSheetsOwnUnevenPace()
        {
            // frameCount 2, impactFrame 1, secondsPerFrame 0.1: the single
            // wind-up frame's raw weight normalises under FrameHoldCurve's
            // own 0.055s floor and clamps to exactly it -- see
            // FrameHoldCurveTests for the floor's own derivation.
            var playback = new FrameStancePlayback(new CombatantState("Test", true, 30, 10, 5, 5), TwoFrameAnimation(), (a, f) => { });

            Assert.AreEqual(FrameHoldCurve.MinimumHoldSeconds, playback.WindupSeconds, 0.0001f,
                "the wind-up for a 2-frame, impact-on-frame-1 stance should be exactly one frame's hold at the floor");
        }

        [Test]
        public void TotalSecondsIsSecondsPerFrameTimesFrameCount()
        {
            var playback = new FrameStancePlayback(new CombatantState("Test", true, 30, 10, 5, 5), TwoFrameAnimation(secondsPerFrame: 0.08f),
                (a, f) => { });

            Assert.AreEqual(0.16f, playback.TotalSeconds, 0.0001f,
                "FightBeatPlayer's SettleAfter depends on this being exactly secondsPerFrame x frameCount, " +
                "or the beat budget is computed against a duration the stance does not actually take");
        }

        [Test]
        public void AFlatOneFrameAnimationHasNoMotion()
        {
            var flat = new StanceAnimation(new Sprite[] { null }, 0.1f, 1, 1);
            var playback = new FrameStancePlayback(new CombatantState("Test", true, 30, 10, 5, 5), flat, (a, f) => { });

            Assert.IsFalse(playback.HasMotion,
                "a single-frame pose has nothing to step through -- Flinch relies on this to skip starting a coroutine for it");
        }

        [Test]
        public void ReturnsToStartIsFalseForAFlatOneFrameAnimation()
        {
            var flat = new StanceAnimation(new Sprite[] { null }, 0.1f, 1, 1, returnsToStart: true);
            var playback = new FrameStancePlayback(new CombatantState("Test", true, 30, 10, 5, 5), flat, (a, f) => { });

            Assert.IsFalse(playback.ReturnsToStart,
                "a one-frame stance has nothing to play back down through, regardless of what the sheet authored");
        }

        [Test]
        public void ReturnsToStartCarriesThroughForAMultiFrameAnimation()
        {
            var playback = new FrameStancePlayback(new CombatantState("Test", true, 30, 10, 5, 5), TwoFrameAnimation(returnsToStart: true),
                (a, f) => { });

            Assert.IsTrue(playback.ReturnsToStart,
                "an authored returns-to-start stance with real frames to release must still ask for its release phase");
        }

        [Test]
        public void ResetToRestSetsFrameZero()
        {
            int? seen = null;
            var actor = new CombatantState("Test", true, 30, 10, 5, 5);
            var playback = new FrameStancePlayback(actor, TwoFrameAnimation(), (a, f) => seen = f);

            playback.ResetToRest();

            Assert.AreEqual(0, seen, "rest is frame 0 -- the same reset every beat always applied");
        }

        // ---- the still-drawing wrapper ------------------------------------
        //
        // What this class exists to do is move the impact instant, so its
        // TIMING is the whole contract. Pinned as a literal for the reason
        // this file's header already gives: derived from the two constants it
        // adds up, this would pass whatever those constants became.

        private static IStancePlayback FlatPlayback() =>
            new FrameStancePlayback(new CombatantState("Test", true, 30, 10, 5, 5),
                new StanceAnimation(new Sprite[] { null }, 0.1f, 1, 1), (a, f) => { });

        [Test]
        public void TheStaticWindupCoversTheAnticipationAndTheLungeTogether()
        {
            var playback = new StaticStancePlayback(FlatPlayback());

            // 0.07s of anticipation plus the 0.055s outbound tween.
            Assert.AreEqual(0.125f, playback.WindupSeconds, 0.0001f,
                "the impact instant is timed off this -- shorten it and the flash, the recoil and " +
                "the damage number fire while the attacker is still crossing, which is the exact " +
                "defect this wrapper exists to fix");
        }

        [Test]
        public void TheStaticStanceSpendsExactlyItsWindupAndNoMore()
        {
            var playback = new StaticStancePlayback(FlatPlayback());

            Assert.AreEqual(0.125f, playback.TotalSeconds, 0.0001f,
                "FightBeatPlayer.SettleAfter budgets the beat against this; reporting anything other " +
                "than what Windup actually waits either lengthens the beat or eats the settle");
            Assert.AreEqual(playback.WindupSeconds, playback.TotalSeconds, 0.0001f,
                "a still has no follow-through, so the whole performance is the wind-up");
        }

        [Test]
        public void TheStaticStancesReleaseAndFollowThroughDoNothing()
        {
            var playback = new StaticStancePlayback(FlatPlayback());

            Assert.IsFalse(playback.ReturnsToStart,
                "there is no second drawing to play back down through");

            // Driven rather than merely inspected: an enumerator that yielded
            // even once would add a frame to every beat, and "empty" is only
            // true if running it ends immediately. No scene and no clock
            // needed for that, which is why this is a plain [Test].
            Assert.IsFalse(playback.FollowThrough().MoveNext(),
                "the follow-through yielded, so the beat is longer than TotalSeconds claims");
            Assert.IsFalse(playback.Release().MoveNext(),
                "the release yielded, and FightBeatPlayer adds a release's time to the beat outright");
        }

        [Test]
        public void TheStaticStanceForwardsTheResetToWhatItWraps()
        {
            int? seen = null;
            var actor = new CombatantState("Test", true, 30, 10, 5, 5);
            var wrapped = new FrameStancePlayback(actor,
                new StanceAnimation(new Sprite[] { null }, 0.1f, 1, 1), (a, f) => seen = f);

            new StaticStancePlayback(wrapped).ResetToRest();

            Assert.AreEqual(0, seen,
                "the wrapper swallowed the between-beats reset, so the real drawing keeps whatever " +
                "pose the last blow left on it");
        }

        [Test]
        public void TheStaticStanceHasNoMotionEvenThoughItTakesTime()
        {
            Assert.IsFalse(new StaticStancePlayback(FlatPlayback()).HasMotion,
                "Flinch skips a victim whose playback has no motion; a wrapper claiming motion would " +
                "start a coroutine that shows nothing for an eighth of a second");
        }
    }
}
