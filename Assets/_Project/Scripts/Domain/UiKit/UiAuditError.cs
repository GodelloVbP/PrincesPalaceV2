namespace PrincesPalace.Domain.UiKit
{
    public enum UiAuditCheck
    {
        SiblingOverlap,      // A1
        ChildContainment,    // A2
        FlowCapacity,        // A3
        DuplicateName,       // A4
        ZeroSizeGraphic,     // A6
        InertOverlapAllowance, // A7
    }

    // One audit failure, with everything needed to act on it without opening
    // the scene: which check, which nodes, at which canvas frame, by how many
    // pixels, and what to do about it.
    //
    // The remedies are not politeness. v1's layout guards were bypassed or
    // locally re-implemented (three separate one-off versions) partly because
    // a bare "this does not fit" reads as an obstacle; a message that names two
    // or three concrete fixes reads as help and gets followed.
    public sealed class UiAuditError
    {
        public UiAuditCheck Check;
        public string Path;
        public string OtherPath;
        public UiVec Frame;
        public string Message;

        public override string ToString() =>
            $"[{Check}] at {UiFrames.Describe(Frame)}: {Message}";
    }
}
