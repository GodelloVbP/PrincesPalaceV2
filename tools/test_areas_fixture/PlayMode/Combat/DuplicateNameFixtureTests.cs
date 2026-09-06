// FIXTURE, not a test. See README.md. The other half of the name collision.
using NUnit.Framework;
using UnityEngine.TestTools;

namespace PrincesPalace.TestAreasFixture
{
    public class DuplicatedFixtureTests
    {
        [UnityTest]
        public void PlayModeHalf() { }
    }
}
