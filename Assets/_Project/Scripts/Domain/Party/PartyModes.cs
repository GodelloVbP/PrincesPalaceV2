using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Party
{
    // The party mode a menu context implies, read off MenuPermissions so the
    // formation and the rest of the menu cannot disagree about what a context
    // may change: no repositioning is ViewOnly, swapping is Camp, and
    // repositioning without swapping is Run.
    public static class PartyModes
    {
        public static PartyMode For(MenuContext context)
        {
            if (!MenuPermissions.Allows(MenuAction.RepositionParty, context)) return PartyMode.ViewOnly;

            return MenuPermissions.Allows(MenuAction.SwapParty, context) ? PartyMode.Camp : PartyMode.Run;
        }
    }
}
