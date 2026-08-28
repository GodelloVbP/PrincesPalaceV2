using NUnit.Framework;
using PrincesPalace.Domain.Rig;
using UnityEngine;

namespace PrincesPalace.Domain.Tests
{
    // Validates the RAT'S OWN COMMITTED animations.json, not a fixture --
    // the rig twin of the frame-sheet content-pin tests (EnemyContentPinTests
    // et al.): a clip with no impact, or one that never wraps its loop, is a
    // content bug that only shows up as a monster standing still or an
    // attack with no punch, and nothing else in the suite would catch it.
    //
    // Reads the raw JSON directly via Resources.Load<TextAsset> +
    // RigAnimationResolver rather than through Core.Rig.RigManifestLoader --
    // both are Domain-safe, so this stays EditMode; RigManifestLoader itself
    // lives in Core and can't be reached from here (see StancePerformanceTests'
    // own header for why that split exists).
    public class RigAnimationContentTests
    {
        private System.Collections.Generic.Dictionary<string, RigStanceClip> _clips;

        [OneTimeSetUp]
        public void LoadTheRatsRealContent()
        {
            var asset = Resources.Load<TextAsset>("Rigs/Enemies/rat/animations");
            Assert.IsNotNull(asset, "Resources/Rigs/Enemies/rat/animations.json is missing -- the rig pilot has no timing at all");

            var raw = JsonUtility.FromJson<RawRigManifest>(asset.text);
            _clips = RigAnimationResolver.Resolve(raw);
        }

        [TestCase("idle")]
        [TestCase("attack")]
        [TestCase("hurt")]
        [TestCase("defeated")]
        public void EveryRequiredStanceHasAClip(string stance)
        {
            Assert.IsTrue(_clips.TryGetValue(stance, out var clip) && !clip.IsEmpty,
                $"the rat's animations.json has no usable '{stance}' clip -- a combatant idle/attacking/hurt/dying with " +
                "no clip silently plays no motion at all rather than failing loudly");
        }

        [Test]
        public void EveryClipsImpactLandsStrictlyInsideItsOwnDuration()
        {
            foreach (var pair in _clips)
            {
                var clip = pair.Value;
                if (clip.IsEmpty) continue;

                Assert.GreaterOrEqual(clip.ImpactAt, 0f, $"'{pair.Key}' authors a negative impactAt");
                Assert.LessOrEqual(clip.ImpactAt, clip.DurationSeconds,
                    $"'{pair.Key}' authors an impactAt past its own durationSeconds -- the blow would never land");
            }
        }

        // A looping clip that ends on a different value than it started
        // wraps with a pop -- RigSampler.Wrap just mods the clock, it does
        // not know or care whether frame 0 and the last frame agree, so
        // this has to be checked as content rather than relied on as an
        // engine guarantee. Every channel: a track can wrap cleanly on
        // rotation and still pop on dy if only one was checked.
        [Test]
        public void LoopingClipTracksStartAndEndOnTheSameValues()
        {
            foreach (var pair in _clips)
            {
                var clip = pair.Value;
                if (clip.IsEmpty || !clip.Loops) continue;

                foreach (var track in clip.Tracks)
                {
                    Assert.AreEqual(track.RotationAt(0f), track.RotationAt(clip.DurationSeconds), 0.01f,
                        $"'{pair.Key}'/{track.BoneName} rotates to a different pose at t=0 than at its own duration -- a looping clip wraps between these two moments every cycle, so a mismatch is a visible pop");
                    Assert.AreEqual(track.DxAt(0f), track.DxAt(clip.DurationSeconds), 0.01f,
                        $"'{pair.Key}'/{track.BoneName} dx mismatches between t=0 and its own duration");
                    Assert.AreEqual(track.DyAt(0f), track.DyAt(clip.DurationSeconds), 0.01f,
                        $"'{pair.Key}'/{track.BoneName} dy mismatches between t=0 and its own duration");
                }
            }
        }

        [Test]
        public void TheIdleClipLoops()
        {
            Assert.IsTrue(_clips["idle"].Loops,
                "the idle clip does not loop -- StepRigIdlePose samples it as a wrap-around cycle regardless of this " +
                "flag, so an unset loop authors a clip that plays once and then holds its last (non-rest) pose forever");
        }

        [TestCase("attack")]
        [TestCase("hurt")]
        [TestCase("defeated")]
        public void OneShotStancesDoNotLoop(string stance)
        {
            Assert.IsFalse(_clips[stance].Loops,
                $"'{stance}' loops -- a beat plays it once and then calls ResetToRest, so a looping one-shot has no " +
                "way to ever be SEEN looping and the flag is very likely a copy-paste mistake from idle");
        }

