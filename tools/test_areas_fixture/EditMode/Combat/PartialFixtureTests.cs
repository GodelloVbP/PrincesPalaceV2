// FIXTURE, not a test. See README.md.
//
// The third control: a legitimate fixture split across two files with
// `partial`, both in this same area. Get-DuplicateClassNames must NOT
// report this -- unlike DuplicateNameFixtureTests.cs (same folder), this is
// one class in two files by design, not a naming accident, and NUnit
// compiles the two pieces into one class regardless of how many files
// declared it.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public partial class SamePartialFixtureTests
    {
        [Test]
        public void FirstHalf() { }
    }
}
