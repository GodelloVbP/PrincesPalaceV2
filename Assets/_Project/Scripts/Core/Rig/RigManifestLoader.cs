using System.Collections.Generic;
using PrincesPalace.Domain.Rig;
using UnityEngine;

namespace PrincesPalace.Core.Rig
{
    // Loads Resources/Rigs/<folder>/animations.json once per folder and
    // hands out the resolved clip for a stance. Same posture as RigLibrary
    // and StanceManifestLoader: one probe per folder, cached including a
    // miss -- a folder with no animations.json (or no rig at all) resolves
    // every stance to RigStanceClip.Empty rather than retrying every frame.
    public static class RigManifestLoader
    {
        private static readonly Dictionary<string, Dictionary<string, RigStanceClip>> _cache =
            new Dictionary<string, Dictionary<string, RigStanceClip>>();

        public static RigStanceClip ClipFor(string folder, string stance)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(stance)) return RigStanceClip.Empty;

            var clips = ClipsFor(folder);
            return clips.TryGetValue(stance, out var clip) ? clip : RigStanceClip.Empty;
        }

        // Same Override/Reset seam as StanceManifestLoader: lets a test
        // install a folder's clips directly, and lets the suite clear the
        // cache between scenes so one test's fixture cannot leak into the
        // next folder that asks.
        public static void Override(string folder, Dictionary<string, RigStanceClip> clips)
        {
            if (string.IsNullOrWhiteSpace(folder)) return;
            _cache[folder] = clips ?? new Dictionary<string, RigStanceClip>();
        }

        public static void Reset() => _cache.Clear();

        private static Dictionary<string, RigStanceClip> ClipsFor(string folder)
        {
            if (_cache.TryGetValue(folder, out var cached)) return cached;

            var resolved = Load(folder);
            _cache[folder] = resolved;
            return resolved;
        }

        private static Dictionary<string, RigStanceClip> Load(string folder)
        {
            var asset = Resources.Load<TextAsset>("Rigs/" + folder + "/animations");
            if (asset == null) return new Dictionary<string, RigStanceClip>();

            var raw = JsonUtility.FromJson<RawRigManifest>(asset.text);
            return RigAnimationResolver.Resolve(raw);
        }
    }
}