        [Test]
        public void TheAttackClipsImpactIsARealMomentNotADegenerateEdge()
        {
            var attack = _clips["attack"];

            Assert.Greater(attack.ImpactAt, 0f,
                "the attack's impact sits at the very start of the clip -- there is no wind-up left for Charge to " +
                "time its travel tween against");
            Assert.Less(attack.ImpactAt, attack.DurationSeconds,
                "the attack's impact sits at the very end of the clip -- there is no follow-through left to play");
        }

        // THE MECHANICAL-CORNER REGRESSION GUARD.
        //
        // Scoped to idle only, deliberately -- attack and hurt WANT a sharp
        // corner at their impact keyframe (that snap is what reads as a
        // blow landing; today's attack head track jumps 403deg/s there and
        // that is correct, not a bug). Idle is the opposite case: a
        // breathing loop that changes VELOCITY abruptly at a keyframe reads
        // as a mechanical tick rather than a breath, which is the exact
        // "drunk sway" defect this content shipped with once already.
        //
        // Samples every idle track's RotationAt/DxAt/DyAt through the REAL
        // RigSampler math at 60Hz (not a reimplementation -- a second copy
        // of this arithmetic is a drift risk the moment the interpolation
        // changes) and pins the largest single-frame change in VELOCITY
        // (not position) on each channel under a literal threshold. Under
        // smoothstep a piecewise curve's velocity is continuous everywhere
        // and zero exactly at every keyframe, so a real jump here means the
        // curve got a genuine corner, not that a keyframe merely exists.
        //
        // Thresholds computed empirically (see the plan file / commit
        // message) against the shipped staggered idle clip's own peak
        // per-channel jump under this exact sampling, then given roughly a
        // 2-4x margin: 6deg/s for rotation (observed peak ~3.0deg/s, on the
        // tail's larger-amplitude track) and 2px/s for translation
        // (observed peak ~0.45px/s, on the body's breathing bob). Tight by
        // design -- this is the regression guard for the exact "drunk sway"
        // defect this content shipped with once already, and the whole
        // point of moving off linear interpolation was for these numbers to
        // shrink to match what a smooth curve actually produces.
        [Test]
        public void TheIdleClipHasNoMechanicalCorner()
        {
            const float MaxRotationVelocityJumpDegPerSecond = 6f;
            const float MaxTranslationVelocityJumpPxPerSecond = 2f;
            const float SampleHz = 60f;

            var idle = _clips["idle"];
            int steps = Mathf.Max(2, Mathf.RoundToInt(idle.DurationSeconds * SampleHz));

            foreach (var track in idle.Tracks)
            {
                CheckNoVelocityCorner(track.BoneName, "rotation", track.RotationAt, idle.DurationSeconds, steps, SampleHz,
                    MaxRotationVelocityJumpDegPerSecond, "deg/s");
                CheckNoVelocityCorner(track.BoneName, "dx", track.DxAt, idle.DurationSeconds, steps, SampleHz,
                    MaxTranslationVelocityJumpPxPerSecond, "px/s");
                CheckNoVelocityCorner(track.BoneName, "dy", track.DyAt, idle.DurationSeconds, steps, SampleHz,
                    MaxTranslationVelocityJumpPxPerSecond, "px/s");
            }
        }

        private static void CheckNoVelocityCorner(string boneName, string channel, System.Func<float, float> valueAt,
            float durationSeconds, int steps, float sampleHz, float maxJump, string unit)
        {
            float previousVelocity = float.NaN;
            float previousValue = valueAt(0f);

            for (int i = 1; i <= steps; i++)
            {
                float t = durationSeconds * i / steps;
                float value = valueAt(t);
                float velocity = (value - previousValue) * sampleHz;

                if (!float.IsNaN(previousVelocity))
                {
                    float jump = Mathf.Abs(velocity - previousVelocity);
                    Assert.LessOrEqual(jump, maxJump,
                        $"idle/{boneName}'s {channel} changes velocity by {jump:F2}{unit} in one 60Hz step near " +
                        $"t={t:F3}s -- that reads as a mechanical tick, not a breath");
                }

                previousVelocity = velocity;
                previousValue = value;
            }
        }

        [Test]
        public void EveryTrackedBoneNameIsARealRatBone()
        {
            // The exact set rig.json authors (see RigPrefabBuilder's own
            // header) -- a track for any other name silently animates
            // nothing, because RigActor.ApplyPose looks bones up by name
            // and a miss is a no-op rather than a thrown exception.
            var knownBones = new System.Collections.Generic.HashSet<string>
            {
                "body", "far_hindleg", "far_foreleg", "tail", "near_hindleg", "near_foreleg", "head",
            };

            foreach (var pair in _clips)
            {
                foreach (var track in pair.Value.Tracks)
                {
                    Assert.IsTrue(knownBones.Contains(track.BoneName),
                        $"'{pair.Key}' authors a track for bone '{track.BoneName}', which is not one of the rat rig's " +
                        "own bones -- this track will silently animate nothing");
                }
            }
        }
    }
}
