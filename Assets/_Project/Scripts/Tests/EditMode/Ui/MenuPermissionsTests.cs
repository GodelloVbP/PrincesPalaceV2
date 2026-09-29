using NUnit.Framework;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The whole of docs/PLAN_SYSTEM_MENU.md C3, cell by cell, as literals: a
    // fight is read-only and every other cell is one deliberate exception.
    public class MenuPermissionsTests
    {
        private const bool Y = true;
        private const bool N = false;

        // action, Camp, Descent, Fight
        private static readonly object[][] Table =
        {
            new object[] { MenuAction.EquipGear, Y, Y, N },
            new object[] { MenuAction.UnequipGear, Y, Y, N },
            new object[] { MenuAction.PlaceBook, N, Y, N },
            new object[] { MenuAction.ReplaceBook, N, Y, N },
            new object[] { MenuAction.RemoveBook, N, Y, N },
            new object[] { MenuAction.SpendAttribute, Y, Y, N },
            new object[] { MenuAction.RefundAttribute, Y, N, N },
            new object[] { MenuAction.SwapParty, Y, N, N },
            new object[] { MenuAction.RepositionParty, Y, Y, N },
            new object[] { MenuAction.CollectReward, Y, Y, N },
            new object[] { MenuAction.CycleTitle, Y, Y, N },
            new object[] { MenuAction.ChangeOption, Y, Y, Y },
            new object[] { MenuAction.ExitAction, Y, Y, Y },
        };

        [TestCaseSource(nameof(Table))]
        public void EveryCellMatchesThePlanTable(object[] row)
        {
            var action = (MenuAction)row[0];

            Assert.AreEqual(row[1], MenuPermissions.Allows(action, MenuContext.Camp), $"{action} at camp");
            Assert.AreEqual(row[2], MenuPermissions.Allows(action, MenuContext.Descent), $"{action} in a descent");
            Assert.AreEqual(row[3], MenuPermissions.Allows(action, MenuContext.Fight), $"{action} in a fight");
        }

        [Test]
        public void TheTableCoversEveryAction()
        {
            // Vacuity guard: a new MenuAction without a row here fails.
            Assert.AreEqual(13, Table.Length);
            Assert.AreEqual(13, System.Enum.GetValues(typeof(MenuAction)).Length);
        }

        [Test]
        public void EveryActionButOptionsAndExitsIsRefusedInAFight()
        {
            foreach (MenuAction action in System.Enum.GetValues(typeof(MenuAction)))
            {
                bool viewOnly = action != MenuAction.ChangeOption && action != MenuAction.ExitAction;
                Assert.AreEqual(!viewOnly, MenuPermissions.Allows(action, MenuContext.Fight), action.ToString());
            }
        }

        // action, context, expected refusal text
        private static readonly object[][] Reasons =
        {
            new object[] { MenuAction.EquipGear, MenuContext.Fight, "Locked during a fight" },
            new object[] { MenuAction.SpendAttribute, MenuContext.Fight, "Locked during a fight" },
            new object[] { MenuAction.RefundAttribute, MenuContext.Fight, "Locked during a fight" },
            new object[] { MenuAction.PlaceBook, MenuContext.Fight, "Locked during a fight" },
            new object[] { MenuAction.RefundAttribute, MenuContext.Descent, "Refunds at camp" },
            new object[] { MenuAction.SwapParty, MenuContext.Descent, "Party changes happen at camp" },
            new object[] { MenuAction.PlaceBook, MenuContext.Camp, "Spell books are placed during a descent" },
            new object[] { MenuAction.ReplaceBook, MenuContext.Camp, "Spell books are placed during a descent" },
            new object[] { MenuAction.RemoveBook, MenuContext.Camp, "Spell books are placed during a descent" },
        };

        [TestCaseSource(nameof(Reasons))]
        public void ARefusalCarriesItsReason(object[] row)
        {
            var action = (MenuAction)row[0];
            var context = (MenuContext)row[1];

            Assert.IsFalse(MenuPermissions.Allows(action, context), "fixture: this cell must be a refusal");
            Assert.AreEqual(row[2], MenuPermissions.Reason(action, context).Template);
        }

        [Test]
        public void AnAllowedActionHasNoReason()
        {
            Assert.IsNull(MenuPermissions.Reason(MenuAction.ChangeOption, MenuContext.Fight).Key);
            Assert.IsNull(MenuPermissions.Reason(MenuAction.EquipGear, MenuContext.Camp).Key);
        }

        [Test]
        public void ThePartyModeFollowsTheTable()
        {
            Assert.AreEqual(PartyMode.Camp, PartyModes.For(MenuContext.Camp));
            Assert.AreEqual(PartyMode.Run, PartyModes.For(MenuContext.Descent));
            Assert.AreEqual(PartyMode.ViewOnly, PartyModes.For(MenuContext.Fight));
        }
    }
}
