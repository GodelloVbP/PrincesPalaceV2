using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace.Core.Rig
{
    // Resolves a spritePath ("Enemies/rat") to its generated skeletal-rig
    // prefab, or null if that actor has no rig yet.
    //
    // Same posture as StanceAnimationLibrary: one Resources.Load probe per
    // folder, cached INCLUDING a miss. Only the rat resolves during the pilot;
    // every other creature's folder correctly and cheaply misses every time,
    // which is what keeps the old frame-swap Image path untouched for them.
    public static class RigLibrary
    {
        private static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        public static GameObject Resolve(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;

            if (_cache.TryGetValue(folder, out var cached)) return cached;

            var prefab = Resources.Load<GameObject>("Rigs/" + folder);
            _cache[folder] = prefab; // caches the null too -- one miss per folder, not one per repaint
            return prefab;
        }
    }
}
