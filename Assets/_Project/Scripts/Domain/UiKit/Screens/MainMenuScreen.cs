using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // What the main menu needs from outside Domain.
    //
    // Domain cannot see SaveSystem (Core), so the slot count arrives as an
    // input rather than being restated here. That matters: this screen builds
    // three separate things off that number -- slot buttons, reset rows, delete
    // buttons -- and a second copy of it is precisely the shape of every
    // count-vs-footprint bug v1 shipped. It also lets an EditMode test audit
    // the screen at a roster the game does not currently have.
    public readonly struct MainMenuInputs
    {
        public readonly int SlotCount;

        public MainMenuInputs(int slotCount)
        {
            SlotCount = slotCount;
        }
    }

    // The main menu, declared. Engine-free, so an EditMode test builds and
    // audits this entire screen -- ambience and all, at four canvas frames --
    // in about a millisecond. v1 could only ever verify a layout by generating
    // a scene and looking at it.
    public sealed class MainMenuScreen
    {
        public const string BackgroundKey = "Backgrounds/Main_menu.png";

        // The hold's own footprint, authored here rather than only inline in
        // Build -- ResetProgressController (Core) needs the same width to
        // resize the fill by, and a number typed twice is a number that can
        // disagree with itself. See ExitsLayout.HoldWidth for the identical
        // reason that constant is public.
        public const float ResetHoldWidth = 200f;

        // Was a 60f const (200x60 = 3.333:1, 11.1% off the Legacy plate's
        // true 3:1 -- ThemedButtonAspectLintTests). static readonly, not
        // const, because Ui.PlateNominalSizeFor is not a compile-time
        // constant -- ResetProgressController and MainMenuScreenTests both
        // read this same field, so the two stay unable to disagree the same
        // way the const did.
        public static readonly float ResetHoldHeight = Ui.PlateNominalSizeFor(ResetHoldWidth, 60f).Y;

        public UiNode Root;

        public NodeRef SceneLayer;
        public MainMenuAmbience.Layer Ambience;

        public NodeRef TitleLabel;

        // The left-edge wash behind the title and the menu.
        public NodeRef LeftScrim;

        // The one column every menu item sits in, first to last. Flow
        // layout is solved at build time and does not reflow when a child
        // is hidden, so MainMenuController closes the gap left by a hidden
        // item at runtime from the authored positions (ColumnReflow).
        public NodeRef MenuColumn;

        // BUILT UNCONDITIONALLY, shown only sometimes. Domain has no
        // SaveSystem to ask at build time whether there is anything to
        // continue into, so the node always exists and MainMenuController
        // decides at runtime whether to show it -- the same split every other
        // screen-can't-know-yet fact in this codebase uses.
        public NodeRef ContinueButton;
        public NodeRef ContinueCaption;
        public NodeRef ContinueSubline;

        public NodeRef NewRunButton;
        public NodeRef LoadButton;
        public NodeRef ExitButton;

        public NodeRef SaveSlotPanel;
        public NodeRef ManageSavesButton;
        public NodeRef CloseSaveSlotButton;

        // Reset Progress, moved here from a standalone Options screen on
        // request: deleting a save is something you do from the screen that
        // shows you the saves, not a separate destination. Opened FROM
        // SaveSlotPanel and returns to it -- see CloseManageSavesButton.
        public NodeRef ManageSavesPanel;
        public NodeRef CloseManageSavesButton;

        public NodeRef ResetConfirmPanel;
        public NodeRef ResetConfirmLabel;

        // A HOLD, not a click, on request -- the same 1.2s track-and-fill
        // ExitsScreen's own abandon button already uses for the one other
        // irreversible action in the game. ResetConfirmYesButton is the
        // invisible-chrome button that actually takes the hold; the fill is
        // exposed separately because the controller has to resize it every
        // frame the hold is live, which the button reference alone cannot do.
        public NodeRef ResetConfirmYesButton;
        public NodeRef ResetConfirmYesFill;
        public NodeRef ResetConfirmNoButton;

        // How wide/tall a slot card is, in both lists -- the same footprint
        // so the two screens read as one vocabulary rather than two.
        public const float CardWidth = 700f;
        public const float CardHeight = 92f;

        // The CHOOSE list's own card height, taller than CardHeight.
        // 700x92 was 72% off the Row6x1 plate's true 6:1 -- the choose-list
        // card wears a ThemedPlate (SaveSlotController drives its filled/
        // empty state through ThemedButtonState.SetMenuState, so it cannot
        // go chromeless the way DebugMenuScreen/GlossaryScreen's rows did),
        // and the honest nominal height for 700 wide is 116.7 -- but eight
        // of those (TheMenuAuditsCleanAtOtherSlotCounts, a roster larger
        // than the shipped SaveSystem.SlotCount=5 the screen's own
        // arithmetic still has to hold for) do not fit a 1080-tall screen
        // even with SaveSlotColumn's spacing trimmed. 112 is the shortest
        // height inside Ui.ContainerAspectTolerance's 5% band (4.2% off, an
        // 111.1-122.8 window) rather than the full nominal, so as little
        // height as the lint allows goes to the roster that does not ship.
        // Manage Saves' rows stay Panels at plain CardHeight -- they carry
        // no plate, so the lint has no opinion on them.
        private const float ChooseCardHeight = 112f;

        // One slot's card content: the number badge, its two lines of text,
        // the gold figure (Choose only -- Gold.IsValid is false on a Manage
        // Saves card, which shows a Delete button in the same spot instead),
        // and the two background states a slot switches between.
        //
        // A STRUCT PER SLOT rather than six more parallel List<NodeRef>
        // fields, deliberately: six lists built from the SAME loop iteration
        // as SlotButtons cannot drift in count from each other the way E4
        // exists to catch two INDEPENDENTLY built collections drifting -- so
        // there is nothing for that audit to check here, and six lists that
        // cannot disagree are just six chances to index one of them wrong.
        public readonly struct SlotCardRefs
        {
            public readonly NodeRef Number;
            public readonly NodeRef Top;
            public readonly NodeRef Detail;
            public readonly NodeRef Gold;
            public readonly NodeRef FilledWash;
            public readonly NodeRef EmptyWash;

            public SlotCardRefs(NodeRef number, NodeRef top, NodeRef detail, NodeRef gold,
                                NodeRef filledWash, NodeRef emptyWash)
            {
                Number = number;
                Top = top;
                Detail = detail;
                Gold = gold;
                FilledWash = filledWash;
                EmptyWash = emptyWash;
            }
        }

        public readonly List<NodeRef> SlotButtons = new List<NodeRef>();
        public readonly List<SlotCardRefs> ChooseCards = new List<SlotCardRefs>();

        public readonly List<NodeRef> DeleteButtons = new List<NodeRef>();
        public readonly List<SlotCardRefs> ManageCards = new List<SlotCardRefs>();

        // Builds one slot's card visuals as children of `host` -- the number
        // badge, two lines of text, the gold figure when `showGold` is true,
        // and the two background washes a slot switches between. ALL DECOR:
        // `host` alone carries the click (a Button in the choose list, an
        // inert Panel in Manage Saves, with its own Delete button added
        // beside these afterwards), and these are painted on top of it,
        // positioned relative to its own centre.
        //
        // EVERY TEXT NODE IS UiString.Runtime. None of this is known at
        // build time -- Domain has no SaveSystem to ask which slots are
        // filled, what a roster's display name is, or what floor a save
        // reached -- so every line here is set by the controller's own
        // Refresh(), the same split Continue's own text takes.
        private static SlotCardRefs AddCardContent(UiNode host, string stem, bool showGold, bool includeWash = true)
        {
            // THE WASH, added FIRST so the text paints on top of it -- draw
            // order is declaration order in this DSL, and a wash added after
            // its own row's text would cover it.
            //
            // includeWash is false for the choose-list Slot{i}Button: that
            // button wears a Gold ThemedPlate instead of NoChrome, so the
            // plate itself IS the background and
            // a wash under it would either hide behind an opaque plate or
            // show through a translucent one for no reason. Manage Saves'
            // rows stay Panels, not Buttons, and keep the wash exactly as
            // before -- ResetProgressController still drives it.
            NodeRef filledWash = default;
            NodeRef emptyWash = default;
            if (includeWash)
            {
                filledWash = Ui.Solid($"{stem}FilledWash", "#E3C16621", new UiVec(CardWidth, CardHeight),
                        Place.At(0f, 0f))
                    .AsDecor();
                emptyWash = Ui.Solid($"{stem}EmptyWash", "#0E070C4D", new UiVec(CardWidth, CardHeight),
                        Place.At(0f, 0f))
                    .AsDecor()
                    .Inactive();
                host.Children.Add(filledWash.Node);
                host.Children.Add(emptyWash.Node);
            }

            // FOUR COLUMNS, LEFT TO RIGHT, and each one's box is placed by its
            // own EDGES rather than guessed at from the card's centre -- a
            // Runtime label has no authored copy for the text-fit audit (E1)
            // to measure, so a column that drifted into its neighbour would
            // ship silently instead of failing the build. Numbers here are
            // the geometry, checked once: number badge to -282, text column
            // -260 to 150, gold/delete column 165 to 335, all inside the
            // card's own -350..350.
            const float NumberBadgeCentreX = -310f;
            const float TextColumnLeftX = -260f;
            const float TextColumnRightX = 150f;
            const float TextColumnWidth = TextColumnRightX - TextColumnLeftX;
            const float TextColumnCentreX = (TextColumnLeftX + TextColumnRightX) * 0.5f;
            const float SideColumnCentreX = 250f;
            const float SideColumnWidth = 170f;

            var number = Ui.Label($"{stem}Number", UiString.Runtime, new UiVec(56f, 48f), 34,
                    "#E3C166", Place.At(NumberBadgeCentreX, 0f))
                .AsDecor();
            host.Children.Add(number);

            var top = Ui.Label($"{stem}Top", UiString.Runtime, new UiVec(TextColumnWidth, 26f), 20,
                    "#E4DBFF", Place.At(TextColumnCentreX, 15f))
                .AsDecor()
                .TextAligned(UiTextAlign.Left);
            host.Children.Add(top);

            var detail = Ui.Label($"{stem}Detail", UiString.Runtime, new UiVec(TextColumnWidth, 22f), 15,
                    "#A99BD4", Place.At(TextColumnCentreX, -15f))
                .AsDecor()
                .TextAligned(UiTextAlign.Left);
            host.Children.Add(detail);

            NodeRef gold = default;
            if (showGold)
            {
                var goldLabel = Ui.Label($"{stem}Gold", UiString.Runtime, new UiVec(SideColumnWidth, 28f), 20,
                        "#E3C166", Place.At(SideColumnCentreX, 0f))
                    .AsDecor()
                    .TextAligned(UiTextAlign.Right);
                host.Children.Add(goldLabel);
                gold = goldLabel;
            }

            return new SlotCardRefs(number, top, detail, gold, filledWash, emptyWash);
        }

        public static MainMenuScreen Build(MainMenuInputs inputs)
        {
            var screen = new MainMenuScreen();
            int slotCount = inputs.SlotCount;

            // --- the painted world, and everything that moves on it ----------
            //
            // Background and ambience share ONE parent so the slow push-in moves
            // them together. Moving the background alone would slide every
            // lantern glow off the lantern it belongs to within seconds.
            screen.Ambience = MainMenuAmbience.Build();
            var sceneLayer = Ui.Panel("SceneLayer", Place.Stretch(), UiSize.Fill,
                Ui.Sprite("Background", BackgroundKey, Place.Stretch(), UiSize.Fill),
                screen.Ambience.Root).AsDecor();
            screen.SceneLayer = sceneLayer;

            // --- the wash behind the text ------------------------------------
            //
            // Declared right after SceneLayer so it dims the painting and
            // nothing else. Left edge only: it is gone well before the palace
            // (canvas x -100), and its bottom edge is above the terrace lantern
            // at canvas (-643, -272), whose glow must not be dimmed.
            const float ScrimLeft = -960f;
            const float ScrimRight = -200f;
            const float ScrimTop = 540f;
            const float ScrimBottom = -200f;
            var leftScrim = Ui.Sprite("LeftScrim", "proc:scrim_left",
                    Place.At((ScrimLeft + ScrimRight) * 0.5f, (ScrimTop + ScrimBottom) * 0.5f),
                    UiSize.Fixed(ScrimRight - ScrimLeft, ScrimTop - ScrimBottom))
                .Coloured("#0A060FD9")
                .AsDecor();
            screen.LeftScrim = leftScrim;

            // --- the wordmark --------------------------------------------------
            //
            // The placeholder logo: gold text that a sprite replaces later
            // without moving anything (same node, same box).
            //
            // LEFT-ANCHORED, not centred on the canvas. The painting's own
            // subject -- the palace, floating mid-frame -- sits right of
            // centre; a centred column lands on top of it and on the water's
            // reflection below it. The left third is comparatively empty sky
            // and stair, which is where every element below is anchored to.
            //
            // ONE SHARED LEFT EDGE, not one shared centre: LeftEdgeX plus
            // each element's own half-width, so the title and every menu
            // item start flush with each other.
            const float LeftEdgeX = -810f;

            // Two lines ("Prince's" / "Palace") at 96pt: a single line at this
            // size would run into the palace. No .Tracked() -- CeremonialTitle's
            // own spec is what ApplyTypography applies once Role is set.
            const float TitleWidth = 640f;
            const float TitleHeight = 270f;
            var title = Ui.Label("TitleLabel", UiStrings.GameTitle, new UiVec(TitleWidth, TitleHeight), 96,
                    "#E3C166", Place.At(LeftEdgeX + TitleWidth * 0.5f, 355f))
                .TextAligned(UiTextAlign.Left)
                .Styled(TypographyRole.CeremonialTitle);
            screen.TitleLabel = title;

            // --- the menu itself ---------------------------------------------
            //
            // Continue is the column's first item and carries a second line
            // under its caption. ThemedPlate, so the caption is a declared
            // child (see ExitsScreen's hold button) sitting above the centre
            // with the subline below it.
            const float MenuWidth = 400f;
            var continueSize = Ui.PlateNominalSizeFor(MenuWidth, 80f);
            var continueButton = Ui.Button("ContinueButton", UiString.Runtime, continueSize, 24)
                .ThemedPlate(ButtonTheme.Gold);
            var continueCaption = Ui.Label("ContinueButtonCaption", UiStrings.ContinueSlot,
                    new UiVec(continueSize.X - 40f, 36f), 24, place: Place.At(0f, 14f))
                .Styled(TypographyRole.ButtonLabel);
            var continueSubline = Ui.Label("ContinueSubline", UiStrings.ContinueSubline,
                    new UiVec(continueSize.X - 40f, 22f), 15, "#F2DB9E", Place.At(0f, -18f))
                .AsDecor();
            continueButton.Children.Add(continueCaption);
            continueButton.Children.Add(continueSubline);
            continueButton.LayerCaptionWithVisuals(continueCaption, continueSubline);
            screen.ContinueButton = continueButton;
            screen.ContinueCaption = continueCaption;
            screen.ContinueSubline = continueSubline;

            // 260x60 was 4.333:1 against the FiveByOne plate's true 5:1 --
            // ThemedButtonAspectLintTests. Height down to nominal, width kept.
            var itemSize = Ui.PlateNominalSizeFor(260f, 60f);
            var newRun = Ui.Button("NewRunButton", UiStrings.NewRun, itemSize, 24)
                .Themed(ButtonTheme.Gold);
            var load = Ui.Button("LoadButton", UiStrings.Load, itemSize, 24)
                .Themed(ButtonTheme.Gold);
            var quit = Ui.Button("ExitButton", UiStrings.Quit, itemSize, 24)
                .Themed(ButtonTheme.Silver);
            screen.NewRunButton = newRun;
            screen.LoadButton = load;
            screen.ExitButton = quit;

            // Top-aligned under the title, left edge shared with it. The
            // column's centre y is authored so its top sits at y = 190.
            const float MenuSpacing = 20f;
            const float MenuTop = 190f;
            float menuHeight = continueSize.Y + itemSize.Y * 3f + MenuSpacing * 3f;
            var menuColumn = Ui.Column("MenuButtons",
                Place.At(LeftEdgeX + MenuWidth * 0.5f, MenuTop - menuHeight * 0.5f), MenuSpacing,
                UiAlign.Start, continueButton, newRun, load, quit);
            screen.MenuColumn = menuColumn;

            // --- save slots ---------------------------------------------------
            var slotChildren = new List<UiNode>
            {
                Ui.Label("SaveSlotTitle", UiStrings.ChooseSlotHeader, new UiVec(400f, 44f), 28)
                    .Styled(TypographyRole.FunctionalHeading)
            };

            for (int i = 0; i < slotCount; i++)
            {
                // GOLD THEMED PLATE, not Chromeless -- the plate is the
                // card's own background, and SaveSlotController drives
                // filled/empty through SetMenuState(Primary/Idle) instead of
                // toggling two washes.
                // 700x92 (aspect 7.6) stretches the 6.0 row plate non-
                // uniformly by about 28% rather than being narrowed to fit
                // it exactly: AddCardContent's column geometry (the number
                // badge at -310, the gold figure centred at 250) is SHARED
                // with Manage Saves' identical-width rows, and narrowing just
                // this button to clear the row plate's own aspect would
                // either desync the two lists' column alignment or force a
                // second copy of that geometry -- the same "accept the
                // stretch" tradeoff RespecButton already established
                // (ButtonPlateArt's own header).
                var button = Ui.Button($"Slot{i}Button", UiString.Runtime,
                        new UiVec(CardWidth, ChooseCardHeight), 20)
                    .ThemedPlate(ButtonTheme.Gold);
                screen.SlotButtons.Add(button);
                var card = AddCardContent(button, $"Slot{i}", showGold: true, includeWash: false);
                screen.ChooseCards.Add(card);
                button.LayerCaptionWithVisuals(card.Number.Node, card.Top.Node, card.Detail.Node, card.Gold.Node);
                slotChildren.Add(button);
            }

            // 200x50 was 4:1 against the FiveByOne plate's true 5:1 --
            // ThemedButtonAspectLintTests. Height down to nominal, width kept.
            var footerButtonSize = Ui.PlateNominalSizeFor(200f, 50f);
            var manageSaves = Ui.Button("ManageSavesButton", UiStrings.ManageSaves, footerButtonSize, 18)
                .Themed(ButtonTheme.Silver);
            var cancel = Ui.Button("CloseSaveSlotButton", UiStrings.Cancel, footerButtonSize, 18)
                .Themed(ButtonTheme.Silver);
            screen.ManageSavesButton = manageSaves;
            screen.CloseSaveSlotButton = cancel;

            // A Row, so the two footer actions cannot drift onto separate lines
            // the way two Column entries would once one of their labels grew.
            slotChildren.Add(Ui.Row("SaveSlotFooter", Place.Flow, spacing: 24f, UiAlign.Centre,
                manageSaves, cancel));

            // 10: at Slot{i}Button's 112-tall size, and at the roster size
            // TheMenuAuditsCleanAtOtherSlotCounts(8) checks (a roster the
            // game does not currently field, but the screen's own arithmetic
            // has to hold for), the column needs this tight a spacing to
            // fit the 1080-tall screen.
            var saveSlotModal = Ui.Modal("SaveSlotPanel", "#000000D9",
                Ui.Column("SaveSlotColumn", Place.At(0f, 0f), spacing: 10f, UiAlign.Centre, slotChildren))
                .Inactive();
            screen.SaveSlotPanel = saveSlotModal;

            // --- manage saves, the destructive half ----------------------------
            var manageRows = new List<UiNode>
            {
                Ui.Label("ManageSavesTitle", UiStrings.ManageSaves, new UiVec(400f, 50f), 32)
                    .Styled(TypographyRole.FunctionalHeading),
                Ui.Label("ManageSavesWarning", UiStrings.ManageSavesWarning, new UiVec(560f, 60f), 15,
                    "#A99BD4")
                    .Styled(TypographyRole.Body),
            };

            for (int i = 0; i < slotCount; i++)
            {
                // A PANEL, not a Button -- this row is not itself clickable,
                // only the Delete button inside it is, so there is no click
                // target to give it. Same card content as the choose list
                // (see AddCardContent), minus the gold figure: Delete sits
                // where gold would have been, and a card that is about to be
                // deleted has no use for a second look at what it is worth.
                var row = Ui.Panel($"ResetSlot{i}Row", Place.Flow, UiSize.Fixed(CardWidth, CardHeight));
                var card = AddCardContent(row, $"ResetSlot{i}", showGold: false);
                screen.ManageCards.Add(card);

                // Centred on the same 250px column AddCardContent's gold
                // figure uses in the choose list, so the two lists' right-hand
                // edge lines up whichever one is on screen.
                var delete = Ui.Button($"ResetSlot{i}DeleteButton", UiStrings.Delete, new UiVec(130f, 44f), 16,
                        Place.At(250f, 0f))
                    .Themed(ButtonTheme.Crimson);
                screen.DeleteButtons.Add(delete);
                row.Children.Add(delete);

                manageRows.Add(row);
            }

            var backToSlots = Ui.Button("CloseManageSavesButton", UiStrings.Back, new UiVec(260f, 50f), 20)
                .Themed(ButtonTheme.Silver);
            screen.CloseManageSavesButton = backToSlots;
            manageRows.Add(backToSlots);

            var manageSavesModal = Ui.Modal("ManageSavesPanel", "#000000D9",
                Ui.Column("ManageSavesColumn", Place.At(0f, 0f), spacing: 14f, UiAlign.Centre, manageRows))
                .Inactive();
            screen.ManageSavesPanel = manageSavesModal;

            // --- the confirmation gate on a destructive action -----------------
            //
            // A SIBLING of ManageSavesPanel and built after it, so it draws on
            // top and its dimmer swallows clicks meant for the Delete buttons
            // underneath. v1 achieved the same by nesting it and relying on
            // sibling order inside the panel; stating it at the root is the same
            // guarantee without the indirection.
            //
            // Note what does NOT need saying here: the label's colour. v1 shipped
            // this exact label invisible, because CreateText defaulted to black
            // and the panel behind it is near-black (AUDIT P0 #4). Ui.Label
            // defaults to white, so the bug has no way to recur.
            // THE HOLD wears the Crimson HQ plate instead of a flat track
            // rect -- the plate itself reads as the track, so only the
            // progress fill
            // survives as a child. Fill stays authored at holdSize, FULL
            // WIDTH and pivoted to its own left edge, unchanged from the flat
            // version: UiAudit refuses a zero-sized graphic, and growing
            // sizeDelta.x from a pinned left edge is what lets
            // ResetProgressController resize it rightward with one number
            // (HoldFillMath.SetFill). It sits inside a clip panel sized to
            // the plate's own measured paint (PlateVisiblePad) so the red
            // rect can never bleed past the border the art already draws --
            // "clipped/scaled over the plate" is the actual mechanism, not
            // just a description of it. ThemedPlate()+LayerCaptionWithVisuals
            // states the fill and the plate are one widget, the same
            // relationship Themed()'s own Visuals/Label pair states.
            //
            // Quiet() stays for the reason it always did: a Button plays its
            // click on RELEASE whatever the press was for, and a hold
            // released early deliberately did nothing.
            var holdSize = new UiVec(ResetHoldWidth, ResetHoldHeight);
            var holdPlateShape = Ui.PlateShapeFor(holdSize.X, holdSize.Y);
            var holdPlatePad = Ui.PlateVisiblePad(holdPlateShape);
            var holdFillClip = Ui.Panel("ResetConfirmYesFillClip",
                    Place.Stretch(holdSize.X * holdPlatePad.Left, holdSize.X * holdPlatePad.Right,
                        holdSize.Y * holdPlatePad.Bottom, holdSize.Y * holdPlatePad.Top),
                    UiSize.Fill)
                .Clipping();
            var holdFill = Ui.Solid("ResetConfirmYesFill", "#E0786E4D", holdSize,
                    Place.At(-holdSize.X * 0.5f, 0f, new UiVec(0f, 0.5f)))
                .AsDecor()
                .AllowOverflow(
                    "authored at the button's full holdSize (TheHoldFillIsAuthoredAtItsFullSize pins this) so " +
                    "HoldFillMath can only ever shrink it, which means it starts a fraction of a pixel past the " +
                    "clip panel's own plate-inset edge - the clip panel is what actually keeps it off the " +
                    "painted border at runtime, not this node's own declared size");
            holdFillClip.Children.Add(holdFill);

            // RUNTIME on the button itself: ThemedPlate() is CaptionPreserving
            // -- unlike Themed(), it builds no <name>Label from the text
            // passed to Ui.Button (Ui.ApplyThemePlateOnly only ever adds
            // Visuals), so a real UiString there was being silently
            // discarded. This regressed the caption entirely: the button
            // held no visible word at all after the Crimson-plate conversion.
            // Declared as its own child instead, named ResetConfirmYesCaption
            // so Ui.CaptionOf finds it (the <name>Caption convention
            // OutlineButton's retirement comment states), styled ButtonLabel
            // like every other themed caption, and added AFTER holdFillClip
            // so it draws above the hold fill -- UiEmitter's child order is
            // draw order.
            var holdButton = Ui.Button("ResetConfirmYesButton", UiString.Runtime, holdSize, 20)
                .Quiet()
                .ThemedPlate(ButtonTheme.Crimson);
            holdButton.Children.Add(holdFillClip);
            var holdCaption = Ui.Label("ResetConfirmYesCaption", UiStrings.Delete, holdSize, 20,
                    place: Place.At(0f, 0f))
                .Styled(TypographyRole.ButtonLabel);
            holdButton.Children.Add(holdCaption);
            holdButton.LayerCaptionWithVisuals(holdFillClip, holdCaption);

            var confirmYes = holdButton;

            // Matches ResetConfirmYesButton's own resized height (both were
            // 200x60, 11.1% off Legacy's true 3:1) so the hold and its
            // cancel sibling stay the same height in their Row.
            var confirmNo = Ui.Button("ResetConfirmNoButton", UiStrings.Cancel,
                    new UiVec(ResetHoldWidth, ResetHoldHeight), 20)
                .Themed(ButtonTheme.Silver);
            var confirmLabel = Ui.Label("ResetConfirmLabel", UiStrings.ConfirmDelete, new UiVec(760f, 50f), 22)
                .Styled(TypographyRole.Body);

            screen.ResetConfirmYesButton = holdButton;
            screen.ResetConfirmYesFill = holdFill;
            screen.ResetConfirmNoButton = confirmNo;
            screen.ResetConfirmLabel = confirmLabel;

            var confirmModal = Ui.Modal("ResetConfirmPanel", "#000000EB",
                Ui.Column("ResetConfirmColumn", Place.At(0f, 0f), spacing: 28f, UiAlign.Centre,
                    confirmLabel,
                    Ui.Row("ResetConfirmButtons", Place.Flow, spacing: 40f, UiAlign.Centre,
                        confirmYes, confirmNo)))
                .Inactive();
            screen.ResetConfirmPanel = confirmModal;

            screen.Root = Ui.Panel("MainMenuPanel", UiSize.Fill,
                sceneLayer, leftScrim, title, menuColumn,
                saveSlotModal, manageSavesModal, confirmModal);

            return screen;
        }
    }
}
