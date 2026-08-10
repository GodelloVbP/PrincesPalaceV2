using PrincesPalace.Domain.Stage;
using UnityEngine;

namespace PrincesPalace
{
    // Loads Resources/StanceManifest.json once and hands out the resolved
    // manifest. The parsing and defaulting rules all live in Domain
    // (StanceManifest); this is only the Unity half -- find the file, read it,
    // cache it, and provide the test seam.
    //
    // Cached statically for the same reason StanceAnimationLibrary caches:
    // RefreshUi repaints once per animation frame, and re-parsing a JSON file
    // at that rate would be absurd. Cached even on FAILURE, so a missing file
    // costs one failed probe rather than one per repaint.
    public static class StanceManifestLoader
    {
        public const string ResourcePath = "StanceManifest";

        private static StanceManifest _cached;
        private static bool _loaded;

        public static StanceManifest Manifest
        {
            get
            {
                if (!_loaded)
                {
                    _cached = Load();
                    _loaded = true;
                }

                return _cached;
            }
        }

        // Same shape as ContentDatabase.Reset and AudioLevels' seam: lets a
        // test install a manifest, and lets the suite clear the static between
        // scene loads so one test's override cannot leak into the next.
        public static void Override(StanceManifest manifest)
        {
            _cached = manifest;
            _loaded = true;
        }

        public static void Reset()
        {
            _cached = null;
            _loaded = false;
        }

        private static StanceManifest Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                // Not a throw: an empty manifest resolves every actor to the
                // canvas-bottom default, which is the stage's pre-manifest
                // behaviour. StanceManifestValidationTests is what makes the
                // absence loud, rather than a crash at runtime.
                Debug.LogWarning($"No stance manifest at Resources/{ResourcePath} -- every actor will fall back to " +
                                 "standing on its own canvas bottom, which is wrong for any art with padding below the feet.");
                return new StanceManifest(null);
            }

            var raw = JsonUtility.FromJson<RawStanceManifest>(asset.text);
            return new StanceManifest(raw);
        }
    }
}
