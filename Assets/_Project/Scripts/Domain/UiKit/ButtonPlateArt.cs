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
    //   button_plate_<theme>_5x1.png:   676x197 .. 676x198, aspect 3.414-3.432
    //   row_plate_<theme>_6x1.png:      713x145 across all six themes, aspect 4.9172
    //
    // Each cluster agrees within ~2% of itself, so ONE canonical aspect per
    // shape (the cluster average, rounded to the precision the selection rule
    // needs) is what SelectionFor/Aspect below hand out.
    internal static class ButtonPlateArt
    {
        // width / height, averaged across the six themes' delivered PNGs.
        internal const float LegacyAspect = 2.79f;
        internal const float ThreeByOneAspect = 3.10f;
        internal const float FiveByOneAspect = 3.42f;
        internal const float Row6x1Aspect = 4.92f;

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
            string t = ThemeKey(theme);
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

        private static string ThemeKey(ButtonTheme theme) => theme.ToString().ToLowerInvariant();
    }
}
