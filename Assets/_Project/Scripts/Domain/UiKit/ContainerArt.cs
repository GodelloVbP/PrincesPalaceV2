using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Which shape of container/banner art a screen wants. The names ARE the
    // ratios now -- this delivery of the kit is at true nominal aspect on
    // every file; see ContainerArt's own comment.
    public enum ContainerRatio
    {
        ThreeByFour,
        NineBySixteen,
        ThreeByTwo,
        TwoByOne,
        FiveByOne,
    }

    // The measured shape of the six-theme container/banner kit
    // (Art/UI/Buttons/Processed/container_<theme>_<ratio>.png and
    // banner_flag_<theme>_<ratio>.png), and the safe interior every one of
    // them shares.
    //
    // THIS DELIVERY IS AT TRUE NOMINAL ASPECT. Every PNG measures
    // (`PIL.Image.size`) to all six themes of every group landing on
    // exactly one size, exactly on the fraction their filename claims:
    //
    //   container_*_3x4:    768x1024  -> 0.75    (all six identical)
    //   container_*_9x16:   576x1024  -> 0.5625  (all six identical)
    //   container_*_3x2:    1536x1024 -> 1.5     (all six identical)
    //   container_*_2x1:    1536x768  -> 2.0     (all six identical)
    //   container_*_5x1:    1530x306  -> 5.0     (all six identical, NEW)
    //   banner_flag_*_3x4:  768x1024  -> 0.75    (all six identical)
    //   banner_flag_*_9x16: 576x1024  -> 0.5625  (all six identical)
    //
    // So the constants below are the clean fractions, not a cluster average,
    // and a screen that declares a nominal box now gets an exact fit.
    //
    // HISTORY, one line, because the screens still carry its fingerprints:
    // every earlier delivery was spliced off sheets and came out several
    // percent off nominal (3x4 measured 0.588, 9x16 0.4083, banner 3x4
    // 0.5145, banner 9x16 0.3004, 3x2 1.49, 2x1 1.98), and every screen was
    // sized against those numbers -- which is why repinning this file moves
    // real layout rather than only a comment.
    internal static class ContainerArt
    {
        // width / height. Exact, for this delivery.
        internal const float ContainerAspect3x4 = 0.75f;
        internal const float ContainerAspect9x16 = 0.5625f;
        internal const float BannerAspect3x4 = 0.75f;
        internal const float BannerAspect9x16 = 0.5625f;
        internal const float ContainerAspect3x2 = 1.5f;
        internal const float ContainerAspect2x1 = 2f;
        internal const float ContainerAspect5x1 = 5f;

        internal static float Aspect(ContainerKind kind, ContainerRatio ratio) => Spec(kind, ratio).Aspect;

        // The band a declared size is allowed to miss the measured aspect by
        // before Container/FlagBanner refuse it outright -- stretching either
        // asset into the wrong ratio is exactly what this kit exists to
        // prevent.
        internal const float AspectTolerance = 0.05f;

        // Content insets, as a FRACTION of the container's own declared
        // width (Left/Right) and height (Top/Bottom) -- fractions rather than
        // pixels because a screen can ask Container() for any size that
        // clears the aspect check, and the painted border scales with it.
        //
        // MEASURED BY measure_inset in tools/splice_ui_kit.py, run directly
        // against the cropped Processed/ PNGs: in from each edge along the
        // mid row/column until the pixel stops looking like the painted
        // border and starts looking like the dark interior. For a BANNER the
        // bottom figure is not the border -- it is the V-notch "shoulder",
        // the first row walking up from the bottom at which the opaque span
        // reaches full width, so a bottom-anchored label cannot sit in the
        // point of the flag. The WORST (largest) raw fraction per side across
        // the six themes takes a safety margin (container ~25%, banner's
        // V-clearance ~20%) and that is what is pinned below.
        //
        // A NOTE ON THE TOOL: measure_inset gates on alpha, not RGB, so it
        // does not mistake the transparent halo's (0,0,0) for the dark
        // panel interior. The figures below are real measurements for every
        // group, including 3x2/2x1.
        //
        // Raw worst-per-side, this delivery:
        //
        //   container_*_3x4    L.0534 T.0410 R.0547 B.0439   (border 34-45px of 768/1024)
        //   container_*_9x16   L.0660 T.0371 R.0660 B.0381   (border 31-39px of 576/1024)
        //   container_*_3x2    L.0124 T.0098 R.0130 B.0215   (hairline: 15-22px of 1536/1024)
        //   container_*_2x1    L.0137 T.0273 R.0156 B.0312   (hairline: 11-24px of 1536/768)
        //   container_*_5x1    L.0052 T.0261 R.0065 B.0327   (hairline: 5-9px of 1530/306)
        //   banner_flag_*_3x4  L.0573 T.0410 R.0573, V-shoulder .1533 (156-157px of 1024)
        //   banner_flag_*_9x16 L.0729 T.0420 R.0729, V-shoulder .1172 (118-120px of 1024)
        //
        // 3x2 AND 2x1 KEEP THE INSETS THEY ALREADY HAD (0.035/0.04/0.045 and
        // 0.035/0.055) rather than dropping to raw*1.25: their frames are
        // hairlines and the existing pins clear them several times over.
        // 2x1's remaining callers are FightScreen's PartyPlate and anything
        // else outside the menu that still wants a 2:1 frame. Already
        // comfortably safe, so leave it: the raw number backing it is a
        // real measurement.
        internal static ContentInsetFrac Inset(ContainerKind kind, ContainerRatio ratio) => Spec(kind, ratio).Inset;

        // THE VISIBLE EDGE, not the rect edge -- every one of these PNGs
        // carries a transparent halo outside its own painted border (a
        // leftover of the sheet-splicing pass, unrelated to Inset above,
        // which is the SAFE INTERIOR the painted border itself leaves).
        // A screen that flushes two rects' bottom edges against each other
        // (FightSubmenuLayout.CommandBottom, the party plate) reads as
        // misaligned even though the rects agree exactly, because the paint
        // stops short of the rect by a different amount on each asset.
        //
        // REUSES ContentInsetFrac rather than a dedicated struct -- same
        // shape (four fractions of the container's own declared width/
        // height), and a second four-float type naming the same thing would
        // only be able to disagree with the first one. "Pad" here reads
        // exactly like "inset": both are "how far in from the declared
        // edge", they just measure two different edges (the border's inner
        // face vs. the halo's outer face).
        //
        // MEASURED BY tools/measure_ui_kit.py AT ALPHA >= 32 (the threshold
        // decided on the main tree -- see the script's own header for why),
        // one fraction per edge, AVERAGED across the six themes -- because
        // this pad exists for FLUSH PLACEMENT, not as a safety margin the way
        // Inset's own "worst raw fraction" choice is (Inset guards against a
        // label reaching the border; this guards against a rect edge reading
        // as "off" beside a neighbour's visible edge).
        //
        // RE-MEASURE: `py tools/measure_ui_kit.py` after any Processed/
        // regeneration and re-paste its "C#-PASTEABLE, threshold 32" block
        // below. It also asserts the six themes agree within 1px per edge
        // and exits 1 naming the file when they do not -- which is what
        // happened for THIS delivery on four edges, noted where they are
        // pinned below, so those four averages are a known approximation
        // rather than a clean six-way agreement.
        internal static ContentInsetFrac VisiblePad(ContainerKind kind, ContainerRatio ratio) => Spec(kind, ratio).VisiblePad;

        internal static string Key(ContainerKind kind, ButtonTheme theme, ContainerRatio ratio)
        {
            string stem = kind == ContainerKind.Container ? "container" : "banner_flag";
            return $"UI/Buttons/Processed/{stem}_{theme.ThemeKey()}_{Spec(kind, ratio).FileSuffix}.png";
        }

        // One statement of "which surfaces exist per (kind, ratio)" -- Aspect,
        // Inset and Key above all read the same table instead of repeating
        // the kind/ratio switch three times. A combination this kit never
        // shipped art for (FlagBanner + ThreeByTwo/TwoByOne/FiveByOne) is
        // simply absent from the dictionary, so the lookup itself is the one
        // throw.
        //
        // VisiblePad, per group -- tools/measure_ui_kit.py output at alpha
        // >= 32, fraction averaged across the six themes. Four callouts, all
        // confirmed against the raw alpha rather than assumed to be threshold
        // artifacts (each row/column ramps 0 -> 255 over about 3px, at a
        // DIFFERENT index per theme):
        //
        //   (Container, ThreeByFour) LEFT: silver's paint starts at column 20
        //   and violet's at 18, where the other four start at 26-27 -- a 9px
        //   spread. This is the same silver/violet splice offset the previous
        //   delivery carried; it was 3-4px then, so the regeneration made it
        //   worse rather than better.
        //   (Container, ThreeByFour) TOP: blue starts at row 28 and crimson
        //   at 26 -- a 2px spread.
        //   (Container, NineBySixteen) LEFT: the same two themes as the 3x4
        //   left edge, 19px against 23-24 -- a 5px spread.
        //   (FlagBanner, ThreeByFour) TOP: crimson and gold start 2px lower
        //   than the other four (29 against 27).
        //
        // Every other edge of every group agrees within 1px, and BOTTOM --
        // the only edge anything is actually placed against today -- is clean
        // on all four of the above. UiKitVisiblePadTests widens its band for
        // exactly those four edges and holds every other one at 1px.
        private static readonly Dictionary<(ContainerKind, ContainerRatio), ContainerSpec> Specs =
            new Dictionary<(ContainerKind, ContainerRatio), ContainerSpec>
        {
            [(ContainerKind.Container, ContainerRatio.ThreeByFour)] = new ContainerSpec(
                ContainerAspect3x4, new ContentInsetFrac(left: 0.069f, right: 0.069f, top: 0.052f, bottom: 0.055f),
                new ContentInsetFrac(left: 0.0315f, right: 0.0349f, top: 0.0264f, bottom: 0.0262f), "3x4"),
            [(ContainerKind.Container, ContainerRatio.NineBySixteen)] = new ContainerSpec(
                ContainerAspect9x16, new ContentInsetFrac(left: 0.083f, right: 0.083f, top: 0.047f, bottom: 0.048f),
                new ContentInsetFrac(left: 0.0379f, right: 0.0411f, top: 0.0229f, bottom: 0.0239f), "9x16"),
            [(ContainerKind.Container, ContainerRatio.ThreeByTwo)] = new ContainerSpec(
                ContainerAspect3x2, new ContentInsetFrac(left: 0.035f, right: 0.035f, top: 0.04f, bottom: 0.045f),
                new ContentInsetFrac(left: 0.0033f, right: 0.0033f, top: 0.0049f, bottom: 0.0049f), "3x2"),
            [(ContainerKind.Container, ContainerRatio.TwoByOne)] = new ContainerSpec(
                ContainerAspect2x1, new ContentInsetFrac(left: 0.035f, right: 0.035f, top: 0.055f, bottom: 0.055f),
                new ContentInsetFrac(left: 0.0029f, right: 0.0031f, top: 0.0056f, bottom: 0.0065f), "2x1"),
            // NEW WITH THIS DELIVERY, and nothing calls it yet -- it is here
            // so the twelve container_*_5x1 PNGs are reachable, keyed and
            // pinned like every other group rather than sitting on disk
            // unreferenced. Insets are the plain raw*1.25: there is no caller
            // to tune them against, and unlike 3x2/2x1 no prior pin to
            // preserve. The frame is a 5-9px hairline on a 1530px bar.
            // Per side, raw*1.25 to three decimals: L .0052 -> 0.007,
            // R .0065 -> 0.008, T .0261 -> 0.033, B .0327 -> 0.041. Left and
            // right were both authored 0.009, which is neither side's number.
            [(ContainerKind.Container, ContainerRatio.FiveByOne)] = new ContainerSpec(
                ContainerAspect5x1, new ContentInsetFrac(left: 0.007f, right: 0.008f, top: 0.033f, bottom: 0.041f),
                new ContentInsetFrac(left: 0.0013f, right: 0.0013f, top: 0.0065f, bottom: 0.0065f), "5x1"),
            [(ContainerKind.FlagBanner, ContainerRatio.ThreeByFour)] = new ContainerSpec(
                BannerAspect3x4, new ContentInsetFrac(left: 0.072f, right: 0.072f, top: 0.052f, bottom: 0.185f),
                new ContentInsetFrac(left: 0.0352f, right: 0.0352f, top: 0.0270f, bottom: 0.0264f), "3x4"),
            [(ContainerKind.FlagBanner, ContainerRatio.NineBySixteen)] = new ContainerSpec(
                BannerAspect9x16, new ContentInsetFrac(left: 0.092f, right: 0.092f, top: 0.053f, bottom: 0.15f),
                new ContentInsetFrac(left: 0.0489f, right: 0.0486f, top: 0.0273f, bottom: 0.0273f), "9x16"),
        };

        private static ContainerSpec Spec(ContainerKind kind, ContainerRatio ratio)
        {
            if (Specs.TryGetValue((kind, ratio), out var spec))
            {
                return spec;
            }

            // FlagBanner never shipped 3x2/2x1/5x1 art -- ThreeByFour and
            // NineBySixteen are the only ratios that resolve to a real
            // banner_flag_ asset, so a caller asking for one of the
            // container-only ratios on a FlagBanner is a mistake to catch
            // here rather than hand back a number that names no PNG.
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio,
                kind == ContainerKind.Container
                    ? "unhandled ContainerRatio"
                    : $"FlagBanner has no {ratio} art -- only ThreeByFour and NineBySixteen ship a banner_flag_ asset.");
        }
    }

    // One (kind, ratio) surface's full spec -- see ContainerArt.Specs for
    // where the numbers come from and why they live in one table.
    internal readonly struct ContainerSpec
    {
        internal readonly float Aspect;
        internal readonly ContentInsetFrac Inset;
        internal readonly ContentInsetFrac VisiblePad;
        internal readonly string FileSuffix;

        internal ContainerSpec(float aspect, ContentInsetFrac inset, ContentInsetFrac visiblePad, string fileSuffix)
        {
            Aspect = aspect;
            Inset = inset;
            VisiblePad = visiblePad;
            FileSuffix = fileSuffix;
        }
    }

    internal enum ContainerKind
    {
        Container,
        FlagBanner,
    }

    // A container/banner's safe interior, as a fraction of its own declared
    // width (Left/Right) and height (Top/Bottom). See ContainerArt.Inset for
    // where the numbers come from.
    public readonly struct ContentInsetFrac
    {
        public readonly float Left;
        public readonly float Right;
        public readonly float Top;
        public readonly float Bottom;

        public ContentInsetFrac(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }
    }
}
