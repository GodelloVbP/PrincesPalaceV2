using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Rig
{
    // Turns a parsed animations.json into the clips RigManifestLoader hands
    // out, keyed by stance name -- the rig twin of how StanceManifest itself
    // resolves RawStanceManifest, split out as its own static function
    // rather than a constructor so RigDefinitionTests-style callers can
    // resolve a hand-built RawRigManifest with no Resources folder at all.
    public static class RigAnimationResolver
    {
        public static Dictionary<string, RigStanceClip> Resolve(RawRigManifest raw)
        {
            var result = new Dictionary<string, RigStanceClip>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawClip in raw?.clips ?? new List<RawRigClip>())
            {
                if (rawClip == null || string.IsNullOrWhiteSpace(rawClip.stance)) continue;

                var tracks = (rawClip.tracks ?? new List<RawRigTrack>())
                    .Where(t => t != null && !string.IsNullOrWhiteSpace(t.bone))
                    .Select(t => new RigBoneTrack(t.bone, (t.keyframes ?? new List<RawRigKeyframe>())
                        .Select(k => new RigKeyframe(k.t, k.deg, k.dx, k.dy))
                        .OrderBy(k => k.TimeSeconds)
                        .ToArray()))
                    .ToArray();

                result[rawClip.stance.Trim()] = new RigStanceClip(
                    rawClip.durationSeconds, rawClip.impactAt, rawClip.soundAt, rawClip.loop, tracks);
            }

            return result;
        }
    }
}
