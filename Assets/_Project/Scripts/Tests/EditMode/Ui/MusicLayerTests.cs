using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Audio;

namespace PrincesPalace.Domain.Tests
{
    // When a layer change is allowed to start. Pure arithmetic, and therefore
    // the one part of the adaptive-music system a headless runner can prove
    // outright rather than merely assert does not throw.
    public class MusicClockTests
    {
        // A two-second bar, for arithmetic anyone can check by eye.
        private const double Bar = 2d;

        [Test]
        public void MidBar_WaitsForTheNextOne()
        {
            Assert.AreEqual(4d, MusicClock.NextBoundary(startedAt: 0d, now: 2.5d, windowSeconds: Bar), 1e-9);
        }

        // At or after, not strictly after: a change requested exactly on a
        // downbeat should take THAT downbeat rather than sit out a whole bar
        // waiting for the next one.
        [Test]
        public void ExactlyOnABoundary_TakesThatBoundary()
        {
            Assert.AreEqual(4d, MusicClock.NextBoundary(0d, 4d, Bar), 1e-9);
        }

        [Test]
        public void ANonZeroStart_IsRespected()
        {
            // The set began at 100.5, so its bars fall at 102.5, 104.5, ...
            Assert.AreEqual(104.5d, MusicClock.NextBoundary(100.5d, 103d, Bar), 1e-9);
        }

        // The ScheduleLead window: PlayScheduled has been called but the first
        // sample has not sounded yet. The first boundary is the start itself.
        [Test]
        public void BeforeTheSetHasAudiblyBegun_TheFirstBoundaryIsTheStart()
        {
            Assert.AreEqual(10d, MusicClock.NextBoundary(startedAt: 10d, now: 9.97d, windowSeconds: Bar), 1e-9);
        }

        // A set with no tempo authored means "change immediately" rather than
        // "never change". A silent refusal to ever transition is far harder to
        // notice than an unquantised fade.
        [Test]
        public void WithNoWindow_TheBoundaryIsNow()
        {
            Assert.AreEqual(7.3d, MusicClock.NextBoundary(0d, 7.3d, 0d), 1e-9);
            Assert.AreEqual(7.3d, MusicClock.NextBoundary(0d, 7.3d, -4d), 1e-9);
        }

        [Test]
        public void SecondsUntil_IsNeverNegative()
        {
            Assert.AreEqual(1.5d, MusicClock.SecondsUntilNextBoundary(0d, 2.5d, Bar), 1e-9);
            Assert.AreEqual(0d, MusicClock.SecondsUntilNextBoundary(0d, 7.3d, 0d), 1e-9);
        }

        // Phrase quantisation is the same arithmetic with a bigger window --
        // which is exactly why `quantiseBars` could be a manifest field
        // instead of a second code path.
        [Test]
        public void AFourBarPhrase_IsJustABiggerWindow()
        {
            Assert.AreEqual(8d, MusicClock.NextBoundary(0d, 2.5d, Bar * 4), 1e-9);
        }
    }

    public class MusicLayerSetTests
    {
        private static MusicLayerSet Resolve(RawMusicSet raw)
        {
            var file = new RawMusicLayerFile
            {
                sets = new[] { raw },
                fallbackSet = raw.id,
            };

            Assert.IsTrue(MusicLayerResolver.TryResolve(file, out var library, out var errors),
                string.Join(" ", errors));
            return library.Get(raw.id);
        }

