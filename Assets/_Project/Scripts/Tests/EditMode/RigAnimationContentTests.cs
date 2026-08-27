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
