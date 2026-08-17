using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The battle screen: two stages facing each other, a three-column command
    // area, and the plates that read out both sides.
    //
    // The largest screen in the game and the last hard test of the construction
    // layer. v1 built it across 1,279 lines of two SceneBuilder partials, and
    // its own header carried this note: "the Fight panel has a history of silent
    // rect collisions (partyStatusLabel once covered both the message log AND
    // the choice row)". That is exactly what A1 is for, and every overlap that
    // survives below now has to say out loud why it is deliberate.
    //
    // COORDINATES ARE v1's. What changed is a handful of label BOX widths, and
    // one box height, narrowed where the audit found two boxes crossing. Each is
    // noted at its site. Nothing moved; only boxes that reached past a neighbour
    // got trimmed, and the E1 text-fit audit is what stops a trim going too far
    // -- A1 says boxes must not overlap, E1 says text must fit its box, and
    // between them there is no width that passes both while being wrong.
    public sealed class FightScreen
    {
        // The three backdrops, swapped at runtime by encounter class. All three
        // are floor-1 forest: an ordinary clearing, a ruined archway for an
        // elite, and a will-o'-wisp swamp for the boss. Declared here rather
        // than looked up in the controller, so every path a build depends on is
        // stated in the tree.
        public const string BackgroundKey = "Backgrounds/Fight.png";
        public const string EliteBackgroundKey = "Assets/_Project/Art/Backgrounds/forest_mob_elite_fight.png";
        public const string BossBackgroundKey = "Assets/_Project/Art/Backgrounds/forest_mob_boss_fight.png";

        private const string PanelViolet = "Assets/_Project/Art/UI/Panels/panel_violet.png";

        // Under Resources/ so the SAME file serves both the build-time bake
        // (this Assets-relative path) and the runtime swap (Resources.Load on
        // "Intent/<slug>"). One file, two loaders, no second copy to drift.
        private const string IntentDefaultIcon = "Assets/_Project/Resources/Intent/attack.png";
        private const string BannerViolet = "Assets/_Project/Art/UI/Panels/banner_violet.png";
        private const string PanelCrimson = "Assets/_Project/Art/UI/Panels/panel_crimson.png";

        // What a combatant with no authored battle art stands in for. Declared
        // HERE with the other sprite keys rather than in the registry, so every
        // path this screen can load art from is greppable in one file.
        public const string FallbackPlateKey = PanelCrimson;
        private const string BannerFrame = "Assets/_Project/Art/UI/Panels/banner_frame.png";

        // The vertical band every command column shares: rows sit above this and
        // grow upward, with the breadcrumb in the strip below.
        private const float CommandBottom = FightSubmenuLayout.CommandBottom;

        // ---- what the wiring binds ------------------------------------------

        public UiNode Root;

        public NodeRef Background;
        public NodeRef InitiativeTracker;
        public List<NodeRef> InitiativeIcons = new List<NodeRef>();
        public List<NodeRef> InitiativeRings = new List<NodeRef>();
        public List<NodeRef> InitiativeLabels = new List<NodeRef>();

        public NodeRef EnemyStage;
        public NodeRef PartyStage;
        public List<NodeRef> EnemySlots = new List<NodeRef>();
        public List<NodeRef> EnemySprites = new List<NodeRef>();
        public List<NodeRef> EnemyHitFlashes = new List<NodeRef>();
        public List<NodeRef> EnemyNameplates = new List<NodeRef>();

        // One per enemy STAGE slot (not per plate): the icon that says what this
        // monster has committed to doing next, and the hover target for the
        // detail behind it.
        public List<NodeRef> EnemyIntentIcons = new List<NodeRef>();
        public List<NodeRef> EnemyFootShadows = new List<NodeRef>();
        public List<NodeRef> PartySlots = new List<NodeRef>();
        public List<NodeRef> PartySprites = new List<NodeRef>();
        public List<NodeRef> PartyHitFlashes = new List<NodeRef>();
        public List<NodeRef> PartyNameplates = new List<NodeRef>();
        public List<NodeRef> PartyFootShadows = new List<NodeRef>();

        public NodeRef BarkPanel;
        public NodeRef BarkPortrait;
        public NodeRef BarkLabel;

        public NodeRef EnemiesHint;
        public List<NodeRef> EnemyPlates = new List<NodeRef>();
        public List<NodeRef> EnemyPlateNames = new List<NodeRef>();
        public List<NodeRef> EnemyPlateHps = new List<NodeRef>();
        public List<NodeRef> EnemyPlateHpFills = new List<NodeRef>();
        public List<NodeRef> EnemyPlateTags = new List<NodeRef>();
        public List<NodeRef> EnemyPlateReticles = new List<NodeRef>();

        public NodeRef PartyPlate;
        public NodeRef PartyPortrait;
        public NodeRef PartyName;
        public NodeRef PartyClass;
        public NodeRef PartyHpFill;
        public NodeRef PartyHpValue;
        public NodeRef PartyMpFill;
        public NodeRef PartyMpValue;
        public NodeRef PartyMpPreview;
        public NodeRef WoolRow;
        public NodeRef WoolValue;
        public List<NodeRef> WoolPips = new List<NodeRef>();

        public List<NodeRef> VerbButtons = new List<NodeRef>();
        public List<NodeRef> VerbLabels = new List<NodeRef>();
        public List<NodeRef> VerbCarets = new List<NodeRef>();
        public NodeRef Breadcrumb;
        public NodeRef ContinueButton;

        // The post-fight payout, mounted here rather than as its own scene so
        // the stage is still standing behind it when it expands.
        public ReckoningScreen Reckoning;

        // Its twin, for the other outcome. Mounted after it so a defeat draws
        // over a victory screen that could never be up at the same time -- the
        // ordering costs nothing and removes the question.
        public DefeatScreen Defeat;

        // The same tree the hub mounts, wired read-only here.
        public CharacterOverlayScreen Sheet;

        public NodeRef SubmenuColumn;
        public NodeRef SubmenuTitle;
        public NodeRef SubmenuHint;
        public NodeRef SubmenuBackButton;
        public List<NodeRef> SubmenuRows = new List<NodeRef>();
        public List<NodeRef> SubmenuMarks = new List<NodeRef>();
        public List<NodeRef> SubmenuNames = new List<NodeRef>();
        public List<NodeRef> SubmenuMetas = new List<NodeRef>();
        public List<NodeRef> SubmenuCosts = new List<NodeRef>();

        public NodeRef DetailColumn;
        public NodeRef DetailName;
        public NodeRef DetailKind;
        public NodeRef DetailBody;
        public List<NodeRef> DetailStatKeys = new List<NodeRef>();
        public List<NodeRef> DetailStatValues = new List<NodeRef>();

        public NodeRef TargetPrompt;
        public NodeRef IntentTooltip;
        public NodeRef IntentTooltipText;
        public NodeRef TargetPromptLabel;

        public NodeRef SpellVfxPool;
        public NodeRef SpellVfx;
        public NodeRef DamagePopupPool;
        public List<NodeRef> DamagePopups = new List<NodeRef>();
        public List<NodeRef> DamagePopupLabels = new List<NodeRef>();

        // ---------------------------------------------------------------------

        public static FightScreen Build()
        {
            var s = new FightScreen();
            var children = new List<UiNode>();

            var background = Ui.Sprite("Background", BackgroundKey, Place.Stretch(), UiSize.Fill).AsDecor();
            s.Background = background;
            children.Add(background);

            // BETWEEN the backdrop and the stages, which is the entire point:
            // declared after the background and before the actors, so it knocks
            // the painting down without touching the figures standing on it.
            children.AddRange(s.BuildScrim());

            // Party stage FIRST so enemies, built after and therefore later
            // siblings, paint over it where the two halves meet near the shared
            // vanishing point at centre.
            var partyStage = s.BuildStage("Party", mirrored: true,
                FightStageAnchors.AllyShadowColor, s.PartySlots, s.PartySprites, s.PartyHitFlashes,
                s.PartyNameplates, s.PartyFootShadows);
            var enemyStage = s.BuildStage("Enemy", mirrored: false,
                FightStageAnchors.EnemyShadowColor, s.EnemySlots, s.EnemySprites, s.EnemyHitFlashes,
                s.EnemyNameplates, s.EnemyFootShadows, s.EnemyIntentIcons);
            s.PartyStage = partyStage;
            s.EnemyStage = enemyStage;
            children.Add(partyStage);
            children.Add(enemyStage);

            children.Add(s.BuildInitiativeTracker());
            children.Add(s.BuildBark());
            children.AddRange(s.BuildEnemiesHeading());
            children.AddRange(s.BuildEnemyPlates());
            children.Add(s.BuildPartyPlate());
            children.AddRange(s.BuildVerbColumn());
            children.Add(s.BuildBreadcrumb());
            children.Add(s.BuildContinueButton());
            children.Add(s.BuildSubmenuColumn());
            children.Add(s.BuildDetailColumn());
            children.Add(s.BuildTargetPrompt());
            children.Add(s.BuildIntentTooltip());
            children.Add(s.BuildSpellVfx());
            children.Add(s.BuildDamagePopups());

            // The character sheet, reachable mid-fight.
            //
            // Readable, never editable: ScreenRegistry sets lockedForFight on
            // this copy, so a player can check what they are wearing against
            // what is hitting them without being able to re-plate between
            // swings. Declared BEFORE the reckoning and the defeat screen, so
            // an end-of-fight modal still draws over it.
            var sheet = CharacterOverlayScreen.Build();
            s.Sheet = sheet;
            children.Add(sheet.Root);

            // LAST, so they draw over the whole stage they dim.
            var reckoning = ReckoningScreen.Build();
            s.Reckoning = reckoning;
            children.Add(reckoning.Root);

            var defeat = DefeatScreen.Build();
            s.Defeat = defeat;
            children.Add(defeat.Root);

            s.Root = Ui.Panel("FightPanel", UiSize.Fixed(1920f, 1080f), children);
            return s;
        }

        // ---- the scrim ---------------------------------------------------------
        //
        // Three darkening layers between the painting and the figures.
        //
        // THE PROBLEM IS NOT THE BACKDROP'S QUALITY. The actors are
        // hard-outlined, flat-shaded cel art; the backdrops are soft, line-free
        // atmospheric painting. Two incompatible drawing languages, so the
        // figures land as stickers on a photograph however well either half is
        // made. Moving the art toward the actors is the real fix and belongs in
        // the prompts; this is what the layout can do about it meanwhile.
        //
        // Each layer answers a measured fault rather than a taste:
        //   band   detail frequency in the actor band matched sprite frequency,
        //          so the figures had nothing quiet to sit against
        //   centre the composition's brightest region was dead centre, which is
        //          the EMPTY GAP between the two armies - the eye was being
        //          pulled to the one place nothing happens
        //   floor  the back rows stood in the frame's only light source
        //
        // TUNED TO A TARGET LUMINANCE, not to maximum separation, and that
        // distinction is load-bearing. v1's first attempt scored better on
        // every readability metric it had and turned the painting into a black
        // rectangle: separation is monotonic in "make it darker", so optimising
        // it optimises the backdrop out of existence. tools/measure_scrim.py
        // reports what actually lands behind the slots and fails outside L
        // 30-50.
        private const string ScrimBandKey = "proc:scrim_band";
        private const string ScrimFloorKey = "proc:scrim_floor";

        // A knock-down and a bloom are the same shape; only the colour differs,
        // so the centre layer borrows the glow the foot rings already use.
        private const string ScrimCentreKey = "proc:radial_glow";

        // Alphas are v1's tuned 0.32 / 0.38 / 0.50 as 8-bit: 52, 61, 80. The
        // colour is the palette's near-black violet rather than pure black --
        // a neutral scrim over a violet painting greys it, which reads as fog
        // rather than as shadow.
        private const string ScrimBandColour = "#0B071852";
        private const string ScrimCentreColour = "#0B071861";
        private const string ScrimFloorColour = "#0B071880";

        private IEnumerable<UiNode> BuildScrim()
        {
            // Across the actor rows. 480 tall centred at -60 spans -300..180,
            // which brackets every ground line (-228..-68) and every head the
            // manifest can produce (to 106), with the soft 30% edges landing
            // outside the figures rather than across them.
            yield return Ui.Sprite("ScrimBand", ScrimBandKey, Place.At(0f, -60f),
                    UiSize.Fixed(1920f, 480f))
                .Coloured(ScrimBandColour)
                .AsDecor();

            // Over the gap. The nearest slots sit at x +/-470, so a 1000-wide
            // knock-down centred on 0 covers the dead middle and falls off
            // before it reaches anybody.
            yield return Ui.Sprite("ScrimCentre", ScrimCentreKey, Place.At(0f, -60f),
                    UiSize.Fixed(1000f, 600f))
                .Coloured(ScrimCentreColour)
                .AsDecor();

            // Under the command columns, weighted to the very bottom so the
            // verb column and the party plate sit on something darker than they
            // do; the gradient is squared, so it is nearly gone by mid-frame.
            yield return Ui.Sprite("ScrimFloor", ScrimFloorKey, Place.At(0f, -320f),
                    UiSize.Fixed(1920f, 440f))
                .Coloured(ScrimFloorColour)
                .AsDecor();
        }

        // ---- the 2.5D stage --------------------------------------------------

        // Enemy slots receding along a fake ground plane. The depth maths lives
        // in StageLayout and the two physical anchors in FightStageAnchors; this
        // only composes them into declared positions.
        //
        // Slots are declared FAR TO NEAR, which is the painter's algorithm --
        // uGUI draws later siblings on top, so slot 0 (nearest, biggest, lowest)
        // being declared last is what puts it in front, with no sibling-index
        // fixups afterwards. The REF LISTS stay indexed by slot, because that is
        // what the controller and every test index by.
        private UiNode BuildStage(string prefix, bool mirrored, string shadowHex,
                                  List<NodeRef> slots, List<NodeRef> sprites, List<NodeRef> flashes,
                                  List<NodeRef> nameplates, List<NodeRef> shadows,
                                  List<NodeRef> intentIcons = null)
        {
            int count = FightHudSpec.StageSlotsPerSide;
            for (int i = 0; i < count; i++)
            {
                slots.Add(default);
                sprites.Add(default);
                flashes.Add(default);
                nameplates.Add(default);
                shadows.Add(default);
                intentIcons?.Add(default);
            }

            var children = new List<UiNode>();
            for (int sibling = 0; sibling < count; sibling++)
            {
                int slot = count - 1 - sibling;
                var offset = FightStageAnchors.SlotOffset(slot, count, mirrored);
                float scale = FightStageAnchors.SlotScale(slot, count);

                // A soft bloom behind the contact ring. Its falloff is widest
                // exactly where the ring is already fully transparent, so the
                // two occupy mostly separate space rather than fighting for the
                // same pixels.
                var glow = Ui.Sprite($"{prefix}{slot}FootGlow", "proc:radial_glow",
                        Place.Frac(new UiVec(-0.45f, -1.1f), new UiVec(1.45f, 2.1f)), UiSize.Fill)
                    .Coloured(shadowHex)
                    .AllowOverflow("the bloom behind the contact ring is meant to bleed well past it - that softness IS the effect");

                // The ground contact shadow, straddling the slot's own ground
                // line. Anchor-stretched to a fraction of the slot's WIDTH
                // rather than given a fixed one, so it scales with whatever size
                // the runtime gives the slot from the real sprite: a boss's ring
                // is wider than Shawn's for free, with no runtime code at all.
                // A baked ring, not a Solid. An Image with no sprite draws a
                // filled RECTANGLE, which is what a flat quad here looked like:
                // a red brick under each monster's feet. Same bug class as the
                // hub's borrowed plot art, and the reason `proc:` keys exist.
                var shadow = Ui.Sprite($"{prefix}{slot}FootShadow", "proc:ring_outline",
                        Place.Frac(new UiVec(0.32f, 0f), new UiVec(0.68f, 0f), bottom: -8f, top: -8f), UiSize.Fill)
                    .Coloured(shadowHex)
                    .AllowOverflow("the contact shadow straddles the ground LINE, so half of it is below the slot by construction")
                    .AllowOverlap("the actor stands ON its own shadow - the sprite covering it is the point");
                shadow.Children.Add(glow);

                var sprite = Ui.Sprite($"{prefix}{slot}Sprite", null, Place.Stretch(), UiSize.Fill).Inactive();

                var flash = Ui.Sprite($"{prefix}{slot}HitFlash", null, Place.Stretch(), UiSize.Fill)
                    .Inactive()
                    .AllowOverlap("the hit flash IS the sprite's silhouette redrawn white - sharing its box is the whole mechanism");

                // Anchored to the slot's BOTTOM, not its centre: the plate hangs
                // just under the feet, so it never depends on how tall a given
                // monster's sprite happens to be.
                var nameplate = Ui.Label($"{prefix}{slot}Nameplate", UiString.Runtime,
                        new UiVec(260f, 28f), 16, FightHudPalette.TextPrimary,
                        Place.Pin(new UiVec(0.5f, 0f), UiVec.Centre, new UiVec(0f, FightStageAnchors.NameplateOffset)))
                    .Inactive()
                    .AllowOverflow("the nameplate hangs BELOW the feet - outside the slot is where it belongs");

                // ENEMIES ONLY. The player already knows what their own party is
                // about to do -- they are about to decide it.
                //
                // A Button rather than a Label because it has to be hoverable:
                // the glyph alone says "something nasty is coming", and the
                // detail (who, and roughly how hard) is what the player opens on
                // demand rather than reading five of at once.
                UiNode intent = null;
                if (!mirrored)
                {
                    intent = Ui.Button($"EnemyIntent{slot}", UiString.Runtime,
                            new UiVec(FightStageAnchors.IntentIconSize, FightStageAnchors.IntentIconSize), 17,
                            Place.Pin(new UiVec(0.5f, 1f), UiVec.Centre,
                                new UiVec(0f, FightStageAnchors.IntentIconOffset)))
                        .Inactive()
                        .Hovers(1.18f)
                        .AllowOverflow("the intent icon floats ABOVE the slot on purpose - it marks the monster without standing on it");

                    // The default badge, so the Image exists and is correctly
                    // configured at build time. The runtime swaps both the
                    // sprite and its tint as the committed intent changes -- one
                    // of seven, which is why they load from Resources rather
                    // than being baked one per kind.
                    intent.SpriteKey = IntentDefaultIcon;

                    // BY SLOT, never Add(). This loop walks FAR TO NEAR for the
                    // painter's order, so appending builds the list backwards --
                    // which is exactly what happened: badge 0 ended up parented
                    // to Enemy2Slot. At three monsters the COUNT still matched,
                    // so it looked correct and every badge described the wrong
                    // monster; at two, the badge for the front rat was parented
                    // to an unused slot and vanished. The header above this
                    // method already says the ref lists stay indexed by slot.
                    intentIcons[slot] = intent;
                }

                var slotNode = Ui.Panel($"{prefix}{slot}Slot",
                        Place.At(offset.X, offset.Y, new UiVec(0.5f, 0f)),
                        UiSize.Fixed(320f, 200f),
                        intent == null
                            ? new[] { shadow, sprite, flash, nameplate }
                            : new[] { shadow, sprite, flash, nameplate, intent })
                    .WithScale(new UiVec(scale, scale))
                    .AllowOverlap("depth-stacked actors standing on one receding floor overlap by construction - that IS the perspective")
                    .AllowOverflow("320x200 is a placeholder resized to the real sprite at runtime; the stage is a coordinate frame, not a clip region");

                slots[slot] = slotNode;
                sprites[slot] = sprite;
                flashes[slot] = flash;
                nameplates[slot] = nameplate;
                shadows[slot] = shadow;
                children.Add(slotNode);
            }

            // Both halves share ONE centred coordinate frame, mirrored only in
            // X, so the ground line is identical on both sides and the two
            // armies read as standing on one floor rather than two platforms at
            // different heights.
            return Ui.Panel($"{prefix}Stage", Place.At(0f, 0f),
                    UiSize.Fixed(FightStageAnchors.StageSize.X, FightStageAnchors.StageSize.Y), children)
                // The exemption is on the FRAME and says nothing about the
                // figures standing in it, which is exactly how the front row
                // came to be standing inside the HUD with a clean build gate.
                // The reasoning was right -- a stage really is a transparent
                // coordinate frame -- and the consequence drawn from it was too
                // broad: A1 skips a pair if EITHER carries a reason, so writing
                // one here silenced every actor-versus-panel pair on the screen.
                //
                // It is still the correct exemption; the frame genuinely
                // overlaps the other stage and every panel. What was missing is
                // a check of the thing the exemption was never entitled to
                // cover, and FightScreenTests.NoAlwaysVisiblePanelStandsInFront-
                // OfAFigureSFeet is now that check.
                .AllowOverlap("the party and enemy stages share one centred frame, and the HUD is drawn over both - a stage is a transparent coordinate frame, never a surface. This covers the FRAME only: figures standing in it are checked by NoAlwaysVisiblePanelStandsInFrontOfAFigureSFeet, because A1 cannot tell the two apart");
        }

        // ---- initiative -------------------------------------------------------

        // A Row, not six hand-stepped x positions. v1 wrote
        // `i * (IconSize + IconGap)` at the construction site; here the gap is
        // the container's Spacing and the loop cannot state a position at all.
        private UiNode BuildInitiativeTracker()
        {
            var entries = Ui.Each(Enumerable.Range(0, FightHudSpec.InitiativeSlots).ToList(), (_, i) =>
            {
                float pad = FightStageAnchors.InitiativeRingPadding;

                var ring = Ui.Sprite($"InitiativeRing{i}", null,
                        Place.Stretch(-pad, -pad, -pad, -pad), UiSize.Fill)
                    .Inactive()
                    .AllowOverflow("the emphasis ring frames the icon from OUTSIDE it, which is what makes the acting combatant pop")
                    .AllowOverlap("ring, portrait and initial are three layers of one badge");

                // Inactive until a combatant is actually in the slot. An Image
                // with no sprite renders as a WHITE QUAD, so six empty slots on
                // a fight that has not started drew six white squares across the
                // top-left corner -- caught by looking at the screenshot, which
                // no audit would have flagged because the box is the right size.
                var icon = Ui.Sprite($"InitiativeIcon{i}", null, Place.Stretch(), UiSize.Fill).Inactive();

                var label = Ui.Label($"InitiativeLabel{i}", UiString.Runtime,
                        new UiVec(FightStageAnchors.InitiativeIconSize, 24f), 14,
                        FightHudPalette.TextPrimary, Place.At(0f, 0f))
                    .AllowOverlap("the initial is printed ON the portrait, as the fallback for a combatant with no art");

                InitiativeRings.Add(ring);
                InitiativeIcons.Add(icon);
                InitiativeLabels.Add(label);

                return Ui.Panel($"InitiativeSlot{i}", Place.Flow,
                    UiSize.Fixed(FightStageAnchors.InitiativeIconSize, FightStageAnchors.InitiativeIconSize),
                    ring, icon, label);
            });

            var tracker = Ui.Row("InitiativeTracker",
                Place.Pin(new UiVec(0f, 1f), new UiVec(0f, 1f), new UiVec(28f, -20f)),
                FightStageAnchors.InitiativeIconGap, UiAlign.Centre, entries);

            InitiativeTracker = tracker;
            return tracker;
        }

        // ---- the dialogue bark (the combat log's home) ------------------------

        private UiNode BuildBark()
        {
            var portrait = Ui.Sprite("BarkPortrait", null, new UiVec(56f, 56f), Place.At(-362f, 0f)).Inactive();

            // 660 wide from a left pivot at -316, so it stops 18px short of the
            // banner's right inner edge. Best-fit shrinking is a runtime
            // property; the BOX is what the audit measures.
            var label = Ui.Label("MessageLabel", UiString.Runtime, new UiVec(660f, 90f), 20,
                FightHudPalette.TextPrimary, Place.At(-316f, 0f, new UiVec(0f, 0.5f)));

            BarkPortrait = portrait;
            BarkLabel = label;

            // banner_violet.png, not the near-square panel texture: this box is
            // an 8:1 strip, and stretching a 1.56:1 canvas into it compressed
            // its own border ~5x more vertically than horizontally -- the "black
            // bar around the log bar + it overflows" playtest report.
            var bark = Ui.Sprite("DialogueBark", BannerViolet, Place.At(0f, 430f), UiSize.Fixed(840f, 104f));
            bark.Children.Add(portrait);
            bark.Children.Add(label);
            BarkPanel = bark;
            return bark;
        }

        // ---- enemy plates ------------------------------------------------------

        private const float PlateX = 720f;
        private const float PlateW = 400f;
        private const float PlateH = 104f;
        private const float PlatePitch = PlateH + 12f;
        // 332 until the stage came up out of the HUD. The anchors' ceiling is
        // this plate stack: the tallest actor needs 300 units above the front
        // slot's ground line, so at Near.Y -228 its head reaches 72, and this
        // stack's lower edge (PlateFirstY - 284) has to stay above that.
        //
        // 392 RATHER THAN 380, and the extra 12 is the interesting part. By
        // hand it looked like only the front slot could ever reach the plates
        // in x, which made 380 ample. tools/measure_stage.py disagreed: slot
        // ONE reaches x 522 against the plates' left edge at 520, so it
        // collides too, and at 380 its head cleared by 7px rather than the 12
        // the rest of this layout is toleranced to. The 2px x-overlap is what
        // made it invisible to inspection.
        private const float PlateFirstY = 392f;

        private IEnumerable<UiNode> BuildEnemiesHeading()
        {
            // 155 wide, not v1's 200. At 200 this box ran to x 720 while the
            // right-aligned hint's box starts at 680 -- a 40px crossing the
            // audit refuses. "E N E M I E S" at 12pt bold is well under 155.
            yield return Ui.Label("EnemiesHeading", UiStrings.EnemiesHeading, new UiVec(155f, 24f), 12,
                FightHudPalette.TextMuted,
                Place.At(PlateX - PlateW * 0.5f, PlateFirstY + 74f, new UiVec(0f, 0.5f)));

            var hint = Ui.Label("EnemiesHint", UiStrings.StandingCount, new UiVec(240f, 24f), 12,
                FightHudPalette.TextDisabled,
                Place.At(PlateX + PlateW * 0.5f, PlateFirstY + 74f, new UiVec(1f, 0.5f)));
            EnemiesHint = hint;
            yield return hint;
        }

        // The plates are BUTTONS as well as readouts -- targeting reuses them
        // rather than opening a fourth column, which keeps the player's eye on
        // the column they are already reading rather than moving it somewhere
        // new mid-decision.
        private IEnumerable<UiNode> BuildEnemyPlates()
        {
            return Ui.Each(Enumerable.Range(0, FightHudSpec.EnemyPlates).ToList(), (_, i) =>
            {
                var name = Ui.Label($"EnemyPlate{i}Name", UiString.Runtime, new UiVec(250f, 26f), 17,
                    FightHudPalette.EnemyName, Place.At(-184f, 30f, new UiVec(0f, 0.5f)));

                // 110 wide, not v1's 140: at 140 the right-aligned HP box began
                // at x 44 and the left-aligned name box ends at 66.
                var hp = Ui.Label($"EnemyPlate{i}Hp", UiStrings.HealthValue, new UiVec(110f, 24f), 14,
                    FightHudPalette.EnemyHpText, Place.At(184f, 30f, new UiVec(1f, 0.5f)));

                var fill = Ui.Solid($"EnemyPlate{i}HpFill", FightHudPalette.HpBright, Place.Stretch(), UiSize.Fill);
                var bar = Ui.Panel($"EnemyPlate{i}Bar", Place.At(0f, 8f), UiSize.Fixed(368f, 11f), fill)
                    .Coloured(FightHudPalette.Track);

                var tags = Ui.Label($"EnemyPlate{i}Tags", UiString.Runtime, new UiVec(360f, 22f), 11,
                    FightHudPalette.TextSecondary, Place.At(-184f, -20f, new UiVec(0f, 0.5f)));

                // A 14px square rotated 45 degrees, pinned just outside the
                // plate's left edge. Visible only while targeting.
                var reticle = Ui.Solid($"EnemyPlate{i}Reticle", FightHudPalette.TargetAmber,
                        new UiVec(14f, 14f), Place.At(-PlateW * 0.5f - 13f, 0f))
                    .Rotated(45f)
                    .Inactive()
                    .AllowOverflow("the reticle is deliberately OUTSIDE the plate - a marker in the margin, not a badge on the card");

                EnemyPlateNames.Add(name);
                EnemyPlateHps.Add(hp);
                EnemyPlateHpFills.Add(fill);
                EnemyPlateTags.Add(tags);
                EnemyPlateReticles.Add(reticle);

                // 1.03 hover, no press pop: the plate is 400+ wide and carries
                // four labels, so the press animator's 1.05/0.95 would swing
                // the name and HP text sideways under the cursor. v1 made the
                // same call at the same number.
                var plate = Ui.Button($"EnemyPlate{i}", UiString.Runtime, new UiVec(PlateW, PlateH), 1,
                    Place.At(PlateX, PlateFirstY - i * PlatePitch)).Hovers(1.03f);
                plate.SpriteKey = PanelCrimson;
                plate.Children.Add(name);
                plate.Children.Add(hp);
                plate.Children.Add(bar);
                plate.Children.Add(tags);
                plate.Children.Add(reticle);
                plate.Inactive();

                EnemyPlates.Add(plate);
                return plate;
            });
        }

        // ---- the party plate ----------------------------------------------------

        // panel_crimson.png, not a dedicated gold plate: every gold banner
        // delivered for this slot shares a wide-notched silhouette whose flat
        // midsection reaches within 0.5% of the canvas edge -- fine for a verb
        // button with nothing near its edges, and wrong here, where the HP/MP
        // rows sit at exactly that mid-height and fought the border for the same
        // few pixels (the "overflow in the health / mana container" report,
        // twice). panel_crimson is a true rectangle and keeps uniform clearance.
        private UiNode BuildPartyPlate()
        {
            var portrait = Ui.Sprite("PartyPortrait", null, new UiVec(64f, 64f), Place.At(-170f, 54f)).Inactive();

            var name = Ui.Label("PartyName", UiString.Runtime, new UiVec(220f, 32f), 24,
                FightHudPalette.PartyNameText, Place.At(-122f, 62f, new UiVec(0f, 0.5f)));

            // 94 wide, not v1's 240: v1's box ran from x -46 straight through
            // the name's, which reaches 98. Right-aligned, so the text still
            // ends exactly where it always did.
            var cls = Ui.Label("PartyClass", UiStrings.LevelAndRole, new UiVec(94f, 34f), 13,
                FightHudPalette.PartyClassText, Place.At(194f, 62f, new UiVec(1f, 0.5f)));

            // 22 wide, not v1's 30: the bar begins at x -170.
            var hpTag = Ui.Label("PartyHpTag", UiStrings.HpTag, new UiVec(22f, 20f), 12,
                FightHudPalette.HpBright, Place.At(-194f, 4f, new UiVec(0f, 0.5f)));
            var hpFill = Ui.Solid("PartyHpFill", FightHudPalette.HpBright, Place.Stretch(), UiSize.Fill);
            var hpBar = Ui.Panel("PartyHpBar", Place.At(-24f, 4f), UiSize.Fixed(292f, 16f), hpFill)
                .Coloured(FightHudPalette.Track);

            // 70 wide, not v1's 100: the bar ends at x 122. And 12pt, not v1's
            // 14: at 14 TMP needs 78px for "9999/9999" and wraps it to two
            // lines inside 70, which E1 catches and a screenshot would only
            // show as a value mysteriously sitting half outside its row. TMP
            // renders crisper than uGUI at the same nominal size, so 12 here
            // reads about as large as v1's 14 did.
            var hpValue = Ui.Label("PartyHpValue", UiStrings.HealthValue, new UiVec(70f, 20f), 12,
                FightHudPalette.HpText, Place.At(194f, 4f, new UiVec(1f, 0.5f)));

            var mpTag = Ui.Label("PartyMpTag", UiStrings.MpTag, new UiVec(22f, 20f), 12,
                FightHudPalette.MpBright, Place.At(-194f, -22f, new UiVec(0f, 0.5f)));
            var mpFill = Ui.Solid("PartyMpFill", FightHudPalette.MpBright, Place.Stretch(), UiSize.Fill);

            // The MP COST PREVIEW: a lighter segment at the right-hand end of
            // the filled portion, showing what a hovered skill would consume
            // from INSIDE the resource. The one piece of this screen that
            // answers "can I afford this" without the player reading a number
            // and doing the subtraction. Right-pivoted so it grows leftward from
            // wherever the current fill ends.
            var mpPreview = Ui.Solid("PartyMpPreview", FightHudPalette.MpPreview,
                    Place.At(0f, 0f, new UiVec(1f, 0.5f)), UiSize.Fixed(40f, 14f))
                .Inactive()
                .AllowOverlap("the preview is drawn INSIDE the mana bar - sitting on the fill is what makes it read as part of the resource rather than beside it");

            var mpBar = Ui.Panel("PartyMpBar", Place.At(-24f, -22f), UiSize.Fixed(292f, 16f), mpFill, mpPreview)
                .Coloured(FightHudPalette.TrackMp);
            var mpValue = Ui.Label("PartyMpValue", UiStrings.HealthValue, new UiVec(70f, 20f), 12,
                FightHudPalette.MpText, Place.At(194f, -22f, new UiVec(1f, 0.5f)));

            PartyPortrait = portrait;
            PartyName = name;
            PartyClass = cls;
            PartyHpFill = hpFill;
            PartyHpValue = hpValue;
            PartyMpFill = mpFill;
            PartyMpPreview = mpPreview;
            PartyMpValue = mpValue;

            var plate = Ui.Sprite("PartyPlate", PanelCrimson, Place.At(-694f, -392f), UiSize.Fixed(452f, 216f));
            plate.Children.Add(portrait);
            plate.Children.Add(name);
            plate.Children.Add(cls);
            plate.Children.Add(hpTag);
            plate.Children.Add(hpBar);
            plate.Children.Add(hpValue);
            plate.Children.Add(mpTag);
            plate.Children.Add(mpBar);
            plate.Children.Add(mpValue);
            plate.Children.Add(BuildWoolRow());
            PartyPlate = plate;
            return plate;
        }

        // 16 pips rather than a fraction. A charge meter you can watch fill
        // without reading numbers is the whole reason this is not just another
        // "3/16" label.
        private UiNode BuildWoolRow()
        {
            var divider = Ui.Solid("WoolDivider", FightHudPalette.Hairline, new UiVec(404f, 1f), Place.At(0f, 22f))
                .AllowOverflow("the divider sits ON the row's top edge, marking where the wool block begins");

            // 60 wide, not v1's 70: at 70 this box reached x -132 and the first
            // pip's box begins at -140.5.
            var tag = Ui.Label("WoolTag", UiStrings.WoolHeading, new UiVec(60f, 20f), 12,
                FightHudPalette.TextMuted, Place.At(-202f, 0f, new UiVec(0f, 0.5f)));

            var pips = Ui.Each(Enumerable.Range(0, FightHudSpec.WoolPips).ToList(), (_, i) =>
            {
                var pip = Ui.Solid($"WoolPip{i}", FightHudPalette.PipEmpty, new UiVec(13f, 20f),
                    Place.At(-134f + i * 18f, 0f));
                WoolPips.Add(pip);
                return pip;
            });

            // 55 wide, not v1's 80: at 80 this reached x 122 and the sixteenth
            // pip ends at 142.5.
            var value = Ui.Label("WoolValue", UiStrings.SignatureValue, new UiVec(55f, 20f), 15,
                FightHudPalette.TextPrimary, Place.At(202f, 0f, new UiVec(1f, 0.5f)));
            WoolValue = value;

            var children = new List<UiNode> { divider, tag };
            children.AddRange(pips);
            children.Add(value);

            var row = Ui.Panel("WoolRow", Place.At(0f, -66f), UiSize.Fixed(452f, 40f), children);
            WoolRow = row;
            return row;
        }

        // ---- column A: the verbs -------------------------------------------------

        private const float VerbColumnX = -286f;
        private const float VerbRowW = 300f;
        private const float VerbRowH = 52f;
        private const float VerbPitch = 62f;

        // Rendered BOTTOM-UP so ATTACK sits nearest the cursor, and tiered so
        // read order matches use frequency: ATTACK loud, SKILL/ITEM neutral, RUN
        // and HOLD BACK quiet. That hierarchy is the main fix over v1's earlier
        // row of five identical gold buttons.
        private IEnumerable<UiNode> BuildVerbColumn()
        {
            var labels = new[]
            {
                UiStrings.VerbAttack, UiStrings.VerbSkill, UiStrings.VerbItem,
                UiStrings.VerbRun, UiStrings.VerbHoldBack,
            };
            var hotkeys = new[]
            {
                UiStrings.HotkeyOne, UiStrings.HotkeyTwo, UiStrings.HotkeyThree,
                UiStrings.HotkeyFour, UiStrings.HotkeyFive,
            };
            var nests = new[] { false, true, true, false, false };

            return Ui.Each(labels, (label, i) =>
            {
                bool primary = i == 0;
                bool quiet = i >= 3;

                var hotkey = Ui.Label($"Verb{i}Hotkey", hotkeys[i], new UiVec(24f, 20f), 12,
                    quiet ? FightHudPalette.QuietHotkey : FightHudPalette.LoudHotkey,
                    Place.At(-134f, 0f, new UiVec(0f, 0.5f)));

                // "Text", not "Label": the emitter names a button's own caption
                // child `<Button>Label`, so a hand-declared `Verb0Label` would
                // collide with the one the emitter creates -- and A4 would only
                // catch it after the emit, not in the tree.
                var text = Ui.Label($"Verb{i}Text", label, new UiVec(200f, 26f), primary ? 19 : 17,
                    quiet ? FightHudPalette.TextMuted : FightHudPalette.TextPrimary,
                    Place.At(-104f, 0f, new UiVec(0f, 0.5f)));

                // Declared on every row and deactivated on the ones that do not
                // nest, rather than existing on some rows only: a ref list with
                // holes in it is exactly the shape that produces an off-by-one
                // at the far end.
                var caret = Ui.Label($"Verb{i}Caret", UiStrings.VerbNestCaret, new UiVec(20f, 26f), 18,
                    FightHudPalette.TextDisabled, Place.At(134f, 0f, new UiVec(1f, 0.5f)));
                if (!nests[i]) caret.Inactive();

                // Named directly. v1 created these as Verb0..4 and then renamed
                // them afterwards, so what a row MEANT lived somewhere else
                // entirely from what it was called.
                var row = Ui.Button($"Verb{i}", UiString.Runtime, new UiVec(VerbRowW, VerbRowH), 1,
                    Place.At(VerbColumnX, CommandBottom + 26f + i * VerbPitch));
                row.Children.Add(hotkey);
                row.Children.Add(text);
                row.Children.Add(caret);

                // HOLD BACK is hidden on request. The button, its wiring and its
                // layout slot all stay, so re-enabling it is one line.
                if (i == 4) row.Inactive();

                VerbButtons.Add(row);
                VerbLabels.Add(text);
                VerbCarets.Add(caret);
                return row;
            });
        }

        private UiNode BuildBreadcrumb()
        {
            var crumb = Ui.Label("Breadcrumb", UiStrings.CommandTitle, new UiVec(VerbRowW, 20f), 11,
                FightHudPalette.TextMuted,
                Place.At(VerbColumnX - VerbRowW * 0.5f, CommandBottom - 16f, new UiVec(0f, 0.5f)));
            Breadcrumb = crumb;
            return crumb;
        }

        private UiNode BuildContinueButton()
        {
            var button = Ui.Button("ContinueButton", UiStrings.Continue, new UiVec(300f, 80f), 20,
                    Place.At(-286f, -420f))
                .Inactive()
                .AllowOverlap("Continue swaps footprints with the verb column - the verbs are fully hidden whenever it is up, so the two are never both live");
            ContinueButton = button;
            return button;
        }

        // ---- column B: the submenu -------------------------------------------------

        private const float SubmenuX = 86f;
        private const float SubmenuRowW = 404f;

        // Opens to the RIGHT of the verbs rather than replacing them: nothing is
        // taken off screen, only added, which is what the breadcrumb underneath
        // is naming.
        //
        // Rows are built at MaxRows and re-anchored at runtime through the SAME
        // FightSubmenuLayout.RowY the build uses. v1 kept a second copy of these
        // constants in Core because "Core cannot see Editor-only constants", and
        // its design preview drew rows at 8-slot positions leaving a gap above
        // BACK -- a bug that cannot be written now, because there is no second
        // copy left to disagree with.
        private UiNode BuildSubmenuColumn()
        {
            var backKey = Ui.Label("SubmenuBackKey", UiStrings.HotkeyEscape, new UiVec(48f, 20f), 12,
                FightHudPalette.TextDisabled, Place.At(-186f, 0f, new UiVec(0f, 0.5f)));
            var backText = Ui.Label("SubmenuBackText", UiStrings.Back, new UiVec(160f, 20f), 13,
                FightHudPalette.BackRowText, Place.At(-132f, 0f, new UiVec(0f, 0.5f)));

            var back = Ui.Button("SubmenuBack", UiString.Runtime, new UiVec(SubmenuRowW, 44f), 1,
                Place.At(SubmenuX, CommandBottom + 22f));
            back.Children.Add(backKey);
            back.Children.Add(backText);
            SubmenuBackButton = back;

            int count = FightSubmenuLayout.MaxRows;
            var rows = Ui.Each(Enumerable.Range(0, count).ToList(), (_, i) =>
            {
                var mark = Ui.Sprite($"CharacterSkill{i}Mark", null, new UiVec(36f, 36f), Place.At(-168f, 0f))
                    .Inactive();

                // 20 tall, not v1's 24: at 24 this box reached y -2 and the meta
                // line's box below it reaches -4.
                // 190, down from 240, to pay for the cost box beside them. The
                // longest authored name is well inside this; the cost is the
                // half that was actually starved.
                var name = Ui.Label($"CharacterSkill{i}Name", UiString.Runtime, new UiVec(190f, 20f), 18,
                    FightHudPalette.RowNameText, Place.At(-136f, 10f, new UiVec(0f, 0.5f)));
                var meta = Ui.Label($"CharacterSkill{i}Meta", UiString.Runtime, new UiVec(190f, 20f), 11,
                    FightHudPalette.TextMuted, Place.At(-136f, -14f, new UiVec(0f, 0.5f)));

                // 128, up from 80. 80 was sized for "5 MP" and the moment costs
                // started naming their resource -- "12 MP + 8 WOOL" -- the label
                // wrapped onto a second line and hung out over the battlefield.
                // The name and meta boxes gave up the width, since 240 was more
                // than any authored name uses and the cost had none to spare.
                var cost = Ui.Label($"CharacterSkill{i}Cost", UiStrings.SignatureValue, new UiVec(128f, 22f), 14,
                    FightHudPalette.GoldLight, Place.At(186f, 0f, new UiVec(1f, 0.5f)));

                // 1.02, the gentler of the two: a submenu row is 404 wide with
                // four columns of text in it, so the press pop would shift all
                // four. Same split v1 used -- verb rows pop, submenu rows hover.
                var row = Ui.Button($"CharacterSkill{i}", UiString.Runtime,
                    new UiVec(SubmenuRowW, FightSubmenuLayout.RowHeight), 1,
                    Place.At(SubmenuX, FightSubmenuLayout.RowY(count, i))).Hovers(1.02f);
                row.Children.Add(mark);
                row.Children.Add(name);
                row.Children.Add(meta);
                row.Children.Add(cost);

                SubmenuRows.Add(row);
                SubmenuMarks.Add(mark);
                SubmenuNames.Add(name);
                SubmenuMetas.Add(meta);
                SubmenuCosts.Add(cost);
                return row;
            }).ToList();

            // 170 wide each, not v1's 220: at 220 the left-aligned title reached
            // x 104 and the right-aligned hint began at 68.
            float headerY = FightSubmenuLayout.HeaderY(count);
            var title = Ui.Label("SubmenuTitle", UiStrings.SubmenuSkillsTitle, new UiVec(170f, 20f), 11,
                FightHudPalette.GoldLight,
                Place.At(SubmenuX - SubmenuRowW * 0.5f, headerY, new UiVec(0f, 0.5f)));
            var hint = Ui.Label("SubmenuHint", UiStrings.SubmenuHint, new UiVec(170f, 20f), 11,
                FightHudPalette.TextDisabled,
                Place.At(SubmenuX + SubmenuRowW * 0.5f, headerY, new UiVec(1f, 0.5f)));
            SubmenuTitle = title;
            SubmenuHint = hint;

            var children = new List<UiNode> { back };
            children.AddRange(rows);
            children.Add(title);
            children.Add(hint);

            var column = Ui.Panel("SubmenuColumn", Place.Stretch(), UiSize.Fill, children)
                .Inactive()
                .Opening()
                .AllowOverlap("a full-screen transparent group toggled as one unit; it deliberately draws over the stage, and its own rows are audited against each other normally");
            SubmenuColumn = column;
            return column;
        }

        // ---- column C: the detail panel ----------------------------------------------

        private const float DetailX = 478f;
        private const float DetailW = 340f;
        private const float DetailH = 300f;

        // A PERSISTENT third column rather than a hover tooltip: arrowing
        // through options lets the player compare without re-hovering, and a
        // tooltip that vanishes the moment the cursor moves cannot be compared
        // against anything.
        private UiNode BuildDetailColumn()
        {
            float left = -DetailW * 0.5f + 22f;
            float top = DetailH * 0.5f - 20f;

            var name = Ui.Label("DetailName", UiString.Runtime, new UiVec(296f, 28f), 20,
                FightHudPalette.TextPrimary, Place.At(left, top - 12f, new UiVec(0f, 0.5f)));
            var kind = Ui.Label("DetailKind", UiStrings.DetailKindSkill, new UiVec(296f, 20f), 12,
                FightHudPalette.GoldLight, Place.At(left, top - 40f, new UiVec(0f, 0.5f)));
            var body = Ui.Label("DetailBody", UiString.Runtime, new UiVec(296f, 76f), 14,
                FightHudPalette.TextSecondary, Place.At(left, top - 92f, new UiVec(0f, 0.5f)));
            var divider = Ui.Solid("DetailDivider", FightHudPalette.Hairline, new UiVec(296f, 1f),
                Place.At(0f, top - 140f));

            DetailName = name;
            DetailKind = kind;
            DetailBody = body;

            var keys = new[]
            {
                UiStrings.DetailStatCost, UiStrings.DetailStatPower,
                UiStrings.DetailStatTarget, UiStrings.DetailStatEffect,
            };

            var children = new List<UiNode> { name, kind, body, divider };
            for (int i = 0; i < FightHudSpec.DetailStatRows; i++)
            {
                float y = top - 162f - i * 24f;
                var key = Ui.Label($"DetailStatKey{i}", keys[i], new UiVec(170f, 20f), 12,
                    FightHudPalette.TextMuted, Place.At(left, y, new UiVec(0f, 0.5f)));

                // 120 wide, not v1's 170: at 170 this box began at x -22 and the
                // key's box reaches 22.
                var value = Ui.Label($"DetailStatValue{i}", UiString.Runtime, new UiVec(120f, 20f), 15,
                    FightHudPalette.GoldLight, Place.At(DetailW * 0.5f - 22f, y, new UiVec(1f, 0.5f)));

                DetailStatKeys.Add(key);
                DetailStatValues.Add(value);
                children.Add(key);
                children.Add(value);
            }

            var column = Ui.Sprite("DetailColumn", PanelViolet,
                Place.At(DetailX, CommandBottom + DetailH * 0.5f), UiSize.Fixed(DetailW, DetailH));
            foreach (var child in children) column.Children.Add(child);
            column.Inactive();
            column.Opening();
            DetailColumn = column;
            return column;
        }

        // ---- the intent tooltip --------------------------------------------------------

        // What the icon above a monster's head means, on demand.
        //
        // Deliberately ONE runtime label rather than a row per field: the three
        // lines are written together by FightHudModel, so splitting them across
        // three nodes would put the sentence's grammar in the view and the words
        // in Domain.
        //
        // AsDecor because a tooltip that swallows a click on the enemy behind it
        // would make the icon actively worse than no icon.
        private UiNode BuildIntentTooltip()
        {
            var label = Ui.Label("IntentTooltipText", UiString.Runtime, new UiVec(400f, 120f), 17,
                FightHudPalette.GoldText, Place.At(0f, 0f));
            IntentTooltipText = label;

            var panel = Ui.Sprite("IntentTooltip", PanelViolet, Place.At(-330f, 250f),
                    UiSize.Fixed(420f, 140f))
                .Inactive()
                .AsDecor()
                .AllowOverlap("a hover tooltip floats over whatever it has to - it is transient and takes no clicks");
            panel.Children.Add(label);
            IntentTooltip = panel;
            return panel;
        }

        // ---- the target prompt ---------------------------------------------------------

        // Visible only at target depth, and non-interactive -- it must never eat
        // a click meant for the plate underneath it. AsDecor is how that is
        // stated: the emitter clears raycastTarget across the whole subtree, so
        // "takes no clicks" is enforced rather than remembered.
        private UiNode BuildTargetPrompt()
        {
            var diamond = Ui.Solid("TargetPromptDiamond", FightHudPalette.TargetAmber, new UiVec(12f, 12f),
                Place.At(-206f, 0f)).Rotated(45f);
            var label = Ui.Label("TargetPromptLabel", UiStrings.TargetPrompt, new UiVec(400f, 24f), 15,
                FightHudPalette.GoldText, Place.At(-182f, 0f, new UiVec(0f, 0.5f)));
            TargetPromptLabel = label;

            var prompt = Ui.Sprite("TargetPrompt", BannerFrame, Place.At(0f, -150f), UiSize.Fixed(480f, 48f));
            prompt.Children.Add(diamond);
            prompt.Children.Add(label);
            prompt.Inactive();
            prompt.Opening();
            prompt.AsDecor();
            prompt.AllowOverlap("the prompt sits over the submenu at target depth on purpose - it is the more urgent of the two, and it takes no clicks");
            TargetPrompt = prompt;
            return prompt;
        }

        // ---- the two genuine pools ------------------------------------------------------

        // The ONLY two runtime-positioned things on this screen, and the reason
        // Ui.Pool exists at all. Everything the brief called a pool -- submenu
        // rows, pips, verbs, enemy plates, initiative slots -- has a computable
        // resting position and is fully audited above.

        // Declared ABOVE the HUD and BELOW the popups. v1's comment claimed
        // "below the action buttons"; its BUILT order put it here, and shipped
        // behaviour wins over a comment describing an earlier draft.
        private UiNode BuildSpellVfx()
        {
            var pool = Ui.Pool("SpellVfx", 1, i =>
            {
                var image = Ui.Sprite($"SpellVfx{i}", null, new UiVec(380f, 380f), Place.At(0f, 0f)).Inactive();
                SpellVfx = image;
                return image;
            }).AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            SpellVfxPool = pool;
            return pool;
        }

        // Six is comfortably more than one beat can ever need (one hit, or one
        // party-wide effect across three members) with headroom for overlap
        // between beats. LAST sibling, so a floating number is never hidden
        // behind a spell.
        private UiNode BuildDamagePopups()
        {
            var pool = Ui.Pool("DamagePopups", FightHudSpec.DamagePopups, i =>
            {
                var label = Ui.Label($"DamagePopup{i}Label", UiString.Runtime, new UiVec(220f, 60f), 40,
                    FightHudPalette.TextPrimary, Place.At(0f, 0f));
                var popup = Ui.Panel($"DamagePopup{i}", Place.At(0f, 0f), UiSize.Fixed(220f, 60f), label)
                    .Inactive();
                DamagePopups.Add(popup);
                DamagePopupLabels.Add(label);
                return popup;
            }).AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            DamagePopupPool = pool;
            return pool;
        }
    }
}
