using System.Collections.Generic;

namespace PrincesPalace.Domain.Audio
{
    // Every music set the manifest declares, plus which floor gets which.
    //
    // The whole point of the adaptive-music design is that adding a floor is a
    // data diff, so this type is what the rest of the game asks instead of
    // asking a switch statement. Nothing outside MusicLayerResolver builds
    // one.
    //
    // GRACEFUL ON EVERY LOOKUP, house style. An unmapped floor lands on the
    // fallback set rather than on silence; an empty library answers null to
    // everything and MusicController falls back to the old MusicTrack path.
    // Neither is an error condition — the first is how you ship floor 7
    // before its music exists, and the second is how the game sounds exactly
    // as it did before this system landed.
    public sealed class MusicLayerLibrary
    {
        // A library with nothing in it. What a missing or malformed manifest
        // resolves to, and the state in which MusicController uses the
        // pre-layering code path.
        public static readonly MusicLayerLibrary Empty =
            new MusicLayerLibrary(new List<MusicLayerSet>(), new Dictionary<int, string>(), null, null);

        private readonly Dictionary<string, MusicLayerSet> _byId;
        private readonly Dictionary<int, string> _floors;
        private readonly string _hubId;
        private readonly string _fallbackId;

        public MusicLayerLibrary(IReadOnlyList<MusicLayerSet> sets, Dictionary<int, string> floors,
            string hubId, string fallbackId)
        {
            _byId = new Dictionary<string, MusicLayerSet>();
            Sets = sets;
            foreach (var set in sets)
            {
                _byId[set.Id] = set;
            }

            _floors = floors;
            _hubId = hubId;
            _fallbackId = fallbackId;
        }

        public IReadOnlyList<MusicLayerSet> Sets { get; }

        public bool IsEmpty => Sets.Count == 0;

        public MusicLayerSet Get(string id)
        {
            return id != null && _byId.TryGetValue(id, out var set) ? set : null;
        }

        // The fallback set, or null in an empty library.
        public MusicLayerSet Fallback => Get(_fallbackId);

        // What the Hub and the main menu play. Falls through to the fallback
        // when the manifest does not name one — the handoff leaves "does the
        // hub share floor 1's set" open, and this is what makes the answer a
        // one-line manifest edit rather than a code change.
        public MusicLayerSet Hub => Get(_hubId) ?? Fallback;

        // The set for a run floor, or the fallback for a floor nobody has
        // written music for yet. Null only in an empty library.
        public MusicLayerSet ForFloor(int floor)
        {
            return _floors.TryGetValue(floor, out string id) ? Get(id) ?? Fallback : Fallback;
        }
    }
}
