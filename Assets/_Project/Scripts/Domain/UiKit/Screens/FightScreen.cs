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

        // One per enemy slot: a click target over the figure itself, live only
        // while the player is choosing a mark. See BuildStage.
        public List<NodeRef> EnemyHitAreas = new List<NodeRef>();
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

        public NodeRef LowHpVignette;

        public NodeRef EnemiesHint;
        public List<NodeRef> EnemyPlates = new List<NodeRef>();
        public List<NodeRef> EnemyPlateIcons = new List<NodeRef>();
        public List<NodeRef> EnemyPlateNames = new List<NodeRef>();
        public List<NodeRef> EnemyPlateHps = new List<NodeRef>();
        public List<NodeRef> EnemyPlateHpFills = new List<NodeRef>();
        public List<NodeRef> EnemyPlateTags = new List<NodeRef>();
        public List<NodeRef> EnemyPlateReticles = new List<NodeRef>();
        public List<NodeRef> EnemyPlateBreakTracks = new List<NodeRef>();
        public List<NodeRef> EnemyPlateBreakFills = new List<NodeRef>();

        public NodeRef PartyPlate;
        public NodeRef PartyPortrait;
        public NodeRef PartyName;
        public NodeRef PartyClass;
        public NodeRef PartyHpFill;
        public NodeRef PartyHpValue;
        public NodeRef PartyMpFill;
        public NodeRef PartyMpValue;
        public List<NodeRef> PartyBuffIcons = new List<NodeRef>();
        public NodeRef PartyBuffTooltip;
        public NodeRef PartyBuffTooltipText;
        public NodeRef WoolRow;
        public NodeRef WoolValue;
        public List<NodeRef> WoolPips = new List<NodeRef>();
        public NodeRef SecondLifeBadge;
        public NodeRef TransformStrip;
        public NodeRef TransformStripText;
        public List<NodeRef> RosterPlates = new List<NodeRef>();
        public List<NodeRef> RosterNames = new List<NodeRef>();
        public List<NodeRef> RosterHpValues = new List<NodeRef>();
        public List<NodeRef> RosterHpFills = new List<NodeRef>();

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
        public SystemMenuScreen SystemMenu;

        public NodeRef SubmenuColumn;
        public NodeRef SubmenuTitle;
        public NodeRef SubmenuHint;
        public NodeRef SubmenuBackButton;
        public List<NodeRef> SubmenuRows = new List<NodeRef>();
        public List<NodeRef> SubmenuMarks = new List<NodeRef>();
        public List<NodeRef> SubmenuNames = new List<NodeRef>();

        // The frame the rows scroll inside. The container is the plate, the
        // content is the rect that moves, and the two bar pieces are shown only
        // when the list is longer than the window.
        public NodeRef SubmenuContainer;
        public NodeRef SubmenuViewport;
        public NodeRef SubmenuContent;
        public NodeRef SubmenuScrollTrack;
        public NodeRef SubmenuScrollThumb;

        public NodeRef DetailColumn;
        public NodeRef DetailName;
        public NodeRef DetailKind;
        public NodeRef DetailBody;
        public List<NodeRef> DetailStatKeys = new List<NodeRef>();
        public List<NodeRef> DetailStatValues = new List<NodeRef>();

        // The skill's element ("FIRE", "ARCANE", ...), tinted at runtime by
        // FightHudPalette.ForDamageType -- see BuildDetailColumn's own
        // comment for why it rides the POWER row instead of a sixth row of
        // its own.
        public NodeRef DetailDamageType;

        public NodeRef TargetPrompt;
        public NodeRef IntentTooltip;
        public NodeRef IntentTooltipText;
        public NodeRef TargetPromptLabel;
        public NodeRef TargetCancelButton;

        public NodeRef SpellVfxPool;
        public List<NodeRef> SpellVfx = new List<NodeRef>();
        public List<NodeRef> SpellVfxNext = new List<NodeRef>();

        // The shared ground layer -- ONE of it, however many enemies are hit,
        // and behind all of them. See BuildSpellGroundVfx.
        public NodeRef SpellGroundVfxPool;
        public NodeRef SpellGroundVfx;
        public NodeRef SpellGroundVfxNext;
        public NodeRef DamagePopupPool;
        public List<NodeRef> DamagePopups = new List<NodeRef>();
        public List<NodeRef> DamagePopupLabels = new List<NodeRef>();

        // ---------------------------------------------------------------------

        public static FightScreen Build()
        {
            var s = new FightScreen();
            var children = new List<UiNode>();
            var hud = new List<UiNode>();

            var background = Ui.Sprite("Background", BackgroundKey, Place.Stretch(), UiSize.Fill).AsDecor();
            s.Background = background;
            children.Add(background);

            // BETWEEN the backdrop and the stages, which is the entire point:
            // declared after the background and before the actors, so it knocks
            // the painting down without touching the figures standing on it.
            // Stays in the ROOT canvas, one level above the nested HUD
            // canvas the stages and everything over them live in -- so it
            // dims the painting from behind every figure standing on it,
            // whatever their own sibling order down there.
            children.AddRange(s.BuildScrim());

            // BEHIND BOTH RACKS, which is the entire reason it is declared here
            // and not beside the per-target pool three hundred lines down. uGUI
            // draws later siblings on top, so a fault opening in the floor has
            // to be an EARLIER sibling than the figures standing on it -- drawn
            // after them it would paint over their feet, which reads as the
            // stone erupting in front of the enemy rather than under it.
            hud.Add(s.BuildSpellGroundVfx());

            // Party stage FIRST so enemies, built after and therefore later
            // siblings, paint over it where the two halves meet near the shared
            // vanishing point at centre.
            var partyStage = s.BuildStage("Party", mirrored: true,
                FightStageAnchors.AllyShadowColor, s.PartySlots, s.PartySprites, s.PartyHitFlashes,
                s.PartyNameplates, s.PartyFootShadows);
            var enemyStage = s.BuildStage("Enemy", mirrored: false,
                FightStageAnchors.EnemyShadowColor, s.EnemySlots, s.EnemySprites, s.EnemyHitFlashes,
                s.EnemyNameplates, s.EnemyFootShadows, s.EnemyIntentIcons, s.EnemyHitAreas);
            s.PartyStage = partyStage;
            s.EnemyStage = enemyStage;
            hud.Add(partyStage);
            hud.Add(enemyStage);
            hud.Add(s.BuildLowHpVignette());

            hud.Add(s.BuildInitiativeTracker());
            hud.Add(s.BuildBark());
            hud.AddRange(s.BuildEnemiesHeading());
            hud.AddRange(s.BuildEnemyPlates());
            hud.Add(s.BuildPartyPlate());
            hud.Add(s.BuildTransformStrip());
            hud.AddRange(s.BuildRosterPlates());
            hud.AddRange(s.BuildVerbColumn());
            hud.Add(s.BuildBreadcrumb());
            hud.Add(s.BuildContinueButton());
            hud.Add(s.BuildSubmenuColumn());
            hud.Add(s.BuildDetailColumn());
            hud.Add(s.BuildTargetPrompt());
            hud.Add(s.BuildIntentTooltip());
            hud.Add(s.BuildPartyBuffTooltip());
            hud.Add(s.BuildSpellVfx());
            hud.Add(s.BuildDamagePopups());

            // The character sheet, reachable mid-fight.
            //
            // Readable, never editable: ScreenRegistry sets lockedForFight on
            // this copy, so a player can check what they are wearing against
            // what is hitting them without being able to re-plate between
            // swings. Declared BEFORE the reckoning and the defeat screen, so
            // an end-of-fight modal still draws over it.
            // The overarching menu, LAST in this scene's children so it draws
            // over everything it can be opened on top of.
            var systemMenu = SystemMenuScreen.Build();
            s.SystemMenu = systemMenu;

            // The old paperdoll is gone; the system menu's Character pane is
            // the character screen now. SheetPanel opens the menu instead, so
            // every caller -- C, I, Escape, the hub's building -- reaches the
            // dossier without any of them knowing the screen changed.

            // LAST, so they draw over the whole stage they dim.
            var reckoning = ReckoningScreen.Build();
            s.Reckoning = reckoning;
            hud.Add(reckoning.Root);

            var defeat = DefeatScreen.Build();
            s.Defeat = defeat;
            hud.Add(defeat.Root);

            // The overarching menu is the LAST child of all: it can be opened
            // on top of the reckoning and the defeat screen, so it has to draw
            // over them too.
            hud.Add(systemMenu.Root);

            // Everything from the stages down lives in its OWN nested
            // canvas at a sortingOrder well clear of the root's, so "the HUD
            // draws over the backdrop and the scrim" is a sorting fact
            // rather than a fact about one flat canvas's child order. It
            // also gives the HUD its own GraphicRaycaster, which is what
            // lets a pointer reach a submenu row at all -- Unity books a
            // raycastable Graphic against its NEAREST enclosing Canvas, not
            // the outermost one (see FightSubmenuScrollTests).
            children.Add(Ui.NestedCanvas("FightHud", 1000, hud.ToArray()));

            s.Root = Ui.Panel("FightPanel", UiSize.Fill, children);
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
                    UiSize.FillWidth(480f))
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
                    UiSize.FillWidth(440f))
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
                                  List<NodeRef> intentIcons = null,
                                  List<NodeRef> hitAreas = null)
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
                hitAreas?.Add(default);
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

                // the actor stands ON its own shadow - the sprite covering it
                // is the point
                // The ground contact shadow, straddling the slot's own ground
                // line. Anchor-stretched to a fraction of the slot's WIDTH
                // rather than given a fixed one, so it scales with whatever size
                // the runtime gives the slot from the real sprite: a boss's ring
                // is wider than Shawn's for free, with no runtime code at all.
                // A baked ring, not a Solid. An Image with no sprite draws a
                // filled RECTANGLE, which is what a flat quad here looked like:
                // a red brick under each monster's feet. Same bug class as the
                // hub's borrowed plot art, and the reason `proc:` keys exist.
                // ASDECOR ON ALL THREE, AND IT IS ABOUT CLICKS RATHER THAN
                // DRAWING.
                //
                // A stage figure is painted, never pressed: the sprites are
                // bound as Image[] and nothing wires a click to one -- targeting
                // goes through the enemy PLATE in the HUD list. Without AsDecor
                // their Images keep raycastTarget, and an Image raycasts against
                // its RECT rather than its alpha, so each monster was a full
                // rectangular click-blocker standing on the stage.
                //
                // The stage is declared after the command UI, so it sits on top
                // of it. Open the skill list against three monsters and the rat
                // covers three of its rows -- and swallowed every click on them.
                // That is the "the buttons are broken in the fight": not the
                // buttons, the transparent corner of a rodent lying over them.
                //
                // Nothing could see it. UiAudit checks overlap between SIBLINGS
                // and these are not siblings; the wiring sweep checks references
                // and they are all present; and every test in the suite clicks
                // through button.onClick.Invoke(), which bypasses the
                // EventSystem and therefore cannot notice a blocker at all.
                //
                // AsDecor covers a node's whole subtree, so the shadow carries
                // the glow parented under it. The intent badge is deliberately
                // NOT in this list -- it is a Button and has to stay hoverable.
                var shadow = Ui.Sprite($"{prefix}{slot}FootShadow", "proc:ring_outline",
                        Place.Frac(new UiVec(0.32f, 0f), new UiVec(0.68f, 0f), bottom: -8f, top: -8f), UiSize.Fill)
                    .Coloured(shadowHex)
                    .AsDecor()
                    .AllowOverflow("the contact shadow straddles the ground LINE, so half of it is below the slot by construction");
                shadow.Children.Add(glow);

                var sprite = Ui.Sprite($"{prefix}{slot}Sprite", null, Place.Stretch(), UiSize.Fill)
                    .Inactive()
                    .AsDecor();

                // the hit flash IS the sprite's silhouette redrawn white -
                // sharing its box is the whole mechanism
                var flash = Ui.Sprite($"{prefix}{slot}HitFlash", null, Place.Stretch(), UiSize.Fill)
                    .Inactive()
                    .AsDecor();

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
                // ---- the figure itself, as a target -----------------------
                //
                // CLICKING THE MONSTER is what a player tries first, and until
                // now the only way to pick one was the plate in the corner --
                // which meant looking away from the thing being pointed at to
                // point at it.
                //
                // A DEDICATED BUTTON, INACTIVE UNTIL TARGETING, and both halves
                // are load-bearing. It cannot be the sprite: an Image raycasts
                // against its RECT, not its alpha, so a monster with a
                // transparent corner is a rectangular click-blocker standing
                // over whatever is behind it -- which is exactly the bug the
                // AsDecor comment above records, where a rat swallowed three
                // rows of the skill list. This one is a separate node that
                // exists only in the state where a click on a monster means
                // something, so outside targeting it raycasts nothing at all.
                //
                // Sized to the figure's own footprint rather than the slot,
                // which the runtime resizes to the real sprite: a wide box on a
                // narrow monster is the same blocker in a smaller costume.
                UiNode hitArea = null;
                if (!mirrored)
                {
                    // NoChrome, NOT AsDecor and not merely a null SpriteKey.
                    // The monster is the button and this is only the area that
                    // hears the click, so it must draw nothing -- but AsDecor
                    // takes raycasting away with the drawing, and an empty
                    // SpriteKey selects the shared button face, which is what
                    // put three gold slabs over the monsters the first time
                    // this ran. NoChrome keeps the Image, keeps the raycast,
                    // and paints nothing; see UiEmitter.EmitButton.
                    hitArea = Ui.Button($"EnemyHitArea{slot}", UiString.Runtime,
                            Place.Frac(new UiVec(0.18f, 0f), new UiVec(0.82f, 0.92f)), UiSize.Fill, 1)
                        .NoChrome()
                        .Inactive();

                    hitAreas[slot] = hitArea;
                }

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

                var kids = new List<UiNode> { shadow, sprite, flash, nameplate };
                if (hitArea != null) kids.Add(hitArea);
                if (intent != null) kids.Add(intent);

                var slotNode = Ui.Panel($"{prefix}{slot}Slot",
                        Place.At(offset.X, offset.Y, new UiVec(0.5f, 0f)),
                        UiSize.Fixed(320f, 200f),
                        kids.ToArray())
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
                    .AllowOverflow("the emphasis ring frames the icon from OUTSIDE it, which is what makes the acting combatant pop");

                // Inactive until a combatant is actually in the slot. An Image
                // with no sprite renders as a WHITE QUAD, so six empty slots on
                // a fight that has not started drew six white squares across the
                // top-left corner -- caught by looking at the screenshot, which
                // no audit would have flagged because the box is the right size.
                var icon = Ui.Sprite($"InitiativeIcon{i}", null, Place.Stretch(), UiSize.Fill).Inactive();

                var label = Ui.Label($"InitiativeLabel{i}", UiString.Runtime,
                        new UiVec(FightStageAnchors.InitiativeIconSize, 24f), 14,
                        FightHudPalette.TextPrimary, Place.At(0f, 0f));

                // ONE BADGE, three layers: the emphasis ring, the portrait, and
                // the initial printed on it as the fallback for a combatant
                // with no art. Declared as a stack rather than given an
                // AllowOverlap apiece -- they are exempt from each other and
                // still checked against the rest of the slot.
                Ui.Layered(ring, icon, label);

                InitiativeRings.Add(ring);
                InitiativeIcons.Add(icon);
                InitiativeLabels.Add(label);

                return Ui.Panel($"InitiativeSlot{i}", Place.Flow,
                    UiSize.Fixed(FightStageAnchors.InitiativeIconSize, FightStageAnchors.InitiativeIconSize),
                    ring, icon, label);
            });

            // Pushed down below BarkHeight (plus a gap) now that the combat log
            // sits flush against the true top of the screen instead of 58px
            // short of it -- the two used to clear each other by a few pixels
            // at the log's old position; a log actually flush to the top would
            // otherwise draw over these icons, since BuildBark is added as a
            // later sibling and later siblings paint over earlier ones.
            var tracker = Ui.Row("InitiativeTracker",
                Place.Pin(new UiVec(0f, 1f), new UiVec(0f, 1f), new UiVec(28f, -(BarkHeight + 16f))),
                FightStageAnchors.InitiativeIconGap, UiAlign.Centre, entries);

            InitiativeTracker = tracker;
            return tracker;
        }

        // ---- the dialogue bark (the combat log's home) ------------------------

        // Bark height, named rather than inlined -- InitiativeTracker's own Y
        // offset is pushed down by exactly this much below, so the two boxes
        // stack instead of overlapping. Change one, change the other.
        private const float BarkHeight = 130f;

        // NO BOX. Used to be a plain black plate the full width of the screen
        // -- a placeholder the comment here admitted was never meant to ship
        // -- and it read as a slab dropped over the battlefield. The log now
        // floats: same position, same reach, just nothing painted behind it.
        //
        // Still a Panel rather than the label sitting bare at the top level,
        // because the portrait and the label need one parent to be pinned and
        // sized together. A colourless Panel emits no Image at all
        // (UiNode.EmitsNoGraphic), so this costs nothing to draw and cannot
        // intercept a click either -- the AsDecor below is about UiAudit's
        // overlap check, not about hiding a graphic that no longer exists.
        private UiNode BuildBark()
        {
            var portrait = Ui.Sprite("BarkPortrait", null, new UiVec(56f, 56f), Place.At(-880f, -8f)).Inactive();

            // Full box width minus the portrait's own column, so four joined
            // lines at 20pt (roughly 100px including line spacing) actually fit
            // the box instead of spilling past it -- TMP's default overflow is
            // unclamped, so a box too short for its own text was the other half
            // of "pops in one go and overflows."
            var label = Ui.Label("MessageLabel", UiString.Runtime, new UiVec(1780f, 108f), 20,
                FightHudPalette.TextPrimary, Place.At(-820f, -8f, new UiVec(0f, 0.5f)));

            BarkPortrait = portrait;
            BarkLabel = label;

            // AsDecor, same as IntentTooltip and the target-prompt banner: a
            // log the player is reading is meant to sit visually over the
            // enemy heading/plates it now reaches at full width, and must not
            // steal the clicks those plates exist to receive. This is also
            // what clears UiAudit's SiblingOverlap check for that overlap --
            // the check exists to catch a decorative-looking node accidentally
            // eating real clicks, which AsDecor is the actual fix for, not
            // just the silencer.
            var bark = Ui.Panel("DialogueBark", Place.Pin(new UiVec(0.5f, 1f), new UiVec(0.5f, 1f), UiVec.Zero),
                    UiSize.Fixed(1920f, BarkHeight), portrait, label)
                .AsDecor();
            BarkPanel = bark;
            return bark;
        }

        // ---- enemy plates ------------------------------------------------------

        // HALF THE SIZE, AND TWO ABREAST. 400x104 stacked three deep was a
        // column of cards down a quarter of the screen's height for information
        // that is a name, a number and a bar. At 200x56 in two columns the same
        // three plates occupy two rows in the same width, and the space that
        // buys goes back to the battlefield they are describing.
        private const float PlateW = 200f;

        // 64 RATHER THAN THE 52 THAT WOULD BE EXACTLY HALF, and the twelve is
        // the status line. A plate is three rows -- name and HP, the bar, and
        // whatever the monster is currently suffering -- and "9999/9999" at
        // 11pt measures 28.6 tall on its own, which UiTextFitAudit refuses to
        // put in a box that cannot hold it. Halving the height exactly means
        // dropping the status row, and an enemy's Vulnerable is not decoration.
        private const float PlateH = 64f;
        private const float PlateGap = 16f;
        private const int PlateColumns = 2;

        // The block's RIGHT edge is unchanged, which is the anchor that matters
        // -- it sits against the same margin the heading and the hint do.
        private const float PlateBlockRight = 920f;
        private const float PlateFirstX =
            PlateBlockRight - (PlateColumns * PlateW + (PlateColumns - 1) * PlateGap) + PlateW * 0.5f;

        private const float PlatePitchX = PlateW + PlateGap;
        private const float PlatePitch = PlateH + 12f;

        // The icon that tells two of the same monster apart at a glance, before
        // the name is read. The actor's own idle sprite, fitted -- no new art,
        // and it cannot disagree with the figure on the stage because it IS the
        // figure on the stage.
        private const float PlateIconSize = 40f;
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

        // FULL-CANVAS, ABOVE THE STAGE AND BELOW EVERY INFORMATIONAL PANEL --
        // built right after the two stage racks for exactly that stacking
        // order, the same "declaration order is paint order" rule the scrim
        // itself relies on (see BuildScrim's own header). AsDecor because it
        // must never eat a click meant for a plate or the battlefield behind
        // it; Inactive because it is a STATE, not ambient art -- shown only
        // while some living ally sits below the low-HP threshold, decided at
        // FightController.Hud's RefreshLowHpVignette.
        private UiNode BuildLowHpVignette()
        {
            var vignette = Ui.Sprite("LowHpVignette", "proc:vignette", Place.Stretch(), UiSize.Fill)
                .Coloured(FightHudPalette.HpDeep)
                .AsDecor()
                .Inactive();
            LowHpVignette = vignette;
            return vignette;
        }

        private IEnumerable<UiNode> BuildEnemiesHeading()
        {
            // 155 wide, not v1's 200. At 200 this box ran to x 720 while the
            // right-aligned hint's box starts at 680 -- a 40px crossing the
            // audit refuses. "E N E M I E S" at 12pt bold is well under 155.
            yield return Ui.Label("EnemiesHeading", UiStrings.EnemiesHeading, new UiVec(155f, 24f), 12,
                FightHudPalette.TextMuted,
                Place.At(PlateFirstX - PlateW * 0.5f, PlateFirstY + 50f, new UiVec(0f, 0.5f)));

            var hint = Ui.Label("EnemiesHint", UiStrings.StandingCount, new UiVec(240f, 24f), 12,
                FightHudPalette.TextDisabled,
                Place.At(PlateBlockRight, PlateFirstY + 50f, new UiVec(1f, 0.5f)));
            EnemiesHint = hint;
            yield return hint;
        }

        // The plates are BUTTONS as well as readouts -- targeting reuses them
        // rather than opening a fourth column, which keeps the player's eye on
        // the column they are already reading rather than moving it somewhere
        // new mid-decision. The figures on the stage are pressable too now; see
        // EnemyHitAreas.
        private IEnumerable<UiNode> BuildEnemyPlates()
        {
            return Ui.Each(Enumerable.Range(0, FightHudSpec.EnemyPlates).ToList(), (_, i) =>
            {
                float left = -PlateW * 0.5f;

                var icon = Ui.Sprite($"EnemyPlate{i}Icon", null,
                        new UiVec(PlateIconSize, PlateIconSize),
                        Place.At(left + 4f + PlateIconSize * 0.5f, 0f))
                    .Inactive()
                    .AsDecor();

                // Everything else starts to the right of the icon.
                float textLeft = left + 8f + PlateIconSize;

                var name = Ui.Label($"EnemyPlate{i}Name", UiString.Runtime, new UiVec(86f, 20f), 13,
                    FightHudPalette.EnemyName, Place.At(textLeft, 17f, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left);

                // 27 tall for a 10pt line: the audit measures "9999/9999" at
                // 26 there, and a box that cannot hold it clips the descenders
                // off the number the plate exists for. 10 rather than 11
                // because the status line moved above the bar and the three
                // rows now have to share 64px.
                var hp = Ui.Label($"EnemyPlate{i}Hp", UiStrings.HealthValue, new UiVec(56f, 27f), 10,
                    FightHudPalette.EnemyHpText, Place.At(PlateW * 0.5f - 8f, 17f, new UiVec(1f, 0.5f)))
                    .TextAligned(UiTextAlign.Right);

                var fill = Ui.Solid($"EnemyPlate{i}HpFill", FightHudPalette.HpBright, Place.Stretch(), UiSize.Fill);
                // ---- ABOVE THE BAR, NOT BELOW IT ---------------------------
                //
                // The order on the plate is now name, what is wrong with them,
                // then how much of them is left. Underneath, the status line sat
                // in the plate's bottom margin against its border and read as a
                // footnote to the bar rather than as a fact about the monster --
                // and the bar, which is the thing being watched, had text on
                // both sides of it.
                //
                // 8pt in a 12-tall box: it is a word or two of shorthand, and
                // the row it is squeezing into came out of the space the old
                // one had.
                var tags = Ui.Label($"EnemyPlate{i}Tags", UiString.Runtime, new UiVec(142f, 12f), 8,
                    FightHudPalette.TextSecondary, Place.At(textLeft, -4f, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left);

                var bar = Ui.Panel($"EnemyPlate{i}Bar",
                        Place.At(textLeft + 71f, -18f), UiSize.Fixed(142f, 7f), fill)
                    .Coloured(FightHudPalette.Track);

                // A 12px square rotated 45 degrees, pinned just outside the
                // plate's left edge. Visible only while targeting.
                var reticle = Ui.Solid($"EnemyPlate{i}Reticle", FightHudPalette.TargetAmber,
                        new UiVec(12f, 12f), Place.At(-PlateW * 0.5f - 9f, 0f))
                    .Rotated(45f)
                    .Inactive()
                    .AllowOverflow("the reticle is deliberately OUTSIDE the plate - a marker in the margin, not a badge on the card");

                // THE BREAK METER, hanging BELOW the plate rather than inside
                // it. There is no room left in 64px -- three rows already
                // share it (see PlateH's own comment) -- but the 12px gap
                // PlatePitch leaves before the next row down is real, unused
                // space, and a 6px bar fits it with margin either side. Same
                // fill mechanism the HP bar already uses (SetFill), same
                // width and x-position, one row lower. Only an elite or boss
                // carries a BreakShield at all, so this stays hidden for
                // everything else.
                var breakFill = Ui.Solid($"EnemyPlate{i}BreakFill", FightHudPalette.TargetAmber,
                    Place.Stretch(), UiSize.Fill);
                var breakTrack = Ui.Panel($"EnemyPlate{i}BreakTrack",
                        Place.At(textLeft + 71f, -PlateH * 0.5f - 6f), UiSize.Fixed(142f, 6f), breakFill)
                    .Coloured(FightHudPalette.Track)
                    .Inactive()
                    .AllowOverflow("the break meter hangs in the 12px row gap below the plate, not inside it - see PlatePitch");

                EnemyPlateIcons.Add(icon);
                EnemyPlateNames.Add(name);
                EnemyPlateHps.Add(hp);
                EnemyPlateHpFills.Add(fill);
                EnemyPlateTags.Add(tags);
                EnemyPlateReticles.Add(reticle);
                EnemyPlateBreakTracks.Add(breakTrack);
                EnemyPlateBreakFills.Add(breakFill);

                // 1.03 hover, no press pop: the plate carries four pieces of
                // text, so the press animator's 1.05/0.95 would swing them all
                // sideways under the cursor. v1 made the same call at the same
                // number.
                var plate = Ui.Button($"EnemyPlate{i}", UiString.Runtime, new UiVec(PlateW, PlateH), 1,
                    Place.At(PlateFirstX + i % PlateColumns * PlatePitchX,
                             PlateFirstY - i / PlateColumns * PlatePitch)).Hovers(1.03f);
                plate.SpriteKey = PanelCrimson;
                plate.Children.Add(icon);
                plate.Children.Add(name);
                plate.Children.Add(hp);
                plate.Children.Add(tags);
                plate.Children.Add(bar);
                plate.Children.Add(reticle);
                plate.Children.Add(breakTrack);
                plate.Inactive();

                EnemyPlates.Add(plate);
                return plate;
            });
        }

        // ---- the party plate ----------------------------------------------------

        // BLUE, 2:1 CONTAINER ART -- the character's own stat card, so the
        // informational/defensive theme (the same one HOLD BACK uses in the
        // verb column) rather than Crimson, which the old panel_crimson.png
        // fallback wore for reasons that had nothing to do with theme (see
        // FallbackPlateKey, still Crimson for the enemy plates that use it).
        // FightHudPalette itself never tied this slot to a colour -- its hex
        // tokens are HP/MP/text roles, not a per-plate theme -- so there was
        // nothing here to defer to.
        //
        // 452x216 (aspect 2.09) was 5.6% off the kit's measured 2:1 aspect
        // (1.98, see ContainerArt.ContainerAspect2x1) -- outside the 5% band
        // Ui.Container refuses. WIDTH STAYS 452 (FightScreenTests pins it);
        // the nudge is entirely in HEIGHT, 216 -> 452/1.98 = 228.28.
        //
        // THE BOTTOM EDGE IS THE ANCHOR, not the centre: -500 was already the
        // canvas's own 40px HUD margin above the floor (UiFrames.Reference.Y
        // * -0.5 + 40). Growing the plate by keeping its old centre Y would
        // have moved that margin; growing it from the bottom up keeps the
        // margin and pushes the top edge up by the same 12.28px instead.
        // Public: FightScreenTests (a separate assembly, no InternalsVisibleTo
        // grant to it) reads these rather than restating the numbers.
        public const float PartyPlateWidth = 452f;
        public const float PartyPlateHeight = PartyPlateWidth / ContainerArt.ContainerAspect2x1; // 228.28, was 216
        private const float PartyPlateCentreX = -694f;
        private const float PartyPlateBottomY = -500f; // -392 - 108: the old plate's bottom edge, preserved
        private const float PartyPlateCentreY = PartyPlateBottomY + PartyPlateHeight * 0.5f; // -385.86, was -392

        private UiNode BuildPartyPlate()
        {
            var portrait = Ui.Sprite("PartyPortrait", null, new UiVec(64f, 64f), Place.At(-170f, 54f)).Inactive();

            // A CHILD of the portrait, not a sibling of the plate -- nesting it
            // here means A1 never has to reason about it (a child overlapping
            // its own parent on purpose is not the case that check exists to
            // catch), and it stays inside the portrait's own 64x64 box with
            // room either side, so it needs no AllowOverflow either. Shown only
            // while the acting character still has an unspent Second Life --
            // the one domain state this whole pass adds that was previously
            // invisible end to end (RunSettlement's own revive, not shown
            // anywhere before this).
            var secondLife = Ui.Solid("SecondLifeBadge", FightHudPalette.TargetAmber,
                    new UiVec(11f, 11f), Place.At(26f, -26f))
                .Inactive();
            SecondLifeBadge = secondLife;
            portrait.Children.Add(secondLife);

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

            // THE MANA-COST OVERLAY (PartyMpPreview) IS GONE, balance-bot
            // 2026-09-02: it drew a lighter segment inside the fill showing
            // what a hovered skill would cost, which is now double-booked
            // with the numeric cost already on the skill row/detail card --
            // and it stayed live even while the player was casting, tracking
            // a cost that no longer meant "if you pick this". The numeric
            // cost is the one source of truth now.
            var mpBar = Ui.Panel("PartyMpBar", Place.At(-24f, -22f), UiSize.Fixed(292f, 16f), mpFill)
                .Coloured(FightHudPalette.TrackMp);
            var mpValue = Ui.Label("PartyMpValue", UiStrings.HealthValue, new UiVec(70f, 20f), 12,
                FightHudPalette.MpText, Place.At(194f, -22f, new UiVec(1f, 0.5f)));

            PartyPortrait = portrait;
            PartyName = name;
            PartyClass = cls;
            PartyHpFill = hpFill;
            PartyHpValue = hpValue;
            PartyMpFill = mpFill;
            PartyMpValue = mpValue;

            var plate = Ui.Container("PartyPlate", ButtonTheme.Blue, ContainerRatio.TwoByOne,
                Place.At(PartyPlateCentreX, PartyPlateCentreY),
                new UiVec(PartyPlateWidth, PartyPlateHeight));

            var content = new List<UiNode>
            {
                portrait, name, cls, hpTag, hpBar, hpValue, mpTag, mpBar, mpValue, BuildWoolRow(),
            };
            content.AddRange(BuildPartyBuffIcons());
            Ui.ContainerContent(plate, ContainerRatio.TwoByOne, "PartyPlateContent", content.ToArray());

            PartyPlate = plate;
            return plate;
        }

        // The party plate's own top edge in world Y -- its bottom edge
        // (PartyPlateBottomY) plus its own height. Both the transformation
        // strip and the roster stack build off this rather than off a
        // restated number, so moving the plate moves them with it.
        private const float PartyPlateTopY = PartyPlateBottomY + PartyPlateHeight;

        // FUSED TO THE PLATE'S TOP EDGE, not inside it -- the header row two
        // labels above already leaves a 2px gap between PartyName and
        // PartyClass, which is not room for a third fact. A separate strip
        // immediately above costs nothing the plate itself would have to give
        // up. Shown only while the acting character carries a Transformation
        // (Black Ram Mode today) -- the only one of the four domain systems
        // this pass surfaces that changes what the plate above it means while
        // it's up, so it reads as fused to that plate rather than as a
        // floating fourth panel.
        private const float TransformStripH = 36f;

        private UiNode BuildTransformStrip()
        {
            var text = Ui.Label("TransformStripText", UiString.Runtime, new UiVec(400f, 24f), 14,
                FightHudPalette.GoldText, Place.At(0f, 0f));
            TransformStripText = text;

            var strip = Ui.Panel("TransformStrip",
                    Place.At(-694f, PartyPlateTopY + TransformStripH * 0.5f),
                    UiSize.Fixed(452f, TransformStripH), text)
                .Coloured(FightHudPalette.PanelActive)
                .Inactive();
            TransformStrip = strip;
            return strip;
        }

        // Two slots -- the stage fields up to StageSlotsPerSide (3) party
        // members and the plate above already shows the acting one, so two is
        // every OTHER member there is room on stage for. Information only,
        // same as the intent icons: nothing here is clickable, there is no
        // swap-who's-acting mechanic to wire.
        //
        // RESERVES ROOM FOR THE TRANSFORM STRIP WHETHER OR NOT IT IS SHOWING.
        // The alternative is repositioning this stack's own RectTransform at
        // runtime whenever Transformation comes or goes, which would make its
        // vertical position a second thing the controller has to keep in sync
        // with a fact the strip already displays. A fixed 44px gap costs
        // nothing when the strip is hidden and avoids that entirely.
        private const int RosterSlots = 2;
        private const float RosterPlateW = 452f;
        private const float RosterPlateH = 44f;
        private const float RosterGap = 6f;
        private const float RosterFirstY =
            PartyPlateTopY + TransformStripH + RosterGap + RosterPlateH * 0.5f;
        private const float RosterPitchY = RosterPlateH + RosterGap;

        private IEnumerable<UiNode> BuildRosterPlates()
        {
            return Ui.Each(Enumerable.Range(0, RosterSlots).ToList(), (_, i) =>
            {
                // TWO ROWS, not one: name/HP-value share the top at y9 (spans
                // -2..20), the bar owns the bottom at y-11 (spans -14..-8) --
                // a 6px gap between them rather than three elements fighting
                // for one row's width the way the first draft of this row did
                // (Name and HpBar overlapping 54px, caught by A1 at build).
                var name = Ui.Label($"Roster{i}Name", UiString.Runtime, new UiVec(140f, 22f), 14,
                    FightHudPalette.TextPrimary, Place.At(-206f, 9f, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left);

                var hpValue = Ui.Label($"Roster{i}HpValue", UiStrings.HealthValue, new UiVec(70f, 20f), 11,
                    FightHudPalette.HpText, Place.At(206f, 9f, new UiVec(1f, 0.5f)))
                    .TextAligned(UiTextAlign.Right);

                var hpFill = Ui.Solid($"Roster{i}HpFill", FightHudPalette.HpBright, Place.Stretch(), UiSize.Fill);
                var hpBar = Ui.Panel($"Roster{i}HpBar", Place.At(0f, -11f), UiSize.Fixed(412f, 6f), hpFill)
                    .Coloured(FightHudPalette.Track);

                RosterNames.Add(name);
                RosterHpValues.Add(hpValue);
                RosterHpFills.Add(hpFill);

                var plate = Ui.Panel($"Roster{i}", Place.At(-694f, RosterFirstY + i * RosterPitchY),
                        UiSize.Fixed(RosterPlateW, RosterPlateH), name, hpValue, hpBar)
                    .Coloured(FightHudPalette.PanelPrimary)
                    .Inactive();
                RosterPlates.Add(plate);
                return plate;
            });
        }

        // Up to four badges in a row above the portrait, reading whatever
        // FightHudModel.BuffBadgesFor says is currently on the acting
        // character. The portrait had nothing here before this -- a buff a
        // relic granted was visible nowhere except a menu that does not even
        // say it fired (see FightHudModel's BuffBadge comment).
        //
        // Two of these ten statuses have real icon art now (Chilled, Rooted
        // -- see FightHudModel.StatusBadgeIcons and
        // tools/art/make_status_icons.py); the rest are still the same
        // "no art authored" shape the rest of this project already uses for
        // that state -- a plain button face, tinted at runtime rather than
        // switching sprites. RefreshPartyBuffs decides per-badge which one
        // applies; no structural change was needed here to carry a sprite,
        // because Ui.Button already gives this node the same Image + synthesised
        // TMP caption pairing the enemy-intent badges use. Four is a guess
        // at how many a character plausibly carries at once, not a measured
        // limit.
        private const int PartyBuffSlots = 4;

        // 27x27, UP FROM 18x18 (x1.5, debuff/buff icon pass, balance-bot
        // 2026-09-02) -- pitch scaled with it, 33 up from 22, so the gap
        // between badges (pitch minus size) grows in the same proportion
        // rather than shrinking toward a collision.
        private const float PartyBuffIconSize = 27f;
        private const float PartyBuffPitch = 33f;

        // Y MOVED, 90 -> 30, because 90 no longer fits. The old row's band
        // (above the header, below the container's own top inset -- see the
        // comment this replaced) is 101.59 - 78 = 23.58px tall, and a 27px
        // icon does not fit inside it at any Y. The header row (PartyName,
        // y46-78) and the HP row (PartyHpBar/PartyHpValue, top edge 14) leave
        // a second, wider band between them -- y14 to y46, 32px tall, clear of
        // both -- and portrait's own right edge (-138) still bounds it on the
        // left, so the badges still cannot land on the portrait.
        private const float PartyBuffY = 30f;

        // Left edge of PartyBuff0 clears the portrait's right edge (-138) by
        // 13.5px, scaled up from the old row's 9px clearance (-129 - -138) by
        // the same x1.5 as the icon itself.
        private const float PartyBuffX0 = -111f;

        private IEnumerable<UiNode> BuildPartyBuffIcons()
        {
            PartyBuffIcons.Clear();

            for (int i = 0; i < PartyBuffSlots; i++)
            {
                float x = PartyBuffX0 + i * PartyBuffPitch;
                var icon = Ui.Button($"PartyBuff{i}", UiString.Runtime,
                        new UiVec(PartyBuffIconSize, PartyBuffIconSize), 11,
                        Place.At(x, PartyBuffY))
                    .NoChrome()
                    .Inactive();

                PartyBuffIcons.Add(icon);
                yield return icon;
            }
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

            // 410 wide, not the plate's own 452: the row's actual content
            // (divider/tag/pips/value) already tops out at +-202, but the
            // PANEL's own declared box is what A2 checks against the
            // container's inset, and the full 452 span (+-226) sat 15.82px
            // past the container's left/right content bound (+-210.18).
            // 410 (+-205) clears both: 3px inside its own content, 5.18px
            // inside the container.
            var row = Ui.Panel("WoolRow", Place.At(0f, -66f), UiSize.Fixed(410f, 40f), children);
            WoolRow = row;
            return row;
        }

        // ---- column A: the verbs -------------------------------------------------

        private const float VerbColumnX = -286f;
        private const float VerbRowW = 300f;
        private const float VerbRowH = 52f;
        private const float VerbPitch = 62f;

        // Rendered BOTTOM-UP so ATTACK sits nearest the cursor, and tiered so
        // read order matches use frequency: ATTACK loud, SKILL/ITEM neutral,
        // HOLD BACK quiet. That hierarchy is the main fix over v1's earlier
        // row of five identical gold buttons.
        //
        // RUN REMOVED, not hidden. It never actually fled a fight -- the
        // handler behind it always answered "There is no way out of this
        // one." and there was no Flee/Run method anywhere in FightSession to
        // wire it to. A command that exists only to refuse itself is worse
        // than no command, so it is gone rather than joining HOLD BACK's old
        // spot as a second hidden-but-wired row.
        private IEnumerable<UiNode> BuildVerbColumn()
        {
            var labels = new[]
            {
                UiStrings.VerbAttack, UiStrings.VerbSkill, UiStrings.VerbItem,
                UiStrings.VerbHoldBack,
            };
            var hotkeys = new[]
            {
                UiStrings.HotkeyOne, UiStrings.HotkeyTwo, UiStrings.HotkeyThree,
                UiStrings.HotkeyFour,
            };
            var nests = new[] { false, true, true, false };

            // ATTACK/SKILL/ITEM/HOLD BACK, in BuildVerbColumn's own fixed
            // order -- no RUN verb to give Silver, so this is four of the
            // kit's six themes rather than the five-verb mapping CLAUDE.md
            // states for a screen that still has one.
            var themes = new[]
            {
                ButtonTheme.Crimson, ButtonTheme.Violet, ButtonTheme.Green, ButtonTheme.Blue,
            };

            return Ui.Each(labels, (label, i) =>
            {
                bool primary = i == 0;
                bool quiet = i >= 3;

                var hotkey = Ui.Label($"Verb{i}Hotkey", hotkeys[i], new UiVec(24f, 20f), 12,
                    quiet ? FightHudPalette.QuietHotkey : FightHudPalette.LoudHotkey,
                    Place.At(-134f, 0f, new UiVec(0f, 0.5f)));

                // "Text", not "Label": ThemedPlate() (like Themed()) reserves
                // `<Button>Label` for a generated caption -- but this row is
                // CaptionPreserving, so that name is never used at all here.
                // Kept as "Text" anyway so the two vocabularies read the same
                // way at every call site.
                var text = Ui.Label($"Verb{i}Text", label, new UiVec(200f, 26f), primary ? 19 : 17,
                    quiet ? FightHudPalette.TextMuted : FightHudPalette.TextPrimary,
                    Place.At(-104f, 0f, new UiVec(0f, 0.5f)))
                    .Styled(TypographyRole.ButtonLabel);

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
                //
                // ThemedPlate(), not Themed(): this row already declares its
                // own caption (hotkey + text + caret) below rather than a
                // single centred string, so the label-generating mode would
                // either draw a second, blank label under the real one or
                // force this row to restructure its own DSL declaration.
                // ThemedPlate() gives it Visuals(Glow, Plate) and nothing
                // else -- the shared gold-outline row art EmitButton would
                // otherwise fall back to is never reached, since the Themed
                // branch returns before that fallback exists.
                var row = Ui.Button($"Verb{i}", UiString.Runtime, new UiVec(VerbRowW, VerbRowH), 1,
                    Place.At(VerbColumnX, CommandBottom + 26f + i * VerbPitch))
                    .ThemedPlate(themes[i]);
                row.Children.Add(hotkey);
                row.Children.Add(text);
                row.Children.Add(caret);

                // Visuals stretches over the whole row on purpose -- the
                // caption sits ON its own plate, the same relationship
                // Themed()'s Visuals/Label pair states with Layered(). Without
                // this A1 reports the plate colliding with every word on it.
                row.LayerCaptionWithVisuals(hotkey, text, caret);

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
            // GOLD, not one of the four verb colours: this is the recommended-
            // next-action button (see CLAUDE.md's colour reservation) rather
            // than a combat choice - it only ever appears once a beat has
            // resolved and there is exactly one thing to do next.
            var button = Ui.Button("ContinueButton", UiStrings.Continue, new UiVec(300f, 80f), 20,
                    Place.At(-286f, -420f))
                .Themed(ButtonTheme.Gold)
                .Inactive()
                .AllowOverlap("Continue swaps footprints with the verb column - the verbs are fully hidden whenever it is up, so the two are never both live");
            ContinueButton = button;
            return button;
        }

        // ---- column B: the submenu -------------------------------------------------

        // 282, DOWN FROM 404 -- thirty percent off. The rows carry a name and
        // nothing else now, so the width was sized for a meta line and a cost
        // that are two columns away.
        //
        // THE LEFT EDGE STAYS PUT and the panel shrinks rightward, which is why
        // SubmenuX moved with the width rather than staying at 86. The panel
        // belongs to the verb column it opens from and sits against it; taking
        // the width off both sides would have opened a gap there and closed one
        // on the battlefield, which is the half of the screen the narrowing is
        // for.
        // Reads FightSubmenuLayout.RowWidth rather than restating 282 --
        // the art frame in BuildSubmenuFrame has to size itself off the same
        // number these rows are actually built at.
        private const float SubmenuRowW = FightSubmenuLayout.RowWidth;

        // DERIVED FROM THE FRAME'S FLUSH POSITION, not the other way round,
        // balance-bot 2026-09-02. SubmenuX used to be the fixed point (25)
        // and the frame's centre (ContainerX below) was built outward from
        // it; now the frame's LEFT EDGE is the fixed point (flush against the
        // verb column's right edge, zero gap) and this is back-solved from
        // it so the rows/viewport/scrollbar/BACK still land correctly inside
        // the frame's own measured content inset -- see ContainerX's comment
        // for why moving one without the other overflows the frame by
        // exactly the same amount the frame moved.
        //
        // Was a flat 25; is now ~38.61, since the frame had to move right by
        // 13.61px (VerbColumnX + VerbRowW*0.5 + FrameWidth*0.5 versus the old
        // containerX) to close the 13.61px OVERLAP the wider (post-01bc943)
        // 3:4 frame had opened with the verb column.
        private static float SubmenuX =>
            ContainerX - FightSubmenuLayout.ContainerWidth * 0.5f + SubmenuRowW * 0.5f
            + FightSubmenuLayout.ContainerPad;

        // THE FRAME'S OWN CENTRE X. Flush against the verb column's right
        // edge (VerbColumnX + VerbRowW * 0.5), zero gap -- see
        // BuildSubmenuFrame's own comment for the overlap this replaces.
        private static float ContainerX =>
            VerbColumnX + VerbRowW * 0.5f + FightSubmenuLayout.FrameWidth * 0.5f;

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
            int count = FightSubmenuLayout.PoolSize;
            var rows = Ui.Each(Enumerable.Range(0, count).ToList(), (_, i) =>
            {
                var mark = Ui.Sprite($"CharacterSkill{i}Mark", null, new UiVec(36f, 36f),
                        Place.At(-SubmenuRowW * 0.5f + 24f, 0f))
                    .Inactive();

                // A NAME, AND NOTHING ELSE.
                //
                // A row used to carry four things: a mark, the name, a meta line
                // ("DAMAGE - SINGLE") under it, and the cost right-aligned. Every
                // one of those three extras is also in the detail column two
                // columns over, in more room and in full sentences, and the list
                // is the place a player SCANS. Sixteen rows each saying four
                // things is a table; sixteen rows each saying one thing is a
                // list, and the detail panel is what the list is for.
                //
                // LEFT-ALIGNED now, not centred (balance-bot item 5,
                // 2026-09-03) -- a single-word-to-short-phrase name centred
                // in a wide box drifts around as its own length changes,
                // which reads worse in a scanned list than a name that
                // starts at the same x every row. The box itself already
                // carries the clearance off the mark (its left edge sits
                // 24px clear of the mark's own right edge); TextAlign.Left
                // is the only change, so that clearance becomes the name's
                // left margin instead of half its centring slack.
                var name = Ui.Label($"CharacterSkill{i}Name", UiString.Runtime,
                    new UiVec(SubmenuRowW - 90f, 24f), 18,
                    FightHudPalette.RowNameText, Place.At(21f, 0f))
                    .TextAligned(UiTextAlign.Left);

                // 1.02, the gentler of the two: a submenu row is 404 wide with
                // four columns of text in it, so the press pop would shift all
                // four. Same split v1 used -- verb rows pop, submenu rows hover.
                // PLACED IN COLUMN COORDINATES, inside a content rect whose own
                // frame is the column's. That is what lets RowY stay exactly
                // what it was through the whole scroll rework: the rows do not
                // know they are inside a viewport, and the only thing that
                // moves when the list scrolls is the rect around them.
                // VIOLET, CAPTION-PRESERVING PLATE -- one row pool serves both
                // the Skill and Item branches (CurrentRows() rebinds the same
                // nodes at runtime; the "CharacterSkill" name is the pool's
                // legacy identity, not a claim this is skill-only), so there
                // is one theme rather than a per-branch swap the pool has no
                // seam for. Violet: neither branch has its own colour
                // reservation the way the four verbs do, and it is the kit's
                // neutral default elsewhere (Relic Draft, the Constellation
                // screen). 282x40 (was 282x48, balance-bot item 5, 2026-09-03)
                // is 7.05:1, which the aspect-nearest rule still resolves to
                // Row6x1 (5.92, ln-distance 0.175 against FiveByOne's 0.362)
                // on its own -- no .Plate() override needed. ThemedButtonState's
                // own hover state replaces the manual .Hovers(1.02f) scale-pop
                // this used to drive.
                var row = Ui.Button($"CharacterSkill{i}", UiString.Runtime,
                    new UiVec(SubmenuRowW, FightSubmenuLayout.RowHeight), 1,
                    Place.At(0f, FightSubmenuLayout.RowYInContent(i)))
                    .ThemedPlate(ButtonTheme.Violet);
                row.Children.Add(mark);
                row.Children.Add(name);
                row.LayerCaptionWithVisuals(mark, name);

                SubmenuRows.Add(row);
                SubmenuMarks.Add(mark);
                SubmenuNames.Add(name);
                return row;
            }).ToList();

            // 110 and 150, not 170 each: the rows lost 30% of their width and
            // the two headers over them were sized for the old span, so they
            // crossed by 58px. The hint is the wider of the two because it is
            // the one that grows -- "SHOWING 9 OF 12" when a kit outruns the
            // window.
            float headerY = FightSubmenuLayout.HeaderY(count);
            var title = Ui.Label("SubmenuTitle", UiStrings.SubmenuSkillsTitle, new UiVec(110f, 20f), 11,
                FightHudPalette.GoldLight,
                Place.At(SubmenuX - SubmenuRowW * 0.5f, headerY, new UiVec(0f, 0.5f)));
            var hint = Ui.Label("SubmenuHint", UiStrings.SubmenuHint, new UiVec(150f, 20f), 11,
                FightHudPalette.TextDisabled,
                Place.At(SubmenuX + SubmenuRowW * 0.5f, headerY, new UiVec(1f, 0.5f)));
            SubmenuTitle = title;
            SubmenuHint = hint;

            var children = new List<UiNode>();
            children.AddRange(BuildSubmenuFrame(rows));
            children.Add(title);
            children.Add(hint);

            var column = Ui.Panel("SubmenuColumn", Place.Stretch(), UiSize.Fill, children)
                .Inactive()
                .Opening()
                .AllowOverlap("a full-screen transparent group toggled as one unit; it deliberately draws over the stage, and its own rows are audited against each other normally");
            SubmenuColumn = column;
            return column;
        }

        // THE FRAME AROUND THE SKILL LIST: a plate, a window that clips, the
        // rows inside it, and a bar down the right showing how much of the list
        // is on screen.
        //
        // The rows used to hang on the battlefield with nothing behind them and
        // nothing bounding them, which was survivable while there were never
        // more than eight -- the list simply ended. It does not end now, it
        // scrolls, and a list that scrolls with no edge to scroll against reads
        // as rows appearing out of the air.
        //
        // THE CONTENT RECT IS THE COLUMN'S OWN COORDINATE FRAME, offset so that
        // a child placed at RowY lands where RowY says. That is the trick that
        // kept this rework off the row-placement arithmetic entirely: the rows
        // are authored in exactly the coordinates they always were, and
        // scrolling is one anchoredPosition on their parent.
        private IEnumerable<UiNode> BuildSubmenuFrame(IEnumerable<UiNode> rows)
        {
            float containerW = FightSubmenuLayout.ContainerWidth;

            // ContainerX/SubmenuX are declared together above (both are
            // solved from the flush-left constraint) -- see ContainerX's own
            // comment for why the frame's centre moved and SubmenuX moved
            // with it.
            float containerX = ContainerX;

            // The content: a full-canvas rect so every row in the pool fits
            // inside it, centred on the column's origin so its children's
            // coordinates ARE column coordinates.
            var content = Ui.Panel("SubmenuContent",
                    Place.At(0f, FightSubmenuLayout.ContentRestY),
                    UiSize.Fixed(SubmenuRowW, FightSubmenuLayout.ContentHeight),
                    rows)
                .AllowOverflow("the content rect holds the whole sixteen-row pool and is deliberately taller than the nine-row window it sits in - that overflow IS the scroll, and SubmenuViewport clips it, which is what makes a list longer than nine rows reachable at all");
            SubmenuContent = content;

            // NOT AT THE CONTAINER'S CENTRE ANY MORE. It was, while the frame
            // held nothing but the list; the back row sits in the bottom
            // padding now, so the viewport rides above the container's middle
            // by half a back row and half a gap. UNCHANGED by the Violet 3:4
            // frame below: this is still "offset from the INNER box's own
            // centre" exactly as it always was, and reparenting it under the
            // frame's own (taller) content inset is what recentres it -- see
            // FightSubmenuLayout.FrameHeight's own comment.
            float viewportY = FightSubmenuLayout.ViewportOffsetInContainer;

            var viewport = Ui.Panel("SubmenuViewport",
                    Place.At(SubmenuX - containerX, viewportY),
                    UiSize.Fixed(SubmenuRowW, FightSubmenuLayout.ViewportHeight),
                    content)
                .Clipping();
            SubmenuViewport = viewport;

            // The bar. Track and thumb both start inactive: a list that fits
            // has no bar at all, and the controller is what knows the count.
            float barX = containerW * 0.5f - FightSubmenuLayout.ContainerPad
                         - FightSubmenuLayout.ScrollbarWidth * 0.5f;

            var track = Ui.Solid("SubmenuScrollTrack", FightHudPalette.ScrollTrack,
                    new UiVec(FightSubmenuLayout.ScrollbarWidth, FightSubmenuLayout.ViewportHeight),
                    Place.At(barX, viewportY))
                .AsDecor()
                .Inactive();
            SubmenuScrollTrack = track;

            var thumb = Ui.Solid("SubmenuScrollThumb", FightHudPalette.ScrollThumb,
                    new UiVec(FightSubmenuLayout.ScrollbarWidth, FightSubmenuLayout.ThumbMinHeight),
                    Place.At(barX, viewportY))
                .AsDecor()
                .Inactive();
            SubmenuScrollThumb = thumb;

            // ---- BACK, as the last row ------------------------------------------
            //
            // Same width, same x and the same row height as a skill, sitting in
            // the container's bottom padding OUTSIDE the scrolling viewport --
            // so it never scrolls away, which is the one behaviour it does not
            // share with the entries above it.
            //
            // The letters are red. It is the only row in the list that leaves
            // rather than commits, and colour is how that reads at a glance
            // without a second line of text saying so.
            var backKey = Ui.Label("SubmenuBackKey", UiStrings.HotkeyEscape, new UiVec(48f, 20f), 12,
                FightHudPalette.TextDisabled,
                Place.At(-SubmenuRowW * 0.5f + 6f, 0f, new UiVec(0f, 0.5f)));
            var backText = Ui.Label("SubmenuBackText", UiStrings.Back,
                new UiVec(SubmenuRowW - 100f, 24f), 18,
                FightHudPalette.BackRowText, Place.At(18f, 0f));

            // SILVER, not Violet: BACK leaves the branch rather than choosing
            // from it, and Silver is the kit's neutral/utility theme
            // elsewhere (Options/RunStats/Exits panes). The dead
            // .Coloured(RowQuiet)/.Hovers() pair is gone -- ThemedButtonState
            // now owns both the idle tint and the hover feedback.
            var back = Ui.Button("SubmenuBack", UiString.Runtime,
                    new UiVec(SubmenuRowW, FightSubmenuLayout.BackRowHeight), 1,
                    Place.At(SubmenuX - containerX,
                             FightSubmenuLayout.BackRowY - FightSubmenuLayout.ContainerCentreY))
                .ThemedPlate(ButtonTheme.Silver);
            back.Children.Add(backKey);
            back.Children.Add(backText);
            back.LayerCaptionWithVisuals(backKey, backText);
            SubmenuBackButton = back;

            // ---- the Violet 3:4 themed frame, wrapping the inner box above --------
            //
            // containerW x FightSubmenuLayout.ContainerHeight (316x500, 0.632)
            // missed the kit's measured 3:4 aspect (0.588) by 7.5% -- past
            // Ui.Container's 5% band. FrameWidth/FrameHeight (363.22x617.72)
            // are what the SAME inner box looks like widened to the kit's own
            // 6.5%-a-side inset and then completed to 0.588 exactly; see
            // FightSubmenuLayout.FrameHeight's own comment for why nothing
            // below RowsBottom had to move for it. The flat Solid + Ui.Rim
            // this replaces is gone -- Ui.Container draws its own border art.
            var frameNode = Ui.Container("SubmenuContainer", ButtonTheme.Violet, ContainerRatio.ThreeByFour,
                Place.At(containerX, FightSubmenuLayout.FrameCentreY),
                new UiVec(FightSubmenuLayout.FrameWidth, FightSubmenuLayout.FrameHeight));

            Ui.ContainerContent(frameNode, ContainerRatio.ThreeByFour, "SubmenuFrameContent",
                viewport, track, thumb, back);

            SubmenuContainer = frameNode;

            yield return frameNode;
        }

        // ---- column C: the detail panel ----------------------------------------------

        // BOTTOM-RIGHT, at the same 40px margin PlateBlockRight and PartyPlate
        // already use against this screen's edges (see their own comments) --
        // no longer level with the skill list. It used to sit centred against
        // the submenu at half the screen's height, with most of that height
        // empty below a short description; moving it out of the middle of the
        // battlefield and shrinking it is the actual request, not a like-for-
        // like reposition.
        private const float DetailW = 340f;
        private const float DetailRightMargin = 40f;
        private const float DetailBottomMargin = 40f;
        private static float DetailX => (960f - DetailRightMargin) - DetailW * 0.5f;

        // HALF the old height, on request -- the old box was tuned for four
        // stat rows and a lot of empty middle; this one carries five (SCALES
        // joined COST/POWER/TARGET/EFFECT) in half the space, so the padding
        // below is retuned alongside the height rather than left to overflow
        // it.
        //
        // BIGGER THAN THE CONTAINER'S HALF BY ITS OWN FRAME'S BLEED, same
        // reason as before: panel_violet is a 9-slice whose art does not reach
        // its own edges (3.32% top, 3.12% bottom, measured off the file), so
        // the RECT has to be bigger than the drawn frame by that margin for
        // the frame itself to land on the intended box.
        private const float PanelBleedY = 0.0332f + 0.0312f;
        private static float DetailH => (FightSubmenuLayout.ContainerHeight * 0.5f) / (1f - PanelBleedY);
        private static float DetailY => -(540f - DetailBottomMargin) + DetailH * 0.5f;

        // Where the drawn frame actually is, which is what the contents have to
        // stay inside. Everything below measures from these rather than from the
        // rect, because the rect's corners are empty pixels.
        private static float DetailFrameTop => DetailH * (0.5f - 0.0332f);
        private static float DetailFrameBottom => -DetailH * (0.5f - 0.0312f);

        // A PERSISTENT third column rather than a hover tooltip: arrowing
        // through options lets the player compare without re-hovering, and a
        // tooltip that vanishes the moment the cursor moves cannot be compared
        // against anything.
        private UiNode BuildDetailColumn()
        {
            float left = -DetailW * 0.5f + 22f;
            float top = DetailFrameTop - 10f;

            // THE STATS ARE A FOOTER, MEASURED FROM THE BOTTOM, same as
            // before -- retuned smaller and tighter for a box half the height
            // carrying a fifth row (SCALES) the old one didn't.
            // 36, not 16: a live capture showed the fifth row (SCALES) sitting
            // ON the panel's own ornate bottom border, half-hidden behind its
            // decoration. DetailFrameBottom already backs off the sprite's
            // measured transparent bleed, but the DRAWN border line itself
            // sits further inward than that bleed alone accounts for -- this
            // margin is empirical, from looking at the actual render, not
            // derived from the sprite measurement above it.
            float statBottom = DetailFrameBottom + 36f;
            float statPitch = 18f;
            float dividerY = statBottom + (FightHudSpec.DetailStatRows - 1) * statPitch + 12f;

            var name = Ui.Label("DetailName", UiString.Runtime, new UiVec(296f, 20f), 17,
                FightHudPalette.TextPrimary, Place.At(left, top - 10f, new UiVec(0f, 0.5f)));
            var kind = Ui.Label("DetailKind", UiStrings.DetailKindSkill, new UiVec(296f, 14f), 11,
                FightHudPalette.GoldLight, Place.At(left, top - 28f, new UiVec(0f, 0.5f)));

            // TOP-ALIGNED IN A TALL BOX. Centred, a one-line description sat in
            // the middle of the space and a three-line one started higher --
            // the block moved every time the text wrapped, which is exactly
            // what a reader uses the first line's position to track.
            float bodyTop = top - 38f;
            float bodyHeight = bodyTop - (dividerY + 8f);
            var body = Ui.Label("DetailBody", UiString.Runtime, new UiVec(296f, bodyHeight), 13,
                    FightHudPalette.TextSecondary,
                    Place.At(left, bodyTop - bodyHeight * 0.5f, new UiVec(0f, 0.5f)))
                .TextAligned(UiTextAlign.TopLeft);
            var divider = Ui.Solid("DetailDivider", FightHudPalette.Hairline, new UiVec(296f, 1f),
                Place.At(0f, dividerY));

            DetailName = name;
            DetailKind = kind;
            DetailBody = body;

            var keys = new[]
            {
                UiStrings.DetailStatCost, UiStrings.DetailStatPower,
                UiStrings.DetailStatTarget, UiStrings.DetailStatEffect,
                UiStrings.DetailStatScaling,
            };

            // keys[1] -- see the DetailDamageType comment in the loop below.
            const int PowerStatRowIndex = 1;

            var children = new List<UiNode> { name, kind, body, divider };
            for (int i = 0; i < FightHudSpec.DetailStatRows; i++)
            {
                float y = statBottom + (FightHudSpec.DetailStatRows - 1 - i) * statPitch;

                // THE POWER ROW GIVES UP WIDTH ON BOTH SIDES, for the element
                // tag that rides it. Five stat rows already fill DetailStatRows
                // (FightMenuStateTests pins Stats at exactly five by index), so
                // there is no sixth row to give the skill's DamageType its own
                // line -- it has to fit on an existing one, and POWER is the
                // one line a damaging skill's element actually describes.
                // "POWER" is the shortest of the five key words, so it is the
                // row whose key box can afford to lose the most.
                bool isPowerRow = i == PowerStatRowIndex;
                float keyW = isPowerRow ? 60f : 170f;
                float valueW = isPowerRow ? 80f : 120f;

                var key = Ui.Label($"DetailStatKey{i}", keys[i], new UiVec(keyW, 16f), 12,
                    FightHudPalette.TextMuted, Place.At(left, y, new UiVec(0f, 0.5f)));

                // 120 wide, not v1's 170: at 170 this box began at x -22 and the
                // key's box reaches 22.
                var value = Ui.Label($"DetailStatValue{i}", UiString.Runtime, new UiVec(valueW, 16f), 13,
                    FightHudPalette.GoldLight, Place.At(DetailW * 0.5f - 22f, y, new UiVec(1f, 0.5f)));

                DetailStatKeys.Add(key);
                DetailStatValues.Add(value);
                children.Add(key);
                children.Add(value);

                if (isPowerRow)
                {
                    // Spans the freed middle -- from just right of the
                    // narrowed key box to just left of the narrowed value
                    // box, so it can never collide with either at any of the
                    // four audited aspects. Empty content for a non-damaging
                    // skill (FightController.RefreshDetail hides it), which
                    // is why it takes no key label of its own: a key beside
                    // an empty value reads as a bug, not as "no element".
                    var damageType = Ui.Label("DetailDamageType", UiString.Runtime,
                            new UiVec(148f, 16f), 11, FightHudPalette.TextMuted,
                            Place.At(left + keyW + 4f, y, new UiVec(0f, 0.5f)))
                        .Styled(TypographyRole.TacticalData);
                    DetailDamageType = damageType;
                    children.Add(damageType);
                }
            }

            var column = Ui.Sprite("DetailColumn", PanelViolet,
                Place.At(DetailX, DetailY),
                UiSize.Fixed(DetailW, DetailH));
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
        private UiNode BuildIntentTooltip()
        {
            var label = Ui.Label("IntentTooltipText", UiString.Runtime, new UiVec(400f, 120f), 17,
                FightHudPalette.GoldText, Place.At(0f, 0f));
            IntentTooltipText = label;

            var panel = Ui.Tooltip("IntentTooltip", PanelViolet, null, Place.At(-330f, 250f),
                UiSize.Fixed(420f, 140f), label);
            IntentTooltip = panel;
            return panel;
        }

        // The party buff row's own tooltip -- same one-runtime-label shape as
        // IntentTooltip and for the same reason, just anchored near the party
        // plate instead of floating over the stage, since that is where the
        // icons it explains actually sit.
        private UiNode BuildPartyBuffTooltip()
        {
            var label = Ui.Label("PartyBuffTooltipText", UiString.Runtime, new UiVec(360f, 100f), 15,
                FightHudPalette.GoldText, Place.At(0f, 0f));
            PartyBuffTooltipText = label;

            var panel = Ui.Tooltip("PartyBuffTooltip", PanelViolet, null, Place.At(-540f, -250f),
                UiSize.Fixed(380f, 120f), label);
            PartyBuffTooltip = panel;
            return panel;
        }

        // ---- the target prompt ---------------------------------------------------------

        // Visible only at target depth. The BAR takes no clicks -- it hangs over
        // the stage the player is being asked to point at, and a banner that
        // swallowed a click on the monster behind it would be worse than no
        // banner. AsDecor states that for the whole subtree rather than leaving
        // it to be remembered.
        //
        // THE CANCEL BUTTON IS THE ONE EXCEPTION, and it exists because the
        // skill list folds once something is picked. BACK lived in that list, so
        // folding it left targeting with no way out at all: Escape belongs to
        // the system menu here (see FightController.Input), and the verbs behind
        // the prompt do not reopen a branch mid-target. A player who changed
        // their mind was stuck choosing a victim.
        //
        // Added AFTER AsDecor, because AsDecor walks the subtree at the moment
        // it is called -- a child appended first would have had its raycast
        // cleared with the rest and been a button that could not be pressed.
        private UiNode BuildTargetPrompt()
        {
            var diamond = Ui.Solid("TargetPromptDiamond", FightHudPalette.TargetAmber, new UiVec(12f, 12f),
                Place.At(-206f, 0f)).Rotated(45f);
            var label = Ui.Label("TargetPromptLabel", UiStrings.TargetPrompt, new UiVec(330f, 24f), 15,
                FightHudPalette.GoldText, Place.At(-182f, 0f, new UiVec(0f, 0.5f)))
                .TextAligned(UiTextAlign.Left);
            TargetPromptLabel = label;

            var prompt = Ui.Sprite("TargetPrompt", BannerFrame, Place.At(0f, -150f), UiSize.Fixed(480f, 48f));
            prompt.Children.Add(diamond);
            prompt.Children.Add(label);
            prompt.Inactive();
            prompt.Opening();
            prompt.AsDecor();

            // SILVER, matching Cancel/Back everywhere else - .Hovers() is
            // dropped rather than left dead: ThemedButtonState drives its own
            // hover/press feedback on Glow/Plate, and EmitButton's themed
            // branch never reaches the HoverScale/ButtonPressAnimator code
            // that a plain button's .Hovers() call would otherwise wire.
            var cancel = Ui.Button("TargetCancelButton", UiStrings.TargetCancel,
                    new UiVec(88f, 30f), 12, Place.At(190f, 0f))
                .Themed(ButtonTheme.Silver);
            TargetCancelButton = cancel;
            prompt.Children.Add(cancel);

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
            // ONE PER STAGE SLOT, not one full stop.
            //
            // A single member was right while every spell hit one thing. An
            // all-enemies cast resolves against every living enemy and animated
            // on whichever the beat named first -- three rats took the damage,
            // one took the spell. Three is the most that can ever be standing
            // on either side (FightHudSpec.StageSlotsPerSide), which covers a
            // full sweep of enemies and a party-wide effect alike.
            var pool = Ui.Pool("SpellVfx", FightHudSpec.StageSlotsPerSide, i =>
            {
                var image = Ui.Sprite($"SpellVfx{i}", null, new UiVec(380f, 380f), Place.At(0f, 0f)).Inactive();

                // THE INCOMING FRAME, DISSOLVING IN OVER THE OUTGOING ONE.
                //
                // A sheet is six to fourteen drawings and the effect is over in
                // three quarters of a second, so at any honest frame rate the
                // player is watching a slideshow -- each drawing sits still for
                // 50ms and is replaced. That reads as steps, and no amount of
                // speeding it up fixes it: faster steps are still steps, and
                // past a point the whole spell is gone before it registers.
                //
                // A SECOND IMAGE STACKED ON THE FIRST is the cheap half of
                // interpolation. The outgoing frame holds at full strength and
                // the incoming one fades up over it, so there is no dip in the
                // middle the way a symmetrical cross-fade has -- see
                // SpellVfxPlayer for the timing.
                //
                // A CHILD rather than a sibling, so it inherits the position,
                // the size and the mirror for free. Those three are set per
                // cast, and a sibling would need all of them kept in step by
                // hand -- which is the class of bug that put an edge's lit core
                // at twice its own offset.
                var next = Ui.Sprite($"SpellVfx{i}Next", null, Place.Stretch(), UiSize.Fill)
                    .AsDecor();
                image.Children.Add(next);

                SpellVfx.Add(image);
                SpellVfxNext.Add(next);
                return image;
            }).AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            SpellVfxPool = pool;
            return pool;
        }

        // THE SHARED GROUND LAYER: one fault, behind everyone standing on it.
        //
        // A SEPARATE POOL FROM THE ONE ABOVE, and the two differences are the
        // whole argument. The per-target pool holds one member per stage slot
        // because an all-enemies cast draws one effect per enemy; this holds
        // exactly ONE because a formation-wide fault is one drawing however
        // many enemies are standing on it, and a second copy of it stacked on
        // the first would read as a rendering fault rather than as two casts.
        // It also sits at a different DEPTH -- behind the racks rather than
        // over them (see the call site) -- and depth in uGUI is sibling order,
        // which a pool member cannot have two of.
        //
        // A POOL OF ONE rather than a bare sprite because that is what it is:
        // a node whose position and SIZE are computed at runtime from the slots
        // the living enemies actually occupy, which is the case Ui.Pool exists
        // for and the case UiAudit cannot solve statically.
        private UiNode BuildSpellGroundVfx()
        {
            var pool = Ui.Pool("SpellGroundVfx", 1, i =>
            {
                // NO AUTHORED SIZE WORTH THE NAME. The per-target pool's 380
                // square is a starting point a spell then overrides; this one
                // is ALWAYS overridden, because the fault spans from the
                // leftmost living enemy to the rightmost and neither is known
                // until a cast. The value here only has to be non-zero, which
                // is UiAudit's rule about zero-sized graphics.
                var image = Ui.Sprite($"SpellGroundVfx{i}", null, new UiVec(380f, 380f), Place.At(0f, 0f))
                    .Inactive();

                // The same dissolve layer the per-target pool carries, and for
                // the same reason -- see BuildSpellVfx. A child rather than a
                // sibling so it inherits position and size for free, which
                // matters more here than there: this layer's size changes on
                // every cast.
                var next = Ui.Sprite($"SpellGroundVfx{i}Next", null, Place.Stretch(), UiSize.Fill)
                    .AsDecor();
                image.Children.Add(next);

                SpellGroundVfx = image;
                SpellGroundVfxNext = next;
                return image;
            }).AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            SpellGroundVfxPool = pool;
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
