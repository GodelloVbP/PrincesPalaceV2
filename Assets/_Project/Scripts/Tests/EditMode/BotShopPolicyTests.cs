using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // IRunPolicy.ChooseShop for the four archetypes, plus the batch-level
    // shop-node preference that wraps ChooseNode.
    //
    // PURE DOMAIN, NO ORCHESTRATOR. ShopView is exactly the flattened shelf
    // the Core driver hands down, so a policy can be asked the whole question
    // from a hand-built struct -- which is the point of the view existing.
    // What the driver does with the answer (the loop, the cap, the refusal
    // exit) is Core's and is exercised by the batch itself.
    public class BotShopPolicyTests
    {
        private static SeededRandom Rng(ulong seed = 1) => new SeededRandom(seed);

        private static ShopCardView Gear(int index, int price, float score, bool sold = false,
            bool affordable = true) =>
            new ShopCardView(ShopStock.GearSection, index, ShopEntryKind.Gear, "gear_" + index,
                price, sold, false, affordable, score);

        private static ShopCardView Relic(int index, int price, bool affordable = true, bool sold = false) =>
            new ShopCardView(ShopStock.RelicSection, index, ShopEntryKind.Relic, "relic_" + index,
                price, sold, false, affordable, 0f);

        private static ShopCardView NoBook() =>
            new ShopCardView(ShopStock.BookSection, 0, ShopEntryKind.Book, "", 0, false, true, false, 0f);

        // Affordability is decided by the DRIVER, not recomputed here, so the
        // fixture sets it explicitly per card -- exactly the way the real view
        // arrives. `gold` is what the policies that reason about saving read.
        private static ShopView Shelf(IEnumerable<ShopCardView> cards, int gold,
            IEnumerable<ShopBagRow> bag = null, int[] rerollsUsed = null, int[] rerollPrices = null)
        {
            return new ShopView(
                cards.ToList(),
                rerollPrices ?? new[] { 15, 15, 15 },
                rerollsUsed ?? new[] { 0, 0, 0 },
                (bag ?? Enumerable.Empty<ShopBagRow>()).ToList(),
                gold);
        }

        private static RunView View(int gold, float hp = 1f) =>
            new RunView(hp, 8, 1, gold, null);

        // ---- GreedyAggressive ---------------------------------------------

        [Test]
        public void GreedyAggressiveBuysTheBestScoredAffordableGear()
        {
            var shop = Shelf(new[] { Gear(0, 40, 1.5f), Gear(1, 60, 9.0f), Gear(2, 30, 4.0f), NoBook() }, 200);

            var choice = new GreedyAggressivePolicy().ChooseShop(shop, View(200), Rng());

            Assert.AreEqual(ShopChoiceKind.BuyGear, choice.Kind);
            Assert.AreEqual(1, choice.Index);
        }

        [Test]
        public void GreedyAggressiveIgnoresGearItCannotAffordEvenWhenItIsTheBest()
        {
            var shop = Shelf(new[]
            {
                Gear(0, 40, 2.0f),
                Gear(1, 600, 9.0f, affordable: false),
            }, 50);

            var choice = new GreedyAggressivePolicy().ChooseShop(shop, View(50), Rng());

            Assert.AreEqual(ShopChoiceKind.BuyGear, choice.Kind);
            Assert.AreEqual(0, choice.Index);
        }

        // A ZERO SCORE IS "NOBODY CAN WEAR IT", not "unranked" -- see
        // ShopCardView.Score. Paying for it would be the defect this pins.
        [Test]
        public void GreedyAggressiveWillNotBuyGearThatScoresZero()
        {
            var shop = Shelf(new[] { Gear(0, 20, 0f), Gear(1, 24, 0f) }, 500);

            var choice = new GreedyAggressivePolicy().ChooseShop(shop, View(500), Rng());

            Assert.AreEqual(ShopChoiceKind.Leave, choice.Kind);
        }

        [Test]
        public void GreedyAggressiveFallsBackToTheRarestAffordableRelic()
        {
            // RelicPrice is a pure function of rarity, so the pricier card IS
            // the rarer one -- see GreedyAggressivePolicy.ChooseShop.
            var shop = Shelf(new[] { Gear(0, 20, 0f), Relic(0, 60), Relic(1, 190) }, 500);

            var choice = new GreedyAggressivePolicy().ChooseShop(shop, View(500), Rng());

            Assert.AreEqual(ShopChoiceKind.BuyRelic, choice.Kind);
            Assert.AreEqual(1, choice.Index);
        }

        [Test]
        public void GreedyAggressiveNeverRerolls()
        {
            var shop = Shelf(new[] { Gear(0, 20, 0f), Relic(0, 900, affordable: false) }, 5000);

            var choice = new GreedyAggressivePolicy().ChooseShop(shop, View(5000), Rng());

            Assert.AreEqual(ShopChoiceKind.Leave, choice.Kind);
        }

        [Test]
        public void GreedyAggressiveLeavesAnEmptiedShelf()
        {
            var shop = Shelf(new[] { Gear(0, 20, 5f, sold: true), Relic(0, 60, sold: true) }, 500);

            Assert.AreEqual(ShopChoiceKind.Leave,
                new GreedyAggressivePolicy().ChooseShop(shop, View(500), Rng()).Kind);
        }

        // ---- GreedyDefensive ----------------------------------------------

        private static ShopBagRow Bag(int index, string id, float score, bool duplicate = false,
            bool consumable = false, int sellPrice = 9) =>
            new ShopBagRow(index, id, 1, sellPrice, score, duplicate, consumable);

        [Test]
        public void GreedyDefensiveSellsJunkBeforeItBuysAnything()
        {
            var shop = Shelf(new[] { Gear(0, 40, 5f) }, 500,
                bag: new[] { Bag(0, "good_helm", 3f), Bag(1, "worthless_ring", 0f) });

            var choice = new GreedyDefensivePolicy().ChooseShop(shop, View(500), Rng());

            Assert.AreEqual(ShopChoiceKind.Sell, choice.Kind);
            Assert.AreEqual(1, choice.Index);
            Assert.AreEqual(1, choice.Quantity, "one copy per call -- a sale renumbers the bag");
        }

        [Test]
        public void GreedyDefensiveSellsADuplicateEvenWhenItScores()
        {
            var shop = Shelf(new ShopCardView[0], 500,
                bag: new[] { Bag(0, "helm", 4f), Bag(1, "helm", 4f, duplicate: true) });

            var choice = new GreedyDefensivePolicy().ChooseShop(shop, View(500), Rng());

            Assert.AreEqual(ShopChoiceKind.Sell, choice.Kind);
            Assert.AreEqual(1, choice.Index);
        }

        // The archetype that drinks potions does not sell them to buy armour.
        [Test]
        public void GreedyDefensiveNeverSellsAConsumable()
        {
            var shop = Shelf(new[] { Gear(0, 40, 5f) }, 500,
                bag: new[] { Bag(0, "health_potion", 0f, consumable: true) });

            var choice = new GreedyDefensivePolicy().ChooseShop(shop, View(500), Rng());

            Assert.AreEqual(ShopChoiceKind.BuyGear, choice.Kind);
            Assert.AreEqual(0, choice.Index);
        }

        [Test]
        public void GreedyDefensiveTakesTheCheapestRelicNotTheRarest()
        {
            var shop = Shelf(new[] { Relic(0, 300), Relic(1, 60) }, 500);

            var choice = new GreedyDefensivePolicy().ChooseShop(shop, View(500), Rng());

            Assert.AreEqual(ShopChoiceKind.BuyRelic, choice.Kind);
            Assert.AreEqual(1, choice.Index);
        }

        [Test]
        public void GreedyDefensiveNeverRerolls()
        {
            var shop = Shelf(new[] { Gear(0, 20, 0f) }, 5000);

            Assert.AreEqual(ShopChoiceKind.Leave,
                new GreedyDefensivePolicy().ChooseShop(shop, View(5000), Rng()).Kind);
        }

        // ---- Lookahead2 -----------------------------------------------------

        [Test]
        public void Lookahead2SavesForARelicItCouldAffordAfterOneMoreLeg()
        {
            // 190 is out of reach at 100 gold, but 100 + 185 covers it -- so
            // the gear on the shelf is deliberately not bought.
            var shop = Shelf(new[] { Gear(0, 40, 8f), Relic(0, 190, affordable: false) }, 100);

            var choice = new Lookahead2Policy().ChooseShop(shop, View(100), Rng());

            Assert.AreEqual(ShopChoiceKind.Leave, choice.Kind);
        }

        [Test]
        public void Lookahead2BuysTheGearWhenTheRelicIsOutOfReachEvenAfterALeg()
        {
            // 460 against 100 + 185 = 285: not reachable, so there is nothing
            // to save for and the gear is the right buy.
            var shop = Shelf(new[] { Gear(0, 40, 8f), Relic(0, 460, affordable: false) }, 100);

            var choice = new Lookahead2Policy().ChooseShop(shop, View(100), Rng());

            Assert.AreEqual(ShopChoiceKind.BuyGear, choice.Kind);
            Assert.AreEqual(0, choice.Index);
        }

        [Test]
        public void Lookahead2TakesAnAffordableRelicBeforeItSavesForAnything()
        {
            var shop = Shelf(new[] { Relic(0, 60), Relic(1, 190, affordable: false) }, 100);

            var choice = new Lookahead2Policy().ChooseShop(shop, View(100), Rng());

            Assert.AreEqual(ShopChoiceKind.BuyRelic, choice.Kind);
            Assert.AreEqual(0, choice.Index);
        }

        [Test]
        public void Lookahead2RerollsADeadGearSectionOnce()
        {
            var shop = Shelf(new[] { Gear(0, 20, 0f), Gear(1, 24, 0f) }, 500);

            var choice = new Lookahead2Policy().ChooseShop(shop, View(500), Rng());

            Assert.AreEqual(ShopChoiceKind.Reroll, choice.Kind);
            Assert.AreEqual(ShopStock.GearSection, choice.Section);
        }

        [Test]
        public void Lookahead2DoesNotRerollTheSameSectionTwice()
        {
            // Both live sections already rerolled once: the rule is one per
            // section per visit, so a shelf that is still dead is walked away
            // from rather than chased.
            var shop = Shelf(new[] { Gear(0, 20, 0f), Relic(0, 900, affordable: false) }, 500,
                rerollsUsed: new[] { 1, 0, 1 }, rerollPrices: new[] { 30, 15, 30 });

            var choice = new Lookahead2Policy().ChooseShop(shop, View(500), Rng());

            Assert.AreEqual(ShopChoiceKind.Leave, choice.Kind);
        }

        // The book shelf rolls NO OFFER until gate 3, so rerolling it buys a
        // second helping of nothing.
        [Test]
        public void Lookahead2NeverRerollsTheBookSection()
        {
            // Gear and relics both already rerolled, so the book shelf is the
            // only section left that a reroll rule could reach. It must not.
            var shop = Shelf(new[] { NoBook(), Gear(0, 20, 0f), Relic(0, 60, sold: true) }, 500,
                rerollsUsed: new[] { 1, 0, 1 }, rerollPrices: new[] { 30, 15, 30 });

            var choice = new Lookahead2Policy().ChooseShop(shop, View(500), Rng());

            Assert.AreEqual(ShopChoiceKind.Leave, choice.Kind);
        }

        [Test]
        public void Lookahead2WillNotRerollASectionItCannotPayFor()
        {
            var shop = Shelf(new[] { Gear(0, 20, 0f) }, 10);

            Assert.AreEqual(ShopChoiceKind.Leave,
                new Lookahead2Policy().ChooseShop(shop, View(10), Rng()).Kind);
        }

        // ---- RandomLegal ----------------------------------------------------

        // THE TERMINATION PROPERTY, not the distribution: the fuzzer's shop
        // rule exists to end, and a bias that stopped biasing would turn every
        // visit into a run at the driver's cap.
        [Test]
        public void RandomLegalLeavesOftenEnoughToEndAVisit()
        {
            var shop = Shelf(new[] { Gear(0, 20, 1f), Gear(1, 24, 1f), Relic(0, 60) }, 500);
            var policy = new RandomLegalPolicy();

            int leaves = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                if (policy.ChooseShop(shop, View(500), Rng(seed)).Kind == ShopChoiceKind.Leave) leaves++;
            }

            // A coin flip over 200 draws: anything outside this band is a
            // broken bias rather than an unlucky sample.
            Assert.That(leaves, Is.InRange(70, 130), "leaves out of 200 draws");
        }

        [Test]
        public void RandomLegalOnlyEverNamesALegalChoice()
        {
            var shop = Shelf(new[]
            {
                Gear(0, 20, 1f),
                Gear(1, 900, 1f, affordable: false),
                Gear(2, 20, 1f, sold: true),
                NoBook(),
                Relic(0, 60),
            }, 100, bag: new[] { Bag(0, "helm", 1f) });

            var policy = new RandomLegalPolicy();

            for (ulong seed = 1; seed <= 300; seed++)
            {
                var choice = policy.ChooseShop(shop, View(100), Rng(seed));
                switch (choice.Kind)
                {
                    case ShopChoiceKind.BuyGear:
                        Assert.AreEqual(0, choice.Index, "seed " + seed + " named an unbuyable gear card");
                        break;
                    case ShopChoiceKind.BuyRelic:
                        Assert.AreEqual(0, choice.Index);
                        break;
                    case ShopChoiceKind.Sell:
                        Assert.AreEqual(0, choice.Index);
                        break;
                    case ShopChoiceKind.Reroll:
                        Assert.That(choice.Section, Is.InRange(0, ShopStock.SectionCount - 1));
                        break;
                }
            }
        }

        [Test]
        public void RandomLegalLeavesWhenNothingIsLegal()
        {
            // Ten gold: nothing affordable, no bag, no reroll payable.
            var shop = Shelf(new[] { Gear(0, 900, 1f, affordable: false) }, 10);
            var policy = new RandomLegalPolicy();

            for (ulong seed = 1; seed <= 50; seed++)
            {
                Assert.AreEqual(ShopChoiceKind.Leave, policy.ChooseShop(shop, View(10), Rng(seed)).Kind);
            }
        }

        // ---- every archetype ------------------------------------------------

        [Test]
        public void EveryArchetypeAnswersAnEmptyShelfWithLeave()
        {
            var shop = Shelf(new ShopCardView[0], 0);

            foreach (string name in Archetypes.Names)
            {
                var policy = (IRunPolicy)Archetypes.Create(name);
                Assert.AreEqual(ShopChoiceKind.Leave, policy.ChooseShop(shop, View(0), Rng()).Kind, name);
            }
        }

        [Test]
        public void EveryArchetypeDeclaresARestThreshold()
        {
            // Pinned as literals rather than read back off the policies --
            // ShopNodePreference reads these, and a test that recomputed them
            // would agree with any value including a wrong one.
            var expected = new Dictionary<string, float>
            {
                { "RandomLegal", 0f },
                { "GreedyAggressive", 0.5f },
                { "GreedyDefensive", 0.75f },
                { "Lookahead2", 0.5f },
            };

            foreach (string name in Archetypes.Names)
            {
                var policy = (IRunPolicy)Archetypes.Create(name);
                Assert.AreEqual(expected[name], policy.RestBelowPartyHpFraction, 0.0001f, name);
            }
        }

        // ---- ShopNodePreference ---------------------------------------------

        private static DescentNode Node(int id, RoomType type) =>
            new DescentNode { Id = id, Depth = 1, Slot = 0, Type = type };

        [Test]
        public void WhenOfferedTakesTheShopNode()
        {
            var choices = new[] { Node(1, RoomType.Fight), Node(2, RoomType.Shop) };

            var picked = ShopNodePreference.PreferredNode(
                ShopNodeMode.WhenOffered, choices, View(0, hp: 1f), 0.75f);

            Assert.IsNotNull(picked);
            Assert.AreEqual(RoomType.Shop, picked.Type);
        }

        [Test]
        public void WhenOfferedDefersToTheArchetypeWhileThePartyIsHurtAndARestIsOffered()
        {
            var choices = new[] { Node(1, RoomType.Rest), Node(2, RoomType.Shop) };

            Assert.IsNull(ShopNodePreference.PreferredNode(
                ShopNodeMode.WhenOffered, choices, View(0, hp: 0.4f), 0.75f));
        }

        [Test]
        public void AHurtPartyStillTakesTheShopWhenThereIsNoRestToTake()
        {
            var choices = new[] { Node(1, RoomType.Fight), Node(2, RoomType.Shop) };

            Assert.IsNotNull(ShopNodePreference.PreferredNode(
                ShopNodeMode.WhenOffered, choices, View(0, hp: 0.1f), 0.75f));
        }

        [Test]
        public void NeverFiltersShopNodesOutBeforeThePolicyIsAsked()
        {
            var choices = new[] { Node(1, RoomType.Fight), Node(2, RoomType.Shop) };

            var narrowed = ShopNodePreference.ChoicesFor(ShopNodeMode.Never, choices);

            Assert.AreEqual(1, narrowed.Count);
            Assert.AreEqual(RoomType.Fight, narrowed[0].Type);
            Assert.IsNull(ShopNodePreference.PreferredNode(ShopNodeMode.Never, choices, View(0), 0f));
        }

        // A column of nothing but shops would otherwise leave the run with no
        // legal move -- a hang dressed as a baseline.
        [Test]
        public void NeverRefusesToNarrowTheChoiceListToNothing()
        {
            var choices = new[] { Node(1, RoomType.Shop) };

            var narrowed = ShopNodePreference.ChoicesFor(ShopNodeMode.Never, choices);

            Assert.AreEqual(1, narrowed.Count);
            Assert.AreEqual(RoomType.Shop, narrowed[0].Type);
        }

        [Test]
        public void WhenOfferedLeavesANonShopColumnAlone()
        {
            var choices = new[] { Node(1, RoomType.Fight), Node(2, RoomType.Treasure) };

            Assert.AreSame(choices, ShopNodePreference.ChoicesFor(ShopNodeMode.WhenOffered, choices));
            Assert.IsNull(ShopNodePreference.PreferredNode(
                ShopNodeMode.WhenOffered, choices, View(0), 0.5f));
        }

        [Test]
        public void TheModeNameRoundTrips()
        {
            Assert.AreEqual(ShopNodeMode.Never, ShopNodePreference.Parse("Never"));
            Assert.AreEqual(ShopNodeMode.Never, ShopNodePreference.Parse("never"));
            Assert.AreEqual(ShopNodeMode.WhenOffered, ShopNodePreference.Parse("WhenOffered"));

            // ANYTHING ELSE IS WhenOffered, deliberately: the batch runner
            // defaults the argument, and a typo that silently produced the
            // baseline would make a shop batch measure no shops at all.
            Assert.AreEqual(ShopNodeMode.WhenOffered, ShopNodePreference.Parse(""));
            Assert.AreEqual(ShopNodeMode.WhenOffered, ShopNodePreference.Parse("nonsense"));

            Assert.AreEqual("Never", ShopNodePreference.Name(ShopNodeMode.Never));
            Assert.AreEqual("WhenOffered", ShopNodePreference.Name(ShopNodeMode.WhenOffered));
        }
    }
}
