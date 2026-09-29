using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.UiKit
{
    // THE list of tabs on the overarching menu.
    //
    // Adding a section is meant to be one entry here and nothing else: the bar
    // computes every position from this table (SystemMenuLayout), the screen
    // builds a tab and a pane per entry, the controller switches on index, and
    // a build-time check refuses a strip that no longer fits.
    //
    // The set is context-driven, not fixed. A tab that cannot be used is
    // absent, never greyed: a greyed tab asks the player to work out why, and
    // there is nowhere on a tab to answer. At camp the menu has four tabs;
    // in a descent or a fight it has five, because Run statistics has
    // something to show there.
    public enum SystemMenuTab
    {
        // The dossier, which carries its own pack. C and I are both keys onto
        // this tab.
        Character,

        // Not RunOnly: Party is meaningful in all three of its own modes.
        // Listed before the run-only tab so its position relative to it is the
        // same in both sets.
        Party,
        RunStats,
        Options,
        Exit,
    }

    public readonly struct SystemMenuTabDef
    {
        public readonly SystemMenuTab Tab;

        // The node-name stem. Every node this tab owns is named from it, so a
        // test or the screenshot tool can find a pane without knowing the
        // index -- and renaming a tab renames its parts together.
        public readonly string Key;

        public readonly UiString Label;

        // Only shown while a run is in progress.
        public readonly bool RunOnly;

        // The rendered width of Label, in pixels, at 18px Chakra Petch 500 with
        // .14em letter-spacing.
        //
        // The build-time approximation the scene is emitted from, before any
        // text exists to measure; the controller re-lays the bar from the
        // drawn labels on every open. Author the width TMP draws, rounded to a
        // whole pixel: SystemMenuLabelWidthTests holds each figure to the drawn
        // label within a pixel, and these figures are what UiAudit and the
        // capacity guard see.
        public readonly float LabelWidth;

        public SystemMenuTabDef(SystemMenuTab tab, string key, UiString label,
                                float labelWidth, bool runOnly = false)
        {
            Tab = tab;
            Key = key;
            Label = label;
            LabelWidth = labelWidth;
            RunOnly = runOnly;
        }
    }

    public static class SystemMenuTabs
    {
        // In the order the bar draws them. The camp set is this list with the
        // run-only entries removed, so the two sets cannot disagree about
        // relative order: Options and Exit stay last in both.
        public static readonly IReadOnlyList<SystemMenuTabDef> All = new[]
        {
            new SystemMenuTabDef(SystemMenuTab.Character, "Character",
                UiStrings.SystemTabCharacter, labelWidth: 121f),
            new SystemMenuTabDef(SystemMenuTab.Party, "Party",
                UiStrings.SystemTabParty, labelWidth: 63f),
            new SystemMenuTabDef(SystemMenuTab.RunStats, "RunStats",
                UiStrings.SystemTabRunStats, labelWidth: 164f, runOnly: true),
            new SystemMenuTabDef(SystemMenuTab.Options, "Options",
                UiStrings.SystemTabOptions, labelWidth: 88f),
            new SystemMenuTabDef(SystemMenuTab.Exit, "Exit",
                UiStrings.SystemTabExit, labelWidth: 43f),
        };

        public static int Count => All.Count;

        // EVERY tab gets a pane built, including the run-only one.
        //
        // Which tabs are VISIBLE varies at runtime; which panes EXIST cannot,
        // because the scene is generated once and UiCountAudit pins the array
        // lengths. So the build makes all five and the controller shows the
        // four or five the context calls for.
        public static IReadOnlyList<SystemMenuTabDef> PaneOwners => All;

        // Pane index and tab index are the same while no two tabs share a
        // pane. A function rather than an inline, so a tab that borrows
        // another's pane changes one place.
        public static int PaneIndexFor(int tabIndex) =>
            tabIndex < 0 || tabIndex >= All.Count ? 0 : tabIndex;

        // THROWS rather than falling back to 0: every miss would become the
        // Character tab and a caller asking for a tab that does not exist would
        // get a real screen back with no hint that it asked for the wrong
        // thing. Every value of the enum is in All, so this fires only on a bug.
        public static int IndexOf(SystemMenuTab tab)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Tab == tab) return i;
            }

            throw new System.ArgumentOutOfRangeException(
                nameof(tab), tab, "no such tab in SystemMenuTabs.All");
        }

        // The tabs shown in this context, as indices into All.
        //
        // Indices rather than defs, because the screen built its buttons
        // against All and the controller has to switch the right ones on.
        public static IReadOnlyList<int> VisibleIndices(bool inRun)
        {
            var visible = new List<int>();
            for (int i = 0; i < All.Count; i++)
            {
                if (!All[i].RunOnly || inRun) visible.Add(i);
            }

            return visible;
        }

        public static IReadOnlyList<SystemMenuTabDef> Visible(bool inRun) =>
            VisibleIndices(inRun).Select(i => All[i]).ToList();

        // Where the menu opens when the caller did not say: Character in every
        // context, so `inRun` is unused. It is the one seam callers go through
        // for "the tab to open on", which is why the parameter stays. A tab the
        // caller names still wins over this.
        public static SystemMenuTab DefaultFor(bool inRun) => SystemMenuTab.Character;
    }
}
