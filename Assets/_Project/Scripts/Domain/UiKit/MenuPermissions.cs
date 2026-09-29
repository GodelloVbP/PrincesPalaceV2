using System;

namespace PrincesPalace.Domain.UiKit
{
    // The one table saying which menu action a context allows (plan
    // docs/PLAN_SYSTEM_MENU.md C3). Controls ask it to decide how a control
    // looks and whether a press does anything; the Core operations ask it
    // again, so a caller that never touches the menu is refused the same way.
    //
    // A fight is read-only: every row that mutates is refused there, and only
    // options and exits stay open. The other refusals carry their own reason,
    // because "why" differs (a refund is a camp privilege, a book exists only
    // inside a descent).
    public static class MenuPermissions
    {
        private readonly struct Row
        {
            public readonly bool Camp;
            public readonly bool Descent;
            public readonly bool Fight;

            // Why a context that is neither Fight nor allowed is refused. A
            // fight refusal always says UiStrings.MenuLockedInFight.
            public readonly UiString OutsideFightReason;

            public Row(bool camp, bool descent, bool fight, UiString outsideFightReason)
            {
                Camp = camp;
                Descent = descent;
                Fight = fight;
                OutsideFightReason = outsideFightReason;
            }

            public bool Allows(MenuContext context)
            {
                switch (context)
                {
                    case MenuContext.Camp: return Camp;
                    case MenuContext.Descent: return Descent;
                    case MenuContext.Fight: return Fight;
                    default: throw new ArgumentOutOfRangeException(nameof(context), context, null);
                }
            }
        }

        private static Row Of(MenuAction action)
        {
            switch (action)
            {
                case MenuAction.EquipGear:
                case MenuAction.UnequipGear:
                case MenuAction.SpendAttribute:
                case MenuAction.RepositionParty:
                case MenuAction.CollectReward:
                case MenuAction.CycleTitle:
                    return new Row(true, true, false, default);

                case MenuAction.PlaceBook:
                case MenuAction.ReplaceBook:
                case MenuAction.RemoveBook:
                    return new Row(false, true, false, UiStrings.MenuBooksInDescent);

                case MenuAction.RefundAttribute:
                    return new Row(true, false, false, UiStrings.MenuRefundAtCamp);

                case MenuAction.SwapParty:
                    return new Row(true, false, false, UiStrings.MenuPartyAtCamp);

                case MenuAction.ChangeOption:
                case MenuAction.ExitAction:
                    return new Row(true, true, true, default);

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        public static bool Allows(MenuAction action, MenuContext context) =>
            Of(action).Allows(context);

        // The refusal reason, or a default UiString (null Key) when the
        // action is allowed.
        public static UiString Reason(MenuAction action, MenuContext context)
        {
            var row = Of(action);
            if (row.Allows(context)) return default;

            return context == MenuContext.Fight ? UiStrings.MenuLockedInFight : row.OutsideFightReason;
        }
    }
}
