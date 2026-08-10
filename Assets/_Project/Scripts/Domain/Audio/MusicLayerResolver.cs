using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Audio
{
    // Validates music_layers.json. Same collected-not-first-only reporting as
    // TalentEntryResolver and every other resolver here, for the same reason:
    // a hand-edited file should say everything wrong with it in one pass
    // rather than making the author fix and rebuild once per typo.
    //
    // The rules worth knowing before authoring a set:
    //
    //   `stems` order is the mix order and is authoritative; a tier's numbers
    //   are indices into it. Reordering `stems` silently re-points every tier,
    //   so it is the one field to change carefully.
    //
    //   All four tiers must be declared. See MusicIntensity for why a missing
    //   one is refused rather than inherited.
    //
    //   Nothing here hard-codes nine stems. Five works, twelve works.
    public static class MusicLayerResolver
    {
        private static readonly MusicIntensity[] AllTiers =
            (MusicIntensity[])Enum.GetValues(typeof(MusicIntensity));

        // Unauthored `quantiseBars` means one bar — responsive, and the
        // reading a composer who did not think about phrasing would expect.
        private const int DefaultQuantiseBars = 1;

        public static bool TryResolve(RawMusicLayerFile raw, out MusicLayerLibrary library, out List<string> errors)
        {
            errors = new List<string>();
            library = MusicLayerLibrary.Empty;

            if (raw == null)
            {
                return true; // no manifest at all is a supported state, not an error
            }

            var sets = new List<MusicLayerSet>();
            var rawSets = raw.sets ?? Array.Empty<RawMusicSet>();
            for (int i = 0; i < rawSets.Length; i++)
            {
                if (TryResolveSet(rawSets[i], i, out var set, errors))
                {
                    sets.Add(set);
                }
            }

            foreach (string duplicate in sets.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate music set id '{duplicate}' — every id must be unique.");
            }

            var byId = new HashSet<string>(sets.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
            var floors = ResolveFloors(raw, byId, errors);

            string fallback = (raw.fallbackSet ?? "").Trim();
            string hub = (raw.hub ?? "").Trim();

            if (sets.Count > 0)
            {
                // Required once anything exists. "The first set" would be an
                // arbitrary answer that silently changes the moment someone
                // reorders the file, and the fallback is what an unmapped
                // floor lands on — it should be a decision, not a side effect
                // of line order.
                if (string.IsNullOrEmpty(fallback))
                {
                    errors.Add("fallbackSet is required once any set exists — an unmapped floor has to land on real music.");
                }
                else if (!byId.Contains(fallback))
                {
                    errors.Add($"fallbackSet '{fallback}' is not a set in this manifest.");
                }

                if (!string.IsNullOrEmpty(hub) && !byId.Contains(hub))
                {
                    errors.Add($"hub '{hub}' is not a set in this manifest.");
                }
            }

            if (errors.Count > 0)
            {
                library = MusicLayerLibrary.Empty;
                return false;
            }

            library = new MusicLayerLibrary(sets, floors, hub, fallback);
            return true;
        }

        private static Dictionary<int, string> ResolveFloors(RawMusicLayerFile raw, HashSet<string> byId, List<string> errors)
        {
            var floors = new Dictionary<int, string>();
            foreach (var entry in raw.floors ?? Array.Empty<RawMusicFloor>())
            {
                if (entry == null)
                {
                    continue;
                }

                string setId = (entry.set ?? "").Trim();

                if (entry.floor < 1)
                {
                    errors.Add($"Floor {entry.floor} is not a real floor — floors start at 1.");
                    continue;
                }

                if (floors.ContainsKey(entry.floor))
                {
                    errors.Add($"Floor {entry.floor} is mapped twice; only one of the two would ever play.");
                    continue;
                }

                // A floor pointing at a set that does not exist is a TYPO, and
                // gets an error — unlike a floor with no mapping at all, which
                // is the legitimate "no music written for floor 7 yet" case
                // and quietly falls back. The difference matters: one is a
                // mistake, the other is a schedule.
                if (string.IsNullOrEmpty(setId) || !byId.Contains(setId))
                {
                    errors.Add($"Floor {entry.floor} maps to '{setId}', which is not a set in this manifest. " +
                               "Remove the line to fall back, or fix the id.");
                    continue;
                }

                floors[entry.floor] = setId;
            }

            return floors;
        }

        private static bool TryResolveSet(RawMusicSet raw, int index, out MusicLayerSet set, List<string> errors)
        {
            set = null;
            string label = raw == null || string.IsNullOrWhiteSpace(raw.id)
                ? $"music_layers.json set #{index + 1}"
                : $"music set '{raw.id}'";

            if (raw == null)
            {
                errors.Add($"{label} is empty.");
                return false;
            }

            int before = errors.Count;

            if (string.IsNullOrWhiteSpace(raw.id))
            {
                errors.Add($"{label}: id is required.");
            }

            if (string.IsNullOrWhiteSpace(raw.folder))
            {
                errors.Add($"{label}: folder is required — it is what stem paths are built from.");
            }

            if (raw.bpm <= 0)
            {
                errors.Add($"{label}: bpm must be positive (got {raw.bpm}); without it there is no bar to land a transition on.");
            }

            if (raw.beatsPerBar <= 0)
            {
                errors.Add($"{label}: beatsPerBar must be positive (got {raw.beatsPerBar}).");
            }

            if (raw.gain <= 0f)
            {
                errors.Add($"{label}: gain must be positive (got {raw.gain}) — 0 is silence, and silence should be expressed by not authoring the set.");
            }

            var stems = ResolveStems(raw, label, errors);
            var active = ResolveTiers(raw, label, stems.Count, errors);

            if (errors.Count > before)
            {
                return false;
            }

            set = new MusicLayerSet(
                raw.id.Trim(),
                raw.folder.Trim().TrimEnd('/'),
                raw.bpm,
                raw.beatsPerBar,
                raw.quantiseBars > 0 ? raw.quantiseBars : DefaultQuantiseBars,
                raw.gain,
                (raw.stingPath ?? "").Trim(),
                stems,
                active);
            return true;
        }

        private static List<string> ResolveStems(RawMusicSet raw, string label, List<string> errors)
        {
            var stems = new List<string>();
            foreach (string stem in raw.stems ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(stem))
                {
                    errors.Add($"{label}: a stem name is blank. Every entry in `stems` is a real file in the set's folder.");
                    continue;
                }

                stems.Add(stem.Trim());
            }

            if (stems.Count == 0)
            {
                errors.Add($"{label}: no stems. A set with nothing in it plays nothing.");
            }

            foreach (string duplicate in stems.GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() > 1).Select(g => g.Key))
            {
                // Two entries naming one file means two AudioSources playing
                // it in perfect phase, which is a 6 dB boost on that layer and
                // nothing else. Silent, and exactly the kind of thing that
                // gets blamed on the mix.
                errors.Add($"{label}: stem '{duplicate}' is listed twice — that plays one file at double volume, not two layers.");
            }

            return stems;
        }

        private static bool[][] ResolveTiers(RawMusicSet raw, string label, int stemCount, List<string> errors)
        {
            var active = new bool[AllTiers.Length][];
            for (int i = 0; i < active.Length; i++)
            {
                active[i] = new bool[Math.Max(0, stemCount)];
            }

            var seen = new HashSet<MusicIntensity>();

            foreach (var tier in raw.tiers ?? Array.Empty<RawMusicTier>())
            {
                if (tier == null || string.IsNullOrWhiteSpace(tier.tier))
                {
                    errors.Add($"{label}: a tier entry has no name.");
                    continue;
                }

                if (!Enum.TryParse<MusicIntensity>(tier.tier.Trim(), ignoreCase: true, out var parsed))
                {
                    errors.Add($"{label}: '{tier.tier}' is not an intensity tier. Valid options: " +
                               $"{string.Join(", ", AllTiers.Select(t => t.ToString().ToLowerInvariant()))}.");
                    continue;
                }

                if (!seen.Add(parsed))
                {
                    errors.Add($"{label}: tier '{parsed}' is declared twice.");
                    continue;
                }

                var indices = tier.stems ?? Array.Empty<int>();
                if (indices.Length == 0)
                {
                    errors.Add($"{label}: tier '{parsed}' has no stems, so that whole tier would be silent. " +
                               "Silence is not an intensity; give it at least one layer.");
                    continue;
                }

                foreach (int stemIndex in indices)
                {
                    if (stemIndex < 0 || stemIndex >= stemCount)
                    {
                        errors.Add($"{label}: tier '{parsed}' names stem {stemIndex}, but this set has " +
                                   $"{stemCount} stem(s) (0-{Math.Max(0, stemCount - 1)}).");
                        continue;
                    }

                    active[(int)parsed][stemIndex] = true;
                }
            }

            foreach (var tier in AllTiers)
            {
                if (!seen.Contains(tier))
                {
                    errors.Add($"{label}: tier '{tier.ToString().ToLowerInvariant()}' is missing. A set declares all " +
                               $"{AllTiers.Length} — an omitted tier would silently play the wrong mix rather than fail.");
                }
            }

            return active;
        }
    }
}
