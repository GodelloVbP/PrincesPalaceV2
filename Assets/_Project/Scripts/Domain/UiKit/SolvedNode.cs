using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // The result of solving one UiNode: where it actually landed.
    //
    // Separate from UiNode on purpose. A declared node says what it wants; a
    // solved node says what it got, in absolute canvas coordinates, at one
    // specific canvas size. The audit works exclusively on solved nodes, which
    // is why it can answer "do these two overlap" without knowing anything
    // about flow, anchors or the tree that produced them.
    public sealed class SolvedNode
    {
        public string Name;

        // Slash-joined ancestry. Audit errors name this rather than the bare
        // node name, because "Button0 overlaps Button1" is useless on a screen
        // with three such pairs.
        public string Path;

        public UiNodeKind Kind;
        public UiRect Rect;
        public float Rotation;
        public UiVec Scale = UiVec.One;

        // The declaration this came from, so the audit can read exemptions and
        // the emitter can read text/sprite/colour without a second lookup.
        public UiNode Source;

        public List<SolvedNode> Children = new List<SolvedNode>();

        // What the node actually covers on screen once scale and rotation are
        // applied. Overlap tests use THIS, never Rect -- the talent tree's
        // rotated edge stripes have a footprint several times their unrotated
        // box, and testing the unrotated one would silently miss real
        // collisions.
        public UiRect Footprint => Rect.Scaled(Scale).BoundingBoxAfterRotation(Rotation);

        public IEnumerable<SolvedNode> Descendants()
        {
            foreach (var child in Children)
            {
                yield return child;
                foreach (var deeper in child.Descendants())
                {
                    yield return deeper;
                }
            }
        }

        public override string ToString() => $"{Path} {Rect}";
    }
}
