using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The overview hint, as the player reads it on each device. Literals on
    // purpose -- this is copy, and a test that rebuilt it from the same
    // templates would pass for any wording.
    public class MapNodeCardTests
    {
        [TestCase(false, PromptDevice.Keyboard, "SPACE  ·  OVERVIEW")]
        [TestCase(false, PromptDevice.Xbox, "Y  ·  OVERVIEW")]
        [TestCase(true, PromptDevice.Keyboard, "OVERVIEW  ·  LOOK ONLY  ·  SPACE TO RETURN")]
        [TestCase(true, PromptDevice.Xbox, "OVERVIEW  ·  LOOK ONLY  ·  Y TO RETURN")]
        public void TheOverviewTextNamesTheKeyOfTheDeviceInHand(bool inOverview, PromptDevice device, string expected) =>
            Assert.AreEqual(expected, MapNodeCard.OverviewText(inOverview, device));

        [Test]
        public void TheTitleReadsFloorAndRoomOnOneLine() =>
            Assert.AreEqual("FLOOR 1  ·  ROOM 7", UiStrings.MapTitle.Format(1, 7));

        [Test]
        public void TheGoldLineIsTheBareAmount() =>
            Assert.AreEqual("39", UiStrings.MapGold.Format(39));
    }
}
