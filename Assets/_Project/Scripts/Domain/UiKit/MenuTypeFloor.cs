namespace PrincesPalace.Domain.UiKit
{
    // The menu's type floor as a rule (plan C14, spec section 1).
    //
    // Nothing a player reads renders below 18 px of body height at 1080p, and
    // Chakra Petch measures about 0.93 of its font size, so the working floor
    // is TMP 20 for mixed case. All caps has no descenders and reads larger
    // than its pixel height, but its caps height is what is measured (0.72), so
    // its floor is TMP 24.
    //
    // A pure function over one node, so a test can walk any solved tree with it
    // and another can hand it a deliberately undersized node. It looks at
    // EVERY label and button whatever its AsDecor: AsDecor is a raycast and
    // audit flag, and readable headings and values carry it today.
    //
    // WHAT "SIZE" MEANS: the size the label is authored at -- its literal,
    // resolved through the role (TypographySpec.ResolveSizeRange), taken at the
    // top of the range. A role's band can autosize below that when the text
    // does not fit its box; UiTextFitAudit is what keeps authored text from
    // ever needing to, so this rule and that audit together mean the size on
    // the node is the size on screen.
    public static class MenuTypeFloor
    {
        public const int MixedCase = 20;
        public const int AllCaps = 24;

        // Null when the node is fine, else what is wrong with it.
        public static string Problem(UiNode node)
        {
            if (node == null) return null;
            if (node.Kind != UiNodeKind.Label && node.Kind != UiNodeKind.Button) return null;
            if (node.AllowSmallTextReason != null) return null;

            // A themed button's caption is its own "<Name>Label" child, which
            // is a Label node in the tree and is checked as one.
            if (node.Kind == UiNodeKind.Button && node.Theme.HasValue) return null;

            if (!node.Role.HasValue)
            {
                return $"'{node.Name}' has no Typography role (.Styled): its size is a bare literal " +
                       $"({node.FontSize}) that nothing holds to the floor.";
            }

            var spec = Typography.Specs[node.Role.Value];
            float? literal = node.FontSize > 0 ? node.FontSize : (float?)null;
            float size = spec.ResolveSizeRange(literal).Max;

            bool caps = spec.Uppercase || IsAllCaps(node.Text);
            float floor = caps ? AllCaps : MixedCase;
            if (size >= floor) return null;

            return $"'{node.Name}' is {size:0.#} in {node.Role.Value}" +
                   $"{(caps ? " (all caps)" : "")}, under the floor of {floor:0}.";
        }

        // Authored text with letters and none of them lower case. A runtime
        // string (UiString.Runtime) is empty here and so is never caps by this
        // test; its role decides.
        private static bool IsAllCaps(UiString text)
        {
            if (!text.IsValid || text.IsContent) return false;

            bool letter = false;
            foreach (char c in text.Template)
            {
                if (!char.IsLetter(c)) continue;
                if (char.IsLower(c)) return false;
                letter = true;
            }

            return letter;
        }
    }
}
