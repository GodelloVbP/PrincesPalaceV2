namespace PrincesPalace.Domain.UiKit
{
    // A handle a screen keeps so its Wire step can find the emitted object.
    //
    // Wraps the UiNode INSTANCE, not its name or path. That is the whole point:
    // v1 wired 330 sites through SetField(controller, "fieldName", value) --
    // reflection over a string, where a typo produced no compile error, no
    // runtime error, and a field that simply stayed null until something far
    // away threw a NullReferenceException naming the symptom instead of the
    // cause. A handle to an object cannot be misspelled, and renaming a node
    // cannot silently break the binding.
    public readonly struct NodeRef
    {
        public readonly UiNode Node;

        public NodeRef(UiNode node)
        {
            Node = node;
        }

        // So a screen can pass the node it just built straight through, without
        // a wrapping call at every site.
        public static implicit operator NodeRef(UiNode node) => new NodeRef(node);

        public bool IsValid => Node != null;

        public override string ToString() => Node?.ToString() ?? "NodeRef(null)";
    }
}
