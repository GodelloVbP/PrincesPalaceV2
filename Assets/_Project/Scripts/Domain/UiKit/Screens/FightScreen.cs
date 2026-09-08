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

        // The soft bloom UNDER the contact ring (a child of the shadow --
        // see BuildStage) -- C2: StageDeathFade fades this alongside the
        // sprite and the ring, so a corpse's glow does not sit at full
        // brightness under a body that has otherwise faded away.
        public List<NodeRef> EnemyFootGlows = new List<NodeRef>();

        // The status row hanging under each enemy figure -- see BuildEnemy-
        // StatusRows. Flattened SLOT-MAJOR (slot 0's five badges, then slot
        // 1's, then slot 2's), matching every other per-slot list on this
        // screen, so FightController can index it the same way it indexes
        // EnemyNameplates or EnemyIntentIcons. Index 4 of every five is the
        // overflow "+N" chip, not a fourth status -- see PLAN_STATUS_EFFECT_UI
        // section 6.
        public List<NodeRef> EnemyStatusBadges = new List<NodeRef>();

        // One backing strip per enemy row (not per badge -- see the plan's
        // "sludge" section): a single translucent plate behind all five
        // badges, AsDecor so it takes no clicks.
        public List<NodeRef> EnemyStatusStrips = new List<NodeRef>();

        public List<NodeRef> PartySlots = new List<NodeRef>();
        public List<NodeRef> PartySprites = new List<NodeRef>();
        public List<NodeRef> PartyHitFlashes = new List<NodeRef>();
        public List<NodeRef> PartyNameplates = new List<NodeRef>();
        public List<NodeRef> PartyFootShadows = new List<NodeRef>();
        public List<NodeRef> PartyFootGlows = new List<NodeRef>();

        public NodeRef BarkPanel;
        public NodeRef BarkPortrait;
        public NodeRef BarkLabel;

        public NodeRef LowHpVignette;

        public NodeRef EnemiesHint;
        public List<NodeRef> EnemyPlates = new List<NodeRef>();

        // The Crimson TwoByOne container frame's own Art sprite (owner's
        // HQ-kit instruction, 2026-09-07), added alongside EnemyPlates
        // rather than in place of it: EnemyPlates is now NoChrome (a
        // transparent, always-alpha-0 click target, see BuildEnemyPlates),
        // so FightController.Hud's RefreshEnemyPlates tints THIS Image for
        // the elite/boss dress and the out-of-reach dim it used to apply to
        // enemyPlates[i].targetGraphic -- tinting the chromeless button's own
        // Image would tint something the player can never see.
        public List<NodeRef> EnemyPlateFrames = new List<NodeRef>();
        public List<NodeRef> EnemyPlateIcons = new List<NodeRef>();
        public List<NodeRef> EnemyPlateNames = new List<NodeRef>();
        public List<NodeRef> EnemyPlateHps = new List<NodeRef>();
        public List<NodeRef> EnemyPlateHpFills = new List<NodeRef>();
        public List<NodeRef> EnemyPlateTags = new List<NodeRef>();
        public List<NodeRef> EnemyPlateReticles = new List<NodeRef>();
        public List<NodeRef> EnemyPlateBreakTracks = new List<NodeRef>();
        public List<NodeRef> EnemyPlateBreakFills = new List<NodeRef>();

        public NodeRef PartyPlate;

        // The plate's frame sprite (Container's "Art" child) -- see
        // BuildPartyPlate's own note. C1: swapped at runtime by
        // RefreshPartyPlate off the acting character's PlateTheme.
        public NodeRef PartyPlateArt;
        public NodeRef PartyPortrait;
        public NodeRef PartyName;
        // PartyClass ("LV1 UTILITY") is GONE, B2 (balance-bot pass) -- see
        // BuildPartyPlate's own note. Removed here only: FightController.
        // Hud.cs:568 (`partyClass.Set(...)`) and the `partyClass`
        // [SerializeField] on FightController still exist on the main tree
        // and are Phase C's job, not this worktree's -- Core is off-limits
        // here (CLAUDE.md/docs/CODE_STANDARDS.md layering).
        public NodeRef PartyHpFill;
        public NodeRef PartyHpValue;
        public NodeRef PartyMpFill;
        public NodeRef PartyMpValue;
        public List<NodeRef> PartyBuffIcons = new List<NodeRef>();

        // The one tooltip every status badge on every surface shares --
        // enemy row, party plate and roster row alike -- repositioned per
        // hover through TooltipPlacement.Beside rather than each surface
        // keeping its own copy. Replaces PartyBuffTooltip/PartyBuffTooltipText
        // outright (S4's review): those were left standing, wired but
        // permanently hidden, through the package that built this shared
        // tooltip, and nothing ever needed them again.
        public NodeRef StatusTooltip;
        public NodeRef StatusTooltipText;
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

        // One status row per roster mini-plate, flattened ROSTER-MAJOR (r0's
        // five badges, then r1's), same convention as EnemyStatusBadges.
        public List<NodeRef> RosterStatusBadges = new List<NodeRef>();

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

        // DETACHED DROPS, in the same band as the sprite layers and declared
        // immediately after them -- so a spray draws over the crown it came out
        // of and under the damage number. A node, not a third semantic
        // category.
        public NodeRef SpellParticlePool;
        public List<NodeRef> SpellParticles = new List<NodeRef>();

        // The shared ground layer -- one per formation, however many enemies
        // are hit, and behind all of them. See BuildSpellGroundVfx.
        public NodeRef SpellGroundVfxPool;
        public List<NodeRef> SpellGroundVfx = new List<NodeRef>();
        public List<NodeRef> SpellGroundVfxNext = new List<NodeRef>();
        public NodeRef DamagePopupPool;
        public List<NodeRef> DamagePopups = new List<NodeRef>();
        public List<NodeRef> DamagePopupLabels = new List<NodeRef>();

        // ---------------------------------------------------------------------

        // partyRosterCardCount threads straight through to SystemMenuScreen.
        // Build -- see HubScreen.Build's identical parameter for why it
        // defaults to 3 and who is meant to override it.
        public static FightScreen Build(int partyRosterCardCount = 3)
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
                s.PartyNameplates, s.PartyFootShadows, s.PartyFootGlows);
            var enemyStage = s.BuildStage("Enemy", mirrored: false,
                FightStageAnchors.EnemyShadowColor, s.EnemySlots, s.EnemySprites, s.EnemyHitFlashes,
                s.EnemyNameplates, s.EnemyFootShadows, s.EnemyFootGlows, s.EnemyIntentIcons, s.EnemyHitAreas);
            s.PartyStage = partyStage;
            s.EnemyStage = enemyStage;
            hud.Add(partyStage);
            hud.Add(enemyStage);

            // A CHILD OF THE STAGE PANEL, appended after BuildStage returns
            // it -- never of the scaled slot node inside it. The slot carries
            // WithScale (see BuildStage), so a row nested there would shrink
            // to 56% size in the back slot; the stage panel itself carries no
            // scale, so a row parented directly to it draws at one size in
            // every slot. See PLAN_STATUS_EFFECT_UI section 1.
            enemyStage.Children.AddRange(s.BuildEnemyStatusRows());
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
            hud.Add(s.BuildStatusTooltip());
            hud.Add(s.BuildSpellVfx());
            hud.Add(s.BuildSpellParticles());
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
            var systemMenu = SystemMenuScreen.Build(partyRosterCardCount);
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
                                  List<NodeRef> nameplates, List<NodeRef> shadows, List<NodeRef> glows,
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
                glows.Add(default);
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
                glows[slot] = glow;

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

        // ---- status badges (enemy row, party plate, roster row) --------------
        //
        // PLAN_STATUS_EFFECT_UI.md sections 1, 3 and 7. Three surfaces, one
        // anatomy: a hoverable button (so Core/HoverIndex.cs can attach at
        // runtime exactly as WirePartyBuffIcons already does) carrying three
        // children -- Glyph, Code, Counter -- named so Package C can find
        // them without this screen handing out a second set of NodeRefs for
        // parts nobody outside the badge needs addressed individually.
        //
        // FRAME SHAPE, AND WHAT THIS PACKAGE DID NOT BUILD. Section 3 wants a
        // smooth rounded outline for a benefit and clipped corners with a top
        // notch for a detriment, drawn as UI geometry. This worktree cannot
        // boot the Editor to bake a new proc: shape (ProceduralSpriteBaker is
        // a menu item), and no shipped sprite already draws a clipped-corner
        // badge frame, so no THIRD child was added here for it. Glyph is the
        // one visible layer in Phase 1 (no status art has landed yet -- see
        // the plan's section 9) and stays that way once art lands, so it is
        // also where the frame belongs: Package C can tint Glyph by holder
        // polarity (FightHudPalette.IntentHeal / HpBright, already used
        // elsewhere on this controller) and, for the geometry half, rotate
        // Glyph's own RectTransform -- an ordinary runtime call, not a DSL
        // feature -- rather than the whole badge, which would tilt Code and
        // Counter with it. Said here rather than guessed at silently: this is
        // the smallest thing that survives UiAudit with the tree as declared,
        // not a claim that it is the final visual.
        private UiNode BuildStatusBadge(string name, float x, float y, float size,
            int codeFontSize, int counterFontSize)
        {
            var glyph = Ui.Sprite("Glyph", null, new UiVec(size * 0.72f, size * 0.72f), Place.At(0f, 0f));

            var code = Ui.Label("Code", UiString.Runtime, new UiVec(size - 4f, size * 0.5f), codeFontSize,
                FightHudPalette.TextPrimary, Place.At(0f, 0f));

            // Bottom-right corner, pulled 1px inward so its own footprint
            // stays inside the 1px-rounded badge box rather than riding the
            // exact edge.
            var counterPin = Place.Pin(new UiVec(1f, 0f), new UiVec(1f, 0f), new UiVec(-1f, 1f));

            // A SMALL DARK PATCH BEHIND THE DIGIT, same corner pin as Counter
            // itself -- section 2's last-tick emphasis (PLAN_STATUS_EFFECT_UI.md
            // section 3/9 Phase 3) needs somewhere to grow when a counter
            // reaches 1, and a bare digit floating over the glyph's own art
            // has no floor under it to grow at all. AsDecor: it must never
            // steal the badge Button's own click/hover, which is how
            // Core/HoverIndex.cs finds this badge in the first place.
            var counterPatch = Ui.Solid("CounterPatch", "#0A0611CC", new UiVec(size * 0.34f, size * 0.34f),
                    counterPin)
                .AsDecor();

            var counter = Ui.Label("Counter", UiString.Runtime, new UiVec(size * 0.5f, size * 0.4f),
                counterFontSize, FightHudPalette.TextPrimary, counterPin);

            // ONE BADGE, four layers -- same shape as the initiative badge's
            // ring/portrait/initial stack above: exempt from each other,
            // still checked against every other sibling.
            Ui.Layered(glyph, code, counterPatch, counter);

            var badge = Ui.Button(name, UiString.Runtime, new UiVec(size, size), 1, Place.At(x, y))
                .NoChrome()
                .Inactive();
            badge.Children.Add(glyph);
            badge.Children.Add(code);
            badge.Children.Add(counterPatch);
            badge.Children.Add(counter);
            return badge;
        }

        // 36/40, per section 1's measured table. 4 statuses plus the "+N"
        // overflow chip (section 6) is 5 nodes per row.
        private const float EnemyStatusBadgeSize = 36f;
        private const float EnemyStatusPitch = 40f;
        private const int EnemyStatusBadgesPerRow = 5;

        // offset.Y - 60, unscaled -- section 1's number. WORTH RESTATING
        // PRECISELY, because the plan's own "23.5px clearance at the near
        // slot" is a CENTRE-to-reach distance (Drop 60 minus the nameplate's
        // scaled reach 36.5), not edge-to-edge. Accounting for the row's own
        // half-height (18, badge size 36) the real edge clearance is 60 - 18
        // - 36.48 = 5.5px at the near slot (more at the far two, since a
        // smaller SlotScale shrinks the nameplate's reach faster than it
        // shrinks anything of this row's, which is not scaled at all) --
        // thin, but positive; see EnemyStatusRowsClearTheirOwnNameplateAnd-
        // StayAboveTheCanvasFloor in FightScreenTests, which pins it against
        // the same FightStageAnchors.SlotScale math rather than the solver's
        // own nameplate Rect (that Rect is UNSCALED, because UiSolver does
        // not cascade an ancestor's WithScale into a descendant's own
        // layout numbers -- Unity's real localScale doesn't either, it is a
        // render-time transform, not a second layout pass -- so comparing
        // this row against it directly overstates the collision by exactly
        // the scale factor and was caught failing before this comment
        // existed).
        private const float EnemyStatusRowDrop = 60f;

        // NO VERTICAL PADDING. The margin above is already down to 5.5px at
        // the near slot; a taller strip than the badges it backs eats that
        // margin directly; and it also closed the 10.5px gap BETWEEN rows
        // to nothing before this comment did (row Y drop compounds with a
        // taller strip at both ends). Horizontal padding costs nothing here
        // -- the enemy plates and verb column are nowhere near this x range
        // at this y (section 1) -- so only that axis gets any.
        private const float EnemyStatusStripPadX = 12f;
        private const float EnemyStatusStripPadY = 0f;

        // ONLY THE FAR SLOT ACTUALLY OVERFLOWS StageSize.X (565 + 98 = 663
        // against a 600px half-width), but the reason is declared uniformly
        // across all three rows rather than conditionally on the far one --
        // the slot node right above this method makes the same call for the
        // same reason: a future depth-curve change should not have to
        // remember which row silently needed this.
        private const string EnemyStatusOverflowReason =
            "the far slot's status row and strip run past FightStageAnchors.StageSize.X the same way the " +
            "slot node above already does - StageSize is a coordinate reference for the depth curve, not a clip region";

        // A CHILD OF THE STAGE PANEL. See the AllowOverlap comment on the
        // slot node above for why this cannot be a child of the slot instead
        // -- WithScale would draw it at 56% size in the back slot.
        private IEnumerable<UiNode> BuildEnemyStatusRows()
        {
            int count = FightHudSpec.StageSlotsPerSide;
            float rowWidth = (EnemyStatusBadgesPerRow - 1) * EnemyStatusPitch + EnemyStatusBadgeSize;

            for (int slot = 0; slot < count; slot++)
            {
                var offset = FightStageAnchors.SlotOffset(slot, count, mirrored: false);
                float y = offset.Y - EnemyStatusRowDrop;

                // ONE STRIP PER ROW, not one per badge -- section 7's sludge
                // budget. Reuses the same painted panel the tooltips on this
                // screen already load, tinted dark rather than drawn from a
                // fresh asset: a real (rounded) panel, not a flat Solid rect.
                var strip = Ui.Sprite($"EnemyStatusStrip{slot}", PanelViolet, Place.At(offset.X, y),
                        UiSize.Fixed(rowWidth + EnemyStatusStripPadX * 2f,
                            EnemyStatusBadgeSize + EnemyStatusStripPadY * 2f))
                    .Coloured("#140A10CC")
                    .AsDecor()
                    .Inactive()
                    .AllowOverflow(EnemyStatusOverflowReason);
                EnemyStatusStrips.Add(strip);
                yield return strip;

                for (int i = 0; i < EnemyStatusBadgesPerRow; i++)
                {
                    float x = offset.X + (i - EnemyStatusBadgesPerRow / 2) * EnemyStatusPitch;
                    var badge = BuildStatusBadge($"EnemyStatusBadge{slot}_{i}", x, y, EnemyStatusBadgeSize, 11, 9)
                        .AllowOverflow(EnemyStatusOverflowReason);
                    EnemyStatusBadges.Add(badge);
                    yield return badge;
                }
            }
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

                // The hairline ring every other emphasis ring in the game wears
                // (talent orbs, reward-track pulse, dossier rift glow). It was
                // declared with a NULL key from the port onward, and since
                // nothing runtime-side ever assigns a ring sprite, ShowSprite
                // could never switch it on -- the acting combatant was never
                // ringed. The controller only decides WHICH slot shows it.
                var ring = Ui.Sprite($"InitiativeRing{i}", "proc:ring_hairline",
                        Place.Stretch(-pad, -pad, -pad, -pad), UiSize.Fill)
                    .Inactive()
                    .AllowOverflow("the emphasis ring frames the icon from OUTSIDE it, which is what makes the acting combatant pop");

                // Inactive until a combatant is actually in the slot. An Image
                // with no sprite renders as a WHITE QUAD, so six empty slots on
                // a fight that has not started drew six white squares across the
                // top-left corner -- caught by looking at the screenshot, which
                // no audit would have flagged because the box is the right size.
                //
                // PreserveAspect fits a portrait inside the square chip
                // instead of stretching it, and AsDecor keeps the chip from
                // eating a click meant for the stage behind it. Both are the
                // slot's shape, not something the round changes, so they
                // belong here where UiAudit can see them rather than being
                // re-set on every repaint from RefreshInitiative.
                var icon = Ui.Sprite($"InitiativeIcon{i}", null, Place.Stretch(), UiSize.Fill)
                    .Inactive()
                    .AsDecor();
                icon.PreserveAspect = true;

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
        //
        // GROWN FROM 200 TO 220 (owner's HQ-kit instruction, 2026-09-07): the
        // Crimson TwoByOne frame's own content inset eats 3.5% of PlateW off
        // each side, which at the old 200 left only 186px of usable width
        // where the icon/name/tag/hp row was already sharing 196 with zero
        // slack (see PlateContentHalfWidth and the row's own "2px gaps"
        // comment below) -- 220 gives back a content half-width of 102.3,
        // just over the original un-inset 100, so the row's own margins and
        // gaps need no further change.
        private const float PlateW = 220f;

        // GROWN FROM 64 TO 110 (owner's HQ-kit instruction, 2026-09-07):
        // EnemyPlate wears a Crimson TwoByOne container frame now instead of
        // the flat panel_crimson.png sprite, and this is PlateW's (220) own
        // exact TwoByOne match. PlatePitch below grows with it (76 to 122)
        // to keep the two rows from overlapping.
        //
        // RESOLVED 2026-09-08, by finally running the tool. Growing PlateH
        // drops the bottom row's lower edge from 284 to 215, and the tallest
        // actor's head at the near slot reaches 149 -- 66 units of daylight
        // against the 12 this layout is toleranced to, so the conversion
        // shipped inside its own band. It took a repair to get that answer:
        // tools/measure_stage.py had been reading PlateH as a literal and a
        // PlateX that no longer exists, and it assumed one column where the
        // stack has had two, so it exited on a parse failure rather than
        // measuring anything.
        private static readonly UiVec PlateSize = Ui.ContainerSizeForWidth(ContainerRatio.TwoByOne, PlateW);
        private static float PlateH => PlateSize.Y;

        // The USABLE half-width inside the Crimson frame's own content
        // inset (3.5% of PlateW off each side for TwoByOne), not PlateW's
        // own half-width -- the icon/name/hp row was authored against the
        // bare plate rect with a 4px margin, and at the original PlateW=200
        // the container's inset ate more than that (7px), putting the icon
        // 3px past the frame's own left edge (caught by UiAudit's
        // ChildContainment at build) and closing the name/tag gap into an
        // overlap besides -- PlateW grew to 220 to buy that room back (see
        // its own comment), and every "left"/"rowRight" style margin below
        // is measured off this content half-width instead, which restores
        // the original 4px margins against the box content
        // actually has to live in.
        private static float PlateContentHalfWidth =>
            PlateSize.X * (0.5f - Ui.ContainerContentInset(ContainerRatio.TwoByOne).Left);
        private const float PlateGap = 16f;
        private const int PlateColumns = 2;

        // The block's RIGHT edge is unchanged, which is the anchor that matters
        // -- it sits against the same margin the heading and the hint do.
        private const float PlateBlockRight = 920f;
        private const float PlateFirstX =
            PlateBlockRight - (PlateColumns * PlateW + (PlateColumns - 1) * PlateGap) + PlateW * 0.5f;

        private const float PlatePitchX = PlateW + PlateGap;
        private static float PlatePitch => PlateH + 12f;

        // The icon that tells two of the same monster apart at a glance, before
        // the name is read. The actor's own idle sprite, fitted -- no new art,
        // and it cannot disagree with the figure on the stage because it IS the
        // figure on the stage.
        //
        // 34 RATHER THAN 40: BRK folding into the name row (see BuildEnemy-
        // Plates' tag comment) needed width the icon column had to give back.
        // 6px off the icon plus 4px off the right margin (PlateW*0.5f minus
        // 4f rather than 8f, below) buys the 12px the name/tag/HP row needed
        // without touching PlateW, PlatePitch or anything a test pins.
        private const float PlateIconSize = 34f;
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

        private const float EnemiesHeadingHeight = 24f;

        // 6px clear of the plate stack's own top edge -- DERIVED from PlateH
        // now rather than the restated literal "+50" this replaces (392+50
        // was only ever "PlateFirstY + PlateH*0.5 + 6 + 12" with PlateH=64
        // baked in by hand). Growing PlateH (owner's HQ-kit instruction,
        // 2026-09-07 -- see PlateH's own comment) moved the plate stack's
        // top edge up into where the restated "+50" still put the heading,
        // an overlap UiAudit caught at build; deriving it instead means the
        // next PlateH change cannot silently reopen the same collision.
        private const float EnemiesHeadingGap = 6f;

        private static float EnemiesHeadingY =>
            PlateFirstY + PlateH * 0.5f + EnemiesHeadingGap + EnemiesHeadingHeight * 0.5f;

        private IEnumerable<UiNode> BuildEnemiesHeading()
        {
            // 155 wide, not v1's 200. At 200 this box ran to x 720 while the
            // right-aligned hint's box starts at 680 -- a 40px crossing the
            // audit refuses. "E N E M I E S" at 12pt bold is well under 155.
            yield return Ui.Label("EnemiesHeading", UiStrings.EnemiesHeading,
                new UiVec(155f, EnemiesHeadingHeight), 12,
                FightHudPalette.TextMuted,
                Place.At(PlateFirstX - PlateW * 0.5f, EnemiesHeadingY, new UiVec(0f, 0.5f)));

            var hint = Ui.Label("EnemiesHint", UiStrings.StandingCount,
                new UiVec(240f, EnemiesHeadingHeight), 12,
                FightHudPalette.TextDisabled,
                Place.At(PlateBlockRight, EnemiesHeadingY, new UiVec(1f, 0.5f)));
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
                float left = -PlateContentHalfWidth;

                var icon = Ui.Sprite($"EnemyPlate{i}Icon", null,
                        new UiVec(PlateIconSize, PlateIconSize),
                        Place.At(left + 4f + PlateIconSize * 0.5f, 0f))
                    .Inactive()
                    .AsDecor();

                // Everything else starts to the right of the icon. 6px rather
                // than the old 8: see PlateIconSize's own comment for where
                // the reclaimed width goes.
                float textLeft = left + 6f + PlateIconSize;

                // The right edge of the row, matching hp's own Place.At
                // below -- named once so the tag can be pinned off it without
                // restating the margin. 4px rather than the old 8: same
                // reclaim as textLeft above.
                float rowRight = PlateContentHalfWidth - 4f;

                // TWO ROWS, NOT THREE. The status line that used to sit
                // between these retired to the stage rows under each figure
                // (see EnemyStatusLine's own header) and left a blank 12px
                // band where it had been. The name/HP row and the bar are
                // now centred as a block in the full 64px -- 11px of margin
                // above and below -- rather than sitting where the old
                // three-row split left them.
                //
                // TopY/BarY are load-bearing together: TopY - 13.5 (half the
                // HP box's 27-tall height) must equal BarY + 3.5 + 8, an 8px
                // gap, for the block to stay centred. Solve them together if
                // either box's height ever changes.
                const float topY = 7.5f;
                const float barY = -17.5f;

                // 72 WIDE, 11PT RATHER THAN THE OLD 86/13PT: BRK now lives
                // INSIDE this row (see the tag below), and the row has to
                // share its 100px of free width (textLeft..hpLeft) between
                // the name, the tag and two 2px gaps.
                //
                // NOT AUTOSIZED: this label has no Role, so ApplyTypography's
                // no-role branch sets a literal fontSize and returns without
                // touching enableAutoSizing. The old 86/13pt box had no slack
                // to give: cutting the box to 72 at 13pt wrapped "Stone Golem"
                // onto two lines (caught by capture, both empirically against
                // tools/screenshots/runtime/status_rows_rest.png -- Domain has
                // no font metric to derive this from, see UiTextFitAudit's own
                // header on why), so the font dropped to 11pt with it, which
                // bought the box back its headroom for that case.
                //
                // Still not enough for a long boss name -- "The Hollow Choir"
                // measures ~83px and "Ironback Beetle" ~79px at 11pt against
                // this 72px box, and FightHudModel.DisplayNames appends " 2"
                // onto a duplicate, widening it further. Truncated()
                // (C3's review) turns off word-wrap and switches to Ellipsis
                // overflow, the same fallback a themed button's caption
                // already gets, so a name that still doesn't fit clips to
                // "The Hollow Cho..." on ONE line instead of wrapping a
                // second line the 20px-tall box has no room to show.
                var name = Ui.Label($"EnemyPlate{i}Name", UiString.Runtime, new UiVec(72f, 20f), 11,
                    FightHudPalette.EnemyName, Place.At(textLeft, topY, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left)
                    .Truncated();

                // 27 tall for a 10pt line: the audit measures "9999/9999" at
                // 26 there, and a box that cannot hold it clips the descenders
                // off the number the plate exists for. Width and font size are
                // both audit-pinned (UiStrings.HealthValue is measured, unlike
                // the runtime name/tag either side of it), so neither moved --
                // only Y did, to the shared topY above, and the row's right
                // edge moved 4px in with it (rowRight, from the reclaimed
                // icon-column width -- see PlateIconSize's own comment).
                var hp = Ui.Label($"EnemyPlate{i}Hp", UiStrings.HealthValue, new UiVec(56f, 27f), 10,
                    FightHudPalette.EnemyHpText, Place.At(rowRight, topY, new UiVec(1f, 0.5f)))
                    .TextAligned(UiTextAlign.Right);

                var fill = Ui.Solid($"EnemyPlate{i}HpFill", FightHudPalette.HpBright, Place.Stretch(), UiSize.Fill);

                // BRK'S ONLY REMAINING HOME. Phase 3 (EnemyStatusLine's own
                // header) retired every other status to the stage rows and
                // left this node carrying nothing but the break tag, so it
                // moved OFF its own row and INTO the name row's right end,
                // between the name and the HP value -- the row the old
                // 12px-tall middle line existed to avoid crowding. 24 wide is
                // room enough for "BRK" at 8pt with margin either side; the
                // 2px gaps to the name box and the HP box on either side hold
                // at all four audited aspects because none of the three boxes
                // repositions between them. SetContent("") when the enemy is
                // not broken (EnemyStatusLine's normal case) renders nothing,
                // so the tag needs no separate active/inactive toggle.
                var tags = Ui.Label($"EnemyPlate{i}Tags", UiString.Runtime, new UiVec(24f, 14f), 8,
                    FightHudPalette.TextSecondary,
                    Place.At(rowRight - 56f - 2f, topY, new UiVec(1f, 0.5f)))
                    .TextAligned(UiTextAlign.Right);

                // Hugs the same two edges the name/HP row does (textLeft on
                // the left, a 2px margin inside rowRight on the right) rather
                // than keeping its own pre-reclaim width -- otherwise the bar
                // would stop 12px short of the row above it, which reads as
                // the bar shrinking rather than as the row it never belonged
                // to changing shape.
                float barLeft = textLeft;
                float barRight = rowRight - 2f;
                float barCentreX = (barLeft + barRight) * 0.5f;
                float barWidth = barRight - barLeft;

                var bar = Ui.Panel($"EnemyPlate{i}Bar",
                        Place.At(barCentreX, barY), UiSize.Fixed(barWidth, 7f), fill)
                    .Coloured(FightHudPalette.Track);

                // A 12px square rotated 45 degrees, pinned just outside the
                // plate's left edge. Visible only while targeting.
                var reticle = Ui.Solid($"EnemyPlate{i}Reticle", FightHudPalette.TargetAmber,
                        new UiVec(12f, 12f), Place.At(-PlateW * 0.5f - 9f, 0f))
                    .Rotated(45f)
                    .Inactive()
                    .AllowOverflow("the reticle is deliberately OUTSIDE the plate - a marker in the margin, not a badge on the card");

                // THE BREAK METER, hanging BELOW the plate rather than inside
                // it. The two rows above now sit centred with 11px of margin
                // on both edges (see topY/barY), so there IS free space inside
                // the plate again -- but the meter stays outside it anyway:
                // the 12px gap PlatePitch leaves before the next row down is
                // real, unused space, and a 6px bar fits it with margin
                // either side without reopening the vertical layout. Same
                // fill mechanism the HP bar already uses (SetFill), same
                // width and x-position, one row lower. Only an elite or boss
                // carries a BreakShield at all, so this stays hidden for
                // everything else.
                var breakFill = Ui.Solid($"EnemyPlate{i}BreakFill", FightHudPalette.TargetAmber,
                    Place.Stretch(), UiSize.Fill);
                var breakTrack = Ui.Panel($"EnemyPlate{i}BreakTrack",
                        Place.At(barCentreX, -PlateH * 0.5f - 6f), UiSize.Fixed(barWidth, 6f), breakFill)
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

                // CRIMSON, 2:1 KIT CONTAINER (owner's HQ-kit instruction,
                // 2026-09-07) as the frame, replacing panel_crimson.png --
                // the mirror of the party plate's own Container(Blue,
                // TwoByOne), and the same frame+chromeless-click-target shape
                // RelicDraftScreen's cards use. The frame carries every piece
                // of content as its ContainerContent; the button itself stays
                // the click/hover target, NoChrome so it paints nothing of
                // its own over the frame.
                var frame = Ui.Container($"EnemyPlate{i}Frame", ButtonTheme.Crimson, ContainerRatio.TwoByOne,
                    Place.At(0f, 0f), PlateSize);
                Ui.ContainerContent(frame, ContainerRatio.TwoByOne, $"EnemyPlate{i}FrameContent",
                    icon, name, hp, tags, bar, reticle, breakTrack);

                // 1.03 hover, no press pop: the plate carries four pieces of
                // text, so the press animator's 1.05/0.95 would swing them all
                // sideways under the cursor. v1 made the same call at the same
                // number.
                var plate = Ui.Button($"EnemyPlate{i}", UiString.Runtime, PlateSize, 1,
                        Place.At(PlateFirstX + i % PlateColumns * PlatePitchX,
                                 PlateFirstY - i / PlateColumns * PlatePitch))
                    .Hovers(1.03f)
                    .NoChrome();
                plate.Children.Add(frame);
                plate.Inactive();

                EnemyPlates.Add(plate);
                EnemyPlateFrames.Add(frame.Children.FirstOrDefault());
                return plate;
            });
        }

        // ---- the party plate ----------------------------------------------------

        // BLUE, 2:1 CONTAINER ART -- the character's own stat card, so the
        // informational/defensive theme (the same one HOLD BACK uses in the
        // verb column) rather than Crimson, which is EnemyPlate{i}'s own
        // theme now (see BuildEnemyPlates) for the same "this is the other
        // side" reason panel_crimson.png used to wear it for no reason at
        // all. FightHudPalette itself never tied this slot to a colour --
        // its hex tokens are HP/MP/text roles, not a per-plate theme -- so
        // there was nothing here to defer to. (FallbackPlateKey still names
        // panel_crimson.png, but only for the stage actor's own missing-art
        // fallback now -- see FightController.StageVisuals -- not for
        // either plate.)
        //
        // COZY, B2 (balance-bot pass): 452x228 was sized for a header row
        // (name + "LV1 UTILITY") that no longer exists -- PartyClass is gone
        // below, see its own note -- and left the HP/MP rows and the wool
        // meter floating in a card built for a fourth fact it no longer
        // states. 380 keeps the 2:1 aspect (380/2.0 = 190, exactly on it
        // since the 2026-09-07 kit repin put the art at a true 2.0; it was
        // 191.92 against the old measured 1.98); the height derives from it
        // rather than being pinned separately, same discipline the old
        // 452/1.98 used.
        // Public: FightScreenTests (a separate assembly, no InternalsVisibleTo
        // grant to it) reads these rather than restating the numbers.
        public const float PartyPlateWidth = 380f;
        public const float PartyPlateHeight = PartyPlateWidth / ContainerArt.ContainerAspect2x1; // 190, was 191.92
        private const float PartyPlateCentreX = -730f; // LEFT EDGE STAYS AT -920 -- see BuildPartyPlate's own note

        // VISIBLE-BOTTOM FLUSH NOW (B1), not the fixed -500 HUD margin it
        // used to be: -500 WAS the canvas's own 40px margin above the floor
        // (UiFrames.Reference.Y * -0.5 + 40), chosen with no reference to the
        // verb column at all, which is why the plate's own visible bottom
        // used to sit ~14px below the verb column's -- two rects that agreed
        // on nothing lined up by coincidence or not at all. It is now placed
        // exactly like FightSubmenuLayout's own frame (see
        // FightSubmenuLayout.VisibleBottomLine's comment for the halo this
        // corrects for): the CENTRE that puts the plate's own visible bottom
        // (rect bottom + height * its 2:1 art's own bottom VisiblePad) on
        // the same line the verb column's visible bottom sits on. Computed,
        // not const, because Ui.PlateVisiblePad/ContainerVisiblePad are
        // ordinary (non-const) static methods -- see PartyPlateWidth/Height's
        // own note for why the two consts above stay literal.
        private static float PartyPlateCentreY =>
            Ui.CentreYForVisibleBottom(FightSubmenuLayout.VisibleBottomLine, PartyPlateHeight,
                Ui.ContainerVisiblePad(ContainerRatio.TwoByOne).Bottom);

        // EVERY LOCAL NUMBER BELOW IS THE OLD 452-WIDE LAYOUT SCALED BY
        // PartyPlateWidth / 452f (0.8407) -- a uniform shrink, not a
        // redesign from scratch. That is deliberate: the old layout was
        // already audited clean (no overlaps, E1 text-fit passing), and a
        // uniform scale of a non-overlapping layout is STILL non-overlapping
        // -- every gap shrinks by the same factor a rect does, so nothing
        // that cleared its neighbour before can now cross into it. The one
        // thing that changes shape rather than just size is PartyClass,
        // which is gone outright (see its own note below) rather than
        // scaled down with everything else. Every number below is therefore
        // its old value times PartyPlateWidth / 452f (0.8407), pre-computed
        // rather than written as a live expression: the numbers are
        // PLACEMENT, the same reason every other rect on this screen is a
        // literal with a comment rather than a formula, and the E1/UiAudit
        // pins below check the RESULT, not that any particular arithmetic
        // produced it.
        private UiNode BuildPartyPlate()
        {
            // 54x54, not v1/A1's 64x64 -- 64 * 0.8407 = 53.8, which is the
            // "portrait ~56px" the brief asked for (rounded to the nearest
            // whole px the scale itself lands on, not a second, independently
            // chosen number that could disagree with everything scaled off
            // it).
            var portrait = Ui.Sprite("PartyPortrait", null, new UiVec(54f, 54f), Place.At(-143f, 45.4f)).Inactive();

            // A CHILD of the portrait, not a sibling of the plate -- nesting it
            // here means A1 never has to reason about it (a child overlapping
            // its own parent on purpose is not the case that check exists to
            // catch), and it stays inside the portrait's own box with room
            // either side, so it needs no AllowOverflow either. Shown only
            // while the acting character still has an unspent Second Life --
            // the one domain state this whole pass adds that was previously
            // invisible end to end (RunSettlement's own revive, not shown
            // anywhere before this).
            var secondLife = Ui.Solid("SecondLifeBadge", FightHudPalette.TargetAmber,
                    new UiVec(9.25f, 9.25f), Place.At(21.9f, -21.9f))
                .Inactive();
            SecondLifeBadge = secondLife;
            portrait.Children.Add(secondLife);

            // WIDE ENOUGH TO USE THE ROOM PartyClass GAVE BACK -- 185 (the
            // scaled figure) would still end well short of the freed span,
            // but there is nothing else on this row to align against any
            // more, so widening it further would only be guessing at how
            // much of a long name actually needs it. 185 is what a uniform
            // shrink of the old 220 gives, and it is not the bottleneck: a
            // runtime name this box cannot hold already truncates via TMP's
            // own overflow, same as every other UiString.Runtime label on
            // this screen.
            var name = Ui.Label("PartyName", UiString.Runtime, new UiVec(185f, 26.9f), 20,
                FightHudPalette.PartyNameText, Place.At(-102.6f, 52.1f, new UiVec(0f, 0.5f)));

            // PartyClass ("LV1 UTILITY") IS GONE -- balance-bot's cozy-plate
            // pass. It repeated a fact the dossier already states in full
            // sentences, and its own removal is most of where this card's
            // 452 -> 380 shrink comes from: the header row now needs to fit
            // only a portrait and a name, not a portrait, a name AND a
            // right-aligned second label competing for the same 32px row.
            var hpTag = Ui.Label("PartyHpTag", UiStrings.HpTag, new UiVec(18.5f, 16.8f), 10,
                FightHudPalette.HpBright, Place.At(-163.1f, 3.36f, new UiVec(0f, 0.5f)));
            var hpFill = Ui.Solid("PartyHpFill", FightHudPalette.HpBright, Place.Stretch(), UiSize.Fill);
            var hpBar = Ui.Panel("PartyHpBar", Place.At(-20.18f, 3.36f), UiSize.Fixed(245.5f, 13.45f), hpFill)
                .Coloured(FightHudPalette.Track);

            // 10pt, scaled down from 12 -- the box shrank with it (58.85,
            // from 70), so the "9999/9999 at 70/12 fits, do not grow past
            // it" relationship A1 pinned is preserved by the SAME ratio
            // rather than re-derived; E1 (this screen's own text-fit audit)
            // is the check that actually proves it still holds.
            var hpValue = Ui.Label("PartyHpValue", UiStrings.HealthValue, new UiVec(58.85f, 16.8f), 10,
                FightHudPalette.HpText, Place.At(163.1f, 3.36f, new UiVec(1f, 0.5f)));

            var mpTag = Ui.Label("PartyMpTag", UiStrings.MpTag, new UiVec(18.5f, 16.8f), 10,
                FightHudPalette.MpBright, Place.At(-163.1f, -18.5f, new UiVec(0f, 0.5f)));
            var mpFill = Ui.Solid("PartyMpFill", FightHudPalette.MpBright, Place.Stretch(), UiSize.Fill);

            // THE MANA-COST OVERLAY (PartyMpPreview) IS GONE, balance-bot
            // 2026-09-02: it drew a lighter segment inside the fill showing
            // what a hovered skill would cost, which is now double-booked
            // with the numeric cost already on the skill row/detail card --
            // and it stayed live even while the player was casting, tracking
            // a cost that no longer meant "if you pick this". The numeric
            // cost is the one source of truth now.
            var mpBar = Ui.Panel("PartyMpBar", Place.At(-20.18f, -18.5f), UiSize.Fixed(245.5f, 13.45f), mpFill)
                .Coloured(FightHudPalette.TrackMp);
            var mpValue = Ui.Label("PartyMpValue", UiStrings.HealthValue, new UiVec(58.85f, 16.8f), 10,
                FightHudPalette.MpText, Place.At(163.1f, -18.5f, new UiVec(1f, 0.5f)));

            PartyPortrait = portrait;
            PartyName = name;
            PartyHpFill = hpFill;
            PartyHpValue = hpValue;
            PartyMpFill = mpFill;
            PartyMpValue = mpValue;

            var plate = Ui.Container("PartyPlate", ButtonTheme.Blue, ContainerRatio.TwoByOne,
                Place.At(PartyPlateCentreX, PartyPlateCentreY),
                new UiVec(PartyPlateWidth, PartyPlateHeight));

            var content = new List<UiNode>
            {
                portrait, name, hpTag, hpBar, hpValue, mpTag, mpBar, mpValue, BuildWoolRow(),
            };
            content.AddRange(BuildPartyBuffIcons());
            Ui.ContainerContent(plate, ContainerRatio.TwoByOne, "PartyPlateContent", content.ToArray());

            PartyPlate = plate;

            // THE FRAME ART ITSELF, not the plate's non-drawing wrapper --
            // Ui.Container's BuildFrameHolder returns `plate` as a plain
            // Panel with the themed sprite as its one Decor child (named
            // "PartyPlateArt" by the "name + Art" convention every
            // Container/FlagBanner uses). RefreshPartyPlate needs this
            // node specifically to swap the runtime theme sprite -- the
            // wrapper itself carries no Image to swap.
            PartyPlateArt = plate.Children.FirstOrDefault();

            return plate;
        }

        // The party plate's own top edge in world Y. Both the transformation
        // strip and the roster stack build off this rather than off a
        // restated number, so moving the plate moves them with it. Computed
        // rather than const now that PartyPlateCentreY is (see its own
        // note) -- and computed as Centre + Height*0.5f, THE SAME EXPRESSION
        // UiRect.Top itself uses, rather than (Centre - Height*0.5f) + Height
        // (algebraically equal, but a different float instruction sequence
        // that rounded to a different last bit here): TransformStrip is
        // placed flush against this value, and UiAudit's SiblingOverlap
        // check is a strict `>`, so an ULP of drift between "the plate's own
        // solved Top" and "the Y this constant claims that Top is" reads as
        // a genuine (if 0px-wide) overlap rather than the two edges touching.
        private static float PartyPlateTopY => PartyPlateCentreY + PartyPlateHeight * 0.5f;

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
        // LEFT FLAT, NOT CONVERTED (owner's HQ-kit instruction, 2026-09-07,
        // asked for Container(Silver, TwoByOne) here "at their current size
        // if within tolerance, otherwise the nearest size that is"). 452x44
        // is aspect 10.27, 414% off TwoByOne's 2.0 -- Ui.ContainerSizeFor*
        // gives 452x226 as the nearest conforming size, a 5x GROWTH in
        // height. RosterPitchY would grow from 50 to 232 with it, and two
        // slots plus the transform strip would then need roughly 460px of
        // vertical room between the party plate's top edge and the stage
        // above it, where the design has room for under 100 today -- this
        // does not "resize a plate", it breaks the HUD's whole vertical
        // budget for two non-acting party members. Judged worse than
        // leaving this one flat; see the conversion report for the
        // alternative (FiveByOne, still a 2x height growth) if a resize is
        // wanted after all.
        private const int RosterSlots = 2;
        private const float RosterPlateW = 452f;
        private const float RosterPlateH = 44f;
        private const float RosterGap = 6f;

        // Computed, not const, now that PartyPlateTopY is (see its own note).
        private static float RosterFirstY =>
            PartyPlateTopY + TransformStripH + RosterGap + RosterPlateH * 0.5f;
        private const float RosterPitchY = RosterPlateH + RosterGap;

        // 20/24, per section 1's measured table -- the gap between Name's
        // right edge (-66) and HpValue's left edge (136), 202px wide, at the
        // same y9 those two share. 4 statuses plus the "+N" chip, same as the
        // enemy row; no counter is shown here (the tooltip carries duration
        // instead), but the Counter node is still declared for anatomy
        // parity with EnemyStatusBadge -- Package C simply never writes to
        // it on this surface.
        private const float RosterStatusBadgeSize = 20f;
        private const float RosterStatusPitch = 24f;
        private const int RosterStatusBadgesPerRow = 5;

        // Midpoint of the -66..136 clear band.
        private const float RosterStatusX0 = 35f;

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

                // No backing strip here -- the plate itself is the painted
                // surface the strip exists to provide on bare stage floor
                // (section 7).
                for (int k = 0; k < RosterStatusBadgesPerRow; k++)
                {
                    float x = RosterStatusX0 + (k - RosterStatusBadgesPerRow / 2) * RosterStatusPitch;
                    var badge = BuildStatusBadge($"RosterStatusBadge{i}_{k}", x, 9f, RosterStatusBadgeSize, 8, 7);
                    RosterStatusBadges.Add(badge);
                    plate.Children.Add(badge);
                }

                RosterPlates.Add(plate);
                return plate;
            });
        }

        // Six badges above the portrait, reading whatever
        // FightHudModel.StatusRowsFor says is currently on the acting
        // character. The portrait had nothing here before the original
        // status-effect UI pass -- a buff a relic granted was visible
        // nowhere except a menu that does not even say it fired.
        //
        // REBUILT THROUGH BuildStatusBadge, same Glyph/Code/Counter anatomy
        // as the enemy and roster rows -- PLAN_STATUS_EFFECT_UI's own defect
        // list, package B's first pass left this one as a plain chromeless
        // Ui.Button (root Image doubling as both frame and glyph, Code
        // carrying the counter folded into its own text: "PSN·2"), which is
        // what made the first capture's party badges unreadable tinted
        // smudges with text stacked on top. There is no structural reason
        // for the party plate to be the one surface with fewer layers than
        // the other two; BuildStatusBadge already builds a hoverable button
        // with a dedicated Frame (the root Image), Glyph, Code and Counter,
        // so this just calls it at the party's own size/pitch instead of
        // hand-rolling a shorter anatomy.
        //
        // 6, UP FROM 4 (PLAN_STATUS_EFFECT_UI section 1) -- section 6 found
        // the realistic worst case for a party member (Poison, Vulnerable,
        // Shielded, Regen, Protect, one speed entry) is exactly 6, so the
        // party plate's own slots cover it with no overflow chip needed in
        // practice; the 6th slot doubles as the "+N" chip on the rare 7th.
        // Unchanged by the B2 shrink -- the row scales with the plate, not
        // the slot count.
        private const int PartyBuffSlots = 6;

        // 22.7x22.7, DOWN FROM 27x27 -- the B2 cozy-plate shrink
        // (PartyPlateWidth 452 -> 380) scaled the whole row by the same
        // 0.8407 the rest of BuildPartyPlate is scaled by (see its own
        // header); pitch scaled with it, 27.74 down from 33, so the gap
        // between badges shrinks by the same proportion rather than toward
        // a collision.
        private const float PartyBuffIconSize = 22.7f;
        private const float PartyBuffPitch = 27.74f;

        // Y scaled with the row (30 -> 25.22): the header row (PartyName)
        // and the HP row below it both moved with the same shrink, so the
        // clear band between them scaled too rather than needing to be
        // re-found by hand.
        private const float PartyBuffY = 25.22f;

        // X0 scaled with the row (-111 -> -93.32): still clear of the
        // portrait's own (now smaller) right edge by the same proportion.
        private const float PartyBuffX0 = -93.32f;

        // 8/7, scaled down from 9/8 (a 27px icon's own font size, x0.8407)
        // -- the enemy row's 11/9 at 36px and the roster row's 8/7 at 20px
        // are the two ends this row already sat between; scaling down with
        // the icon keeps it there rather than picking a third number.
        private const int PartyBuffCodeFontSize = 8;
        private const int PartyBuffCounterFontSize = 7;

        private IEnumerable<UiNode> BuildPartyBuffIcons()
        {
            PartyBuffIcons.Clear();

            for (int i = 0; i < PartyBuffSlots; i++)
            {
                float x = PartyBuffX0 + i * PartyBuffPitch;
                var icon = BuildStatusBadge($"PartyBuff{i}", x, PartyBuffY, PartyBuffIconSize,
                    PartyBuffCodeFontSize, PartyBuffCounterFontSize);

                PartyBuffIcons.Add(icon);
                yield return icon;
            }
        }

        // 16 pips rather than a fraction. A charge meter you can watch fill
        // without reading numbers is the whole reason this is not just another
        // "3/16" label.
        private UiNode BuildWoolRow()
        {
            // Every number in this method is the old 410-wide row's own
            // number times PartyPlateWidth / 452f (0.8407) -- see
            // BuildPartyPlate's own header for why a uniform scale, not a
            // redesign, is what B2's shrink applies here too. WoolPips
            // (16) is untouched: it is a display reservation for a gameplay
            // number (FightHudSpec.WoolPips), not a layout choice this pass
            // gets to make smaller.
            var divider = Ui.Solid("WoolDivider", FightHudPalette.Hairline, new UiVec(339.65f, 0.84f), Place.At(0f, 18.5f))
                .AllowOverflow("the divider sits ON the row's top edge, marking where the wool block begins");

            var tag = Ui.Label("WoolTag", UiStrings.WoolHeading, new UiVec(50.44f, 16.8f), 10,
                FightHudPalette.TextMuted, Place.At(-169.82f, 0f, new UiVec(0f, 0.5f)));

            var pips = Ui.Each(Enumerable.Range(0, FightHudSpec.WoolPips).ToList(), (_, i) =>
            {
                var pip = Ui.Solid($"WoolPip{i}", FightHudPalette.PipEmpty, new UiVec(10.93f, 16.8f),
                    Place.At(-112.66f + i * 15.13f, 0f));
                WoolPips.Add(pip);
                return pip;
            });

            var value = Ui.Label("WoolValue", UiStrings.SignatureValue, new UiVec(46.24f, 16.8f), 13,
                FightHudPalette.TextPrimary, Place.At(169.82f, 0f, new UiVec(1f, 0.5f)));
            WoolValue = value;

            var children = new List<UiNode> { divider, tag };
            children.AddRange(pips);
            children.Add(value);

            // 344.69 wide, not the plate's own 380: same reasoning the old
            // 410-vs-452 gap documented, scaled down with it -- the row's
            // actual content already tops out at +-169.82, and the panel's
            // own declared box (what A2 checks against the container's
            // inset) has to clear both its own content and the container's
            // left/right bound (+-176.7 at this plate size).
            var row = Ui.Panel("WoolRow", Place.At(0f, -55.49f), UiSize.Fixed(344.69f, 33.63f), children);
            WoolRow = row;
            return row;
        }

        // ---- column A: the verbs -------------------------------------------------

        private const float VerbColumnX = -286f;

        // Reads FightSubmenuLayout.VerbRowW/VerbRowH rather than restating
        // 300/52 -- FightSubmenuLayout.VisibleBottomLine has to pick the
        // SAME plate shape these rows actually build at (see its own
        // comment), so there is one copy of the row's own size, not two that
        // could drift.
        private const float VerbRowW = FightSubmenuLayout.VerbRowW;
        private const float VerbRowH = FightSubmenuLayout.VerbRowH;
        private const float VerbPitch = 62f;

        // Rendered BOTTOM-UP so ATTACK sits nearest the cursor, and tiered so
        // read order matches use frequency: ATTACK loud, SKILL/ITEM neutral,
        // MOVE quiet. That hierarchy is the main fix over v1's earlier row of
        // five identical gold buttons.
        //
        // RUN REMOVED, not hidden. It never actually fled a fight -- the
        // handler behind it always answered "There is no way out of this
        // one." and there was no Flee/Run method anywhere in FightSession to
        // wire it to. A command that exists only to refuse itself is worse
        // than no command, so it is gone rather than joining the fourth row
        // as a second hidden-but-wired row. That fourth row read HOLD BACK
        // until banking was removed; it reads MOVE now, and unlike Hold Back
        // it nests.
        private IEnumerable<UiNode> BuildVerbColumn()
        {
            var labels = new[]
            {
                UiStrings.VerbAttack, UiStrings.VerbSkill, UiStrings.VerbItem,
                UiStrings.VerbMove,
            };
            var hotkeys = new[]
            {
                UiStrings.HotkeyOne, UiStrings.HotkeyTwo, UiStrings.HotkeyThree,
                UiStrings.HotkeyFour,
            };
            // MOVE NESTS. Hold Back resolved on the press and had no caret;
            // Move opens a two-row column (FORWARD/BACK), so it wears one.
            var nests = new[] { false, true, true, true };

            // ATTACK/SKILL/ITEM/MOVE, in BuildVerbColumn's own fixed
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
            //
            // 300x80 was 3.75:1 against the Legacy plate's true 3:1 --
            // ThemedButtonAspectLintTests. Height up to nominal, width kept;
            // the new bottom edge (-470) still clears CommandBottom (-486),
            // so the Y stays put.
            var continueSize = Ui.PlateNominalSizeFor(300f, 80f);
            var button = Ui.Button("ContinueButton", UiStrings.Continue, continueSize, 20,
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
                // Row6x1 (6.0, ln-distance 0.161 against FiveByOne's 0.344)
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
            //
            // 86, DOWN FROM 110 (FightSubmenuLayout.RowWidth's own comment:
            // 282 -> 240, ThemedButtonAspectLintTests). The hint keeps its
            // 150 -- it carries the longer, growing text ("SHOWING 9 OF 12")
            // -- so the trim comes off the short "S K I L L S" title instead,
            // same asymmetric split as the previous resize. 86 + 150 = 236,
            // 4px inside the row's new 240 (was 22px at the old 282).
            float headerY = FightSubmenuLayout.HeaderY(count);
            var title = Ui.Label("SubmenuTitle", UiStrings.SubmenuSkillsTitle, new UiVec(86f, 20f), 11,
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
            // misses the kit's 3:4 aspect (0.75 since the 2026-09-07 repin,
            // 0.588 before it) by more than Ui.Container's 5% band either
            // way. FrameWidth/FrameHeight (419.9x559.9, was 363.22x617.72)
            // are what the SAME inner box looks like grown to the kit's own
            // 5.2%/5.5% top/bottom inset and then completed to 0.75 exactly
            // -- the frame is height-bound now rather than width-bound; see
            // FightSubmenuLayout.FrameHeight's own comment for why, and for
            // why nothing below RowsBottom had to move for it. The flat Solid + Ui.Rim
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

        // The ONE tooltip every status badge on every surface shares --
        // enemy row, party plate and roster row alike (PLAN_STATUS_EFFECT_UI
        // section 11's fixed contract). Repositioned per hover through
        // Domain/UiKit/TooltipPlacement.cs's Beside, so its build-time
        // position here is only a placeholder that never has to be reached
        // -- unlike IntentTooltip above, whose fixed spot IS where it is
        // read. (PartyBuffTooltip, the party plate's own former fixed-spot
        // tooltip, was removed outright rather than left as a second example
        // here -- S4's review: dead the moment this shared tooltip shipped.)
        //
        // 300 WIDE, DOWN FROM 400 -- the first capture's defect: a
        // ~380x160 panel is sized for a paragraph, not "Rooted -- Skill
        // Only, 2 turns", and Beside's own reach (gap + half the badge +
        // half the tooltip) grows with this box, which is what pushed the
        // computed position far enough from the badge to read as
        // unrelated.
        //
        // PHASE 3: HEIGHT NO LONGER BAKED FOR THE FIVE-LINE CASE. The build-
        // time size only has to survive UiAudit, which solves this screen
        // with no real hover text in hand -- so it is built at ONE LINE tall
        // (StatusTooltipOneLineHeight) and Core/TooltipFit.ToBody grows it at
        // runtime for section 6's "+N" chip (up to five lines,
        // StatusTooltipMaxHeight -- the old fixed 130 the second capture
        // actually needed). A build-time 130 sat as
        // ~74px of empty violet under the common one-line case, which was
        // this screen's own second capture defect -- see PlaceStatusTooltip's
        // header for the first.
        public const float StatusTooltipWidth = 300f;
        public const float StatusTooltipPad = 10f;
        public const float StatusTooltipOneLineHeight = 56f;
        public const float StatusTooltipMaxHeight = 130f;

        private UiNode BuildStatusTooltip()
        {
            float labelW = StatusTooltipWidth - StatusTooltipPad * 2f;
            float labelH = StatusTooltipOneLineHeight - StatusTooltipPad * 2f;
            var label = Ui.Label("StatusTooltipText", UiString.Runtime, new UiVec(labelW, labelH), 13,
                FightHudPalette.GoldText, Place.At(0f, 0f));
            StatusTooltipText = label;

            var panel = Ui.Tooltip("StatusTooltip", PanelViolet, null, Place.At(-330f, -60f),
                UiSize.Fixed(StatusTooltipWidth, StatusTooltipOneLineHeight), label);
            StatusTooltip = panel;
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
            var pool = Ui.Pool("SpellVfx", FightHudSpec.SpellLayerRenderers, i =>
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
            var pool = Ui.Pool("SpellGroundVfx", FightHudSpec.SpellGroundRenderers, i =>
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

                SpellGroundVfx.Add(image);
                SpellGroundVfxNext.Add(next);
                return image;
            }).AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            SpellGroundVfxPool = pool;
            return pool;
        }

        // DETACHED DROPS, AND A BARE Image EACH.
        //
        // A SEPARATE NODE FROM THE SPRITE POOL rather than more members of it,
        // because every member of that pool carries a dissolve child and a
        // renderer component -- all of which is dead weight on a droplet, which
        // draws one still and never cross-fades. Sixty-four of them carrying a
        // second Image apiece would double the canvas's rebuild for nothing.
        //
        // A LATER SIBLING THAN THE SPRITE POOL AND AN EARLIER ONE THAN THE
        // NUMBERS, which is the whole of its draw order: spray over the crown
        // it came out of, under the damage figure. That is the brief's
        // "appropriate contact/foreground spray" as a node inside the effects
        // band, not as a third band.
        private UiNode BuildSpellParticles()
        {
            var pool = Ui.Pool("SpellParticles", FightHudSpec.SpellParticles, i =>
            {
                // The size here only has to be non-zero, which is UiAudit's
                // rule about zero-sized graphics: every drop's real box is
                // computed per frame from its own size variation.
                var image = Ui.Sprite($"SpellParticle{i}", null, new UiVec(64f, 64f), Place.At(0f, 0f))
                    .Inactive();

                SpellParticles.Add(image);
                return image;
            }).AllowOverlap("a pool's own rect is the whole canvas because its members are placed at runtime; it draws nothing itself and takes no clicks");

            SpellParticlePool = pool;
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
