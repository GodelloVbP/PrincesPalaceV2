// FIXTURE, not a test. See README.md.
//
// Two folders deep. Get-TestIndex does not recurse, so this file is in no
// area and no run of any kind reaches it -- which is why the folder is
// refused rather than adopted into Run/ above it.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public class DepthTwoFixtureTests
    {
        [Test]
        public void Unreachable() { }
    }
}