        // Tier masks are built by CLAMPING against the stem count rather than
        // hardcoded, so the same helper serves a one-stem set and a
        // twelve-stem one. That is the point being tested in
        // ASetOfAnySize_Works, and a helper that only worked at four would
        // have quietly made that test a fiction.
        private static RawMusicSet Pilot(int stemCount = 4)
        {
            int[] UpTo(int count) => Enumerable.Range(0, System.Math.Min(count, stemCount)).ToArray();

            return new RawMusicSet
            {
                id = "test_set",
                folder = "Audio/Music/Test",
                bpm = 120,
                beatsPerBar = 4,
                quantiseBars = 1,
                gain = 0.8f,
                stems = Enumerable.Range(0, stemCount).Select(i => $"0{i}_stem").ToArray(),
                tiers = new[]
                {
                    new RawMusicTier { tier = "ambient", stems = UpTo(1) },
                    new RawMusicTier { tier = "fight", stems = UpTo(2) },
                    new RawMusicTier { tier = "elite", stems = UpTo(3) },
                    new RawMusicTier { tier = "boss", stems = UpTo(stemCount) },
                },
            };
        }

        [Test]
        public void AStemPath_IsFolderPlusName_Extensionless()
        {
            Assert.AreEqual("Audio/Music/Test/00_stem", Resolve(Pilot()).StemPath(0));
        }

        [Test]
        public void ATrailingSlashOnTheFolder_DoesNotDoubleUp()
        {
            var raw = Pilot();
            raw.folder = "Audio/Music/Test/";

            Assert.AreEqual("Audio/Music/Test/00_stem", Resolve(raw).StemPath(0));
        }

        [Test]
        public void EachTier_TurnsOnExactlyTheStemsItNamed()
        {
            var set = Resolve(Pilot());

            Assert.IsTrue(set.IsActive(MusicIntensity.Ambient, 0));
            Assert.IsFalse(set.IsActive(MusicIntensity.Ambient, 1));

            Assert.IsTrue(set.IsActive(MusicIntensity.Fight, 1));
            Assert.IsFalse(set.IsActive(MusicIntensity.Fight, 2));

            Assert.IsTrue(set.IsActive(MusicIntensity.Boss, 3));
        }

        // A voice's source pool is reused across sets of different sizes, so a
        // caller can legitimately ask about a stem index this set does not
        // have. Silent is the right answer; throwing would make pooling
        // impossible.
        [Test]
        public void AnOutOfRangeStem_IsSilentRatherThanAnError()
        {
            var set = Resolve(Pilot());

            Assert.IsFalse(set.IsActive(MusicIntensity.Boss, 99));
            Assert.IsFalse(set.IsActive(MusicIntensity.Boss, -1));
            Assert.IsNull(set.StemPath(99));
        }

        [Test]
        public void BarSeconds_ComesFromTempoAndMetre()
        {
            var set = Resolve(Pilot());

            // 120 BPM is half a second a beat; four of them is a two-second bar.
            Assert.AreEqual(2d, set.BarSeconds, 1e-9);
            Assert.AreEqual(2d, set.QuantiseSeconds, 1e-9);
        }

        [Test]
        public void APhraseLengthQuantise_MultipliesTheWindowNotTheBar()
        {
            var raw = Pilot();
            raw.quantiseBars = 4;
            var set = Resolve(raw);

            Assert.AreEqual(2d, set.BarSeconds, 1e-9, "A bar is still a bar");
            Assert.AreEqual(8d, set.QuantiseSeconds, 1e-9, "But a transition waits four of them");
        }

        // Unauthored means one bar -- responsive, and what an author who did
        // not think about phrasing would expect.
        [Test]
        public void AnUnauthoredQuantise_DefaultsToOneBar()
        {
            var raw = Pilot();
            raw.quantiseBars = 0;

            Assert.AreEqual(1, Resolve(raw).QuantiseBars);
        }

        // Nine is the expected stem count and nothing may depend on it.
        [Test]
        public void ASetOfAnySize_Works()
        {
            foreach (int count in new[] { 1, 5, 12 })
            {
                var set = Resolve(Pilot(count));

                Assert.AreEqual(count, set.StemCount, $"{count}-stem set");
                Assert.IsTrue(set.IsActive(MusicIntensity.Boss, count - 1),
                    $"A {count}-stem set's boss tier should reach its last stem");
            }
        }
    }

