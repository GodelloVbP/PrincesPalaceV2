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
        // The tab labels as if TMP had measured them, in SystemMenuTabs.All
        // order (CHARACTER, PARTY, RUN STATISTICS, OPTIONS, EXIT) -- the spec's
        // 18.3px-a-character estimate at 24 caps, rounded. The real widths come
        // from the Editor's measurement; SystemMenuLabelWidthTests holds the
        // built boxes to what is drawn. The arithmetic below is the rule, and
        // does not care where the widths came from.
        private static readonly float[] Full = { 165f, 92f, 256f, 128f, 73f };

        // CHAR, PARTY, STATS, OPTS, EXIT.
        private static readonly float[] Short = { 90f, 92f, 91f, 73f, 73f };

        private static SystemMenuBar Bar(bool inRun, float[] full = null, float[] brief = null)
        {
            var visible = SystemMenuTabs.VisibleIndices(inRun);
            return SystemMenuLayout.Arrange(
                SystemMenuTabs.Visible(inRun),
                visible.Select(i => (full ?? Full)[i]).ToList(),
                visible.Select(i => (brief ?? Short)[i]).ToList());
        }

        private static IReadOnlyList<SystemMenuTabDef> OutOfRun => SystemMenuTabs.Visible(inRun: false);
        private static IReadOnlyList<SystemMenuTabDef> InRun => SystemMenuTabs.Visible(inRun: true);

        private static float[] Same(float width) => new[] { width, width, width, width, width };

        // ---- both sets lay out legally -----------------------------------------

        [TestCase(false)]
        [TestCase(true)]
        public void NoTwoTabsOverlap(bool inRun)
        {
            var tabs = SystemMenuTabs.Visible(inRun);
            var bar = Bar(inRun);

            for (int i = 0; i < tabs.Count - 1; i++)
            {
                Assert.LessOrEqual(bar.Lefts[i] + bar.Widths[i], bar.Lefts[i + 1],
                    $"tab {i} ({tabs[i].Key}) runs into tab {i + 1} ({tabs[i + 1].Key})");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheGameGroupStartsAtTheInsetAndTheSystemGroupEndsFlushRight(bool inRun)
        {
            var bar = Bar(inRun);
            int last = bar.Count - 1;

            Assert.AreEqual(SystemMenuLayout.BarInsetLeft, bar.Lefts[0], 0.01f,
                "the first tab does not start at the declared inset");
            Assert.AreEqual(SystemMenuLayout.BarInsetLeft + SystemMenuLayout.RowWidth,
                bar.Lefts[last] + bar.Widths[last], 0.01f,
                "the system group does not end flush against the right inset");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EveryDividerSitsInTheMiddleOfItsGap(bool inRun)
        {
            var bar = Bar(inRun);

            for (int i = 0; i < bar.Count - 1; i++)
            {
                float expected = -SystemMenuLayout.HalfWidth
                                 + (bar.Lefts[i] + bar.Widths[i] + bar.Lefts[i + 1]) * 0.5f;

                Assert.AreEqual(expected, bar.DividerCentreX(i), 0.01f, $"divider {i} is not centred in its gap");
            }
        }

        // Pinned as literals, hand-derived from the rule over Full above: a box
        // is its label + 48 rounded to 4 (212 / 140 / 304 / 176 / 120), 48
        // between boxes of a group, the system group flush right, the group gap
        // whatever is left.
        //
        // Camp: game 212 + 48 + 140 = 400 from x 40; system 176 + 48 + 120 =
        // 344 ending at 1560, so from 1216; group gap 1216 - 440 = 776.
        [Test]
        public void TheCampBarMatchesItsOwnArithmetic()
        {
            var bar = Bar(inRun: false);

            Assert.IsFalse(bar.ShortLabels);
            CollectionAssert.AreEqual(new[] { 212f, 140f, 176f, 120f }, bar.Widths);
            CollectionAssert.AreEqual(new[] { 40f, 300f, 1216f, 1440f }, bar.Lefts);
            Assert.AreEqual(776f, bar.GroupGap, 0.01f);
            Assert.AreEqual(840f, bar.Needed, 0.01f);

            var centres = new[] { -654f, -430f, 504f, 700f };
            for (int i = 0; i < centres.Length; i++) Assert.AreEqual(centres[i], bar.CentreX(i), 0.01f, $"centre {i}");

            var dividers = new[] { -524f, 28f, 616f };
            for (int i = 0; i < dividers.Length; i++) Assert.AreEqual(dividers[i], bar.DividerCentreX(i), 0.01f, $"divider {i}");
        }

        // Descent: game 212 + 48 + 140 + 48 + 304 = 752 from x 40, ending at 792;
        // the system group is where it was; group gap 1216 - 792 = 424.
        [Test]
        public void TheDescentBarMatchesItsOwnArithmetic()
        {
            var bar = Bar(inRun: true);

            Assert.IsFalse(bar.ShortLabels);
            CollectionAssert.AreEqual(new[] { 212f, 140f, 304f, 176f, 120f }, bar.Widths);
            CollectionAssert.AreEqual(new[] { 40f, 300f, 488f, 1216f, 1440f }, bar.Lefts);
            Assert.AreEqual(424f, bar.GroupGap, 0.01f);
            Assert.AreEqual(1192f, bar.Needed, 0.01f);

            var dividers = new[] { -524f, -336f, 204f, 616f };
            for (int i = 0; i < dividers.Length; i++) Assert.AreEqual(dividers[i], bar.DividerCentreX(i), 0.01f, $"divider {i}");
        }

        // The underline marks the WORD, not the box: label width + 24. A rule as
        // wide as the box reads as a second divider.
        [Test]
        public void TheUnderlineIsTheLabelsWidthNotTheBoxs()
        {
            var bar = Bar(inRun: false);
            var expected = new[] { 189f, 116f, 152f, 97f };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], SystemMenuLayout.UnderlineWidth(bar.LabelWidths[i]), 0.01f, $"tab {i}");
                Assert.Less(SystemMenuLayout.UnderlineWidth(bar.LabelWidths[i]), bar.Widths[i],
                    $"tab {i}'s underline is as wide as its box");
            }
        }

        // ---- overflow: full labels, then short labels, then refusal -------------

        // The boundary, to the pixel. Four labels of 284 make four 332 boxes:
        // 2 * 332 + 48 per group = 712 each, plus the 96 minimum group gap =
        // 1520, exactly the row. Fits, with a group gap of exactly 96.
        [Test]
        public void FullLabelsThatJustFitAreKept()
        {
            var bar = Bar(inRun: false, full: Same(284f));

            Assert.IsFalse(bar.ShortLabels);
            CollectionAssert.AreEqual(new[] { 40f, 420f, 848f, 1228f }, bar.Lefts);
            Assert.AreEqual(96f, bar.GroupGap, 0.01f);
            Assert.AreEqual(1520f, bar.Needed, 0.01f);
        }

        // One step wordier: 288 makes 336 boxes, 1536 needed. Every label goes
        // short at once -- never a mix -- and the short set is what is laid out.
        [Test]
        public void ALabelSetJustTooWideFallsBackToShortLabelsTogether()
        {
            var bar = Bar(inRun: false, full: Same(288f));

            Assert.IsTrue(bar.ShortLabels);
            CollectionAssert.AreEqual(new[] { 140f, 140f, 120f, 120f }, bar.Widths);
            CollectionAssert.AreEqual(new[] { 90f, 92f, 73f, 73f }, bar.LabelWidths);
        }

        // Wordy full labels on the five-tab set. Short boxes 140 / 140 / 140 /
        // 120 / 120: game 140 * 3 + 96 = 516 from x 40, ending at 556; system
        // 120 + 48 + 120 = 288 from 1272; group gap 1272 - 556 = 716.
        [Test]
        public void AWordySetFallsBackToTheShortLabels()
        {
            var bar = Bar(inRun: true, full: Same(400f));

            Assert.IsTrue(bar.ShortLabels);
            CollectionAssert.AreEqual(new[] { 140f, 140f, 140f, 120f, 120f }, bar.Widths);
            CollectionAssert.AreEqual(new[] { 40f, 228f, 416f, 1272f, 1440f }, bar.Lefts);
            Assert.AreEqual(716f, bar.GroupGap, 0.01f);
        }

        [Test]
        public void IfEvenTheShortLabelsOverflowTheBuildThrows()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => Bar(inRun: true, full: Same(400f), brief: Same(400f)),
                "five 448px boxes cannot be laid out in a 1520px row, short labels or not");
        }

        [Test]
        public void TheSceneBuildThrowsWhenTheMeasuredLabelsOverflowEvenShort()
        {
            var tooWide = new SystemMenuLabelWidths(Same(400f), Same(400f));

            Assert.Throws<System.InvalidOperationException>(() => SystemMenuScreen.Build(3, tooWide));
        }

        [Test]
        public void ATabTableWithAGameTabAfterASystemTabIsRefused()
        {
            var tabs = new[]
            {
                new SystemMenuTabDef(SystemMenuTab.Options, "Options", UiStrings.SystemTabOptions,
                    UiStrings.SystemTabOptionsShort, SystemMenuTabGroup.System),
                new SystemMenuTabDef(SystemMenuTab.Character, "Character", UiStrings.SystemTabCharacter,
                    UiStrings.SystemTabCharacterShort, SystemMenuTabGroup.Game),
            };

            Assert.Throws<System.InvalidOperationException>(
                () => SystemMenuLayout.Arrange(tabs, new[] { 100f, 100f }, new[] { 50f, 50f }));
        }

        [Test]
        public void TheCurrentTabsFitAtTheirFullLabels()
        {
            Assert.IsFalse(Bar(inRun: true).ShortLabels,
                "the five-tab bar no longer fits at full labels, so it has lost its wording");
        }

        [Test]
        public void EveryLabelHasAShortFormNoLongerThanItsFullOne()
        {
            foreach (var def in SystemMenuTabs.All)
            {
                Assert.LessOrEqual(def.ShortLabel.Template.Length, def.Label.Template.Length, def.Key);
            }
        }

        // ---- the table ---------------------------------------------------------

        [Test]
        public void EachTabCarriesItsGroupShortLabelAndHotkey()
        {
            var expected = new[]
            {
                (SystemMenuTab.Character, SystemMenuTabGroup.Game, "CHARACTER", "CHAR", 'C'),
                (SystemMenuTab.Party, SystemMenuTabGroup.Game, "PARTY", "PARTY", 'P'),
                (SystemMenuTab.RunStats, SystemMenuTabGroup.Game, "RUN STATISTICS", "STATS", SystemMenuTabDef.NoHotkey),
                (SystemMenuTab.Options, SystemMenuTabGroup.System, "OPTIONS", "OPTS", SystemMenuTabDef.NoHotkey),
                (SystemMenuTab.Exit, SystemMenuTabGroup.System, "EXIT", "EXIT", SystemMenuTabDef.NoHotkey),
            };

            Assert.AreEqual(expected.Length, SystemMenuTabs.Count, "a tab was added without an expectation here");
            for (int i = 0; i < expected.Length; i++)
            {
                var def = SystemMenuTabs.All[i];
                Assert.AreEqual(expected[i].Item1, def.Tab, $"tab {i}");
                Assert.AreEqual(expected[i].Item2, def.Group, $"{def.Key} group");
                Assert.AreEqual(expected[i].Item3, def.Label.Template, $"{def.Key} label");
                Assert.AreEqual(expected[i].Item4, def.ShortLabel.Template, $"{def.Key} short label");
                Assert.AreEqual(expected[i].Item5, def.Hotkey, $"{def.Key} hotkey");
            }
        }

        [Test]
        public void TheGameGroupIsListedBeforeTheSystemGroup()
        {
            var groups = SystemMenuTabs.All.Select(t => t.Group).ToList();
            CollectionAssert.AreEqual(groups.OrderBy(g => g).ToList(), groups);
        }

        [Test]
        public void TheHotkeysAreTheTabKeysThePackKeyAndView()
        {
            var axes = SystemMenuTabs.Hotkeys.Select(h => h.Axis).ToList();
            CollectionAssert.AreEqual(new[] { "HotkeyC", "HotkeyP", "HotkeyI", "View" }, axes);

            var pack = SystemMenuTabs.HotkeyFor('I');
            Assert.AreEqual(SystemMenuTab.Character, pack.Tab);
            Assert.AreEqual(MenuTargetKind.Section, pack.Target.Kind);
            Assert.AreEqual("pack", pack.Target.Id);

            Assert.IsTrue(SystemMenuTabs.HotkeyFor('C').Target.IsNone);
            Assert.IsNull(SystemMenuTabs.Hotkeys.First(h => h.Axis == "View").Tab,
                "View has no tab of its own: it follows the context");
        }

        [Test]
        public void ViewOpensTheRunInADescentAndTheSquadAtCamp()
        {
            Assert.AreEqual(SystemMenuTab.Character, SystemMenuTabs.ViewTabFor(MenuContext.Camp));
            Assert.AreEqual(SystemMenuTab.RunStats, SystemMenuTabs.ViewTabFor(MenuContext.Descent));
            Assert.AreEqual(SystemMenuTab.RunStats, SystemMenuTabs.ViewTabFor(MenuContext.Fight));
        }

        // The scene's bar is the five-tab arrangement, built from whatever the
        // caller measured: the boxes and centres the tree carries are the ones
        // Arrange gives for those widths.
        [Test]
        public void TheBuiltBarIsTheArrangementOfTheMeasuredWidths()
        {
            var screen = SystemMenuScreen.Build(3, new SystemMenuLabelWidths(Full, Short));
            var bar = Bar(inRun: true);

            for (int i = 0; i < bar.Count; i++)
            {
                var button = screen.TabButtons[i].Node;
                Assert.AreEqual(bar.Widths[i], button.Size.X, 0.01f, $"tab {i} box");
                Assert.AreEqual(bar.CentreX(i), button.Place.Offset.X, 0.01f, $"tab {i} centre");
                Assert.AreEqual(SystemMenuLayout.UnderlineWidth(bar.LabelWidths[i]),
                    screen.TabUnderlines[i].Node.Size.X, 0.01f, $"tab {i} underline");
            }

            for (int i = 0; i < bar.Count - 1; i++)
            {
                Assert.AreEqual(bar.DividerCentreX(i), screen.TabDividers[i].Node.Place.Offset.X, 0.01f,
                    $"divider {i}");
            }
        }

        [Test]
        public void TheBuiltBarUsesShortLabelsWhenTheFullOnesDoNotFit()
        {
            var screen = SystemMenuScreen.Build(3, new SystemMenuLabelWidths(Same(400f), Short));

            for (int i = 0; i < SystemMenuTabs.Count; i++)
            {
                Assert.AreEqual(SystemMenuTabs.All[i].ShortLabel.Template, screen.TabButtons[i].Node.Text.Template,
                    $"tab {i} is not showing its short label");
            }
        }

        // Spec section 2: FunctionalHeading at a literal 24, caps, tracked .14em.
        [Test]
        public void EveryTabLabelIsFunctionalHeadingAt24TrackedFourteenHundredths()
        {
            foreach (var def in SystemMenuTabs.All)
            {
                var label = SystemMenuScreen.TabLabelProbe(def, shortLabel: false);

                Assert.AreEqual(TypographyRole.FunctionalHeading, label.Role, def.Key);
                Assert.AreEqual(24, label.FontSize, def.Key);
                Assert.AreEqual(14f, label.RoleTracking, def.Key);
            }
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

        [TestCase(MenuContext.Camp)]
        [TestCase(MenuContext.Descent)]
        [TestCase(MenuContext.Fight)]
        public void TheDefaultTabIsCharacterInEveryContext(MenuContext context)
        {
            Assert.AreEqual(SystemMenuTab.Character, SystemMenuTabs.DefaultFor(context),
                "with nothing remembered the menu should land on the squad");
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
