using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.UiKit
{
    // The character dossier's geometry, as pure arithmetic.
    //
    // The handover authors this screen at 1360x766 and says: scale the whole
    // panel as one unit, never reflow, never convert individual values to
    // percentages -- the mannequin slots, the leader hairlines and the numeral
    // columns are positioned against each other and only stay coherent under
    // uniform scale.
    //
    // It gets that for free here by being SMALLER than the box it sits in. The
    // system menu's content pane is 1600x804, so the dossier is placed at its
    // authored size and centred, and no scaling happens at all. That also side-
    // steps the handover's legibility floor: nothing shrinks, so 9.5px type
    // never drops under 9.
    //
    // Coordinates are measured from the DOSSIER's own centre, like every other
    // screen in this project.
    public static class DossierLayout
    {
        public const float Width = 1360f;
        public const float Height = 766f;

        public const float HalfWidth = Width * 0.5f;
        public const float HalfHeight = Height * 0.5f;

        // Three columns, no gap; the dividers are borders.
        public const float ColumnAWidth = 318f;
        public const float ColumnCWidth = 408f;
        public const float ColumnBWidth = Width - ColumnAWidth - ColumnCWidth;   // 634

        public const float PadY = 30f;
        public const float ColumnAPadX = 26f;
        public const float ColumnBPadLeft = 34f;
        public const float ColumnCPadX = 34f;

        // Centre x of each column, measured from the panel centre.
        public const float ColumnACentreX = -HalfWidth + ColumnAWidth * 0.5f;
        public const float ColumnBCentreX = -HalfWidth + ColumnAWidth + ColumnBWidth * 0.5f;
        public const float ColumnCCentreX = HalfWidth - ColumnCWidth * 0.5f;

        // The two dividers sit on the column boundaries.
        public const float DividerAtoB = -HalfWidth + ColumnAWidth;
        public const float DividerBtoC = HalfWidth - ColumnCWidth;

        // ---- column A -----------------------------------------------------------

        public const float ContentAWidth = ColumnAWidth - ColumnAPadX * 2f;      // 266
        public const float PortraitHeight = 270f;

        // Everything in column A stacks from the top, so each y is the one
        // above it minus its own height. Stated as running totals rather than
        // as a flow container because the pack panel has to cover the column
        // exactly and a flow would fight that.
        public const float ColumnATop = HalfHeight - PadY;
        public const float PortraitCentreY = ColumnATop - PortraitHeight * 0.5f;
        public const float NameCentreY = PortraitCentreY - PortraitHeight * 0.5f - 18f - 21f;
        public const float SubLineCentreY = NameCentreY - 21f - 12f;
        public const float XpRowCentreY = SubLineCentreY - 12f - 14f - 8f;
        public const float SkillsRowCentreY = XpRowCentreY - 8f - 14f - 22f;
        public const float PackRowCentreY = SkillsRowCentreY - 44f;

        public const float NavRowHeight = 44f;

        // ---- column B: the loadout stage ----------------------------------------

        // The handover's stage is 560x658 with the mannequin at left 148, top 92
        // inside it, 264x564. Those are top-left offsets in the stage; this
        // converts them once, here, rather than at eight call sites.
        public const float StageWidth = 560f;
        public const float StageHeight = 658f;
        public const float StageTop = HalfHeight - PadY - 34f;
        public const float StageCentreY = StageTop - StageHeight * 0.5f;

        public const float MannequinWidth = 264f;
        public const float MannequinHeight = 564f;
        public const float MannequinLeft = 148f;
        public const float MannequinTop = 92f;

        public const float SlotSize = 74f;

        // A point given as (left, top) inside the stage, in dossier coordinates.
        public static UiVec FromStage(float left, float top, float w, float h)
        {
            return new UiVec(
                ColumnBCentreX - StageWidth * 0.5f + left + w * 0.5f,
                StageCentreY + StageHeight * 0.5f - top - h * 0.5f);
        }

        // Where each slot sits, straight from the handover's table.
        public static UiVec SlotAt(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return FromStage(243f, 0f, SlotSize, SlotSize);
                case EquipmentSlot.Necklace: return FromStage(152f, 150f, SlotSize, SlotSize);
                case EquipmentSlot.Torso: return FromStage(140f, 248f, SlotSize, SlotSize);
                case EquipmentSlot.Gloves: return FromStage(120f, 350f, SlotSize, SlotSize);
                case EquipmentSlot.Legs: return FromStage(132f, 452f, SlotSize, SlotSize);
                case EquipmentSlot.Weapon1: return FromStage(388f, 248f, SlotSize, SlotSize);
                case EquipmentSlot.Weapon2: return FromStage(388f, 350f, SlotSize, SlotSize);
                default: return FromStage(392f, 540f, SlotSize, SlotSize);        // Shoes
            }
        }

        // The hairline that ties a slot back to the body: left, top, width in
        // stage coordinates, 1px tall.
        public static UiVec LeaderAt(EquipmentSlot slot, out float width)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: width = 37f; return FromStage(243f, 82f, width, 1f);
                case EquipmentSlot.Necklace: width = 36f; return FromStage(226f, 187f, width, 1f);
                case EquipmentSlot.Torso: width = 24f; return FromStage(214f, 285f, width, 1f);
                case EquipmentSlot.Gloves: width = 18f; return FromStage(194f, 387f, width, 1f);
                case EquipmentSlot.Legs: width = 36f; return FromStage(206f, 489f, width, 1f);
                case EquipmentSlot.Weapon1: width = 40f; return FromStage(348f, 285f, width, 1f);
                case EquipmentSlot.Weapon2: width = 40f; return FromStage(348f, 387f, width, 1f);
                default: width = 67f; return FromStage(325f, 577f, width, 1f);    // Shoes
            }
        }

        // ---- column C: the numbers ----------------------------------------------

        public const float ContentCWidth = ColumnCWidth - ColumnCPadX * 2f;       // 340

        public const float AttributeCellHeight = 66f;
        public const float AttributeRows = 2f;
        public const float AttributeColumns = 3f;
        public const float AttributeCellWidth = ContentCWidth / AttributeColumns;

        public const float ColumnCTop = HalfHeight - PadY;
        public const float SectionLabelHeight = 22f;
        public const float AttributeBlockTop = ColumnCTop - SectionLabelHeight - 10f;

        public static float AttributeCellCentreX(int column) =>
            ColumnCCentreX - ContentCWidth * 0.5f + AttributeCellWidth * (column + 0.5f);

        public static float AttributeCellCentreY(int row) =>
            AttributeBlockTop - AttributeCellHeight * (row + 0.5f);

        // The stat list starts under the attribute block, with the handover's
        // 18px gap and its own rule.
        public const float StatListTop =
            AttributeBlockTop - AttributeCellHeight * AttributeRows - 18f;

        public const float StatRowHeight = 34f;

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
