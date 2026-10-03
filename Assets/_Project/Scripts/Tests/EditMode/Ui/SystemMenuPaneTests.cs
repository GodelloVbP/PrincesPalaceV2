using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The system menu's RUN and Exit panes.
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

            Assert.AreEqual(width, RunLayout.PaneWidth, 0.001f, "RUN is not the pane's width");
            Assert.AreEqual(height, RunLayout.PaneHeight, 0.001f, "RUN is not the pane's height");

            Assert.AreEqual(width, GlossaryLayout.PaneWidth, 0.001f, "GLOSSARY is not the pane's width");
            Assert.AreEqual(height, GlossaryLayout.PaneHeight, 0.001f, "GLOSSARY is not the pane's height");

            Assert.AreEqual(width, ExitsLayout.PaneWidth, 0.001f, "the Exit pane is not the pane's width");
            Assert.AreEqual(height, ExitsLayout.PaneHeight, 0.001f, "the Exit pane is not the pane's height");

            Assert.AreEqual(width, PartyLayout.PaneWidth, 0.001f, "Party is not the pane's width");
            Assert.AreEqual(height, PartyLayout.PaneHeight, 0.001f, "Party is not the pane's height");
        }

        // The footer note once sat at ContentBottom + 12 under the Restore
        // button, inside its rect, and the text drew through the button's border.
        [Test]
        public void OptionsFooterNoteIsClearOfTheRestoreButton()
        {
            var groups = OptionRows.Groups;
            float restoreY = OptionsLayout.RestoreCentreY(groups);
            Assert.AreEqual(-312.22f, restoreY, 0.01f);

            // Restore: centre (-380, -312.22), 280 x 56 -> x [-520, -240], y [-340.22, -284.22].
            Assert.AreEqual(-380f, OptionsLayout.ColumnCentreX(0), 0.01f);
            Assert.AreEqual(280f, OptionsLayout.RestoreWidth, 0.01f);
            Assert.AreEqual(56f, OptionsLayout.RestoreHeight, 0.01f);
            // Note: centre (380, -312.22), 640 x 27 (one Body 20 line) -> x [60, 700], y [-325.72, -298.72].
            Assert.AreEqual(380f, OptionsLayout.ColumnCentreX(1), 0.01f);
            Assert.AreEqual(640f, OptionsLayout.NoteWidth, 0.01f);
            Assert.AreEqual(27f, OptionsLayout.NoteHeight, 0.01f);

            float restoreRight = -380f + 140f;
            float noteLeft = 380f - 320f;
            Assert.Less(restoreRight, noteLeft, "the note's rect intersects the Restore button's");

            // Both stay above the pane floor (-353.78) and inside the column's right edge.
            Assert.GreaterOrEqual(restoreY - 28f, OptionsLayout.ContentBottom);
            Assert.LessOrEqual(380f + 320f, OptionsLayout.HalfWidth);
        }

        // And their contents reach the floor rather than stopping in the middle
        // of it. Stated as a floor on how much of the usable height the tallest
        // thing in each pane uses -- an exact fit would be a change-detector,
        // and the failure being guarded against is a pane a THIRD empty.
        [TestCase("dossier")]
        [TestCase("options")]
        [TestCase("run")]
        [TestCase("exits")]
        [TestCase("party")]
        public void EveryPaneUsesMostOfItsHeight(string pane)
        {
            float used;
            float usable;

            switch (pane)
            {
                case "dossier":
                    // The rail and the detail area run from the strip's foot to
                    // the pane's working floor.
                    usable = DossierLayout.AreaHeight;
                    used = DossierLayout.StripHeight + DossierLayout.StripGap + DossierLayout.BodyHeight;
                    break;

                case "options":
                    usable = OptionsLayout.UsableHeight;
                    used = OptionsLayout.ColumnHeight(OptionRows.Groups, 0);
                    break;

                case "run":
                    // The relic column is the tallest: its last row's bottom
                    // edge closes the pane.
                    usable = RunLayout.AreaHeight;
                    used = RunLayout.Top - RunLayout.RelicRowsBottom;
                    break;

                case "party":
                    // The roster row is the last structural content down the
                    // pane, so its own bottom edge is what "used" measures
                    // against.
                    usable = PartyLayout.UsableHeight;
                    used = PartyLayout.ContentTop
                           - (PartyLayout.RosterRowCentreY - PartyLayout.CardHeight * 0.5f);
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

        // The Options pane's container theme/ratio and content inset are
        // covered by KitContainerPlacementTests, not repeated here.

        // ---- the Exit pane --------------------------------------------------

        // The Exits pane's container theme/ratio and content inset are
        // covered by KitContainerPlacementTests, not repeated here.

        [Test]
        public void TheExitsStackStaysInsideThePane()
        {
            Assert.IsTrue(ExitsLayout.StackFits,
                $"the Exit pane reaches y {ExitsLayout.StackBottom:F0} against a floor of " +
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
                "the Exit pane does not declare the exits its layout is measured for");
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

        // ---- every tab has a pane behind it ----------------------------------------

        // The build refuses a tab with no branch, so this pins the other half:
        // each listed tab really hosts its own screen class, and the table of
        // expectations covers every tab so a new entry cannot skip the check.
        [Test]
        public void EveryTabHostsARealScreen()
        {
            var screen = SystemMenuScreen.Build();

            var hosted = new Dictionary<SystemMenuTab, bool>
            {
                { SystemMenuTab.Character, screen.Dossier != null },
                { SystemMenuTab.Party, screen.Party != null },
                { SystemMenuTab.Options, screen.Options != null },
                { SystemMenuTab.Run, screen.Run != null },
                { SystemMenuTab.Glossary, screen.Glossary != null },
                { SystemMenuTab.Exit, screen.Exits != null },
            };

            Assert.AreEqual(SystemMenuTabs.Count, hosted.Count,
                "a tab was added without an expectation here");

            foreach (var pair in hosted)
            {
                Assert.IsTrue(pair.Value, $"{pair.Key} is listed but hosts nothing");
            }
        }

        // The scrim hides the host completely. At any alpha below FF a bright
        // host control still reads through it: the map's AbandonRunButton sits
        // at x 40..280, y 58..118, partly outside the panel and its plates, and
        // showed as a faint "ABANDON" beside the top bar on every tab.
        [Test]
        public void TheScrimIsOpaqueSoNoHostControlReadsThrough()
        {
            var dimmer = SystemMenuScreen.Build().Root.Children
                .Single(c => c.Name == "SystemMenuPanelDimmer");

            Assert.AreEqual("#0A0614FF", dimmer.ColorHex);
        }
    }
}
