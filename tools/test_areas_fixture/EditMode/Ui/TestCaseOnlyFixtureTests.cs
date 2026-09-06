// FIXTURE, not a test. See README.md -- nothing compiles this folder.
//
// The control case: every case here is [TestCase]/[TestCaseSource] and there
// is no bare [Test] anywhere. Under the old $TestAttrPattern this file was
// not a test file at all, which is how KitContainerPlacementTests and
// TypographyAssetTests could have been parked in Shared/ with the gate quiet.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public class TestCaseOnlyFixtureTests
    {
        static readonly int[] Cases = { 1, 2 };

        [TestCase(1)]
        public void OneCase(int n) { }

        [TestCaseSource(nameof(Cases))]
        public void SourcedCase(int n) { }
    }
}
