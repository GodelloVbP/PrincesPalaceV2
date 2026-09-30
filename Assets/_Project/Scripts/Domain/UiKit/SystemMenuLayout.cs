using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Where the overarching menu's parts sit, as pure arithmetic.
    //
    // The whole point of this type is that the TAB BAR IS RELATIVE: every
    // position is computed from the tab table and the measured label widths,
    // so changing that table moves the rest along on its own. Nothing here is
    // a hand-placed x.
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
        // Every tab's box is arithmetic over its label's measured width, and a
        // width measured at one spacing is a lie at another. The build
        // measures with this value and the emitter draws with it (Tracked
        // over the role's own), so the two cannot part.
        //
        // SystemMenuLabelWidthTests holds the drawn label to the built box.
        public const float TabLabelTracking = 14f;

        // The tab labels' size, in the FunctionalHeading role and all caps
        // (spec section 2). Literal rather than the role's own band: the
        // role's minimum is 34, which no bar of six tabs could carry.
        public const int TabLabelSize = 24;

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

        // ---- the shared pane ground, bare -----------------------------------------
        //
        // PaneContentHalfWidth/HalfHeight are 744/357.78, PanelWidth/
        // ContentHeight against a 3.5%/5.5% side/top inset. They are
        // DECLARED rather than derived from ContainerArt, because there is
        // no art asset to measure them from; if a pane's content ever needs
        // to reach further into its box, change these two numbers and every
        // hosted pane moves together.
        //
        // Every hosted pane's own HalfWidth/HalfHeight reads these instead of
        // Width * 0.5f / Height * 0.5f -- CharacterDossierScreen,
        // RewardTrackScreen, OptionsScreen, PartyScreen, RunScreen and
        // ExitsScreen all lay their content out against these, so a pane's
        // declared frame (Width/Height, still PanelWidth x ContentHeight --
        // SystemMenuPaneTests.EveryHostedPaneIsTheSizeOfTheContentArea pins
        // this) and its usable content region are not the same rect.
        public const float PaneContentHalfWidth = 744f;

        public const float PaneContentHalfHeight = 357.78f;

        // The row the tabs live in, inside the insets.
        public const float RowWidth = PanelWidth - BarInsetLeft - BarInsetRight;   // 1520

        // ---- the bar: two groups, one row -----------------------------------------
        //
        // The game group packs from the left inset with TabGap between boxes;
        // the system group packs flush against the right inset the same way.
        // What is left between the groups is the group gap, and it may not
        // shrink below MinimumGroupGap or the two read as one run of tabs.
        //
        // A box is its label plus this much padding on each side, rounded so
        // the row does not land on half pixels.
        public const float MeasuredPadX = 24f;
        public const float MeasuredRounding = 4f;

        // Between two boxes of one group.
        public const float TabGap = 48f;

        // Between the groups, at the least. The real gap is the leftover.
        public const float MinimumGroupGap = 96f;

        // The underline is the label plus a little, NOT the box: a rule as wide
        // as the box reads as a second divider rather than as a marker for
        // the word above it.
        public const float UnderlineOverhang = 24f;
        public const float UnderlineHeight = 3f;

        // Distance below the bar's centre line to the underline, from the
        // design's panel-space y 81 against a bar spanning panel y 0..96.
        public const float UnderlineOffsetY = -33f;

        // Arithmetic over LABEL WIDTHS, not tab definitions: that is the seam
        // that lets the bar lay itself out around what is actually drawn. The
        // build measures every label once and the controller measures again
        // on each open; both hand the widths to Arrange.
        //
        // Full labels first. If they do not fit at the minimum gaps the whole
        // bar switches to short labels (never a mix, which reads as an
        // accident), and if those do not fit either the build throws: that is
        // an authoring error for this language, and the runtime cannot fix it.
        public static SystemMenuBar Arrange(
            IReadOnlyList<SystemMenuTabDef> tabs,
            IReadOnlyList<float> fullWidths,
            IReadOnlyList<float> shortWidths)
        {
            if (fullWidths.Count != tabs.Count || shortWidths.Count != tabs.Count)
            {
                throw new System.ArgumentException(
                    $"{tabs.Count} tabs but {fullWidths.Count} full and {shortWidths.Count} short label widths.");
            }

            var full = Place(tabs, fullWidths, shortLabels: false);
            if (full.Fits) return full;

            var brief = Place(tabs, shortWidths, shortLabels: true);
            if (brief.Fits) return brief;

            throw new System.InvalidOperationException(
                $"The system menu's {tabs.Count} tabs need {brief.Needed:F0}px even with short labels " +
                $"but the bar row is {RowWidth:F0}px. Shorten a short label, widen the panel, or the bar " +
                "needs to wrap or scroll - it cannot simply be given another entry.");
        }

        private static SystemMenuBar Place(
            IReadOnlyList<SystemMenuTabDef> tabs, IReadOnlyList<float> labelWidths, bool shortLabels)
        {
            int count = tabs.Count;
            var boxes = new float[count];
            var lefts = new float[count];

            // The table lists the game group before the system group, and the
            // packing below relies on it: a game tab behind a system tab would
            // be placed at the wrong end.
            int firstSystem = count;
            for (int i = 0; i < count; i++)
            {
                if (tabs[i].Group == SystemMenuTabGroup.System)
                {
                    if (firstSystem == count) firstSystem = i;
                }
                else if (firstSystem != count)
                {
                    throw new System.InvalidOperationException(
                        $"The system menu's tab {tabs[i].Key} is a game tab listed after a system tab.");
                }

                // System.Math, not Mathf: this assembly deliberately does not
                // reference UnityEngine, which is what keeps the layout
                // arithmetic testable without a player loop. AwayFromZero
                // because "nearest" is what the design says, and Math.Round's
                // default is banker's rounding.
                boxes[i] = (float)System.Math.Round(
                    (labelWidths[i] + MeasuredPadX * 2f) / MeasuredRounding,
                    System.MidpointRounding.AwayFromZero) * MeasuredRounding;
            }

            float x = BarInsetLeft;
            for (int i = 0; i < firstSystem; i++)
            {
                lefts[i] = x;
                x += boxes[i] + TabGap;
            }

            float gameWidth = firstSystem == 0 ? 0f : x - TabGap - BarInsetLeft;

            float systemWidth = 0f;
            for (int i = firstSystem; i < count; i++)
            {
                systemWidth += boxes[i] + (i > firstSystem ? TabGap : 0f);
            }

            float systemLeft = BarInsetLeft + RowWidth - systemWidth;
            x = systemLeft;
            for (int i = firstSystem; i < count; i++)
            {
                lefts[i] = x;
                x += boxes[i] + TabGap;
            }

            bool bothGroups = firstSystem > 0 && firstSystem < count;
            float groupGap = bothGroups ? systemLeft - (BarInsetLeft + gameWidth) : 0f;
            float needed = gameWidth + systemWidth + (bothGroups ? MinimumGroupGap : 0f);

            return new SystemMenuBar(shortLabels, labelWidths, boxes, lefts, groupGap, needed,
                fits: needed <= RowWidth);
        }

        // Whether a hairline stands between tab `slot` and the next one.
        // Dividers separate ADJACENT tabs of one group; the gap between the
        // groups is empty by design, and a hairline in the middle of it read as
        // a tab that had lost its label.
        public static bool DividerFollows(IReadOnlyList<SystemMenuTabDef> tabs, int slot) =>
            slot >= 0 && slot + 1 < tabs.Count && tabs[slot].Group == tabs[slot + 1].Group;

        public static float UnderlineWidth(float labelWidth) =>
            labelWidth + UnderlineOverhang;

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
        // The reference stage the lintel and the panel are both placed on --
        // read from UiFrames rather than restated, so "the frame everything is
        // authored against" has one value.
        public static float StageWidth => UiFrames.Reference.X;
        public static float StageHeight => UiFrames.Reference.Y;
        public static float StageHalfHeight => StageHeight * 0.5f;
        public const float LintelTopScreenY = 34f;
        public static float LintelCentreY => StageHalfHeight - LintelTopScreenY - LintelHeight * 0.5f;

        // The gold rule under the lintel, at screen y 86 -- flush against the
        // panel's own top edge at screen y 90.
        public const float GoldRuleScreenY = 86f;
        public static float GoldRuleCentreY => StageHalfHeight - GoldRuleScreenY;

        public const float LintelPadX = 24f;
        public const float CloseButtonSize = 38f;

        // ---- the mode pill (plan C10) -----------------------------------------------
        //
        // A rimmed plate on the lintel, sized to its words. It is authored at
        // PillMaxWidth so UiAudit sees the widest it can be, and the controller
        // fits it to the measured text on every open (PillWidth). Its left edge
        // is fixed; the lintel's gold and embers start after PillMaxWidth's end.
        public const float PillHeight = 36f;
        public const float PillPadX = 16f;
        public const float PillMaxWidth = 392f;

        // Lintel space, measured from the lintel's own centre.
        public const float PillLeftEdgeX = -100f;

        public static float PillWidth(float textWidth) =>
            System.Math.Min(PillMaxWidth,
                (float)System.Math.Ceiling((textWidth + PillPadX * 2f) / 2f) * 2f);

        public static float PillCentreX(float width) => PillLeftEdgeX + width * 0.5f;
    }

    // One arrangement of the bar: which label set it uses and where every box
    // sits. Immutable; the build and the controller each make one from
    // measured widths.
    public sealed class SystemMenuBar
    {
        // Whether every label is its short form.
        public readonly bool ShortLabels;

        // The label widths this arrangement was made from, full or short. The
        // underline is drawn from these.
        public readonly IReadOnlyList<float> LabelWidths;

        // Box widths, left edges in panel space (0 at the panel's left edge),
        // and the leftover between the groups.
        public readonly IReadOnlyList<float> Widths;
        public readonly IReadOnlyList<float> Lefts;
        public readonly float GroupGap;

        // The row the boxes need at the minimum gaps, and whether it fits.
        public readonly float Needed;
        public readonly bool Fits;

        internal SystemMenuBar(bool shortLabels, IReadOnlyList<float> labelWidths, IReadOnlyList<float> widths,
                               IReadOnlyList<float> lefts, float groupGap, float needed, bool fits)
        {
            ShortLabels = shortLabels;
            LabelWidths = labelWidths;
            Widths = widths;
            Lefts = lefts;
            GroupGap = groupGap;
            Needed = needed;
            Fits = fits;
        }

        public int Count => Widths.Count;

        // Centre x of a box, measured from the PANEL CENTRE, which is the space
        // every node in this project is placed in.
        public float CentreX(int index) =>
            -SystemMenuLayout.HalfWidth + Lefts[index] + Widths[index] * 0.5f;

        // The divider that follows tab `index`, centred in the gap after it.
        // Only meaningful where SystemMenuLayout.DividerFollows says one
        // belongs: across the group gap this is the middle of empty space.
        public float DividerCentreX(int index)
        {
            float rightOfThis = Lefts[index] + Widths[index];
            float leftOfNext = Lefts[index + 1];
            return -SystemMenuLayout.HalfWidth + (rightOfThis + leftOfNext) * 0.5f;
        }
    }

    // What every tab's label measures, in both forms, in SystemMenuTabs.All
    // order. Domain has no font metrics, so the Editor measures with the text
    // the emitter will draw and passes the numbers in.
    public sealed class SystemMenuLabelWidths
    {
        public readonly IReadOnlyList<float> Full;
        public readonly IReadOnlyList<float> Short;

        public SystemMenuLabelWidths(IReadOnlyList<float> full, IReadOnlyList<float> brief)
        {
            Full = full;
            Short = brief;
        }

        // A stand-in for a caller with no font to measure with, an EditMode
        // test or a screen built to look at its layout: 18.3px a character, the
        // spec's own estimate for the tab labels at 24 caps.
        public static SystemMenuLabelWidths Estimated()
        {
            var tabs = SystemMenuTabs.All;
            var full = new float[tabs.Count];
            var brief = new float[tabs.Count];
            for (int i = 0; i < tabs.Count; i++)
            {
                full[i] = Estimate(tabs[i].Label);
                brief[i] = Estimate(tabs[i].ShortLabel);
            }

            return new SystemMenuLabelWidths(full, brief);
        }

        private static float Estimate(UiString label) =>
            (float)System.Math.Round(
                (label.IsTemplated ? label.AuditSample : label.Template).Length * 18.3);
    }
}
