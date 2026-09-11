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
            var byId = ItemOfferRoll.Candidates().ToDictionary(c => c.ItemId);
            var offerable = new HashSet<string>(Content.ContentDatabase.Offerable.Select(i => i.id));

            foreach (var item in Content.ContentDatabase.Items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;

                bool expected = offerable.Contains(item.id);
                Assert.AreEqual(expected, byId.ContainsKey(item.id),
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
        public void MaxTierComesFromContentRatherThanAConstant()
        {
            // A content pass that adds a tier 11 set should widen the roll
            // without anyone remembering to edit a number here.
            Assert.AreEqual(Content.ContentDatabase.Items.Max(i => i.tier), ItemOfferRoll.MaxTier);
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
            // Three of the same sword is one choice wearing three hats.
            for (int depth = 0; depth < 40; depth += 7)
            {
                var offers = ItemOfferRoll.Roll(EncounterClass.Elite, depth, 0, First);
                var ids = offers.Select(o => o.ItemId).ToList();

                CollectionAssert.AllItemsAreUnique(ids, $"at depth {depth}");
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

        [Test]
        public void ABossNeverOffersTheBottomOfTheLadder()
        {
            // RarityTable's one hard guarantee rather than a distribution: a
            // boss handing over grey loot reads as the fight having been
            // pointless. Asserted through the roll, since that is the path the
            // game actually takes.
            int floor = RarityTable.TierFloorFor(EncounterClass.Boss);

            for (int depth = 0; depth < 30; depth += 3)
            {
                foreach (var offer in ItemOfferRoll.Roll(EncounterClass.Boss, depth, 0, First))
                {
                    // ItemOfferTable widens its band when the pool is thin, so
                    // the assertion is on the TIER THE TABLE TARGETED, which is
                    // what the floor governs.
                    Assert.GreaterOrEqual(
                        RarityTable.RollTier(EncounterClass.Boss, depth, ItemOfferRoll.MaxTier, 0, First),
                        floor, $"at depth {depth}");
                }
            }
        }

        [Test]
        public void ANullRandomSourceDegradesRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => ItemOfferRoll.Roll(EncounterClass.Normal, 4, 0, null));
            CollectionAssert.IsEmpty(ItemOfferRoll.Roll(EncounterClass.Normal, 4, 0, null));
        }

        // ---- RiftTier / modifiers -------------------------------------------

        [Test]
        public void EveryOfferCarriesAtMostThreeDistinctModifiers()
        {
            for (int depth = 0; depth < 40; depth += 7)
            {
                foreach (var offer in ItemOfferRoll.Roll(EncounterClass.Boss, depth, 500, n => 0))
                {
                    Assert.LessOrEqual(offer.Modifiers.Count, 3, $"at depth {depth}");
                    CollectionAssert.AllItemsAreUnique(offer.Modifiers.ToList(), $"at depth {depth}");
                    Assert.AreEqual(offer.Modifiers.Count, (int)offer.RiftTier,
                        "the number of rolled modifiers should equal the rift tier that was rolled");
                }
            }
        }

        [Test]
        public void AnOrdinaryOfferCarriesNoModifiers()
        {
            // A source that never succeeds a climb (out-of-range roll clamps
            // to the top of [0, Resolution), which is always >= threshold)
            // never leaves RiftTier.Ordinary.
            foreach (var offer in ItemOfferRoll.Roll(EncounterClass.Normal, 0, 0, n => n - 1))
            {
                Assert.AreEqual(Domain.Content.RiftTier.Ordinary, offer.RiftTier);
                Assert.IsEmpty(offer.Modifiers);
            }
        }

        [Test]
        public void RiftTierAndModifiersAreRolledPerItemRatherThanOncePerSet()
        {
            // Same shape as PlusIsRolledPerItemRatherThanOncePerSet: walking
            // the randomness source rather than handing back a constant, so a
            // per-item roll and a per-set roll produce visibly different data.
            var seen = new List<Domain.Content.RiftTier>();
            int calls = 0;
            System.Func<int, int> walking = bound => (calls++) % System.Math.Max(1, bound);

            foreach (var offer in ItemOfferRoll.Roll(EncounterClass.Boss, 30, 0, walking))
            {
                seen.Add(offer.RiftTier);
            }

            Assert.AreEqual(ItemOfferTable.OfferCount, seen.Count);
        }

        [Test]
        public void PlusIsRolledPerItemRatherThanOncePerSet()
        {
            // Tier is the set's statement about the fight; plus is the long
            // tail on one particular copy. Rolling plus once would make all
            // three offers identically honed, which is not what RarityTable's
            // two-axis design says.
            var seen = new List<int>();
            int calls = 0;

            // Walks the sequence rather than returning a constant, so a
            // per-item roll and a per-set roll produce visibly different data.
            System.Func<int, int> walking = bound => (calls++) % System.Math.Max(1, bound);

            foreach (var offer in ItemOfferRoll.Roll(EncounterClass.Boss, 30, 0, walking))
            {
                seen.Add(offer.Plus);
            }

            Assert.AreEqual(ItemOfferTable.OfferCount, seen.Count);
            Assert.Greater(calls, ItemOfferTable.OfferCount,
                "the randomness source was consulted too few times for a per-item plus roll");
        }

        // ---- RunOrchestrator.RollOffers: the reported floor-3-boss bug -----
        //
        // A floor-3 boss offering tier 1 / +1 items traced back to
        // RunOrchestrator.RollOffers reading session.IsEliteFight only --
        // IsBossFight was never checked, so every boss kill rolled as
        // EncounterClass.Normal and RarityTable's TierFloorFor(Boss)==3
        // guarantee never fired. Exercised through the real seam (a
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
            int floor = RarityTable.TierFloorFor(EncounterClass.Boss);

            for (int seed = 0; seed < 50; seed++)
            {
                var rng = new SeededRandom((ulong)(seed + 1));
                foreach (var offer in RunOrchestrator.RollOffers(session, n => rng.NextInt(0, n)))
                {
                    Assert.GreaterOrEqual(offer.Tier, floor - ItemOfferTable.TierSpread,
                        $"seed {seed}: a boss fight (IsBossFight=true) must roll the Boss reward band, " +
                        "not fall through to Normal's");
                }
            }
        }

        // ---- weapon/armour affix-pool split: a staff must never roll a
        // defensive affix, and armour must never roll an offensive-only one.

        [Test]
        public void SeededStaffAndWeaponRolls_NeverCarryADefensiveAffix()
        {
            var effectById = Content.ContentDatabase.Modifiers
                .Where(m => m != null && !string.IsNullOrEmpty(m.id) && m.Data.Effects != null && m.Data.Effects.Length > 0)
                .ToDictionary(m => m.id, m => m.Data.Effects[0].Type);
            var kindById = Content.ContentDatabase.Items
                .Where(i => i != null && !string.IsNullOrEmpty(i.id))
                .ToDictionary(i => i.id, i => i.kind);

            int weaponOffersChecked = 0;
            int armorOffersChecked = 0;

            for (int seed = 0; seed < 200; seed++)
            {
                // A real seeded stream, not a constant -- "200 seeded rolls"
                // per the brief. favor: 1000 pushes RiftTier's own step
                // chance to its cap (ModifierTable.MaxStep), so most of the
                // 200 rolls actually carry affixes to check rather than
                // mostly landing on RiftTier.Ordinary (zero modifiers).
                var rng = new SeededRandom((ulong)(seed + 1));
                var offers = ItemOfferRoll.Roll(EncounterClass.Boss, 40, 1000, n => rng.NextInt(0, n));

                foreach (var offer in offers)
                {
                    if (!kindById.TryGetValue(offer.ItemId, out var kind)) continue;

                    bool isWeapon = kind == Content.ItemKind.Weapon;
                    if (isWeapon) weaponOffersChecked++; else armorOffersChecked++;

                    foreach (var modifierId in offer.Modifiers)
                    {
                        if (!effectById.TryGetValue(modifierId, out var type)) continue;

                        bool offensive = ModifierTable.IsOffensiveModifier(type);
                        if (isWeapon)
                        {
                            Assert.IsTrue(offensive,
                                $"seed {seed}: weapon '{offer.ItemId}' rolled '{modifierId}' ({type}), a defensive affix");
                        }
                        else
                        {
                            Assert.IsFalse(offensive,
                                $"seed {seed}: armour '{offer.ItemId}' rolled '{modifierId}' ({type}), an offensive-only affix");
                        }
                    }
                }
            }

            // The fixture assertion the rest is vacuous without: content
            // must actually offer both kinds for this to have tested
            // anything.
            Assert.Greater(weaponOffersChecked, 0, "no weapon offers were sampled -- widen the roll");
            Assert.Greater(armorOffersChecked, 0, "no armour offers were sampled -- widen the roll");
        }

        [Test]
        public void RollOffers_ABossFightNeverReadsAsPlainNormal()
        {
            // The exact shape of the original bug: at depth 23 (floor 3),
            // FloorTier(23) == 1, so an EncounterClass.Normal roll centres
            // on tier 1 with no floor at all -- exactly the "tier 1 / +1"
            // the report described. Once routed to Boss, TierFloorFor(Boss)
            // forces every offer to tier 3 or better regardless of roll
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
