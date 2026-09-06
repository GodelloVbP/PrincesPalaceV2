// FIXTURE, not a test. See README.md -- nothing compiles this folder.
//
// A suite parked among the helpers whose attribute is written out in full.
// C# treats [NUnit.Framework.Test] and [Test] as the same attribute, and the
// old $TestAttrPattern could see only the second: this file was not a test
// file at all, so Shared/ took it without complaint and the suite ran under
// no area.

namespace PrincesPalace.TestAreasFixture
{
    public class QualifiedAttrFixtureTests
    {
        [NUnit.Framework.Test]
        public void FullyQualified() { }
    }
}
