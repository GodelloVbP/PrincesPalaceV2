using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Audio
{
    // One floor's song, as N simultaneous stems and four intensity mixes over
    // them.
    //
    // Validated and immutable — MusicLayerResolver is the only thing that
    // builds one, so anything holding a MusicLayerSet already knows its tier
    // indices are in range, its stem names are distinct, and its tempo is a
    // real number. That is the same guarantee ResolvedTalent and ResolvedSkill
    // give their callers, and it is why nothing downstream re-checks.
    //
    // NINE STEMS IS NOT BAKED IN ANYWHERE. It is the expected count and the
    // number the pilot floor uses; a set with five or twelve has to work, so
    // everything here reads Stems.Count.
    public sealed class MusicLayerSet
    {
        public readonly string Id;

        // Resources-relative, no trailing slash.
        public readonly string Folder;

        public readonly int Bpm;
        public readonly int BeatsPerBar;

        // How many bars a layer change waits for. At least 1.
        public readonly int QuantiseBars;

        // ONE multiplier for the WHOLE set, applied uniformly to every stem.
        //
        // This is the single most destructive thing to get wrong, so it is
        // stated on the type rather than only in the manifest.
        // AudioLevels.GainFor normalises each clip independently, which is
        // correct for unrelated one-shots and actively wrong for stems: a
        // quiet pad and a loud lead are quiet and loud ON PURPOSE, and
        // levelling them individually flattens the arrangement. Measure the
        // set by rendering all stems at unity and correcting THAT, then apply
        // the same number to all of them.
        public readonly float Gain;

        // Fired once when this set first reaches Boss, or empty. See
        // RawMusicSet.stingPath.
        public readonly string StingPath;

        // Mix order, authoritative.
        public readonly IReadOnlyList<string> Stems;

        // One bool per stem per tier: _active[(int)tier][stemIndex]. A dense
        // mask rather than the authored index list, because the question asked
        // at runtime — "is stem 4 on right now" — is answered in one array
        // lookup instead of a scan, and it is asked once per stem per fade
        // frame.
        private readonly bool[][] _active;

        public MusicLayerSet(string id, string folder, int bpm, int beatsPerBar, int quantiseBars,
            float gain, string stingPath, IReadOnlyList<string> stems, bool[][] active)
        {
            Id = id;
            Folder = folder;
            Bpm = bpm;
            BeatsPerBar = beatsPerBar;
            QuantiseBars = Math.Max(1, quantiseBars);
            Gain = gain;
            StingPath = stingPath ?? "";
            Stems = stems;
            _active = active;
        }

        public int StemCount => Stems.Count;

        public bool HasSting => !string.IsNullOrEmpty(StingPath);

        // The Resources path of one stem, extensionless — the shape
        // Resources.Load wants and the shape Resources/Audio/README.md
        // documents.
        public string StemPath(int index)
        {
            return index < 0 || index >= Stems.Count ? null : Folder + "/" + Stems[index];
        }

        // Whether this stem is part of that tier's mix. Out-of-range answers
        // false rather than throwing: a caller iterating its own source array
        // may legitimately hold more sources than this set has stems (voices
        // are pooled and reused across sets of different sizes), and a
        // spurious layer being silent is the correct outcome there.
        public bool IsActive(MusicIntensity tier, int stemIndex)
        {
            int t = (int)tier;
            return t >= 0 && t < _active.Length
                   && stemIndex >= 0 && stemIndex < _active[t].Length
                   && _active[t][stemIndex];
        }

        // How long one bar lasts, in seconds. The unit MusicClock quantises
        // against.
        public double BarSeconds => Bpm <= 0 || BeatsPerBar <= 0 ? 0d : 60d / Bpm * BeatsPerBar;

        // How long one QUANTISATION WINDOW lasts — a bar, or the phrase the
        // set authored instead.
        public double QuantiseSeconds => BarSeconds * QuantiseBars;
    }
}
