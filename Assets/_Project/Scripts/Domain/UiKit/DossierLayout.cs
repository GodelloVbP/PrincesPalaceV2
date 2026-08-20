using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.UiKit
{
    // The character dossier's geometry, as pure arithmetic.
    //
    // IT FILLS THE PANE. The handover authors this screen at 1360x766 and says
    // to scale the whole panel as one unit rather than reflow it, and for a
    // while it was placed at exactly that size and centred in the system menu's
    // 1600x804 content pane -- which cost 118px of dead margin down each side
    // and left all three columns stopping about 200px short of the floor. It
    // read as a small screen sitting inside a big empty one, which is what it
    // was.
    //
    // Uniform scale cannot fix that, and it is worth saying why rather than
    // leaving it to be rediscovered: the authored panel is 1.775 wide for every
    // 1 tall and the pane is 1.990, so scaling to fill the height leaves 86px a
    // side and scaling to fill the width overflows the height by 97. There is
    // no scale factor that fills this box. So the frame is authored AT the
    // pane's size and the columns take their share of it.
    //
    // What the handover's rule was actually protecting is kept: the mannequin
    // slots and their leader hairlines are still the handover's own numbers,
    // still positioned against each other, and FromStage scales them as one
    // unit -- so the stage grows without a single slot being re-authored, which
    // is the reflow the handover was warning against. Nothing shrinks either,
    // so its legibility floor holds.
    //
    // Coordinates are measured from the DOSSIER's own centre, like every other
    // screen in this project.
    public static class DossierLayout
    {
        // The pane it fills, read from the menu rather than restated, so a
        // panel resize cannot leave the dossier behind at the old size.
        public const float Width = SystemMenuLayout.PanelWidth;                              // 1600
        public const float Height = SystemMenuLayout.PanelHeight - SystemMenuLayout.BarHeight; // 804

        public const float HalfWidth = Width * 0.5f;
        public const float HalfHeight = Height * 0.5f;

        // Three columns, no gap; the dividers are borders. Widened in the
        // handover's own proportions -- 318 and 408 of 1360 are 23.4% and 30%,
        // which is what these are of 1600 -- so the balance between the three
        // is the one that was drawn.
        public const float ColumnAWidth = 374f;
        public const float ColumnCWidth = 480f;
        public const float ColumnBWidth = Width - ColumnAWidth - ColumnCWidth;   // 746

        public const float PadY = 32f;
        public const float ColumnAPadX = 30f;
        public const float ColumnBPadLeft = 40f;
        public const float ColumnCPadX = 40f;

        // Centre x of each column, measured from the panel centre.
        public const float ColumnACentreX = -HalfWidth + ColumnAWidth * 0.5f;
        public const float ColumnBCentreX = -HalfWidth + ColumnAWidth + ColumnBWidth * 0.5f;
        public const float ColumnCCentreX = HalfWidth - ColumnCWidth * 0.5f;

        // The two dividers sit on the column boundaries.
        public const float DividerAtoB = -HalfWidth + ColumnAWidth;
        public const float DividerBtoC = HalfWidth - ColumnCWidth;

        // ---- column A -----------------------------------------------------------

        public const float ContentAWidth = ColumnAWidth - ColumnAPadX * 2f;      // 314

        // Square-ish, because the Image carries preserveAspect: a box taller
        // than the art's own aspect letterboxes rather than filling, so height
        // past about the content width buys empty bars and nothing else. This
        // is why column A cannot simply be filled by growing the portrait.
        public const float PortraitHeight = 318f;

        // Everything in column A stacks from the top, so each y is the one
        // above it minus its own height. Stated as running totals rather than
        // as a flow container because the pack panel has to cover the column
        // exactly and a flow would fight that.
        public const float ColumnATop = HalfHeight - PadY;
        public const float ColumnABottom = -HalfHeight + PadY;

        public const float PortraitCentreY = ColumnATop - PortraitHeight * 0.5f;
        // Each step is the PREVIOUS box's half-height, then this box's, then the
        // gap between them. Written that way rather than as one number because
        // the name's box is 42 tall and the sub-line's 24, and a step that
        // forgets either of those overlaps them -- which is exactly what the
        // sibling-overlap audit refused when this was first widened.
        public const float NameCentreY = PortraitCentreY - PortraitHeight * 0.5f - 24f - 21f;
        public const float SubLineCentreY = NameCentreY - 21f - 12f - 6f;
        public const float XpRowCentreY = SubLineCentreY - 12f - 8f - 20f;

        public const float NavRowHeight = 56f;

        // THE TWO NAV ROWS ARE A FOOTER, measured up from the column's floor
        // rather than down from the XP bar above them.
        //
        // Stacked immediately under the identity block they left 200px of the
        // column empty beneath them, which is most of what made this screen
        // read as too small for its box. Anchored to the bottom they close the
        // column, and the space that is left falls between two groups that mean
        // different things -- who this is, and what you can open -- which is
        // where negative space belongs.
        public const float PackRowCentreY = ColumnABottom + NavRowHeight * 0.5f;
        public const float SkillsRowCentreY = PackRowCentreY + NavRowHeight;

        // ---- the XP bar ---------------------------------------------------------
        //
        // IN THE LAYOUT rather than computed in the screen, because there are
        // two readers: the screen authors the track and the fill at this width,
        // and the controller grows the fill BY width to show a fraction of it.
        // While the screen owned the number the controller could not have it,
        // which is how the fill came to be driven by anchors instead -- and
        // anchors are relative to the PARENT, so a bar meant to cross 226px of
        // track crossed that fraction of the whole column and ran out into the
        // loadout beside it.
        public const float XpLabelWidth = 30f;
        public const float XpLabelGap = 10f;
        public const float XpRemainingWidth = 70f;

        public const float XpTrackWidth =
            ContentAWidth - XpLabelWidth - XpLabelGap - XpRemainingWidth;

        // Left edge of the track, from column A's centre.
        public const float XpTrackLeft =
            -ContentAWidth * 0.5f + XpLabelWidth + XpLabelGap;

        // ---- column B: the loadout stage ----------------------------------------

        // The handover's stage is 560x658 with the mannequin at left 148, top 92
        // inside it, 264x564. Those are top-left offsets in the stage; this
        // converts them once, here, rather than at eight call sites.
        // THE HANDOVER'S OWN STAGE, kept as the coordinate system its sixteen
        // slot and leader positions are written in. Nothing below re-authors
        // them; FromStage scales them.
        public const float AuthoredStageWidth = 560f;
        public const float AuthoredStageHeight = 658f;

        public const float StageWidth = 660f;
        public const float StageHeight = 730f;

        public const float StageScaleX = StageWidth / AuthoredStageWidth;
        public const float StageScaleY = StageHeight / AuthoredStageHeight;

        public const float StageTop = HalfHeight - PadY - 30f;
        public const float StageCentreY = StageTop - StageHeight * 0.5f;

        // Scaled off the handover's figures rather than restated, so the
        // mannequin and the slots hung on it can never drift apart.
        public const float MannequinWidth = 264f * StageScaleX;
        public const float MannequinHeight = 564f * StageScaleY;
        public const float MannequinLeft = 148f * StageScaleX;
        public const float MannequinTop = 92f * StageScaleY;

        // THE SMALLER OF THE TWO SCALES, and the difference is not academic.
        //
        // A slot is square, and the stage did not grow squarely -- 1.179 across
        // against 1.109 down. Scaled by the width, the slot boxes grew faster
        // than the gaps between them did, and the caption that lives in that
        // gap ended up printed across the icon of the slot below it. Scaled by
        // the height they grow no faster than the room they have.
        public const float StageScale = StageScaleX < StageScaleY ? StageScaleX : StageScaleY;

        public const float SlotSize = 74f * StageScale;

        // ---- where the body ACTUALLY is ----------------------------------------
        //
        // The handover placed every slot against a figure filling the whole
        // 264x564 mannequin box. The drawn figure does not fill it, and the
        // reason is in the PNG rather than in any layout code: UiEmitter does
        // not set preserveAspect, so the sprite is stretched to the box exactly
        // -- but the artwork carries transparent margins, 8% at the top and 9%
        // at the bottom. Measured off the outline's own alpha: its opaque
        // content runs y 126..1395 of 1536, which lands at 46..512 of 564.
        //
        // So the body occupies stage y 138..604, and the slots were laid out
        // around 92..656. The head slot floated about 100px clear of the head,
        // and everything below it sat progressively low.
        //
        // The first attempt at this assumed preserveAspect was on and
        // letterboxing the figure into the middle 324px. That was wrong, and
        // wrong in a way worth recording: it compressed the column so hard that
        // the slots overlapped each other, which looked like a different bug
        // entirely. The number below came from measuring the art, not from
        // reasoning about what the emitter probably did.
        //
        // FRACTIONS OF THE ART, not pixels of a particular box, and that is the
        // difference between this surviving a resize and silently lying about
        // one. These were 46 and 466 against a 564-tall mannequin; the moment
        // the stage grew, a slot column tuned to the drawn figure would have
        // been tuned to a figure 17% taller than the numbers describing it.
        //
        // Straight off the outline's alpha: opaque content runs y 126..1395 of
        // 1536.
        public const float BodyTopFraction = 126f / 1536f;
        public const float BodyHeightFraction = 1269f / 1536f;

        public const float BodyTopInBox = MannequinHeight * BodyTopFraction;
        public const float BodyHeightInBox = MannequinHeight * BodyHeightFraction;

        public static float BodyTop => MannequinTop + BodyTopInBox;

        // A y authored against a box-filling figure, moved onto the drawn one.
        // Proportional rather than clamped, so a slot the handover put ABOVE
        // the body -- the head slot is one -- stays proportionally above it.
        public static float OnBody(float authoredCentreY)
        {
            float fraction = (authoredCentreY - MannequinTop) / MannequinHeight;
            return BodyTop + fraction * BodyHeightInBox;
        }

        // As FromStage, but with the top read against the figure that is really
        // drawn rather than the one the handover assumed.
        // `authoredTop` is in the handover's stage; OnBody works in the drawn
        // one, so it is scaled on the way in and unscaled on the way out --
        // FromStage scales its `top` argument again.
        private static UiVec OnBodyFromStage(float left, float authoredTop, float w, float h)
        {
            float centre = OnBody(authoredTop * StageScaleY + h * 0.5f);
            return FromStage(left, (centre - h * 0.5f) / StageScaleY, w, h);
        }

        // A point given as (left, top) in the HANDOVER's stage, converted into
        // dossier coordinates on the stage this build actually draws.
        //
        // THE ONE PLACE THE SCALE IS APPLIED. Every slot and leader below is
        // still written in the handover's numbers against its 560x658 stage, so
        // growing the stage is two constants rather than thirty-two edits --
        // and the slots keep their relationship to each other and to the
        // figure, which is the whole of what the handover's "never reflow" rule
        // was protecting.
        //
        // Widths and heights scale too. A slot whose BOX grew but whose OFFSET
        // did not would drift further out of place the further down the figure
        // it sat.
        public static UiVec FromStage(float left, float top, float w, float h)
        {
            return new UiVec(
                ColumnBCentreX - StageWidth * 0.5f + left * StageScaleX + w * 0.5f,
                StageCentreY + StageHeight * 0.5f - top * StageScaleY - h * 0.5f);
        }

        // Where each slot sits, straight from the handover's table.
        public static UiVec SlotAt(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return OnBodyFromStage(243f, 0f, SlotSize, SlotSize);
                case EquipmentSlot.Necklace: return OnBodyFromStage(152f, 150f, SlotSize, SlotSize);
                case EquipmentSlot.Torso: return OnBodyFromStage(140f, 248f, SlotSize, SlotSize);
                case EquipmentSlot.Gloves: return OnBodyFromStage(120f, 350f, SlotSize, SlotSize);
                case EquipmentSlot.Legs: return OnBodyFromStage(132f, 452f, SlotSize, SlotSize);
                case EquipmentSlot.Weapon1: return OnBodyFromStage(388f, 248f, SlotSize, SlotSize);
                case EquipmentSlot.Weapon2: return OnBodyFromStage(388f, 350f, SlotSize, SlotSize);
                default: return OnBodyFromStage(392f, 540f, SlotSize, SlotSize);  // Shoes
            }
        }

        // The hairline that ties a slot back to the body: left, top, width in
        // stage coordinates, 1px tall.
        //
        // ITS WIDTH SCALES TOO. A leader spans the gap between the slot column
        // and the figure, and that gap is in stage coordinates -- so a leader
        // left at the handover's own length on a stage 18% wider stops short of
        // the body it is supposed to tie the slot to, which reads as eight
        // hairlines pointing at nothing.
        //
        // One table and one scale point: the cases set the handover's numbers
        // and everything after the switch is common, so a slot cannot be scaled
        // by being edited and another missed.
        public static UiVec LeaderAt(EquipmentSlot slot, out float width)
        {
            float left;
            float top;

            switch (slot)
            {
                case EquipmentSlot.Head: width = 37f; left = 243f; top = 82f; break;
                case EquipmentSlot.Necklace: width = 36f; left = 226f; top = 187f; break;
                case EquipmentSlot.Torso: width = 24f; left = 214f; top = 285f; break;
                case EquipmentSlot.Gloves: width = 18f; left = 194f; top = 387f; break;
                case EquipmentSlot.Legs: width = 36f; left = 206f; top = 489f; break;
                case EquipmentSlot.Weapon1: width = 40f; left = 348f; top = 285f; break;
                case EquipmentSlot.Weapon2: width = 40f; left = 348f; top = 387f; break;
                default: width = 67f; left = 325f; top = 577f; break;  // Shoes
            }

            width *= StageScaleX;
            return OnBodyFromStage(left, top, width, 1f);
        }

        // ---- column C: the numbers ----------------------------------------------

        public const float ContentCWidth = ColumnCWidth - ColumnCPadX * 2f;       // 340

        public const float AttributeCellHeight = 86f;
        public const float AttributeRows = 2f;
        public const float AttributeColumns = 3f;
        public const float AttributeCellWidth = ContentCWidth / AttributeColumns;

        public const float ColumnCTop = HalfHeight - PadY;
        public const float SectionLabelHeight = 24f;
        public const float AttributeBlockTop = ColumnCTop - SectionLabelHeight - 12f;

        public static float AttributeCellCentreX(int column) =>
            ColumnCCentreX - ContentCWidth * 0.5f + AttributeCellWidth * (column + 0.5f);

        public static float AttributeCellCentreY(int row) =>
            AttributeBlockTop - AttributeCellHeight * (row + 0.5f);

        // The stat list starts under the attribute block, with the handover's
        // 18px gap and its own rule.
        public const float StatListTop =
            AttributeBlockTop - AttributeCellHeight * AttributeRows - 22f;

        // Taller, because the list is what closes column C. At 34 the nine
        // derived stats ended 184px above the floor and the column read as
        // half-drawn; at 48 they reach within 78px of it, which MaxStatRows
        // still leaves room to grow into.
        public const float StatRowHeight = 48f;

        public static float StatRowCentreY(int index) =>
            StatListTop - StatRowHeight * (index + 0.5f);

        // How many rows fit before the list runs out of column, so a stat added
        // to SheetStats.Derived cannot silently fall off the bottom.
        public static int MaxStatRows()
        {
            float usable = StatListTop - (-HalfHeight + PadY);
            return (int)(usable / StatRowHeight);
        }

        public static bool StatListFits(int count) => count <= MaxStatRows();
    }
}
