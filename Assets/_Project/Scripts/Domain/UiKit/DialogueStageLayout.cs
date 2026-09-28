using System;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.UiKit
{
    // Where the dialogue stage's pieces sit for one line, as numbers
    // (docs/PLAN_DIALOGUE_STAGE.md contracts 7-9). EventScreen builds the
    // tree at the left-speaker layout below; EventController moves the same
    // nodes with these answers when the speaker is on the right, when the
    // line is narration, when the speaker's bust is missing, and when a
    // backdrop has to cover the canvas.
    //
    // Engine-free, so the geometry is pinned by EditMode literals rather than
    // by a capture. Every pin is bottom-edge anchored on the stage (y measured
    // up from the screen bottom), because the box, the bust and the choices
    // all hang off the bottom of the screen at every aspect; only x decides
    // which edge they hang from.
    public readonly struct StagePin
    {
        // 0 = the stage's left edge, 1 = its right, 0.5 = its centre.
        public readonly float AnchorX;

        // Which point of the node the offset positions, 0 = its left edge.
        public readonly float PivotX;

        // From the anchor to the pivot, in reference pixels.
        public readonly float OffsetX;

        public StagePin(float anchorX, float pivotX, float offsetX)
        {
            AnchorX = anchorX;
            PivotX = pivotX;
            OffsetX = offsetX;
        }

        // Where a node this wide lands, measured from the stage's left edge,
        // on a stage this wide -- what a RectTransform does with the same
        // three numbers.
        public float LeftEdge(float stageWidth, float nodeWidth) =>
            AnchorX * stageWidth + OffsetX - PivotX * nodeWidth;

        public override string ToString() => $"anchor {AnchorX} pivot {PivotX} offset {OffsetX}";
    }

    // How one line frames the stage. Derived from the line, never authored.
    // A speaker whose bust failed every fallback (contract 11) is framed
    // centred, as narration is, because there is no face for the box to
    // stand beside -- but keeps the plate, because
    // the speaker still has a name (contract 15).
    public enum StageFraming
    {
        LeftSpeaker,
        RightSpeaker,
        CentredSpeaker,
        Narration,
    }

    public static class DialogueStageLayout
    {
        // ---- sizes at the 1920x1080 reference (contract 8) ------------------

        public const float BustHeight = 840f;

        // The final art is 2560x512 and 960x192, painted at 2x and drawn at
        // exactly half -- never stretched.
        public const float BoxWidth = 1280f;
        public const float BoxHeight = 256f;
        public const float BoxBottom = 32f;
        public const float PlateWidth = 480f;
        public const float PlateHeight = 96f;

        // How far the box's near edge sits from the speaker's screen edge.
        // 1920 - 32 - 1280: at 16:9 and 4:3 (both 1920 wide under Expand) the
        // box's far edge keeps the same 32px margin as its bottom, and the
        // ~840-wide bust runs about 230px behind the box's near end, which is
        // the overlap the Hades reference shows. Measured from the SPEAKER'S
        // edge rather than pinned to the far one so that at 21:9 the box
        // stays with the bust instead of drifting 600px away from it.
        public const float BoxInset = 608f;

        // The plate sits in from the box's near end and straddles its top
        // edge: centre PlateRise above it, so a third of the plate overlaps
        // the box's frame and the rest stands clear.
        public const float PlateInset = 40f;
        public const float PlateRise = 16f;

        public const float BoxTop = BoxBottom + BoxHeight;
        public const float PlateCentreY = BoxTop + PlateRise;

        // The choice rows stand just above the box, a small fixed gap, not
        // a climb over the plate. They clear the plate
        // SIDEWAYS -- rows at the box's far end, plate at its near end -- so
        // nothing has to stand above the plate's top.
        public const float ChoicesGap = 20f;
        public const float ChoicesBottom = BoxTop + ChoicesGap;

        // The least distance from a screen edge to the focus marker's box.
        public const float MarkerEdgeMargin = 24f;

        // How far left of a row the marker's box can reach: a choice row is
        // far wider than tall, so FocusMarkerPlacement puts the marker Gap +
        // Size left of the row's edge, and its bob carries it BobAmplitude
        // further out. A row whose left edge is at least this far in keeps
        // the whole marker MarkerEdgeMargin clear of the screen's left edge.
        public const float ChoicesMinLeft = MarkerEdgeMargin + FocusMarkerPlacement.Gap
            + FocusMarkerPlacement.Size + FocusMarkerPlacement.BobAmplitude;

        // ---- cover-crop (contract 7) --------------------------------------

        // The size to draw a sprite at so it FILLS the canvas at the canvas's
        // aspect while keeping its own: scale by the larger of the two axis
        // ratios, so one axis fits exactly and the other overhangs (centred,
        // and cropped by the stage's RectMask2D). Never a stretch. A degenerate
        // sprite (no art, zero size) answers the canvas itself, which is what
        // the caller draws its solid ground at.
        public static UiVec CoverSize(UiVec sprite, UiVec canvas)
        {
            if (sprite.X <= 0f || sprite.Y <= 0f) return canvas;

            float scale = Math.Max(canvas.X / sprite.X, canvas.Y / sprite.Y);
            return new UiVec(sprite.X * scale, sprite.Y * scale);
        }

        // ---- the bust (contracts 8, 9) --------------------------------------

        // BustHeight tall at the sprite's own aspect. The rect is sized to the
        // art rather than left square with PreserveAspect, because
        // PreserveAspect centres a narrower sprite inside its box and the
        // bust would float away from its screen edge (the owl's canvas is
        // 1122x1402, the sheep's 1408x1402).
        public static UiVec BustSize(UiVec sprite)
        {
            if (sprite.X <= 0f || sprite.Y <= 0f) return new UiVec(BustHeight, BustHeight);
            return new UiVec(BustHeight * sprite.X / sprite.Y, BustHeight);
        }

        // Against its own screen edge. The pivot is the bust's horizontal
        // CENTRE so the mirror (a negative x scale, which flips about the
        // pivot) turns it in place rather than swinging it off the screen.
        public static StagePin BustPin(DialogueSide side, float bustWidth)
        {
            float half = bustWidth * 0.5f;
            return side == DialogueSide.Left
                ? new StagePin(0f, 0.5f, half)
                : new StagePin(1f, 0.5f, -half);
        }

        // Every bust is painted facing the viewer's right (contract 9): shown
        // as painted on the left, mirrored on the right, so a speaker always
        // looks inward. The same rule, and the same helper, the fight stage
        // mirrors combatants with.
        public static float BustMirrorX(DialogueSide side) =>
            StageFacing.MirrorScaleX(SpriteFacing.Right, side == DialogueSide.Left ? StageSide.Left : StageSide.Right);

        // ---- box, plate, choices ---------------------------------------------

        public static StageFraming FramingFor(bool narration, bool hasBust, DialogueSide side)
        {
            if (narration) return StageFraming.Narration;
            if (!hasBust) return StageFraming.CentredSpeaker;
            return side == DialogueSide.Left ? StageFraming.LeftSpeaker : StageFraming.RightSpeaker;
        }

        // Centred with no bust beside it (contract 8, and the missing-bust
        // call). Otherwise the box starts BoxInset in from the speaker's
        // edge, which puts it on the opposite side of the screen from the
        // bust.
        public static StagePin BoxPin(StageFraming framing)
        {
            switch (framing)
            {
                case StageFraming.LeftSpeaker: return new StagePin(0f, 0f, BoxInset);
                case StageFraming.RightSpeaker: return new StagePin(1f, 1f, -BoxInset);
                default: return new StagePin(0.5f, 0.5f, 0f);
            }
        }

        // On the box's top edge, at its SPEAKER'S end -- the end nearest the
        // bust, so the name reads as belonging to the face beside it. With no
        // bust the box is centred and the plate takes its left end, the end
        // a line reads from. Narration shows no plate; it gets the centred
        // answer so no caller has to special-case the question.
        public static StagePin PlatePin(StageFraming framing)
        {
            const float fromEdge = BoxInset + PlateInset;
            switch (framing)
            {
                case StageFraming.LeftSpeaker: return new StagePin(0f, 0f, fromEdge);
                case StageFraming.RightSpeaker: return new StagePin(1f, 1f, -fromEdge);
                default: return new StagePin(0.5f, 0f, -BoxWidth * 0.5f + PlateInset);
            }
        }

        // Above the box, flush with its FAR end -- the end the plate is not
        // on -- so the rows never land on the name. Centred over a centred
        // box for narration, which has no plate to avoid.
        //
        // Then pushed right, if it has to be, until the focus marker on the
        // rows' left keeps MarkerEdgeMargin from the screen's left edge. That
        // bites for a right-hand speaker on a 1920-wide stage (16:9, 16:10,
        // 4:3), where the box's far end is 32px from the edge; at 21:9 the
        // rows already stand far enough in. One rule for every framing rather
        // than a case per side: the marker is always on the rows' left, so
        // the left edge is the only one it can run off.
        public static StagePin ChoicesPin(StageFraming framing, float stageWidth, float choicesWidth)
        {
            const float farEdge = BoxInset + BoxWidth;
            StagePin pin;
            switch (framing)
            {
                case StageFraming.LeftSpeaker: pin = new StagePin(0f, 1f, farEdge); break;
                case StageFraming.RightSpeaker: pin = new StagePin(1f, 0f, -farEdge); break;
                case StageFraming.CentredSpeaker: pin = new StagePin(0.5f, 1f, BoxWidth * 0.5f); break;
                default: pin = new StagePin(0.5f, 0.5f, 0f); break;
            }

            return pin.LeftEdge(stageWidth, choicesWidth) < ChoicesMinLeft
                ? new StagePin(0f, 0f, ChoicesMinLeft)
                : pin;
        }

        // The rows' panel is built four rows tall and fills from its top, so
        // pinning its BOTTOM at ChoicesBottom leaves every hidden row as a gap
        // between the shown ones and the box (a concluded Leave stood three
        // rows up). Pinned by where its shown rows END instead: the panel's
        // bottom drops by the hidden rows' height, so the last shown row sits
        // ChoicesGap above the box and more rows grow upward. The hidden part
        // is a bare panel over the box, which draws nothing and takes no click.
        public static float ChoicesPanelBottom(float panelHeight, float shownHeight) =>
            ChoicesBottom - (panelHeight - shownHeight);
    }
}
