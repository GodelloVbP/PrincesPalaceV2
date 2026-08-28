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
        // Samples every idle track's RotationAt through the REAL RigSampler
        // math at 60Hz (not a reimplementation -- a second copy of this
        // arithmetic is a drift risk the moment the interpolation changes)
        // and pins the largest single-frame change in angular VELOCITY
        // (not position) under a literal threshold. A piecewise-linear
        // track's velocity is constant within a segment and jumps only at
        // a keyframe, so this catches exactly a corner and nothing else.
        //
        // 40deg/s, not tight: today's linear idle clip already reaches
        // ~25deg/s at its own keyframes (a real, if small, jump) and this
        // guards against a WORSE regression, not zero. It is expected to
        // tighten considerably once the sampler moves off linear
        // interpolation -- a smooth curve's velocity approaches zero at
        // its own keyframes by construction, and this threshold should
        // shrink to match whatever that curve actually produces rather
        // than sit here as a number nobody revisits.
        [Test]
        public void TheIdleClipHasNoMechanicalCorner()
        {
            const float MaxVelocityJumpDegPerSecond = 40f;
            const float SampleHz = 60f;

            var idle = _clips["idle"];
            int steps = Mathf.Max(2, Mathf.RoundToInt(idle.DurationSeconds * SampleHz));

            foreach (var track in idle.Tracks)
            {
                float previousVelocity = float.NaN;
                float previousAngle = track.RotationAt(0f);

                for (int i = 1; i <= steps; i++)
                {
                    float t = idle.DurationSeconds * i / steps;
                    float angle = track.RotationAt(t);
                    float velocity = (angle - previousAngle) * SampleHz;

                    if (!float.IsNaN(previousVelocity))
                    {
                        float jump = Mathf.Abs(velocity - previousVelocity);
                        Assert.LessOrEqual(jump, MaxVelocityJumpDegPerSecond,
                            $"idle/{track.BoneName} changes angular velocity by {jump:F1}deg/s in one 60Hz step " +
                            $"near t={t:F3}s -- that reads as a mechanical tick, not a breath");
                    }

                    previousVelocity = velocity;
                    previousAngle = angle;
                }
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
