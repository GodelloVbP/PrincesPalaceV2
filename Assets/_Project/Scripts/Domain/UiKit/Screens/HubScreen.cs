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

        // Sizes and positions now come from HubAnchors, which stages them at
        // depth over the void rather than laying them out as a grid. The gate
        // keeps its "unmistakably primary" job and gets bigger still: it is the
        // one thing standing on solid ground.

        public UiNode Root;

        public NodeRef TalentsButton;
        public NodeRef PrincipalityButton;
        public NodeRef CharacterSheetButton;
        public NodeRef RelicsButton;
        public NodeRef StartRunButton;
        public NodeRef MainMenuButton;
        public NodeRef CurrencyLabel;
        public NodeRef StartRunCaption;
        public NodeRef World;

        // The character overlay lives in the hub permanently and hidden.
        // Declared LAST so it draws over everything it dims.
        public CharacterOverlayScreen Overlay;

        // Over even the overlay: a debug tool has to be reachable from
        // whatever state the game is wedged in, and being covered by the very
        // screen you are debugging is the one place it must not be.
        public DebugMenuScreen Debug;

        // The start-of-run relic draft. Mounted here rather than as its own
        // scene because the hub is where a descent begins, and because the
        // Descend button on it is the only way past -- a draft you can
        // navigate around is not a draft.
        public RelicDraftScreen Draft;

        // What the Relics building opens now. That building has been dimmed
        // and unpressable since the hub was built, because relics were never
        // a screen's worth of thing on their own -- a record of ALL content is.
        public GlossaryScreen Glossary;

        public HubAmbience.Layer Ambience;

        // Node -> Resources folder, for the runtime frame looper. Recorded at
        // build time from the same key the sprite was given, so an animation
        // cannot end up playing a different building's frames.
        public readonly List<(NodeRef Node, string Folder)> BuildingArt = new List<(NodeRef, string)>();

        public static HubScreen Build()
        {
            var screen = new HubScreen();

            // Declared FAR TO NEAR: declaration order is painter's order, so a
            // nearer building draws over a further one with nothing sorting
            // anything -- the fight stage's rule, reused.
            var relics = screen.Staged("RelicsBuilding", UiStrings.HubRelics, "empty_plot", HubAnchors.Relics);
            var talents = screen.Staged("TalentsBuilding", UiStrings.HubTalents, "talents", HubAnchors.Talents);
            var characterSheet = screen.Staged("CharacterSheetBuilding", UiStrings.HubCharacterSheet, "character_sheet", HubAnchors.CharacterSheet);
            var principality = screen.Staged("PrincipalityBuilding", UiStrings.HubPrincipality, "principality", HubAnchors.Principality);

            // The gate STANDS ON THE TERRACE, feet on the path, while everything
            // else floats out over the drop. Its caption is a declared node of
            // its own rather than the emitter's auto-label, because the
            // controller swaps it between BEGIN DESCENT and RESUME -- and
            // because the arch's centre is a swirling void the eye needs to read
            // as an opening, so the words hang below the plinths.
            var gateCaption = Ui.Label("StartRunGateCaption", UiString.Runtime, new UiVec(420f, 44f), 22,
                    "#F2DB9E", Place.At(0f, HubAnchors.GateCaptionOffset))
                .AllowOverflow("the gate's nameplate hangs below the plinths - inside the arch it would fill the void the eye needs to read as an opening");

            var gate = Ui.Button("StartRunGate", UiString.Runtime,
                new UiVec(HubAnchors.GateSize, HubAnchors.GateSize), 22,
                Place.At(HubAnchors.Gate.X, HubAnchors.Gate.Y, new UiVec(0.5f, 0f)));
            gate.SpriteKey = $"{HubArtRoot}/gate/f0.png";
            screen.BuildingArt.Add((gate, "Hub/gate"));
            gate.Children.Add(gateCaption);

            screen.TalentsButton = talents.Button;
            screen.PrincipalityButton = principality.Button;
            screen.CharacterSheetButton = characterSheet.Button;
            screen.RelicsButton = relics.Button;
            screen.StartRunButton = gate;
            screen.StartRunCaption = gateCaption;

            var currency = Ui.Label("CurrencyLabel", UiStrings.HubWallet, new UiVec(500f, 80f), 20, "#F2DB9E",
                Place.At(0f, 0f));
            screen.CurrencyLabel = currency;

            var mainMenu = Ui.Button("MainMenuButton", UiStrings.HubMainMenu, new UiVec(220f, 60f), 16,
                Place.At(-830f, 480f));
            screen.MainMenuButton = mainMenu;

            var ambience = HubAmbience.Build();
            screen.Ambience = ambience;

            // The WORLD, wrapped. Everything staged lives in here so one handle
            // can drift or settle the whole place; the chrome outside it stays
            // put, which is the split MainMenu's SceneLayer already uses.
            // A FIXED 1920x1080 stage, not a stretch.
            //
            // The composition is authored in reference coordinates, so the world
            // has to keep them at every frame -- stretched to fit, a 900-tall
            // window pushed the far buildings out of their own parent and the
            // gate through the floor. Fixed, the place simply crops at other
            // aspects, exactly as a full-bleed background does, and the audit
            // measures the coordinates the design actually uses.
            var world = Ui.Panel("HubWorld", Place.At(0f, 0f), UiSize.Fixed(1920f, 1080f),
                Ui.Sprite("HubBackground", BackgroundKey, Place.Stretch(), UiSize.Fill).AsDecor(),
                relics.Node, talents.Node, characterSheet.Node, principality.Node, gate,

                // LAST inside the world, so every glow sits over the thing it
                // lights rather than behind it.
                ambience.Root)
                .AllowOverlap("the world is a full-frame coordinate layer; the chrome siblings sit on top of it by design")
                .AllowOverflow("the world is the 1920x1080 reference stage and crops at other aspects, exactly as its own full-bleed background does");

            screen.World = world;

            var overlay = CharacterOverlayScreen.Build();
            screen.Overlay = overlay;

            var debug = DebugMenuScreen.Build();
            screen.Debug = debug;

            var draft = RelicDraftScreen.Build();
            screen.Draft = draft;

            var glossary = GlossaryScreen.Build();
            screen.Glossary = glossary;

            screen.Root = Ui.Panel("HubPanel", UiSize.Fixed(1920f, 1080f),
                world,

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
                    Ui.Label("HubSubtitleLabel", UiStrings.HubSubtitle, new UiVec(600f, 32f), 18, "#B8A8D9"))
                    .AsDecor(),

                // Lighter than it looks like it should be. v1's first pass made
                // this near-identical to the background it sits on, so the plate
                // vanished and the numbers read as text floating in space -
                // found by rendering the panel and looking at it, which no test
                // would have caught.
                // Widened for a third currency: Embers is what talents actually
                // cost and it was invisible here.
                Ui.Panel("CurrencyPlate", Place.At(620f, 470f), UiSize.Fixed(520f, 90f), currency)
                    .Coloured("#2C1C42E0").AsDecor(),

                mainMenu,
                overlay.Root,
                debug.Root,
                draft.Root,
                glossary.Root);

            return screen;
        }

        private readonly struct BuildingNodes
        {
            public readonly UiNode Node;
            public readonly NodeRef Button;
            public BuildingNodes(UiNode node, NodeRef button) { Node = node; Button = button; }
        }

        // A building is a button wearing its art, staged at depth.
        //
        // Bottom-centre pivot, so it grows upward from wherever its plot puts
        // it rather than about its own middle -- the fight stage's convention,
        // and what makes a floating rock read as hanging rather than centred.
        // The size is BAKED from the depth rather than applied as a transform
        // scale, because the audit measures declared boxes and a full-size node
        // scaled down would be reported as overlapping things it never touches.
        private BuildingNodes Staged(string name, UiString caption, string art, HubAnchors.Plot plot)
        {
            var position = HubAnchors.PositionFor(plot);
            float size = HubAnchors.SizeFor(plot);

            // The button's own text is RUNTIME-EMPTY and the caption is a
            // declared node BELOW the art.
            //
            // The emitter's auto-label centres on the button, which put
            // "PRINCIPALITY" across the stall's awning and "TALENTS" through the
            // trunk of the tree -- every building wearing its own name like a
            // sticker. A staged object gets a nameplate under it, which is the
            // same thing the fight stage does for its combatants and for the
            // same reason.
            var button = Ui.Button(name, UiString.Runtime, new UiVec(size, size), 22,
                Place.At(position.X, position.Y, new UiVec(0.5f, 0f)));
            button.SpriteKey = $"{HubArtRoot}/{art}/f0.png";
            BuildingArt.Add((button, $"Hub/{art}"));

            // Scaled with the plot: a distant building's nameplate has to read
            // as being at that distance too, or the depth staging is undone by
            // the type.
            float scale = HubAnchors.ScaleFor(plot);
            // Tall enough for TWO LINES. "CHARACTER SHEET" wraps at this width
            // and needs 49px; a 34px plate failed E1 and took the whole scene
            // build down with it.
            var plate = Ui.Label($"{name}Caption", caption, new UiVec(size * 1.4f, 56f),
                    (int)(20f * scale + 0.5f), "#EDE6FF",
                    Place.At(0f, HubAnchors.CaptionOffsetFor(size, plot.ContentBottom)))
                .AllowOverflow("a nameplate hangs BELOW the thing it names - inside the art it reads as a label printed on the building");

            button.Children.Add(plate);
            return new BuildingNodes(button, button);
        }
    }
}
