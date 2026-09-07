using System;

namespace PrincesPalace.Domain.UiKit
{
    // Which shape of button plate a themed button wears. Nominal names only --
    // see ButtonPlateArt's own comment for why the literal aspect a selection
    // is made against is measured off the PNGs, not a clean 3:1/5:1 fraction.
    public enum ButtonPlateShape
    {
        Legacy,
        ThreeByOne,
        FiveByOne,
        Row6x1,
    }

    // The measured shape of the button-plate kit
    // (Art/UI/Buttons/Processed/button_plate_<theme>[_3x1|_5x1].png and
    // row_plate_<theme>_6x1.png).
    //
    // NOMINAL SUFFIXES, MEASURED ASPECT -- same discipline ContainerArt
    // established. "_3x1" and "_5x1" are the kit's own filenames, not a
    // literal 3.0 or 5.0; every delivered PNG was measured (`PIL.Image.size`)
    // and none of the four shapes lands on its filename number:
    //
    //   button_plate_<theme>.png:       482x171 .. 483x174, aspect 2.770-2.819
    //   button_plate_<theme>_3x1.png:   459x148 .. 461x149, aspect 3.080-3.115
    //   button_plate_<theme>_5x1.png:   953x194 .. 954x194, aspect 4.912-4.918
    //   row_plate_<theme>_6x1.png:      856x144 .. 856x145, aspect 5.903-5.944
    //
    // The 5x1 and 6x1 pair above are the RESPLICED numbers (2026-09-02): the
    // user regenerated those two source sheets (plus the 2x1 container) at
    // closer-to-true ratios, and tools/splice_ui_kit.py was re-run against
    // buttons_51.png/rows_61.png in place of the original button_51.png/
    // row_61.png -- same Processed/ filenames, so no caller changes, but the
    // FiveByOne aspect jumped from 3.42 to ~4.92 and Row6x1 from 4.92 to
    // ~5.92. The two shapes stay well separated (0.4 ln-distance apart) so
    // the ratio-distance selection rule below still discriminates cleanly
    // between them; button_31/container_32 (3x1 button, 3x2 container) were
    // not touched by this regeneration and keep their prior numbers.
    //
    // Each cluster agrees within ~2% of itself, so ONE canonical aspect per
    // shape (the cluster average, rounded to the precision the selection rule
    // needs) is what SelectionFor/Aspect below hand out.
    internal static class ButtonPlateArt
    {
        // width / height, averaged across the six themes' delivered PNGs.
        internal const float LegacyAspect = 2.79f;
        internal const float ThreeByOneAspect = 3.10f;
        internal const float FiveByOneAspect = 4.91f;
        internal const float Row6x1Aspect = 5.92f;

        // Content inset, worst raw fraction across the six themes measured
        // by tools/splice_ui_kit.py's measure_inset against the resliced
        // 5x1/6x1 PNGs (button text is centred by layout today, not inset,
        // so nothing consumes these -- recorded for whoever wires per-shape
        // text padding next):
        //   button_plate_*_5x1 raw max: L0.008 T0.021 R0.008 B0.047
        //   row_plate_*_6x1   raw max: L0.012 T0.057 R0.013 B0.043

        internal static float Aspect(ButtonPlateShape shape)
        {
            switch (shape)
            {
                case ButtonPlateShape.ThreeByOne: return ThreeByOneAspect;
                case ButtonPlateShape.FiveByOne: return FiveByOneAspect;
                case ButtonPlateShape.Row6x1: return Row6x1Aspect;
                default: return LegacyAspect;
            }
        }

        // THE VISIBLE EDGE, not the rect edge -- see ContainerArt.VisiblePad's
        // own header for why this exists and reuses ContentInsetFrac's shape
        // rather than a dedicated type. Measured by tools/measure_ui_kit.py
        // at alpha >= 32, fraction averaged across the six themes; all four
        // shapes agreed within 1px across every theme on every edge (unlike
        // two of ContainerArt's groups -- see its own note).
        //
        // Row6x1.Bottom is the one this phase actually reads:
        // FightSubmenuLayout.VisibleBottomLine is built from the verb row's
        // own visible bottom, and the verb row (300x52) resolves to Row6x1
        // via ShapeFor below.
        //
        // RE-MEASURE: `py tools/measure_ui_kit.py`, re-paste its
        // "C#-PASTEABLE, threshold 32" block.
        internal static ContentInsetFrac VisiblePad(ButtonPlateShape shape)
        {
            switch (shape)
            {
                case ButtonPlateShape.ThreeByOne:
                    return new ContentInsetFrac(left: 0.0043f, right: 0.0043f, top: 0.0135f, bottom: 0.0135f);
                case ButtonPlateShape.FiveByOne:
                    return new ContentInsetFrac(left: 0.0021f, right: 0.0021f, top: 0.0103f, bottom: 0.0103f);
                case ButtonPlateShape.Row6x1:
                    return new ContentInsetFrac(left: 0.0023f, right: 0.0023f, top: 0.0138f, bottom: 0.0138f);
                default:
                    return new ContentInsetFrac(left: 0.0263f, right: 0.0256f, top: 0.0694f, bottom: 0.0703f);
            }
        }

        // Selection rule: the shape whose measured aspect is nearest the
        // declared rect's aspect, by RATIO distance (min |ln(rect/plate)|)
        // rather than absolute difference -- a button that misses every
        // shape's aspect by "the same" number of aspect-units is not missing
        // them by the same amount visually; a stretch from 2.79 to 3.10 (a
        // ratio of 1.11) reads far less than the same 0.31 gap would between
        // two shapes that were themselves closer to 1, and ratio distance is
        // symmetric under swapping which one is "wider" where a plain
        // subtraction is not.
        internal static ButtonPlateShape ShapeFor(float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                throw new ArgumentException($"ButtonPlateArt.ShapeFor needs a positive rect; got {width}x{height}.");
            }

            float rectAspect = width / height;
            var best = ButtonPlateShape.Legacy;
            float bestDistance = float.MaxValue;

            foreach (ButtonPlateShape shape in new[]
                     {
                         ButtonPlateShape.Legacy, ButtonPlateShape.ThreeByOne,
                         ButtonPlateShape.FiveByOne, ButtonPlateShape.Row6x1,
                     })
            {
                float distance = Math.Abs((float)Math.Log(rectAspect / Aspect(shape)));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = shape;
                }
            }

            return best;
        }

        internal static string Key(ButtonTheme theme, ButtonPlateShape shape)
        {
            string t = theme.ThemeKey();
            switch (shape)
            {
                case ButtonPlateShape.ThreeByOne: return $"UI/Buttons/Processed/button_plate_{t}_3x1.png";
                case ButtonPlateShape.FiveByOne: return $"UI/Buttons/Processed/button_plate_{t}_5x1.png";
                // Row plate is its own asset family (row_plate_, not
                // button_plate_) -- a wide verb/menu row, not a scaled-up
                // button, per the art brief that delivered it.
                case ButtonPlateShape.Row6x1: return $"UI/Buttons/Processed/row_plate_{t}_6x1.png";
                default: return $"UI/Buttons/Processed/button_plate_{t}.png";
            }
        }
    }
}
