using NUnit.Framework;
using PrincesPalace.Domain.Rig;

namespace PrincesPalace.Domain.Tests
{
    // The rig twin of FrameHoldCurveTests/LoopCycleTests: the sampling
    // arithmetic pinned with no scene, no prefab and no rig at all -- a
    // RigStanceClip is just data, and RigSampler.Sample is a pure function
    // over it.
    public class RigSamplerTests
    {
        private static RigStanceClip Clip(bool loops, float duration, params (string bone, (float t, float deg)[] keys)[] tracks)
        {
            var built = new RigBoneTrack[tracks.Length];
            for (int i = 0; i < tracks.Length; i++)
            {
                var keys = tracks[i].keys;
                var frames = new RigKeyframe[keys.Length];
                for (int k = 0; k < keys.Length; k++) frames[k] = new RigKeyframe(keys[k].t, keys[k].deg);
                built[i] = new RigBoneTrack(tracks[i].bone, frames);
            }

            return new RigStanceClip(duration, 0f, 0f, loops, built);
        }

        [Test]
        public void ASingleKeyframeHoldsItsRotationForTheWholeClip()
        {
            var clip = Clip(false, 1f, ("head", new[] { (0f, 12f) }));

            Assert.AreEqual(12f, RigSampler.Sample(clip, 0.5f)[0].RotationDegrees, 0.0001f,
                "a track with one keyframe has nothing to interpolate toward, so every sample must read that one value");
        }

        [Test]
        public void BetweenTwoKeyframesTheRotationEasesWithSmoothstep()
        {
            var clip = Clip(false, 1f, ("head", new[] { (0f, 0f), (1f, 10f) }));

            Assert.AreEqual(5f, RigSampler.Sample(clip, 0.5f)[0].RotationDegrees, 0.0001f,
                "smoothstep is symmetric about its own midpoint, so exactly halfway between a 0deg and a 10deg " +
                "keyframe should still read 5deg -- this pin alone can't tell smoothstep from linear");
        }

        [Test]
        public void OffCentreTheEaseIsNotLinear()
        {
            var clip = Clip(false, 1f, ("head", new[] { (0f, 0f), (1f, 10f) }));

            // smoothstep(0.25) = 0.25^2 * (3 - 2*0.25) = 0.15625, not the
            // linear 0.25 -- this is the pin that actually distinguishes the
            // curve, and the reason a keyframe's velocity approaches zero
            // rather than snapping (see TheIdleClipHasNoMechanicalCorner).
            Assert.AreEqual(1.5625f, RigSampler.Sample(clip, 0.25f)[0].RotationDegrees, 0.0001f,
                "a quarter of the way from 0deg to 10deg should ease to 1.5625deg under smoothstep");
        }

        [Test]
        public void BeforeTheFirstKeyframeTheRotationClampsRatherThanExtrapolates()
        {
            var clip = Clip(false, 1f, ("head", new[] { (0.4f, 20f), (1f, 0f) }));

            Assert.AreEqual(20f, RigSampler.Sample(clip, 0f)[0].RotationDegrees, 0.0001f,
                "a clock before the first keyframe should hold that keyframe's pose, not extrapolate backward past it");
        }

        [Test]
        public void PastTheClipsEndTheRotationHoldsTheLastKeyframe()
        {
            var clip = Clip(false, 1f, ("head", new[] { (0f, 0f), (0.6f, 30f) }));

            Assert.AreEqual(30f, RigSampler.Sample(clip, 4f)[0].RotationDegrees, 0.0001f,
                "a clock past the last authored keyframe should hold that pose, the same clamp StanceAnimation.FrameAt takes for a stale frame index");
        }

        [Test]
        public void ALoopingClipWrapsItsClockRatherThanClamping()
        {
            var clip = Clip(true, 2f, ("head", new[] { (0f, 0f), (1f, 10f), (2f, 0f) }));

            // 2.5s into a 2s loop is the same moment as 0.5s in -- halfway
            // from 0deg to 10deg.
            Assert.AreEqual(5f, RigSampler.Sample(clip, 2.5f)[0].RotationDegrees, 0.0001f,
                "a looping clip should wrap its clock modulo the duration rather than clamp at the end");
        }

        [Test]
        public void ANonLoopingClipDoesNotWrap()
        {
            var clip = Clip(false, 2f, ("head", new[] { (0f, 0f), (2f, 40f) }));

            Assert.AreEqual(40f, RigSampler.Sample(clip, 2.5f)[0].RotationDegrees, 0.0001f,
                "a one-shot clip sampled past its own duration must hold its last pose, not wrap back to the start");
        }

        [Test]
        public void AnEmptyClipSamplesToNoBones()
        {
            Assert.AreEqual(0, RigSampler.Sample(RigStanceClip.Empty, 0.5f).Length,
                "an empty clip has nothing authored, so it should report no bone poses at all rather than zeros for everything");
        }

        [Test]
        public void ImpactAndSoundAreClampedToTheClipsOwnDuration()
        {
            var clip = new RigStanceClip(durationSeconds: 0.5f, impactAt: 0.9f, soundAt: -1f, loops: false,
                tracks: System.Array.Empty<RigBoneTrack>());

            Assert.AreEqual(0.5f, clip.ImpactAt, 0.0001f,
                "an impact authored past the clip's own duration cannot be honoured -- the blow has to land inside the stance it belongs to");
            Assert.AreEqual(0f, clip.SoundAt, 0.0001f,
                "a negative sound cue is not a real moment in the clip; it should clamp to the start rather than go negative");
        }

        [Test]
        public void TranslationEasesWithTheSameSmoothstepCurveAsRotation()
        {
            var track = new RigBoneTrack("body", new[]
            {
                new RigKeyframe(0f, 0f, 0f, 0f),
                new RigKeyframe(1f, 0f, 10f, -6f),
            });
            var clip = new RigStanceClip(1f, 0f, 0f, false, new[] { track });

            var pose = RigSampler.Sample(clip, 0.25f)[0];
            Assert.AreEqual(1.5625f, pose.DxPixels, 0.0001f,
                "dx should ease along the same smoothstep curve as rotation -- one interpolation, three channels");
            Assert.AreEqual(-0.9375f, pose.DyPixels, 0.0001f,
                "dy eases the same way, including sign -- 0.15625 of the way from 0 to -6");
        }

        [Test]
        public void ATrackAuthoredWithNoTranslationStaysAtZero()
        {
            var clip = Clip(false, 1f, ("head", new[] { (0f, 0f), (1f, 10f) }));

            var pose = RigSampler.Sample(clip, 0.5f)[0];
            Assert.AreEqual(0f, pose.DxPixels, 0.0001f,
                "a track built from (t, deg) tuples alone should read dx as zero, not garbage -- every clip authored " +
                "before translation existed still parses as rotation-only");
            Assert.AreEqual(0f, pose.DyPixels, 0.0001f);
        }

        [Test]
        public void MultipleBonesEachSampleIndependently()
        {
            var clip = Clip(false, 1f,
                ("head", new[] { (0f, 0f), (1f, 20f) }),
                ("tail", new[] { (0f, 0f), (1f, -30f) }));

            var poses = RigSampler.Sample(clip, 1f);

            Assert.AreEqual(2, poses.Length);
            Assert.AreEqual(20f, System.Array.Find(poses, p => p.BoneName == "head").RotationDegrees, 0.0001f);
            Assert.AreEqual(-30f, System.Array.Find(poses, p => p.BoneName == "tail").RotationDegrees, 0.0001f,
                "one bone's track must not leak into another's -- a shared clock sampled at the same moment should still answer per bone");
        }
    }
}
