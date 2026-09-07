using NUnit.Framework;
using PrincesPalace.Domain.Audio;

namespace PrincesPalace.Domain.Tests
{
    // ONE ANSWER TO "IS THIS THE SAME SET ID", on both sides of the seam.
    //
    // MusicLayerResolver validates every reference -- fallbackSet, hub, each
    // floor's set -- against a HashSet built with OrdinalIgnoreCase, while
    // MusicLayerLibrary looks the same id up in a Dictionary built with the
    // default ordinal comparer. A reference that differs only in case
    // therefore passes validation and resolves to null at runtime, which is
    // the one outcome MusicLayerResolver's own header rules out: "a floor
    // pointing at a set that does not exist is a TYPO, and gets an error".
    //
    // It fails silently in the worst direction, too. A mis-cased fallbackSet
    // leaves Fallback, Hub and every ForFloor null, and MusicController reads
    // a null library-lookup as "no layered music authored" and drops to the
    // pre-layering MusicTrack path -- a manifest that validates clean and
    // plays none of the song it declares.
    public class MusicSetIdCaseTests
    {
        private static RawMusicSet Set(string id) => new RawMusicSet
        {
            id = id,
            folder = "Audio/Music/" + id,
            bpm = 96,
            beatsPerBar = 4,
            gain = 1f,
            stems = new[] { "01_pad", "02_bass" },
            tiers = new[]
            {
                new RawMusicTier { tier = "ambient", stems = new[] { 0 } },
                new RawMusicTier { tier = "fight", stems = new[] { 0, 1 } },
                new RawMusicTier { tier = "elite", stems = new[] { 0, 1 } },
                new RawMusicTier { tier = "boss", stems = new[] { 0, 1 } },
            },
        };

        [Test]
        public void AMisCasedFallbackResolvesToTheSetItNames()
        {
            var file = new RawMusicLayerFile
            {
                sets = new[] { Set("floor_1") },
                floors = new RawMusicFloor[0],
                hub = "",
                fallbackSet = "Floor_1",
            };

            Assert.IsTrue(MusicLayerResolver.TryResolve(file, out var library, out var errors),
                string.Join(" ", errors));
            Assert.IsNotNull(library.Fallback,
                "the resolver accepted 'Floor_1' as naming a real set; the library must find the same set");
            Assert.AreEqual("floor_1", library.Fallback.Id);
        }

        [Test]
        public void AMisCasedFloorMappingResolvesToTheSetItNames()
        {
            var file = new RawMusicLayerFile
            {
                sets = new[] { Set("floor_1"), Set("floor_2") },
                floors = new[] { new RawMusicFloor { floor = 2, set = "FLOOR_2" } },
                hub = "",
                fallbackSet = "floor_1",
            };

            Assert.IsTrue(MusicLayerResolver.TryResolve(file, out var library, out var errors),
                string.Join(" ", errors));
            Assert.AreEqual("floor_2", library.ForFloor(2).Id,
                "floor 2 silently fell back to floor_1's music instead of playing its own");
        }

        [Test]
        public void AMisCasedHubResolvesToTheSetItNames()
        {
            var file = new RawMusicLayerFile
            {
                sets = new[] { Set("floor_1"), Set("hub_theme") },
                floors = new RawMusicFloor[0],
                hub = "Hub_Theme",
                fallbackSet = "floor_1",
            };

            Assert.IsTrue(MusicLayerResolver.TryResolve(file, out var library, out var errors),
                string.Join(" ", errors));
            Assert.AreEqual("hub_theme", library.Hub.Id,
                "the hub silently fell through to the fallback set");
        }

        // The other half of one comparer: if references match case-insensitively
        // then two sets whose ids differ only in case are the SAME id, and the
        // duplicate guard has to say so rather than letting one silently win
        // the library's lookup.
        [Test]
        public void TwoSetIdsDifferingOnlyInCaseAreRefusedAsDuplicates()
        {
            var file = new RawMusicLayerFile
            {
                sets = new[] { Set("floor_1"), Set("Floor_1") },
                floors = new RawMusicFloor[0],
                hub = "",
                fallbackSet = "floor_1",
            };

            Assert.IsFalse(MusicLayerResolver.TryResolve(file, out _, out var errors),
                "two sets named the same id in different cases were both accepted");
            StringAssert.Contains("Duplicate music set id", string.Join(" ", errors));
        }
    }
}
