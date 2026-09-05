using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The overarching menu's bar and panes.
    //
    // The brief was "make the top bar relative so we can add more options
    // easily", so that is what these test: not that the bar looks a particular
    // way, but that changing the tab table is a one-line change which cannot
    // silently break the layout.
    //
    // THE THREE-TAB LAYOUT IS ONLY EVER CHECKED HERE. The scene on disk carries
    // the five-tab bar, because that is the set with the most parts and a scene
    // is generated once; the three-tab layout is applied at runtime by
    // SystemMenuController. So UiAudit -- which re-solves what was emitted --
    // cannot see it, and these tests stand in for that: same overlap, inset and
    // flush-right checks, done against the arithmetic instead of the tree.
    public class SystemMenuScreenTests
    {
        private static IReadOnlyList<SystemMenuTabDef> OutOfRun => SystemMenuTabs.Visible(inRun: false);
        private static IReadOnlyList<SystemMenuTabDef> InRun => SystemMenuTabs.Visible(inRun: true);

        // ---- both modes lay out legally ----------------------------------------

        [TestCase(false)]
        [TestCase(true)]
        public void NoTwoTabsOverlap(bool inRun)
        {
            var tabs = SystemMenuTabs.Visible(inRun);
            var lefts = SystemMenuLayout.TabLefts(tabs);
            var widths = SystemMenuLayout.TabWidths(tabs);

            for (int i = 0; i < tabs.Count - 1; i++)
            {
                Assert.LessOrEqual(lefts[i] + widths[i], lefts[i + 1],
                    $"tab {i} ({tabs[i].Key}) runs into tab {i + 1} ({tabs[i + 1].Key})");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheRowStartsAtTheInsetAndEndsFlush(bool inRun)
        {
            var tabs = SystemMenuTabs.Visible(inRun);
            var lefts = SystemMenuLayout.TabLefts(tabs);
            var widths = SystemMenuLayout.TabWidths(tabs);

            Assert.AreEqual(SystemMenuLayout.BarInsetLeft, lefts[0], 0.01f,
                "the first tab does not start at the declared inset");

            float right = lefts[tabs.Count - 1] + widths[tabs.Count - 1];
            Assert.AreEqual(SystemMenuLayout.BarInsetLeft + SystemMenuLayout.RowWidth, right, 0.01f,
                "the row does not end flush against the right inset - the gaps are not sharing out the " +
                "remainder, so the bar will look left-heavy");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EveryDividerSitsInTheMiddleOfItsGap(bool inRun)
        {
            var tabs = SystemMenuTabs.Visible(inRun);
            var lefts = SystemMenuLayout.TabLefts(tabs);
            var widths = SystemMenuLayout.TabWidths(tabs);

            for (int i = 0; i < tabs.Count - 1; i++)
            {
                float expected = -SystemMenuLayout.HalfWidth
                                 + (lefts[i] + widths[i] + lefts[i + 1]) * 0.5f;

                Assert.AreEqual(expected, SystemMenuLayout.DividerCentreX(tabs, i), 0.01f,
                    $"divider {i} is not centred in its gap");
            }
        }

        // The design states these outright, so they are pinned as literals
        // rather than recomputed -- a test that re-derives the formula only
        // proves the formula equals itself.
        [Test]
        public void TheThreeTabRowMatchesTheDesignsNumbers()
        {
            var lefts = SystemMenuLayout.TabLefts(OutOfRun);
            var widths = SystemMenuLayout.TabWidths(OutOfRun);

            Assert.AreEqual(3, OutOfRun.Count, "out of a run the menu should have three tabs");
            CollectionAssert.AreEqual(new[] { 40f, 590f, 1140f }, lefts.Select(l => (float)l).ToArray());
            Assert.That(widths, Is.All.EqualTo(420f));
        }

        [Test]
        public void TheFiveTabRowMatchesTheDesignsNumbers()
        {
            var lefts = SystemMenuLayout.TabLefts(InRun);
            var widths = SystemMenuLayout.TabWidths(InRun);

            Assert.AreEqual(5, InRun.Count, "in a run the menu should have five tabs");
            CollectionAssert.AreEqual(new[] { 320f, 168f, 216f, 140f, 168f }, widths);
            CollectionAssert.AreEqual(new[] { 40f, 487f, 782f, 1125f, 1392f }, lefts);
            Assert.AreEqual(127f, SystemMenuLayout.GapFor(InRun), 0.01f);
        }

        // The underline marks the WORD, not the box. In the three-tab mode the
        // boxes are 420 wide and the words are not, so an underline sized to
        // the box reads as a second divider.
        [Test]
        public void TheUnderlineIsTheLabelsWidthNotTheBoxs()
        {
            var tabs = OutOfRun;
            var widths = SystemMenuLayout.TabWidths(tabs);

            for (int i = 0; i < tabs.Count; i++)
            {
                Assert.AreEqual(tabs[i].LabelWidth + SystemMenuLayout.UnderlineOverhang,
                    SystemMenuLayout.UnderlineWidth(tabs[i]), 0.01f);
                Assert.Less(SystemMenuLayout.UnderlineWidth(tabs[i]), widths[i],
                    $"{tabs[i].Key}'s underline is as wide as its box");
            }
        }

        // ---- the capacity guard -------------------------------------------------

        [Test]
        public void TheCurrentTabsFit()
        {
            Assert.IsTrue(SystemMenuLayout.StripFits(InRun),
                $"the five-tab bar needs {SystemMenuLayout.StripWidth(InRun)}px of a " +
                $"{SystemMenuLayout.RowWidth}px row");
        }

        // The guard is ARITHMETIC, not a count, and this is what that buys: the
        // same number of tabs with longer labels must be refused. A count-based
        // limit would call this fine and ship a bar running off its own panel.
        [Test]
        public void TheGuardRefusesTabsThatAreTooWordyRatherThanTooMany()
        {
            var wordy = SystemMenuTabs.All
                .Select(t => new SystemMenuTabDef(t.Tab, t.Key, t.Label, labelWidth: 400f, runOnly: t.RunOnly))
                .ToList();

            Assert.AreEqual(SystemMenuTabs.Count, wordy.Count, "the count is unchanged");
            Assert.IsFalse(SystemMenuLayout.StripFits(wordy),
                "five 448px boxes need 2336px in a 1520px row and the guard let them through");
        }

        [Test]
        public void ThereIsRoomToGrow()
        {
            var plusOne = InRun.Concat(new[]
            {
                new SystemMenuTabDef(SystemMenuTab.Options, "Spare", UiStrings.SystemTabOptions, labelWidth: 92f),
            }).ToList();

            Assert.IsTrue(SystemMenuLayout.StripFits(plusOne),
                "a sixth tab no longer fits, so the bar is full at five");
        }

        // ---- the tree matches the table ----------------------------------------

        [Test]
        public void EveryTabGetsAButtonAnUnderlineAndAHoverPlate()
        {
            var screen = SystemMenuScreen.Build();

            Assert.AreEqual(SystemMenuTabs.Count, screen.TabButtons.Count, "a tab is missing its button");
            Assert.AreEqual(SystemMenuTabs.Count, screen.TabUnderlines.Count, "a tab is missing its underline");
            Assert.AreEqual(SystemMenuTabs.Count, screen.TabHovers.Count, "a tab is missing its hover plate");
        }

        // EVERY tab gets a pane now. Character and Inventory used to share one;
        // the design merged them into a single tab instead, which is the same
        // decision expressed once rather than twice.
        [Test]
        public void EveryTabOwnsItsOwnPane()
        {
            var screen = SystemMenuScreen.Build();

            Assert.AreEqual(SystemMenuTabs.Count, screen.Panes.Count,
                "the pane count no longer matches the tab count");

            for (int i = 0; i < SystemMenuTabs.Count; i++)
            {
                Assert.AreEqual(i, SystemMenuTabs.PaneIndexFor(i),
                    "a tab is borrowing another's pane again - the controller and this table disagree");
            }
        }

        [Test]
        public void OneBarHoldsEnoughDividersForTheWidestSet()
        {
            var screen = SystemMenuScreen.Build();

            Assert.AreEqual(SystemMenuTabs.Count - 1, screen.TabDividers.Count,
                "the bar cannot draw a divider for every gap the five-tab set has");
        }

        [Test]
        public void TheCharacterPaneCarriesTheDossier()
        {
            var screen = SystemMenuScreen.Build();

            Assert.IsNotNull(screen.Dossier, "the Character & Inventory pane has no dossier in it");
            Assert.IsNotNull(screen.Dossier.Root);
        }

        // The one that catches a half-added tab: the pane and the button have to
        // be named from the SAME key, or the controller switches the wrong pane
        // and everything still counts correctly.
        [Test]
        public void EachPaneIsNamedForItsOwnTab()
        {
            var screen = SystemMenuScreen.Build();

            for (int i = 0; i < SystemMenuTabs.Count; i++)
            {
                Assert.AreEqual($"SystemTab{SystemMenuTabs.All[i].Key}", screen.TabButtons[i].Node.Name);
                Assert.AreEqual($"SystemPane{SystemMenuTabs.All[i].Key}", screen.Panes[i].Node.Name);
            }
        }

        [Test]
        public void EveryPaneStartsSwitchedOffSoTheControllerOwnsTheChoice()
        {
            var screen = SystemMenuScreen.Build();

            CollectionAssert.IsEmpty(
                screen.Panes.Where(p => !p.Node.StartInactive).Select(p => p.Node.Name).ToList(),
                "a pane is declared active, so two could be visible before the controller runs");
        }

        [Test]
        public void TheMenuStartsClosed()
        {
            var screen = SystemMenuScreen.Build();

            Assert.IsTrue(screen.Root.StartInactive,
                "the overarching menu is declared open, so it would cover the game on load");
        }

        [Test]
        public void TabsHaveDistinctKeys()
        {
            CollectionAssert.AllItemsAreUnique(SystemMenuTabs.All.Select(t => t.Key).ToList(),
                "two tabs share a key, so their nodes would collide by name");
        }

        // ---- the context rule ---------------------------------------------------

        [Test]
        public void TheRunOnlyTabsAreAbsentBetweenRuns()
        {
            var out_ = OutOfRun.Select(t => t.Tab).ToList();

            CollectionAssert.DoesNotContain(out_, SystemMenuTab.FloorMap);
            CollectionAssert.DoesNotContain(out_, SystemMenuTab.RunStats);
            CollectionAssert.Contains(out_, SystemMenuTab.CharacterInventory);
        }

        // Relative order has to survive the filter, or the bar reshuffles as a
        // run starts and the tab a player was aiming at moves under the cursor.
        [Test]
        public void FilteringKeepsTheTabsInTheSameOrder()
        {
            var full = InRun.Select(t => t.Tab).ToList();
            var filtered = OutOfRun.Select(t => t.Tab).ToList();

            CollectionAssert.AreEqual(full.Where(filtered.Contains).ToList(), filtered,
                "the out-of-run tabs are not in the order the in-run bar puts them");
        }

        [Test]
        public void TheDefaultTabSuitsTheContext()
        {
            Assert.AreEqual(SystemMenuTab.CharacterInventory, SystemMenuTabs.DefaultFor(inRun: false),
                "opened in the hub, the menu should land on the squad");

            // The design's rule is that a player opening this mid-run is asking
            // about the run, so Floor map is where it should land -- but only
            // once that pane has something in it. Landing there today would
            // open the menu on the words "CONTENT TO COME" every time, which
            // reads as a broken screen rather than as an unfinished one.
            //
            // Written against Built rather than pinned to today's answer, so
            // filling that pane makes the design's rule take effect and this
            // test follow it, with no edit here. That is the whole reason the
            // flag exists rather than a comment saying "change this later".
            var floorMap = SystemMenuTabs.All[SystemMenuTabs.IndexOf(SystemMenuTab.FloorMap)];
            var expected = floorMap.Built ? SystemMenuTab.FloorMap : SystemMenuTab.CharacterInventory;

            Assert.AreEqual(expected, SystemMenuTabs.DefaultFor(inRun: true),
                floorMap.Built
                    ? "opened during a run, the menu should land on where the player is"
                    : "Floor map is still a placeholder, so the mid-run default has to fall back "
                      + "to the dossier rather than open on 'CONTENT TO COME'");
        }
    }
}
