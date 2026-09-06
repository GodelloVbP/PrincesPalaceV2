// FIXTURE, not a test. See README.md.
//
// A suite parked among the helpers, carrying only [TestCase]. Shared belongs
// to no area, so this is a suite in no area -- and with the old attribute
// pattern the refusal could not see a [TestCase]-only fixture at all.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public class SharedSuiteFixtureTests
    {
        [TestCase(1)]
        public void ParkedInShared(int n) { }
    }
}
