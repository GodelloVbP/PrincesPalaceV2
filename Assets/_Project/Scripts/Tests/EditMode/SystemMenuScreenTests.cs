using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The overarching menu's skeleton.
    //
    // The brief for this screen was "make the top bar relative so we can add
    // more options easily", so that is what these test: not that the bar looks
    // a particular way, but that ADDING A TAB is a one-line change which cannot
    // silently break the layout. Every assertion here is about the arithmetic
    // and the pairing, because the content is a design job that has not
    // happened yet.
    public class SystemMenuScreenTests
    {
        // ---- the bar is relative, not hand-placed ------------------------------

        // The property the whole design rests on: tab N+1 sits exactly one
        // pitch right of tab N, whatever N is. A bar with hand-placed x values
        // passes every other test in this file and still has to be re-authored
        // to add an entry.
        [Test]
        public void EveryTabSitsOnePitchRightOfTheOneBeforeIt()
        {
            float pitch = SystemMenuLayout.TabWidth + SystemMenuLayout.TabGap;

            for (int i = 1; i < 8; i++)
            {
                Assert.AreEqual(pitch,
                    SystemMenuLayout.TabCentreX(i) - SystemMenuLayout.TabCentreX(i - 1), 0.01f,
                    $"tab {i} is not one pitch from tab {i - 1} - the strip is not relative");
            }
        }

        [Test]
        public void TheFirstTabStartsAtTheDeclaredInset()
        {
            float leftEdge = SystemMenuLayout.TabCentreX(0) - SystemMenuLayout.TabWidth * 0.5f;

            Assert.AreEqual(-SystemMenuLayout.HalfWidth + SystemMenuLayout.BarInsetLeft, leftEdge, 0.01f,
                "the strip does not begin at the bar's left inset");
        }

        // A divider belongs to the GAP, not to a tab. If it drifted onto a tab's
        // edge it would read as a border on the tab rather than a separator
        // between two.
        [Test]
        public void EachDividerSitsInTheMiddleOfItsGap()
        {
            for (int i = 0; i < 4; i++)
            {
                float rightOfThis = SystemMenuLayout.TabCentreX(i) + SystemMenuLayout.TabWidth * 0.5f;
                float leftOfNext = SystemMenuLayout.TabCentreX(i + 1) - SystemMenuLayout.TabWidth * 0.5f;

                Assert.AreEqual((rightOfThis + leftOfNext) * 0.5f,
                    SystemMenuLayout.DividerCentreX(i), 0.01f,
                    $"divider {i} is not centred between tabs {i} and {i + 1}");
            }
        }

        // ---- and it refuses to overflow rather than doing it quietly ----------

        // AUDIT #6 and #7 are both the same bug: a strip sized from a count that
        // grew, running into something, found by a player. This screen exists to
        // be added to, so it gets the check up front.
        [Test]
        public void TheCurrentTabsFitTheBar()
        {
            Assert.IsTrue(SystemMenuLayout.StripFits(SystemMenuTabs.Count),
                $"{SystemMenuTabs.Count} tabs do not fit a bar that holds {SystemMenuLayout.MaxTabs()}");

            Assert.LessOrEqual(SystemMenuLayout.StripRightEdge(SystemMenuTabs.Count),
                SystemMenuLayout.HalfWidth - SystemMenuLayout.BarInsetRight + 0.01f,
                "the strip reaches past the bar's right inset");
        }

        // MaxTabs has to be honest in both directions, or the guard is decoration.
        [Test]
        public void MaxTabsIsTheLastCountThatActuallyFits()
        {
            int max = SystemMenuLayout.MaxTabs();

            Assert.LessOrEqual(SystemMenuLayout.StripRightEdge(max),
                SystemMenuLayout.HalfWidth - SystemMenuLayout.BarInsetRight + 0.01f,
                $"MaxTabs says {max} fits, but that strip already overflows");

            Assert.Greater(SystemMenuLayout.StripRightEdge(max + 1),
                SystemMenuLayout.HalfWidth - SystemMenuLayout.BarInsetRight,
                $"MaxTabs says {max}, but {max + 1} would still fit - the bar is under-reporting");

            Assert.IsFalse(SystemMenuLayout.StripFits(max + 1));
        }

        [Test]
        public void ThereIsRoomToGrow()
        {
            // Not a layout rule, a DESIGN one: the stated purpose of this bar is
            // that more options get added to it. Shipping it already full would
            // meet every other assertion here and defeat the point.
            Assert.Greater(SystemMenuLayout.MaxTabs(), SystemMenuTabs.Count,
                "the bar is already at capacity, so the next tab cannot simply be added");
        }

        // ---- the tree matches the table ---------------------------------------

        [Test]
        public void EveryTabGetsAButtonAnUnderlineAndAPane()
        {
            var screen = SystemMenuScreen.Build();

            Assert.AreEqual(SystemMenuTabs.Count, screen.TabButtons.Count, "a tab is missing its button");
            Assert.AreEqual(SystemMenuTabs.Count, screen.TabUnderlines.Count, "a tab is missing its underline");
            Assert.AreEqual(SystemMenuTabs.Count, screen.Panes.Count, "a tab is missing its content pane");
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
                string key = SystemMenuTabs.All[i].Key;
                Assert.AreEqual($"SystemTab{key}", screen.TabButtons[i].Node.Name);
                Assert.AreEqual($"SystemPane{key}", screen.Panes[i].Node.Name);
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
            var keys = SystemMenuTabs.All.Select(t => t.Key).ToList();

            CollectionAssert.AllItemsAreUnique(keys,
                "two tabs share a key, so their nodes would collide by name");
        }
    }
}
