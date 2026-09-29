using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Which game-group tab the menu last showed in each context, so it
    // reopens where the player left it (plan C7).
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

        // THE ONE SEAM FOR "THE TAB TO OPEN ON": the remembered tab, else the
        // context's default. A caller that names a tab does not come here.
        public static SystemMenuTab TabFor(MenuContext context) =>
            Last.TryGetValue(context, out var tab) ? tab : SystemMenuTabs.DefaultFor(context);

        public static void Remember(MenuContext context, SystemMenuTab tab)
        {
            if (SystemMenuTabs.Def(tab).Group != SystemMenuTabGroup.Game) return;
            Last[context] = tab;
        }

        public static void Clear() => Last.Clear();
    }
}
