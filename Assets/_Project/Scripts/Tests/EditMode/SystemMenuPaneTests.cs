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
                    usable = DossierLayout.ColumnATop - (-DossierLayout.HalfHeight + DossierLayout.PadY);
                    used = DossierLayout.ColumnATop
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

        // ---- the Main menu pane --------------------------------------------------

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
    }
}
