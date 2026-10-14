using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // MapView and the path rule of each archetype, on a hand-built realm:
    //
    //   entry(0) --> spine(1) --> heart(3)
    //        \--> pocket(2) --/
    //
    // The party stands on the entry and chooses between the spine room and
    // the pocket room; the pocket costs one extra room.
    public class BotMapPolicyTests
    {
        private static SeededRandom Rng() => new SeededRandom(1);

        private sealed class Realm
        {
            public RealmMap Map = new RealmMap();
            public RealmNode Entry, Spine, Pocket, Heart;
            public Dictionary<int, int> Strength = new Dictionary<int, int>();

            public Realm(RoomType spine, int spineStrength, PrizeKind spinePrize,
                         RoomType pocket, int pocketStrength, PrizeKind pocketPrize)
            {
                Entry = Node(0, 0, RoomType.Entry, RealmNodeRole.Entry, PrizeKind.None);
                Spine = Node(1, 1, spine, RealmNodeRole.Spine, spinePrize);
                Pocket = Node(2, 1, pocket, RealmNodeRole.Pocket, pocketPrize);
                Heart = Node(3, 2, RoomType.Boss, RealmNodeRole.Heart, PrizeKind.None);
                Entry.Next.AddRange(new[] { 1, 2 });
                Pocket.Next.Add(1);
                Spine.Next.Add(3);
                Strength[1] = spineStrength;
                Strength[2] = pocketStrength;
            }

            private RealmNode Node(int id, int offset, RoomType type, RealmNodeRole role, PrizeKind prize)
            {
                var node = new RealmNode { Id = id, Offset = offset, Type = type, Role = role, Prize = prize };
                Map.Nodes.Add(node);
                return node;
            }

            public RunView View(float hp) =>
                new RunView(hp, 0, 1, 0, null).WithMap(
                    MapView.Build(Map, Entry,
                        n => Strength.TryGetValue(n.Id, out int s) ? s : 0,
                        n => n.Prize));

            public IReadOnlyList<RealmNode> Offer(params RealmNode[] nodes) => nodes;
        }

        // ---- MapView ----------------------------------------------------------

        [Test]
        public void MapViewCountsRoomsToTheHeartAndTheDetourOfThePocket()
        {
            var realm = new Realm(RoomType.Fight, 1, PrizeKind.Gold, RoomType.Fight, 2, PrizeKind.Consumable);
            var view = realm.View(1f).Map;

            Assert.AreEqual(2, view.ShortestToHeart);
            Assert.AreEqual(3, view.LongestToHeart);
            Assert.AreEqual(1, view.Of(realm.Spine).ShortestToHeart);
            Assert.AreEqual(2, view.Of(realm.Pocket).ShortestToHeart);
            Assert.AreEqual(0, view.Of(realm.Spine).DetourRooms);
            Assert.AreEqual(1, view.Of(realm.Pocket).DetourRooms);
            Assert.IsTrue(view.Of(realm.Pocket).InPocket);
            Assert.IsFalse(view.Of(realm.Spine).InPocket);
        }

        [Test]
        public void MapViewCarriesStrengthPrizeAndAnchors()
        {
            var realm = new Realm(RoomType.EliteFight, 3, PrizeKind.None, RoomType.Fight, 2, PrizeKind.Relic);
            var view = realm.View(1f).Map;

            Assert.AreEqual(3, view.Of(realm.Spine).Strength);
            Assert.IsTrue(view.Of(realm.Spine).IsAnchor);
            Assert.AreEqual(2, view.Of(realm.Pocket).Strength);
            Assert.AreEqual(PrizeKind.Relic, view.Of(realm.Pocket).Prize);
            Assert.AreEqual(1, view.AnchorsAhead);
            Assert.AreEqual(1, view.Of(realm.Pocket).AnchorsAhead);
        }

        // ---- GreedyAggressive ----------------------------------------------------

        [Test]
        public void GreedyAggressiveTakesTheStrongPocketFightFromHalfHpAndNotBelow()
        {
            var realm = new Realm(RoomType.Fight, 1, PrizeKind.Gold, RoomType.Fight, 3, PrizeKind.Gold);
            var policy = new GreedyAggressivePolicy();
            var offer = realm.Offer(realm.Spine, realm.Pocket);

            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(0.9f), Rng()));
            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(0.5f), Rng()));
            Assert.AreSame(realm.Spine, policy.ChooseNode(offer, realm.View(0.4f), Rng()));
        }

        [Test]
        public void GreedyAggressiveWithoutAMapKeepsItsRoomTypeOrder()
        {
            var realm = new Realm(RoomType.Fight, 1, PrizeKind.Gold, RoomType.Treasure, 0, PrizeKind.None);
            var policy = new GreedyAggressivePolicy();

            var view = new RunView(0.2f, 0, 1, 0, null);
            Assert.AreSame(realm.Pocket, policy.ChooseNode(realm.Offer(realm.Spine, realm.Pocket), view, Rng()));
        }

        [Test]
        public void GreedyAggressiveHurtTakesAPocketBeforeAStrengthThreeFightWhenBothBreakAGate()
        {
            var realm = new Realm(RoomType.Fight, 3, PrizeKind.Gold, RoomType.Treasure, 0, PrizeKind.None);
            var policy = new GreedyAggressivePolicy();

            Assert.AreSame(realm.Pocket, policy.ChooseNode(realm.Offer(realm.Spine, realm.Pocket), realm.View(0.3f), Rng()));
        }

        // ---- GreedyDefensive -----------------------------------------------------

        [Test]
        public void GreedyDefensiveNeverTakesAnAnchorWhileAnotherRoomIsOpen()
        {
            var realm = new Realm(RoomType.EliteFight, 3, PrizeKind.None, RoomType.Fight, 2, PrizeKind.Gold);
            var policy = new GreedyDefensivePolicy();

            Assert.AreSame(realm.Pocket, policy.ChooseNode(realm.Offer(realm.Spine, realm.Pocket), realm.View(1f), Rng()));
        }

        [Test]
        public void GreedyDefensiveTakesAStrengthThreeFightOnlyFromNinetyPercent()
        {
            var realm = new Realm(RoomType.Fight, 3, PrizeKind.Gold, RoomType.Rest, 0, PrizeKind.None);
            realm.Pocket.Role = RealmNodeRole.ForkSibling;
            var policy = new GreedyDefensivePolicy();
            var offer = realm.Offer(realm.Spine, realm.Pocket);

            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(0.8f), Rng()));
            Assert.AreSame(realm.Spine, policy.ChooseNode(offer, realm.View(0.95f), Rng()));
        }

        [Test]
        public void GreedyDefensiveTakesAPocketFromSeventyFivePercent()
        {
            var realm = new Realm(RoomType.Fight, 1, PrizeKind.Gold, RoomType.Fight, 1, PrizeKind.Gold);
            var policy = new GreedyDefensivePolicy();
            var offer = realm.Offer(realm.Pocket, realm.Spine);

            Assert.AreSame(realm.Spine, policy.ChooseNode(offer, realm.View(0.74f), Rng()));
            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(0.75f), Rng()));
        }

        [Test]
        public void GreedyDefensiveEntersAPocketThatOpensOnARestWhenHurt()
        {
            var realm = new Realm(RoomType.Fight, 1, PrizeKind.Gold, RoomType.Rest, 0, PrizeKind.None);
            var policy = new GreedyDefensivePolicy();
            var offer = realm.Offer(realm.Spine, realm.Pocket);

            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(0.5f), Rng()));
        }

        // ---- ProtectTheFront -----------------------------------------------------

        [Test]
        public void ProtectTheFrontTakesAPocketFromSixtyPercentAndAnAnchorFromNinety()
        {
            var realm = new Realm(RoomType.Fight, 1, PrizeKind.Gold, RoomType.Fight, 1, PrizeKind.Gold);
            var policy = new ProtectTheFrontPolicy();
            var offer = realm.Offer(realm.Pocket, realm.Spine);

            Assert.AreSame(realm.Spine, policy.ChooseNode(offer, realm.View(0.55f), Rng()));
            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(0.6f), Rng()));

            var anchors = new Realm(RoomType.Event, 0, PrizeKind.None, RoomType.EliteFight, 3, PrizeKind.None);
            var anchorOffer = anchors.Offer(anchors.Spine, anchors.Pocket);
            Assert.AreSame(anchors.Spine, policy.ChooseNode(anchorOffer, anchors.View(0.85f), Rng()));
            Assert.AreSame(anchors.Pocket, policy.ChooseNode(anchorOffer, anchors.View(0.9f), Rng()));
        }

        // ---- Lookahead2 ------------------------------------------------------------

        [Test]
        public void Lookahead2TakesTheRichPocketAtFullHpAndTheWeakSpineWhenHurt()
        {
            var realm = new Realm(RoomType.Fight, 1, PrizeKind.Gold, RoomType.Fight, 3, PrizeKind.Gold);
            var policy = new Lookahead2Policy();
            var offer = realm.Offer(realm.Spine, realm.Pocket);

            // Full HP: spine 0.75 + heart 0 = 0.75; pocket 0.75 + spine 0.75 - 0.3 = 1.2.
            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(1f), Rng()));

            // 40% HP: spine 0.15; pocket -4.65 + 0.15 - 0.3.
            Assert.AreSame(realm.Spine, policy.ChooseNode(offer, realm.View(0.4f), Rng()));
        }

        // The I3 inversion: a Gear chip (prize 3) on a strength-3 pocket fight
        // outweighed its risk at every HP, so a hurt party still took it.
        [Test]
        public void Lookahead2TakesAGearChipStrengthThreePocketOnlyWhenHealthy()
        {
            var realm = new Realm(RoomType.Fight, 1, PrizeKind.Gold, RoomType.Fight, 3, PrizeKind.Item);
            var policy = new Lookahead2Policy();
            var offer = realm.Offer(realm.Spine, realm.Pocket);

            // Full HP: spine 0.75; pocket 4.5 - 2.25 + 0.75 - 0.3 = 2.7.
            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(1f), Rng()));

            // 80% HP: spine 0.55; pocket 4.5 - 4.05 + 0.55 - 0.3 = 0.7.
            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(0.8f), Rng()));

            // 60% HP: spine 0.35; pocket 4.5 - 5.85 + 0.35 - 0.3 = -1.3.
            Assert.AreSame(realm.Spine, policy.ChooseNode(offer, realm.View(0.6f), Rng()));

            // 30% HP: spine 0.05; pocket 4.5 - 8.55 + 0.05 - 0.3 = -4.3.
            Assert.AreSame(realm.Spine, policy.ChooseNode(offer, realm.View(0.3f), Rng()));
        }

        [Test]
        public void Lookahead2ValuesARestByMissingHp()
        {
            var realm = new Realm(RoomType.Fight, 1, PrizeKind.Gold, RoomType.Rest, 0, PrizeKind.None);
            realm.Pocket.Role = RealmNodeRole.ForkSibling;
            realm.Pocket.Next.Clear();
            realm.Pocket.Next.Add(3);
            var policy = new Lookahead2Policy();
            var offer = realm.Offer(realm.Spine, realm.Pocket);

            // 50% HP: rest 2.0; fight 1 - 0.75 = 0.25.
            Assert.AreSame(realm.Pocket, policy.ChooseNode(offer, realm.View(0.5f), Rng()));

            // Full HP: rest 0; fight 0.75.
            Assert.AreSame(realm.Spine, policy.ChooseNode(offer, realm.View(1f), Rng()));
        }
    }
}
