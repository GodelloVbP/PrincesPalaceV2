using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Where the footer's parts sit, as arithmetic over measured widths (plan
    // C12, spec section 2).
    //
    // The footer is a strip BELOW the panel, mirroring the lintel above it, so
    // no pane loses height to it. The notice sits on its left; the prompts sit
    // on its right as [glyph] Label pairs, right-aligned, and are re-laid every
    // time the row changes. Domain has no font, so the caller measures the
    // glyph letters and the labels and hands the widths in, exactly as the tab
    // bar does (SystemMenuLayout.Arrange).
    public static class MenuFooterLayout
    {
        public const float Width = SystemMenuLayout.PanelWidth;
        public const float Height = 48f;

        // Between the panel's bottom edge and the footer: the lintel's own gap.
        public const float GapBelowPanel = 4f;

        // Screen y of the panel's bottom edge on the 1080 stage: the panel is
        // centred, so its top is at (1080 - 900) / 2 = 90 and its bottom 990.
        public static float PanelBottomScreenY =>
            SystemMenuLayout.StageHalfHeight + SystemMenuLayout.PanelHeight * 0.5f;

        // Stage-space y of the footer's centre.
        public static float CentreY =>
            SystemMenuLayout.StageHalfHeight - (PanelBottomScreenY + GapBelowPanel) - Height * 0.5f;

        public const float PadX = 24f;

        // ---- the notice, left ---------------------------------------------------------
        public const float NoticeWidth = 700f;
        public const float NoticeHeight = 32f;

        // Footer space, measured from the footer's own centre.
        public static float NoticeCentreX => -Width * 0.5f + PadX + NoticeWidth * 0.5f;

        // ---- the prompts, right ---------------------------------------------------------
        public const float GlyphHeight = 32f;
        public const float GlyphMinWidth = 32f;
        public const float GlyphPadX = 16f;
        public const float GlyphToLabel = 8f;
        public const float PairGap = 24f;

        // The most pairs the row draws: the frame's own four plus one for a
        // pane. The scene authors this many slots, and the row drops from the
        // tail beyond what fits.
        public const int PromptSlots = 5;

        // The right edge the row is flush against, and the room it has.
        public static float PromptsRightX => Width * 0.5f - PadX;
        public static float PromptsLeftX => NoticeCentreX + NoticeWidth * 0.5f + 32f;
        public static float PromptsWidth => PromptsRightX - PromptsLeftX;

        // One placed pair. X values are the centres of the glyph plate and of
        // the label, in footer space.
        public readonly struct Pair
        {
            public readonly float PlateWidth;
            public readonly float PlateCentreX;
            public readonly float LabelWidth;
            public readonly float LabelCentreX;

            public Pair(float plateWidth, float plateCentreX, float labelWidth, float labelCentreX)
            {
                PlateWidth = plateWidth;
                PlateCentreX = plateCentreX;
                LabelWidth = labelWidth;
                LabelCentreX = labelCentreX;
            }
        }

        public static float PlateWidthFor(float glyphTextWidth) =>
            System.Math.Max(GlyphMinWidth, (float)System.Math.Ceiling(glyphTextWidth + GlyphPadX));

        // The row, flush right. Pairs are kept from the head of the list until
        // the next one would cross into the notice's room: the tail is what
        // goes, because the head is the frame's Select / Close.
        public static IReadOnlyList<Pair> Arrange(
            IReadOnlyList<float> glyphTextWidths, IReadOnlyList<float> labelWidths)
        {
            int available = System.Math.Min(System.Math.Min(glyphTextWidths.Count, labelWidths.Count), PromptSlots);

            var plate = new float[available];
            var pair = new float[available];
            int count = 0;
            float total = 0f;

            for (int i = 0; i < available; i++)
            {
                plate[i] = PlateWidthFor(glyphTextWidths[i]);
                pair[i] = plate[i] + GlyphToLabel + labelWidths[i];

                float next = total + pair[i] + (i > 0 ? PairGap : 0f);
                if (next > PromptsWidth) break;

                total = next;
                count++;
            }

            var placed = new List<Pair>(count);
            float x = PromptsRightX - total;
            for (int i = 0; i < count; i++)
            {
                placed.Add(new Pair(
                    plate[i], x + plate[i] * 0.5f,
                    labelWidths[i], x + plate[i] + GlyphToLabel + labelWidths[i] * 0.5f));

                x += pair[i] + PairGap;
            }

            return placed;
        }
    }
}
