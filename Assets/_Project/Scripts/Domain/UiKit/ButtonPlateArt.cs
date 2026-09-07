using System;

namespace PrincesPalace.Domain.UiKit
{
    // Which shape of button plate a themed button wears. The suffixes ARE the
    // ratios now -- this delivery of the kit is at true nominal aspect on
    // every file; see ButtonPlateArt's own comment, including why Legacy and
    // ThreeByOne have become the same shape.
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
    // THIS DELIVERY IS AT TRUE NOMINAL ASPECT, which is new -- same as
    // ContainerArt. Every PNG was re-measured (`PIL.Image.size`) after the
    // 2026-09-07 regeneration and all six themes of every shape land on
    // exactly one size, exactly on the fraction the filename claims:
    //
    //   button_plate_<theme>.png:       1536x512  -> 3.0  (all six identical)
    //   button_plate_<theme>_3x1.png:   1536x512  -> 3.0  (all six identical)
    //   button_plate_<theme>_5x1.png:   1530x306  -> 5.0  (all six identical)
    //   row_plate_<theme>_6x1.png:      1536x256  -> 6.0  (all six identical)
    //
    // LEGACY AND THREEBYONE ARE NOW THE SAME PICTURE. button_plate_<theme>.png
    // is byte-identical to button_plate_<theme>_3x1.png for all six themes
    // (verified by hash), which is what the regeneration did to a "legacy"
    // plate whose only distinction was being off-nominal at 2.79. Both keys
    // are kept -- the files exist, callers name both, and deleting art is not
    // this change's business -- and both are pinned at 3.0. The consequence
    // to know: ShapeFor's nearest-aspect loop now TIES between them, and
    // Legacy is first in the loop, so any rect nearer 3.0 than to 5.0 or 6.0
    // resolves to Legacy. ThreeByOne is therefore unreachable from ShapeFor
    // and only reachable by an explicit .Plate(ThreeByOne) override -- which
    // costs nothing, because the two now load the same pixels.
    //
    // HISTORY, one line: the previous delivery was spliced off sheets and
    // measured 2.79 / 3.10 / 4.91 / 5.92, and the FiveByOne/Row6x1 pair had
    // already moved once (3.42 -> 4.91, 4.92 -> 5.92) on 2026-09-02. The
    // selection literals below and the rects ButtonPlateArtTests pins against
    // them are what those numbers used to drive.
    internal static class ButtonPlateArt
    {
        // width / height. Exact, for this delivery.
        internal const float LegacyAspect = 3f;
        internal const float ThreeByOneAspect = 3f;
        internal const float FiveByOneAspect = 5f;
        internal const float Row6x1Aspect = 6f;

        // Content inset, worst raw fraction across the six themes, measured
        // by tools/splice_ui_kit.py's measure_inset (which gates on alpha
        // now -- see ContainerArt.Inset's own note on why its older readings
        // were zeros). Button text is centred by layout today, not inset, so
        // nothing consumes these; they are recorded for whoever wires
        // per-shape text padding next:
        //   button_plate_*      raw max: L0.0182 T0.0469 R0.0182 B0.0684
        //   button_plate_*_3x1  raw max: L0.0182 T0.0469 R0.0182 B0.0684  (same art)
        //   button_plate_*_5x1  raw max: L0.0105 T0.0359 R0.0111 B0.0588
        //   row_plate_*_6x1     raw max: L0.0124 T0.0508 R0.0124 B0.0586

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
        // shapes agree within 1px across every theme on every edge (unlike
        // three edges of ContainerArt's groups -- see its own note). Legacy
        // and ThreeByOne read the same numbers because they are the same
        // file.
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
                    return new ContentInsetFrac(left: 0.0034f, right: 0.0033f, top: 0.0101f, bottom: 0.0098f);
                case ButtonPlateShape.FiveByOne:
                    return new ContentInsetFrac(left: 0.0020f, right: 0.0019f, top: 0.0093f, bottom: 0.0098f);
                case ButtonPlateShape.Row6x1:
                    return new ContentInsetFrac(left: 0.0020f, right: 0.0020f, top: 0.0117f, bottom: 0.0117f);
                default:
                    return new ContentInsetFrac(left: 0.0034f, right: 0.0033f, top: 0.0101f, bottom: 0.0098f);
            }
        }

        // Selection rule: the shape whose measured aspect is nearest the
        // declared rect's aspect, by RATIO distance (min |ln(rect/plate)|)
        // rather than absolute difference -- a button that misses every
        // shape's aspect by "the same" number of aspect-units is not missing
        // them by the same amount visually; a stretch from 3.0 to 5.0 (a
        // ratio of 1.67) reads far less than the same 2.0 gap would between
        // two shapes that were themselves closer to 1, and ratio distance is
        // symmetric under swapping which one is "wider" where a plain
        // subtraction is not.
        //
        // LEGACY WINS THE 3.0 TIE. Legacy and ThreeByOne now share an aspect
        // (they share a file), and Legacy is first in the loop below, so
        // `distance < bestDistance` keeps it. That is deliberate rather than
        // incidental: the two resolve to identical pixels, so which one is
        // named only shows up in the sprite KEY, and Legacy's key is the one
        // that has always been the fallback. Pinned by
        // ButtonPlateArtTests.
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
