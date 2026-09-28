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
    // The set is CONTEXT-DRIVEN rather than fixed, which is the design pass's
    // answer to "can a tab be disabled": no. Out of a run the menu has four
    // tabs; in a run it has six, because Floor map and Run statistics have
    // something to show. A tab that cannot be used is absent, never greyed --
    // a greyed tab asks the player to work out why, and there is nowhere on a
    // tab to answer.
    public enum SystemMenuTab
    {
        // ONE tab, not two. Character and Inventory were separate doors into a
        // screen that was already both -- the dossier carries its own pack --
        // so the design merged the label rather than keeping a second tab that
        // showed the same pane. C and I both still work; they are keys onto
        // this tab now.
        CharacterInventory,

        // NOT RunOnly, unlike FloorMap/RunStats -- Party is meaningful in all
        // three of its own modes (Camp/Run/ViewOnly), and Camp IS the
        // out-of-run context. Placed here, right after CharacterInventory and
        // before the run-only pair, so its relative order survives the
        // Visible(inRun) filter unchanged in both sets.
        Party,
        FloorMap,
        RunStats,
        Options,
        MainMenu,
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

        // Whether this tab's pane has real content yet, as opposed to a
        // placeholder.
        //
        // Only DefaultFor reads it, and only so the menu cannot open itself
        // onto the words "CONTENT TO COME". An unbuilt tab is still listed and
        // still selectable: a placeholder somebody chose to look at is honest,
        // whereas one the menu picked for them looks like a broken screen.
        public readonly bool Built;

        // The rendered width of Label, in pixels, at 18px Chakra Petch 500 with
        // .14em letter-spacing.
        //
        // AUTHORED, not measured, and that is a liability worth naming: these
        // came off the design's own measuring pass, and nothing in this build
        // re-checks them. Change a label without changing its width here and
        // the bar will lay out around a lie -- tabs will sit slightly wrong and
        // the underline will be the wrong length, which looks like sloppy
        // spacing rather than like stale data.
        //
        // Measuring at build time is the real fix and needs a TMP text instance
        // in the builder; recorded as a follow-up rather than smuggled in here.
        public readonly float LabelWidth;

        public SystemMenuTabDef(SystemMenuTab tab, string key, UiString label,
                                float labelWidth, bool runOnly = false, bool built = false)
        {
            Tab = tab;
            Key = key;
            Label = label;
            LabelWidth = labelWidth;
            RunOnly = runOnly;
            Built = built;
        }
    }

    public static class SystemMenuTabs
    {
        // In the order the bar draws them. The out-of-run set is this list with
        // the run-only entries removed, so the two modes cannot disagree about
        // relative order -- Options and Main menu stay last in both.
        public static readonly IReadOnlyList<SystemMenuTabDef> All = new[]
        {
            new SystemMenuTabDef(SystemMenuTab.CharacterInventory, "CharacterInventory",
                UiStrings.SystemTabCharacterInventory, labelWidth: 272f, built: true),

            // labelWidth ESTIMATED from the neighbours' own measured ratio --
            // "OPTIONS" is 92px for 7 chars (13.14px/char) and "MAIN MENU" is
            // 119px for 9 chars (13.22px/char); averaged, 13.18px/char, times
            // "PARTY"'s 5 chars is 65.9, rounded to 66. Same status as every
            // other authored figure in this table: a build-time approximation
            // SystemMenuLabelWidthTests corrects against the real TMP metrics
            // the moment the menu opens (that test is PlayMode-only and out
            // of reach from a worktree -- see this package's own report).
            new SystemMenuTabDef(SystemMenuTab.Party, "Party",
                UiStrings.SystemTabParty, labelWidth: 66f, built: true),
            new SystemMenuTabDef(SystemMenuTab.FloorMap, "FloorMap",
                UiStrings.SystemTabFloorMap, labelWidth: 120f, runOnly: true),
            new SystemMenuTabDef(SystemMenuTab.RunStats, "RunStats",
                UiStrings.SystemTabRunStats, labelWidth: 168f, runOnly: true, built: true),
            new SystemMenuTabDef(SystemMenuTab.Options, "Options",
                UiStrings.SystemTabOptions, labelWidth: 92f, built: true),
            new SystemMenuTabDef(SystemMenuTab.MainMenu, "MainMenu",
                UiStrings.SystemTabMainMenu, labelWidth: 119f, built: true),
        };

        public static int Count => All.Count;

        // EVERY tab gets a pane built, including the run-only ones.
        //
        // Which tabs are VISIBLE varies at runtime; which panes EXIST cannot,
        // because the scene is generated once and UiCountAudit pins the array
        // lengths. So the build makes all five and the controller shows the
        // three or five the context calls for.
        public static IReadOnlyList<SystemMenuTabDef> PaneOwners => All;

        // Pane index and tab index are the same now that no two tabs share a
        // pane. Kept as a function rather than inlined at the call sites: the
        // moment a tab borrows another's pane again -- which is exactly what
        // Character and Inventory did until this pass -- this is the one place
        // that has to change.
        public static int PaneIndexFor(int tabIndex) =>
            tabIndex < 0 || tabIndex >= All.Count ? 0 : tabIndex;

        // THROWS rather than falling back to 0: returning 0 would be a
        // silently wrong answer dressed as a safe one, since every miss
        // would become "Character & Inventory" and a caller asking for a
        // tab that does not exist would get a real screen back with no
        // hint that it asked for the wrong thing. Every value of the enum
        // is in All, so this cannot fire without a genuine bug behind it.
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

        // Where the menu opens when the caller did not say.
        //
        // Context-appropriate rather than sticky: opened over a fight or the
        // map the player is asking about the run, and opened in the hub they
        // are asking about their squad. An explicit tab from the caller still
        // wins over this.
        public static SystemMenuTab DefaultFor(bool inRun)
        {
            var preferred = inRun ? SystemMenuTab.FloorMap : SystemMenuTab.CharacterInventory;

            // NEVER ONTO A PLACEHOLDER.
            //
            // The design's rule is that a player opening this over a fight or
            // the map is asking about the run, and Floor map is where that
            // lands. But that pane is still empty, so obeying the rule today
            // means every mid-run Escape opens the menu on "CONTENT TO COME".
            // Delete this fallback the moment that pane is built.
            if (All[IndexOf(preferred)].Built) return preferred;

            return SystemMenuTab.CharacterInventory;
        }
    }
}
