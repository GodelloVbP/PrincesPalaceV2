using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // What the main menu needs from outside Domain.
    //
    // Domain cannot see SaveSystem (Core), so the slot count arrives as an
    // input rather than being restated here. That matters: this screen builds
    // two separate things off that number -- slot cards and their delete
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
        // Build -- SaveSlotController (Core) needs the same width to
        // resize the fill by, and a number typed twice is a number that can
        // disagree with itself. See ExitsLayout.HoldWidth for the identical
        // reason that constant is public.
        public const float ResetHoldWidth = 200f;

        // static readonly, not const, because Ui.PlateNominalSizeFor is not
        // a compile-time constant -- SaveSlotController and MainMenuScreenTests both
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
        public NodeRef SettingsButton;
        public NodeRef ExitButton;

        // The Options pane, unchanged, hosted in a modal over the menu.
        // Wired by ScreenRegistry.WireOptions, the same call the system menu's
        // Options tab goes through, so both surfaces share one controller type.
        public OptionsScreen Options;
        public NodeRef SettingsPanel;
        public NodeRef CloseSettingsButton;

        // The Load list: one row per slot, each a card and its own Delete.
        public NodeRef SaveSlotPanel;
        public NodeRef CloseSaveSlotButton;

        public NodeRef ResetConfirmPanel;
        public NodeRef ResetConfirmLabel;

        // A HOLD, not a click -- the same 1.2s track-and-fill ExitsScreen's
        // own abandon button uses for the one other irreversible action in
        // the game. ResetConfirmYesButton takes the hold; the fill is exposed
        // separately because the controller resizes it every frame the hold
        // is live, which the button reference alone cannot do.
        public NodeRef ResetConfirmYesButton;
        public NodeRef ResetConfirmYesFill;
        public NodeRef ResetConfirmNoButton;

        // A slot card is 5:1, exactly the FiveByOne plate's own aspect, so the
        // plate is drawn unstretched (ThemedButtonAspectLintTests). Height is
        // the budget the eight-slot audit (TheMenuAuditsCleanAtOtherSlotCounts,
        // a roster larger than the shipped SaveSystem.SlotCount) has to fit
        // in 1080; width is what the row has left beside the Delete.
        public static readonly UiVec CardSize = Ui.PlateNominalSizeFor(560f, 112f);

        // Delete sits to the right of its card, a sibling Button, not a child
        // of the card's Button.
        public static readonly UiVec DeleteSize = Ui.PlateNominalSizeFor(128f, 44f);
        private const float RowSpacing = 12f;

        // One slot's card content: the number badge, its two lines of text
        // and the gold figure. Each of the four is UiString.Runtime and set by
        // SaveSlotController.Refresh.
        //
        // A STRUCT PER SLOT rather than four more parallel List<NodeRef>
        // fields: lists built from the SAME loop iteration as SlotButtons
        // cannot drift in count from each other, so there is nothing for E4 to
        // check here, and parallel lists are just more chances to index one
        // of them wrong.
        public readonly struct SlotCardRefs
        {
            public readonly NodeRef Number;
            public readonly NodeRef Top;
            public readonly NodeRef Detail;
            public readonly NodeRef Gold;

            public SlotCardRefs(NodeRef number, NodeRef top, NodeRef detail, NodeRef gold)
            {
                Number = number;
                Top = top;
                Detail = detail;
                Gold = gold;
            }
        }

        public readonly List<NodeRef> SlotButtons = new List<NodeRef>();
        public readonly List<SlotCardRefs> ChooseCards = new List<SlotCardRefs>();

        // Shown only on a filled slot: SaveSlotController.Refresh toggles it.
        public readonly List<NodeRef> DeleteButtons = new List<NodeRef>();

        // Builds one slot's card content as children of `host`, the card's
        // Button, which alone carries the click. All decor, positioned
        // relative to the card's centre.
        //
        // EVERY TEXT NODE IS UiString.Runtime: Domain has no SaveSystem to
        // ask which slots are filled or what a save's character is called.
        //
        // THREE COLUMNS, LEFT TO RIGHT, each placed by its own EDGES: a
        // Runtime label has no authored copy for the text-fit audit (E1) to
        // measure, so a column that drifted into its neighbour would ship
        // silently. The card is -280..280; number badge -266 to -210, text
        // column -205 to 100, gold column 115 to 265.
        private static SlotCardRefs AddCardContent(UiNode host, string stem)
        {
            const float NumberBadgeCentreX = -238f;
            const float TextColumnLeftX = -205f;
            const float TextColumnRightX = 100f;
            const float TextColumnWidth = TextColumnRightX - TextColumnLeftX;
            const float TextColumnCentreX = (TextColumnLeftX + TextColumnRightX) * 0.5f;
            const float GoldColumnCentreX = 190f;
            const float GoldColumnWidth = 150f;

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

            var gold = Ui.Label($"{stem}Gold", UiString.Runtime, new UiVec(GoldColumnWidth, 28f), 20,
                    "#E3C166", Place.At(GoldColumnCentreX, 0f))
                .AsDecor()
                .TextAligned(UiTextAlign.Right);
            host.Children.Add(gold);

            return new SlotCardRefs(number, top, detail, gold);
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
            var settings = Ui.Button("SettingsButton", UiStrings.Settings, itemSize, 24)
                .Themed(ButtonTheme.Gold);
            var quit = Ui.Button("ExitButton", UiStrings.Quit, itemSize, 24)
                .Themed(ButtonTheme.Silver);
            screen.NewRunButton = newRun;
            screen.LoadButton = load;
            screen.SettingsButton = settings;
            screen.ExitButton = quit;

            // Top-aligned under the title, left edge shared with it. The
            // column's centre y is authored so its top sits at y = 190.
            const float MenuSpacing = 20f;
            const float MenuTop = 190f;
            float menuHeight = continueSize.Y + itemSize.Y * 4f + MenuSpacing * 4f;
            var menuColumn = Ui.Column("MenuButtons",
                Place.At(LeftEdgeX + MenuWidth * 0.5f, MenuTop - menuHeight * 0.5f), MenuSpacing,
                UiAlign.Start, continueButton, newRun, load, settings, quit);
            screen.MenuColumn = menuColumn;

            // --- save slots ---------------------------------------------------
            var slotChildren = new List<UiNode>
            {
                Ui.Label("SaveSlotTitle", UiStrings.ChooseSlotHeader, new UiVec(400f, 44f), 28)
                    .Styled(TypographyRole.FunctionalHeading)
            };

            for (int i = 0; i < slotCount; i++)
            {
                // The plate is the card's own background: SaveSlotController
                // drives filled/empty through SetMenuState(Primary/Idle).
                var button = Ui.Button($"Slot{i}Button", UiString.Runtime, CardSize, 20)
                    .ThemedPlate(ButtonTheme.Gold);
                screen.SlotButtons.Add(button);
                var card = AddCardContent(button, $"Slot{i}");
                screen.ChooseCards.Add(card);
                button.LayerCaptionWithVisuals(card.Number.Node, card.Top.Node, card.Detail.Node, card.Gold.Node);

                var delete = Ui.Button($"Slot{i}DeleteButton", UiStrings.Delete, DeleteSize, 18)
                    .Themed(ButtonTheme.Crimson);
                screen.DeleteButtons.Add(delete);

                slotChildren.Add(Ui.Row($"Slot{i}Row", Place.Flow, RowSpacing, UiAlign.Centre, button, delete));
            }

            // 200x50 was 4:1 against the FiveByOne plate's true 5:1 --
            // ThemedButtonAspectLintTests. Height down to nominal, width kept.
            var footerButtonSize = Ui.PlateNominalSizeFor(200f, 50f);
            var cancel = Ui.Button("CloseSaveSlotButton", UiStrings.Cancel, footerButtonSize, 18)
                .Closes()
                .Themed(ButtonTheme.Silver);
            screen.CloseSaveSlotButton = cancel;
            slotChildren.Add(cancel);

            // 10: at the roster size TheMenuAuditsCleanAtOtherSlotCounts(8)
            // checks (a roster the game does not currently field, but the
            // screen's own arithmetic has to hold for), the column needs this
            // tight a spacing to fit the 1080-tall screen.
            var saveSlotModal = Ui.Modal("SaveSlotPanel", "#000000D9",
                Ui.Column("SaveSlotColumn", Place.At(0f, 0f), spacing: 10f, UiAlign.Centre, slotChildren))
                .Inactive();
            screen.SaveSlotPanel = saveSlotModal;

            // --- settings -----------------------------------------------------
            //
            // OptionsScreen.Build() is a bare pane: in the system menu it sits
            // on that frame's fill and rim, so the modal supplies its own
            // (same tokens) and a Close under it. The stack is offset up so
            // the Close fits between the frame and the screen's bottom edge.
            var options = OptionsScreen.Build();
            screen.Options = options;

            var paneSize = new UiVec(OptionsLayout.PaneWidth, OptionsLayout.PaneHeight);
            var frameChildren = new List<UiNode>
            {
                Ui.Solid("SettingsFill", "#1A1024F5", paneSize, Place.At(0f, 0f)).AsDecor(),
                options.Root,
            };
            frameChildren.AddRange(Ui.Rim("Settings", paneSize, FightHudPalette.BorderGold));

            const float SettingsFrameY = 50f;
            var settingsFrame = Ui.Panel("SettingsFrame", Place.At(0f, SettingsFrameY),
                UiSize.Fixed(paneSize.X, paneSize.Y), frameChildren);

            var settingsClose = Ui.Button("CloseSettingsButton", UiStrings.Close, footerButtonSize, 18,
                    Place.At(0f, SettingsFrameY - paneSize.Y * 0.5f - 12f - footerButtonSize.Y * 0.5f))
                .Closes()
                .Themed(ButtonTheme.Silver);
            screen.CloseSettingsButton = settingsClose;

            var settingsModal = Ui.Modal("SettingsPanel", "#0A0614ED",
                Ui.Panel("SettingsStack", Place.Stretch(), UiSize.Fill, settingsFrame, settingsClose))
                .Inactive();
            screen.SettingsPanel = settingsModal;

            // --- the confirmation gate on a destructive action -----------------
            //
            // A SIBLING of SaveSlotPanel and built after it, so it draws on
            // top and its dimmer swallows clicks meant for the Delete buttons
            // underneath.
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
            // SaveSlotController resize it rightward with one number
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
            var holdCaption = Ui.Label("ResetConfirmYesCaption", UiStrings.HoldToDelete, holdSize, 20,
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
                .Closes()
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
                saveSlotModal, settingsModal, confirmModal);

            return screen;
        }
    }
}
