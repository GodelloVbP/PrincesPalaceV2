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

        [Test]
        public void AnEventNamesItsTitleInPlaceOfTheStockSentence()
        {
            var facts = new MapNodeFacts(RoomType.Event).WithEventTitle("The Toll Bridge");
            Assert.AreEqual("The Toll Bridge", MapNodeCard.Body(facts));
            Assert.AreEqual("The Toll Bridge\nWalking past the detour heals the squad 8%.",
                MapNodeCard.Body(facts.WithRealm(0, default, skipHealPercent: 8, guardsHeart: false)));
        }

        [TestCase(MapTileState.Current, "YOU ARE HERE")]
        [TestCase(MapTileState.Walked, "ALREADY CLEARED")]
        [TestCase(MapTileState.Ahead, "TOO FAR AHEAD")]
        [TestCase(MapTileState.Closed, "NO PATH")]
        [TestCase(MapTileState.Open, "ON THE MOVE")]
        public void EachStateHasItsRefusal(MapTileState state, string expected) =>
            Assert.AreEqual(expected, MapNodeCard.Refusal(state));

        // ---- the realm's facts (PLAN_RUN_MAP section 3) ---------------------------------

        [Test]
        public void AFightWithStrengthAndAGoldChip_ShowsBoth()
        {
            var facts = new MapNodeFacts(RoomType.Fight, new[] { "Giant Rat" }, experience: 30, gold: 12)
                .WithRealm(2, new MapNodeChip(PrizeKind.Gold, 15), skipHealPercent: 0, guardsHeart: false);

            Assert.AreEqual("FIGHT  ·  STRENGTH 2", MapNodeCard.Title(facts));
            Assert.AreEqual("Enemies: Giant Rat\nPays 12 gold · 30 exp  ·  Prize: 15 gold", MapNodeCard.Body(facts));
        }

        [Test]
        public void AnAnchorSaysItGuardsTheBoss_AndASpineChoiceNamesTheSkipHeal()
        {
            var anchor = new MapNodeFacts(RoomType.EliteFight, new[] { "Hollow Treant" }, experience: 40, gold: 20)
                .WithRealm(3, default, skipHealPercent: 0, guardsHeart: true);
            Assert.AreEqual("Enemies: Hollow Treant\nPays 20 gold · 40 exp\nLeft alive, it guards the boss.",
                MapNodeCard.Body(anchor));

            var rest = new MapNodeFacts(RoomType.Event).WithRealm(0, default, skipHealPercent: 8, guardsHeart: false);
            Assert.AreEqual("EVENT", MapNodeCard.Title(rest));
            Assert.AreEqual("Something waits here. What, you learn when you step in.\n" +
                            "Walking past the detour heals the squad 8%.", MapNodeCard.Body(rest));
        }

        [TestCase(PrizeKind.Consumable, 1, "Prize: a potion")]
        [TestCase(PrizeKind.Consumable, 3, "Prize: 3 potions")]
        [TestCase(PrizeKind.Item, 0, "Prize: gear")]
        [TestCase(PrizeKind.Book, 0, "Prize: a spell book")]
        [TestCase(PrizeKind.Relic, 0, "Prize: a relic")]
        [TestCase(PrizeKind.None, 0, "")]
        public void EachChipHasItsLine(PrizeKind kind, int amount, string expected) =>
            Assert.AreEqual(expected, MapNodeCard.ChipLine(new MapNodeChip(kind, amount)));
    }
}
