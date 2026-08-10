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

        public UiNode Root;

        public NodeRef SceneLayer;
        public MainMenuAmbience.Layer Ambience;

        public NodeRef PlayButton;
        public NodeRef OptionsButton;
        public NodeRef ExitButton;

        public NodeRef SaveSlotPanel;
        public NodeRef OptionsPanel;
        public NodeRef CloseSaveSlotButton;
        public NodeRef CloseOptionsButton;

        public NodeRef ResetConfirmPanel;
        public NodeRef ResetConfirmLabel;
        public NodeRef ResetConfirmYesButton;
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

            // --- the menu itself ---------------------------------------------
            var play = Ui.Button("PlayButton", UiStrings.Play, new UiVec(260f, 60f), 24);
            var options = Ui.Button("OptionsButton", UiStrings.Options, new UiVec(260f, 60f), 24);
            var exit = Ui.Button("ExitButton", UiStrings.Exit, new UiVec(260f, 60f), 24);
            screen.PlayButton = play;
            screen.OptionsButton = options;
            screen.ExitButton = exit;

            var menuColumn = Ui.Column("MenuButtons", Place.At(0f, -60f), spacing: 20f, UiAlign.Centre,
                play, options, exit);

            // --- save slots ---------------------------------------------------
            var slotChildren = new List<UiNode>();
            for (int i = 0; i < slotCount; i++)
            {
                var button = Ui.Button($"Slot{i}Button", UiStrings.SlotButton, slotSize, 20);
                screen.SlotButtons.Add(button);
                slotChildren.Add(button);
            }

            var cancel = Ui.Button("CloseSaveSlotButton", UiStrings.Cancel, slotSize, 20);
            screen.CloseSaveSlotButton = cancel;
            slotChildren.Add(cancel);

            var saveSlotModal = Ui.Modal("SaveSlotPanel", "#000000D9",
                Ui.Column("SaveSlotColumn", Place.At(0f, 0f), spacing: 16f, UiAlign.Centre, slotChildren))
                .Inactive();
            screen.SaveSlotPanel = saveSlotModal;

            // --- options, including the destructive half ----------------------
            var optionRows = new List<UiNode>
            {
                Ui.Label("OptionsTitle", UiStrings.OptionsTitle, new UiVec(400f, 50f), 32),
                Ui.Label("ResetProgressHeader", UiStrings.ResetProgressHeader, new UiVec(400f, 40f), 24),
            };

            for (int i = 0; i < slotCount; i++)
            {
                var label = Ui.Label($"ResetSlot{i}Label", UiStrings.SlotEmpty, new UiVec(300f, 36f), 18);
                var delete = Ui.Button($"ResetSlot{i}DeleteButton", UiStrings.Delete, new UiVec(140f, 36f), 16);
                screen.SlotLabels.Add(label);
                screen.DeleteButtons.Add(delete);

                // A Row, so the label and its Delete button cannot drift apart
                // and cannot collide however long the label gets.
                optionRows.Add(Ui.Row($"ResetSlot{i}Row", Place.Flow, spacing: 20f, UiAlign.Centre, label, delete));
            }

            var closeOptions = Ui.Button("CloseOptionsButton", UiStrings.Close, new UiVec(260f, 50f), 20);
            screen.CloseOptionsButton = closeOptions;
            optionRows.Add(closeOptions);

            var optionsModal = Ui.Modal("OptionsPanel", "#000000D9",
                Ui.Column("OptionsColumn", Place.At(0f, 0f), spacing: 14f, UiAlign.Centre, optionRows))
                .Inactive();
            screen.OptionsPanel = optionsModal;

            // --- the confirmation gate on a destructive action -----------------
            //
            // A SIBLING of OptionsPanel and built after it, so it draws on top
            // and its dimmer swallows clicks meant for the Delete buttons
            // underneath. v1 achieved the same by nesting it and relying on
            // sibling order inside the panel; stating it at the root is the same
            // guarantee without the indirection.
            //
            // Note what does NOT need saying here: the label's colour. v1 shipped
            // this exact label invisible, because CreateText defaulted to black
            // and the panel behind it is near-black (AUDIT P0 #4). Ui.Label
            // defaults to white, so the bug has no way to recur.
            var confirmYes = Ui.Button("ResetConfirmYesButton", UiStrings.Delete, new UiVec(200f, 60f), 20);
            var confirmNo = Ui.Button("ResetConfirmNoButton", UiStrings.Cancel, new UiVec(200f, 60f), 20);
            var confirmLabel = Ui.Label("ResetConfirmLabel", UiStrings.ConfirmDelete, new UiVec(760f, 50f), 22);

            screen.ResetConfirmYesButton = confirmYes;
            screen.ResetConfirmNoButton = confirmNo;
            screen.ResetConfirmLabel = confirmLabel;

            var confirmModal = Ui.Modal("ResetConfirmPanel", "#000000EB",
                Ui.Column("ResetConfirmColumn", Place.At(0f, 0f), spacing: 28f, UiAlign.Centre,
                    confirmLabel,
                    Ui.Row("ResetConfirmButtons", Place.Flow, spacing: 40f, UiAlign.Centre,
                        confirmYes, confirmNo)))
                .Inactive();
            screen.ResetConfirmPanel = confirmModal;

            screen.Root = Ui.Panel("MainMenuPanel", UiSize.Fixed(1920f, 1080f),
                sceneLayer, menuColumn, saveSlotModal, optionsModal, confirmModal);

            return screen;
        }
    }
}
