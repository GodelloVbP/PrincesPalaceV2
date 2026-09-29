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
    // THE FOUR-TAB LAYOUT IS ONLY EVER CHECKED HERE. The scene on disk carries
    // the five-tab bar, because that is the set with the most parts and a scene
    // is generated once; the four-tab layout is applied at runtime by
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

        // Pinned as literals, hand-derived once from SystemMenuLayout's rules
        // (measured mode: box = label + 2 * 24 rounded to 4, the gap shares out
        // the rest of the 1520px row, flush right) over the authored label
        // widths 121 / 63 / 164 / 88 / 43 for CHARACTER / PARTY / RUN STATISTICS /
        // OPTIONS / EXIT.
        //
        // Camp: boxes 168 112 136 92 = 508, gap (1520 - 508) / 3 = 337.33.
        [Test]
        public void TheOutOfRunRowMatchesItsOwnArithmetic()
        {
            var lefts = SystemMenuLayout.TabLefts(OutOfRun);
            var widths = SystemMenuLayout.TabWidths(OutOfRun);

            Assert.AreEqual(4, OutOfRun.Count, "at camp the menu should have four tabs");
            CollectionAssert.AreEqual(new[] { 168f, 112f, 136f, 92f }, widths);
            AssertNearlyEqual(new[] { 40f, 545.3333f, 994.6667f, 1468f }, lefts);
            Assert.AreEqual(337.3333f, SystemMenuLayout.GapFor(OutOfRun), 0.01f);

            var dividers = new[] { -423.3333f, 26f, 499.3333f };
            for (int i = 0; i < dividers.Length; i++)
            {
                Assert.AreEqual(dividers[i], SystemMenuLayout.DividerCentreX(OutOfRun, i), 0.01f,
                    $"divider {i}");
            }
        }

        // Descent: boxes 168 112 212 136 92 = 720, gap (1520 - 720) / 4 = 200.
        [Test]
        public void TheInRunRowMatchesItsOwnArithmetic()
        {
            var lefts = SystemMenuLayout.TabLefts(InRun);
            var widths = SystemMenuLayout.TabWidths(InRun);

            Assert.AreEqual(5, InRun.Count, "in a descent the menu should have five tabs");
            CollectionAssert.AreEqual(new[] { 168f, 112f, 212f, 136f, 92f }, widths);
            AssertNearlyEqual(new[] { 40f, 408f, 720f, 1132f, 1468f }, lefts);
            Assert.AreEqual(200f, SystemMenuLayout.GapFor(InRun), 0.01f);

            var dividers = new[] { -492f, -180f, 232f, 568f };
            for (int i = 0; i < dividers.Length; i++)
            {
                Assert.AreEqual(dividers[i], SystemMenuLayout.DividerCentreX(InRun, i), 0.01f,
                    $"divider {i}");
            }
        }

        private static void AssertNearlyEqual(float[] expected, IReadOnlyList<float> actual)
        {
            Assert.AreEqual(expected.Length, actual.Count, "different number of tabs than expected");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i], 0.01f, $"tab {i}'s left edge");
            }
        }

        // The underline marks the WORD, not the box. The boxes are wider than
        // their words, so an underline sized to the box reads as a second
        // divider. Label width + 24 for CHARACTER / PARTY / OPTIONS / EXIT.
        [Test]
        public void TheUnderlineIsTheLabelsWidthNotTheBoxs()
        {
            var tabs = OutOfRun;
            var widths = SystemMenuLayout.TabWidths(tabs);
            var expected = new[] { 145f, 87f, 112f, 67f };

            Assert.AreEqual(expected.Length, tabs.Count, "a tab was added without an expectation here");
            for (int i = 0; i < tabs.Count; i++)
            {
                Assert.AreEqual(expected[i], SystemMenuLayout.UnderlineWidth(tabs[i]), 0.01f, tabs[i].Key);
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
                "five 448px boxes need 2240px in a 1520px row and the guard let them through");
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

            Assert.IsNotNull(screen.Dossier, "the Character pane has no dossier in it");
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

            CollectionAssert.DoesNotContain(out_, SystemMenuTab.RunStats);
            CollectionAssert.Contains(out_, SystemMenuTab.Character);
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
        public void TheDefaultTabIsCharacterInEveryContext()
        {
            Assert.AreEqual(SystemMenuTab.Character, SystemMenuTabs.DefaultFor(inRun: false),
                "opened at camp, the menu should land on the squad");
            Assert.AreEqual(SystemMenuTab.Character, SystemMenuTabs.DefaultFor(inRun: true),
                "opened in a descent, the menu should land on the squad");
        }

        // ---- no frame anywhere in the menu ---------------------------------------

        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }

        // THE MECHANISED FORM OF THE OWNER'S RULE (2026-09-07): "containers in
        // the menu should be removed because it looks bad." Exits, Options,
        // Party, Run statistics, Reward track and the dossier's column A frame
        // all lost their kit art in the same pass this test was added by --
        // this is what stops a future pane from quietly growing one back,
        // rather than relying on everybody who touches this tree remembering
        // a rule stated only in a doc and a commit message.
        [Test]
        public void NoSystemMenuNodeUsesAContainerOrFlagBannerSprite()
        {
            var offenders = Walk(SystemMenuScreen.Build().Root)
                .Where(n => !string.IsNullOrEmpty(n.SpriteKey)
                            && (n.SpriteKey.Contains("container_") || n.SpriteKey.Contains("banner_flag_")))
                .Select(n => $"{n.Name} ({n.SpriteKey})")
                .ToList();

            CollectionAssert.IsEmpty(offenders,
                "the system menu should have no kit frames -- " + string.Join(", ", offenders));
        }
    }
}
