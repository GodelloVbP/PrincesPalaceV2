using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // The one Core-side step that was missing between generated content and a
    // fully tested reward table.
    //
    // PlayMode rather than EditMode because it reads ContentDatabase, which is
    // a Core type loaded from Resources. The RULES it calls -- RarityTable,
    // ItemOfferTable -- are Domain and already covered without a scene; what is
    // left here is the translation those two could never do for themselves,
    // and which is why they had no production caller at all until now.
    public class ItemOfferRollTests
    {
        // A deterministic stand-in for Random.Range: always the first element.
        // Enough to make the SHAPE of a roll assertable without asserting on a
        // distribution, which RarityTableTests already owns.
        private static int First(int bound) => 0;

        [Test]
        public void ContentOffersSomethingToRoll()
        {
            // Everything below is vacuous if the pool is empty, so this is the
            // fixture assertion the rest depends on.
            Assert.IsNotEmpty(ItemOfferRoll.Candidates());
        }

        // REWRITTEN FROM OnlyEquippablesAreOffered (AUDIT #114, owner's call
        // 2026-09-11). That test asserted `IsEquippable`, which is what
        // Candidates() filtered on, and the two agreed with each other and
        // with nothing else: ContentDatabase.Offerable had said for months
        // that the hand-authored one-offs in items.json are not rewards
        // because they "would otherwise turn up as a 'reward' the player
        // already owns six of", and the roll drew from them anyway.
        //
        // The predicate is "was this generated with a tier", not the kind --
        // an item with no tier cannot be ranked on the axis the offer screen
        // scales by. Asserted against Offerable rather than restated here, so
        // a content pass that adds a generated family widens both at once.
        [Test]
        public void OnlyOfferableItemsAreOffered()
        {
            var candidateIds = new HashSet<string>(ItemOfferRoll.Candidates().Select(c => c.ItemId));
            var offerable = new HashSet<string>(Content.ContentDatabase.Offerable.Select(i => i.id));

            foreach (var item in Content.ContentDatabase.Items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;

                bool expected = offerable.Contains(item.id);
                Assert.AreEqual(expected, candidateIds.Contains(item.id),
                    $"{item.id} is {(expected ? "offerable but not offered" : "offered but not offerable")}");
            }
        }

        // The half of #114 the repro is written about: the starting kit is
        // gear the player is already wearing on the first floor, and it sat in
        // exactly the tier band floor 1 rolls from. Stated separately from the
        // Offerable comparison above because it is the rule a content author
        // would go looking for, and because it keeps saying something the day
        // items.json carries a startingStock row again.
        [Test]
        public void TheStartingKitIsNeverOfferedAsAReward()
        {
            var offered = new HashSet<string>(ItemOfferRoll.Candidates().Select(c => c.ItemId));

            foreach (var item in Content.ContentDatabase.StartingStock)
            {
                Assert.IsFalse(offered.Contains(item.id),
                    $"'{item.id}' is granted to every new profile and is also on the reward table");
            }
        }

        [Test]
        public void MaxTierIsTheCopyCap()
        {
            Assert.AreEqual(ItemInstance.TierCap, ItemOfferRoll.MaxTier);
        }

        [Test]
        public void ARollProducesTheOfferCountTheScreenBuildsFor()
        {
            var offers = ItemOfferRoll.Roll(EncounterClass.Normal, 8, 0, First);

            Assert.AreEqual(ItemOfferTable.OfferCount, offers.Count);
        }

        [Test]
        public void TheSameItemIsNeverOfferedTwiceInOneRoll()
        {
            // Three of the same sword is one choice wearing three hats. The
            // draw is without replacement by (base, tier): the same base at
            // another tier is another copy, as the per-tier assets were.
            for (int depth = 0; depth < 40; depth += 7)
            {
                var offers = ItemOfferRoll.Roll(EncounterClass.Elite, depth, 0, First);
                var copies = offers.Select(o => $"{o.ItemId}@{o.Tier}").ToList();

                CollectionAssert.AllItemsAreUnique(copies, $"at depth {depth}");
            }
        }

        [Test]
        public void EveryOfferNamesRealContent()
        {
            var offers = ItemOfferRoll.Roll(EncounterClass.Boss, 24, 0, First);

            foreach (var offer in offers)
            {
                Assert.IsNotNull(Content.ContentDatabase.GetItem(offer.ItemId),
                    $"offered '{offer.ItemId}', which is not in the database");
            }
        }

        // A boss centres one tier above the depth (boss_gear tierOffset 1);
        // the ladder only climbs, so nothing pays below that centre.
        [Test]
        public void ABossNeverOffersBelowItsCentre()
        {
            for (int depth = 0; depth < 30; depth += 3)
            {
                foreach (var offer in ItemOfferRoll.Roll(EncounterClass.Boss, depth, 0, First))
                {
                    Assert.GreaterOrEqual(offer.Tier, depth / 16 + 1, $"at depth {depth}");
                }
            }
        }

        [Test]
        public void ANullRandomSourceDegradesRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => ItemOfferRoll.Roll(EncounterClass.Normal, 4, 0, null));
            CollectionAssert.IsEmpty(ItemOfferRoll.Roll(EncounterClass.Normal, 4, 0, null));
        }

        // ---- RunOrchestrator.RollOffers: the reported floor-3-boss bug -----
        //
        // A floor-3 boss offering tier 1 / +1 items traced back to
        // RunOrchestrator.RollOffers reading session.IsEliteFight only --
        // IsBossFight was never checked, so every boss kill rolled as
        // EncounterClass.Normal and the boss row's dials never applied. Exercised through the real seam (a
        // FightSession with IsBossFight true) rather than by calling
        // ItemOfferRoll directly with EncounterClass.Boss, which would pass
        // even with the bug still in place -- this is the test that would
        // have caught it.
        private static Domain.Combat.Session.FightSession BossSession()
        {
            var hero = new CombatantState("Hero", true, 100, 10, 20, 5);
            var foe = new CombatantState("Boss", false, 500, 0, 40, 3);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var session = new Domain.Combat.Session.FightSession(
                encounter, null, null, new SeededRandom(1), isBossFight: true, isEliteFight: false)
            {
                DepthStep = 23,
            };
            return session;
        }

        [Test]
        public void RollOffers_RoutesABossKillToTheBossRewardBand()
        {
            var session = BossSession();
            // Depth 23 centres a boss on tier 2 (FloorTier 1, +1).
            const int floor = 2;

            for (int seed = 0; seed < 50; seed++)
            {
                var rng = new SeededRandom((ulong)(seed + 1));
                foreach (var offer in RunOrchestrator.RollOffers(session, n => rng.NextInt(0, n)))
                {
                    Assert.GreaterOrEqual(offer.Tier, floor,
                        $"seed {seed}: a boss fight (IsBossFight=true) must roll the Boss reward band, " +
                        "not fall through to Normal's");
                }
            }
        }

        // ---- the keyed offer stream (RngStreams.Reward) ----------------------

        private static string[] OfferIds(ulong seed, int step, int node, int pick) =>
            RunOrchestrator.RollOffers(BossSession(), seed, step, node, pick)
                .Select(o => $"{o.ItemId}@{o.Tier}").ToArray();

        [Test]
        public void RollOffers_SameKeyGivesSameOffers_DifferentPickRollsApart()
        {
            var first = OfferIds(12345UL, 23, 5, 0);
            CollectionAssert.AreEqual(first, OfferIds(12345UL, 23, 5, 0),
                "the same (seed, step, node, pick) must show the same offers");
            string all = string.Join(",", first) + " / " + string.Join(",", OfferIds(12345UL, 23, 5, 1)) + " / " +
                         string.Join(",", OfferIds(12345UL, 23, 6, 0));
            CollectionAssert.AreEqual(
                new[] { "brigandine_gloves@3", "brigandine_boots@3", "silk_gloves@3" }, first, all);
            CollectionAssert.AreEqual(
                new[] { "court_coronet@2", "vellum_hood@2", "regalia_slippers@2" },
                OfferIds(12345UL, 23, 5, 1), all);
            CollectionAssert.AreEqual(
                new[] { "wool_jumper@2", "bulwark_tassets@2", "wool_hood@2" },
                OfferIds(12345UL, 23, 6, 0), all);
        }

        // ---- weapon/armour affix-pool split: a staff must never roll a
        // defensive affix, and armour must never roll an offensive-only one.

        [Test]
        public void RollOffers_ABossFightNeverReadsAsPlainNormal()
        {
            // The exact shape of the original bug: at depth 23 (floor 3),
            // FloorTier(23) == 1, so an EncounterClass.Normal roll centres
            // on tier 1 with no floor at all -- exactly the "tier 1 / +1"
            // the report described. Once routed to Boss, boss_gear's +1
            // centre puts every offer at tier 2 or better regardless of roll
            // luck, so the Normal-band low end (tier 0-1) must never appear.
            var session = BossSession();
            var rng = new SeededRandom(20260903);
            var nextIndex = new System.Func<int, int>(n => rng.NextInt(0, n));

            var offers = RunOrchestrator.RollOffers(session, nextIndex);
            Assert.IsNotEmpty(offers);
            foreach (var offer in offers)
            {
                Assert.GreaterOrEqual(offer.Tier, 2,
                    "a boss offer at floor 3 landed in the Normal band this fix exists to close off");
            }
        }
    }
}
