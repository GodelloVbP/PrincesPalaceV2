using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Where the menu reopens (plan C7). MenuMemory is static, so every test
    // clears it on both sides: a leftover would hand the next test a menu that
    // reopens somewhere.
    public class MenuMemoryTests
    {
        [SetUp]
        [TearDown]
        public void Clear() => MenuMemory.Clear();

        // Plan C7: the squad at camp and on the map; in a fight the run, where
        // the menu is opened to look a relic up.
        [TestCase(MenuContext.Camp, SystemMenuTab.Character)]
        [TestCase(MenuContext.Descent, SystemMenuTab.Character)]
        [TestCase(MenuContext.Fight, SystemMenuTab.Run)]
        public void WithNothingRememberedEachContextOpensOnItsDefault(MenuContext context, SystemMenuTab expected)
        {
            Assert.AreEqual(expected, MenuMemory.TabFor(context));
        }

        [Test]
        public void AGameTabIsRememberedPerContext()
        {
            MenuMemory.Remember(MenuContext.Descent, SystemMenuTab.Run);
            MenuMemory.Remember(MenuContext.Camp, SystemMenuTab.Party);

            Assert.AreEqual(SystemMenuTab.Run, MenuMemory.TabFor(MenuContext.Descent));
            Assert.AreEqual(SystemMenuTab.Party, MenuMemory.TabFor(MenuContext.Camp));
            Assert.AreEqual(SystemMenuTab.Run, MenuMemory.TabFor(MenuContext.Fight),
                "a context nobody used keeps the default");
        }

        [Test]
        public void TheLastGameTabWins()
        {
            MenuMemory.Remember(MenuContext.Camp, SystemMenuTab.Party);
            MenuMemory.Remember(MenuContext.Camp, SystemMenuTab.Character);

            Assert.AreEqual(SystemMenuTab.Character, MenuMemory.TabFor(MenuContext.Camp));
        }

        [TestCase(SystemMenuTab.Options)]
        [TestCase(SystemMenuTab.Exit)]
        public void ASystemTabIsNeverRemembered(SystemMenuTab tab)
        {
            MenuMemory.Remember(MenuContext.Camp, SystemMenuTab.Party);
            MenuMemory.Remember(MenuContext.Camp, tab);

            Assert.AreEqual(SystemMenuTab.Party, MenuMemory.TabFor(MenuContext.Camp),
                "a visit to a system tab is an errand, not a place to come back to");
        }

        [Test]
        public void ClearForgetsEveryContext()
        {
            MenuMemory.Remember(MenuContext.Camp, SystemMenuTab.Party);
            MenuMemory.Remember(MenuContext.Descent, SystemMenuTab.Run);

            MenuMemory.Clear();

            Assert.AreEqual(SystemMenuTab.Character, MenuMemory.TabFor(MenuContext.Camp));
            Assert.AreEqual(SystemMenuTab.Character, MenuMemory.TabFor(MenuContext.Descent));
        }
    }
}
