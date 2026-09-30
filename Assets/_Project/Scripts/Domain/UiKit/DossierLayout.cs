using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.UiKit
{
    // The CHARACTER pane's geometry (spec section 3), as pure arithmetic.
    //
    // A portrait strip across the top, a rail down the left that nothing ever
    // covers, and a detail area showing the rail's selected section. Every
    // section is a panel the size of the detail area, placed at its centre,
    // with its own children measured from that centre: DX and DY turn the
    // spec's left/top offsets into that frame once, here.
    //
    // Pane coordinates are measured from the pane's centre, the frame every
    // node in this project is placed in.
    public static class DossierLayout
    {
        // The hosted-pane contract: the whole content area.
        public const float Width = SystemMenuLayout.PanelWidth;                              // 1600
        public const float Height = SystemMenuLayout.PanelHeight - SystemMenuLayout.BarHeight; // 804

        public const float InsetX = 40f;
        public const float InsetY = 24f;
        public const float AreaWidth = Width - InsetX * 2f;    // 1520
        public const float AreaHeight = Height - InsetY * 2f;  // 756

        public const float Left = -AreaWidth * 0.5f;
        public const float Top = AreaHeight * 0.5f;

        // ---- the strip -------------------------------------------------------------

        public const float StripHeight = 72f;
        public const float StripCentreY = Top - StripHeight * 0.5f;

        public const float ChipWidth = 240f;
        public const float ChipHeight = 64f;
        public const float ChipGap = 16f;

        // The gap that holds the divider between the seated party and the
        // bench, in place of the ordinary one.
        public const float DividerGap = 32f;

        // The LB/RB plates at the strip's ends. Clickable with the mouse; the
        // pad reaches the same step with its shoulders, never by moving focus
        // here (spec section 9: the strip is not in the D-pad graph).
        public const float PagerWidth = 56f;
        public const float PagerHeight = 40f;

        public const float PagerPrevX = Left + PagerWidth * 0.5f;
        public const float PagerNextX = -Left - PagerWidth * 0.5f;

        public const float ChipsLeft = Left + PagerWidth + ChipGap;

        // As many chips as fit between the pagers with the divider drawn:
        // 5 x 240 + 4 x 16 + 16 = 1280 of 1376.
        public const int ChipCapacity = 5;

        // A chip's centre when the window's divider falls before chip
        // `dividerIndex` (-1: no divider). The chips from the divider on move
        // right by the difference between the two gaps.
        public static float ChipCentreX(int slot, int dividerIndex)
        {
            float left = ChipsLeft + slot * (ChipWidth + ChipGap);
            if (dividerIndex >= 0 && slot >= dividerIndex) left += DividerGap - ChipGap;
            return left + ChipWidth * 0.5f;
        }

        public static float DividerCentreX(int dividerIndex) =>
            ChipCentreX(dividerIndex, dividerIndex) - ChipWidth * 0.5f - DividerGap * 0.5f;

        // Inside a chip, from its centre.
        public const float ChipPortraitSize = 48f;
        public const float ChipInset = 8f;
        public const float ChipPortraitX = -ChipWidth * 0.5f + ChipInset + ChipPortraitSize * 0.5f;
        public const float ChipTextLeft = -ChipWidth * 0.5f + ChipInset + ChipPortraitSize + 12f;
        public const float ChipPipSize = 12f;
        public const float ChipTextRight = ChipWidth * 0.5f - ChipInset - ChipPipSize - 4f;
        public const float ChipTextWidth = ChipTextRight - ChipTextLeft;
        public const float ChipTextCentreX = ChipTextLeft + ChipTextWidth * 0.5f;
        public const float ChipLineHeight = 26f;
        public const float ChipNameY = 13f;
        public const float ChipSeatY = -13f;
        public const float ChipPipX = ChipWidth * 0.5f - ChipInset - ChipPipSize * 0.5f;
        public const float ChipPipY = ChipHeight * 0.5f - ChipInset - ChipPipSize * 0.5f;

        // ---- the body: rail and detail --------------------------------------------

        public const float StripGap = 24f;
        public const float BodyTop = Top - StripHeight - StripGap;   // 282
        public const float BodyHeight = 660f;
        public const float BodyCentreY = BodyTop - BodyHeight * 0.5f;

        public const float RailWidth = 300f;
        public const float RailGap = 24f;
        public const float RailCentreX = Left + RailWidth * 0.5f;

        public const float DetailWidth = AreaWidth - RailWidth - RailGap;   // 1196
        public const float DetailHeight = BodyHeight;                        // 660
        public const float DetailLeft = Left + RailWidth + RailGap;
        public const float DetailCentreX = DetailLeft + DetailWidth * 0.5f;
        public const float DetailCentreY = BodyCentreY;

        public const float RailRuleX = Left + RailWidth + RailGap * 0.5f;

        // Rail-local y, from the rail's top edge.
        public static float RailY(float fromTop) => BodyHeight * 0.5f - fromTop;

        // Detail-local x and y, from the detail's left and top edges.
        public static float DX(float fromLeft) => -DetailWidth * 0.5f + fromLeft;
        public static float DY(float fromTop) => DetailHeight * 0.5f - fromTop;

        // ---- the rail: who, then where -----------------------------------------------

        public const float PortraitSize = 120f;
        public static float PortraitCentreY => RailY(PortraitSize * 0.5f);

        public const float NameHeight = 36f;
        public static float NameCentreY => RailY(128f + NameHeight * 0.5f);

        public const float LevelHeight = 26f;
        public static float LevelCentreY => RailY(166f + LevelHeight * 0.5f);

        // The XP bar spans the rail; the fill grows from its left edge.
        public const float XpTrackWidth = RailWidth;
        public const float XpTrackHeight = 6f;
        public static float XpTrackCentreY => RailY(204f + XpTrackHeight * 0.5f);
        public const float XpLabelHeight = 26f;
        public static float XpLabelCentreY => RailY(214f + XpLabelHeight * 0.5f);

        public const float IdentityHeight = 260f;
        public static float IdentityRuleY => RailY(IdentityHeight);

        public const float SectionTop = IdentityHeight + 24f;
        public const float SectionRowHeight = 64f;
        public const float SectionInset = 20f;
        public const float SectionBarWidth = 3f;
        public const float SectionBarHeight = 40f;
        public const float SectionPipSize = 12f;
        public const float SectionLabelHeight = 32f;

        public static float SectionRowCentreY(int index) =>
            RailY(SectionTop + SectionRowHeight * 0.5f + index * SectionRowHeight);

        // ---- LOADOUT (spec 3.1) ------------------------------------------------------

        public const float SlotsLeft = 0f;
        public const float SlotsWidth = 440f;
        public const float PackLeft = 464f;
        public const float PackWidth = 408f;
        public const float CompareLeft = 896f;
        public const float CompareWidth = 300f;

        public const float SlotSize = 88f;
        public const float SlotPitch = 116f;
        public const float SlotCaptionWidth = 104f;
        public const float SlotCaptionHeight = 26f;
        public const float SlotCaptionGap = 2f;

        // The three files the gear hangs in, as x from the column's left.
        public const float FileLeftX = 52f;
        public const float FileCentreX = 220f;
        public const float FileRightX = 388f;

        public const int GearRows = 4;

        // TODAY'S ARRANGEMENT, IN FOUR ROWS: Head top centre and Necklace
        // beside it; Torso, Gloves, Legs down the left; Weapon 1, Weapon 2,
        // Shoes down the right, level with Torso, Gloves and Legs. One table
        // read by the screen for positions and by the controller for the
        // focus grid, so where a slot is drawn and where a press lands cannot
        // disagree.
        public static void GearCell(EquipmentSlot slot, out int row, out float fileX)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: row = 0; fileX = FileCentreX; return;
                case EquipmentSlot.Necklace: row = 0; fileX = FileLeftX; return;
                case EquipmentSlot.Torso: row = 1; fileX = FileLeftX; return;
                case EquipmentSlot.Gloves: row = 2; fileX = FileLeftX; return;
                case EquipmentSlot.Legs: row = 3; fileX = FileLeftX; return;
                case EquipmentSlot.Weapon1: row = 1; fileX = FileRightX; return;
                case EquipmentSlot.Weapon2: row = 2; fileX = FileRightX; return;
                case EquipmentSlot.Shoes: row = 3; fileX = FileRightX; return;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(slot), slot,
                        "The loadout has no cell for this slot -- add it to GearCell.");
            }
        }

        public static UiVec GearCentre(EquipmentSlot slot)
        {
            GearCell(slot, out int row, out float fileX);
            return new UiVec(DX(SlotsLeft + fileX), DY(row * SlotPitch + SlotSize * 0.5f));
        }

        public static UiVec GearCaptionCentre(EquipmentSlot slot)
        {
            GearCell(slot, out int row, out float fileX);
            return new UiVec(DX(SlotsLeft + fileX),
                DY(row * SlotPitch + SlotSize + SlotCaptionGap + SlotCaptionHeight * 0.5f));
        }

        // The figure the gear hangs on, between the files and under Head: the
        // armour stand art at its own aspect (264 x 564).
        public const float FigureTop = 100f;
        public const float FigureBottom = 462f;
        public const float FigureHeight = FigureBottom - FigureTop;
        public const float FigureWidth = FigureHeight * 264f / 564f;
        public static UiVec FigureCentre => new UiVec(DX(SlotsLeft + FileCentreX), DY((FigureTop + FigureBottom) * 0.5f));

        // The book row, under the gear.
        public const float BooksHeadingTop = 474f;
        public const float BooksHeadingHeight = 34f;
        public static float BooksHeadingCentreY => DY(BooksHeadingTop + BooksHeadingHeight * 0.5f);
        public const float BookRowTop = 512f;

        // Spread across the column, matching the gear files for three slots.
        public static float BookFileX(int index)
        {
            int count = SpellBooks.MaxSpellSlots;
            if (count <= 1) return FileCentreX;
            return FileLeftX + index * (FileRightX - FileLeftX) / (count - 1);
        }

        public static UiVec BookCentre(int index) =>
            new UiVec(DX(SlotsLeft + BookFileX(index)), DY(BookRowTop + SlotSize * 0.5f));

        public static UiVec BookCaptionCentre(int index) =>
            new UiVec(DX(SlotsLeft + BookFileX(index)),
                DY(BookRowTop + SlotSize + SlotCaptionGap + SlotCaptionHeight * 0.5f));

        // What stands in the book row for a character who cannot carry books.
        public const float NoBooksHeight = 60f;
        public static UiVec NoBooksCentre =>
            new UiVec(DX(SlotsLeft + SlotsWidth * 0.5f), DY(BookRowTop + NoBooksHeight * 0.5f));

        // ---- the pack, beside the slots ----------------------------------------------

        public const float FilterHeight = 44f;
        // Five chips across the pack; "Weapons" at 20 is the widest caption.
        public const float FilterWidth = 78f;
        public const float FilterGap = 4f;

        public static float FilterCentreX(int index) =>
            DX(PackLeft + FilterWidth * 0.5f + index * (FilterWidth + FilterGap));

        public static float FilterCentreY => DY(FilterHeight * 0.5f);

        public const float PackGridTop = 58f;
        public const float PackCell = 112f;
        public const float PackCellGap = 14f;
        public const int PackColumns = 3;
        public const int PackRows = 4;
        public const int PackVisibleCells = PackColumns * PackRows;
        public const float PackIconSize = 64f;

        public const float PackGridWidth = PackColumns * PackCell + (PackColumns - 1) * PackCellGap;   // 364
        public const float PackGridHeight = PackRows * PackCell + (PackRows - 1) * PackCellGap;        // 490

        public static UiVec PackCellCentre(int visibleIndex)
        {
            int column = visibleIndex % PackColumns;
            int row = visibleIndex / PackColumns;
            return new UiVec(
                DX(PackLeft + PackCell * 0.5f + column * (PackCell + PackCellGap)),
                DY(PackGridTop + PackCell * 0.5f + row * (PackCell + PackCellGap)));
        }

        public static UiVec PackGridCentre =>
            new UiVec(DX(PackLeft + PackGridWidth * 0.5f), DY(PackGridTop + PackGridHeight * 0.5f));

        // The scrollbar, right of the grid: a track and a thumb the controller
        // sizes and moves.
        public const float ScrollbarWidth = 8f;
        public const float ScrollbarGap = 10f;
        public static float ScrollbarCentreX => DX(PackLeft + PackGridWidth + ScrollbarGap + ScrollbarWidth * 0.5f);
        public const float ScrollThumbMinHeight = 32f;

        // The thumb's length says how much of the pack is on screen.
        public static float ScrollThumbHeight(int rows)
        {
            if (rows <= PackRows) return PackGridHeight;
            float height = PackGridHeight * PackRows / rows;
            return height < ScrollThumbMinHeight ? ScrollThumbMinHeight : height;
        }

        public const float CarriedTop = 560f;
        public const float CarriedHeight = 26f;
        public static UiVec CarriedCentre =>
            new UiVec(DX(PackLeft + PackGridWidth * 0.5f), DY(CarriedTop + CarriedHeight * 0.5f));

        public const float PackEmptyWidth = 320f;
        public const float PackEmptyHeight = 80f;

        // ---- compare, beside the pack --------------------------------------------------

        public static float CompareCentreX => DX(CompareLeft + CompareWidth * 0.5f);

        public const float CompareNameHeight = 64f;
        public static float CompareNameCentreY => DY(CompareNameHeight * 0.5f);
        public const float CompareKindHeight = 26f;
        public static float CompareKindCentreY => DY(70f + CompareKindHeight * 0.5f);
        public const float CompareDescriptionHeight = 196f;
        public static float CompareDescriptionCentreY => DY(104f + CompareDescriptionHeight * 0.5f);

        public const float CompareRowsTop = 312f;
        public const float CompareRowHeight = 32f;
        public const float CompareStatNameWidth = 140f;
        public const float CompareStatValueWidth = CompareWidth - CompareStatNameWidth;

        public static float CompareRowCentreY(int index) =>
            DY(CompareRowsTop + CompareRowHeight * 0.5f + index * CompareRowHeight);

        public static float CompareStatNameCentreX => DX(CompareLeft + CompareStatNameWidth * 0.5f);
        public static float CompareStatValueCentreX =>
            DX(CompareLeft + CompareStatNameWidth + CompareStatValueWidth * 0.5f);

        // ---- ATTRIBUTES (spec 3.2; phase 5d redesigns it) ------------------------------

        public const float AttributesHeaderHeight = 30f;
        public static float AttributesHeaderCentreY => DY(AttributesHeaderHeight * 0.5f);

        public const float AttributeRowsTop = 44f;
        public const float AttributeRowWidth = 700f;
        public const float AttributeRowHeight = 84f;
        public static float AttributeRowCentreX => DX(AttributeRowWidth * 0.5f);

        public static float AttributeRowCentreY(int index) =>
            DY(AttributeRowsTop + AttributeRowHeight * 0.5f + index * AttributeRowHeight);

        // Inside a row, from its centre.
        public const float StepperSize = 44f;
        public const float StepperGap = 8f;
        public const float StepperY = AttributeRowHeight * 0.5f - 4f - StepperSize * 0.5f;
        public const float PlusX = AttributeRowWidth * 0.5f - StepperSize * 0.5f;
        public const float MinusX = PlusX - StepperSize - StepperGap;
        public const float AttributeNameWidth = 110f;
        public const float AttributeValueWidth = 70f;
        public const float AttributeInvestedWidth = 200f;
        public const float AttributeTopLineHeight = 34f;
        public const float AttributeTopLineY = AttributeRowHeight * 0.5f - 4f - AttributeTopLineHeight * 0.5f;
        public const float AttributeEffectWidth = 600f;
        public const float AttributeEffectHeight = 44f;
        public const float AttributeEffectY = -AttributeRowHeight * 0.5f + 2f + AttributeEffectHeight * 0.5f;

        public const float DerivedLeft = 726f;
        public const float DerivedWidth = DetailWidth - DerivedLeft;   // 470
        public const float DerivedRowHeight = 44f;
        public const float DerivedNameWidth = 280f;
        public const float DerivedValueWidth = DerivedWidth - DerivedNameWidth;

        public static float DerivedRowCentreY(int index) =>
            DY(AttributeRowsTop + DerivedRowHeight * 0.5f + index * DerivedRowHeight);

        public static float DerivedCentreX => DX(DerivedLeft + DerivedWidth * 0.5f);
        public static float DerivedNameCentreX => DX(DerivedLeft + DerivedNameWidth * 0.5f);
        public static float DerivedValueCentreX => DX(DerivedLeft + DerivedNameWidth + DerivedValueWidth * 0.5f);

        // ---- SKILLS (spec 3.3; phase 5c redesigns it) ------------------------------------

        public const int SkillsVisible = 8;
        public const float SkillRowHeight = 76f;
        public const float SkillWidth = 900f;
        public const float SkillNameHeight = 30f;
        public const float SkillDescriptionHeight = 44f;
        public static float SkillCentreX => DX(SkillWidth * 0.5f);

        public static float SkillNameCentreY(int index) =>
            DY(index * SkillRowHeight + SkillNameHeight * 0.5f);

        public static float SkillDescriptionCentreY(int index) =>
            DY(index * SkillRowHeight + SkillNameHeight + SkillDescriptionHeight * 0.5f);
    }
}
