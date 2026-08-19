using System.Collections.Generic;
using System.Linq;

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

        // Which tab's PANE this one shows.
        //
        // Character and Inventory are two doors into ONE screen -- the dossier
        // carries its own pack, exactly as the old sheet carried its own
        // inventory tab and exactly as C and I behave today. Giving Inventory a
        // second pane would mean two copies of the same screen, so it borrows
        // Character's and opens the pack instead.
        public readonly SystemMenuTab PaneOwner;

        public bool OwnsPane => PaneOwner == Tab;

        public SystemMenuTabDef(SystemMenuTab tab, string key, UiString label, SystemMenuTab? paneOwner = null)
        {
            Tab = tab;
            Key = key;
            Label = label;
            PaneOwner = paneOwner ?? tab;
        }
    }

    public static class SystemMenuTabs
    {
        public static readonly IReadOnlyList<SystemMenuTabDef> All = new[]
        {
            new SystemMenuTabDef(SystemMenuTab.Character, "Character", UiStrings.SystemTabCharacter),
            new SystemMenuTabDef(SystemMenuTab.Inventory, "Inventory", UiStrings.SystemTabInventory,
                paneOwner: SystemMenuTab.Character),
            new SystemMenuTabDef(SystemMenuTab.Options,   "Options",   UiStrings.SystemTabOptions),
            new SystemMenuTabDef(SystemMenuTab.MainMenu,  "MainMenu",  UiStrings.SystemTabMainMenu),
        };

        public static int Count => All.Count;

        // The tabs that actually get a pane built for them.
        public static IReadOnlyList<SystemMenuTabDef> PaneOwners =>
            All.Where(t => t.OwnsPane).ToList();

        // Which PANE a tab index shows. Not the identity function: Inventory
        // shows Character's.
        public static int PaneIndexFor(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= All.Count) return 0;

            var owner = All[tabIndex].PaneOwner;
            var owners = PaneOwners;
            for (int i = 0; i < owners.Count; i++)
            {
                if (owners[i].Tab == owner) return i;
            }

            return 0;
        }

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
