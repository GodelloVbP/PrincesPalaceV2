namespace PrincesPalace.Domain.UiKit
{
    // Where the combat submenu's rows sit, as pure arithmetic.
    //
    // This exists to kill the one RUNTIME instance of the count-vs-footprint
    // failure. v1 built these rows at a fixed 8-slot reservation in the Editor
    // and then RE-ANCHORED them at runtime from a second copy of the same
    // constants, living in Core because -- in that file's own words --
    // "Core cannot see Editor-only constants". Two hand-mirrored copies of
    // 66 / +8 / -486+46, and no build-time guard could see the runtime one.
    //
    // Now there is one copy, in Domain, that both the builder and the
    // controller call. The numbers are v1's exactly, so ported screens land
    // pixel-identically.
    public static class FightSubmenuLayout
    {
        public const float RowHeight = 66f;
        public const float RowGap = 8f;
        public const float RowPitch = RowHeight + RowGap;

        // The command column's bottom edge, lifted by the BACK row's height.
        public const float CommandBottom = -486f;
        public const float RowsBottom = CommandBottom + 46f;

        // The maximum a character can ever show; the pool is built at this size
        // and rows beyond `count` are deactivated rather than destroyed.
        public const int MaxRows = 8;

        // Row i of a list of `count`, measured from the BOTTOM.
        //
        // Anchoring to the bottom rather than the top is the behaviour a
        // playtest asked for ("menu pops up to the top"): the last visible row
        // lands on the same slot just above BACK whether the actor has two
        // skills or eight, instead of the list floating with dead space beneath
        // it.
        public static float RowY(int count, int index)
        {
            return RowsBottom + (count - 1 - index) * RowPitch + RowHeight * 0.5f;
        }

        // Total vertical extent a list of `count` rows occupies. The builder
        // sizes the reservation from this, so the two can no longer disagree.
        public static float ColumnHeight(int count)
        {
            if (count <= 0) return 0f;
            return count * RowHeight + (count - 1) * RowGap;
        }
    }
}