    // music_layers.json's rules, at the boundary where an authoring slip
    // should become a named error rather than a song that quietly plays the
    // wrong mix.
    public class MusicLayerResolverTests
    {
        private static RawMusicSet ValidSet(string id = "floor_1")
        {
            return new RawMusicSet
            {
                id = id,
                folder = "Audio/Music/" + id,
                bpm = 96,
                beatsPerBar = 4,
                gain = 1f,
                stems = new[] { "01_pad", "02_bass", "03_kick" },
                tiers = new[]
                {
                    new RawMusicTier { tier = "ambient", stems = new[] { 0 } },
                    new RawMusicTier { tier = "fight", stems = new[] { 0, 1 } },
                    new RawMusicTier { tier = "elite", stems = new[] { 0, 1, 2 } },
                    new RawMusicTier { tier = "boss", stems = new[] { 0, 1, 2 } },
                },
            };
        }

        private static RawMusicLayerFile ValidFile()
        {
            return new RawMusicLayerFile
            {
                sets = new[] { ValidSet() },
                floors = new[] { new RawMusicFloor { floor = 1, set = "floor_1" } },
                hub = "floor_1",
                fallbackSet = "floor_1",
            };
        }

        private static string Errors(RawMusicLayerFile file)
        {
            Assert.IsFalse(MusicLayerResolver.TryResolve(file, out _, out var errors),
                "This manifest was expected to be rejected");
            return string.Join(" ", errors);
        }

        private static MusicLayerLibrary Ok(RawMusicLayerFile file)
        {
            Assert.IsTrue(MusicLayerResolver.TryResolve(file, out var library, out var errors),
                string.Join(" ", errors));
            return library;
        }

        [Test]
        public void AValidManifest_Resolves()
        {
            var library = Ok(ValidFile());

            Assert.AreEqual(1, library.Sets.Count);
            Assert.AreEqual("floor_1", library.ForFloor(1).Id);
            Assert.AreEqual("floor_1", library.Hub.Id);
        }

        // No manifest is a supported state, not an error -- it is how the game
        // ships before any stems exist, and MusicController falls back to the
        // pre-layering beds.
        [Test]
        public void NoManifestAtAll_ResolvesToAnEmptyLibrary()
        {
            Assert.IsTrue(MusicLayerResolver.TryResolve(null, out var library, out var errors));
            CollectionAssert.IsEmpty(errors);
            Assert.IsTrue(library.IsEmpty);
        }

        [Test]
        public void AnEmptyManifest_IsAlsoFine()
        {
            Assert.IsTrue(MusicLayerResolver.TryResolve(new RawMusicLayerFile(), out var library, out _));
            Assert.IsTrue(library.IsEmpty);
            Assert.IsNull(library.Fallback);
            Assert.IsNull(library.ForFloor(1));
        }

        // A floor with NO mapping is a schedule ("floor 7's music is not
        // written yet"), and falls back. A floor mapped to a set that does not
        // exist is a TYPO, and is refused. The difference is the whole reason
        // both behaviours exist.
        [Test]
        public void AnUnmappedFloor_FallsBack()
        {
            Assert.AreEqual("floor_1", Ok(ValidFile()).ForFloor(7).Id);
        }

        [Test]
        public void AFloorPointingAtAMissingSet_IsATypoAndIsRefused()
        {
            var file = ValidFile();
            file.floors = new[] { new RawMusicFloor { floor = 2, set = "floor_2" } };

            StringAssert.Contains("floor_2", Errors(file));
        }

        [Test]
        public void TwoFloorsMayShareOneSet()
        {
            var file = ValidFile();
            file.floors = new[]
            {
                new RawMusicFloor { floor = 1, set = "floor_1" },
                new RawMusicFloor { floor = 2, set = "floor_1" },
            };

            var library = Ok(file);
            Assert.AreSame(library.ForFloor(1), library.ForFloor(2));
        }

        [Test]
        public void TheSameFloorTwice_IsRefused()
        {
            var file = ValidFile();
            file.floors = new[]
            {
                new RawMusicFloor { floor = 1, set = "floor_1" },
                new RawMusicFloor { floor = 1, set = "floor_1" },
            };

            StringAssert.Contains("mapped twice", Errors(file));
        }

