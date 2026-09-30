using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.UiKit
{
    // THE list of tabs on the overarching menu.
    //
    // Adding a section is meant to be one entry here and nothing else: the bar
    // computes every position from this table (SystemMenuLayout), the screen
    // builds a tab and a pane per entry, the controller switches on index, and
    // the build refuses a strip that no longer fits.
    //
    // The set is context-driven, not fixed. A tab that cannot be used is
    // absent, never greyed: a greyed tab asks the player to work out why, and
    // there is nowhere on a tab to answer. At camp the menu has five tabs;
    // in a descent or a fight it has six, because RUN has something to show
    // there.
    public enum SystemMenuTab
    {
        // The dossier, which carries its own pack. C and I are both keys onto
        // this tab.
        Character,

        // Not RunOnly: Party is meaningful in all three of its own modes.
        // Listed before the run-only tab so its position relative to it is the
        // same in both sets.
        Party,

        // The descent at a glance: relics, effects in force, supplies and the
        // figures. Run-only.
        Run,

        // Everything the game contains, by category (plan C25). Not RunOnly:
        // a player looks things up at camp as much as in a fight.
        Glossary,
        Options,
        Exit,
    }

    // Which end of the bar a tab sits at. The game group is what the player
    // is playing with and packs from the left; the system group is the way out
    // and packs flush right, in a quieter colour (SystemMenuLayout).
    public enum SystemMenuTabGroup
    {
        Game,
        System,
    }

    public readonly struct SystemMenuTabDef
    {
        // Domain is engine-free, so a hotkey is the character on the key and
        // this is what "none" looks like.
        public const char NoHotkey = (char)0;

        public readonly SystemMenuTab Tab;

        // The node-name stem. Every node this tab owns is named from it, so a
        // test or the screenshot tool can find a pane without knowing the
        // index -- and renaming a tab renames its parts together.
        public readonly string Key;

        public readonly UiString Label;

        // What the bar draws when the full labels do not fit
        // (SystemMenuLayout.Arrange). Every tab has one.
        public readonly UiString ShortLabel;

        public readonly SystemMenuTabGroup Group;

        // The plain key that opens or toggles this tab. The keys that go to a
        // target (I, for the pack) live in SystemMenuTabs.Hotkeys.
        public readonly char Hotkey;

        // Only shown while a run is in progress.
        public readonly bool RunOnly;

        // What LB/RB page within this tab (plan C8), as the footer's prompt
        // label. Default (invalid) for a tab that pages nothing, and then no
        // pager prompt is shown.
        public readonly UiString Pages;

        public SystemMenuTabDef(SystemMenuTab tab, string key, UiString label, UiString shortLabel,
                                SystemMenuTabGroup group, char hotkey = NoHotkey, bool runOnly = false,
                                UiString pages = default)
        {
            Tab = tab;
            Key = key;
            Label = label;
            ShortLabel = shortLabel;
            Group = group;
            Hotkey = hotkey;
            RunOnly = runOnly;
            Pages = pages;
        }

        public bool HasHotkey => Hotkey != NoHotkey;
    }

    public static class SystemMenuTabs
    {
        // In the order the bar draws them: the game group, then the system
        // group. The camp set is this list with the run-only entries removed,
        // so the two sets cannot disagree about relative order.
        public static readonly IReadOnlyList<SystemMenuTabDef> All = new[]
        {
            new SystemMenuTabDef(SystemMenuTab.Character, "Character",
                UiStrings.SystemTabCharacter, UiStrings.SystemTabCharacterShort,
                SystemMenuTabGroup.Game, hotkey: 'C', pages: UiStrings.PromptPagesCharacter),
            new SystemMenuTabDef(SystemMenuTab.Party, "Party",
                UiStrings.SystemTabParty, UiStrings.SystemTabPartyShort,
                SystemMenuTabGroup.Game, hotkey: 'P'),
            new SystemMenuTabDef(SystemMenuTab.Run, "Run",
                UiStrings.SystemTabRun, UiStrings.SystemTabRunShort,
                SystemMenuTabGroup.Game, hotkey: 'R', runOnly: true, pages: UiStrings.PromptPagesRun),
            new SystemMenuTabDef(SystemMenuTab.Glossary, "Glossary",
                UiStrings.SystemTabGlossary, UiStrings.SystemTabGlossaryShort,
                SystemMenuTabGroup.Game, hotkey: 'G', pages: UiStrings.PromptPagesGlossary),
            new SystemMenuTabDef(SystemMenuTab.Options, "Options",
                UiStrings.SystemTabOptions, UiStrings.SystemTabOptionsShort,
                SystemMenuTabGroup.System),
            new SystemMenuTabDef(SystemMenuTab.Exit, "Exit",
                UiStrings.SystemTabExit, UiStrings.SystemTabExitShort,
                SystemMenuTabGroup.System),
        };

        // ---- hotkeys ------------------------------------------------------------
        //
        // Every key and button that opens the menu somewhere, read by the one
        // dispatcher (NavigationInputModule) and answered by MenuHotkeys. The
        // plain tab keys come from the table above, so a tab's key is one
        // field; the two entries after them are the ones the table cannot say.
        // Declared after All: static initialisers run in textual order.
        public const string ViewAxis = "View";

        // I: the Character tab, with the pack up.
        public const char PackKey = 'I';

        // The Input Manager button a plain key is bound to.
        public static string AxisFor(char key) => "Hotkey" + char.ToUpperInvariant(key);

        public static readonly IReadOnlyList<MenuHotkeyDef> Hotkeys = BuildHotkeys();

        private static IReadOnlyList<MenuHotkeyDef> BuildHotkeys()
        {
            var list = new List<MenuHotkeyDef>();
            foreach (var def in All)
            {
                if (def.HasHotkey) list.Add(new MenuHotkeyDef(AxisFor(def.Hotkey), def.Tab));
            }

            list.Add(new MenuHotkeyDef(AxisFor(PackKey), SystemMenuTab.Character, MenuTarget.Pack));
            list.Add(new MenuHotkeyDef(ViewAxis, tab: null));
            return list;
        }

        public static MenuHotkeyDef HotkeyFor(char key)
        {
            string axis = AxisFor(key);
            foreach (var hotkey in Hotkeys)
            {
                if (hotkey.Axis == axis) return hotkey;
            }

            throw new System.ArgumentOutOfRangeException(nameof(key), key, "no such menu hotkey");
        }

        // The pad's View button: the run at a glance where there is a run, the
        // squad at camp.
        public static SystemMenuTab ViewTabFor(MenuContext context) =>
            context == MenuContext.Camp ? SystemMenuTab.Character : SystemMenuTab.Run;

        public static int Count => All.Count;

        // EVERY tab gets a pane built, including the run-only one.
        //
        // Which tabs are VISIBLE varies at runtime; which panes EXIST cannot,
        // because the scene is generated once and UiCountAudit pins the array
        // lengths. So the build makes all six and the controller shows the
        // five or six the context calls for.
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

        public static SystemMenuTabDef Def(SystemMenuTab tab) => All[IndexOf(tab)];

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

        // Where the menu opens when nothing is remembered (plan C7): the squad
        // at camp and on the map, the run in a fight, where a player opens the
        // menu to look something up. Callers go through MenuMemory.TabFor, the
        // one seam for "the tab to open on"; a tab the caller names wins over
        // both.
        public static SystemMenuTab DefaultFor(MenuContext context) =>
            context == MenuContext.Fight ? SystemMenuTab.Run : SystemMenuTab.Character;
    }
}
