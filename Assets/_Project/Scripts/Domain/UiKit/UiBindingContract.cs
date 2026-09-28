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

        // A field whose provenance names a node the emit result has no object
        // for. Not "wrong node" and not "not from this screen": the value
        // came through one of the five typed accessors, so it HAS a
        // provenance -- and the node that provenance names cannot be looked
        // up, which means the two halves of the emit result disagree with
        // each other and nothing here can say what the field should have held.
        //
        // Reported rather than passed over: returning quietly here would let
        // a screen in this state be audited to zero problems and read as
        // checked, while whatever produced the mismatch (a node dropped from
        // the tree after a Wire line was recorded against it, an accessor
        // handing back a provenance for a node it never emitted) stayed
        // invisible.
        public static string ProvenanceNodeNotEmitted(
            string controllerType, string fieldName, string expectedNode)
        {
            return $"{controllerType}.{fieldName} was assigned from node '{expectedNode}', but this screen's " +
                   "emit result has no object for that node -- so what the field holds cannot be checked " +
                   "against what it was supposed to hold. The screen tree and the wiring disagree about " +
                   "which nodes exist; check that the NodeRef is still in the tree this screen declares.";
        }

        // The header the auditor prints above the collected lines. Here rather
        // than in the auditor so the phrasing and the rule live together.
        public static string Header(string screenName, int problemCount) =>
            $"[SceneBuilder] FAILED: '{screenName}' has {problemCount} field(s) bound to the wrong node:";
    }
}
