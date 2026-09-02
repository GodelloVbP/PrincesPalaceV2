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
        // 48 and 6, down from 66 and 8.
        //
        // 48 IS THE FLOOR THE ROW'S OWN CONTENTS SET, not a number picked for
        // looking tidy. A row carries a 36px mark, a name box 20 tall centred
        // at +10 and a meta box 20 tall centred at -14, so its contents span
        // -24 to +20 -- 44 -- and the two pixels either side are all the margin
        // there is. Going below 48 means moving the text, which is a different
        // change with different reasons.
        public const float RowHeight = 48f;
        public const float RowGap = 6f;
        public const float RowPitch = RowHeight + RowGap;

        public const float CommandBottom = -486f;

        // ---- BACK IS A ROW NOW, AND IT IS INSIDE THE FRAME -----------------------
        //
        // It used to be a 404-wide button floating BELOW the container, in the
        // style the screen used before the list got a frame around it. Two
        // things were wrong with that and only one of them was visible: it did
        // not line up with the panel above it (the panel is 438 wide and grew
        // to the right to find room for the scrollbar, so the button was inset
        // 10px on the left and 34 on the right), and it was the last thing on
        // the screen still drawn in the old idiom.
        //
        // As a row inside the container it inherits the rows' width and x for
        // free, which is the alignment fix -- there is no second number left to
        // disagree. It reads as the last entry in the list, which is what it is.
        public const float BackRowHeight = RowHeight;

        // FLUSH WITH THE VERB COLUMN'S BOTTOM EDGE, which is what CommandBottom
        // is. ATTACK sits at CommandBottom + 26 and is 52 tall, so its lower
        // edge lands exactly here -- and the panel beside it was floating 36px
        // above that line, which reads as a panel that missed rather than as
        // two columns of one control.
        public const float ContainerBottom = CommandBottom;

        // In the container's bottom padding, so the frame closes tight under it.
        public static float BackRowY => ContainerBottom + ContainerPad + BackRowHeight * 0.5f;

        // The list ends one gap above the back row. This was CommandBottom + 46
        // -- the old external button's top -- and every row in the pool moves up
        // with it, which is the whole visible cost of folding BACK inside.
        public static float RowsBottom => BackRowY + BackRowHeight * 0.5f + RowGap;

        // ---- the container and its scroll ---------------------------------------
        //
        // THE LIST USED TO BE A COLUMN OF LOOSE ROWS FLOATING ON THE
        // BATTLEFIELD, and anything past the eighth was undrawable: there was
        // no ninth rect, so a character with twelve skills simply could not
        // reach four of them. The clamp that hid them was correct -- an
        // unclamped count laid the column out for seventeen rows and pushed
        // every rect that existed off the top of the screen -- but it was
        // hiding content rather than presenting it.
        //
        // The rows sit inside a bounded, scrolling container now. What changes
        // for the player is that a long kit is reachable; what changes for the
        // code is that "how many rows exist" and "how many are on screen" stop
        // being the same number, which they were only ever by accident.

        // How many rows fit in the viewport at a time.
        //
        // EIGHT, down from nine, and the ninth went to the back row. The
        // container's outer footprint is unchanged, so the choice was between
        // taking a row's worth of height off the list or growing the panel
        // downward past the verb column it stands beside. A list that scrolls
        // loses nothing by being one row shorter; a panel that overhangs its
        // neighbour is wrong at every list length.
        public const int RowsInView = 8;

        // How many row rects the tree emits. The list SCROLLS now, so this is
        // no longer "as many as fit" -- it is as many as a character can ever
        // offer, and every one of them has to exist to be scrolled to.
        //
        // 24, up from 16, and the sixteen was caught in a screenshot rather
        // than by the pin that exists to catch it. Adding Frost Flare and
        // Lightning Bolt to Shawn's ladder took a real save to EIGHTEEN skills;
        // the list said "SHOWING 16 OF 18" and two of them had no rect to
        // scroll to.
        //
        // FightCapacityPinTests did not fail, and its model is why: it computes
        // the worst case as levelled skills plus the grants of a SINGLE talent
        // root, on the reading that allegiance makes the roots exclusive. The
        // save in the capture had more than that. Whether that save is
        // reachable in play is a separate question worth asking -- but the pool
        // is a display reservation, and sizing it to the strictest reading of a
        // gameplay rule means any hole in that rule silently eats rows.
        public const int PoolSize = 24;

        public static float ViewportHeight => ColumnHeight(RowsInView);

        // The viewport's bottom edge is the last row's bottom edge, which is
        // RowsBottom exactly -- RowY puts the final row's centre half a row
        // above it. So a list that fits is framed precisely where it already
        // sat, and nothing moves for the common case.
        public static float ViewportCentreY => RowsBottom + ViewportHeight * 0.5f;

        // Padding inside the container, and the strip the scrollbar runs in.
        public const float ContainerPad = 10f;
        public const float ScrollbarWidth = 6f;
        public const float ScrollbarGap = 8f;

        // The thumb never shrinks below this, however long the list gets. A
        // two-pixel thumb is a mark rather than a handle.
        public const float ThumbMinHeight = 28f;

        // Padding, the list, a gap, and the back row.
        public static float ContainerHeight =>
            ContainerPad * 2f + ViewportHeight + RowGap + BackRowHeight;

        // The row's own content width -- ONE COPY, because the art frame below
        // (built in FightScreen.BuildSubmenuFrame) has to size itself from the
        // exact same number the rows are actually built at, not a second 282
        // that could drift from it the way this file's own header warns about.
        public const float RowWidth = 282f;

        public static float ContainerWidth =>
            RowWidth + ContainerPad * 2f + ScrollbarGap + ScrollbarWidth;

        // ---- the Violet 3:4 art frame around the box above ----------------------
        //
        // ContainerWidth/ContainerHeight (316x500, aspect 0.632) are the INNER
        // box every row, the scrollbar and BACK already lay out against,
        // UNCHANGED by wrapping it in themed art -- nothing about RowY,
        // ScrollRange or ThumbHeight above had to move, because the frame is
        // built AROUND this box in BuildSubmenuFrame rather than replacing its
        // arithmetic. 0.632 misses the kit's measured 3:4 aspect (0.588) by
        // 7.5%, past Ui.Container's 5% band, so the frame is WIDER than the
        // inner box by the kit's own measured inset (6.5% a side) -- widened so
        // ContainerWidth is EXACTLY what the frame's own left+right inset
        // leaves once its border is subtracted (316 = FrameWidth * (1 - .13)),
        // which is what keeps the inner box's horizontal centre equal to the
        // frame's own with no separate x correction.
        //
        // Tall enough (617.72) for that wider frame to still hit 0.588 exactly.
        // The inner box's own vertical need (500 of the 565.21 the frame's
        // 4.5%/4% top/bottom inset leaves) uses 88% of it: BuildSubmenuFrame
        // reparents the unchanged viewport/track/thumb/BACK under this inset
        // instead of a bare Panel at the old ContainerCentreY -- and because
        // every one of their own Y's is already authored AS AN OFFSET FROM
        // that centre (ViewportOffsetInContainer, BackRowY - ContainerCentreY),
        // reparenting them under a DIFFERENT centre (FrameContentCentreY,
        // below) recentres the whole 500-tall block inside the taller frame
        // automatically -- no shift added anywhere in the screen.
        public static readonly ContentInsetFrac FrameInset =
            Ui.ContainerContentInset(ContainerRatio.ThreeByFour);

        public static float FrameWidth => ContainerWidth / (1f - FrameInset.Left - FrameInset.Right);

        public static float FrameHeight =>
            Ui.ContainerSizeForWidth(ContainerRatio.ThreeByFour, FrameWidth).Y;

        // BOTTOM-ANCHORED AT THE FRAME'S OWN TRUE EDGE, not its inset -- this
        // is what keeps FightScreenTests.TheSkillPanelEndsOnTheSameLineAsThe
        // VerbColumn true: the frame, not the content inside it, is what has
        // to end on the same line as the verb column.
        public static float FrameCentreY => ContainerBottom + FrameHeight * 0.5f;

        public static float FrameTop => FrameCentreY + FrameHeight * 0.5f;

        // The content inset's OWN centre, which is not the frame's centre once
        // the top and bottom insets differ (4.5% vs 4%) -- half that 0.5% of
        // FrameHeight, about 1.5px, plus all of the frame's own half-height.
        // What BuildSubmenuFrame's viewport/track/thumb/BACK are reparented
        // under.
        public static float FrameContentCentreY =>
            FrameCentreY + FrameHeight * (FrameInset.Bottom - FrameInset.Top) * 0.5f;

        // How far every ABSOLUTE position below RowsBottom moves once the
        // rows/scrollbar/BACK are reparented under FrameContentCentreY instead
        // of the old ContainerCentreY. Nothing that reads its own POSITION
        // relative to its immediate parent (RowYInContent, ContentY, the
        // scroll math) needs this -- Unity's own nesting applies it for free.
        // It exists for the one place that does need it: a test comparing a
        // BUILT tree's absolute row position back against RowY's literal
        // output (FightScreenTests.TheSubmenuRowsComeFromTheSharedLayout
        // Function).
        public static float FrameRecentreShiftY => FrameContentCentreY - ContainerCentreY;

        // NO LONGER CONCENTRIC WITH THE VIEWPORT. It was, while the container
        // held nothing but the list; the back row hangs below the viewport now,
        // so the two centres are a back row and a gap apart. Written from the
        // bottom edge, which is the part that is actually pinned.
        public static float ContainerCentreY => ContainerBottom + ContainerHeight * 0.5f;

        // How far the list can travel. Zero when everything fits, which is also
        // what hides the bar.
        public static float ScrollRange(int count)
        {
            float content = ColumnHeight(VisibleCount(count));
            float over = content - ViewportHeight;

            return over > 0f ? over : 0f;
        }

        // ---- the scrolled content ------------------------------------------------
        //
        // The content rect holds the whole pool at fixed positions and MOVES AS
        // ONE. That is the shape of the rework: the old code re-anchored every
        // row individually on every open, through RowY, because the list's
        // bottom had to stay put as the count changed. One rect carrying
        // sixteen fixed children does the same job with one write, and it is
        // the only construction that can also scroll.

        public static float ContentHeight => ColumnHeight(PoolSize);

        // The pool's centre, in column coordinates. ColumnHeight is measured
        // from the last row's bottom edge, which is RowsBottom exactly.
        public static float ContentCentreY => RowsBottom + ContentHeight * 0.5f;

        // Row i's position INSIDE the content rect. Fixed forever: the pool
        // never re-anchors, the rect it lives in does.
        public static float RowYInContent(int index) =>
            RowY(PoolSize, index) - ContentCentreY;

        // The content's resting place: the pool sitting exactly where RowY
        // says it does, which is a full list scrolled to its bottom.
        //
        // THIS IS WHAT THE TREE BUILDS AT, and it is chosen so that a solved
        // row's ABSOLUTE position still equals RowY(PoolSize, i) -- the
        // build-time picture is the same one it always was, and FightScreenTests
        // can go on checking placement against the shared function rather than
        // against a chain of four nested rects.
        public static float ContentRestY => ContentCentreY - ViewportCentreY;

        // Where the content sits inside the viewport, for a list of `count`
        // rows scrolled by `scroll`.
        //
        // Two corrections on top of the rect's resting place, and they are
        // separate things that happen to add:
        //
        //   BOTTOM-ANCHORING. Rows are at pool positions, so a five-row list
        //   occupies the pool's top five slots and would float. Sliding the
        //   whole rect down by the unused slots puts the last row just above
        //   BACK, which is where every list ends however long it is.
        //
        //   THE SCROLL itself, which only ever moves a list too long to fit.
        public static float ContentY(int count, float scroll)
        {
            int shown = VisibleCount(count);
            float bottomAnchor = -(PoolSize - shown) * RowPitch;

            return ContentCentreY - ViewportCentreY + bottomAnchor + ContentOffsetY(count, scroll);
        }

        // Where the scrolled content sits, given how far down the list the
        // player has scrolled.
        //
        // SCROLL 0 IS THE TOP OF THE LIST and the range is the bottom, which is
        // the opposite way round from how the rows are laid out: RowY anchors
        // them to the BOTTOM, so row 0 is the highest on screen and a list too
        // long to fit overflows upward, off the top.
        //
        // Both are right and the inversion is the price. Bottom-anchoring is
        // what a playtest asked for -- the last row lands just above BACK
        // whether the actor has two skills or nine, instead of the list
        // floating with dead space under it. Opening at the top of the list is
        // what a reader expects, because row 0 is the first skill. So the
        // content is pushed DOWN by the whole range at scroll 0 and sits at its
        // authored position at full scroll.
        public static float ContentOffsetY(int count, float scroll)
        {
            float range = ScrollRange(count);
            if (range <= 0f) return 0f;

            if (scroll < 0f) scroll = 0f;
            if (scroll > range) scroll = range;

            return scroll - range;
        }

        // The thumb's height for `count` rows: the visible fraction of the
        // content, floored so it stays grabbable.
        public static float ThumbHeight(int count)
        {
            float content = ColumnHeight(VisibleCount(count));
            if (content <= ViewportHeight || content <= 0f) return ViewportHeight;

            float height = ViewportHeight * (ViewportHeight / content);
            return height < ThumbMinHeight ? ThumbMinHeight : height;
        }

        // How far the list sits above the container's own middle. Zero until
        // the back row moved into the bottom padding; half a back row and half
        // a gap ever since.
        //
        // Named rather than inlined because THREE things read it -- the
        // viewport, the scroll track and the thumb -- and the thumb's is
        // computed at runtime while the other two are placed at build time.
        // That is precisely the split this whole type exists to keep honest.
        public static float ViewportOffsetInContainer => ViewportCentreY - ContainerCentreY;

        // Where the thumb's centre sits, in CONTAINER coordinates. The track is
        // the viewport's own height, so the thumb reads as the visible window
        // over the whole list.
        public static float ThumbCentreY(int count, float scroll)
        {
            float travel = ViewportHeight - ThumbHeight(count);
            float range = ScrollRange(count);
            if (travel <= 0f || range <= 0f) return ViewportOffsetInContainer;

            float fraction = scroll / range;
            if (fraction < 0f) fraction = 0f;
            if (fraction > 1f) fraction = 1f;

            return ViewportOffsetInContainer + travel * 0.5f - fraction * travel;
        }

        // Turning a grab on the track into a scroll: where the pointer sits
        // along the track, as a fraction, times the range.
        //
        // `fromTop` is 0 at the track's top edge and 1 at its bottom, which is
        // the same direction scroll runs in.
        public static float ScrollAt(int count, float fromTop)
        {
            if (fromTop < 0f) fromTop = 0f;
            if (fromTop > 1f) fromTop = 1f;

            return ScrollRange(count) * fromTop;
        }

        // How many rows can be DRAWN AT ALL, whatever the caller asks for.
        //
        // Clamped here rather than at either call site because the builder and
        // the controller have to agree about it, which is the entire reason
        // this type exists -- v1 kept two hand-mirrored copies of these numbers
        // and they drifted. A count above the pool is not an error worth
        // throwing over: it is ordinary content growth, and the screen's job is
        // to stay legible.
        //
        // IT CLAMPS TO THE POOL, NOT TO THE VIEWPORT, and that is the whole
        // change. It used to be both, because they were the same eight; a
        // request for twelve came back as eight and four skills ceased to
        // exist. Twelve now comes back as twelve, nine of them on screen and
        // three a scroll away.
        public static int VisibleCount(int requested)
        {
            if (requested < 0) return 0;
            return requested > PoolSize ? PoolSize : requested;
        }

        // Row i of a list of `count`, measured from the BOTTOM of the content.
        //
        // Anchoring to the bottom rather than the top is the behaviour a
        // playtest asked for ("menu pops up to the top"): the last row lands on
        // the same slot just above BACK whether the actor has two skills or
        // eight, instead of the list floating with dead space beneath it.
        //
        // MEASURED FROM RowsBottom STILL, so a list that fits sits exactly
        // where it always did -- the container is drawn around the same place
        // the rows were already in. Only a list too long to fit is moved by
        // anything, and then only by the scroll.
        public static float RowY(int count, int index)
        {
            return RowsBottom + (count - 1 - index) * RowPitch + RowHeight * 0.5f;
        }

        // Where the title and the "esc to go back" hint sit: just above the
        // CONTAINER, rather than just above the top row.
        //
        // It used to ride the top row so a short list kept its label attached
        // to it. With a container that is wrong twice over: the header would
        // sit inside the frame for a short list and the frame's own top edge
        // would cut it, and it would move every time the count changed while
        // the box around it did not.
        // MEASURED AGAINST THE FRAME'S OWN TOP EDGE now, not the inner box's --
        // the inner box sits recentred well inside the frame (see FrameHeight's
        // own comment), so the old ContainerCentreY + ContainerHeight * 0.5f
        // would land the header deep inside the painted border instead of
        // above it.
        public static float HeaderY(int count)
        {
            return FrameTop + 16f;
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
