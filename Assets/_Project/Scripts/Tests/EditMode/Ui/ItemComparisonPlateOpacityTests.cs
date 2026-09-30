using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The comparison plate is a reading surface that stands over art by
    // design -- the shop's detail panel beside its offers. At 95% alpha the
    // bright gear art still showed faintly under its text. Checked on the
    // REAL screen tree rather than on the builder alone, so a screen that
    // re-tints its own copy of the fill cannot slip past.
    public class ItemComparisonPlateOpacityTests
    {
        [TestCase("ShopDetailPanelFill")]
        public void ThePlateUnderTheTextIsFullyOpaque(string fillName)
        {
            var root = ShopScreen.Build().Root;

            var fill = Find(root, fillName);

            Assert.IsNotNull(fill, fillName + " is missing from its screen tree.");
            Assert.AreEqual("#1D1226FF", fill.ColorHex,
                fillName + " must be opaque: any alpha below FF lets the art behind it show through the text.");
        }

        private static UiNode Find(UiNode node, string name)
        {
            var stack = new Stack<UiNode>();
            stack.Push(node);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current.Name == name) return current;
                foreach (var child in current.Children) stack.Push(child);
            }

            return null;
        }
    }
}
