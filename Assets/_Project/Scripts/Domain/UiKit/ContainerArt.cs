namespace PrincesPalace.Domain.UiKit
{
    // Which shape of container/banner art a screen wants. Nominal names only --
    // see ContainerArt's own comment for why the literal aspect a screen must
    // declare against is measured off the PNGs, not 3f/4f or 9f/16f.
    public enum ContainerRatio
    {
        ThreeByFour,
        NineBySixteen,
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

        internal static float Aspect(ContainerKind kind, ContainerRatio ratio)
        {
            switch (kind)
            {
                case ContainerKind.Container:
                    return ratio == ContainerRatio.ThreeByFour ? ContainerAspect3x4 : ContainerAspect9x16;
                default:
                    return ratio == ContainerRatio.ThreeByFour ? BannerAspect3x4 : BannerAspect9x16;
            }
        }

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
        internal static ContentInsetFrac Inset(ContainerKind kind, ContainerRatio ratio)
        {
            switch (kind)
            {
                case ContainerKind.Container:
                    return ratio == ContainerRatio.ThreeByFour
                        ? new ContentInsetFrac(left: 0.065f, right: 0.065f, top: 0.045f, bottom: 0.04f)
                        : new ContentInsetFrac(left: 0.08f, right: 0.08f, top: 0.04f, bottom: 0.04f);
                default:
                    return ratio == ContainerRatio.ThreeByFour
                        ? new ContentInsetFrac(left: 0.075f, right: 0.075f, top: 0.04f, bottom: 0.18f)
                        : new ContentInsetFrac(left: 0.09f, right: 0.09f, top: 0.035f, bottom: 0.15f);
            }
        }

        internal static string Key(ContainerKind kind, ButtonTheme theme, ContainerRatio ratio)
        {
            string stem = kind == ContainerKind.Container ? "container" : "banner_flag";
            return $"UI/Buttons/Processed/{stem}_{ThemeKey(theme)}_{RatioKey(ratio)}.png";
        }

        private static string ThemeKey(ButtonTheme theme) => theme.ToString().ToLowerInvariant();

        private static string RatioKey(ContainerRatio ratio) =>
            ratio == ContainerRatio.ThreeByFour ? "3x4" : "9x16";
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
