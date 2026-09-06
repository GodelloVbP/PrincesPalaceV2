// FIXTURE, not a test. See README.md.
//
// Half of a name collision; the other half is PlayMode/Combat with the same
// file name. The index is keyed by class name, so one of the two overwrites
// the other and takes its platform, area and host with it.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public class DuplicatedFixtureTests
    {
        [Test]
        public void EditModeHalf() { }
    }
}
