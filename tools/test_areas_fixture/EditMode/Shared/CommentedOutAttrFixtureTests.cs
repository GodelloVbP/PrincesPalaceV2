// FIXTURE, not a test. See README.md -- nothing compiles this folder.
//
// The counter-case to QualifiedAttrFixtureTests, and it is a helper rather
// than a suite: the only [Test] in it is inside a comment, and one more is
// inside a string. Both refusals that ask "does this file carry tests" read
// the code with the comments and literals stripped, so neither may fire --
//
//   * Shared/ must NOT be refused over it (it is a helper, correctly placed,
//     and there is no move that would fix a complaint about a comment).
//   * the internal class beside it must NOT be reported as a blind spot,
//     which is what happens when a file is wrongly counted as carrying tests.
//
// Written as a helper on purpose: a commented-out [Test] is what a suite
// disabled while something is investigated looks like, and the disabling
// should not take the whole gate down with it.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public static class CommentedOutAttrFixtureTests
    {
        // [Test] -- disabled while the flake is investigated.
        public static string Advice => "annotate the method with [Test] to run it";

        public static int Helper() => 1;
    }

    internal static class CommentedOutAttrFixtureHelpers
    {
        public static int Also() => 2;
    }
}
