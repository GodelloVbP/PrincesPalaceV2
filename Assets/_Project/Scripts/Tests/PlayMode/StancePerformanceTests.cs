using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Rig;
using PrincesPalace.Domain.Stage;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // The IStancePlayback seam FightBeatPlayer now drives instead of
    // StanceStepper/FrameHoldCurve directly. Lives in PlayMode rather than
    // EditMode because FrameStancePlayback/RigStancePlayback are Core types
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

            Assert.AreEqual(0, seen, "the frame-sheet twin of the rig's bind pose is frame 0 -- the same reset every beat always applied");
        }

        // ---- the rig side of the same seam -------------------------------

        private static RigStanceClip Clip(float duration, float impactAt) =>
            new RigStanceClip(duration, impactAt, impactAt, false,
                new[] { new RigBoneTrack("head", new[] { new RigKeyframe(0f, 0f), new RigKeyframe(duration, 10f) } ) });

        [Test]
        public void RigWindupSecondsIsTheClipsOwnImpactAt()
        {
            var playback = new RigStancePlayback(null, Clip(0.5f, 0.32f));

            Assert.AreEqual(0.32f, playback.WindupSeconds, 0.0001f,
                "Charge needs to know the rig's own authored impact moment before either phase plays a single frame");
        }

        [Test]
        public void RigTotalSecondsIsTheClipsOwnDuration()
        {
            var playback = new RigStancePlayback(null, Clip(0.5f, 0.32f));

            Assert.AreEqual(0.5f, playback.TotalSeconds, 0.0001f);
        }

        [Test]
        public void RigStancesNeverReturnToStart()
        {
            var playback = new RigStancePlayback(null, Clip(0.5f, 0.32f));

            Assert.IsFalse(playback.ReturnsToStart,
                "no rig clip authors a release phase during the pilot -- every clip is drawn to already end at rest");
        }

        [Test]
        public void AnEmptyRigClipHasNoMotion()
        {
            var playback = new RigStancePlayback(null, RigStanceClip.Empty);

            Assert.IsFalse(playback.HasMotion,
                "a folder with no animations.json resolves to RigStanceClip.Empty, and Flinch relies on HasMotion to skip it");
        }
    }
}
