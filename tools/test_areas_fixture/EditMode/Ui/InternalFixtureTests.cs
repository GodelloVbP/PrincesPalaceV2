// FIXTURE, not a test. See README.md.
//
// A public fixture beside an internal one. Discovery indexes the public one,
// so "pass the file if ANY of its classes was discovered" reported this file
// clean while InternalOnlyFixtureTests never ran under any filter.
using NUnit.Framework;

namespace PrincesPalace.TestAreasFixture
{
    public class InternalFixtureTests
    {
        [Test]
        public void Visible() { }
    }

    internal class InternalOnlyFixtureTests
    {
        [Test]
        public void Invisible() { }
    }
}
