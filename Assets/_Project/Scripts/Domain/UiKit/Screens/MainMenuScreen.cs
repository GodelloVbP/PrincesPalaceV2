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
        public const float ResetHoldHeight = 60f;

        public UiNode Root;

        public NodeRef SceneLayer;
        public MainMenuAmbience.Layer Ambience;

        public NodeRef TitleLabel;

        // BUILT UNCONDITIONALLY, shown only sometimes. Domain has no
        // SaveSystem to ask at build time whether there is anything to
        // continue into, so the node always exists and MainMenuController
        // decides at runtime whether to show it -- the same split every other
        // screen-can't-know-yet fact in this codebase uses.
        //
        // NOT a child of the Play/Exit column, on purpose. A flow container's
        // remaining children do not reflow when one of them is hidden, so a
        // conditionally-visible element inside one would leave a gap-shaped
        // hole the size of a button whenever there was nothing to continue.
        // Positioned on its own instead: hiding it just means the menu opens
        // on Play, which is the graceful-degradation answer this house
        // already gives to a missing thing everywhere else.
        public NodeRef ContinueButton;

        public NodeRef PlayButton;
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

        public readonly List<NodeRef> SlotButtons = new List<NodeRef>();
        public readonly List<NodeRef> SlotLabels = new List<NodeRef>();
        public readonly List<NodeRef> DeleteButtons = new List<NodeRef>();

        public static MainMenuScreen Build(MainMenuInputs inputs)
        {
            var screen = new MainMenuScreen();
            var slotSize = new UiVec(300f, 60f);
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

            // --- the wordmark --------------------------------------------------
            //
            // Never existed before this: the shipped menu had a painted world
            // and three buttons floating on it, with nothing saying what game
            // this is. Gold to match the plaques it sits above.
            //
            // LEFT-ANCHORED, not centred on the canvas. The painting's own
            // subject -- the palace, floating mid-frame -- sits right of
            // centre; a centred column lands on top of it and on the water's
            // reflection below it. The left third is comparatively empty sky
            // and stair, which is where every element below is anchored to.
            //
            // ONE SHARED LEFT EDGE, not one shared centre -- LeftEdgeX plus
            // each element's own half-width, so a narrower button (Play, Exit)
            // and a wider one (Continue, which carries a slot number) start
            // flush with each other and with the title above them rather than
            // centring under it.
            const float LeftEdgeX = -810f;

            var title = Ui.Label("TitleLabel", UiStrings.GameTitle, new UiVec(760f, 100f), 64,
                    "#E3C166", Place.At(LeftEdgeX + 380f, 260f))
                .Tracked(5f)
                .TextAligned(UiTextAlign.Left);
            screen.TitleLabel = title;

            // A FLAT SCRIM, not a gradient -- this DSL's procedural sprites
            // have no linear-gradient generator, and a solid rectangle behind
            // the text block is the same technique every modal dimmer on this
            // screen already uses, just far more transparent. Sized to the
            // text column plus a margin, not the whole canvas: it exists to
            // keep gold text legible against a busy painting, not to darken
            // the palace itself.
            var scrim = Ui.Solid("TextScrim", "#03072B8C", new UiVec(900f, 560f),
                    Place.At(LeftEdgeX + 380f, 60f))
                .AsDecor();

            // --- the menu itself ---------------------------------------------
            //
            // Continue sits ABOVE the column rather than in it -- see the field
            // comment on why it is not a fourth Column child.
            var continueButton = Ui.Button("ContinueButton", UiStrings.Continue,
                new UiVec(340f, 66f), 24, Place.At(LeftEdgeX + 170f, 90f));
            screen.ContinueButton = continueButton;

            var play = Ui.Button("PlayButton", UiStrings.Play, new UiVec(260f, 60f), 24);
            var exit = Ui.Button("ExitButton", UiStrings.Exit, new UiVec(260f, 60f), 24);
            screen.PlayButton = play;
            screen.ExitButton = exit;

            var menuColumn = Ui.Column("MenuButtons", Place.At(LeftEdgeX + 130f, -60f), spacing: 20f,
                UiAlign.Centre, play, exit);

            // --- save slots ---------------------------------------------------
            var slotChildren = new List<UiNode>
            {
                Ui.Label("SaveSlotTitle", UiStrings.ChooseSlotHeader, new UiVec(400f, 44f), 28)
            };

            for (int i = 0; i < slotCount; i++)
            {
                var button = Ui.Button($"Slot{i}Button", UiStrings.SlotButton, slotSize, 20);
                screen.SlotButtons.Add(button);
                slotChildren.Add(button);
            }

            var manageSaves = Ui.Button("ManageSavesButton", UiStrings.ManageSaves, new UiVec(200f, 50f), 18);
            var cancel = Ui.Button("CloseSaveSlotButton", UiStrings.Cancel, new UiVec(200f, 50f), 18);
            screen.ManageSavesButton = manageSaves;
            screen.CloseSaveSlotButton = cancel;

            // A Row, so the two footer actions cannot drift onto separate lines
            // the way two Column entries would once one of their labels grew.
            slotChildren.Add(Ui.Row("SaveSlotFooter", Place.Flow, spacing: 24f, UiAlign.Centre,
                manageSaves, cancel));

            var saveSlotModal = Ui.Modal("SaveSlotPanel", "#000000D9",
                Ui.Column("SaveSlotColumn", Place.At(0f, 0f), spacing: 16f, UiAlign.Centre, slotChildren))
                .Inactive();
            screen.SaveSlotPanel = saveSlotModal;

            // --- manage saves, the destructive half ----------------------------
            var manageRows = new List<UiNode>
            {
                Ui.Label("ManageSavesTitle", UiStrings.ManageSaves, new UiVec(400f, 50f), 32),
                Ui.Label("ManageSavesWarning", UiStrings.ManageSavesWarning, new UiVec(560f, 60f), 15,
                    "#A99BD4"),
            };

            for (int i = 0; i < slotCount; i++)
            {
                var label = Ui.Label($"ResetSlot{i}Label", UiStrings.SlotEmpty, new UiVec(300f, 36f), 18);
                var delete = Ui.Button($"ResetSlot{i}DeleteButton", UiStrings.Delete, new UiVec(140f, 36f), 16);
                screen.SlotLabels.Add(label);
                screen.DeleteButtons.Add(delete);

                // A Row, so the label and its Delete button cannot drift apart
                // and cannot collide however long the label gets.
                manageRows.Add(Ui.Row($"ResetSlot{i}Row", Place.Flow, spacing: 20f, UiAlign.Centre, label, delete));
            }

            var backToSlots = Ui.Button("CloseManageSavesButton", UiStrings.Back, new UiVec(260f, 50f), 20);
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
            // THE HOLD, built exactly the way ExitsScreen's abandon-hold is:
            // a track, a fill authored FULL WIDTH and pivoted to its own left
            // edge (UiAudit refuses a zero-sized graphic, and growing from a
            // pinned left edge is what lets the controller resize it rightward
            // as sizeDelta.x alone), and a chromeless, silent button on top
            // that takes the actual press -- silent because a Button plays the
            // shared click on RELEASE whatever the press was for, and a hold
            // released early deliberately did nothing.
            var holdSize = new UiVec(ResetHoldWidth, ResetHoldHeight);
            var holdTrack = Ui.Solid("ResetConfirmYesTrack", FightHudPalette.Track, holdSize,
                    Place.At(0f, 0f))
                .AsDecor();
            var holdFill = Ui.Solid("ResetConfirmYesFill", "#E0786E4D", holdSize,
                    Place.At(-holdSize.X * 0.5f, 0f, new UiVec(0f, 0.5f)))
                .AsDecor();
            var holdButton = Ui.Button("ResetConfirmYesButton", UiStrings.Delete, holdSize, 20,
                    Place.At(0f, 0f))
                .NoChrome()
                .Quiet();

            var confirmYes = Ui.Panel("ResetConfirmYesGroup", Place.Flow, UiSize.Fixed(holdSize),
                holdTrack, holdFill, holdButton);

            var confirmNo = Ui.Button("ResetConfirmNoButton", UiStrings.Cancel, new UiVec(200f, 60f), 20);
            var confirmLabel = Ui.Label("ResetConfirmLabel", UiStrings.ConfirmDelete, new UiVec(760f, 50f), 22);

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
                sceneLayer, scrim, title, continueButton, menuColumn,
                saveSlotModal, manageSavesModal, confirmModal);

            return screen;
        }
    }
}
