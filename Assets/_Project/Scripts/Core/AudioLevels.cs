using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace
{
    // Per-clip loudness correction, measured offline and applied at playback.
    //
    // The source audio arrived at wildly different levels: 14.6 dB of spread
    // in integrated loudness and 20.8 dB in RMS across the 31 clips, with
    // Shawn's voice lines around -8 dB and the UI sounds near -29 dB. One
    // master volume cannot serve both, so a setting loud enough to hear the
    // menu click made a victory shout painful. Twelve clips also peaked above
    // full scale and were clipping outright.
    //
    // The numbers come from tools/measure_audio_levels.py (ffmpeg, EBU R128
    // for music and RMS for effects -- see that tool for why the metric
    // differs by category) and are committed as a plain table so a change to
    // the mix is a reviewable diff rather than an invisible re-encode.
    //
    // Applied as a MULTIPLIER at play time rather than baked into the files:
    // four music tracks are .ogg and two effects .mp3, so "just normalise the
    // assets" would mean decoding and re-encoding lossy audio -- the same
    // compounding-loss trap slice_actor_sheet.py refuses when it declines to
    // read its own output back in.
    //
    // Graceful on absence, like every other content lookup here: an unknown
    // path, a missing table, or a malformed one all yield 1.0, which is
    // exactly the behaviour before this existed.
    public static class AudioLevels
    {
        // Resources-relative, extensionless -- the same shape every caller
        // already holds (SoundLibrary.PathOf, MusicLibrary.PathOf, and
        // skills.json's sfxPath all address audio this way).
        private const string TablePath = "Audio/audio_levels";

        private static Dictionary<string, float> _gains;

        [Serializable]
        private class Entry
        {
            public string path;
            public float gain = 1f;
        }

        [Serializable]
        private class Table
        {
            public List<Entry> entries = new List<Entry>();
        }

        // The linear multiplier for a clip, or 1.0 if it has no entry.
        public static float GainFor(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return 1f;
            }

            EnsureLoaded();
            return _gains.TryGetValue(Normalise(resourcePath), out float gain) ? gain : 1f;
        }

        // Test seam, and the same shape ContentDatabase.Reset already uses:
        // drops the cache so the next lookup reloads from Resources.
        public static void Reset()
        {
            _gains = null;
        }

        // How many clips the table actually carries. Exposed so a test can
        // fail loudly on an empty table rather than passing vacuously against
        // a lookup that silently answers 1.0 for everything.
        public static int Count
        {
            get
            {
                EnsureLoaded();
                return _gains.Count;
            }
        }

        private static void EnsureLoaded()
        {
            if (_gains != null)
            {
                return;
            }

            _gains = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            var asset = Resources.Load<TextAsset>(TablePath);
            if (asset == null)
            {
                return; // no table shipped -- every clip plays at its authored level
            }

            Table table;
            try
            {
                table = JsonUtility.FromJson<Table>(asset.text);
            }
            catch (Exception)
            {
                // A malformed table must not take the game's audio down with
                // it; unity-normalised playback is a far better failure than
                // an exception on the first button click.
                return;
            }

            if (table?.entries == null)
            {
                return;
            }

            foreach (var entry in table.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.path) || entry.gain <= 0f)
                {
                    continue;
                }

                _gains[Normalise(entry.path)] = entry.gain;
            }
        }

        // The table is keyed relative to Audio/ ("Sfx/button click") while
        // callers hold the full Resources path ("Audio/Sfx/button click").
        // Accepting either keeps every call site free of a prefix dance.
        private static string Normalise(string path)
        {
            string trimmed = path.Replace('\\', '/').Trim();
            const string prefix = "Audio/";
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(prefix.Length);
            }

            return trimmed;
        }
    }
}
