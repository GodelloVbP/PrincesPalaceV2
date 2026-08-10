using System;

namespace PrincesPalace.Domain.Audio
{
    // music_layers.json, exactly as typed. Same Raw*/Resolved* split as every
    // other content type here: this shape exists to be deserialised, the
    // Resolved one to be used, and MusicLayerResolver is the only thing that
    // crosses between them.
    //
    // TWO DEVIATIONS FROM THE HANDOFF'S EXAMPLE JSON, both forced by
    // JsonUtility and both worth stating where an author will read them.
    //
    // The handoff writes `tiers` and `floors` as OBJECTS keyed by name
    // ("ambient": [0,1] and "1": "floor_1"). JsonUtility cannot deserialise a
    // dictionary at all — not `Dictionary<K,V>`, not an arbitrary-keyed object
    // — so both become ARRAYS of little records here. That is also the shape
    // every other content file in this project already uses, so the diff a
    // composer reads looks like talents.json rather than like a special case.
    //
    // The design goal survives intact: adding a floor is still "drop nine
    // files in a folder, copy a `sets` block, add one line to `floors`".
    [Serializable]
    public class RawMusicLayerFile
    {
        public RawMusicSet[] sets = Array.Empty<RawMusicSet>();
        public RawMusicFloor[] floors = Array.Empty<RawMusicFloor>();

        // Which set the Hub and the main menu play. Empty means "use
        // fallbackSet", which is the sane default and the reason the field is
        // optional rather than required.
        public string hub = "";

        // The set anything unmapped falls back to. Required once any set
        // exists — a floor the manifest forgot must land on real music rather
        // than on silence, and "the first set" would be an arbitrary answer
        // that changes when someone reorders the file.
        public string fallbackSet = "";
    }

    [Serializable]
    public class RawMusicSet
    {
        public string id = "";

        // Resources-relative folder, extensionless paths built as
        // folder + "/" + stem — the same convention every other audio path in
        // this project uses (see Resources/Audio/README.md).
        public string folder = "";

        // Needed to find a bar boundary, which is the whole reason a layer
        // change sounds composed rather than reactive. See MusicClock.
        public int bpm;
        public int beatsPerBar;

        // How many bars a transition waits for. 1 is responsive; 4 or 8 is
        // more musical and can leave up to ~20s of lag at 96 BPM. Authored
        // per set rather than decided globally, because the right answer
        // depends on the song.
        public int quantiseBars;

        // ONE gain for the WHOLE set, deliberately. Stems are quiet and loud
        // on purpose — normalising each one individually flattens the
        // arrangement the composer wrote. See MusicLayerSet.Gain.
        public float gain = 1f;

        // A one-shot fired over the top when this set first reaches its Boss
        // tier. Optional, and the answer to the handoff's open question about
        // where the boss intro goes: a sting keeps the drama without the
        // scheduling complexity of an intro every stem has to queue behind.
        public string stingPath = "";

        // MIX ORDER, and authoritative. Tier arrays are indices into this.
        public string[] stems = Array.Empty<string>();

        public RawMusicTier[] tiers = Array.Empty<RawMusicTier>();
    }

    [Serializable]
    public class RawMusicTier
    {
        // A MusicIntensity member name, matched case-insensitively.
        public string tier = "";

        // Indices into the set's own `stems` array.
        public int[] stems = Array.Empty<int>();
    }

    [Serializable]
    public class RawMusicFloor
    {
        public int floor;
        public string set = "";
    }
}
