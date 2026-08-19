using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // THE list of tabs on the overarching menu.
    //
    // Adding a section to that menu is meant to be one entry here and nothing
    // else: the bar computes every position from the index (SystemMenuLayout),
    // the screen builds a tab and an empty pane per entry, the controller
    // switches on index, and a build-time check refuses a strip that no longer
    // fits. That is the whole contract, and it is written down because "add
    // another one later" is the stated reason this menu exists.
    //
    // The panes are DELIBERATELY EMPTY. This is a skeleton; the content is a
    // design job, and each pane is a named container waiting for it.
    public enum SystemMenuTab
    {
        Character,
        Inventory,
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

        public SystemMenuTabDef(SystemMenuTab tab, string key, UiString label)
        {
            Tab = tab;
            Key = key;
            Label = label;
        }
    }

    public static class SystemMenuTabs
    {
        public static readonly IReadOnlyList<SystemMenuTabDef> All = new[]
        {
            new SystemMenuTabDef(SystemMenuTab.Character, "Character", UiStrings.SystemTabCharacter),
            new SystemMenuTabDef(SystemMenuTab.Inventory, "Inventory", UiStrings.SystemTabInventory),
            new SystemMenuTabDef(SystemMenuTab.Options,   "Options",   UiStrings.SystemTabOptions),
            new SystemMenuTabDef(SystemMenuTab.MainMenu,  "MainMenu",  UiStrings.SystemTabMainMenu),
        };

        public static int Count => All.Count;

        public static int IndexOf(SystemMenuTab tab)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Tab == tab) return i;
            }

            return 0;
        }
    }
}
