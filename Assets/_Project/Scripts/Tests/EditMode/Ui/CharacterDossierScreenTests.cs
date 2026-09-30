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
        [TestCase(EquipmentSlot.Head, -396f, 286f)]
        [TestCase(EquipmentSlot.Necklace, -546f, 286f)]
        [TestCase(EquipmentSlot.Torso, -546f, 164f)]
        [TestCase(EquipmentSlot.Gloves, -546f, 42f)]
        [TestCase(EquipmentSlot.Legs, -546f, -80f)]
        [TestCase(EquipmentSlot.Weapon1, -246f, 164f)]
        [TestCase(EquipmentSlot.Weapon2, -246f, 42f)]
        [TestCase(EquipmentSlot.Shoes, -246f, -80f)]
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

            Assert.AreEqual(-246f, DossierLayout.BookCentre(0).Y, 0.001f);
            Assert.AreEqual(-534f, DossierLayout.BookCentre(0).X, 0.001f);
            Assert.AreEqual(-396f, DossierLayout.BookCentre(1).X, 0.001f);
            Assert.AreEqual(-258f, DossierLayout.BookCentre(2).X, 0.001f);

            // The lowest gear caption ends above the heading.
            Assert.LessOrEqual(3 * DossierLayout.SlotPitch + DossierLayout.SlotSize + DossierLayout.SlotCaptionGap
                               + DossierLayout.SlotCaptionHeight, DossierLayout.BooksHeadingTop);
            Assert.LessOrEqual(DossierLayout.BookRowTop + DossierLayout.SlotSize + DossierLayout.SlotCaptionGap
                               + DossierLayout.SlotCaptionHeight, DossierLayout.DetailHeight);
        }

        // A caption sits under its cell and clears the cell below it.
        [Test]
        public void EveryGearCaptionSitsBetweenItsCellAndTheNextRow()
        {
            float captionBottom = DossierLayout.SlotSize + DossierLayout.SlotCaptionGap + DossierLayout.SlotCaptionHeight;
            Assert.AreEqual(120f, captionBottom, 0.001f);
            Assert.Greater(DossierLayout.SlotPitch, captionBottom, "a caption runs into the cell below it");

            foreach (var slot in EquipmentSlots.All)
            {
                var cell = DossierLayout.GearCentre(slot);
                var caption = DossierLayout.GearCaptionCentre(slot);
                Assert.AreEqual(cell.X, caption.X, 0.001f, $"{slot}'s caption is not under its cell");
                Assert.AreEqual(-61f, caption.Y - cell.Y, 0.001f, $"{slot}'s caption is not 61 below its cell's centre");
            }
        }

        // A label that outgrows its box's height draws nothing in TMP, and a
        // box under one line of its own font is such a box, so a 26 px box for
        // a Body 20 line shows no caption or chip name at all. A Body 20 line
        // is 26.53 px (measured), 1.33 of the size.
        [Test]
        public void EveryLabelThatTruncatesOrClampsIsBoxedForAtLeastOneLine()
        {
            var tooShort = new List<string>();
            foreach (var node in Walk(CharacterDossierScreen.Build().Root))
            {
                if (!node.Truncates && !node.Clamps) continue;

                float line = node.FontSize * 1.33f;
                if (node.Size.Y < line) tooShort.Add($"{node.Name} ({node.Size.Y} < {line})");
            }

            Assert.IsEmpty(tooShort, "boxed below one line, so TMP draws nothing: " + string.Join(", ", tooShort));
        }

        [Test]
        public void TheStripsChipLinesFillTheChipWithoutOverlap()
        {
            Assert.AreEqual(30f, DossierLayout.ChipLineHeight, 0.001f);
            Assert.LessOrEqual(2 * DossierLayout.ChipLineHeight, DossierLayout.ChipHeight);
            Assert.AreEqual(DossierLayout.ChipLineHeight, DossierLayout.ChipNameY - DossierLayout.ChipSeatY, 0.001f,
                "the name and seat lines overlap");
        }

        [Test]
        public void ThePackIsAThreeByFourGridBesideTheSlots()
        {
            var screen = CharacterDossierScreen.Build();

            Assert.AreEqual(12, screen.PackCells.Count);
            Assert.AreEqual(5, screen.FilterChips.Count);

            Assert.AreEqual(-122f, DossierLayout.PackCellCentre(0).X, 0.001f);
            Assert.AreEqual(216f, DossierLayout.PackCellCentre(0).Y, 0.001f);
            Assert.AreEqual(130f, DossierLayout.PackCellCentre(11).X, 0.001f);
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
            Assert.AreEqual(422f, DossierLayout.CompareCentreX, 0.001f);
            Assert.AreEqual(352f, DossierLayout.CompareWidth, 0.001f);

            // A stat is two lines: 54 a row, eight rows end at 644 of 660.
            Assert.AreEqual(54f, DossierLayout.CompareRowHeight, 0.001f);
            float lastBottom = DossierLayout.CompareRowsTop + SheetStats.Derived.Length * DossierLayout.CompareRowHeight;
            Assert.AreEqual(644f, lastBottom, 0.001f);
            Assert.LessOrEqual(lastBottom, DossierLayout.DetailHeight);
        }

        // The columns fill the detail area exactly, in order.
        [Test]
        public void TheThreeLoadoutColumnsFillTheDetailArea()
        {
            Assert.AreEqual(420f, DossierLayout.PackLeft, 0.001f);
            Assert.AreEqual(844f, DossierLayout.CompareLeft, 0.001f);
            Assert.AreEqual(DossierLayout.DetailWidth, DossierLayout.CompareLeft + DossierLayout.CompareWidth, 0.001f);
            Assert.LessOrEqual(DossierLayout.SlotsLeft + DossierLayout.SlotsWidth + DossierLayout.ColumnGap,
                DossierLayout.PackLeft);
        }

        // The compare column has a body for a book's or a salve's words, which
        // the rows would otherwise crowd out.
        [Test]
        public void TheCompareColumnHasABodyForABooksWords()
        {
            var screen = CharacterDossierScreen.Build();
            Assert.IsNotNull(screen.CompareBody);
            Assert.IsTrue(screen.CompareDescription.Node.Clamps, "a gear summary should wrap, not cut at the first line");
            Assert.IsFalse(screen.CompareDescription.Node.Truncates);
            Assert.AreEqual(108f, screen.CompareDescription.Node.Size.Y, 0.001f, "four lines of a summary");
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
            Assert.AreEqual(652f, rowsBottom, 0.001f);
            Assert.LessOrEqual(rowsBottom, DossierLayout.DetailHeight);
        }

        // Spec 3.2: the buttons sit right after the value, not at the far end
        // of the row.
        [Test]
        public void TheSteppersSitRightAfterTheValue()
        {
            float valueRight = DossierLayout.AttributeValueLeft + DossierLayout.AttributeValueWidth;
            Assert.AreEqual(156f, valueRight, 0.001f);
            Assert.AreEqual(164f, DossierLayout.MinusLeft, 0.001f);
            Assert.AreEqual(216f, DossierLayout.PlusLeft, 0.001f);
            Assert.LessOrEqual(DossierLayout.MinusLeft - valueRight, 16f, "a gap between the value and its minus");

            // 44 x 44 and legible: the glyph is 32, over the type floor.
            Assert.AreEqual(44f, DossierLayout.StepperSize, 0.001f);
            Assert.AreEqual(32, CharacterDossierScreen.StepperGlyphSize);
        }

        // The effect line may take two lines and sits inside its row.
        [Test]
        public void TheEffectLineHoldsTwoBodyLinesInsideItsRow()
        {
            Assert.GreaterOrEqual(DossierLayout.AttributeEffectHeight, 2 * 20 * 1.33f);
            Assert.LessOrEqual(DossierLayout.AttributeEffectTop + DossierLayout.AttributeEffectHeight,
                DossierLayout.AttributeRowInner);

            var node = CharacterDossierScreen.Build().AttributeEffects[0].Node;
            Assert.IsTrue(node.Clamps, "the effect line should wrap");
            Assert.IsFalse(node.Truncates, "the effect line should not cut at one line");
        }

        [Test]
        public void TheDerivedValueColumnHoldsABeforeAndAfterOnOneLine()
        {
            Assert.AreEqual(300f, DossierLayout.DerivedValueWidth, 0.001f);
            Assert.AreEqual(470f, DossierLayout.DerivedNameWidth + DossierLayout.DerivedValueWidth, 0.001f);
        }

        // ---- SKILLS ---------------------------------------------------------------------------

        // The pane pools its nodes by the kind of entry the window can hold, so
        // the tree must carry exactly as many as the model bounds it to.
        [Test]
        public void TheSkillsPoolsHoldWhatTheWindowCanShow()
        {
            var screen = CharacterDossierScreen.Build();

            Assert.AreEqual(6, screen.SkillRows.Count);
            Assert.AreEqual(6, screen.SkillRowNames.Count);
            Assert.AreEqual(6, screen.SkillRowMetas.Count);
            Assert.AreEqual(6, screen.SkillRowDescriptions.Count);
            Assert.AreEqual(6, screen.SkillHeaders.Count);
            Assert.AreEqual(3, screen.SkillNotes.Count);
            Assert.IsNotNull(screen.SkillLink);
            Assert.IsNotNull(screen.SkillsScrollTrack);
            Assert.IsNotNull(screen.SkillsScrollThumb);
        }

        [Test]
        public void ASkillRowIsAFocusableButtonHoldingANameAMetaLineAndTwoDescriptionLines()
        {
            var screen = CharacterDossierScreen.Build();
            var row = screen.SkillRows[0].Node;

            Assert.AreEqual(UiNodeKind.Button, row.Kind);
            Assert.AreEqual(1172f, row.Size.X, 0.001f);
            Assert.AreEqual(100f, row.Size.Y, 0.001f, "38 above the description, 58 for two lines at 29 each, 4 below");

            var name = screen.SkillRowNames[0].Node;
            var meta = screen.SkillRowMetas[0].Node;
            var description = screen.SkillRowDescriptions[0].Node;

            Assert.AreEqual(22, name.FontSize);
            Assert.GreaterOrEqual(name.Size.Y, 22 * 1.33f, "a Body 22 line needs its 29 px or TMP draws nothing");
            Assert.AreEqual(20, meta.FontSize);
            Assert.AreEqual(20, description.FontSize);
            Assert.GreaterOrEqual(description.Size.Y, 2 * 20 * 1.33f, "the description holds two Body 20 lines");
            Assert.AreEqual(58f, description.Size.Y, 0.001f,
                "two lines at 29: a box of exactly two nominal lines (54) drew one and an ellipsis");
            Assert.IsTrue(description.Clamps, "the description wraps to two lines and ends in an ellipsis past them");
            Assert.IsTrue(name.Truncates);
            Assert.IsTrue(meta.Truncates);
        }

        [Test]
        public void ASkillRowsThreeLinesSitInsideItWithoutOverlap()
        {
            float rowTop = DossierLayout.SkillsRowHeight * 0.5f;
            float nameTop = DossierLayout.SkillsLineY + DossierLayout.SkillsNameHeight * 0.5f;
            float nameBottom = DossierLayout.SkillsLineY - DossierLayout.SkillsNameHeight * 0.5f;
            float descTop = DossierLayout.SkillsDescriptionY + DossierLayout.SkillsDescriptionHeight * 0.5f;
            float descBottom = DossierLayout.SkillsDescriptionY - DossierLayout.SkillsDescriptionHeight * 0.5f;

            Assert.LessOrEqual(nameTop, rowTop);
            Assert.LessOrEqual(descTop, nameBottom, "the description starts under the name line");
            Assert.GreaterOrEqual(descBottom, -rowTop, "and ends inside the row");

            // Name and meta share the first line without touching.
            float nameRight = DossierLayout.SkillsNameX + DossierLayout.SkillsNameWidth * 0.5f;
            float metaLeft = DossierLayout.SkillsMetaX - DossierLayout.SkillsMetaWidth * 0.5f;
            Assert.Less(nameRight, metaLeft);
            Assert.AreEqual(692f, DossierLayout.SkillsMetaWidth, 0.001f);
        }

        // The window is as tall as the detail and the rows leave room for the
        // scrollbar at its right edge.
        [Test]
        public void TheSkillsRowsLeaveRoomForTheScrollbar()
        {
            Assert.AreEqual(1172f, DossierLayout.SkillsRowWidth, 0.001f);
            Assert.AreEqual(1196f, DossierLayout.SkillsRowWidth + DossierLayout.SkillsScrollbarGap + DossierLayout.ScrollbarWidth,
                0.001f);
            Assert.AreEqual(594f, DossierLayout.SkillsScrollbarCentreX, 0.001f, "the bar sits at the detail's right edge");
        }

        [Test]
        public void TheConstellationLinkIsA420By48PlateWithItsWords()
        {
            var link = CharacterDossierScreen.Build().SkillLink.Node;

            Assert.AreEqual(UiNodeKind.Button, link.Kind);
            Assert.AreEqual(420f, link.Size.X, 0.001f);
            Assert.AreEqual(48f, link.Size.Y, 0.001f);
            Assert.AreEqual(20, link.FontSize);
        }
    }
}
