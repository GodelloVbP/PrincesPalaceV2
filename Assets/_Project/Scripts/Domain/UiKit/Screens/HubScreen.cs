using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The Divine Principality: where a run is started from and meta-progression
    // is spent.
    //
    // Coordinates are v1's exactly, because the composition was tuned by eye
    // against the real background and is worth preserving. What changed is that
    // they are now DECLARED positions the audit checks, rather than arithmetic
    // scattered across a builder — and the two corner buttons, the currency
    // plate and the five buildings are all siblings in one tree, so a collision
    // between any pair of them fails the build.
    public sealed class HubScreen
    {
        public const string BackgroundKey = "Backgrounds/Divine_principality_nebula.png";

        // Hub building art is Resources-loaded at runtime in v1 (frame folders
        // for HubBuildingAnimator). Until that animator is ported, the first
        // frame is referenced directly as a static sprite.
        private const string HubArtRoot = "Assets/_Project/Resources/Hub";

        // The gate is 560 against the others' 360/340 — 56% bigger, so it reads
        // unmistakably as the primary action. v1's own note records that 500
        // was tried and lost the hierarchy.
        private const float GateSize = 560f;
        private const float BuildingSize = 360f;
        private const float SmallBuildingSize = 340f;

        public UiNode Root;

        public NodeRef TalentsButton;
        public NodeRef PrincipalityButton;
        public NodeRef CharacterSheetButton;
        public NodeRef RelicsButton;
        public NodeRef StartRunButton;
        public NodeRef MainMenuButton;
        public NodeRef CurrencyLabel;

        public static HubScreen Build()
        {
            var screen = new HubScreen();

            var talents = Building("TalentsBuilding", UiStrings.HubTalents, "talents", -620f, 200f, BuildingSize);
            var principality = Building("PrincipalityBuilding", UiStrings.HubPrincipality, "principality", 620f, 200f, BuildingSize);
            var characterSheet = Building("CharacterSheetBuilding", UiStrings.HubCharacterSheet, "character_sheet", -500f, -200f, SmallBuildingSize);

            // Relics has no hub art of its own yet, so it borrows the reserved
            // plot's. Deliberate: an Image with no sprite renders as a solid
            // WHITE QUAD, not as nothing, which is a bug class this project has
            // already shipped once.
            var relics = Building("RelicsBuilding", UiStrings.HubRelics, "empty_plot", 500f, -200f, SmallBuildingSize);
            var gate = Building("StartRunGate", UiStrings.HubStartRun, "gate", 0f, -170f, GateSize);

            screen.TalentsButton = talents.Button;
            screen.PrincipalityButton = principality.Button;
            screen.CharacterSheetButton = characterSheet.Button;
            screen.RelicsButton = relics.Button;
            screen.StartRunButton = gate.Button;

            var currency = Ui.Label("CurrencyLabel", UiStrings.WalletSummary, new UiVec(360f, 80f), 22, "#F2DB9E",
                Place.At(0f, 0f));
            screen.CurrencyLabel = currency;

            var mainMenu = Ui.Button("MainMenuButton", UiStrings.HubMainMenu, new UiVec(220f, 60f), 16,
                Place.At(-830f, 480f));
            screen.MainMenuButton = mainMenu;

            screen.Root = Ui.Panel("HubPanel", UiSize.Fixed(1920f, 1080f),
                Ui.Sprite("Background", BackgroundKey, Place.Stretch(), UiSize.Fill).AsDecor(),

                // Live text on top of painted ornament, never lettering baked
                // into the background: the background stretches non-uniformly
                // off 16:9, which would squash baked letterforms, and live text
                // stays localisable.
                //
                // A Column rather than two hand-placed y values. v1 had them at
                // 470 and 415, whose boxes overlap by exactly 1px - harmless
                // there, and a build failure here, which is the audit doing its
                // job. Stacking them removes the question instead of nudging a
                // number until it passes.
                Ui.Column("HubHeading", Place.At(0f, 445f), spacing: 4f, UiAlign.Centre,
                    Ui.Label("HubTitleLabel", UiStrings.HubTitle, new UiVec(1000f, 80f), 56, "#EDE6FF"),
                    Ui.Label("HubSubtitleLabel", UiStrings.HubSubtitle, new UiVec(600f, 32f), 18, "#B8A8D9")),

                talents.Node, principality.Node, characterSheet.Node, relics.Node, gate.Node,

                // Lighter than it looks like it should be. v1's first pass made
                // this near-identical to the background it sits on, so the plate
                // vanished and the numbers read as text floating in space -
                // found by rendering the panel and looking at it, which no test
                // would have caught.
                Ui.Panel("CurrencyPlate", Place.At(690f, 470f), UiSize.Fixed(380f, 90f), currency)
                    .Coloured("#2C1C42E0"),

                mainMenu);

            return screen;
        }

        private readonly struct BuildingNodes
        {
            public readonly UiNode Node;
            public readonly NodeRef Button;
            public BuildingNodes(UiNode node, NodeRef button) { Node = node; Button = button; }
        }

        // A building is a button wearing its art: the sprite fills the button,
        // the caption sits on top. Declared as one thing so the caption cannot
        // drift away from the art it names.
        private static BuildingNodes Building(string name, UiString caption, string art, float x, float y, float size)
        {
            var button = Ui.Button(name, caption, new UiVec(size, size), 22, Place.At(x, y));
            button.SpriteKey = $"{HubArtRoot}/{art}/f0.png";
            return new BuildingNodes(button, button);
        }
    }
}
