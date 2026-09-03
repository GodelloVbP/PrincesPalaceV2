using System;
using System.Collections.Generic;
using System.Linq;

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

    // The plate art a button wears. One PNG per theme
    // (Art/UI/Buttons/Processed/button_plate_<theme>.png), each already
    // carrying its own surface, texture and coloured border baked in -- there
    // is no separate border layer to keep in step with a colour swatch here.
    public enum ButtonTheme
    {
        Gold,
        Crimson,
        Violet,
        Blue,
        Green,
        Silver,
    }

    // Shared by ContainerArt and ButtonPlateArt, whose Processed/ filenames
    // both key on the theme's lowercase name.
    internal static class ButtonThemeExtensions
    {
        internal static string ThemeKey(this ButtonTheme theme) => theme.ToString().ToLowerInvariant();
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

        // Where the text sits in the rect. Centre is the default and stays the
        // overwhelming case; see UiTextAlign for why the other four exist.
        public UiTextAlign TextAlign = UiTextAlign.Centre;

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

        // Sprite only. Fits the sprite inside its declared box on its longer
        // axis instead of stretching it to fill both -- required for the
        // Container/FlagBanner kit (Ui.Container/Ui.FlagBanner), whose art is
        // refused at any size that does not already match its own measured
        // aspect, so preserving it changes nothing visible but guards against
        // a future caller loosening that check and stretching the border.
        public bool PreserveAspect;

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

        // Which plate this button wears. Null means the unthemed path: the
        // shared button_cropped frame or a bespoke SpriteKey, whichever
        // EmitButton already resolved before this existed. Set only through
        // Themed(), never assigned directly -- see that method for why setting
        // it is inseparable from building the Visuals/Label children a themed
        // button needs.
        public ButtonTheme? Theme;

        // True when this button wears a theme's Visuals(Glow, Plate) but kept
        // its OWN declared caption children instead of getting a generated
        // <name>Label. Set only through ThemedPlate(), never assigned
        // directly, and never true at the same time node.Children already
        // holds a "<Name>Label" the way Themed()'s output does.
        //
        // Exists for a composite button that already declares its caption as
        // real children (the fight's verb rows: a hotkey badge, a
        // left-aligned text child, a nesting caret) -- Themed()'s Label
        // would either draw a second, blank, centred label under the real
        // one, or force the row to restructure its own DSL declaration just
        // to reach the shared caption shape. This gives such a row the plate
        // and the focus/press/disabled state machine without touching what
        // it already draws for its own words.
        public bool CaptionPreserving;

        // Forces a specific plate shape instead of the aspect-nearest pick
        // Ui.PlateShapeFor would otherwise make. Null is the normal path --
        // see UiNode.Plate() for when a screen actually needs this.
        public ButtonPlateShape? PlateShapeOverride;

        // Which typography role (Typography.cs, engine-free) this label's
        // presentation follows. Null is the untouched path: UiEmitter's
        // ApplyTypography leaves SceneBuilder.UiFont at node.FontSize with
        // node.Tracking as written, no case transform, no autosize -- exactly
        // what every label emitted before this field existed. Set through
        // Styled() below, and automatically to ButtonLabel by Ui.ApplyTheme
        // for a themed button's own <name>Label.
        public TypographyRole? Role;

        // Escape hatches. Both REQUIRE a reason, so every exemption is greppable
        // and reviewable -- in v1 everything was an escape hatch and none of them
        // were enumerable.
        public string AllowOverlapReason;
        public string AllowOverflowReason;

        // NestedCanvas only. Overlay (the default every existing caller gets)
        // composites after ALL camera rendering unconditionally -- it can
        // never sit BEHIND a SpriteRenderer, only ever in front of one,
        // regardless of SortingOrder. WorldInterleaved switches to
        // ScreenSpaceCamera instead, which genuinely interleaves with
        // camera-rendered content (SpriteRenderers, Renderer2D) by
        // SortingLayer then SortingOrder like everything else on that
        // camera -- the only way to sandwich a nested canvas BETWEEN two
        // pieces of world-space content rather than always in front of both.
        public int SortingOrder;
        public bool WorldInterleaved;
        public string SortingLayerName;

        public List<UiNode> Children = new List<UiNode>();

        public UiNode Rotated(float degrees) { Rotation = degrees; return this; }
        public UiNode WithScale(UiVec scale) { Scale = scale; return this; }
        public UiNode Inactive() { StartInactive = true; return this; }

        // Which set of alternatives this node belongs to, if any. Siblings
        // sharing one are pages of the same book: they occupy the same box on
        // purpose and at most one is ever on screen, so overlapping EACH OTHER
        // is what they are for.
        //
        // A token rather than a name, so two groups cannot collide by both
        // being called "panes" and nobody has to keep a string in step. Set
        // through Ui.Exclusive, which is the only thing that should write it.
        //
        // WHY THIS EXISTS AT ALL. Six screens said this in prose --
        // AllowOverlap("the three tab pages share one coordinate frame and
        // exactly one is ever active") -- and AllowOverlap is a blanket: A1
        // skips a PAIR if EITHER side carries a reason, so a page exempted to
        // sit on its sibling pages was also exempt from colliding with the tab
        // bar, the footer, and anything added later. Saying "these three are
        // alternatives" exempts exactly the pairs it should and leaves every
        // other overlap checked.
        public object ExclusiveGroup;

        // Which WIDGET this node is part of, if any. Siblings sharing one are
        // layers of a single thing -- a fill inside its track, a label over its
        // own plate, the ring and portrait and initial of one badge. They are
        // all on screen at once, which is the difference from ExclusiveGroup.
        //
        // Set through Ui.Layered. Same token trick, and the same reason for it:
        // the alternative is each layer carrying an AllowOverlap, and that is a
        // blanket -- A1 skips a pair when EITHER side has a reason, so a label
        // exempted to sit on its own plate stops being checked against every
        // other sibling on the screen too.
        public object LayerGroup;
        public UiNode AsDecor() { Decor = true; return this; }
        public UiNode Clipping() { Masks = true; return this; }
        public UiNode NoChrome() { Chromeless = true; return this; }
        public UiNode Hovers(float scale = 1.02f) { HoverScale = scale; return this; }
        public UiNode Opening() { OpensOnShow = true; return this; }
        public UiNode Quiet() { SilentClick = true; return this; }

        // Wears a theme's plate instead of the shared button frame.
        //
        // Builds the Visuals(Glow, Plate) + Label children a themed button
        // needs RIGHT HERE, at declaration time -- see Ui.ApplyTheme for why
        // that has to be Domain-side rather than something UiEmitter invents
        // later: UiAudit and UiKitAuditTests only ever see the UiNode tree, so
        // a hierarchy the emitter built for itself would be untestable and
        // unaudited. Contradicts Chromeless (no plate at all) and NoChrome's
        // own reasoning, so calling this on one throws rather than silently
        // picking a winner.
        public UiNode Themed(ButtonTheme theme)
        {
            Ui.ApplyTheme(this, theme);
            return this;
        }

        // Wears a theme's PLATE ONLY -- Visuals(Glow, Plate), no generated
        // <name>Label -- for a button whose caption is already declared as
        // its own children. See CaptionPreserving for why this has to be a
        // separate mode rather than a flag on Themed(): the label-generating
        // path stays byte-for-byte unchanged for every existing caller.
        //
        // Call LayerCaptionWithVisuals() once the caption children have been
        // added, or A1 reports the plate colliding with them -- Visuals
        // stretches over the whole button and the caption sits inside it on
        // purpose, the same relationship Themed()'s own Visuals/Label pair
        // states with Layered().
        public UiNode ThemedPlate(ButtonTheme theme)
        {
            Ui.ApplyThemePlateOnly(this, theme);
            return this;
        }

        // Marks this button's Visuals (built by ThemedPlate) and its own
        // caption children as ONE widget, the way Themed()'s Visuals/Label
        // pair already is -- without this, A1 sees Visuals' full-button
        // footprint sitting under a hotkey/text/caret it was never told is
        // meant to be there.
        // Forces this themed button to wear a specific plate shape instead
        // of whichever one Ui.PlateShapeFor(width, height) would pick for
        // its declared rect. Must be called BEFORE Themed()/ThemedPlate() --
        // both read PlateShapeOverride while building Visuals, so calling
        // this after has nothing left to affect.
        public UiNode Plate(ButtonPlateShape shape)
        {
            PlateShapeOverride = shape;
            return this;
        }

        public UiNode LayerCaptionWithVisuals(params UiNode[] captionChildren)
        {
            var visuals = Children.FirstOrDefault(c => c.Name == "Visuals");
            if (visuals == null)
            {
                throw new InvalidOperationException(
                    $"'{Name}' has no Visuals child to layer a caption against - call ThemedPlate() before " +
                    "LayerCaptionWithVisuals().");
            }

            var members = new List<UiNode> { visuals };
            members.AddRange(captionChildren.Where(c => c != null));
            Ui.Layered(members);
            return this;
        }

        public UiNode Padded(UiPad pad) { Pad = pad; return this; }

        // Named Styled, not Role -- a field and a method cannot share a name
        // in C#, and the field is the one every reader looks for first.
        public UiNode Styled(TypographyRole role) { Role = role; return this; }

        // Containers default to FromChildren, which is right almost always. A
        // Fixed size is what a container needs before any child can Fill it --
        // Fill inside FromChildren is the circular case the solver refuses.
        public UiNode Sized(UiSize size) { Size = size; return this; }
        public UiNode Aligned(UiAlign align) { Align = align; return this; }
        public UiNode TextAligned(UiTextAlign align) { TextAlign = align; return this; }
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

        // Emits no Graphic component at all: a bare coordinate rect that draws
        // nothing and cannot intercept a click. UiEmitter's rule exactly -- a
        // Panel gets an Image only when it has been given a colour.
        //
        // Worth having as a derived fact rather than a flag somebody sets,
        // because screens kept stating it in prose instead: "an invisible pool
        // container spans the panel by construction and has no graphic to
        // intercept anything", "a full-bleed viewport spans the headings it
        // shares the panel with; it draws nothing and has no graphic to
        // intercept a click". Both are true, both are readable off the tree,
        // and both were spent on an AllowOverlap that then stopped checking
        // that node against everything else as well.
        public bool EmitsNoGraphic =>
            !IsVisibleGraphic && string.IsNullOrEmpty(ColorHex) && string.IsNullOrEmpty(SpriteKey);

        public override string ToString() => $"{Kind}:{Name}";
    }
}
