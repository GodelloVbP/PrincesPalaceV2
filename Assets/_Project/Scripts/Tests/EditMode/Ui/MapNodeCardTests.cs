using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The words on the map's rooms: captions, the prize chip's tag and the
    // reason a refused press gives. Literals on purpose -- these are copy, and
    // a test that rebuilt them from the same templates would pass for any
    // wording.
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

        [TestCase(MapTileState.Current, "YOU ARE HERE")]
        [TestCase(MapTileState.Walked, "ALREADY CLEARED")]
        [TestCase(MapTileState.Ahead, "TOO FAR AHEAD")]
        [TestCase(MapTileState.Closed, "NO PATH")]
        [TestCase(MapTileState.Open, "ON THE MOVE")]
        public void EachStateHasItsRefusal(MapTileState state, string expected) =>
            Assert.AreEqual(expected, MapNodeCard.Refusal(state));

        [TestCase(PrizeKind.Gold, 15, "15 G")]
        [TestCase(PrizeKind.Consumable, 1, "POTION")]
        [TestCase(PrizeKind.Consumable, 3, "3 POTIONS")]
        [TestCase(PrizeKind.Item, 0, "GEAR")]
        [TestCase(PrizeKind.Book, 0, "BOOK")]
        [TestCase(PrizeKind.Relic, 0, "RELIC")]
        [TestCase(PrizeKind.None, 0, "")]
        public void EachChipHasItsTag(PrizeKind kind, int amount, string expected) =>
            Assert.AreEqual(expected, MapNodeCard.ChipTag(new MapNodeChip(kind, amount)));
    }
}
