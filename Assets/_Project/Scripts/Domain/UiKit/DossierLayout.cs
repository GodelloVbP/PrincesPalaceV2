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

        // The glyph plate drawn on the pager: the footer's own plate, refitted
        // to its letters between these bounds.
        public const float PagerPlateWidth = 48f;

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
        // A label whose box is shorter than one line of its font draws nothing
        // (TMP clips the first line before it starts), so a Body 20 line is
        // never boxed below its 26.5 px line: two 30s make the chip's 60 of 64.
        public const float ChipLineHeight = 30f;
        public const float ChipNameY = 15f;
        public const float ChipSeatY = -15f;
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

        public const float NameHeight = 40f;
        public static float NameCentreY => RailY(126f + NameHeight * 0.5f);

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

        // 404 + 16 + 408 + 16 + 352 = 1196. The pack keeps 408 because five
        // filter chips of 78 need it; the slot column gives up what the gear
        // files do not use, so that the compare column can hold "Physical DEF
        // 1000 (75%) -> 1200 (79%) (+200)" on one line.
        public const float ColumnGap = 16f;
        public const float SlotsLeft = 0f;
        public const float SlotsWidth = 404f;
        public const float PackLeft = SlotsLeft + SlotsWidth + ColumnGap;   // 420
        public const float PackWidth = 408f;
        public const float CompareLeft = PackLeft + PackWidth + ColumnGap;  // 844
        public const float CompareWidth = DetailWidth - CompareLeft;        // 352

        public const float SlotSize = 88f;

        // A caption is one Body 20 line (26.5 px) and needs a box at least that
        // tall, or TMP draws nothing: 30 under a 2 px gap, then 4 clear before
        // the next cell.
        public const float SlotPitch = 122f;
        public const float SlotCaptionWidth = 104f;
        public const float SlotCaptionHeight = 30f;
        public const float SlotCaptionGap = 2f;

        // The three files the gear hangs in, as x from the column's left.
        public const float FileLeftX = 52f;
        public const float FileCentreX = 202f;
        public const float FileRightX = 352f;

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
        public const float FigureTop = 126f;
        public const float FigureBottom = 486f;
        public const float FigureHeight = FigureBottom - FigureTop;
        public const float FigureWidth = FigureHeight * 264f / 564f;
        public static UiVec FigureCentre => new UiVec(DX(SlotsLeft + FileCentreX), DY((FigureTop + FigureBottom) * 0.5f));

        // The book row, under the gear.
        public const float BooksHeadingTop = 494f;
        public const float BooksHeadingHeight = 34f;
        public static float BooksHeadingCentreY => DY(BooksHeadingTop + BooksHeadingHeight * 0.5f);
        public const float BookRowTop = 532f;

        // A book's caption is its name once filled, so it is wider than a gear
        // caption: the neighbours' centres are 138 apart, the first and last
        // sit half a caption inside the column.
        public const float BookCaptionWidth = 128f;

        // Spread across the column, the middle one under Head.
        public static float BookFileX(int index)
        {
            int count = SpellBooks.MaxSpellSlots;
            if (count <= 1) return FileCentreX;
            float first = BookCaptionWidth * 0.5f;
            float last = SlotsWidth - BookCaptionWidth * 0.5f;
            return first + index * (last - first) / (count - 1);
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

        // Body 20 lines are 26.5 px: no box under that (see ChipLineHeight).
        public const float CompareKindTop = 66f;
        public const float CompareKindHeight = 30f;
        public static float CompareKindCentreY => DY(CompareKindTop + CompareKindHeight * 0.5f);

        // Four lines of a gear summary; a longer one ends in an ellipsis.
        public const float CompareDescriptionTop = 100f;
        public const float CompareDescriptionHeight = 108f;
        public static float CompareDescriptionCentreY =>
            DY(CompareDescriptionTop + CompareDescriptionHeight * 0.5f);

        // The words of a book or a salve, which have no stat rows to make room
        // for: the whole column under the kind line. Shown INSTEAD of the
        // description and the rows.
        public const float CompareBodyHeight = 448f;
        public static float CompareBodyCentreY => DY(CompareDescriptionTop + CompareBodyHeight * 0.5f);

        // A stat is two lines, its name and under it what it becomes, because
        // "10 (9%) -> 29 (22%) (+19)" does not share a line with a name in 352.
        // Eight of them end at 644 of 660.
        public const float CompareRowsTop = 212f;
        public const float CompareLineHeight = 27f;
        public const float CompareRowHeight = CompareLineHeight * 2f;

        public static float CompareNameLineCentreY(int index) =>
            DY(CompareRowsTop + index * CompareRowHeight + CompareLineHeight * 0.5f);

        public static float CompareValueLineCentreY(int index) =>
            DY(CompareRowsTop + index * CompareRowHeight + CompareLineHeight * 1.5f);

        // ---- ATTRIBUTES (spec 3.2) ------------------------------------------------------

        // The header line: points to spend on the left, why a change is
        // refused on the right.
        public const float AttributesHeaderHeight = 30f;
        public static float AttributesHeaderCentreY => DY(AttributesHeaderHeight * 0.5f);

        // Six rows on a 102 pitch, each a 100 panel: a 44 top line (name,
        // value, the steppers, the points invested), then the effect line,
        // which may take two lines. 40 + 6 x 102 = 652 of 660.
        public const float AttributeRowsTop = 40f;
        public const float AttributeRowWidth = 700f;
        public const float AttributeRowHeight = 102f;
        public const float AttributeRowInner = 100f;
        public static float AttributeRowCentreX => DX(AttributeRowWidth * 0.5f);

        public static float AttributeRowCentreY(int index) =>
            DY(AttributeRowsTop + AttributeRowHeight * 0.5f + index * AttributeRowHeight);

        // Inside a row, x from its left edge and y from its top edge. The
        // steppers sit right after the value, so the eye that reads "CON 9"
        // finds the buttons for it without crossing the pane.
        public const float AttributeTopLineHeight = 44f;
        public const float AttributeNameLeft = 0f;
        public const float AttributeNameWidth = 96f;
        public const float AttributeValueLeft = 100f;
        public const float AttributeValueWidth = 56f;
        public const float StepperSize = 44f;
        public const float StepperGap = 8f;
        public const float MinusLeft = 164f;
        public const float PlusLeft = MinusLeft + StepperSize + StepperGap;   // 216
        public const float AttributeInvestedLeft = PlusLeft + StepperSize + 16f;   // 276
        public const float AttributeInvestedWidth = 220f;

        // Two Body 20 lines are 53 px; 54 holds them.
        public const float AttributeEffectTop = 46f;
        public const float AttributeEffectHeight = 54f;

        // A point inside a row, in the row's own centred frame.
        public static float RowX(float fromLeft, float width) => -AttributeRowWidth * 0.5f + fromLeft + width * 0.5f;
        public static float RowY(float fromTop, float height) => AttributeRowInner * 0.5f - fromTop - height * 0.5f;

        public const float DerivedLeft = 726f;
        public const float DerivedWidth = DetailWidth - DerivedLeft;   // 470
        public const float DerivedRowHeight = 44f;

        // A row shows "1000 (75%) -> 1020 (76%)" while a stepper is on it, which
        // the value column has to hold on one line.
        public const float DerivedNameWidth = 170f;
        public const float DerivedValueWidth = DerivedWidth - DerivedNameWidth;   // 300

        public static float DerivedRowCentreY(int index) =>
            DY(AttributeRowsTop + DerivedRowHeight * 0.5f + index * DerivedRowHeight);

        public static float DerivedCentreX => DX(DerivedLeft + DerivedWidth * 0.5f);
        public static float DerivedNameCentreX => DX(DerivedLeft + DerivedNameWidth * 0.5f);
        public static float DerivedValueCentreX => DX(DerivedLeft + DerivedNameWidth + DerivedValueWidth * 0.5f);

        // ---- SKILLS (spec 3.3) ------------------------------------------------------------

        // One vertical list of entries of four heights, windowed by whole
        // entries (SkillsListModel). The scrollbar sits at the detail's right
        // edge, so the rows are 24 narrower than the detail.
        public const float SkillsScrollbarGap = 16f;
        public const float SkillsRowWidth = DetailWidth - ScrollbarWidth - SkillsScrollbarGap;   // 1172

        public const float SkillsHeaderHeight = 44f;
        public const float SkillsRowHeight = 96f;
        public const float SkillsNoteHeight = 44f;
        public const float SkillsLinkHeight = 64f;

        // How many of each a window can hold. Rows are what the height bounds
        // (660 / 96); the headers are one per group, and a list has at most
        // the five skill groups and TALENTS; a list has two notes at most (the
        // empty sentence and the embers line) and one link.
        public const int SkillsRowSlots = 6;
        public const int SkillsHeaderSlots = 6;
        public const int SkillsNoteSlots = 3;

        // Inside a row, from its centre. A Body 22 name line is 29 px, and a
        // Body 20 line 26.5: the description box holds two of them.
        public const float SkillsInset = 16f;
        public const float SkillsNameWidth = 440f;
        public const float SkillsNameHeight = 30f;
        public const float SkillsMetaWidth = SkillsRowWidth - SkillsInset * 2f - SkillsNameWidth - 8f;   // 692
        public const float SkillsDescriptionHeight = 54f;
        public const float SkillsLineTop = 6f;
        public const float SkillsDescriptionTop = SkillsLineTop + SkillsNameHeight + 2f;                  // 38

        public static float SkillsNameX => -SkillsRowWidth * 0.5f + SkillsInset + SkillsNameWidth * 0.5f;
        public static float SkillsMetaX => SkillsRowWidth * 0.5f - SkillsInset - SkillsMetaWidth * 0.5f;
        public static float SkillsLineY => SkillsRowHeight * 0.5f - SkillsLineTop - SkillsNameHeight * 0.5f;
        public static float SkillsDescriptionY => SkillsRowHeight * 0.5f - SkillsDescriptionTop - SkillsDescriptionHeight * 0.5f;

        // The link is a plate, not a full row.
        public const float SkillsLinkWidth = 420f;
        public const float SkillsLinkPlateHeight = 48f;

        public static float SkillsRowCentreX => DX(SkillsRowWidth * 0.5f);
        public static float SkillsLinkCentreX => DX(SkillsInset + SkillsLinkWidth * 0.5f);

        // An entry's centre when it starts `fromTop` below the list's top.
        public static float SkillsCentreY(float fromTop, float height) => DY(fromTop + height * 0.5f);

        public static float SkillsScrollbarCentreX => DX(SkillsRowWidth + SkillsScrollbarGap + ScrollbarWidth * 0.5f);

        // The thumb's length says how much of the list is on screen.
        public static float SkillsThumbHeight(float shown, float total)
        {
            if (total <= 0f || shown >= total) return DetailHeight;
            float height = DetailHeight * shown / total;
            return height < ScrollThumbMinHeight ? ScrollThumbMinHeight : height;
        }
    }
}
