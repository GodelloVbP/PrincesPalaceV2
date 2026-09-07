using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Which shape of container/banner art a screen wants. Nominal names only --
    // see ContainerArt's own comment for why the literal aspect a screen must
    // declare against is measured off the PNGs, not 3f/4f or 9f/16f.
    public enum ContainerRatio
    {
        ThreeByFour,
        NineBySixteen,
        ThreeByTwo,
        TwoByOne,
    }

    // The measured shape of the six-theme container/banner kit
    // (Art/UI/Buttons/Processed/container_<theme>_<ratio>.png and
    // banner_flag_<theme>_<ratio>.png), and the safe interior every one of
    // them shares.
    //
    // NOMINAL RATIOS, MEASURED ASPECT. "3x4" and "9x16" are the kit's own
    // filenames, not a literal 0.75 or 0.5625 -- every delivered PNG was
    // measured (`PIL.Image.size`) and none of the six themes lands there:
    //
    //   container_*_3x4:    334x569 .. 338x569, aspect 0.583-0.594
    //   container_*_9x16:   284x699 .. 287x699, aspect 0.406-0.411
    //   banner_flag_*_3x4:  330x640 .. 330x642, aspect 0.514-0.516
    //   banner_flag_*_9x16: 242x802 .. 243x810, aspect 0.299-0.301
    //
    // Each cluster agrees within ~2% of itself, so ONE canonical aspect per
    // (kind, ratio) -- the average below -- covers every theme comfortably
    // inside the 5% refusal band Container/FlagBanner enforce, without
    // pretending the art matches a clean fraction it does not.
    internal static class ContainerArt
    {
        // width / height, averaged across the six themes' delivered PNGs.
        internal const float ContainerAspect3x4 = 0.588f;
        internal const float ContainerAspect9x16 = 0.4083f;
        internal const float BannerAspect3x4 = 0.5145f;
        internal const float BannerAspect9x16 = 0.3004f;

        // The transparent-grid kit's second delivery -- container_<theme>_
        // 3x2.png and _2x1.png, container-only (no banner_flag_ equivalent
        // shipped for these two).
        //
        // container_*_3x2 was RESPLICED 2026-09-02: the user regenerated
        // container_32.png in place at a true-to-nominal 3:2 ratio (the
        // filename didn't change, only the pixels -- tools/splice_ui_kit.py
        // re-ran against it and overwrote the same Processed/ filenames, so
        // every .meta GUID stayed valid). Old measurement was 464x341 ..
        // 466x344, aspect 1.349-1.367; resliced:
        //
        //   container_*_3x2: 504x338 .. 505x338, aspect 1.491-1.494
        //
        // container_*_2x1 was RESPLICED 2026-09-02 (same day, earlier): the
        // user regenerated container_21.png (plus the two button sheets
        // above) at a closer-to-true 2:1 ratio, and tools/splice_ui_kit.py
        // re-ran against the new containers_21.png in place of the original
        // -- same Processed/ filenames, so callers are unaffected, but the
        // measured aspect moved from 1.718-1.759 to a noticeably tighter
        // cluster:
        //
        //   container_*_2x1: 530x269 .. 536x270, aspect 1.963-1.993
        //
        // Each cluster agrees within ~2% of itself.
        internal const float ContainerAspect3x2 = 1.49f;
        internal const float ContainerAspect2x1 = 1.98f;

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
        // Measured by scanning each PNG's alpha/colour in python (see the
        // commit message for the exact script): for a container, the inner
        // edge of the painted border on all four sides; for a banner, the
        // same border on left/top/right, but the BOTTOM figure is not the
        // border -- it is measured up to the row where the V-notch first
        // narrows the opaque span (the "shoulder"), so a bottom-anchored
        // label cannot sit in the point of the flag. Both numbers carry a
        // safety margin over the raw measurement (container ~25%, banner's
        // V-clearance ~20%) so the six themes' small per-asset variance can
        // never eat into it.
        //
        //   container_gold_3x4:  334x569, border L17 T17 R18 B15px  -> raw frac  L.051 T.030 R.054 B.026
        //   container_gold_9x16: 285x699, border L18 T18 R19 B19px  -> raw frac  L.063 T.026 R.067 B.027
        //   banner_flag_gold_3x4:  330x641, border L19 T18 R18px, V-shoulder 86px from bottom -> raw frac L.058 T.028 R.055 B.134
        //   banner_flag_gold_9x16: 242x810, border L17 T18 R17px, V-shoulder 72px from bottom -> raw frac L.070 T.022 R.070 B.111
        //
        // 3x2/2x1 measured the same way (measure_inset in tools/
        // splice_ui_kit.py, run directly against the already-cropped PNGs
        // rather than a sheet's grid box), across all six themes -- the
        // WORST (largest) raw fraction per side, then the same margin
        // discipline as the pair above:
        //
        // container_*_3x2's raw max was re-measured against the resliced
        // art (see ContainerAspect3x2's comment above): L.008 T.003 R.008
        // B.015 -- smaller than the original .015/.018/.015/.021 the pinned
        // insets below were sized against, so those insets (kept as-is)
        // stay comfortably safe rather than needing to shrink.
        //
        // container_*_2x1's raw max was re-measured against the resliced
        // art (see ContainerAspect2x1's comment above): L.010 T.019 R.011
        // B.023 -- smaller than the original .017/.030/.017/.030 the pinned
        // insets below were sized against, so those insets (kept as-is)
        // stay comfortably safe rather than needing to shrink.
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
        // one fraction per edge, AVERAGED across the six themes -- same
        // discipline ContainerArt.Aspect already uses (a canonical number
        // per (kind, ratio), not a per-theme table), because this pad exists
        // for FLUSH PLACEMENT, not as a safety margin the way Inset's own
        // "worst raw fraction" choice is (Inset guards against a label
        // reaching the border; this guards against a rect edge reading as
        // "off" beside a neighbour's visible edge).
        //
        // RE-MEASURE: `py tools/measure_ui_kit.py` after any Processed/
        // regeneration and re-paste its "C#-PASTEABLE, threshold 32" block
        // below. It also asserts the six themes agree within 1px per edge
        // and exits 1 naming the file when they do not -- which is exactly
        // what happened for THIS delivery on two groups, noted where they
        // are pinned below, so the averages there are a known approximation
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
        // shipped art for (FlagBanner + ThreeByTwo/TwoByOne) is simply absent
        // from the dictionary, so the lookup itself is the one throw.
        // VisiblePad, per group -- tools/measure_ui_kit.py output at alpha
        // >= 32, fraction averaged across the six themes. Two callouts:
        //
        //   (Container, ThreeByFour) and (Container, NineBySixteen) FAILED
        //   the script's own six-theme agreement check on their LEFT edge --
        //   silver/violet's cells were spliced with 3-4px less left padding
        //   than the other four themes (confirmed on the raw alpha, not a
        //   threshold artifact: the column jumps 0 -> 255 at col 8-9 for
        //   those two PNGs and at col 12 for the rest). Every OTHER edge of
        //   both groups agrees within 1px, and BOTTOM -- the only edge B1
        //   actually places anything against -- is exact. Left/Top/Right
        //   have no reader yet; the averages below are the honest number for
        //   when one arrives, not a claim the six themes agree on it.
        private static readonly Dictionary<(ContainerKind, ContainerRatio), ContainerSpec> Specs =
            new Dictionary<(ContainerKind, ContainerRatio), ContainerSpec>
        {
            [(ContainerKind.Container, ContainerRatio.ThreeByFour)] = new ContainerSpec(
                ContainerAspect3x4, new ContentInsetFrac(left: 0.065f, right: 0.065f, top: 0.045f, bottom: 0.04f),
                new ContentInsetFrac(left: 0.0324f, right: 0.0359f, top: 0.0211f, bottom: 0.0211f), "3x4"),
            [(ContainerKind.Container, ContainerRatio.NineBySixteen)] = new ContainerSpec(
                ContainerAspect9x16, new ContentInsetFrac(left: 0.08f, right: 0.08f, top: 0.04f, bottom: 0.04f),
                new ContentInsetFrac(left: 0.0397f, right: 0.0421f, top: 0.0172f, bottom: 0.0174f), "9x16"),
            [(ContainerKind.Container, ContainerRatio.ThreeByTwo)] = new ContainerSpec(
                ContainerAspect3x2, new ContentInsetFrac(left: 0.035f, right: 0.035f, top: 0.04f, bottom: 0.045f),
                new ContentInsetFrac(left: 0.0040f, right: 0.0040f, top: 0.0059f, bottom: 0.0059f), "3x2"),
            [(ContainerKind.Container, ContainerRatio.TwoByOne)] = new ContainerSpec(
                ContainerAspect2x1, new ContentInsetFrac(left: 0.035f, right: 0.035f, top: 0.055f, bottom: 0.055f),
                new ContentInsetFrac(left: 0.0037f, right: 0.0037f, top: 0.0074f, bottom: 0.0074f), "2x1"),
            [(ContainerKind.FlagBanner, ContainerRatio.ThreeByFour)] = new ContainerSpec(
                BannerAspect3x4, new ContentInsetFrac(left: 0.075f, right: 0.075f, top: 0.04f, bottom: 0.18f),
                new ContentInsetFrac(left: 0.0364f, right: 0.0364f, top: 0.0190f, bottom: 0.0187f), "3x4"),
            [(ContainerKind.FlagBanner, ContainerRatio.NineBySixteen)] = new ContainerSpec(
                BannerAspect9x16, new ContentInsetFrac(left: 0.09f, right: 0.09f, top: 0.035f, bottom: 0.15f),
                new ContentInsetFrac(left: 0.0496f, right: 0.0496f, top: 0.0149f, bottom: 0.0149f), "9x16"),
        };

        private static ContainerSpec Spec(ContainerKind kind, ContainerRatio ratio)
        {
            if (Specs.TryGetValue((kind, ratio), out var spec))
            {
                return spec;
            }

            // FlagBanner never shipped 3x2/2x1 art -- ThreeByFour/
            // NineBySixteen are the only ratios that resolve to a real
            // banner_flag_ asset, so a caller asking for one of the new
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
