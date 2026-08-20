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

        // Letter-spacing, in hundredths of an em -- 14 is the .14em the design
        // handoffs write. Zero means the font's own spacing.
        //
        // It exists because the DSL had no way to say it and the absence was
        // measurable: the system menu's tab widths were authored off a design
        // prototype drawn at .14em, and the emitter drew them at zero, so every
        // label came out about a quarter narrower than the box built for it and
        // the selected tab's underline overhung its own word by 40px a side.
        // The bar was laid out around a number nothing in the build produced.
        //
        // Deliberately NOT a default. Applying tracking everywhere would move
        // text on every screen in the game to fix one bar; this says it where
        // the design said it.
        public float Tracking;
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

        // Clips descendants to this node's rect (a RectMask2D).
        //
        // The DSL had no way to say this at all, and the absence showed: the
        // Reckoning's open animation drove localScale because a mask was not
        // available, which squashed every child horizontally instead of
        // revealing them, and its phase sweep sent cards out over the
        // battlefield because nothing bounded them.
        //
        // Note what this does NOT do: the audit still measures declared rects,
        // so a mask is not a licence to overflow. Anything that genuinely
        // hangs past its parent still has to say AllowOverflow and why.
        public bool Masks;

        // A button with no plate: no sprite, nothing drawn, still clickable.
        //
        // NOT the same as leaving SpriteKey empty, which means "use the shared
        // button frame" and is the emitter's fallback. There was no way to say
        // "no chrome at all", so the Reckoning's three item offers wore a gold
        // plate each behind their art for as long as the screen existed.
        public bool Chromeless;

        // ---- motion (all three migrated from v1, which lost them in the
        // rebuild along with every other button animation) --------------------

        // A WIDE row or plate: hover-scales by this factor and does NOT get the
        // press "pop".
        //
        // Zero means "use the default press animator". The two are mutually
        // exclusive by construction in the emitter, because both drive
        // localScale and would fight over it every frame. The reason a wide row
        // wants the gentler one is legibility, not taste: the press animator's
        // 1.05/0.95 visibly shifts a long caption sideways, which reads as the
        // text wobbling rather than the row responding.
        public float HoverScale;

        // Slides in and fades up when shown (ColumnOpenAnimator).
        //
        // Only meaningful on a node the runtime drives with SetActive, since
        // the animation is triggered by OnEnable -- "just activated" IS "just
        // opened". Adds a CanvasGroup, which the animator requires.
        public bool OpensOnShow;

        // Suppresses the shared click sound on a button whose own controller
        // plays a flourish on the same click. Without it the two sound at once.
        // Nothing in v2 needs this yet -- v1 needed it for the save slots --
        // but the flag is what makes the "every button has a click sound" test
        // able to state an exception rather than be weakened.
        public bool SilentClick;

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
        public UiNode Clipping() { Masks = true; return this; }
        public UiNode NoChrome() { Chromeless = true; return this; }
        public UiNode Hovers(float scale = 1.02f) { HoverScale = scale; return this; }
        public UiNode Opening() { OpensOnShow = true; return this; }
        public UiNode Quiet() { SilentClick = true; return this; }
        public UiNode Padded(UiPad pad) { Pad = pad; return this; }

        // Containers default to FromChildren, which is right almost always. A
        // Fixed size is what a container needs before any child can Fill it --
        // Fill inside FromChildren is the circular case the solver refuses.
        public UiNode Sized(UiSize size) { Size = size; return this; }
        public UiNode Aligned(UiAlign align) { Align = align; return this; }
        public UiNode Coloured(string hex) { ColorHex = hex; return this; }

        // In hundredths of an em, matching CSS: Tracked(14) is .14em.
        public UiNode Tracked(float emHundredths) { Tracking = emHundredths; return this; }

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

        // Long enough to be a sentence about this node rather than a label.
        // 20 is not a measurement of anything; it is the number eight separate
        // screen tests had each settled on independently, and the shortest
        // reason actually in the tree is 39 characters, so it has room.
        public const int MinimumReasonLength = 20;

        private static string Require(string reason, string what)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    $"{what} must state WHY. An unexplained exemption is indistinguishable from a bug someone silenced.");
            }

            // CHECKED HERE, not per screen. Eight screen test classes used to
            // carry their own copy of this -- walk my tree, assert every reason
            // is longer than 20 -- which meant a ninth screen was covered on the
            // day somebody remembered to paste the block, and until then its
            // exemptions could say "layout" and pass. Enforcing it where the
            // reason is SET covers every screen that exists and every screen
            // that will, and it fails at the declaration rather than in a test
            // named after some other subject.
            if (reason.Trim().Length <= MinimumReasonLength)
            {
                throw new ArgumentException(
                    $"{what}(\"{reason}\") is too short to be a reason. It needs to say what this node sits on " +
                    $"and why that is intended, in a sentence the next reader can check against the screen -- " +
                    $"a label like \"overlap\" or \"by design\" only records that somebody wanted the audit quiet.");
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
