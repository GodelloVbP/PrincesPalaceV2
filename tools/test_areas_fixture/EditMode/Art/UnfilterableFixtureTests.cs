// FIXTURE, not a test. See README.md.
//
// Two shapes NUnit renames at run time: a generic fixture is reported as
// GenericFixtureTests<Int32> and a nested one as
// OuterFixtureTests+NestedInnerFixtureTests. Every filter this repo builds is
// the bare declared name, so neither can be selected by an area run, a class
// run or a -Changed run.
//
// The "{" inside the string below is on purpose: brace depth is what decides
// nesting, so a brace in a literal must not count.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public class GenericFixtureTests<T>
    {
        [Test]
        public void Parameterised() { }
    }

    public class OuterFixtureTests
    {
        const string Brace = "{ not a scope";

        [Test]
        public void Outer() { }

        public class NestedInnerFixtureTests
        {
            [Test]
            public void Inner() { }
        }
    }
}
