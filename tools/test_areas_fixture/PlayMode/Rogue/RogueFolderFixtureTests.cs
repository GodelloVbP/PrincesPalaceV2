// FIXTURE, not a test. See README.md.
//
// A ninth folder beside the eight, which is the second structural refusal.
// It exists only to give the folder something to hold -- git does not track
// an empty directory, so the case cannot be made with a folder alone.
//
// The refusal is about Rogue/ itself, not about this file: an unrecognised
// folder is either a typo or somebody adding an area without saying so in
// tools/test_areas.ps1's placement guide, and both end the same way -- every
// test underneath it silently skipped by every area run, because $AreaNames
// has no entry that reaches it.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public class RogueFolderFixtureTests
    {
        [Test]
        public void InAFolderThatIsNotAnArea() { }
    }
}
