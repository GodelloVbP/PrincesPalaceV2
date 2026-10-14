using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The CHARACTER tab's pure models (plan C5, C7, C15, C16): who the strip
    // shows and in what order, what the LOADOUT pack holds and filters to, how
    // a change reads in the compare column, and the section the menu reopens
    // on.
    public class CharacterPaneModelTests
    {
        [SetUp]
        [TearDown]
        public void ClearMemory() => MenuMemory.Clear();

        // seats are indexed Front, Middle, Rear.
        private static readonly string[] Seats = { "bear", "sheep", "owl" };

        // ---- the strip (C15) ------------------------------------------------------------

        [Test]
        public void TheStripIsTheSeatedPartyInStageOrderThenTheBench()
        {
            var roster = new[] { "sheep", "owl", "bear", "turtle", "fly" };

            var chips = CharacterStrip.Order(Seats, roster);

            CollectionAssert.AreEqual(new[] { "owl", "sheep", "bear", "turtle", "fly" }, chips.Select(c => c.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 2, 1, 0, -1, -1 }, chips.Select(c => c.Seat).ToArray());
            CollectionAssert.AreEqual(new[] { false, false, false, true, true }, chips.Select(c => c.Benched).ToArray());
        }

        [Test]
        public void AnOpenSeatIsSkippedAndItsCharacterIsBenched()
        {
            var chips = CharacterStrip.Order(new[] { "bear", "", "owl" }, new[] { "sheep", "owl", "bear" });

            CollectionAssert.AreEqual(new[] { "owl", "bear", "sheep" }, chips.Select(c => c.Id).ToArray());
            Assert.IsTrue(chips[2].Benched);
        }

        [Test]
        public void WithNobodySeatedTheRosterReadsAsTheParty()
        {
            var chips = CharacterStrip.Order(new string[0], new[] { "sheep", "owl" });

            CollectionAssert.AreEqual(new[] { "sheep", "owl" }, chips.Select(c => c.Id).ToArray());
            Assert.IsFalse(chips.Any(c => c.Benched), "nobody is dimmed when nobody is seated");
        }

        [Test]
        public void ASeatNamingSomeoneOffTheRosterIsDropped()
        {
            var chips = CharacterStrip.Order(new[] { "ghost", "sheep", "sheep" }, new[] { "sheep" });

            CollectionAssert.AreEqual(new[] { "sheep" }, chips.Select(c => c.Id).ToArray());
        }

        [Test]
        public void SteppingWrapsAtBothEnds()
        {
            Assert.AreEqual(1, CharacterStrip.Step(0, 3, 1));
            Assert.AreEqual(0, CharacterStrip.Step(2, 3, 1));
            Assert.AreEqual(2, CharacterStrip.Step(0, 3, -1));
        }

        [Test]
        public void TheStripPagesAWholeWindowAtATimeAndKeepsTheLastPageFull()
        {
            Assert.AreEqual(0, CharacterStrip.WindowStart(4, 5, 5), "everything fits");
            Assert.AreEqual(0, CharacterStrip.WindowStart(4, 7, 5));
            Assert.AreEqual(2, CharacterStrip.WindowStart(5, 7, 5), "the last page is full, not a lone chip");
            Assert.AreEqual(2, CharacterStrip.WindowStart(6, 7, 5));
        }

        [Test]
        public void TheDividerFallsBeforeTheFirstBenchedChipInTheWindow()
        {
            var chips = CharacterStrip.Order(Seats, new[] { "sheep", "owl", "bear", "turtle" });

            Assert.AreEqual(3, CharacterStrip.DividerIndex(chips, 0, 4));
            Assert.AreEqual(-1, CharacterStrip.DividerIndex(chips, 0, 3), "no benched chip in the window");
        }

        // ---- the pack (C16) -------------------------------------------------------------

        private static BagItem Item(string id, EquipmentSlot slot, bool equippable, int tier = 1, int count = 1) =>
            new BagItem(id, id, equippable ? 2 : 0, slot, tier, count, "", equippable);

        private static IReadOnlyList<PackEntry> Pack() => LoadoutPack.Build(
            new[]
            {
                Item("salve", EquipmentSlot.Head, equippable: false, count: 3),
                Item("helm", EquipmentSlot.Head, equippable: true),
                Item("sword", EquipmentSlot.Weapon1, equippable: true),
            },
            new[] { new PackBook("zap", "Zap"), new PackBook("bolt", "Bolt"), new PackBook("zap", "Zap") });

        [Test]
        public void ThePackHoldsGearThenSalvesThenBooksByName()
        {
            var pack = Pack();

            CollectionAssert.AreEqual(
                new[] { PackEntryKind.Gear, PackEntryKind.Gear, PackEntryKind.Salve, PackEntryKind.Book, PackEntryKind.Book },
                pack.Select(e => e.Kind).ToArray());
            Assert.AreEqual("bolt", pack[3].BookId);
            Assert.AreEqual("zap", pack[4].BookId);
            Assert.AreEqual(2, pack[4].Count, "two copies of one book are one cell");
            Assert.AreEqual(3, pack[2].Count);
        }

        [TestCase(PackFilter.All, 5)]
        [TestCase(PackFilter.Armour, 1)]
        [TestCase(PackFilter.Weapons, 1)]
        [TestCase(PackFilter.Salves, 1)]
        [TestCase(PackFilter.Books, 2)]
        public void EachFilterShowsItsKind(PackFilter filter, int expected)
        {
            Assert.AreEqual(expected, LoadoutPack.Filter(Pack(), filter).Count);
        }

        [Test]
        public void AnArmedSlotTakesOnlyGearItAccepts()
        {
            var pack = Pack();

            Assert.AreEqual(1, pack.Count(e => LoadoutPack.FitsGearSlot(e, EquipmentSlot.Head)));
            Assert.AreEqual(1, pack.Count(e => LoadoutPack.FitsGearSlot(e, EquipmentSlot.Weapon2)),
                "a Weapon 1 item fits the off hand");
            Assert.AreEqual(0, pack.Count(e => LoadoutPack.FitsGearSlot(e, EquipmentSlot.Shoes)));
            Assert.AreEqual(PackFilter.Weapons, LoadoutPack.FilterFor(EquipmentSlot.Weapon2));
            Assert.AreEqual(PackFilter.Armour, LoadoutPack.FilterFor(EquipmentSlot.Legs));
        }

        // ---- the compare column ----------------------------------------------------------

        [Test]
        public void AChangeReadsOldThenNewThenTheDifference()
        {
            Assert.AreEqual("280 → 338 (+58)", LoadoutCompare.DeltaText("280", "338", 58));
            Assert.AreEqual("40 → 31 (-9)", LoadoutCompare.DeltaText("40", "31", -9));
        }

        // What one point does to a derived row (the stepper preview and the
        // compare column read it from one place). Pinned in literals against
        // AbilityDerivation's own table: 20 health and 2 physical defence a
        // Constitution point, 2 mana and 2 magical defence a Wisdom point,
        // 1 speed per 2 Dexterity, 1 signature gain per 4 Charisma.
        [Test]
        public void OnePointMovesTheRowsItFeedsByTheirPerPointRate()
        {
            var ten = new Domain.Stats.AbilityScoreBlock(10, 10, 10, 10, 10, 10);
            var con = ten.With(Domain.Stats.AbilityScore.Constitution, 11);
            var wis = ten.With(Domain.Stats.AbilityScore.Wisdom, 11);

            Assert.AreEqual(20, SheetStats.ScoreDelta(SheetStat.MaxHealth, ten, con));
            Assert.AreEqual(2, SheetStats.ScoreDelta(SheetStat.PhysicalDefense, ten, con));
            Assert.AreEqual(0, SheetStats.ScoreDelta(SheetStat.Speed, ten, con));
            Assert.AreEqual(0, SheetStats.ScoreDelta(SheetStat.Attack, ten, con));

            Assert.AreEqual(2, SheetStats.ScoreDelta(SheetStat.MaxMana, ten, wis));
            Assert.AreEqual(2, SheetStats.ScoreDelta(SheetStat.MagicalDefense, ten, wis));

            // A refund is the same rate, the other way.
            Assert.AreEqual(-20, SheetStats.ScoreDelta(SheetStat.MaxHealth, con, ten));
        }

        [Test]
        public void SpeedAndSignatureGainMoveInSteps()
        {
            var ten = new Domain.Stats.AbilityScoreBlock(10, 10, 10, 10, 10, 10);

            Assert.AreEqual(0, SheetStats.ScoreDelta(SheetStat.Speed, ten,
                ten.With(Domain.Stats.AbilityScore.Dexterity, 11)), "the first Dexterity point is mid-step");
            Assert.AreEqual(1, SheetStats.ScoreDelta(SheetStat.Speed, ten,
                ten.With(Domain.Stats.AbilityScore.Dexterity, 12)));

            Assert.AreEqual(0, SheetStats.ScoreDelta(SheetStat.SignatureGain, ten,
                ten.With(Domain.Stats.AbilityScore.Charisma, 13)));
            Assert.AreEqual(1, SheetStats.ScoreDelta(SheetStat.SignatureGain, ten,
                ten.With(Domain.Stats.AbilityScore.Charisma, 14)));
        }

        // Wisdom moves nothing in a pool whose capacity is fixed.
        [Test]
        public void AFixedPoolIsNotMovedByWisdom()
        {
            var ten = new Domain.Stats.AbilityScoreBlock(10, 10, 10, 10, 10, 10);
            var wis = ten.With(Domain.Stats.AbilityScore.Wisdom, 11);

            Assert.AreEqual(0, SheetStats.ScoreDelta(SheetStat.MaxMana, ten, wis, Domain.Content.PoolCapacityRule.Fixed));
            Assert.AreEqual(2, SheetStats.ScoreDelta(SheetStat.MagicalDefense, ten, wis, Domain.Content.PoolCapacityRule.Fixed));
        }

        // The slot names live in the string manifest, once each.
        [Test]
        public void EveryGearSlotHasItsNameInTheManifest()
        {
            CollectionAssert.AreEqual(
                new[] { "Head", "Necklace", "Torso", "Legs", "Shoes", "Gloves", "Weapon 1", "Weapon 2" },
                EquipmentSlots.All.Select(slot => UiStrings.EquipmentSlotName(slot).Format()).ToArray());
        }

        [Test]
        public void AnEmptyBookSlotIsCaptionedByItsNumber()
        {
            Assert.AreEqual("Book 1", UiStrings.LoadoutBookSlot.Format(1));
            Assert.AreEqual("Book 3", UiStrings.LoadoutBookSlot.Format(3));
        }

        // ---- pending states ---------------------------------------------------------------

        [Test]
        public void APendingStateSaysWhatItWaitsFor()
        {
            Assert.IsTrue(LoadoutPending.None.IsNone);
            Assert.IsTrue(LoadoutPending.ArmGear(4).ArmsGear);
            Assert.IsFalse(LoadoutPending.ArmGear(4).ArmsBook);
            Assert.IsTrue(LoadoutPending.ArmBook(0).ArmsBook);
            Assert.AreEqual(LoadoutPendingKind.BookPicked, LoadoutPending.PickBook("zap").Kind);
            Assert.AreEqual("zap", LoadoutPending.PickBook("zap").BookId);
        }

        // ---- the reopen rule (C7) --------------------------------------------------------

        [Test]
        public void TheCharacterSectionIsRememberedPerContextAndDefaultsToLoadout()
        {
            Assert.AreEqual(CharacterSection.Loadout, MenuMemory.SectionFor(MenuContext.Camp));

            MenuMemory.RememberSection(MenuContext.Descent, CharacterSection.RewardTrack);

            Assert.AreEqual(CharacterSection.RewardTrack, MenuMemory.SectionFor(MenuContext.Descent));
            Assert.AreEqual(CharacterSection.Loadout, MenuMemory.SectionFor(MenuContext.Fight));

            MenuMemory.Clear();
            Assert.AreEqual(CharacterSection.Loadout, MenuMemory.SectionFor(MenuContext.Descent));
        }
    }
}
