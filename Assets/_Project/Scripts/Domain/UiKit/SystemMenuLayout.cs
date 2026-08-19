namespace PrincesPalace.Domain.UiKit
{
    // Where the overarching menu's parts sit, as pure arithmetic.
    //
    // The whole point of this type is that the TAB BAR IS RELATIVE: every tab's
    // position is computed from its index and the count, so adding one to
    // SystemMenuTabs.All moves the rest along on its own. Nothing here is a
    // hand-placed x.
    //
    // It lives in Domain, beside FightSubmenuLayout and for the same reason:
    // the builder places the tabs and the controller has to know where they are,
    // and two hand-mirrored copies of the same constants is the drift that
    // FightSubmenuLayout's own header exists to record.
    public static class SystemMenuLayout
    {
        // The overlay fills the reference stage. A menu that sits over
        // everything has no reason to be smaller than everything.
        public const float PanelWidth = 1600f;
        public const float PanelHeight = 900f;

        public const float BarHeight = 96f;

        // How far the first tab starts from the panel's left edge, and how much
        // clear space the strip must leave at the right.
        public const float BarInsetLeft = 40f;
        public const float BarInsetRight = 40f;

        public const float TabWidth = 210f;
        public const float TabHeight = 56f;
        public const float TabGap = 26f;

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

        // Tab `index` of `count`, measured from the panel centre.
        //
        // Left-aligned rather than distributed across the full width: a strip
        // that spread four tabs edge to edge would re-space every one of them
        // the moment a fifth arrived, which is exactly the kind of silent
        // re-layout that makes a screenshot from last week a lie.
        public static float TabCentreX(int index)
        {
            return -HalfWidth + BarInsetLeft + TabWidth * 0.5f + index * (TabWidth + TabGap);
        }

        // The divider that follows tab `index`, centred in the gap after it.
        public static float DividerCentreX(int index)
        {
            return TabCentreX(index) + TabWidth * 0.5f + TabGap * 0.5f;
        }

        // How wide the whole strip is at this count.
        public static float StripWidth(int count)
        {
            if (count <= 0) return 0f;
            return count * TabWidth + (count - 1) * TabGap;
        }

        // The x the strip's right edge reaches at this count.
        public static float StripRightEdge(int count)
        {
            return -HalfWidth + BarInsetLeft + StripWidth(count);
        }

        // The most tabs the bar can hold before the strip runs into the panel's
        // right edge.
        //
        // Stated as a FUNCTION rather than a constant so it cannot go stale
        // when a width changes, and asserted at build time -- this is the
        // count-versus-footprint failure that AUDIT #6 and #7 both record, and
        // the design brief for this bar is literally "so we can add more
        // options easily", which is the exact motion that trips it.
        public static int MaxTabs()
        {
            float usable = PanelWidth - BarInsetLeft - BarInsetRight;
            return (int)((usable + TabGap) / (TabWidth + TabGap));
        }

        public static bool StripFits(int count) => count <= MaxTabs();
    }
}
