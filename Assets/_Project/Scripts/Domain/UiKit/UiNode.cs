using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    public enum UiNodeKind
    {
        Panel,
        Column,
        Row,
        Grid,
        Label,
        Button,
        Sprite,
        Solid,
        Space,
        NestedCanvas,
        Modal,
        Pool,
    }

    // One node of a screen's declared tree. Plain data plus fluent modifiers --
    // it knows nothing about GameObjects, RectTransforms or Unity at all, which
    // is what lets an entire screen be built and audited from an EditMode test
    // in well under a second.
    //
    // Mutable during construction and treated as frozen after: the factories in
    // Ui.cs are the only intended way to make one, and the fluent modifiers
    // return `this` so a node can be decorated inline without a builder type.
    public sealed class UiNode
    {
        public string Name;
        public UiNodeKind Kind;
        public Place Place = Place.Flow;
        public UiSize Size = UiSize.Fixed(0f, 0f);

        // Container properties. Spacing and padding live HERE, on the container,
        // never at a child construction site -- that placement is the whole
        // mechanism by which an index-stepped positioning loop becomes
        // unwritable.
        public UiPad Pad = UiPad.Zero;
        public float Spacing;
        public UiAlign Align = UiAlign.Centre;

        // Grid only.
        public int Columns = 1;
        public UiVec Cell = UiVec.Zero;
        public UiVec Gap = UiVec.Zero;

        public UiString Text;
        public int FontSize = 24;
        public string ColorHex;
        public string SpriteKey;

        public float Rotation;
        public UiVec Scale = UiVec.One;
        public bool StartInactive;

        // Marks a subtree as pure decoration: the emitter clears raycastTarget
        // throughout it. v1 needed a hand-written sweep over 110 ambient sprites
        // to guarantee the same thing, and a single missed one is an unclickable
        // button with no visible cause.
        public bool Decor;

        // Escape hatches. Both REQUIRE a reason, so every exemption is greppable
        // and reviewable -- in v1 everything was an escape hatch and none of them
        // were enumerable.
        public string AllowOverlapReason;
        public string AllowOverflowReason;

        // NestedCanvas only.
        public int SortingOrder;

        public List<UiNode> Children = new List<UiNode>();

        public UiNode Rotated(float degrees) { Rotation = degrees; return this; }
        public UiNode WithScale(UiVec scale) { Scale = scale; return this; }
        public UiNode Inactive() { StartInactive = true; return this; }
        public UiNode AsDecor() { Decor = true; return this; }
        public UiNode Padded(UiPad pad) { Pad = pad; return this; }

        // Containers default to FromChildren, which is right almost always. A
        // Fixed size is what a container needs before any child can Fill it --
        // Fill inside FromChildren is the circular case the solver refuses.
        public UiNode Sized(UiSize size) { Size = size; return this; }
        public UiNode Aligned(UiAlign align) { Align = align; return this; }
        public UiNode Coloured(string hex) { ColorHex = hex; return this; }

        public UiNode AllowOverlap(string reason)
        {
            AllowOverlapReason = Require(reason, nameof(AllowOverlap));
            return this;
        }

        public UiNode AllowOverflow(string reason)
        {
            AllowOverflowReason = Require(reason, nameof(AllowOverflow));
            return this;
        }

        private static string Require(string reason, string what)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    $"{what} must state WHY. An unexplained exemption is indistinguishable from a bug someone silenced.");
            }
            return reason;
        }

        public bool IsContainer =>
            Kind == UiNodeKind.Panel || Kind == UiNodeKind.Column || Kind == UiNodeKind.Row
            || Kind == UiNodeKind.Grid || Kind == UiNodeKind.NestedCanvas || Kind == UiNodeKind.Modal
            || Kind == UiNodeKind.Pool;

        // Lays children out along an axis with Spacing between them.
        public bool IsFlowContainer => Kind == UiNodeKind.Column || Kind == UiNodeKind.Row;

        // Draws something, so a zero size means it renders as nothing (or, with
        // no sprite assigned, as a solid white quad -- a shipped v1 bug class).
        public bool IsVisibleGraphic =>
            Kind == UiNodeKind.Label || Kind == UiNodeKind.Button
            || Kind == UiNodeKind.Sprite || Kind == UiNodeKind.Solid;

        public override string ToString() => $"{Kind}:{Name}";
    }
}
