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

        // The size of the row POOL. Rows beyond `count` are deactivated rather
        // than destroyed.
        //
        // This used to be described as "the maximum a character can ever show",
        // and that was simply false -- a level 4 Shawn offers 17 skill rows.
        // Nothing clamped, so RowY was handed 17, sized the column for 17 rows,
        // and put the eight rects that exist at y 777..259: off the top of the
        // screen, over the bark banner, while BACK stayed pinned at -464. The
        // menu was torn in half down the screen and the top row was cut off by
        // the canvas edge.
        public const int MaxRows = 8;

        // How many rows can actually be DRAWN, whatever the caller asks for.
        //
        // Clamped here rather than at either call site because the builder and
        // the controller have to agree about it, which is the entire reason
        // this type exists -- v1 kept two hand-mirrored copies of these numbers
        // and they drifted. A count above the pool is not an error worth
        // throwing over: it is ordinary content growth, and the screen's job is
        // to stay legible.
        //
        // NOTE this makes rows past the eighth undrawable. They already were --
        // there is no rect for a ninth row, so nothing beyond the pool has ever
        // been reachable - so this loses no capability and fixes the placement.
        // Making them reachable needs paging, which Domain/UiKit/Paging.cs
        // already provides for three other lists. Recorded rather than smuggled
        // in here.
        public static int VisibleCount(int requested)
        {
            if (requested < 0) return 0;
            return requested > MaxRows ? MaxRows : requested;
        }

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

        // Where the title and the "esc to go back" hint sit: just above the top
        // row of a list of `count`.
        //
        // Derived rather than fixed for the same reason RowY is. The builder
        // reserves the full pool, so a header pinned at the 8-row height sat
        // 273px above a 5-row list -- the label "S K I L L S" floating in the
        // middle of the battlefield with nothing under it, which is what a
        // header detached from its own list looks like.
        public static float HeaderY(int count)
        {
            return RowsBottom + VisibleCount(count) * RowPitch + 10f;
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
