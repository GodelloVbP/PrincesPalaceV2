using System.Collections.Generic;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.UiKit
{
    // Which game-group tab, and which CHARACTER section, the menu last showed
    // in each context, so it reopens where the player left it (plan C7).
    //
    // Session-static, survives scene loads, never saved: the hub, the map and
    // the fight each carry their own menu instance, and a memory held on the
    // instance would forget at every scene change. System-group tabs are never
    // recorded; a visit to Options or Exit is an errand, not a place to
    // return to. Tests clear it through TestGlobals.
    public static class MenuMemory
    {
        private static readonly Dictionary<MenuContext, SystemMenuTab> Last =
            new Dictionary<MenuContext, SystemMenuTab>();

        private static readonly Dictionary<MenuContext, CharacterSection> LastSection =
            new Dictionary<MenuContext, CharacterSection>();

        // THE ONE SEAM FOR "THE TAB TO OPEN ON": the remembered tab, else the
        // context's default. A caller that names a tab does not come here.
        public static SystemMenuTab TabFor(MenuContext context) =>
            Last.TryGetValue(context, out var tab) ? tab : SystemMenuTabs.DefaultFor(context);

        public static void Remember(MenuContext context, SystemMenuTab tab)
        {
            if (SystemMenuTabs.Def(tab).Group != SystemMenuTabGroup.Game) return;
            Last[context] = tab;
        }

        // The CHARACTER section to show, else LOADOUT: the section the plan
        // names as the default for camp and descent alike.
        public static CharacterSection SectionFor(MenuContext context) =>
            LastSection.TryGetValue(context, out var section) ? section : CharacterSection.Loadout;

        public static void RememberSection(MenuContext context, CharacterSection section) =>
            LastSection[context] = section;

        // The order every bag list is shown in (the LOADOUT pack and the shop's
        // PACK): one choice, so the two never disagree. A preference rather
        // than a place, so unlike the pack's filter it outlives a reopening.
        // Session only, like the rest of this class: it is not worth a save
        // field, and Tier is the pack's long-standing default.
        public static BagSortKey PackSort { get; set; } = BagSortKey.Tier;

        public static void Clear()
        {
            PackSort = BagSortKey.Tier;
            Last.Clear();
            LastSection.Clear();
        }
    }
}
