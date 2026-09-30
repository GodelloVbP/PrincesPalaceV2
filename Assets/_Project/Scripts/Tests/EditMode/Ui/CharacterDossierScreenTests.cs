using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The CHARACTER tab's tree and geometry (plan C15-C16, spec section 3),
    // pinned in the spec's own numbers.
    public class CharacterDossierScreenTests
    {
        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }

        [Test]
        public void TheScreenAuditsCleanAtEveryFrame()
        {
            var errors = UiAudit.RunAllFrames(CharacterDossierScreen.Build().Root);

            Assert.IsEmpty(errors, $"first 5 of {errors.Count}: " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        // ---- the frame ---------------------------------------------------------------

        [Test]
        public void TheStripRailAndDetailHaveTheSpecsSizes()
        {
            Assert.AreEqual(1520f, DossierLayout.AreaWidth, 0.001f);
            Assert.AreEqual(72f, DossierLayout.StripHeight, 0.001f);
            Assert.AreEqual(300f, DossierLayout.RailWidth, 0.001f);
            Assert.AreEqual(1196f, DossierLayout.DetailWidth, 0.001f);
            Assert.AreEqual(660f, DossierLayout.DetailHeight, 0.001f);

            // Strip, gap and body fill the pane's working height exactly.
            Assert.AreEqual(756f, DossierLayout.StripHeight + DossierLayout.StripGap + DossierLayout.BodyHeight, 0.001f);
            Assert.AreEqual(162f, DossierLayout.DetailCentreX, 0.001f);
            Assert.AreEqual(-48f, DossierLayout.DetailCentreY, 0.001f);
            Assert.AreEqual(-610f, DossierLayout.RailCentreX, 0.001f);
        }

        [Test]
        public void EverySectionIsTheDetailAreaAndOnlyLoadoutStartsShown()
        {
            var screen = CharacterDossierScreen.Build();
            var sections = new[]
            {
                screen.LoadoutSection, screen.AttributesSection, screen.SkillsSection, screen.RewardTrackSection,
            };

            Assert.AreEqual(CharacterSections.All.Length, sections.Length);

            foreach (NodeRef section in sections)
            {
                Assert.AreEqual(1196f, section.Node.Size.X, 0.001f, $"{section.Node.Name} is not the detail's width");
                Assert.AreEqual(660f, section.Node.Size.Y, 0.001f, $"{section.Node.Name} is not the detail's height");
            }

            Assert.IsFalse(screen.LoadoutSection.Node.StartInactive, "LOADOUT is the default section");
            Assert.IsTrue(screen.AttributesSection.Node.StartInactive);
            Assert.IsTrue(screen.SkillsSection.Node.StartInactive);
            Assert.IsTrue(screen.RewardTrackSection.Node.StartInactive);
        }

        [Test]
        public void TheRailListsTheFourSectionsInOrder()
        {
            var screen = CharacterDossierScreen.Build();

            Assert.AreEqual(4, screen.SectionRows.Count);
            CollectionAssert.AreEqual(
                new[] { "LOADOUT", "ATTRIBUTES", "SKILLS", "REWARD TRACK" },
                CharacterSections.All.Select(s => CharacterSections.Label(s).Format()).ToArray());

            // 64 apart, under the 260 identity block and a 24 gap.
            Assert.AreEqual(14f, DossierLayout.SectionRowCentreY(0), 0.001f);
            Assert.AreEqual(-178f, DossierLayout.SectionRowCentreY(3), 0.001f);
        }

        // ---- the strip ------------------------------------------------------------------

        [Test]
        public void FiveChipsFitBetweenThePagersWithTheBenchDivider()
        {
            Assert.AreEqual(5, DossierLayout.ChipCapacity);
            Assert.AreEqual(5, CharacterDossierScreen.Build().Chips.Count);

            // Chip 0 starts after the LB plate and its gap.
            Assert.AreEqual(-568f, DossierLayout.ChipCentreX(0, -1), 0.001f);

            // A divider before chip 3 moves chips 3 and 4 right by 16.
            Assert.AreEqual(200f, DossierLayout.ChipCentreX(3, -1), 0.001f);
            Assert.AreEqual(216f, DossierLayout.ChipCentreX(3, 3), 0.001f);
            Assert.AreEqual(80f, DossierLayout.DividerCentreX(3), 0.001f);

            float lastRight = DossierLayout.ChipCentreX(4, 1) + DossierLayout.ChipWidth * 0.5f;
            float pagerLeft = DossierLayout.PagerNextX - DossierLayout.PagerWidth * 0.5f;
            Assert.LessOrEqual(lastRight, pagerLeft, "the last chip runs under the RB plate");
        }

        // ---- LOADOUT ------------------------------------------------------------------------

        // Today's arrangement in four rows (spec 3.1): Head top centre with
        // Necklace beside it, the left file down Torso, Gloves, Legs, the
        // right file Weapon 1, Weapon 2, Shoes level with them.
        [TestCase(EquipmentSlot.Head, -378f, 286f)]
        [TestCase(EquipmentSlot.Necklace, -546f, 286f)]
        [TestCase(EquipmentSlot.Torso, -546f, 170f)]
        [TestCase(EquipmentSlot.Gloves, -546f, 54f)]
        [TestCase(EquipmentSlot.Legs, -546f, -62f)]
        [TestCase(EquipmentSlot.Weapon1, -210f, 170f)]
        [TestCase(EquipmentSlot.Weapon2, -210f, 54f)]
        [TestCase(EquipmentSlot.Shoes, -210f, -62f)]
        public void EachGearSlotSitsWhereTheSpecPutsIt(EquipmentSlot slot, float x, float y)
        {
            var at = DossierLayout.GearCentre(slot);
            Assert.AreEqual(x, at.X, 0.001f);
            Assert.AreEqual(y, at.Y, 0.001f);
        }

        [Test]
        public void EveryGearSlotHasItsOwnCellAndItsOwnNode()
        {
            var seen = new HashSet<(float, float)>();
            foreach (var slot in EquipmentSlots.All)
            {
                var at = DossierLayout.GearCentre(slot);
                Assert.IsTrue(seen.Add((at.X, at.Y)), $"{slot} shares a cell");
            }

            Assert.AreEqual(EquipmentSlots.All.Length, CharacterDossierScreen.Build().SlotCells.Count);
        }

        [Test]
        public void TheBookRowSitsUnderTheGearWithOneCellPerBookSlot()
        {
            var screen = CharacterDossierScreen.Build();
            Assert.AreEqual(SpellBooks.MaxSpellSlots, screen.BookCells.Count);

            Assert.AreEqual(-226f, DossierLayout.BookCentre(0).Y, 0.001f);
            Assert.AreEqual(-546f, DossierLayout.BookCentre(0).X, 0.001f);
            Assert.AreEqual(-378f, DossierLayout.BookCentre(1).X, 0.001f);
            Assert.AreEqual(-210f, DossierLayout.BookCentre(2).X, 0.001f);

            // The lowest gear caption ends above the heading.
            Assert.LessOrEqual(3 * DossierLayout.SlotPitch + DossierLayout.SlotSize + DossierLayout.SlotCaptionGap
                               + DossierLayout.SlotCaptionHeight, DossierLayout.BooksHeadingTop);
            Assert.LessOrEqual(DossierLayout.BookRowTop + DossierLayout.SlotSize + DossierLayout.SlotCaptionGap
                               + DossierLayout.SlotCaptionHeight, DossierLayout.DetailHeight);
        }

        [Test]
        public void ThePackIsAThreeByFourGridBesideTheSlots()
        {
            var screen = CharacterDossierScreen.Build();

            Assert.AreEqual(12, screen.PackCells.Count);
            Assert.AreEqual(5, screen.FilterChips.Count);

            Assert.AreEqual(-78f, DossierLayout.PackCellCentre(0).X, 0.001f);
            Assert.AreEqual(216f, DossierLayout.PackCellCentre(0).Y, 0.001f);
            Assert.AreEqual(174f, DossierLayout.PackCellCentre(11).X, 0.001f);
            Assert.AreEqual(-162f, DossierLayout.PackCellCentre(11).Y, 0.001f);

            // Grid, gap and scrollbar stay inside the 408 column.
            float right = DossierLayout.PackLeft + DossierLayout.PackGridWidth + DossierLayout.ScrollbarGap
                          + DossierLayout.ScrollbarWidth;
            Assert.LessOrEqual(right, DossierLayout.PackLeft + DossierLayout.PackWidth);
        }

        [Test]
        public void TheThumbSaysHowMuchOfThePackIsShowing()
        {
            Assert.AreEqual(490f, DossierLayout.ScrollThumbHeight(4), 0.001f, "a pack that fits fills the track");
            Assert.AreEqual(245f, DossierLayout.ScrollThumbHeight(8), 0.001f);
            Assert.AreEqual(32f, DossierLayout.ScrollThumbHeight(500), 0.001f, "the thumb keeps a grabbable floor");
        }

        [Test]
        public void TheCompareColumnHoldsARowPerDerivedStat()
        {
            var screen = CharacterDossierScreen.Build();

            Assert.AreEqual(SheetStats.Derived.Length, screen.CompareStatNames.Count);
            Assert.AreEqual(SheetStats.Derived.Length, screen.CompareStatValues.Count);
            Assert.AreEqual(448f, DossierLayout.CompareCentreX, 0.001f);

            float lastBottom = DossierLayout.CompareRowsTop + SheetStats.Derived.Length * DossierLayout.CompareRowHeight;
            Assert.LessOrEqual(lastBottom, DossierLayout.DetailHeight);
        }

        // ---- one selection language (plan C13) ------------------------------------------------

        [Test]
        public void NothingOnThePaneScalesOnHover()
        {
            var scaled = Walk(CharacterDossierScreen.Build().Root).Where(n => n.HoverScale != 0f && n.HoverScale != 1f)
                .Select(n => n.Name).ToList();

            Assert.IsEmpty(scaled, "hover is a plate tint, never a scale: " + string.Join(", ", scaled));
        }

        // ---- ATTRIBUTES ---------------------------------------------------------------------

        [Test]
        public void EveryAbilityHasAMinusAndAPlus()
        {
            var screen = CharacterDossierScreen.Build();

            Assert.AreEqual(SheetStats.Abilities.Length, screen.AttributePluses.Count);
            Assert.AreEqual(SheetStats.Abilities.Length, screen.AttributeMinuses.Count);
            Assert.AreEqual(44f, DossierLayout.StepperSize, 0.001f);

            float rowsBottom = DossierLayout.AttributeRowsTop + SheetStats.Abilities.Length * DossierLayout.AttributeRowHeight;
            Assert.LessOrEqual(rowsBottom, DossierLayout.DetailHeight);
        }
    }
}
