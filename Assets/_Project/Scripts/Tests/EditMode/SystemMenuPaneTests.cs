using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The system menu's two new content panes: Run statistics and Main menu.
    //
    // UiAudit re-solves whatever the scene build emits, so overlap, overflow
    // and duplicate names in these trees are already covered at build time and
    // are not re-checked here. What IS here is the arithmetic and the tables --
    // the parts that decide whether a pane will still be right after somebody
    // adds a row, and the parts a solved tree cannot answer because they are
    // about intent rather than about geometry.
    public class SystemMenuPaneTests
    {
        // ---- every pane fills its box -------------------------------------------
        //
        // THE RULE, and it is here because breaking it is invisible to
        // everything else. A pane authored smaller than the content area still
        // solves, still passes containment, still passes overflow -- it just
        // sits in the middle of a box it does not fill, with dead violet all
        // round it. The dossier did exactly that at the handover's own 1360x766
        // inside a 1600x804 pane, for as long as it had been hosted here.
        [Test]
        public void EveryHostedPaneIsTheSizeOfTheContentArea()
        {
            float width = SystemMenuLayout.PanelWidth;
            float height = SystemMenuLayout.ContentHeight;

            Assert.AreEqual(width, DossierLayout.Width, 0.001f, "the dossier is not the pane's width");
            Assert.AreEqual(height, DossierLayout.Height, 0.001f, "the dossier is not the pane's height");

            Assert.AreEqual(width, OptionsLayout.PaneWidth, 0.001f, "Options is not the pane's width");
            Assert.AreEqual(height, OptionsLayout.PaneHeight, 0.001f, "Options is not the pane's height");

            Assert.AreEqual(width, RunStatsLayout.PaneWidth, 0.001f, "Run statistics is not the pane's width");
            Assert.AreEqual(height, RunStatsLayout.PaneHeight, 0.001f, "Run statistics is not the pane's height");

            Assert.AreEqual(width, ExitsLayout.PaneWidth, 0.001f, "the Main menu pane is not the pane's width");
            Assert.AreEqual(height, ExitsLayout.PaneHeight, 0.001f, "the Main menu pane is not the pane's height");
        }

        // And their contents reach the floor rather than stopping in the middle
        // of it. Stated as a floor on how much of the usable height the tallest
        // thing in each pane uses -- an exact fit would be a change-detector,
        // and the failure being guarded against is a pane a THIRD empty.
        [TestCase("dossier")]
        [TestCase("options")]
        [TestCase("runstats")]
        [TestCase("exits")]
        public void EveryPaneUsesMostOfItsHeight(string pane)
        {
            float used;
            float usable;

            switch (pane)
            {
                case "dossier":
                    // Column C's stat list is the last thing down the tallest
                    // column, and it was the one stopping 184px early.
                    //
                    // HalfHeight - PadY, NOT DossierLayout.ColumnATop -- the two
                    // were the same number before column A got its own Blue 3:4
                    // container frame (shorter than the pane, see
                    // DossierLayout.ColumnAFrameHeight), and this row is about
                    // column C's own top, which still runs the full pane.
                    float genericTop = DossierLayout.HalfHeight - DossierLayout.PadY;
                    usable = genericTop - (-DossierLayout.HalfHeight + DossierLayout.PadY);
                    used = genericTop
                           - (DossierLayout.StatListTop
                              - DossierLayout.StatRowHeight * SheetStats.Derived.Length);
                    break;

                case "options":
                    usable = OptionsLayout.UsableHeight;
                    used = OptionsLayout.ColumnHeight(OptionRows.Groups, 0);
                    break;

                case "runstats":
                    usable = RunStatsLayout.UsableHeight;
                    used = RunStatsLayout.TallestCard(RunStatRows.Groups);
                    break;

                default:
                    usable = ExitsLayout.ContentTop - ExitsLayout.ContentBottom;
                    used = ExitsLayout.StackHeight;
                    break;
            }

            Assert.Greater(used / usable, 0.85f,
                $"the {pane} pane fills only {used / usable:P0} of its height ({used:F0} of " +
                $"{usable:F0}px), so it reads as a small screen inside a big empty one");
        }

        // ---- the Run statistics table -------------------------------------------

        [Test]
        public void EveryRunStatRowHasAKeyAndALabel()
        {
            foreach (var row in RunStatRows.AllRows)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(row.Key), "a run-stat row has no key");
                Assert.IsTrue(row.Label.IsValid, $"run-stat row '{row.Key}' has no label");
            }
        }

        // A duplicate key is not a compile error and does not look wrong: the
        // pane draws both rows, the controller answers both with the same
        // figure, and the second row silently becomes a copy of the first.
        [Test]
        public void NoTwoRunStatRowsShareAKey()
        {
            var keys = RunStatRows.AllRows.Select(r => r.Key).ToList();
            CollectionAssert.AreEquivalent(keys.Distinct().ToList(), keys,
                "two run-stat rows share a key, so one of them prints the other's number");
        }

        // Was a bare Panel sitting on the shared SystemMenuFill -- balance-bot,
        // 2026-09-02 gave it a Silver 2:1 ground, same pattern as Options and
        // the Main menu pane.
        [Test]
        public void TheRunStatsPaneIsASilverTwoByOneContainer()
        {
            var ground = RunStatsScreen.Build().Root;

            Assert.IsFalse(ground.Decor,
                "the wrapper must stay non-Decor, or content beneath it audits clean against itself");
            var art = ground.Children.Single(c => c.Kind == UiNodeKind.Sprite);
            Assert.AreEqual("UI/Buttons/Processed/container_silver_2x1.png", art.SpriteKey);
            Assert.IsTrue(art.Decor);
        }

        [Test]
        public void TheRunStatsPaneContentSitsInsideTheMeasuredInset()
        {
            var ground = RunStatsScreen.Build().Root;
            var content = ground.Children.Single(c => c.Name == "RunStatsPaneContent");
            var inset = Ui.ContainerContentInset(ContainerRatio.TwoByOne);

            Assert.AreEqual(PlaceKind.Stretch, content.Place.Kind);
            Assert.AreEqual(RunStatsLayout.PaneWidth * inset.Left, content.Place.Left, 0.01f);
            Assert.AreEqual(RunStatsLayout.PaneHeight * inset.Top, content.Place.Top, 0.01f);
        }

        [Test]
        public void TheRunStatCardsFitWithoutScrolling()
        {
            Assert.IsTrue(RunStatsLayout.CardsFit(RunStatRows.Groups),
                $"the tallest card needs {RunStatsLayout.TallestCard(RunStatRows.Groups):F0}px of " +
                $"{RunStatsLayout.UsableHeight:F0}px - this pane has no scrolling");
        }

        // The columns are laid out from a width that DIVIDES: three 420s and
        // two 50s inside 120px insets is exactly 1600. A gap that leaves a
        // remainder puts one card half a pixel off and gives it a rim that is
        // one pixel on one side and two on the other.
        [Test]
        public void TheRunStatColumnsFillThePaneExactly()
        {
            float left = RunStatsLayout.ColumnCentreX(0) - RunStatsLayout.ColumnWidth * 0.5f;
            float right = RunStatsLayout.ColumnCentreX(2) + RunStatsLayout.ColumnWidth * 0.5f;

            Assert.AreEqual(-RunStatsLayout.HalfWidth + RunStatsLayout.PadX, left, 0.001f,
                "the first stats column does not start at the declared inset");
            Assert.AreEqual(RunStatsLayout.HalfWidth - RunStatsLayout.PadX, right, 0.001f,
                "the last stats column does not end at the declared inset");
            Assert.AreEqual(System.Math.Round(RunStatsLayout.ColumnWidth), RunStatsLayout.ColumnWidth,
                "the stats column width is not a whole number of pixels");
        }

        // Cards are top-aligned, which is the only reason three headings of
        // three different card heights sit on one line.
        [Test]
        public void EveryRunStatCardStartsAtTheSameTop()
        {
            var groups = RunStatRows.Groups;

            foreach (var group in groups)
            {
                int rows = group.Rows.Count;
                float top = RunStatsLayout.CardCentreY(groups, rows)
                            + RunStatsLayout.CardHeight(rows) * 0.5f;
                Assert.AreEqual(RunStatsLayout.BlockTop(groups), top, 0.001f,
                    $"the {group.Key} card does not start on the shared top line");
            }
        }

        // The block is centred in what the pane has spare, so the tallest card
        // has the same margin above it as below. Hung from the pane's top it
        // leaves a quarter of the screen empty underneath and reads as a pane
        // that failed to finish drawing.
        [Test]
        public void TheRunStatBlockIsCentredInThePane()
        {
            var groups = RunStatRows.Groups;

            float above = RunStatsLayout.ContentTop - RunStatsLayout.BlockTop(groups);
            float below = (RunStatsLayout.BlockTop(groups) - RunStatsLayout.TallestCard(groups))
                          - RunStatsLayout.ContentBottom;

            Assert.AreEqual(above, below, 0.001f,
                "the stats cards are not centred in the pane - one margin is bigger than the other");
        }

        // The pane declares one value node per declared row, in order, carrying
        // that row's key. Index alignment across two files is the drift this
        // project keeps writing rules against; this is what makes the pairing
        // provable rather than assumed.
        [Test]
        public void TheRunStatsScreenDeclaresOneValuePerRow()
        {
            var screen = RunStatsScreen.Build();

            CollectionAssert.AreEqual(
                RunStatRows.AllRows.Select(r => r.Key).ToList(),
                screen.ValueKeys,
                "the Run statistics pane's value nodes do not match its own row table");

            Assert.AreEqual(screen.ValueKeys.Count, screen.Values.Count,
                "the pane declared a different number of keys and value nodes");
        }

        // Was a bare Panel sitting on the shared SystemMenuFill -- balance-bot,
        // 2026-09-02 gave it a Silver 2:1 ground, same pattern as Run
        // statistics and the Main menu pane.
        [Test]
        public void TheOptionsPaneIsASilverTwoByOneContainer()
        {
            var ground = OptionsScreen.Build().Root;

            Assert.IsFalse(ground.Decor,
                "the wrapper must stay non-Decor, or content beneath it audits clean against itself");
            var art = ground.Children.Single(c => c.Kind == UiNodeKind.Sprite);
            Assert.AreEqual("UI/Buttons/Processed/container_silver_2x1.png", art.SpriteKey);
            Assert.IsTrue(art.Decor);
        }

        [Test]
        public void TheOptionsPaneContentSitsInsideTheMeasuredInset()
        {
            var ground = OptionsScreen.Build().Root;
            var content = ground.Children.Single(c => c.Name == "OptionsPaneContent");
            var inset = Ui.ContainerContentInset(ContainerRatio.TwoByOne);

            Assert.AreEqual(PlaceKind.Stretch, content.Place.Kind);
            Assert.AreEqual(OptionsLayout.PaneWidth * inset.Left, content.Place.Left, 0.01f);
            Assert.AreEqual(OptionsLayout.PaneHeight * inset.Top, content.Place.Top, 0.01f);
        }

        // ---- the Main menu pane --------------------------------------------------

        // Was a bare Panel sitting on the shared SystemMenuFill -- balance-bot,
        // 2026-09-02 gave it a Silver 2:1 ground, same pattern as the other
        // container conversions (CharacterDossierScreenTests, RelicDraft
        // ScreenTests).
        [Test]
        public void TheExitsPaneIsASilverTwoByOneContainer()
        {
            var ground = ExitsScreen.Build().Root;

            Assert.IsFalse(ground.Decor,
                "the wrapper must stay non-Decor, or content beneath it audits clean against itself");
            var art = ground.Children.Single(c => c.Kind == UiNodeKind.Sprite);
            Assert.AreEqual("UI/Buttons/Processed/container_silver_2x1.png", art.SpriteKey);
            Assert.IsTrue(art.Decor);
        }

        [Test]
        public void TheExitsPaneContentSitsInsideTheMeasuredInset()
        {
            var ground = ExitsScreen.Build().Root;
            var content = ground.Children.Single(c => c.Name == "ExitsPaneContent");
            var inset = Ui.ContainerContentInset(ContainerRatio.TwoByOne);

            Assert.AreEqual(PlaceKind.Stretch, content.Place.Kind);
            Assert.AreEqual(ExitsLayout.PaneWidth * inset.Left, content.Place.Left, 0.01f);
            Assert.AreEqual(ExitsLayout.PaneWidth * inset.Right, content.Place.Right, 0.01f);
            Assert.AreEqual(ExitsLayout.PaneHeight * inset.Top, content.Place.Top, 0.01f);
            Assert.AreEqual(ExitsLayout.PaneHeight * inset.Bottom, content.Place.Bottom, 0.01f);
        }

        [Test]
        public void TheExitsStackStaysInsideThePane()
        {
            Assert.IsTrue(ExitsLayout.StackFits,
                $"the Main menu pane reaches y {ExitsLayout.StackBottom:F0} against a floor of " +
                $"{ExitsLayout.ContentBottom:F0}");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TheTwoExitsDoNotOverlapInEitherContext(bool withAbandon)
        {
            float lower = ExitsLayout.ExitBlockCentreY(ExitsLayout.ExitIndexTitle, withAbandon)
                          - ExitsLayout.ExitBlockHeight * 0.5f;
            float upper = ExitsLayout.ExitBlockCentreY(ExitsLayout.ExitIndexQuit, withAbandon)
                          + ExitsLayout.ExitBlockHeight * 0.5f;

            Assert.GreaterOrEqual(lower - upper, 0f, "the two exits run into each other");
            Assert.AreEqual(ExitsLayout.ExitGap, lower - upper, 0.001f,
                "the gap between the two exits is not the declared one");
        }

        // With the card, the three-piece stack is centred; without it, the pair
        // is. A fixed layout has to be wrong in one of the two contexts, and the
        // one it would be wrong in is the hub - the only way out of the game
        // there, left floating in the pane's top third.
        [Test]
        public void EachContextIsCentredOnItsOwnContents()
        {
            float stackTop = ExitsLayout.ExitBlockCentreY(0, withAbandon: true)
                             + ExitsLayout.ExitBlockHeight * 0.5f;
            Assert.AreEqual(stackTop, -ExitsLayout.StackBottom, 0.001f,
                "the full stack is not centred in the pane");

            float pairTop = ExitsLayout.ExitBlockCentreY(0, withAbandon: false)
                            + ExitsLayout.ExitBlockHeight * 0.5f;
            float pairBottom = ExitsLayout.ExitBlockCentreY(ExitsLayout.ExitCount - 1, withAbandon: false)
                               - ExitsLayout.ExitBlockHeight * 0.5f;
            Assert.AreEqual(pairTop, -pairBottom, 0.001f,
                "the two exits are not centred when the abandon card is absent");
        }

        // ABANDON IS SET APART, and that is the design's whole shape for this
        // pane: two ways out of the game above a rule, and the one that throws
        // a descent away below it.
        [Test]
        public void AbandonSitsBelowTheRuleThatSeparatesIt()
        {
            float abandonTop = ExitsLayout.AbandonCentreY + ExitsLayout.AbandonHeight * 0.5f;

            Assert.Less(abandonTop, ExitsLayout.SeparatorY,
                "the abandon card is not below the separator that is supposed to set it apart");
            Assert.Less(ExitsLayout.SeparatorY, ExitsLayout.PairBottom,
                "the separator is not below the two exits it separates from");
        }

        // The fill grows rightwards from the track's left edge. Placed anywhere
        // else it grows from the middle, or -- the mistake the Options slider
        // records in its own comment -- spans the parent instead.
        [Test]
        public void TheHoldFillStartsAtTheTracksLeftEdge()
        {
            Assert.AreEqual(-ExitsLayout.HoldWidth * 0.5f, ExitsLayout.HoldFillLeft, 0.001f,
                "the hold's fill does not start at the left edge of its own track");
        }

        [Test]
        public void TheExitsPaneDeclaresTwoExitsAndAnAbandonCard()
        {
            var screen = ExitsScreen.Build();

            Assert.AreEqual(ExitsLayout.ExitCount, screen.ExitButtons.Count,
                "the Main menu pane does not declare the exits its layout is measured for");
            Assert.AreEqual(screen.ExitButtons.Count, screen.ExitHovers.Count,
                "an exit has a button and no hover plate, or the other way round");

            Assert.AreEqual(screen.ExitButtons.Count, screen.ExitBlocks.Count,
                "an exit has no container, so nothing can move it when the context changes");

            Assert.IsTrue(screen.AbandonCard.IsValid, "there is no abandon card");
            Assert.IsTrue(screen.AbandonHold.IsValid, "the abandon card has no hold button");
            Assert.IsTrue(screen.AbandonFill.IsValid, "the hold has nothing to draw its progress in");
        }

        // Absent, never greyed -- the same rule the tab bar follows one level
        // up. The card starts switched off and the controller raises it only in
        // a descent.
        [Test]
        public void TheAbandonCardAndItsRuleStartSwitchedOff()
        {
            var screen = ExitsScreen.Build();

            Assert.IsTrue(screen.AbandonCard.Node.StartInactive,
                "the abandon card is built active, so it shows between descents where there is no " +
                "descent to abandon");

            // A line with nothing under it is not a separator, it is a stray
            // mark under two buttons.
            Assert.IsTrue(screen.Separator.Node.StartInactive,
                "the separator outlives the card it separates from");
        }

        // ---- the Built flag has to mean something ---------------------------------

        // A tab claiming content it does not have is worse than one admitting
        // it: DefaultFor reads this flag to decide where the menu opens, so a
        // lie here lands the player on the words CONTENT TO COME.
        [Test]
        public void EveryTabMarkedBuiltHasItsPlaceholderSwitchedOff()
        {
            var screen = SystemMenuScreen.Build();
            var owners = SystemMenuTabs.PaneOwners;

            for (int i = 0; i < owners.Count; i++)
            {
                var def = owners[i];
                bool placeholderShowing = !screen.PanePlaceholders[i].Node.StartInactive;

                Assert.AreEqual(!def.Built, placeholderShowing,
                    $"the {def.Key} tab says Built={def.Built} but its pane " +
                    (placeholderShowing ? "still shows the placeholder" : "hides the placeholder"));
            }
        }

        // The panes that are built are built from their own screen classes, so
        // this catches a pane wired to nothing as well as a flag out of step.
        [Test]
        public void TheBuiltPanesHostARealScreen()
        {
            var screen = SystemMenuScreen.Build();

            var hosted = new Dictionary<SystemMenuTab, bool>
            {
                { SystemMenuTab.CharacterInventory, screen.Dossier != null },
                { SystemMenuTab.Options, screen.Options != null },
                { SystemMenuTab.RunStats, screen.RunStats != null },
                { SystemMenuTab.MainMenu, screen.Exits != null },
            };

            foreach (var pair in hosted)
            {
                Assert.IsTrue(SystemMenuTabs.All[SystemMenuTabs.IndexOf(pair.Key)].Built,
                    $"{pair.Key} hosts a screen but is not marked Built");
                Assert.IsTrue(pair.Value, $"{pair.Key} is marked Built but hosts nothing");
            }
        }
        // ---- the loadout silhouette ---------------------------------------------
        //
        // THE HANDOVER'S OWN RULE: scale the whole panel as one unit, never
        // convert individual values to percentages, because the mannequin slots
        // and their leader hairlines are positioned against each other and only
        // stay coherent under UNIFORM scale. Growing the stage 1.179 across and
        // 1.109 down broke it -- the figure stretched wider than it grew tall
        // while the slots moved on a third ratio, so every icon crept off the
        // part of the body it names.
        [Test]
        public void TheStageScalesAsOneUnit()
        {
            Assert.LessOrEqual(DossierLayout.StageWidth, DossierLayout.StageFitWidth + 0.001f,
                "the stage is wider than the column allows");
            Assert.LessOrEqual(DossierLayout.StageHeight, DossierLayout.StageFitHeight + 0.001f,
                "the stage is taller than the pane allows");

            // Same ratio in and out, which is what "one unit" means.
            Assert.AreEqual(
                DossierLayout.AuthoredStageWidth / DossierLayout.AuthoredStageHeight,
                DossierLayout.StageWidth / DossierLayout.StageHeight,
                0.0001f,
                "the stage no longer has the aspect its slot positions were authored against, so " +
                "the icons cannot line up with the figure whatever else is done to them");
        }

        // "Perfectly centered in the silhouette part", both ways.
        [Test]
        public void TheSilhouetteIsCentredInItsOwnColumn()
        {
            // The mannequin's centre, in dossier coordinates, via the same
            // FromStage every slot goes through -- so this measures what the
            // figure actually gets rather than what the constants say.
            var body = DossierLayout.FromStage(
                DossierLayout.MannequinLeft / DossierLayout.StageScale,
                DossierLayout.MannequinTop / DossierLayout.StageScale,
                DossierLayout.MannequinWidth,
                DossierLayout.MannequinHeight);

            Assert.AreEqual(DossierLayout.ColumnBCentreX, body.X, 0.5f,
                "the silhouette is not centred in the loadout column");

            float left = DossierLayout.ColumnBCentreX - DossierLayout.StageWidth * 0.5f;
            float right = DossierLayout.ColumnBCentreX + DossierLayout.StageWidth * 0.5f;
            float columnLeft = -DossierLayout.HalfWidth + DossierLayout.ColumnAWidth;
            float columnRight = DossierLayout.HalfWidth - DossierLayout.ColumnCWidth;

            Assert.AreEqual(left - columnLeft, columnRight - right, 0.5f,
                "the stage sits off-centre in its column - one margin is wider than the other");
        }

        // The three columns still have to sum to the CONTENT canvas -- the
        // outer Blue 2:1 container's own inset boundary (1488, balance-bot
        // 2026-09-02), not the declared 1600px frame any more.
        [Test]
        public void TheThreeColumnsFillTheContentCanvas()
        {
            Assert.AreEqual(DossierLayout.ContentWidth,
                DossierLayout.ColumnAWidth + DossierLayout.ColumnBWidth + DossierLayout.ColumnCWidth,
                0.001f, "the three columns no longer fill the pane's content canvas");

            // COLUMN A'S WIDTH IS DERIVED, not the old authored 449 (374 base
            // + a 75px give-up from column B) -- it is capped now by fitting
            // its own Blue 3:4 container inside the OUTER container's shrunk
            // content height, with the same 4px margin ExitsLayout/
            // OptionsLayout use over their own container's inset. Pinned
            // literal, not a re-derivation of DossierLayout's own formula
            // (that would be a tautology) -- 416.05, was 449.
            Assert.AreEqual(416.04528f, DossierLayout.ColumnAWidth, 0.01f,
                "column A's width no longer matches what fits the outer container's content height");
        }

        // ---- the pack, two abreast and scrolling ---------------------------------

        [Test]
        public void ThePackShowsTwoEntriesAbreast()
        {
            Assert.AreEqual(2, (int)DossierLayout.PackColumns,
                "the pack is not two abreast, which is what buys each entry room for its name");
        }

        // The rows and the scrollbar together have to fit the column's content
        // width exactly, or the bar sits over the second column of entries.
        [Test]
        public void TheRowsAndTheScrollbarShareTheColumnExactly()
        {
            float rows = DossierLayout.PackCellWidth * DossierLayout.PackColumns
                         + DossierLayout.PackColumnGap * (DossierLayout.PackColumns - 1f);

            Assert.AreEqual(DossierLayout.PackListWidth, rows, 0.001f,
                "the entry columns do not fill the list width");

            Assert.AreEqual(DossierLayout.ContentAWidth,
                DossierLayout.PackListWidth + DossierLayout.PackScrollbarGap
                    + DossierLayout.PackScrollbarWidth,
                0.001f,
                "the list and its scrollbar do not add up to the column, so the bar overlaps the " +
                "entries or floats off the edge");
        }

        // A half-drawn row at the bottom edge reads as a clipping bug rather
        // than as more to scroll to, and this list has no mask to cut one
        // cleanly -- so the visible count must FLOOR into the space available.
        [Test]
        public void OnlyWholeRowsAreDrawn()
        {
            float used = DossierLayout.PackVisibleRows * DossierLayout.PackCellHeight
                         + (DossierLayout.PackVisibleRows - 1) * DossierLayout.PackRowGap;

            Assert.LessOrEqual(used, DossierLayout.PackListHeight + 0.001f,
                $"{DossierLayout.PackVisibleRows} rows need {used:F0}px of " +
                $"{DossierLayout.PackListHeight:F0}px, so the last one is cut off");

            Assert.Greater(DossierLayout.PackVisibleRows, 1,
                "the pack shows fewer than two rows, which is not a list");
        }

        // The thumb's LENGTH is how much of the bag is on screen. A bag that
        // fits entirely fills the track; one twice as long fills half of it,
        // down to a floor so it never becomes an invisible stub.
        [Test]
        public void TheThumbSaysHowMuchOfTheBagIsShowing()
        {
            int visible = DossierLayout.PackVisibleCells;

            Assert.AreEqual(DossierLayout.PackTrackHeight,
                DossierLayout.PackThumbHeight(visible), 0.001f,
                "a bag that fits should fill the track");

            Assert.Less(DossierLayout.PackThumbHeight(visible * 2),
                DossierLayout.PackTrackHeight,
                "a bag twice the window still filled the whole track");

            Assert.GreaterOrEqual(DossierLayout.PackThumbHeight(visible * 500),
                DossierLayout.PackThumbMinHeight,
                "a very long bag shrank the thumb below the point it can be grabbed");
        }

        // The cells are a WINDOW rather than the capacity, which is the whole
        // of the scrolling: a 25th item used to be simply absent.
        [Test]
        public void EveryCellSitsInsideTheListArea()
        {
            for (int row = 0; row < DossierLayout.PackVisibleRows; row++)
            {
                float top = DossierLayout.PackCellCentreY(row) + DossierLayout.PackCellHeight * 0.5f;
                float bottom = DossierLayout.PackCellCentreY(row) - DossierLayout.PackCellHeight * 0.5f;

                Assert.LessOrEqual(top, DossierLayout.PackListTop + 0.001f,
                    $"pack row {row} starts above the list");
                Assert.GreaterOrEqual(bottom, DossierLayout.PackListBottom - 0.001f,
                    $"pack row {row} runs below the list and into the carried footer");
            }
        }

    }
}
