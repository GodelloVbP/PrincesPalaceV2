using System.Collections.Generic;
using PrincesPalace.Domain.Talents;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The constellations: three skies per character, one orb per talent.
    //
    // Every position comes from ConstellationLayout and TalentSkeleton, which
    // are both already tested -- so this file declares STRUCTURE and nothing
    // else. The three pages are laid out side by side a full screen apart and
    // slid between, which is what makes them read as three PLACES rather than
    // three tabs.
    public sealed class TalentScreen
    {
        // ---- the stones ----------------------------------------------------------
        //
        // SIX STATES, FIVE SPRITES, NO RUNTIME FILTERING. The design states each
        // non-kindled state as a CSS filter over the unlit stone -- ash is
        // grayscale(.94) brightness(.70) contrast(1.18), reachable is
        // brightness 1.28 -- and uGUI has no equivalent: Image.color multiplies,
        // so it can darken and tint but can neither desaturate nor brighten past
        // white. Its own handoff settled it (§12): the variants are BAKED,
        // delivered beside the sources, applying those exact formulas once.
        //
        // Which means Image.color here only ever multiplies white, and the state
        // a stone is in is the sprite it wears rather than a tint laid over one.
        // ASH IS A MATERIAL, NOT AN OPACITY -- dimming the purple only ever read
        // as far away, which is a depth cue in a screen that has no depth.
        public const string OrbUnlitKey = "UI/TalentTree/orb-unlit.png";
        public const string OrbLitKey = "UI/TalentTree/orb-lit-core.png";
        public const string OrbAshKey = "UI/TalentTree/orb-ash.png";
        public const string OrbAshUnauthoredKey = "UI/TalentTree/orb-ash-unauthored.png";
        public const string OrbReachableKey = "UI/TalentTree/orb-reachable.png";
        public const string OrbCostlyKey = "UI/TalentTree/orb-costly.png";

        // Soft along its short axis, so a limb fades at its sides rather than
        // ending on a hard band.
        public const string EdgeStripeKey = "proc:soft_edge_stripe";

        // A DEAD CONDUIT, A WARM HALO, A WHITE-HOT CORE.
        //
        // Not bark. v1's kit was painted as gnarled branches and
        // branch_tile_set is still sitting in Art/UI/TalentTree unreferenced
        // because of it -- these limbs are lines carrying current, so an unlit
        // one is a cold wire rather than a piece of wood, and everything that
        // happens to it is light.
        private const string EdgeDim = "#33263D";
        private const string EdgeGlow = "#FF91455A";
        private const string EdgeCore = "#FFD98C";
        private const string EdgeSpark = "#FFEBC0E6";

        public const string BackgroundKey = "Assets/_Project/Art/Backgrounds/Divine_principality_nebula.png";

        // One orb per slot per path. Declared rather than pooled: the skeleton
        // is a fixed shape, so every position is known at build time and the
        // audit can check all of them.
        // static readonly rather than const, because the skeleton derives its
        // own slot count from the rows it is made of now. Nothing needs this
        // at compile time -- it sizes arrays and bounds loops, both of which
        // are happy with a value read at type-init.
        public static readonly int OrbCount = TalentSkeleton.SlotCount;

        public UiNode Root;

        public NodeRef Sky;
        public NodeRef CharacterName;
        public NodeRef EmberCount;
        public NodeRef PathName;
        public NodeRef PrevPathButton;
        public NodeRef NextPathButton;
        public NodeRef PrevCharacterButton;
        public NodeRef NextCharacterButton;
        public NodeRef BackButton;

        public NodeRef Panel;
        public NodeRef PanelKicker;
        public NodeRef PanelPrice;
        public NodeRef PanelRefusal;
        public NodeRef PanelMeter;
        public NodeRef PanelMeterFill;

        public NodeRef RespecDialog;
        public NodeRef RespecDialogBody;
        public NodeRef RespecConfirmButton;
        public NodeRef RespecCancelButton;

        public List<NodeRef> StarFields = new List<NodeRef>();
        public List<NodeRef> CloudWashes = new List<NodeRef>();
        public List<NodeRef> DustMotes = new List<NodeRef>();
        public List<NodeRef> ShootingStars = new List<NodeRef>();

        public NodeRef DetailName;
        public NodeRef DetailBody;
        public NodeRef InvestButton;
        public NodeRef InvestLabel;
        public NodeRef RespecButton;

        // Indexed [path * OrbCount + slot]. One flat list rather than a list of
        // lists, because the wiring binds arrays and E4 counts them.
        public List<NodeRef> Orbs = new List<NodeRef>();
        public List<NodeRef> OrbGlows = new List<NodeRef>();

        // The reveal: a mask per orb, holding the LIT medallion. The mask is
        // what grows when a talent is kindled; the orb inside it is held at
        // full size and full strength the whole way, which is the difference
        // between something arriving and something switching on.
        // A stone's own furniture, one entry per orb in the same order.
        public List<NodeRef> OrbAuras = new List<NodeRef>();
        public List<NodeRef> OrbCores = new List<NodeRef>();
        public List<NodeRef> OrbRings = new List<NodeRef>();
        public List<NodeRef> OrbLabels = new List<NodeRef>();
        public List<NodeRef> OrbPrices = new List<NodeRef>();

        // The two gated slots per path, and nothing else -- these lists are
        // SHORTER than the orb lists and are indexed by their own position.
        // CollarSlots maps that position back to an orb index, which is the only
        // way the controller can know which stone a collar belongs to without
        // re-deriving the skeleton.
        public List<int> CollarSlots = new List<int>();
        public List<NodeRef> CollarTracks = new List<NodeRef>();
        public List<NodeRef> CollarFills = new List<NodeRef>();
        public List<NodeRef> CollarCounts = new List<NodeRef>();
        public List<NodeRef> Edges = new List<NodeRef>();

        // The lit half of each edge, in the SAME order as Edges. Switched off
        // at build time and raised by the controller for the connections the
        // player has actually earned -- so an unspent tree draws as dim limbs
        // and investing is what lights the path behind it.
        public List<NodeRef> EdgeGlows = new List<NodeRef>();

        // Which slot each edge arrives at, parallel to Edges. The controller
        // lights an edge when its CHILD is invested: the parent necessarily
        // already is, since that is what the prerequisite means, so the child
        // alone answers it and nothing has to re-walk the skeleton at runtime.
        public List<int> EdgeChildSlots = new List<int>();

        // The two animated layers, and each edge's own length. The components
        // that drive them live in Core -- Domain cannot name a MonoBehaviour --
        // so the wiring step attaches them, and the spark is TOLD its travel
        // rather than measuring a parent whose rect is a cosmetic width.
        public List<NodeRef> EdgeCores = new List<NodeRef>();
        public List<NodeRef> EdgeSparks = new List<NodeRef>();
        public List<float> EdgeLengths = new List<float>();

        public static int OrbIndex(int path, int slot) => path * OrbCount + slot;

        // The inverse, for the two lists that are indexed by their OWN position
        // rather than by slot -- the collars. They store an orb index, and the
        // controller needs to get back to the path and slot it names without
        // re-deriving the skeleton on the far side.
        public static int PathOf(int orbIndex) => orbIndex / OrbCount;

        public static int SlotOf(int orbIndex) => orbIndex % OrbCount;

        public static TalentScreen Build()
        {
            var screen = new TalentScreen();
            var pages = new List<UiNode>();

            for (int path = 0; path < TalentPage.PathCount; path++)
            {
                pages.Add(screen.BuildPage(path));
            }

            // THE SKY IS THE WHOLE CANVAS and the panel sits OVER its right-hand
            // end, which is why the tree's origin is the sky stage's centre
            // rather than the screen's. It holds all three pages side by side
            // and SLIDES: the others hang off the edges, and that overhang is
            // exactly what the paging is made of.
            var sky = Ui.Panel("TalentSky", Place.At(0f, 0f),
                    UiSize.Fixed(ConstellationLayout.SkyWidth, ConstellationLayout.SkyHeight), pages)
                .AllowOverflow("the other two constellations hang off this page's edges - that overhang IS the paging")
                .AllowOverlap("three full-size pages share one coordinate frame; exactly one is ever on screen");
            screen.Sky = sky;

            // Paging arrows flank the SKY at its own edges, so they read as
            // belonging to the stage rather than to the screen. The controller
            // hides the one that has nowhere to go: paging is a clamped line,
            // and an arrow that greys out is how a player finds the ends without
            // counting.
            var prevPath = Ui.Button("PrevPathButton", UiStrings.TalentPrev,
                new UiVec(ConstellationLayout.ArrowWidth, ConstellationLayout.ArrowHeight), 30,
                Place.At(ConstellationLayout.ArrowLeftX, 0f));
            var nextPath = Ui.Button("NextPathButton", UiStrings.TalentNext,
                new UiVec(ConstellationLayout.ArrowWidth, ConstellationLayout.ArrowHeight), 30,
                Place.At(ConstellationLayout.ArrowRightX, 0f));
            screen.PrevPathButton = prevPath;
            screen.NextPathButton = nextPath;

            var back = Ui.Button("TalentBackButton", UiStrings.TalentBack, new UiVec(190f, 52f), 15,
                Place.At(-UiFrames.Reference.X * 0.5f + 119f, UiFrames.Reference.Y * 0.5f - 50f));
            screen.BackButton = back;

            // THE VIGNETTE, over the sky and under everything that has to be
            // read. The design states it as an inset shadow, which uGUI has no
            // equivalent of; this is that shadow as a sprite, and its job is
            // that no backdrop layer ever competes with a stone.
            var vignette = Ui.Sprite("TalentVignette", "proc:sky_vignette",
                    Place.Stretch(), UiSize.Fill)
                .Coloured("#000000B8")
                .AsDecor();

            var children = new List<UiNode>
            {
                Ui.Sprite("TalentBackground", BackgroundKey, Place.Stretch(), UiSize.Fill).AsDecor(),
            };

            children.AddRange(screen.BuildSkyLayers());
            children.Add(sky);
            children.Add(vignette);
            children.Add(screen.BuildPanel());
            children.Add(prevPath);
            children.Add(nextPath);
            children.Add(back);

            // Last, so it draws over everything it is asking about.
            children.Add(screen.BuildRespecDialog());

            screen.Root = Ui.Panel("TalentPanel", UiSize.Fill, children);

            return screen;
        }

        // ---- the detail panel ----------------------------------------------------
        //
        // A COLUMN DOWN THE RIGHT, OVER THE SKY. It replaced an 880x150 plate
        // along the bottom, and the reason is the figure: the tree fills ~85% of
        // the canvas height now, so a bottom plate would either crop the
        // capstone or force the stones below the size the art needs.
        //
        // It also stops being hidden-when-empty. A column that appears and
        // disappears is the screen changing shape as the pointer moves; a
        // column that is always there and says what to do with it is furniture.
        private UiNode BuildPanel()
        {
            var parts = new List<UiNode>
            {
                Ui.Sprite("TalentPanelGround", "proc:card_ground",
                        new UiVec(ConstellationLayout.PanelWidth, ConstellationLayout.PanelHeight),
                        Place.At(0f, 0f))
                    .Coloured("#0B0913F2")
                    .AsDecor(),

                // The 1px accent edge down its left side: the only line
                // separating the panel from the sky it sits over.
                Ui.Solid("TalentPanelEdge", "#E7B25C4D",
                        new UiVec(1f, ConstellationLayout.PanelHeight),
                        Place.At(-ConstellationLayout.PanelWidth * 0.5f + 0.5f, 0f))
                    .AsDecor(),
            };

            // THE CHARACTER'S NAME HEADS THE COLUMN, and is a CHILD of it.
            //
            // It was a sibling, positioned at the panel's own centre-x: the
            // same place on screen, and wrong in a way the audit caught -- an
            // opaque column drawn under two live buttons, which is how a
            // control ends up silently eating clicks meant for the thing
            // beneath it. Inside the panel the two are one object that moves,
            // hides and pages together.
            var characterName = Ui.Label("TalentCharacterName", UiString.Runtime,
                    new UiVec(ConstellationLayout.PanelInnerWidth - 90f, 44f), 36, "#EDE6FF",
                    Place.At(0f, ConstellationLayout.PanelHeaderY))
                .Tracked(20f)
                .AsDecor();
            CharacterName = characterName;
            parts.Add(characterName);

            // Hidden outright while there is one character -- see
            // TalentController.Refresh, and the reason written there.
            var prevCharacter = Ui.Button("PrevCharacterButton", UiStrings.TalentPrev,
                new UiVec(40f, 40f), 20,
                Place.At(-ConstellationLayout.PanelInnerWidth * 0.5f + 20f,
                         ConstellationLayout.PanelHeaderY));
            var nextCharacter = Ui.Button("NextCharacterButton", UiStrings.TalentNext,
                new UiVec(40f, 40f), 20,
                Place.At(ConstellationLayout.PanelInnerWidth * 0.5f - 20f,
                         ConstellationLayout.PanelHeaderY));
            PrevCharacterButton = prevCharacter;
            NextCharacterButton = nextCharacter;
            parts.Add(prevCharacter);
            parts.Add(nextCharacter);

            var pathName = Ui.Label("TalentPathName", UiString.Runtime,
                    new UiVec(ConstellationLayout.PanelInnerWidth, 34f), 28, "#B8A8D9",
                    Place.At(0f, ConstellationLayout.PanelPathY))
                .Tracked(18f)
                .AsDecor();
            PathName = pathName;
            parts.Add(pathName);

            var detailName = Ui.Label("TalentDetailName", UiString.Runtime,
                    new UiVec(ConstellationLayout.PanelInnerWidth, 96f), 38, "#EDE6FF",
                    Place.At(0f, ConstellationLayout.PanelNameY))
                .Tracked(4f)
                .AsDecor();
            DetailName = detailName;
            parts.Add(detailName);

            // THE KICKER IS THE STATE, in words, above the name. The state is
            // also said by the stone's own material, but a material is a thing
            // you learn and a word is a thing you read.
            var kicker = Ui.Label("TalentDetailKicker", UiString.Runtime,
                    new UiVec(ConstellationLayout.PanelInnerWidth, 22f), 12, "#8E7FB0",
                    Place.At(0f, ConstellationLayout.PanelKickerY))
                .Tracked(26f)
                .AsDecor();
            PanelKicker = kicker;
            parts.Add(kicker);

            var price = Ui.Label("TalentDetailPrice", UiString.Runtime,
                    new UiVec(ConstellationLayout.PanelInnerWidth, 26f), 13, "#F2DB9E",
                    Place.At(0f, ConstellationLayout.PanelPriceY))
                .Tracked(10f)
                .AsDecor();
            PanelPrice = price;
            parts.Add(price);

            var body = Ui.Label("TalentDetailBody", UiString.Runtime,
                    new UiVec(ConstellationLayout.PanelInnerWidth, ConstellationLayout.PanelBodyHeight),
                    20, "#B8A8D9",
                    Place.At(0f, ConstellationLayout.PanelBodyY))
                .AsDecor();
            DetailBody = body;
            parts.Add(body);

            // THE REFUSAL, in its own line and its own colour. A stone that
            // cannot be bought says why here rather than by simply doing
            // nothing when it is pressed.
            var refusal = Ui.Label("TalentDetailRefusal", UiString.Runtime,
                    new UiVec(ConstellationLayout.PanelInnerWidth, 60f), 16, "#C2603A",
                    Place.At(0f, ConstellationLayout.PanelRefusalY))
                .AsDecor();
            PanelRefusal = refusal;
            parts.Add(refusal);

            var investLabel = Ui.Label("InvestButtonCaption", UiStrings.TalentInvest,
                    new UiVec(ConstellationLayout.PanelActionWidth - 30f, 40f), 22, "#F2DB9E",
                    Place.At(0f, 0f))
                .Tracked(26f);
            var invest = Ui.Button("InvestButton", UiString.Runtime,
                new UiVec(ConstellationLayout.PanelActionWidth, ConstellationLayout.PanelActionHeight), 20,
                Place.At(0f, ConstellationLayout.PanelActionY));
            invest.Children.Add(investLabel);
            InvestButton = invest;
            InvestLabel = investLabel;
            parts.Add(invest);

            // ---- the committed meter ------------------------------------------
            //
            // 30 embers across all paths, drawn as a bar rather than a fraction:
            // the cap is a budget, and a budget is a length.
            parts.Add(Ui.Solid("TalentMeterTrack", "#221838",
                    new UiVec(ConstellationLayout.PanelInnerWidth, 6f),
                    Place.At(0f, ConstellationLayout.PanelMeterY))
                .AsDecor());

            var meterFill = Ui.Solid("TalentMeterFill", "#E7B25C",
                    new UiVec(ConstellationLayout.PanelInnerWidth, 6f),
                    Place.At(-ConstellationLayout.PanelInnerWidth * 0.5f,
                             ConstellationLayout.PanelMeterY, new UiVec(0f, 0.5f)))
                .AsDecor();
            PanelMeterFill = meterFill;
            parts.Add(meterFill);

            var meter = Ui.Label("TalentEmberCount", UiString.Runtime,
                    new UiVec(ConstellationLayout.PanelInnerWidth, 22f), 12, "#8E7FB0",
                    Place.At(0f, ConstellationLayout.PanelMeterY + 24f))
                .Tracked(20f)
                .AsDecor();
            EmberCount = meter;
            PanelMeter = meter;
            parts.Add(meter);

            var respec = Ui.Button("TalentRespecButton", UiStrings.TalentRespec,
                    new UiVec(ConstellationLayout.PanelActionWidth, 52f), 14,
                    Place.At(0f, ConstellationLayout.PanelRespecY))
                .Inactive();
            RespecButton = respec;
            parts.Add(respec);

            var panel = Ui.Panel("TalentPanelColumn",
                Place.At(ConstellationLayout.PanelCentreX, 0f),
                UiSize.Fixed(ConstellationLayout.PanelWidth, ConstellationLayout.PanelHeight),
                parts);
            Panel = panel;
            return panel;
        }

        // ---- the sky's own layers ------------------------------------------------
        //
        // The design's backdrop is nine CSS layers. Three groups survive into
        // uGUI unchanged in intent: star fields that pan, cloud washes that
        // drift and breathe, and life -- dust, and the occasional shooting star.
        //
        // TWO DEPARTURES, BOTH STATED. The hue rotation is CUT, which is the
        // handoff's own call: it needs a shader, and the drifts, the breathe and
        // the two unequal star pans already carry "never the same frame twice".
        // And the four star FIELDS are four baked textures rather than
        // seventy-eight nodes -- a div is what the prototype has, an Image is
        // what this has, and at one pixel a star the difference is in the node
        // count rather than on the screen. The property that matters, that the
        // fields never synchronise, lives in the periods.
        private IEnumerable<UiNode> BuildSkyLayers()
        {
            // Two washes drifting against each other, and a third that fades in
            // and out entirely -- which is what stops the backdrop reading as a
            // fixed painting behind the tree.
            string[] cloudTints = { "#5B2E6B3D", "#3A2A6B33", "#1F4A5E2E" };

            for (int i = 0; i < cloudTints.Length; i++)
            {
                var cloud = Ui.Sprite($"TalentCloud{i}", "proc:cloud_wash",
                        new UiVec(2400f, 1500f), Place.At(0f, 0f))
                    .Coloured(cloudTints[i])
                    .AllowOverflow("a cloud layer is deliberately larger than the canvas so its own edge never enters frame as it drifts")
                    .AsDecor();
                CloudWashes.Add(cloud);
                yield return cloud;
            }

            // Four fields on two pan tracks, deliberately unequal and in
            // opposite directions. That inequality IS the parallax.
            string[] fields = { "star_field_far", "star_field_mid", "star_field_mid_b", "star_field_near" };
            string[] fieldTints = { "#C8B4DE57", "#D8CCF0D9", "#CFC2E8C4", "#FFFFFFFF" };

            for (int i = 0; i < fields.Length; i++)
            {
                var field = Ui.Sprite($"TalentStars{i}", $"proc:{fields[i]}",
                        new UiVec(2200f, 1400f), Place.At(0f, 0f))
                    .Coloured(fieldTints[i])
                    .AllowOverflow("a star field is larger than the canvas so panning it never walks its own edge into frame")
                    .AsDecor();
                StarFields.Add(field);
                yield return field;
            }

            // Seven motes, rising. All of them start MID-FLIGHT rather than
            // together -- the controller gives each a negative delay, so the
            // first frame of the screen is not seven embers leaving the ground
            // in formation.
            for (int i = 0; i < 7; i++)
            {
                var mote = Ui.Sprite($"TalentDust{i}", "proc:radial_glow",
                        new UiVec(6f, 6f), Place.At(-560f + i * 190f, -400f))
                    .Coloured("#F2DB9E00")
                    .AsDecor();
                DustMotes.Add(mote);
                yield return mote;
            }

            // TWO STREAKS, IDLE FOR MOST OF THEIR CYCLE. This is the one EVENT
            // in the backdrop and it has to stay rare to read as one: 38s and
            // 57s apart with a 23s offset, so they never appear together and the
            // sky is empty of them nearly all the time.
            for (int i = 0; i < 2; i++)
            {
                var streak = Ui.Sprite($"TalentShootingStar{i}", "proc:shimmer_band",
                        new UiVec(i == 0 ? 180f : 132f, 3f), Place.At(0f, 300f - i * 220f))
                    .Coloured("#FFFFFF00")
                    .Rotated(-24f)
                    .AsDecor();
                ShootingStars.Add(streak);
                yield return streak;
            }
        }

        // ---- putting out every ember ---------------------------------------------
        //
        // A CONFIRMATION, WHICH THIS SCREEN DID NOT HAVE. One click used to
        // clear every path in all three constellations. The handoff settled both
        // halves of it: the respec stays FREE -- charging for it taxes
        // experimenting with a system whose whole point is experimenting -- and
        // it asks first.
        private UiNode BuildRespecDialog()
        {
            var title = Ui.Label("RespecDialogTitle", UiStrings.TalentRespecTitle,
                    new UiVec(700f, 56f), 34, "#EDE6FF", Place.At(0f, 96f))
                .Tracked(4f)
                .AsDecor();

            // NAMES THE EXACT REFUND, filled by the controller. What goes dark
            // and what comes back is the part a player needs before pressing;
            // a dialog that only asked "are you sure?" would be asking them to
            // remember what they had.
            var body = Ui.Label("RespecDialogBody", UiString.Runtime,
                    new UiVec(700f, 90f), 20, "#B8A8D9", Place.At(0f, 8f))
                .AsDecor();
            RespecDialogBody = body;

            var cancel = Ui.Button("RespecCancelButton", UiStrings.TalentRespecCancel,
                new UiVec(260f, 64f), 18, Place.At(-150f, -104f));
            var confirm = Ui.Button("RespecConfirmButton", UiStrings.TalentRespecConfirm,
                new UiVec(260f, 64f), 18, Place.At(150f, -104f));

            RespecCancelButton = cancel;
            RespecConfirmButton = confirm;

            var card = Ui.Panel("RespecDialogCard", Place.At(0f, 0f), UiSize.Fixed(780f, 320f),
                Ui.Sprite("RespecDialogGround", "proc:card_ground", new UiVec(780f, 320f), Place.At(0f, 0f))
                    .Coloured("#241735")
                    .AsDecor(),
                title, body, cancel, confirm);

            var dialog = Ui.Modal("TalentRespecDialog", "#05030ACC", card).Inactive();
            RespecDialog = dialog;
            return dialog;
        }

        private UiNode BuildPage(int path)
        {
            var children = new List<UiNode>();

            // EDGES FIRST, so every orb draws over the lines that reach it --
            // declaration order is painter's order and a line across an orb
            // reads as a crack in it.
            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                foreach (int parent in TalentSkeleton.Parents[slot])
                {
                    children.Add(BuildEdge(path, slot, parent));
                }
            }

            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                children.Add(BuildOrb(path, slot));
            }

            return Ui.Panel($"TalentPage{path}", Place.At(ConstellationLayout.PageX(path, 0), 0f),
                    UiSize.Fixed(ConstellationLayout.SkyWidth, ConstellationLayout.SkyHeight), children)
                .AllowOverflow("orbs and their glows sit on the sky's own edges; the constellation is not a boxed diagram")
                .AllowOverlap("edges pass beneath the orbs they connect, which is what makes them read as connections");
        }

        // PER PATH, not per (dx, depth). The tree is hand-plotted now: each
        // allegiance draws its own figure out of the same 21 slots, so where a
        // stone sits depends on which constellation it belongs to and no longer
        // on a column index times a pitch.
        private static UiVec PositionOf(int path, int slot) => new UiVec(
            ConstellationLayout.TreeOriginX + ConstellationLayout.StarX(path, slot),
            ConstellationLayout.TreeOriginY + ConstellationLayout.StarY(path, slot));

        private UiNode BuildOrb(int path, int slot)
        {
            var at = PositionOf(path, slot);
            string kind = TalentSkeleton.Kind[slot];
            float size = ConstellationLayout.OrbSize(kind);

            // THE ORB IS THE BUTTON and everything else hangs off it, so the
            // whole stone -- aura, ring, collar, label -- moves as one and the
            // click target is the stone itself rather than a box around it.
            var orb = Ui.Button($"Orb{path}_{slot}", UiString.Runtime, new UiVec(size, size), 12,
                    Place.At(at.X, at.Y))
                .AllowOverlap("a constellation's stars share their light - the aura and the collar reach past the stone, and reaching a neighbour is the effect rather than a collision")
                .AllowOverflow("the plots put stones near the sky's edges by construction, and a stone's own light is drawn larger than the stone");

            // Unlit is the BUILT state: a scene opened with nothing invested --
            // every screenshot, every fresh save -- draws the tree dark, and
            // lighting up is something the player does.
            orb.SpriteKey = OrbUnlitKey;

            // A LIT STONE'S AURA CIRCULATES, IT DOES NOT PULSE. Two soft arcs in
            // a band just outside the shell, rotated by the controller and never
            // changing size or brightness. Motion without pulsing is the
            // difference between "this is alive" and "you may press this" -- the
            // pulse is reserved for the reachable ring, which is an invitation.
            var aura = Ui.Sprite($"Orb{path}_{slot}Aura", "proc:orb_aura",
                    new UiVec(size * 1.7f, size * 1.7f), Place.At(0f, 0f))
                .Coloured("#FF914500")
                .AllowOverflow("a lit stone's aura is light around the stone - contained inside it, it would be a filling instead")
                .AsDecor();
            OrbAuras.Add(aura);
            orb.Children.Add(aura);

            // The warm drop-shadow a kindled stone carries. Behind the stone,
            // wider than it, and off entirely until the stone is lit.
            var glow = Ui.Sprite($"Orb{path}_{slot}Glow", "proc:radial_glow",
                    new UiVec(size * 2.4f, size * 2.4f), Place.At(0f, 0f))
                .Coloured("#F2DB9E00")
                .AllowOverflow("a drop-shadow that stops at the stone's own edge is not a shadow")
                .AsDecor();
            OrbGlows.Add(glow);
            orb.Children.Add(glow);

            // THE CORE, screen-blended over the sprite: the crackle is INSIDE
            // the stone. The art already shows a molten core with veins running
            // out of it, so what moves is that core guttering behind the shell;
            // flickering the halo instead was what read as a strobe.
            var core = Ui.Sprite($"Orb{path}_{slot}Core", "proc:radial_glow",
                    new UiVec(size * 0.52f, size * 0.52f), Place.At(0f, 0f))
                .Coloured("#FFC97A00")
                .AsDecor();
            OrbCores.Add(core);
            orb.Children.Add(core);

            // The ring a reachable stone wears, and the fainter one on a stone
            // that is only too expensive. One node, two weights, because a stone
            // is never both.
            var ring = Ui.Sprite($"Orb{path}_{slot}Ring", "proc:ring_hairline",
                    new UiVec(size + 14f, size + 14f), Place.At(0f, 0f))
                .Coloured("#E7B25C00")
                .AllowOverflow("the ring is drawn 7px outside the stone on purpose - it circles the stone rather than sitting on it")
                .AsDecor();
            OrbRings.Add(ring);
            orb.Children.Add(ring);

            // ---- the gate collar, on the two slots that have a gate ----------

            if (kind == "merge" || kind == "cap")
            {
                float collar = ConstellationLayout.CollarSize(kind);

                var track = Ui.Sprite($"Orb{path}_{slot}CollarTrack", "proc:collar_ring",
                        new UiVec(collar, collar), Place.At(0f, 0f))
                    .Coloured("#2A1E3C00")
                    .AllowOverflow("the collar is a ring AROUND the gated stone - ConstellationLayout.CollarSize is the stone plus its inset by construction")
                    .AsDecor();

                // THE FILL IS THE SAME RING, radial-filled by the controller.
                // uGUI's Image.type Filled / Radial360 IS the design's conic
                // gradient: an arc from twelve o'clock filled to spent/required,
                // which needs no shader to say and steps rather than tweens --
                // in the same frame as the kindling beat, so the two read as one
                // event.
                var fill = Ui.Sprite($"Orb{path}_{slot}CollarFill", "proc:collar_ring",
                        new UiVec(collar, collar), Place.At(0f, 0f))
                    .Coloured("#F2DB9E00")
                    .AllowOverflow("same ring as the track it fills, and the same reason")
                    .AsDecor();

                CollarSlots.Add(OrbIndex(path, slot));
                CollarTracks.Add(track);
                CollarFills.Add(fill);

                orb.Children.Add(track);
                orb.Children.Add(fill);

                // The reading, above the stone: spent over required. Drawn in
                // every state, which is the whole point of the collar.
                var count = Ui.Label($"Orb{path}_{slot}Gate", UiString.Runtime,
                        new UiVec(ConstellationLayout.LabelWidth, ConstellationLayout.LabelHeight),
                        ConstellationLayout.PriceFont, "#B8A8D9",
                        Place.At(0f, collar * 0.5f + 14f))
                    .Tracked(10f)
                    .AllowOverflow("the gate reading sits above the stone's own box by design - it belongs to the collar, which is already wider than the stone")
                    .AsDecor();
                CollarCounts.Add(count);
                orb.Children.Add(count);
            }

            // ---- the name and the price --------------------------------------
            //
            // Under the stone, and the price under the name. A NAME IS
            // TRANSIENT AND A PRICE IS PERMANENT, so where the two would
            // collide the price yields -- it is repeated in the panel and the
            // label is the thing that names what is being looked at.
            var label = Ui.Label($"Orb{path}_{slot}Name", UiString.Runtime,
                    new UiVec(ConstellationLayout.LabelWidth, ConstellationLayout.LabelHeight),
                    ConstellationLayout.LabelFont, "#EDE6FF",
                    Place.At(0f, ConstellationLayout.LabelY(kind)))
                .Tracked(16f)
                .AllowOverflow("a stone's name is wider than the stone - it is a caption on the sky, not a fill inside a box")
                .AsDecor();
            OrbLabels.Add(label);
            orb.Children.Add(label);

            var price = Ui.Label($"Orb{path}_{slot}Price", UiString.Runtime,
                    new UiVec(ConstellationLayout.LabelWidth, ConstellationLayout.LabelHeight),
                    ConstellationLayout.PriceFont, "#F2DB9E",
                    Place.At(0f, ConstellationLayout.PriceY(kind)))
                .Tracked(10f)
                .AllowOverflow("same as the label above it, and for the same reason")
                .AsDecor();
            OrbPrices.Add(price);
            orb.Children.Add(price);

            Orbs.Add(orb);
            return orb;
        }

        private UiNode BuildEdge(int path, int slot, int parent)
        {
            var a = PositionOf(path, parent);
            var b = PositionOf(path, slot);

            // A thin quad stretched between the two, rotated to face along the
            // line. One node per connection rather than a line renderer: the
            // emitter builds Images, and a rotated quad is a line.
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float length = (float)System.Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)(System.Math.Atan2(dy, dx) * 180.0 / System.Math.PI);

            // TEN WIDE, not three. A 3px hairline reads as a wiring diagram;
            // v1's edges are limbs with a lit crack down them, and the width is
            // most of what makes a connection look grown rather than drawn.
            var edge = Ui.Solid($"Edge{path}_{parent}_{slot}", EdgeDim,
                    Place.At((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f),
                    UiSize.Fixed(length, ConstellationLayout.EdgeWidth))
                .Rotated(angle)
                .AsDecor()
                .AllowOverflow("a rotated edge's axis-aligned box is wider than the line inside it");

            // THE LIT LAYER, a sibling rather than a child.
            //
            // v1 hangs the core off the glow so it inherits the rotation for
            // free, and that is right there -- but here the glow is what the
            // controller switches, and a switched-off parent takes its children
            // with it whether or not that was meant. Sibling and child are the
            // same picture; only one of them can be reasoned about.
            var glow = Ui.Sprite($"Edge{path}_{parent}_{slot}Glow", EdgeStripeKey,
                    new UiVec(length, ConstellationLayout.EdgeGlowWidth),
                    Place.At((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f))
                .Coloured(EdgeGlow)
                .Rotated(angle)
                .Inactive()
                .AsDecor()
                .AllowOverflow("a soft halo is meant to bleed past the limb - that bleed is the light");

            // AT ZERO, AND UNROTATED, BECAUSE IT IS A CHILD.
            //
            // This carried the glow's own midpoint and the glow's own angle,
            // which are both correct for a SIBLING and both wrong for a child:
            // a child's position composes with its parent's, so the core landed
            // at twice the midpoint, turned to twice the angle, somewhere else
            // in the sky entirely. Every lit edge drew a second gold line at a
            // reflected position -- visible in a runtime capture as loose
            // hairlines with no stone at either end, and invisible to UiAudit
            // because the AllowOverflow below waives exactly the containment
            // check that would have caught it. The spark beside it was already
            // local and was the clue.
            var core = Ui.Sprite($"Edge{path}_{parent}_{slot}Core", EdgeStripeKey,
                    new UiVec(length, ConstellationLayout.EdgeCoreWidth),
                    Place.At(0f, 0f))
                .Coloured(EdgeCore)
                // NOT Inactive. It is a CHILD of the glow, which is the thing
                // the controller switches, so it comes and goes with its
                // parent -- and switching it off here as well meant it never
                // came back on at all. A lit edge showed its halo and no crack,
                // and the crackle rode an object that never ran.
                .AsDecor()
                .AllowOverflow("a rotated edge's axis-aligned box is wider than the line inside it");

            // THE DOT THAT RUNS THE LINE. A child of the glow like the core,
            // so it inherits the rotation and can travel along its own local X
            // -- and so an unlit edge has no spark for free, because a
            // switched-off glow takes its children with it.
            //
            // Placed at the far end to start; TalentEdgeSpark moves it from
            // there. Authored somewhere real rather than at zero so the audit
            // measures it where it will actually be seen.
            var spark = Ui.Sprite($"Edge{path}_{parent}_{slot}Spark", "proc:radial_glow",
                    new UiVec(ConstellationLayout.EdgeSparkSize, ConstellationLayout.EdgeSparkSize),
                    Place.At(-length * 0.5f, 0f))
                .Coloured(EdgeSpark)
                .AsDecor()
                .AllowOverflow("the spark sits ON the edge's end and is wider than the line - it is meant to spill past it");

            glow.Children.Add(core);
            glow.Children.Add(spark);

            Edges.Add(edge);
            EdgeGlows.Add(glow);
            EdgeChildSlots.Add(slot);
            EdgeCores.Add(core);
            EdgeSparks.Add(spark);
            EdgeLengths.Add(length);

            return Ui.Panel($"Edge{path}_{parent}_{slot}Group", Place.At(0f, 0f), UiSize.Fill,
                    edge, glow)
                .AsDecor();
        }
    }
}
