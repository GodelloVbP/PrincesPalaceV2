using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Where the overarching menu's parts sit, as pure arithmetic.
    //
    // The whole point of this type is that the TAB BAR IS RELATIVE: every
    // position is computed from the tab table, so changing that table moves the
    // rest along on its own. Nothing here is a hand-placed x.
    //
    // It lives in Domain, beside FightSubmenuLayout and for the same reason:
    // the builder places the tabs and the controller has to know where they
    // are, and two hand-mirrored copies of the same constants is the drift that
    // FightSubmenuLayout's own header exists to record.
    public static class SystemMenuLayout
    {
        // The overlay fills the reference stage. A menu that sits over
        // everything has no reason to be smaller than everything.
        public const float PanelWidth = 1600f;
        public const float PanelHeight = 900f;

        public const float BarHeight = 96f;

        // How far the strip stays clear of the panel's edges.
        public const float BarInsetLeft = 40f;
        public const float BarInsetRight = 40f;

        public const float TabHeight = 56f;

        // LETTER-SPACING, in hundredths of an em, and it is part of the layout
        // rather than of the styling.
        //
        // Every SystemMenuTabDef.LabelWidth was measured at 18px Chakra Petch
        // with .14em tracking, and the entire bar is arithmetic over those
        // numbers. Draw the labels at the font's own spacing and they come out
        // about a quarter narrower than the boxes built for them -- measured,
        // "CHARACTER & INVENTORY" is 215.5px untracked against the 272 this
        // table claims, and 265.9 with the tracking on. So the two belong
        // together: change this and the widths are lies.
        //
        // SystemMenuLabelWidthTests holds them to each other.
        public const float TabLabelTracking = 14f;

        // The design's .22em for the run title on the lintel.
        public const float LintelTitleTracking = 22f;

        // The hairline between two tabs, centred in the gap.
        public const float DividerWidth = 2f;
        public const float DividerHeight = 40f;

        // Half the panel, so a child placed at these is measured from the
        // panel's own centre like every other node in this project.
        public const float HalfWidth = PanelWidth * 0.5f;
        public const float HalfHeight = PanelHeight * 0.5f;

        // The bar hangs from the top edge; the content fills everything under it.
        public static float BarCentreY => HalfHeight - BarHeight * 0.5f;

        public static float ContentHeight => PanelHeight - BarHeight;

        public static float ContentCentreY => HalfHeight - BarHeight - ContentHeight * 0.5f;

        // The row the tabs live in, inside the insets.
        public const float RowWidth = PanelWidth - BarInsetLeft - BarInsetRight;   // 1520

        // ---- the two modes ------------------------------------------------------
        //
        // ONE COMPONENT, TWO RULES, and the split is forced rather than chosen.
        //
        // Merging Character and Inventory produced a label no uniform box could
        // hold, and the in-run set is five tabs, which no uniform width fits --
        // five boxes wide enough for "CHARACTER & INVENTORY" need 2100px in a
        // 1520px row. So a small set gets even, generous boxes, and a large set
        // sizes each box to its own label and shares out what is left.
        //
        // The threshold is a count, not a measurement, because the design
        // states it as one: three or fewer is Mode A.
        public const int UniformModeMaxTabs = 3;

        // Mode A: the gap is fixed and the width is what remains.
        public const float UniformGap = 130f;

        // Mode B: each box is its label plus this much padding on each side,
        // rounded so the row does not land on half pixels.
        public const float MeasuredPadX = 24f;
        public const float MeasuredRounding = 4f;

        // The smallest gap Mode B will accept before the bar is overfull.
        public const float MinimumGap = 24f;

        // The underline is the label plus a little, NOT the box: a rule as wide
        // as a generous Mode A box reads as a second divider rather than as a
        // marker for the word above it.
        public const float UnderlineOverhang = 24f;
        public const float UnderlineHeight = 3f;

        // Distance below the bar's centre line to the underline, from the
        // design's panel-space y 81 against a bar spanning panel y 0..96.
        public const float UnderlineOffsetY = -33f;

        public static bool IsUniformMode(int count) => count <= UniformModeMaxTabs;

        // ---- everything below is arithmetic over LABEL WIDTHS ------------------
        //
        // Widths, not tab definitions, and that is the seam that lets the bar
        // lay itself out around what is ACTUALLY DRAWN.
        //
        // SystemMenuTabDef.LabelWidth is a hand-measured number and was wrong
        // for months: it came off a design prototype at .14em while the emitter
        // drew at zero tracking, so every box carried 50px of air and the
        // underline overhung its own word. The authored figure is now the
        // BUILD-TIME approximation only -- the scene has to be emitted from
        // something before any text exists to measure -- and the controller
        // re-lays the bar from the real TMP metrics the moment it opens.
        //
        // So changing a tab's wording is now just changing the wording.
        public static float[] TabWidths(IReadOnlyList<float> labelWidths)
        {
            var widths = new float[labelWidths.Count];
            if (labelWidths.Count == 0) return widths;

            if (IsUniformMode(labelWidths.Count))
            {
                float uniform = (RowWidth - UniformGap * (labelWidths.Count - 1)) / labelWidths.Count;
                for (int i = 0; i < labelWidths.Count; i++) widths[i] = uniform;
                return widths;
            }

            for (int i = 0; i < labelWidths.Count; i++)
            {
                float wanted = labelWidths[i] + MeasuredPadX * 2f;
                // System.Math, not Mathf: this assembly deliberately does not
                // reference UnityEngine, which is what keeps the layout
                // arithmetic testable without a player loop. AwayFromZero
                // because "nearest" is what the design says, and Math.Round's
                // default is banker's rounding.
                widths[i] = (float)System.Math.Round(
                    wanted / MeasuredRounding, System.MidpointRounding.AwayFromZero) * MeasuredRounding;
            }

            return widths;
        }

        // The gap between boxes. Uniform mode fixes it; measured mode shares
        // out whatever the boxes did not use, so the row always ends flush
        // against the right inset.
        public static float GapFor(IReadOnlyList<float> labelWidths)
        {
            if (labelWidths.Count <= 1) return 0f;
            if (IsUniformMode(labelWidths.Count)) return UniformGap;

            float used = 0f;
            foreach (float w in TabWidths(labelWidths)) used += w;
            return (RowWidth - used) / (labelWidths.Count - 1);
        }

        // Left edge of each box, in panel space (0 at the panel's left edge).
        public static float[] TabLefts(IReadOnlyList<float> labelWidths)
        {
            var widths = TabWidths(labelWidths);
            float gap = GapFor(labelWidths);
            var lefts = new float[labelWidths.Count];

            float x = BarInsetLeft;
            for (int i = 0; i < labelWidths.Count; i++)
            {
                lefts[i] = x;
                x += widths[i] + gap;
            }

            return lefts;
        }

        // Centre x of each box, measured from the PANEL CENTRE, which is the
        // space every node in this project is placed in.
        public static float[] TabCentresX(IReadOnlyList<float> labelWidths)
        {
            var widths = TabWidths(labelWidths);
            var lefts = TabLefts(labelWidths);
            var centres = new float[labelWidths.Count];

            for (int i = 0; i < labelWidths.Count; i++)
            {
                centres[i] = -HalfWidth + lefts[i] + widths[i] * 0.5f;
            }

            return centres;
        }

        // The divider that follows tab `index`, centred in the gap after it.
        public static float DividerCentreX(IReadOnlyList<float> labelWidths, int index)
        {
            var widths = TabWidths(labelWidths);
            var lefts = TabLefts(labelWidths);

            float rightOfThis = lefts[index] + widths[index];
            float leftOfNext = lefts[index + 1];
            return -HalfWidth + (rightOfThis + leftOfNext) * 0.5f;
        }

        public static float UnderlineWidth(float labelWidth) =>
            labelWidth + UnderlineOverhang;

        // ---- the authored figures, for the build ------------------------------
        //
        // The scene has to be emitted before any text exists to measure, so the
        // builder lays the bar out from SystemMenuTabDef.LabelWidth and the
        // controller corrects it on first open. These overloads are that path,
        // and the only place the authored numbers are read.
        public static IReadOnlyList<float> AuthoredWidths(IReadOnlyList<SystemMenuTabDef> tabs)
        {
            var widths = new float[tabs.Count];
            for (int i = 0; i < tabs.Count; i++) widths[i] = tabs[i].LabelWidth;
            return widths;
        }

        // Thin delegating overloads, so a caller that has definitions does not
        // have to say AuthoredWidths at every site. ONE implementation either
        // way -- these exist for readability, not as a second code path.
        public static float[] TabWidths(IReadOnlyList<SystemMenuTabDef> tabs) =>
            TabWidths(AuthoredWidths(tabs));

        public static float GapFor(IReadOnlyList<SystemMenuTabDef> tabs) =>
            GapFor(AuthoredWidths(tabs));

        public static float[] TabLefts(IReadOnlyList<SystemMenuTabDef> tabs) =>
            TabLefts(AuthoredWidths(tabs));

        public static float[] TabCentresX(IReadOnlyList<SystemMenuTabDef> tabs) =>
            TabCentresX(AuthoredWidths(tabs));

        public static float DividerCentreX(IReadOnlyList<SystemMenuTabDef> tabs, int index) =>
            DividerCentreX(AuthoredWidths(tabs), index);

        public static float StripWidth(IReadOnlyList<SystemMenuTabDef> tabs) =>
            StripWidth(AuthoredWidths(tabs));

        public static bool StripFits(IReadOnlyList<SystemMenuTabDef> tabs) =>
            StripFits(AuthoredWidths(tabs));

        public static float UnderlineWidth(SystemMenuTabDef tab) =>
            UnderlineWidth(tab.LabelWidth);

        // ---- the title lintel ---------------------------------------------------
        //
        // ABOVE the panel, in the 90px of screen the panel does not use. It is
        // not panel chrome: it carries the run's identity and the currency,
        // which belong to the run rather than to whichever tab is open, and
        // putting it inside the panel would have cost the content pane 52px for
        // something that never changes with the tab.
        public const float LintelWidth = PanelWidth;
        public const float LintelHeight = 52f;

        // Screen y 34..86 against a 1080 stage, converted once here rather than
        // at each call site.
        // The reference stage the lintel and the panel are both placed on.
        public const float StageWidth = 1920f;
        public const float StageHeight = 1080f;
        public const float StageHalfHeight = StageHeight * 0.5f;
        public const float LintelTopScreenY = 34f;
        public const float LintelCentreY = StageHalfHeight - LintelTopScreenY - LintelHeight * 0.5f;

        // The gold rule under the lintel, at screen y 86 -- flush against the
        // panel's own top edge at screen y 90.
        public const float GoldRuleScreenY = 86f;
        public const float GoldRuleCentreY = StageHalfHeight - GoldRuleScreenY;

        public const float LintelPadX = 24f;
        public const float CloseButtonSize = 34f;

        // ---- capacity -----------------------------------------------------------
        //
        // ARITHMETIC, not a fixed count, and that is the design's explicit ask.
        // A count-based guard was honest while every box was 210px wide; once
        // boxes are sized to their own labels, "how many fit" is a question
        // about the words, and a localisation that doubles a label's length
        // overflows a bar that still has the same number of tabs.
        public static float StripWidth(IReadOnlyList<float> labelWidths)
        {
            if (labelWidths.Count == 0) return 0f;

            float used = 0f;
            foreach (float w in TabWidths(labelWidths)) used += w;
            return used + MinimumGap * (labelWidths.Count - 1);
        }

        public static bool StripFits(IReadOnlyList<float> labelWidths) =>
            labelWidths.Count <= 1 || StripWidth(labelWidths) <= RowWidth;
    }
}