        [Test]
        public void ADuplicateSetId_IsRefused()
        {
            var file = ValidFile();
            file.sets = new[] { ValidSet(), ValidSet() };

            StringAssert.Contains("Duplicate music set id", Errors(file));
        }

        [Test]
        public void AMissingFallback_IsRefusedOnceAnySetExists()
        {
            var file = ValidFile();
            file.fallbackSet = "";

            StringAssert.Contains("fallbackSet is required", Errors(file));
        }

        [Test]
        public void AHubPointingAtNothing_IsRefused()
        {
            var file = ValidFile();
            file.hub = "nope";

            StringAssert.Contains("hub", Errors(file));
        }

        // Unauthored hub means "share the fallback", which is what makes the
        // handoff's open question ("does the hub get its own set?") a one-line
        // manifest edit rather than a code change.
        [Test]
        public void NoHubAuthored_FallsThroughToTheFallbackSet()
        {
            var file = ValidFile();
            file.hub = "";

            Assert.AreEqual("floor_1", Ok(file).Hub.Id);
        }

        [Test]
        public void ATierIndexPastTheEndOfTheStems_IsRefused()
        {
            var file = ValidFile();
            file.sets[0].tiers[3].stems = new[] { 0, 1, 9 };

            StringAssert.Contains("names stem 9", Errors(file));
        }

        [Test]
        public void AnUnknownTierName_IsRefused()
        {
            var file = ValidFile();
            file.sets[0].tiers[1].tier = "medium";

            StringAssert.Contains("medium", Errors(file));
        }

        // Inheriting a missing tier from the one below would hide the hole: a
        // song with no elite entry would play its ordinary fight mix in an
        // elite room and sound like nothing was wrong.
        [Test]
        public void AMissingTier_IsRefusedRatherThanInherited()
        {
            var file = ValidFile();
            file.sets[0].tiers = file.sets[0].tiers.Where(t => t.tier != "elite").ToArray();

            StringAssert.Contains("elite", Errors(file));
        }

        [Test]
        public void ATierWithNoStems_IsRefused()
        {
            var file = ValidFile();
            file.sets[0].tiers[0].stems = new int[0];

            StringAssert.Contains("silent", Errors(file));
        }

        // Two entries naming one file is one file at double volume, not two
        // layers -- silent, and exactly the kind of thing that gets blamed on
        // the mix.
        [Test]
        public void ADuplicateStem_IsRefused()
        {
            var file = ValidFile();
            file.sets[0].stems = new[] { "01_pad", "02_bass", "01_pad" };

            StringAssert.Contains("listed twice", Errors(file));
        }

        [Test]
        public void ASetWithNoStems_IsRefused()
        {
            var file = ValidFile();
            file.sets[0].stems = new string[0];

            StringAssert.Contains("no stems", Errors(file));
        }

        [Test]
        public void TempoIsRequired_BecauseWithoutItThereIsNoBarToLandOn()
        {
            var file = ValidFile();
            file.sets[0].bpm = 0;
            StringAssert.Contains("bpm", Errors(file));

            file = ValidFile();
            file.sets[0].beatsPerBar = 0;
            StringAssert.Contains("beatsPerBar", Errors(file));
        }

        [Test]
        public void AZeroGain_IsRefused()
        {
            var file = ValidFile();
            file.sets[0].gain = 0f;

            StringAssert.Contains("gain", Errors(file));
        }

        [Test]
        public void EveryProblem_IsReportedInOnePass()
        {
            var file = ValidFile();
            file.sets[0].bpm = 0;
            file.sets[0].gain = 0f;
            file.sets[0].folder = "";

            Assert.IsFalse(MusicLayerResolver.TryResolve(file, out _, out List<string> errors));
            Assert.GreaterOrEqual(errors.Count, 3,
                "A hand-edited file should say everything wrong with it at once, not one typo per rebuild");
        }
    }
}
