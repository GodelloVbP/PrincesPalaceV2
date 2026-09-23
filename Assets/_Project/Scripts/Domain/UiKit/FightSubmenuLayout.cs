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
        // NOW EQUAL TO VerbRowH (owner playtest, 2026-09-23): "the skill
        // buttons are narrower/shorter with thinner borders than the main
        // command buttons beside them -- make the submenu buttons match the
        // command buttons' size and border." A submenu row used to be sized
        // off its own text content (40, then 48, then 66) and picked the
        // same Row6x1 plate as the verb row by aspect-nearest coincidence,
        // not by construction -- close enough that nobody noticed the two
        // rows were never actually the same size until they were compared
        // side by side. Reading VerbRowH directly (rather than restating 52
        // and hoping it stays in sync) is what makes "the same size" a fact
        // the compiler can check instead of a resemblance two authors have
        // to maintain by hand.
        public const float RowHeight = VerbRowH;

        // ONE SOURCE FOR THE PITCH, not a second hand-tuned gap. This used to
        // be a bare `6f` here while the verb column carried its own separate
        // `VerbPitch = 62f` in FightScreen -- RowHeight already read VerbRowH
        // so the two columns' ROW HEIGHTS matched, but nothing made their ROW
        // SPACING match, and 52+6=58 drifted a steady 4px/row short of the
        // verb column's 62. That is the whole shape of the playtest bug this
        // closes ("submenu rows drift upward against the command rows"): row
        // 0 lined up, row 3 (BACK) was 12px off, and it read as the panel
        // sitting too high rather than as an arithmetic mismatch. RowPitch
        // now reads VerbPitch directly -- the same fix RowHeight already
        // applied to VerbRowH, for the same reason -- and RowGap is DERIVED
        // from it rather than being the independent number that drifted.
        public const float VerbPitch = 62f;
        public const float RowPitch = VerbPitch;
        public const float RowGap = RowPitch - RowHeight;

        public const float CommandBottom = -486f;

        // The verb column's own row size -- moved here from FightScreen so
        // VisibleBottomLine below and FightScreen's own verb rows read the
        // SAME 300x52, the same reason RowWidth lives here rather than being
        // restated at the submenu frame's build site.
        public const float VerbRowW = 300f;
        public const float VerbRowH = 52f;

        // FLUSH WITH THE VERB COLUMN'S VISIBLE BOTTOM EDGE -- not
        // CommandBottom itself. CommandBottom is the Attack row's RECT
        // bottom; every button-plate/container PNG in the kit carries a
        // transparent halo outside its own painted border (tools/
        // measure_ui_kit.py measures it), so the paint actually stops a few
        // pixels short of the rect on every side. Two rects with equal
        // bottoms therefore show two DIFFERENT painted edges, which is
        // exactly the "rect-flush reads as misaligned" defect this line
        // exists to close: the frame and the party plate are placed (via
        // Ui.CentreYForVisibleBottom) so their own visible bottoms land HERE
        // instead of on CommandBottom directly.
        //
        // ButtonPlateArt.ShapeFor(VerbRowW, VerbRowH), not a guessed shape --
        // the verb row is 300x52, which is what the emitter's own
        // ThemedPlate()/Plate() selection actually resolves to (Row6x1, per
        // ButtonPlateArtTests.VerbRow_300x52_PicksRow6x1); asking for that
        // shape's own pad is what keeps this in sync if the row ever changes
        // size and picks a different plate.
        public static float VisibleBottomLine =>
            CommandBottom + VerbRowH * Ui.PlateVisiblePad(Ui.PlateShapeFor(VerbRowW, VerbRowH)).Bottom;

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

        // How many rows fit in the viewport AT MOST -- the CAP a list grows
        // to before it scrolls, not a fixed reservation every list pays for
        // whether it needs it or not.
        //
        // FIVE, down from eight (owner's ask, 2026-09-22): the container's
        // own visible height now follows the actual row count up to this
        // many rows (see VisibleRows/ViewportHeightFor and
        // FightController.AnchorSubmenuRows' own header for how), so a
        // three-skill character's box is three rows tall rather than
        // reserving five empty slots' worth of dead frame under it. This
        // constant is still what the TREE is built at (the widest the box
        // is ever allowed to get) -- FightScreen.BuildSubmenuColumn sizes
        // the static frame from it, and the runtime resize can only shrink
        // toward a shorter list, never grow past what was built.
        public const int RowsInView = 5;

        // How many of `count` rows the viewport actually shows before it
        // must scroll for the rest -- VisibleCount above (pool-clamped)
        // answers "how many row RECTS exist to scroll to"; this answers "how
        // tall does the window get", which is capped far sooner. Floored at
        // 1 so an empty list still reserves one row's worth of frame rather
        // than collapsing to nothing.
        public static int VisibleRows(int count)
        {
            if (count < 1) return 1;
            return count > RowsInView ? RowsInView : count;
        }

        // WHAT THE STATIC TREE IS ACTUALLY BUILT AT, and it is NOT RowsInView
        // -- kept apart on purpose (2026-09-22). Originally this had to stay
        // eight because FrameWidth was DERIVED from this height through the
        // kit's fixed 3:4 container art, and a 5-row-tall frame's 3:4-matched
        // width came out narrower than the fixed-width rows inside it
        // (TheFightScreenAuditsCleanAtEveryFrame). THAT REASON IS GONE since
        // the 2026-09-23 flat-fill rework (FrameWidth's own header): width is
        // simply ContainerWidth now, at any height. Eight stays anyway,
        // rather than collapsing this into RowsInView, because nothing
        // forces it back down and a static tree one row taller than the
        // common case is free width-wise headroom for whatever the pool
        // grows to next, not a cost worth spending a change on today. The
        // static tree is never what the player actually sees -- FightController.
        // AnchorSubmenuRows shrinks the real frame down to RowsInView's own
        // cap the moment any real actor's skill count is known, before a
        // frame is ever drawn to the screen.
        private const int BuildReservationRows = 8;

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

        // BuildReservationRows, NOT RowsInView -- see that constant's own
        // header for why the static tree's own reservation has to stay
        // bigger than the runtime cap.
        public static float ViewportHeight => ColumnHeight(BuildReservationRows);

        // The viewport's bottom edge is the last row's bottom edge, which is
        // RowsBottom exactly -- RowY puts the final row's centre half a row
        // above it. So a list that fits is framed precisely where it already
        // sat, and nothing moves for the common case.
        public static float ViewportCentreY => RowsBottom + ViewportHeight * 0.5f;

        // The viewport's own height for a list of `count` rows, capped at
        // RowsInView -- what makes the container GROW TO FIT rather than
        // reserve a fixed footprint. FightController.AnchorSubmenuRows reads
        // this (and everything below derived from it) every time the
        // submenu opens or its branch/count changes, and resizes the actual
        // frame/viewport RectTransforms to match -- see that method's own
        // header for why only height moves and width never does.
        public static float ViewportHeightFor(int count) => ColumnHeight(VisibleRows(count));

        // Same bottom-anchoring RowsBottom already describes, just for
        // whatever height ViewportHeightFor(count) comes out to.
        public static float ViewportCentreYFor(int count) => RowsBottom + ViewportHeightFor(count) * 0.5f;

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

        public static float ContainerHeightFor(int count) =>
            ContainerPad * 2f + ViewportHeightFor(count) + RowGap + BackRowHeight;

        public static float ContainerCentreYFor(int count) =>
            ContainerBottom + ContainerHeightFor(count) * 0.5f;

        public static float ViewportOffsetInContainerFor(int count) =>
            ViewportCentreYFor(count) - ContainerCentreYFor(count);

        // The row's own content width -- ONE COPY, because the viewport
        // (built in FightScreen.BuildSubmenuFrame) has to size itself from the
        // exact same number the rows are actually built at, not a second
        // number that could drift from it the way this file's own header
        // warns about.
        //
        // NOW EQUAL TO VerbRowW, for the same reason RowHeight reads VerbRowH
        // above -- see that constant's own header. 300x52 is exactly the verb
        // row's own size, so the two pick the identical Row6x1 plate at the
        // identical dimensions: not merely a close aspect match any more, the
        // same border, at the same size.
        public const float RowWidth = VerbRowW;

        public static float ContainerWidth =>
            RowWidth + ContainerPad * 2f + ScrollbarGap + ScrollbarWidth;

        // ---- the frame around the box above (flat fill, not kit art) ------------
        //
        // WAS a Violet 3:4 Ui.Container. Turning PreserveAspect off (tried
        // first, 2026-09-23) kept the rect the right size but not the ART:
        // Type.Simple's non-uniform stretch squashed the painted border hard
        // at 1-3 rows -- the "stretched container art looks bad" defect the
        // owner rejected on the relic draft screen the same day, and the kit
        // already lints against elsewhere. RelicDraftScreen's own fix for
        // that screen (a flat Solid fill plus a hairline Rim, replacing a
        // Violet 3:2 Container for the identical reason -- see that file's
        // own header on DraftFrameFill) is the fix here too: a flat fill has
        // no aspect to keep, so it resizes to any height with zero
        // distortion and FightController.ResizeSubmenuContainer's
        // height-only runtime move stays exactly as simple as it already was.
        //
        // EXACTLY THE CONTENT BOX NOW, not bigger. The 3:4 art's own content
        // inset used to force the frame wider/taller than ContainerWidth/
        // ContainerHeight and recentre the inner box inside that slack (see
        // this section's own history in git blame if that math is ever
        // needed again); a flat fill has no border art to leave room for, so
        // FrameWidth/FrameHeight ARE ContainerWidth/ContainerHeight(For) --
        // no FrameInset, no separate FrameContentCentreY layer.
        public static float FrameWidth => ContainerWidth;

        public static float FrameHeight => ContainerHeight;

        public static float FrameHeightFor(int count) => ContainerHeightFor(count);

        // BOTTOM-ANCHORED AT VisibleBottomLine, ZERO PAD -- this is what
        // keeps FightScreenTests.TheSkillPanelEndsOnTheSameLineAsTheVerbColumn
        // true. A flat Solid fill draws exactly to its own rect on every
        // edge (no transparent halo the way the kit's container art always
        // carried, per VisibleBottomLine's own header) -- the same "rect
        // bottom IS the last painted pixel" convention that test's own
        // comment already documents for the PC plate's cropped art, so the
        // frame's rect can sit flush at VisibleBottomLine directly with no
        // pad correction, where the old art needed one.
        //
        // ViewportOffsetInContainer(For)/BackRowY-ContainerCentreY(For) below
        // are UNCHANGED by this -- they are still authored as an offset from
        // ContainerCentreY(For), and BuildSubmenuFrame/ResizeSubmenuContainer
        // still parent viewport/track/thumb/BACK directly under this frame
        // using those same offsets, so the whole row block shifts by
        // (FrameCentreY(For) - ContainerCentreY(For)) when reparented --
        // exactly the mechanism the old FrameContentCentreY used, just
        // simpler now that there is no second, art-inset-driven correction
        // to add on top of it.
        public static float FrameCentreY => VisibleBottomLine + FrameHeight * 0.5f;

        public static float FrameCentreYFor(int count) =>
            VisibleBottomLine + FrameHeightFor(count) * 0.5f;

        public static float FrameTop => FrameCentreY + FrameHeight * 0.5f;

        public static float FrameTopFor(int count) => FrameCentreYFor(count) + FrameHeightFor(count) * 0.5f;

        // NO LONGER CONCENTRIC WITH THE VIEWPORT. It was, while the container
        // held nothing but the list; the back row hangs below the viewport now,
        // so the two centres are a back row and a gap apart. Written from the
        // bottom edge, which is the part that is actually pinned.
        public static float ContainerCentreY => ContainerBottom + ContainerHeight * 0.5f;

        // How far the list can travel. Zero when everything fits, which is
        // also what hides the bar. Compared against RowsInView's OWN window
        // (ColumnHeight(RowsInView)), NOT the static (BuildReservationRows)
        // ViewportHeight -- the runtime viewport this is describing is
        // ALWAYS resized to RowsInView's cap the moment a real actor is
        // bound (ResizeSubmenuContainer, in FightController.cs), so the
        // scroll threshold has to agree with that actual size, not with the
        // oversized placeholder the static tree happens to be built at.
        public static float ScrollRange(int count)
        {
            float window = ColumnHeight(RowsInView);
            float content = ColumnHeight(VisibleCount(count));
            float over = content - window;

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
        // ViewportCentreYFor(count), NOT the static ViewportCentreY -- the
        // viewport this content sits inside is whatever height/position
        // AnchorSubmenuRows resized it to for THIS `count` (see that
        // method's own header), so the LOCAL offset that lands the pool
        // correctly inside it has to be measured against that same, possibly
        // shrunk, centre. Identical to the static term whenever count >=
        // RowsInView (VisibleRows clamps both to the same figure there), so
        // every existing caller passing a long list is unaffected -- only a
        // list shorter than RowsInView, which never scrolls anyway, reads a
        // different number here.
        public static float ContentY(int count, float scroll)
        {
            int shown = VisibleCount(count);
            float bottomAnchor = -(PoolSize - shown) * RowPitch;

            return ContentCentreY - ViewportCentreYFor(count) + bottomAnchor + ContentOffsetY(count, scroll);
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
        // content, floored so it stays grabbable. Measured against
        // RowsInView's OWN window, same reason ScrollRange is -- the thumb
        // only ever appears once the runtime viewport has actually been
        // resized to that cap (ResizeSubmenuContainer), never at the static
        // tree's own oversized BuildReservationRows footprint.
        public static float ThumbHeight(int count)
        {
            float window = ColumnHeight(RowsInView);
            float content = ColumnHeight(VisibleCount(count));
            if (content <= window || content <= 0f) return window;

            float height = window * (window / content);
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
        // over the whole list. ViewportOffsetInContainerFor(count), not the
        // static ViewportOffsetInContainer -- same reason ThumbHeight reads
        // RowsInView's window instead of the static one: the track/thumb this
        // describes only ever shows once ResizeSubmenuContainer has already
        // moved the real viewport there.
        public static float ThumbCentreY(int count, float scroll)
        {
            float window = ColumnHeight(RowsInView);
            float travel = window - ThumbHeight(count);
            float range = ScrollRange(count);
            float resting = ViewportOffsetInContainerFor(count);
            if (travel <= 0f || range <= 0f) return resting;

            float fraction = scroll / range;
            if (fraction < 0f) fraction = 0f;
            if (fraction > 1f) fraction = 1f;

            return resting + travel * 0.5f - fraction * travel;
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
        // the frame sits a few pixels above the inner box's own ContainerCentreY
        // (FrameCentreY is VisibleBottomLine-flush, not ContainerBottom-flush --
        // see FrameCentreY's own comment), so the old ContainerCentreY +
        // ContainerHeight * 0.5f would land the header inside the frame instead of
        // above it.
        // STILL IGNORES `count` -- the parameter is kept only because
        // BuildSubmenuColumn's one build-time call already passes one
        // (PoolSize), and this answers the STATIC tree's own header
        // position, off the STATIC FrameTop (BuildReservationRows), same as
        // always. HeaderYFor below is the count-AWARE twin AnchorSubmenuRows
        // actually wants once a real actor's skill count is known.
        public static float HeaderY(int count)
        {
            return FrameTop + 16f;
        }

        // The header's position for a REAL row count, off the frame's own
        // (possibly shrunk) top edge -- FrameTopFor(count), not the static
        // FrameTop. This is what tracks the container as it grows to fit a
        // short list or caps at RowsInView for a long one.
        public static float HeaderYFor(int count)
        {
            return FrameTopFor(count) + 16f;
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
