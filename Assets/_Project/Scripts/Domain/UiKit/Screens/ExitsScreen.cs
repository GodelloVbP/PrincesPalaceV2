using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The Main menu pane: three ways out, and none of them fires on one press.
    //
    // Hosted inside the system menu's Main menu pane the way the dossier,
    // Options and Run statistics are -- its own screen class, its own
    // controller, and the menu knows nothing about either.
    //
    // TWO GESTURES, NOT ONE, and that is the design's rule rather than a
    // flourish. The two exits above the rule arm on the first press and fire on
    // the second; abandon is a 1.2s hold. The difference between them is the
    // difference in what they cost: leaving the game is undone by starting it
    // again, and a descent thrown away is not.
    //
    // BUTTONS ARE CHROMELESS OVER DRAWN PLATES, like every other button in this
    // menu. The shared button sprite is authored for a 220px button and stretches
    // Simple, so a 520px one would smear it; the menu already draws its own
    // plates for the bar, the cards and the rims, and one more is cheaper than
    // one distorted sprite.
    public sealed class ExitsScreen
    {
        private const string Plate = FightHudPalette.CardFill;
        private const string PlateRim = FightHudPalette.Hairline;
        private const string Hover = FightHudPalette.HoverTint;
        private const string Hairline = FightHudPalette.Hairline;
        private const string TextMuted = FightHudPalette.TextMuted;

        // The destructive framing, straight out of the design's token table.
        private const string RedFill = FightHudPalette.PanelRed;
        private const string RedRim = "#E0786E73";
        private const string RedText = FightHudPalette.EnemyHpText;

        // The track the hold runs across, and the wash that crosses it. The
        // wash is deliberately not the rim's colour: it passes under the label
        // for 1.2 seconds and the label has to stay readable the whole way.
        private const string HoldTrack = FightHudPalette.Track;
        private const string HoldFill = "#E0786E4D";

        public UiNode Root;

        // Indexed by ExitsLayout.ExitIndexTitle / ExitIndexQuit, so the
        // controller can wire hover and click off one loop.
        public List<NodeRef> ExitButtons = new List<NodeRef>();
        public List<NodeRef> ExitHovers = new List<NodeRef>();

        // The containers the two exits live in. The controller moves THESE --
        // one rect per exit instead of the seven nodes each one is made of.
        public List<NodeRef> ExitBlocks = new List<NodeRef>();

        public NodeRef Separator;
        public NodeRef AbandonCard;
        public NodeRef AbandonHold;
        public NodeRef AbandonFill;

        public static ExitsScreen Build()
        {
            var screen = new ExitsScreen();

            // Authored geometry rather than a table, so this cannot drift the
            // way a list-driven pane can -- but the stack is centred on its own
            // height now, so growing any part of it pushes BOTH ends outwards
            // at once, and the end that goes first is the one carrying the
            // descent-ending button.
            if (!ExitsLayout.StackFits)
            {
                throw new System.InvalidOperationException(
                    $"The Main menu pane's stack runs from y {ExitsLayout.StackTop:F0} to " +
                    $"{ExitsLayout.StackBottom:F0}, against a pane of {ExitsLayout.ContentTop:F0} to " +
                    $"{ExitsLayout.ContentBottom:F0}. Shrink the abandon card, tighten the separator " +
                    "gaps, or shorten the exits - the stack is centred, so it cannot simply be moved.");
            }

            var children = new List<UiNode>
            {
                // AUTHORED IN THE WITH-ABANDON LAYOUT, and re-laid at runtime
                // for the other one -- the same arrangement the tab bar uses,
                // and it looks like the same violation of the generated-scenes
                // rule for the same reason it is not one. A scene is generated
                // once and this pane has two shapes; the arithmetic is still in
                // exactly one place, only the moment it runs moved.
                BuildExit(screen, ExitsLayout.ExitIndexTitle, "Title",
                    UiStrings.ExitToTitle, UiStrings.ExitToTitleNote),
                BuildExit(screen, ExitsLayout.ExitIndexQuit, "Quit",
                    UiStrings.ExitQuit, UiStrings.ExitQuitNote),
            };

            // The rule that sets abandon apart, and it goes when the card goes:
            // a line with nothing under it is not a separator, it is a stray
            // mark under two buttons.
            var separator = Ui.Solid("ExitsSeparator", Hairline,
                    new UiVec(ExitsLayout.SeparatorWidth, 1f),
                    Place.At(0f, ExitsLayout.SeparatorY))
                .Inactive()
                .AsDecor();

            screen.Separator = separator;
            children.Add(separator);

            children.Add(BuildAbandon(screen));

            // A SILVER 2:1 CONTAINER, not a bare Panel -- this pane sat on the
            // shared SystemMenuFill with no ground of its own before this
            // (balance-bot, 2026-09-02). Silver: the neutral/utility theme,
            // same choice as Options and Run statistics. PaneWidth x PaneHeight
            // (1600x804) already hits the kit's measured 2:1 aspect within
            // 0.5% -- see SystemMenuLayout.PaneInset's own comment -- so no
            // size nudge was needed to make this a valid container.
            var ground = Ui.Container("ExitsPane", ButtonTheme.Silver, ContainerRatio.TwoByOne,
                Place.At(0f, 0f), new UiVec(ExitsLayout.PaneWidth, ExitsLayout.PaneHeight));
            Ui.ContainerContent(ground, ContainerRatio.TwoByOne, "ExitsPaneContent", children.ToArray());

            screen.Root = ground;
            return screen;
        }

        // One exit: a plate, a hover plate over it, a chromeless button over
        // that, and a note underneath saying what it does to the run.
        //
        // The note is not flavour. Both of these END THE DESCENT -- the
        // codebase's rule is that leaving one kills it, and the hub's own title
        // button and the main menu's quit both enforce it -- and neither
        // button's own words say so.
        private static UiNode BuildExit(
            ExitsScreen screen, int index, string key, UiString label, UiString note)
        {
            float buttonY = ExitsLayout.ExitButtonCentreY;
            var size = new UiVec(ExitsLayout.ExitWidth, ExitsLayout.ExitHeight);

            var blockChildren = new List<UiNode>
            {
                Ui.Solid($"Exit{key}Plate", Plate, size, Place.At(0f, buttonY))
                    .AsDecor(),
            };

            blockChildren.AddRange(Ui.Rim($"Exit{key}",
                new UiVec(ExitsLayout.ExitWidth, ExitsLayout.ExitHeight), PlateRim,
                new UiVec(0f, buttonY)));

            // A PLATE, not a scale, for the same reason the tab bar's hover is
            // one: the button is chromeless over a drawn plate, so scaling the
            // button would grow its label and leave the frame behind standing
            // still.
            var hover = Ui.Solid($"Exit{key}Hover", Hover, size, Place.At(0f, buttonY))
                .Inactive()
                .AsDecor();

            var button = Ui.Button($"Exit{key}", label, size, 17, Place.At(0f, buttonY))
                .NoChrome();

            screen.ExitHovers.Add(hover);
            screen.ExitButtons.Add(button);

            blockChildren.Add(hover);
            blockChildren.Add(button);

            blockChildren.Add(Ui.Label($"Exit{key}Note", note,
                    new UiVec(ExitsLayout.ExitWidth, ExitsLayout.NoteHeight), 13, TextMuted,
                    Place.At(0f, ExitsLayout.ExitNoteCentreY))
                .AsDecor());

            var block = Ui.Panel($"Exit{key}Block",
                Place.At(0f, ExitsLayout.ExitBlockCentreY(index, withAbandon: true)),
                UiSize.Fixed(ExitsLayout.ExitBlockBoxWidth, ExitsLayout.ExitBlockBoxHeight),
                blockChildren);

            screen.ExitBlocks.Add(block);
            return block;
        }

        private static UiNode BuildAbandon(ExitsScreen screen)
        {
            float w = ExitsLayout.AbandonWidth;
            float h = ExitsLayout.AbandonHeight;

            var cardChildren = new List<UiNode>
            {
                Ui.Solid("ExitAbandonCardFill", RedFill, new UiVec(w, h), Place.At(0f, 0f))
                    .AsDecor(),
            };

            cardChildren.AddRange(Ui.Rim("ExitAbandonCard", new UiVec(w, h), RedRim));

            cardChildren.Add(Ui.Label("ExitAbandonHeading", UiStrings.ExitAbandonHeading,
                    new UiVec(w - 40f, ExitsLayout.AbandonHeadingHeight), 16, RedText,
                    Place.At(0f, ExitsLayout.AbandonHeadingCentreY))
                .AsDecor());

            cardChildren.Add(Ui.Label("ExitAbandonNote", UiStrings.ExitAbandonNote,
                    new UiVec(w - 40f, ExitsLayout.AbandonNoteHeight), 13, TextMuted,
                    Place.At(0f, ExitsLayout.AbandonNoteCentreY))
                .AsDecor());

            var holdSize = new UiVec(ExitsLayout.HoldWidth, ExitsLayout.HoldHeight);

            cardChildren.Add(Ui.Solid("ExitAbandonHoldTrack", HoldTrack, holdSize,
                    Place.At(0f, ExitsLayout.HoldCentreY))
                .AsDecor());

            // AUTHORED FULL WIDTH and driven to zero by the controller on
            // wiring. It has to be authored at its real size because UiAudit
            // refuses a zero-sized graphic -- rightly, since a graphic with no
            // area is indistinguishable from one somebody forgot to size.
            //
            // PIVOTED LEFT, at the track's left edge, so growing it by width
            // extends it rightwards. Anchoring 0..progress instead is relative
            // to the PARENT, which is the card, and would paint a red bar
            // straight through the note above it -- the same mistake the
            // Options slider's fill records in its own comment.
            var fill = Ui.Solid("ExitAbandonHoldFill", HoldFill, holdSize,
                    Place.At(ExitsLayout.HoldFillLeft, ExitsLayout.HoldCentreY, new UiVec(0f, 0.5f)))
                .AsDecor();

            // QUIET, because a Button plays the shared click on release
            // whatever the press was for -- and a hold released early is a
            // press that deliberately did nothing. A click sound there says
            // something happened.
            var hold = Ui.Button("ExitAbandonHold", UiStrings.ExitAbandonHold, holdSize, 16,
                    Place.At(0f, ExitsLayout.HoldCentreY))
                .NoChrome()
                .Quiet();

            screen.AbandonFill = fill;
            screen.AbandonHold = hold;

            cardChildren.Add(fill);
            cardChildren.Add(hold);

            // INACTIVE by default, switched on only in a descent.
            //
            // The same rule the tab bar follows one level up: a control that
            // cannot be used is absent, never greyed. Between descents there is
            // no descent to abandon, and a greyed button asks the player to work
            // out why with nowhere to answer.
            var card = Ui.Panel("ExitAbandonCard", Place.At(0f, ExitsLayout.AbandonCentreY),
                    UiSize.Fixed(w, h), cardChildren)
                .Inactive();

            screen.AbandonCard = card;
            return card;
        }

                    }
}
