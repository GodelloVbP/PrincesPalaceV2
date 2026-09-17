using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // UiAudit.CheckNavigable, the structural half of plan section 9's split --
    // proves a declared UiNavDeclaration resolves against the tree it was
    // declared for. Never proves stick+Submit reaches anything (section 9b).
    public class UiAuditNavigableTests
    {
        private static UiNode Btn(string name) => Ui.Button(name, UiString.FromContent(name), new UiVec(100f, 40f), 18);

        private static UiNode Screen(params UiNode[] children) =>
            Ui.Panel("TestScreen", UiSize.Fixed(1920f, 1080f), children);

        [Test]
        public void NullDeclaration_IsNotChecked_NotFailed()
        {
            var tree = Screen(Btn("A"));

            var errors = UiAudit.CheckNavigable(tree, null);

            Assert.IsEmpty(errors, "phase 2's opt-in: an undeclared screen is not checked, not checked-and-clean");
        }

        [Test]
        public void FullyValidDeclaration_IsClean()
        {
            var a = Btn("A"); var b = Btn("B");
            var tree = Screen(a, b);
            var group = new UiNavGroup("rows", UiNavGroupKind.List, new[] { a, b });
            var nav = new UiNavDeclaration(a, new[] { group },
                requiredActions: new[] { new UiRequiredAction(b) });

            var errors = UiAudit.CheckNavigable(tree, nav);

            Assert.IsEmpty(errors);
        }

        [Test]
        public void EntryNotInTree_IsRejected()
        {
            var a = Btn("A");
            var stray = Btn("Stray"); // never added to the tree below
            var tree = Screen(a);
            var nav = new UiNavDeclaration(stray);

            var errors = UiAudit.CheckNavigable(tree, nav);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(UiAuditCheck.NavEntryUnresolved, errors[0].Check);
            StringAssert.Contains("Stray", errors[0].Path);
        }

        [Test]
        public void GroupMemberNotInTree_IsRejected()
        {
            var a = Btn("A");
            var stray = Btn("Stray");
            var tree = Screen(a);
            var group = new UiNavGroup("rows", UiNavGroupKind.List, new[] { a, stray });
            var nav = new UiNavDeclaration(a, new[] { group });

            var errors = UiAudit.CheckNavigable(tree, nav);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(UiAuditCheck.NavGroupMemberUnresolved, errors[0].Check);
        }

        [Test]
        public void LinkToNodeOutsideTheTree_IsRejected()
        {
            var a = Btn("A"); var b = Btn("B");
            var stray = Btn("Stray");
            var tree = Screen(a, b);
            var group = new UiNavGroup("rows", UiNavGroupKind.List, new[] { a, b });
            var nav = new UiNavDeclaration(a, new[] { group },
                links: new[] { new UiNavLink(b, UiNavDirection.Down, stray) });

            var errors = UiAudit.CheckNavigable(tree, nav);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(UiAuditCheck.NavLinkUnresolved, errors[0].Check);
        }

        [Test]
        public void RequiredActionNodeNotInTree_IsRejected()
        {
            var a = Btn("A");
            var stray = Btn("Stray");
            var tree = Screen(a);
            var nav = new UiNavDeclaration(a, requiredActions: new[] { new UiRequiredAction(stray, "owed > 0") });

            var errors = UiAudit.CheckNavigable(tree, nav);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(UiAuditCheck.NavRequiredActionUnresolved, errors[0].Check);
            StringAssert.Contains("owed > 0", errors[0].Message);
        }
    }
}
