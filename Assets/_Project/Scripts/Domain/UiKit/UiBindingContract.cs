namespace PrincesPalace.Domain.UiKit
{
    // What a wired field is allowed to be pointing at, and how to say it is
    // not.
    //
    // The companion to UiBindingNames, and here for the same reason: the rule
    // is stated once, where both the build-time auditor (Editor's
    // UiBindingAudit) and a test can read it, rather than each carrying a
    // copy. This half owns the MESSAGE; the auditor owns the comparison,
    // because "is this the same GameObject" needs an engine and this
    // assembly does not have one.
    //
    // WHY A COUNT IS NOT THE CHECK. The obvious version -- assert the binder
    // filled more than zero fields -- passes for a screen where every field
    // was bound to the wrong node, which is the failure worth catching. A
    // count is a measure of effort, not of correctness.
    public static class UiBindingContract
    {
        // Which of ScreenRegistry's two routes filled the field. Kept in the
        // message because the fix differs: an auto-bound field that landed on
        // the wrong node means the name rule matched something unintended,
        // and an explicit one means the Wire line names the wrong NodeRef.
        public enum Source
        {
            // UiAutoBind matched the field's own identifier to a NodeRef on
            // the screen.
            AutoBound,

            // A Wire line in ScreenRegistry assigned it from a NodeRef.
            Explicit,
        }

        // All four facts, in the order a reader needs them: whose field, which
        // field, what it should be pointing at, what it is pointing at.
        public static string WrongNode(
            string controllerType, string fieldName, string expectedNode, string actualNode, Source source)
        {
            string route = source == Source.AutoBound
                ? "auto-bound from the screen member of the same name"
                : "assigned by an explicit Wire line";

            return $"{controllerType}.{fieldName} ({route}) should be bound to node " +
                   $"'{expectedNode}' but holds '{actualNode}'.";
        }

        // A field of a bindable type holding an object that is not one of the
        // nodes this screen declared. Not a wrong node -- no node at all,
        // which means it came from outside the tree the screen declared and
        // nothing downstream can reason about where it lives.
        public static string NotFromThisScreen(string controllerType, string fieldName, string actualNode)
        {
            return $"{controllerType}.{fieldName} holds '{actualNode}', which this screen never emitted as a " +
                   "declared node. Every UI reference on a controller has to come from a node the screen " +
                   "declared, so that what it points at is checkable at all.";
        }

        // The header the auditor prints above the collected lines. Here rather
        // than in the auditor so the phrasing and the rule live together.
        public static string Header(string screenName, int problemCount) =>
            $"[SceneBuilder] FAILED: '{screenName}' has {problemCount} field(s) bound to the wrong node:";
    }
}
