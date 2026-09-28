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

        // THE PANE'S OWN GROUND IS BARE -- HalfHeight reads SystemMenuLayout.
        // PaneContentHalfWidth/HalfHeight, the pane's own declared content
        // half-extents, instead of PaneHeight * 0.5f. PaneWidth/PaneHeight
        // above stay exactly what they were: they are still the FRAME's
        // declared size (SystemMenuPaneTests.EveryHostedPaneIsTheSizeOfThe
        // ContentArea pins them against the panel) -- see SystemMenuLayout.
        // PaneContentHalfWidth/HalfHeight's own comment.
        public static float HalfHeight => SystemMenuLayout.PaneContentHalfHeight;

        // A SMALL SLACK MARGIN over the pane's own content inset, not a
        // second authored pad. Same pattern as DossierLayout.
        // ColumnAContentMargin: a few pixels over the audit's own 0.01
        // containment tolerance, nothing more.
        public const float ContentMargin = 4f;

        public static float ContentTop => HalfHeight - ContentMargin;
        public static float ContentBottom => -HalfHeight + ContentMargin;

        // ---- the two exits ------------------------------------------------------

        // Wide and tall enough to be the thing the pane is about. At 520x76
        // they were three small controls adrift in 1600x804; the pane has no
        // other content to give them scale, so they have to carry it
        // themselves.
        public const float ExitWidth = 720f;

        // They wear a Silver kit plate, so the height comes from
        // Ui.PlateNominalSizeFor rather than being authored: static
        // readonly, not const, for the same reason MainMenuScreen.
        // ResetHoldHeight is -- PlateNominalSizeFor is not a compile-time
        // constant. It resolves to Row6x1 (nominal 6:1, height 120), which
        // StackFits below confirms clears the pane's vertical rhythm -- the
        // note underneath each button (NoteGap/NoteHeight, unchanged) still
        // sits clear of it by construction of
        // ExitNoteCentreY/ExitButtonCentreY below.
        public static readonly float ExitHeight = Ui.PlateNominalSizeFor(ExitWidth, 96f).Y;

        // The quiet line under each, saying what the button actually does to
        // the run. Not decoration: both of these end a descent, and a player
        // who thinks "return to title" parks one is owed the correction before
        // the click rather than after it.
        public const float NoteGap = 10f;
        public const float NoteHeight = 20f;

        // A PROPERTY, not a const: ExitHeight is a static readonly field,
        // not a compile-time literal, and a const expression cannot
        // reference one.
        public static float ExitBlockHeight => ExitHeight + NoteGap + NoteHeight;
        public const float ExitGap = 40f;

        public const int ExitCount = 2;

        // Named rather than left as 0 and 1 at four call sites. The order is
        // load-bearing -- it is the order they are read in, and the order the
        // screen builds the two blocks in.
        public const int ExitIndexTitle = 0;
        public const int ExitIndexQuit = 1;

        // A PROPERTY for the same reason ExitBlockHeight above is.
        public static float PairHeight => ExitBlockHeight * ExitCount + ExitGap * (ExitCount - 1);

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

        public const float SeparatorWidth = 980f;
        public const float SeparatorGap = 56f;

        public static float SeparatorY => PairBottom - SeparatorGap;

        // ---- the abandon card ----------------------------------------------------

        public const float AbandonWidth = 840f;

        public const float AbandonPadY = 26f;
        public const float AbandonHeadingHeight = 30f;
        public const float AbandonHeadingGap = 10f;
        public const float AbandonNoteHeight = 20f;
        public const float AbandonNoteGap = 22f;

        public const float HoldWidth = 620f;

        // It wears the Crimson kit plate, with the progress fill
        // clipped inside it exactly as MainMenuScreen.ResetConfirmYesButton's
        // own hold does -- see that screen's comment for the clip mechanism.
        // static readonly for the same reason ExitHeight above is: it
        // resolves to Row6x1 (nominal 6:1, height 103.33), which pushes the
        // whole abandon card, and so the whole three-piece stack, taller --
        // StackFits below is what actually proves the pane still holds it
        // rather than this comment asserting it.
        public static readonly float HoldHeight = Ui.PlateNominalSizeFor(HoldWidth, 80f).Y;

        // A PROPERTY, not a const, for the same reason ExitBlockHeight above
        // is -- HoldHeight is no longer a compile-time literal.
        public static float AbandonHeight =>
            AbandonPadY * 2f + AbandonHeadingHeight + AbandonHeadingGap +
            AbandonNoteHeight + AbandonNoteGap + HoldHeight;

        public static float AbandonCentreY => SeparatorY - SeparatorGap - AbandonHeight * 0.5f;

        // Pair, both separator gaps, and the card. The rule itself is 1px and
        // sits inside the gap rather than adding to it. A PROPERTY now, for
        // the same reason as its two dependencies above.
        public static float StackHeight => PairHeight + SeparatorGap * 2f + AbandonHeight;

        private static float AbandonHalf => AbandonHeight * 0.5f;

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
