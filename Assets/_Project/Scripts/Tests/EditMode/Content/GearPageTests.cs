using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The gear page pipeline (docs/PLAN_ITEMS.md 1.2 steps 1-8), on a
    // literal catalogue and the shipped rewards.json dials typed as literals.
    public class GearPageTests
    {
        private sealed class FakeCatalog : IGearCatalog
        {
            public Func<string, string, int, EquipmentSlot, bool> FitsRule = (_, _, _, _) => true;

            // Trait ids the member may be offered; the default is the whole pool.
            public Func<string, EquipmentSlot, IReadOnlyList<string>> TraitRule;

            private static readonly string[] Weapons = { "sword", "axe", "mace", "staff" };

            private static readonly Dictionary<EquipmentSlot, string[]> Armour = new Dictionary<EquipmentSlot, string[]>
            {
                [EquipmentSlot.Head] = new[] { "hood", "helm", "coif" },
                [EquipmentSlot.Torso] = new[] { "vest", "mail", "robe" },
                [EquipmentSlot.Legs] = new[] { "greaves", "trews" },
                [EquipmentSlot.Shoes] = new[] { "boots", "slippers" },
                [EquipmentSlot.Gloves] = new[] { "gauntlets", "mitts" },
            };

            public IReadOnlyList<string> BasesFor(EquipmentSlot slot)
            {
                if (EquipmentSlots.IsWeaponSlot(slot)) return Weapons;
                return Armour.TryGetValue(slot, out var list) ? list : Array.Empty<string>();
            }

            public bool Fits(string memberId, string baseId, int tier, EquipmentSlot slot) =>
                FitsRule(memberId, baseId, tier, slot);

            public IReadOnlyList<string> AffixPool(EquipmentSlot slot) =>
                EquipmentSlots.IsWeaponSlot(slot)
                    ? new[] { "w_fire", "w_bleed", "w_crit", "w_haste" }
                    : new[] { "a_ward", "a_thorns", "a_vigor", "a_resist" };

            public IReadOnlyList<string> TraitsFor(string memberId, EquipmentSlot slot) =>
                TraitRule != null
                    ? TraitRule(memberId, slot)
                    : EquipmentSlots.IsWeaponSlot(slot)
                        ? new[] { "w_t_dual", "w_t_rune", "w_t_deck" }
                        : new[] { "a_t_shield", "a_t_pipe" };
        }

        private static readonly ResolvedRewardSource NormalGear = new ResolvedRewardSource
        {
            Id = "normal_gear", Kind = RewardPageKind.Gear, ExtraCards = 2, TierOffset = 0, TierStep = 0.22f,
            LadderStep = 0.35f, QualityFloor = Quality.Common,
        };

        private static readonly ResolvedRewardSource AnchorGear = new ResolvedRewardSource
        {
            Id = "anchor_gear", Kind = RewardPageKind.Gear, ExtraCards = 2, TierOffset = 0, TierStep = 0.30f,
            LadderStep = 0.42f, QualityFloor = Quality.Common, WeaponGuarantee = true,
        };

        private static readonly ResolvedRewardSource BossGear = new ResolvedRewardSource
        {
            Id = "boss_gear", Kind = RewardPageKind.Gear, ExtraCards = 2, TierOffset = 1, TierStep = 0.38f,
            LadderStep = 0.45f, QualityFloor = Quality.Uncommon, WeaponGuarantee = true, WeaponFloor = Quality.Rare,
        };

        private static ItemInstance T(int tier) => ItemInstance.Of("worn", tier);

        // Wears a weapon and every armour slot but the ones named empty.
        private static PageMember Member(string id, int tier = 0, params EquipmentSlot[] empty)
        {
            var worn = EquipmentSlots.All
                .Where(s => !empty.Contains(s))
                .Select(s => new KeyValuePair<EquipmentSlot, ItemInstance>(s, T(tier)));
            return new PageMember(id, worn);
        }

        private static GearPageRequest Request(ResolvedRewardSource source, params PageMember[] members) =>
            new GearPageRequest { Source = source, Chips = 1, DepthStep = 40, Members = members };

        private static Func<int, int> Seeded(ulong seed)
        {
            var rng = new SeededRandom(seed);
            return n => rng.NextInt(0, n);
        }

        // ---- step 1: owners ----

        [Test]
        public void Owners_ThreeOrMore_SquadOrderCycling()
        {
            var a = Member("a"); var b = Member("b"); var c = Member("c");

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, GearPage.Owners(new[] { a, b, c }, 3).Select(m => m.Id));
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "a", "b" }, GearPage.Owners(new[] { a, b, c }, 5).Select(m => m.Id));
        }

        [Test]
        public void Owners_FewerThanThree_StartWithTheMostEmptySlots()
        {
            var a = Member("a", 0, EquipmentSlot.Head);
            var b = Member("b", 0, EquipmentSlot.Head, EquipmentSlot.Shoes);

            CollectionAssert.AreEqual(new[] { "b", "a", "b" }, GearPage.Owners(new[] { a, b }, 3).Select(m => m.Id));
        }

        [Test]
        public void Owners_TiedEmptySlots_LowestSummedTierFirst_ThenSquadOrder()
        {
            var a = Member("a", 2, EquipmentSlot.Head);
            var b = Member("b", 1, EquipmentSlot.Head);
            CollectionAssert.AreEqual(new[] { "b", "a", "b" }, GearPage.Owners(new[] { a, b }, 3).Select(m => m.Id));

            var c = Member("c", 1, EquipmentSlot.Head);
            CollectionAssert.AreEqual(new[] { "b", "c", "b" }, GearPage.Owners(new[] { b, c }, 3).Select(m => m.Id));
        }

        // ---- step 2: the weapon card ----

        [Test]
        public void WeaponCard_GoesToAnEmptyWeapon1First_ElseTheLowestTierWeapon()
        {
            var full3 = Member("a", 3); var full1 = Member("b", 1); var bare = Member("c", 5, EquipmentSlot.Weapon1);

            Assert.AreEqual(2, GearPage.WeaponCardIndex(new[] { full3, full1, bare }));
            Assert.AreEqual(1, GearPage.WeaponCardIndex(new[] { full3, full1, Member("d", 3) }));
        }

        [Test]
        public void WeaponTarget_EmptyWeapon1_ThenEmptyWeapon2_ThenTheLowerTier()
        {
            Assert.AreEqual(EquipmentSlot.Weapon1, GearPage.WeaponTarget(Member("a", 0, EquipmentSlot.Weapon1)));
            Assert.AreEqual(EquipmentSlot.Weapon2, GearPage.WeaponTarget(Member("a", 0, EquipmentSlot.Weapon2)));

            var mixed = new PageMember("m", new[]
            {
                new KeyValuePair<EquipmentSlot, ItemInstance>(EquipmentSlot.Weapon1, T(1)),
                new KeyValuePair<EquipmentSlot, ItemInstance>(EquipmentSlot.Weapon2, T(4)),
            });
            Assert.AreEqual(EquipmentSlot.Weapon1, GearPage.WeaponTarget(mixed));
            Assert.AreEqual(EquipmentSlot.Weapon2, GearPage.WeaponTarget(Member("t", 2)), "a tie replaces the off hand");
        }

        [Test]
        public void AnAnchorPage_HasExactlyOneGuaranteedWeaponCard()
        {
            var roll = GearPage.Roll(Request(AnchorGear, Member("a", 3), Member("b", 1), Member("c", 2)),
                new FakeCatalog(), Seeded(7));

            Assert.AreEqual(1, roll.Cards.Count(c => c.GuaranteedWeapon));
            var weapon = roll.Cards.Single(c => c.GuaranteedWeapon);
            Assert.AreEqual("b", weapon.OwnerId);
            Assert.IsTrue(EquipmentSlots.IsWeaponSlot(weapon.Slot));
        }

        // ---- step 3: slots ----

        [Test]
        public void AnEmptyWeapon1_AlwaysGetsAWeaponCard()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var roll = GearPage.Roll(Request(NormalGear, Member("a", 0, EquipmentSlot.Weapon1), Member("b"), Member("c")),
                    new FakeCatalog(), Seeded(seed));
                Assert.AreEqual(EquipmentSlot.Weapon1, roll.Cards[0].Slot, $"seed {seed}");
            }
        }

        [Test]
        public void TheSlotDraw_WeighsEmptySlotsThreeToOne_AndSkipsSlotsWithNoBases()
        {
            // Head and Shoes empty: weights Head 3, Torso 1, Legs 1, Shoes 3,
            // Gloves 1, Weapon1 1, Weapon2 1 = 11. Necklace has no base.
            var owner = Member("a", 0, EquipmentSlot.Head, EquipmentSlot.Shoes);
            var seen = new Dictionary<EquipmentSlot, int>();
            for (int roll = 0; roll < 11; roll++)
            {
                int fixedRoll = roll;
                var slot = GearPage.DrawSlot(owner, new FakeCatalog(), _ => fixedRoll);
                seen[slot] = seen.TryGetValue(slot, out var n) ? n + 1 : 1;
            }

            Assert.AreEqual(3, seen[EquipmentSlot.Head]);
            Assert.AreEqual(3, seen[EquipmentSlot.Shoes]);
            Assert.AreEqual(1, seen[EquipmentSlot.Torso]);
            Assert.IsFalse(seen.ContainsKey(EquipmentSlot.Necklace));
        }

        // ---- page shape, bases ----

        [TestCase(1, 3)]
        [TestCase(2, 4)]
        [TestCase(3, 5)]
        public void APageOfKChips_ShowsKPlusTwoCards(int chips, int cards)
        {
            var request = Request(NormalGear, Member("a"), Member("b"), Member("c"));
            request.Chips = chips;

            Assert.AreEqual(cards, GearPage.Roll(request, new FakeCatalog(), Seeded(3)).Cards.Count);
        }

        // Four weapon bases, four weapon cards. A fifth would reuse one: a
        // thin pool degrades rather than leaving a card blank.
        [Test]
        public void BasesAreDistinctAcrossThePage()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var request = Request(NormalGear, Member("a", 0, EquipmentSlot.Weapon1), Member("b", 0, EquipmentSlot.Weapon1));
                request.Chips = 2;
                var ids = GearPage.Roll(request, new FakeCatalog(), Seeded(seed)).Cards.Select(c => c.Copy.ItemId).ToList();

                CollectionAssert.AllItemsAreUnique(ids, $"seed {seed}");
            }
        }

        [Test]
        public void ABaseThatFits_IsPreferred_AndNoneFittingShowsInert()
        {
            var catalog = new FakeCatalog { FitsRule = (_, id, _, _) => id == "axe" };
            var roll = GearPage.Roll(Request(NormalGear, Member("a", 0, EquipmentSlot.Weapon1)), catalog, Seeded(11));

            Assert.AreEqual("axe", roll.Cards[0].Copy.ItemId);
            Assert.IsFalse(roll.Cards[0].Inert);

            catalog.FitsRule = (_, _, _, _) => false;
            roll = GearPage.Roll(Request(NormalGear, Member("a", 0, EquipmentSlot.Weapon1)), catalog, Seeded(11));
            Assert.IsTrue(roll.Cards.All(c => c.Inert));
        }

        // ---- step 6: quality ----

        private const int QualitySamples = 20000;

        private static int[] QualityCounts(ResolvedRewardSource source, int favor)
        {
            var next = Seeded(20261014);
            var counts = new int[4];
            for (int i = 0; i < QualitySamples; i++) counts[(int)GearPage.RollQuality(source, favor, next)]++;
            return counts;
        }

        // Pinned on one seed, and within 1.5 points of the plan's table.
        [TestCase("normal", new[] { 13079, 4485, 1575, 861 }, new[] { 0.65, 0.228, 0.080, 0.043 })]
        [TestCase("anchor", new[] { 11714, 4810, 2016, 1460 }, new[] { 0.58, 0.244, 0.102, 0.074 })]
        [TestCase("boss", new[] { 0, 10988, 5022, 3990 }, new[] { 0.0, 0.55, 0.248, 0.203 })]
        public void QualityTable_AtFavorZero(string name, int[] pinned, double[] plan)
        {
            var source = name == "normal" ? NormalGear : name == "anchor" ? AnchorGear : BossGear;
            var counts = QualityCounts(source, 0);

            CollectionAssert.AreEqual(pinned, counts, $"{name}: [{string.Join(", ", counts)}]");
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(plan[i], counts[i] / (double)QualitySamples, 0.015, $"{name} {(Quality)i}");
            }
        }

        [Test]
        public void QualityFavor_StopsAt055()
        {
            var counts = QualityCounts(NormalGear, 500);
            Assert.AreEqual(0.45, counts[0] / (double)QualitySamples, 0.015, "Common = 1 - 0.55");
        }

        // ---- step 7: floors and pity ----

        private static GearPageRoll RollWith(ResolvedRewardSource source, Action<GearPageRequest> edit, ulong seed)
        {
            var request = Request(source, Member("a"), Member("b"), Member("c"));
            edit?.Invoke(request);
            return GearPage.Roll(request, new FakeCatalog(), Seeded(seed));
        }

        [Test]
        public void Strength_RaisesTheBestCard()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var two = RollWith(NormalGear, r => r.Strength = 2, seed);
                var three = RollWith(NormalGear, r => r.Strength = 3, seed);
                Assert.GreaterOrEqual(two.Cards[two.BestIndex].Rolled, Quality.Uncommon, $"seed {seed}");
                Assert.GreaterOrEqual(three.Cards[three.BestIndex].Rolled, Quality.Rare, $"seed {seed}");
            }
        }

        [Test]
        public void AfterACommonBestPage_TheNextBestCardIsAtLeastUncommon()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var roll = RollWith(NormalGear, r => r.LastPageBestCommon = true, seed);
                Assert.GreaterOrEqual(roll.Cards[roll.BestIndex].Rolled, Quality.Uncommon, $"seed {seed}");
                Assert.IsFalse(roll.BestWasCommon);
            }
        }

        [Test]
        public void TheBossWeaponCard_IsAtLeastRare()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var roll = RollWith(BossGear, null, seed);
                Assert.GreaterOrEqual(roll.Cards.Single(c => c.GuaranteedWeapon).Rolled, Quality.Rare, $"seed {seed}");
            }
        }

        [Test]
        public void TheSeventhPageWithoutALegendary_MakesItsBestCardLegendary()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sixth = RollWith(NormalGear, r => r.LegendaryPity = 5, seed);
                bool sixthHadOne = sixth.Cards.Any(c => c.Rolled == Quality.Legendary);
                Assert.AreEqual(sixthHadOne ? 0 : 6, sixth.LegendaryPity, $"seed {seed}");

                var seventh = RollWith(NormalGear, r => r.LegendaryPity = 6, seed);
                Assert.AreEqual(Quality.Legendary, seventh.Cards[seventh.BestIndex].Rolled, $"seed {seed}");
                Assert.AreEqual(0, seventh.LegendaryPity);
            }
        }

        // ---- step 8: affixes ----

        [Test]
        public void AffixCount_FollowsQuality_LegendaryIsTwoPlusATrait()
        {
            Assert.AreEqual(0, GearPage.AffixCount(Quality.Common));
            Assert.AreEqual(1, GearPage.AffixCount(Quality.Uncommon));
            Assert.AreEqual(2, GearPage.AffixCount(Quality.Rare));
            Assert.AreEqual(2, GearPage.AffixCount(Quality.Legendary));
            Assert.AreEqual(Quality.Legendary, ItemQuality.Of(2, 1));
            Assert.AreEqual(Quality.Rare, ItemQuality.Of(3, 0), "no trait left: a third affix, read as Rare");
        }

        [Test]
        public void AffixesAreDistinct_AndDrawnFromTheSlotsPool()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                foreach (var card in RollWith(BossGear, r => r.Chips = 3, seed).Cards)
                {
                    string prefix = EquipmentSlots.IsWeaponSlot(card.Slot) ? "w_" : "a_";
                    int traits = card.Rolled == Quality.Legendary ? 1 : 0;
                    Assert.AreEqual(GearPage.AffixCount(card.Rolled) + traits, card.Copy.ModifierIds.Count, $"seed {seed}");
                    CollectionAssert.AllItemsAreUnique(card.Copy.ModifierIds);
                    Assert.IsTrue(card.Copy.ModifierIds.All(m => m.StartsWith(prefix)), $"seed {seed} {card.Slot}");
                }
            }
        }

        // ---- step 8: the Legendary trait ----

        private static bool IsTrait(string id) => id.Contains("_t_");

        // Owner decision Q6: the top quality of ANY slot carries a trait.
        [Test]
        public void EveryLegendary_CarriesTwoAffixesAndOneTraitFromItsPool()
        {
            int legendaries = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                foreach (var card in RollWith(BossGear, r => r.Chips = 3, seed).Cards)
                {
                    if (card.Rolled != Quality.Legendary) continue;
                    legendaries++;

                    string prefix = EquipmentSlots.IsWeaponSlot(card.Slot) ? "w_" : "a_";
                    var traits = card.Copy.ModifierIds.Where(IsTrait).ToList();
                    Assert.AreEqual(1, traits.Count, $"seed {seed} {card.Slot}");
                    Assert.IsTrue(traits[0].StartsWith(prefix), $"seed {seed} {card.Slot}: {traits[0]}");
                    Assert.AreEqual(2, card.Copy.ModifierIds.Count(m => !IsTrait(m)), $"seed {seed}: two affixes beside the trait");
                }
            }

            Assert.Greater(legendaries, 10, "the sweep must reach Legendary cards to mean anything");
        }

        [Test]
        public void ATraitTheOwnerCannotUseOrAlreadyGets_IsNeverDrawn()
        {
            var catalog = new FakeCatalog
            {
                TraitRule = (member, slot) => EquipmentSlots.IsWeaponSlot(slot)
                    ? new[] { "w_t_rune" }
                    : Array.Empty<string>(),
            };

            int weaponLegendaries = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var request = Request(BossGear, Member("a"), Member("b"), Member("c"));
                request.Chips = 3;
                foreach (var card in GearPage.Roll(request, catalog, Seeded(seed)).Cards)
                {
                    if (card.Rolled != Quality.Legendary) continue;

                    var traits = card.Copy.ModifierIds.Where(IsTrait).ToList();
                    if (EquipmentSlots.IsWeaponSlot(card.Slot))
                    {
                        weaponLegendaries++;
                        if (traits.Count > 0)
                        {
                            CollectionAssert.AreEqual(new[] { "w_t_rune" }, traits, $"seed {seed}");
                        }
                        else
                        {
                            // The page already gave this owner the only trait.
                            Assert.AreEqual(3, card.Copy.ModifierIds.Count, $"seed {seed}");
                        }
                    }
                    else
                    {
                        Assert.IsEmpty(traits, $"seed {seed}: no trait offered, so none drawn");
                        Assert.AreEqual(3, card.Copy.ModifierIds.Count, $"seed {seed}: Rare with a third affix");
                        Assert.AreEqual(Quality.Rare, ItemQuality.Of(card.Copy.ModifierIds.Count, 0));
                    }
                }
            }

            Assert.Greater(weaponLegendaries, 0);
        }

        [Test]
        public void TwoLegendariesForOneOwnerOnAPage_NeverShareATrait()
        {
            var catalog = new FakeCatalog { TraitRule = (_, slot) => new[] { "w_t_dual", "w_t_rune" } };
            int pairs = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var request = Request(BossGear, Member("solo"));
                request.Chips = 2;
                var legendaryTraits = GearPage.Roll(request, catalog, Seeded(seed)).Cards
                    .Where(c => c.Rolled == Quality.Legendary)
                    .Select(c => c.Copy.ModifierIds.FirstOrDefault(IsTrait))
                    .Where(t => t != null)
                    .ToList();
                if (legendaryTraits.Count < 2) continue;

                pairs++;
                CollectionAssert.AllItemsAreUnique(legendaryTraits, $"seed {seed}");
            }

            Assert.Greater(pairs, 0);
        }

        // ---- the whole page, pinned ----

        [Test]
        public void ASeededBossPage_IsPinned()
        {
            var request = Request(BossGear, Member("shawn", 2), Member("odette", 1, EquipmentSlot.Head), Member("bjorn", 2));
            request.DepthStep = 23;
            request.Favor = 4;
            request.Strength = 2;

            var cards = GearPage.Roll(request, new FakeCatalog(), Seeded(12345)).Cards
                .Select(c => $"{c.OwnerId}:{c.Slot}:{c.Copy}:{c.Rolled}")
                .ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "shawn:Weapon2:sword t2 [w_haste]:Uncommon",
                "odette:Weapon2:axe t2 [w_haste,w_fire,w_t_rune]:Legendary",
                "bjorn:Shoes:slippers t2 [a_ward,a_thorns]:Rare",
            }, cards, string.Join(" | ", cards));
        }

        [Test]
        public void NoMembersOrNoSource_IsAnEmptyPage()
        {
            Assert.IsEmpty(GearPage.Roll(Request(NormalGear), new FakeCatalog(), Seeded(1)).Cards);
            Assert.IsEmpty(GearPage.Roll(Request(null, Member("a")), new FakeCatalog(), Seeded(1)).Cards);
        }
    }
}
