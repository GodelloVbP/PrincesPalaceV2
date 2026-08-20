namespace PrincesPalace.Domain.UiKit
{
    // The Main menu pane's geometry, as pure arithmetic.
    //
    // Three exits, and the third SET APART -- above a rule, two ways out that
    // leave the game; below it, the one that throws away a descent. That
    // separation is the design's, and it is doing real work: the two above are
    // reversible in the sense that matters (the game is still there when you
    // come back), and the one below is not.
    //
    // THE PAIR MOVES WITH THE CONTEXT, which is the same shape the tab bar
    // uses one level up and is here for the same reason.
    //
    // Abandon is absent between descents -- there is no descent to abandon --
    // and a fixed layout has to be wrong in one of the two contexts: hung where
    // the three-piece stack wants it, the two survivors sit in the pane's top
    // third with a void under them, which is what the hub's only way out of the
    // game would look like. So the stack is centred WITH the card, the pair is
    // centred WITHOUT it, and the controller re-applies this the way it
    // re-applies the bar. The arithmetic still lives in exactly one place.
    public static class ExitsLayout
    {
        public const float PaneWidth = SystemMenuLayout.PanelWidth;                               // 1600
        public const float PaneHeight = SystemMenuLayout.PanelHeight - SystemMenuLayout.BarHeight;  // 804

        public const float HalfHeight = PaneHeight * 0.5f;

        public const float PadTop = 44f;
        public const float PadBottom = 64f;

        public static float ContentTop => HalfHeight - PadTop;
        public static float ContentBottom => -HalfHeight + PadBottom;

        // ---- the two exits ------------------------------------------------------

        public const float ExitWidth = 520f;
        public const float ExitHeight = 76f;

        // The quiet line under each, saying what the button actually does to
        // the run. Not decoration: both of these end a descent, and a player
        // who thinks "return to title" parks one is owed the correction before
        // the click rather than after it.
        public const float NoteGap = 10f;
        public const float NoteHeight = 18f;

        public const float ExitBlockHeight = ExitHeight + NoteGap + NoteHeight;   // 104
        public const float ExitGap = 32f;

        public const int ExitCount = 2;

        // Named rather than left as 0 and 1 at four call sites. The order is
        // load-bearing -- it is the order they are read in, and the order the
        // screen builds the two blocks in.
        public const int ExitIndexTitle = 0;
        public const int ExitIndexQuit = 1;

        public const float PairHeight = ExitBlockHeight * ExitCount + ExitGap * (ExitCount - 1);  // 240

        // A block is a CONTAINER, so the controller moves one rect per exit
        // rather than seven. Its box is a couple of pixels larger than its
        // contents all round: the button is flush with the block's top edge by
        // the definition of ExitBlockHeight, and a child whose edge sits
        // exactly on its parent's is the case the containment audit is right to
        // be suspicious of. Invisible either way.
        public const float ExitBlockPad = 2f;

        public static float ExitBlockBoxWidth => ExitWidth + ExitBlockPad * 2f;
        public static float ExitBlockBoxHeight => ExitBlockHeight + ExitBlockPad * 2f;

        // Where the pair sits, which depends on whether the abandon card is
        // under it. Centred with it, centred on its own without it.
        public static float PairCentreY(bool withAbandon) =>
            withAbandon ? StackHeight * 0.5f - PairHeight * 0.5f : 0f;

        public static float ExitBlockCentreY(int index, bool withAbandon) =>
            PairCentreY(withAbandon) + PairHeight * 0.5f - ExitBlockHeight * 0.5f
                - index * (ExitBlockHeight + ExitGap);

        // Inside a block, measured from the block's own centre.
        public static float ExitButtonCentreY => ExitBlockHeight * 0.5f - ExitHeight * 0.5f;
        public static float ExitNoteCentreY => -ExitBlockHeight * 0.5f + NoteHeight * 0.5f;

        // Everything below is the WITH-ABANDON layout, because the separator and
        // the card only exist in it.
        public static float PairBottom => PairCentreY(withAbandon: true) - PairHeight * 0.5f;

        // ---- the rule that sets abandon apart ------------------------------------

        public const float SeparatorWidth = 760f;
        public const float SeparatorGap = 48f;

        public static float SeparatorY => PairBottom - SeparatorGap;

        // ---- the abandon card ----------------------------------------------------

        public const float AbandonWidth = 620f;

        public const float AbandonPadY = 22f;
        public const float AbandonHeadingHeight = 26f;
        public const float AbandonHeadingGap = 8f;
        public const float AbandonNoteHeight = 18f;
        public const float AbandonNoteGap = 18f;

        public const float HoldWidth = 460f;
        public const float HoldHeight = 64f;

        public const float AbandonHeight =
            AbandonPadY * 2f + AbandonHeadingHeight + AbandonHeadingGap +
            AbandonNoteHeight + AbandonNoteGap + HoldHeight;                     // 178

        public static float AbandonCentreY => SeparatorY - SeparatorGap - AbandonHeight * 0.5f;

        // Pair, both separator gaps, and the card. The rule itself is 1px and
        // sits inside the gap rather than adding to it.
        public const float StackHeight = PairHeight + SeparatorGap * 2f + AbandonHeight;

        private const float AbandonHalf = AbandonHeight * 0.5f;

        public static float AbandonHeadingCentreY =>
            AbandonHalf - AbandonPadY - AbandonHeadingHeight * 0.5f;

        public static float AbandonNoteCentreY =>
            AbandonHalf - AbandonPadY - AbandonHeadingHeight - AbandonHeadingGap - AbandonNoteHeight * 0.5f;

        public static float HoldCentreY =>
            AbandonHalf - AbandonPadY - AbandonHeadingHeight - AbandonHeadingGap -
            AbandonNoteHeight - AbandonNoteGap - HoldHeight * 0.5f;

        // The fill grows from the button's left edge, so it is placed there and
        // pivoted there. Same shape as the Options slider's fill and for the
        // same reason: anchoring 0..progress instead spans the PARENT, which is
        // the card, and would paint over the note above it.
        public static float HoldFillLeft => -HoldWidth * 0.5f;

        // ---- the no-overflow guard -----------------------------------------------
        //
        // Everything here is authored rather than derived from a list, so this
        // cannot drift the way a table-driven pane can -- but it is the one
        // number a future edit would break silently, by nudging PairCentreY or
        // growing the card until the descent-ending button runs off the pane.
        public static float StackBottom => AbandonCentreY - AbandonHeight * 0.5f;

        public static float StackTop =>
            ExitBlockCentreY(0, withAbandon: true) + ExitBlockBoxHeight * 0.5f;

        public static bool StackFits => StackBottom >= ContentBottom && StackTop <= ContentTop;
    }
}
