using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The words on the map's rooms: captions, the detail card per room type,
    // and the reason a refused press gives. Literals on purpose -- the card is
    // copy, and a test that rebuilt it from the same templates would pass for
    // any wording.
    public class MapNodeCardTests
    {
        [TestCase(RoomType.Entry, "START")]
        [TestCase(RoomType.Fight, "FIGHT")]
        [TestCase(RoomType.EliteFight, "ELITE")]
        [TestCase(RoomType.Boss, "BOSS")]
        [TestCase(RoomType.Treasure, "TREASURE")]
        [TestCase(RoomType.Rest, "REST")]
        [TestCase(RoomType.Shop, "SHOP")]
        [TestCase(RoomType.Event, "EVENT")]
        public void EachRoomTypeHasItsCaption(RoomType type, string expected) =>
            Assert.AreEqual(expected, MapNodeCard.Caption(type));

        [Test]
        public void AFightNamesItsEnemiesCountsRepeatsAndQuotesTheBasePayout()
        {
            var facts = new MapNodeFacts(RoomType.Fight, new[] { "Giant Rat", "Giant Rat", "Wolf" },
                experience: 30, gold: 12);

            Assert.AreEqual("Enemies: Giant Rat ×2, Wolf\nPays 12 gold · 30 exp", MapNodeCard.Body(facts));
        }

        [Test]
        public void AnEliteAndABossQuoteTheSameWayAsAFight()
        {
            Assert.AreEqual("Enemies: Hollow Treant ×2\nPays 31 gold · 46 exp",
                MapNodeCard.Body(new MapNodeFacts(RoomType.EliteFight, new[] { "Hollow Treant", "Hollow Treant" },
                    experience: 46, gold: 31)));

            Assert.AreEqual("Enemies: Bog Witch\nPays 40 gold · 90 exp",
                MapNodeCard.Body(new MapNodeFacts(RoomType.Boss, new[] { "Bog Witch" }, experience: 90, gold: 40)));
        }

        [Test]
        public void AFightWithNothingRolledSaysNothingRatherThanAHalfLine() =>
            Assert.AreEqual("", MapNodeCard.Body(new MapNodeFacts(RoomType.Fight)));

        [Test]
        public void TreasureNamesItsStash() =>
            Assert.AreEqual("A stash of 22 gold.", MapNodeCard.Body(new MapNodeFacts(RoomType.Treasure, stashGold: 22)));

        [Test]
        public void RoomsThatDecideOnArrivalSaySoInsteadOfGuessing()
        {
            Assert.AreEqual("The squad rests and recovers to full health.",
                MapNodeCard.Body(new MapNodeFacts(RoomType.Rest)));
            Assert.AreEqual("A trader. The stock is set when you step in.",
                MapNodeCard.Body(new MapNodeFacts(RoomType.Shop)));
            Assert.AreEqual("Something waits here. What, you learn when you step in.",
                MapNodeCard.Body(new MapNodeFacts(RoomType.Event)));
            Assert.AreEqual("Where this stretch of the descent began.",
                MapNodeCard.Body(new MapNodeFacts(RoomType.Entry)));
        }

        [TestCase(MapTileState.Current, "YOU ARE HERE")]
        [TestCase(MapTileState.Walked, "ALREADY CLEARED")]
        [TestCase(MapTileState.Ahead, "TOO FAR AHEAD")]
        [TestCase(MapTileState.Closed, "NO PATH")]
        [TestCase(MapTileState.Open, "ON THE MOVE")]
        public void EachStateHasItsRefusal(MapTileState state, string expected) =>
            Assert.AreEqual(expected, MapNodeCard.Refusal(state));
    }
}
