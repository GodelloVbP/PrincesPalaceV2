using NUnit.Framework;

namespace PrincesPalace.PlayModeTests
{
    public class CaptureTimingJsonTests
    {
        [Test]
        public void AttackTimingPinsLiteral()
        {
            Assert.AreEqual(
                "{\"kind\":\"attack\",\"id\":\"rat\",\"skill\":\"Bite\",\"fps\":60,\"frames\":120,\"speed\":1.50}",
                CaptureScenarioRunner.TimingJson("attack", "rat", "Bite", 60, 120, 1.5f));
        }

        [Test]
        public void StatusTimingPinsLiteral()
        {
            Assert.AreEqual(
                "{\"kind\":\"status\",\"id\":\"Bleed\",\"fps\":60,\"frames\":120,\"speed\":1.00}",
                CaptureScenarioRunner.TimingJson("status", "Bleed", null, 60, 120, 1f));
        }
    }
}
