// FIXTURE, not a test. See README.md -- nothing compiles this folder.
//
// The case $TestAttrPattern used to get wrong: an indexer such as
// "_starts[test.Id]" reads as "[test" when matched case-insensitively,
// which used to satisfy the same alternation as a real [Test] attribute.
// Two things make this file the control for the fix:
//
//   * "test" is the local variable's name, lower-case, so only a
//     case-INSENSITIVE match confuses it with [Test]/[TestCase]/... --
//     -cmatch/-cnotmatch must tell them apart.
//   * the "[" is immediately preceded by an identifier character (the
//     indexer's receiver "_starts"), which a real attribute's "[" never
//     is -- the (?<!\w) anchor must tell THAT apart too.
//
// Shared/ must NOT be refused over this file: it carries no real test.
using System.Collections.Generic;

namespace PrincesPalace.TestAreasFixture
{
    public static class IndexerFalsePositiveFixtureTests
    {
        private static readonly Dictionary<int, int> _starts = new Dictionary<int, int> { { 1, 100 } };

        public static int LookUp(FakeRequest test) => _starts[test.Id];
    }

    public struct FakeRequest
    {
        public int Id;
    }
}
