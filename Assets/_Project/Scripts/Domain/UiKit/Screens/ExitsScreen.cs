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
    // BUTTONS WEAR THE KIT'S OWN PLATES now (owner's HQ-kit instruction,
    // 2026-09-07), superseding the "chromeless over a drawn plate" call this
    // comment used to make: the shared button sprite used to be authored for
    // a 220px button and stretch Simple, so a 520px one would have smeared it
    // and the menu drew its own flat plates instead. The kit's regenerated
    // art (ButtonPlateArt) is delivered at several true nominal shapes now,
    // so ExitTitle/ExitQuit size to whichever one fits their 720px width
    // (ExitsLayout.ExitHeight) instead of drawing a bespoke rect.
    public sealed class ExitsScreen
    {
        private const string Hover = FightHudPalette.HoverTint;
        private const string Hairline = FightHudPalette.Hairline;
        private const string TextMuted = FightHudPalette.TextMuted;

        // The destructive framing, straight out of the design's token table.
        private const string RedFill = FightHudPalette.PanelRed;
        private const string RedRim = "#E0786E73";
        private const string RedText = FightHudPalette.EnemyHpText;

        // The wash that crosses the hold. Deliberately not the rim's colour:
        // it passes under the label for 1.2 seconds and the label has to
        // stay readable the whole way. The track it used to run across is
        // gone -- the Crimson kit plate reads as the track now.
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

            // BARE, not a kit container (owner's call, 2026-09-07 -- every
            // frame inside the system menu read as ugly). This pane sits on
            // the shared SystemMenuFill with no ground of its own, same as
            // the dossier always did.
            var ground = Ui.SystemMenuPane("ExitsPane", "ExitsPaneContent",
                new UiVec(ExitsLayout.PaneWidth, ExitsLayout.PaneHeight), children.ToArray());

            screen.Root = ground;
            return screen;
        }

        // One exit: a themed plate, a hover wash over it, and a note
        // underneath saying what it does to the run.
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

            var blockChildren = new List<UiNode>();

            var button = Ui.Button($"Exit{key}", label, size, 17, Place.At(0f, buttonY))
                .Themed(ButtonTheme.Silver);

            // A WASH, not a scale, for the same reason it always was: scaling
            // the button would grow its label and leave the plate behind
            // standing still. DECLARED AFTER the button now (it used to sit
            // under a chromeless button's own drawn plate) so it draws ON TOP
            // of the kit plate -- Themed()'s own Glow already answers "does
            // this button react to hover" on the plate itself, so this stays
            // only for the same wash the design still asks for on top of it,
            // and drawing it behind the new opaque plate would have hidden it
            // completely.
            var hover = Ui.Solid($"Exit{key}Hover", Hover, size, Place.At(0f, buttonY))
                .Inactive()
                .AsDecor();

            screen.ExitHovers.Add(hover);
            screen.ExitButtons.Add(button);

            blockChildren.Add(button);
            blockChildren.Add(hover);

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

            // CRIMSON THEMED PLATE now (owner's HQ-kit instruction,
            // 2026-09-07), replacing the flat track Solid this used to draw
            // under NoChrome -- the plate itself reads as the track, so only
            // the progress fill survives as a child, clipped to the plate's
            // own measured paint exactly as MainMenuScreen.
            // ResetConfirmYesButton's identical hold already does (see that
            // screen's own comment for the clip/fill mechanism this copies).
            var holdPlateShape = Ui.PlateShapeFor(holdSize.X, holdSize.Y);
            var holdPlatePad = Ui.PlateVisiblePad(holdPlateShape);
            var holdFillClip = Ui.Panel("ExitAbandonHoldFillClip",
                    Place.Stretch(holdSize.X * holdPlatePad.Left, holdSize.X * holdPlatePad.Right,
                        holdSize.Y * holdPlatePad.Bottom, holdSize.Y * holdPlatePad.Top),
                    UiSize.Fill)
                .Clipping();

            // AUTHORED AT THE HOLD'S FULL SIZE and driven to zero by the
            // controller on wiring, same as before -- UiAudit refuses a
            // zero-sized graphic, and growing sizeDelta.x from a pinned left
            // edge is what lets HoldFillMath resize it with one number.
            //
            // PIVOTED LEFT, at the button's own left edge (button-local now
            // that this is a CHILD of the button rather than a card-level
            // sibling), so growing it by width extends it rightwards.
            var fill = Ui.Solid("ExitAbandonHoldFill", HoldFill, holdSize,
                    Place.At(ExitsLayout.HoldFillLeft, 0f, new UiVec(0f, 0.5f)))
                .AsDecor()
                .AllowOverflow(
                    "authored at the button's full holdSize so HoldFillMath can only ever shrink it, which " +
                    "means it starts a fraction of a pixel past the clip panel's own plate-inset edge - the " +
                    "clip panel is what actually keeps it off the painted border at runtime, not this node's " +
                    "own declared size");
            holdFillClip.Children.Add(fill);

            // QUIET, because a Button plays the shared click on release
            // whatever the press was for -- and a hold released early is a
            // press that deliberately did nothing. A click sound there says
            // something happened.
            var hold = Ui.Button("ExitAbandonHold", UiStrings.ExitAbandonHold, holdSize, 16,
                    Place.At(0f, ExitsLayout.HoldCentreY))
                .Quiet()
                .ThemedPlate(ButtonTheme.Crimson);
            hold.Children.Add(holdFillClip);
            hold.LayerCaptionWithVisuals(holdFillClip);

            screen.AbandonFill = fill;
            screen.AbandonHold = hold;

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
