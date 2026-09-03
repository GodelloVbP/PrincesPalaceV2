using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Small helpers shared by several *ScreenTests files rather than
    // reimplemented in each. Kept file-local to Tests/EditMode: nothing
    // outside the test assembly needs them.
    internal static class UiTreeTestHelpers
    {
        // Buttons the caption-collision check (DebugMenuScreenTests,
        // DefeatScreenTests, ReckoningScreenTests) scans for a same-named
        // child: n.Theme == null narrows past a THEMED button's own
        // "<Name>Label" child, which is Ui.ApplyTheme's real, intended
        // output (see UiNode.Themed), not the emitter-name collision the
        // check exists to catch.
        internal static IEnumerable<UiNode> UnthemedButtons(UiNode root) =>
            Walk(root).Where(n => n.Kind == UiNodeKind.Button && n.Theme == null);

        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child))
                {
                    yield return found;
                }
            }
        }
    }
}
