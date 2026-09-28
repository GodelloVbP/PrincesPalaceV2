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

        // An event fight's overlay (the flock closing in) and its round
        // counter (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.4, M6). Both start
        // hidden; FightController shows them only for a fight with a round
        // limit.
        public NodeRef RoundOverlay;
        public NodeRef RoundCounter;
        public NodeRef RoundCounterPlate;

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

        // The same thing over the PARTY figures, live only while an ALLY is
        // being picked (AUDIT #147). Same node, same rule, built by the same
        // loop -- clicking the character you mean to help is what a player
        // tries first, exactly as clicking the monster is.
        //
        // INDEXED BY STAGE SLOT, which on this side is NOT the party list's
        // order: a slot belongs to one character for the whole fight while
        // the party list reorders on every Move (see
        // FightController.StageVisuals.DrawSide -- "the two halves are
        // indexed differently and that is the point"). The controller
        // resolves a slot to its occupant rather than indexing the party by
        // it; the enemy side gets away with treating the two as one number
        // only because nothing on that side ever moves.
        public List<NodeRef> PartyHitAreas = new List<NodeRef>();

        // One per party SEAT (0 = front), standing where a figure in that seat
        // would stand -- the destinations of a seat pick (Palace Passage onto
        // any seat, PLAN_BELLWETHER_KIT 1.2/3.6). INDEXED BY SEAT, not by
        // stage slot: an empty seat has no character and so no slot. Inactive
        // until a seat pick opens; only EMPTY seats show one (an occupied
        // seat is picked on its figure or plate, as before). See
        // BuildPartySeatMarkers.
        public List<NodeRef> PartySeatMarkers = new List<NodeRef>();
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

        // THE AFTERIMAGE, drawn UNDER EnemyPlateHpFills so a hit reads as a
        // chunk taken off the bar rather than a snap -- FightController.Hud's
        // UpdateGhostFill eases this one down to meet the live fill over
        // GhostFillDecaySeconds. HpDeep rather than a new token: a darker
        // shade of the same hue reads as "the health that WAS here" without
        // introducing a fourth colour to this one bar.
        public List<NodeRef> EnemyPlateGhostFills = new List<NodeRef>();

        // THE WARD SEGMENT, drawn OVER both of the above -- sized ward/maxHP
        // and clamped, starting where the HP fill ends (SetWardFill). Hidden
        // outright with no ward up, the same "only an elite/boss carries a
        // BreakShield at all" rule EnemyPlateBreakTracks already follows.
        public List<NodeRef> EnemyPlateWardFills = new List<NodeRef>();

        public List<NodeRef> EnemyPlateTags = new List<NodeRef>();
        public List<NodeRef> EnemyPlateReticles = new List<NodeRef>();
        public List<NodeRef> EnemyPlateBreakTracks = new List<NodeRef>();
        public List<NodeRef> EnemyPlateBreakFills = new List<NodeRef>();

        // ---- the HUD column: three PC plates ---------------------------------
        //
        // CARD-MAJOR EVERYWHERE, without exception. Plate i's four pool-rim
        // edges are PcMpRims[4i..4i+3] in Ui.RimEdgesOf's Top/Bottom/Left/
        // Right order, and its five status badges are
        // PcStatusBadges[5i..5i+4]. FightController indexes both off i, so an
        // edge-major mix-up here paints the wrong plate rather than throwing
        // -- rim edges are AsDecor, so UiAudit never looks at their colour.
        // FightScreenTests pins every one of these lists by literal node
        // name for exactly that reason.
        //
        // PartyPlate/PartyName/PartyHpFill/PartyMpFill/PartyHpTag/PartyMpTag/
        // PartyMpShade/PartyMpRims/PartyCardRims/PartyBuffIcons/WoolRow/
        // WoolPips and the whole Roster* family are GONE (2026-09-10). They
        // were two anatomies for one idea -- see BuildPcPlates' own header --
        // and the acting character is a highlight on their own plate now
        // rather than a promotion to a taller card.
        // BUTTONS, not panels, since AUDIT #147: a ward, a gift and every
        // single-ally skill authored after them are confirmed by clicking the
        // squadmate, on the plate that already IS that squadmate. Nothing
        // about the card's drawing changed -- the click target is the plate
        // node itself, NoChrome so it paints none of its own -- which is the
        // same shape the enemy plates have carried all along.
        public List<NodeRef> PcPlates = new List<NodeRef>();

        // The ally-pick marker, one per plate: the enemy plates' own 12px
        // rotated square, in the margin on the party column's outer edge.
        // Shown only while an ally is being picked, and greyed on a plate
        // this cast will not accept -- the mirror of EnemyPlateReticles.
        public List<NodeRef> PcPlateReticles = new List<NodeRef>();

        // The leather itself, one Image per plate, swapped at runtime from
        // the occupant's characters.json plateArt. THE identity surface on
        // this column: the head embossed at the right end is who the plate
        // belongs to.
        public List<NodeRef> PcPlateArts = new List<NodeRef>();

        // The acting halo, one per plate, hidden until its occupant is the
        // one acting and tinted with their PcTheme.Rim when they are. Exactly
        // one is ever shown, which PartyFormationCaptureTests asserts.
        public List<NodeRef> PcPlateHighlights = new List<NodeRef>();

        // THE IDENTITY WORD, one per plate, in the free band under the bars:
        // MASTER once mastery is collected, else whichever title the player
        // has chosen. ONE word -- three ("LEGEND . VICTOR . MASTER") read as
        // noise at this band's width, and VICTOR is a victory-screen word,
        // not a plate one. Empty and hidden for every character below level
        // 31, which is most of a career -- see Core/CharacterIdentity.
        // LookFor's PlateWord.
        public List<NodeRef> PcIdentityLines = new List<NodeRef>();

        // THE IDENTITY RIM, one per plate: the plate's own glow decal again,
        // tinted silver or gold by the collected PlateRim node (or forced gold
        // by Mastery). A SECOND decal rather than a state on the acting
        // highlight, because the two answer different questions -- the
        // highlight says whose turn it is and goes out again, and a rim earned
        // at level 32 is worn for the rest of the career.
        public List<NodeRef> PcPlateRims = new List<NodeRef>();

        public List<NodeRef> PcNames = new List<NodeRef>();
        public List<NodeRef> PcSignatures = new List<NodeRef>();
        public List<NodeRef> PcHpValues = new List<NodeRef>();
        public List<NodeRef> PcHpFills = new List<NodeRef>();

        // The party column's own afterimage and ward segment -- same
        // mechanism and same reasoning as EnemyPlateGhostFills/
        // EnemyPlateWardFills above, one bar, one rule for both plate kinds.
        public List<NodeRef> PcGhostFills = new List<NodeRef>();
        public List<NodeRef> PcWardFills = new List<NodeRef>();

        // "+12" beside the HP value while a shield is up, hidden at zero --
        // owner's playtest ask (2026-09-23), see PcPlate{i}HpWardFill's own
        // build-site comment. Right-aligned against the bar's own right
        // edge, mirroring hpValue's left-aligned margin, so the two never
        // contest the same pixels regardless of how many digits either one
        // carries.
        public List<NodeRef> PcWardValues = new List<NodeRef>();

        public List<NodeRef> PcMpValues = new List<NodeRef>();
        public List<NodeRef> PcMpFills = new List<NodeRef>();

        // THE SECOND METER IS NOT A MANA BAR (plan P7). It draws whatever pool
        // its holder actually carries -- mana for most, Fury for Bjorn, and
        // whatever a future row authors. Ui.Meter BAKES its colours at
        // scene-build time and Ui.Label bakes its tag, so every surface of
        // that meter which is not the resource-neutral sheen has to be
        // reachable at runtime: the fill, the band under it, the four rim
        // edges and the value (whose template carries the tag as an
        // argument). The HP meter beside it stays baked -- red is health on
        // every plate, whoever's plate it is.
        public List<NodeRef> PcMpShades = new List<NodeRef>();
        public List<NodeRef> PcMpRims = new List<NodeRef>();

        // Five per plate, flattened CARD-MAJOR.
        public List<NodeRef> PcStatusBadges = new List<NodeRef>();

        // The one status box the whole screen shares: every active status on
        // ONE actor, one row each, hung under that actor. See BuildStatusBox
        // for what it replaced and why a wider tooltip would not have done.
        // Both lists are ROW-MAJOR and the same length -- row i is
        // StatusBoxIcons[i] beside StatusBoxTexts[i].
        public NodeRef StatusBox;
        public List<NodeRef> StatusBoxIcons = new List<NodeRef>();
        public List<NodeRef> StatusBoxTexts = new List<NodeRef>();

        // ONE badge for a party-wide charge (RunSettlement's own revive),
        // parked on the bottom plate -- see BuildPcPlates for why it has no
        // per-character home.
        public NodeRef SecondLifeBadge;

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
        public NodeRef DetailColumnFill;
        public List<NodeRef> DetailColumnRim = new List<NodeRef>();
        public NodeRef DetailName;
        public NodeRef DetailKind;

        // ICON ROWS, NOT TEXT (2026-09-23 icon rework) -- DetailBody and the
        // DetailStatKeys/DetailStatValues/DetailDamageType text-row pool
        // this replaced are gone from the built tree; see BuildDetailColumn's
        // own header. One pool of icon+value pairs, sized to
        // FightHudSpec.DetailIconRows, the same fixed-pool/toggle-visibility
        // idiom the old row pool used.
        public List<NodeRef> DetailIconImages = new List<NodeRef>();
        public List<NodeRef> DetailIconValues = new List<NodeRef>();

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

            // Over the painting and its scrim, under every figure: what closes
            // in round by round closes in BEHIND the fight, in the fog, not in
            // front of the people in it. Root canvas, like the scrim, so the
            // nested HUD canvas draws every figure over it whatever its scale.
            children.Add(s.BuildRoundOverlay());

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
                s.PartyNameplates, s.PartyFootShadows, s.PartyFootGlows,
                intentIcons: null, hitAreas: s.PartyHitAreas);
            var enemyStage = s.BuildStage("Enemy", mirrored: false,
                FightStageAnchors.EnemyShadowColor, s.EnemySlots, s.EnemySprites, s.EnemyHitFlashes,
                s.EnemyNameplates, s.EnemyFootShadows, s.EnemyFootGlows, s.EnemyIntentIcons, s.EnemyHitAreas);
            s.PartyStage = partyStage;
            s.EnemyStage = enemyStage;

            // BEFORE the party stage, so every figure paints over a marker
            // rather than a marker over a neighbour's feet. A frame of its
            // own rather than children of the stage: DrawSide re-sorts the
            // stage's children by sibling index 0..2 on every repaint, and a
            // marker among them would be sorted into the figures' order.
            hud.Add(s.BuildPartySeatMarkers());
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
            hud.AddRange(s.BuildRoundCounter());
            hud.AddRange(s.BuildEnemiesHeading());
            hud.AddRange(s.BuildEnemyPlates());
            hud.AddRange(s.BuildPcPlates());
            hud.AddRange(s.BuildVerbColumn());
            hud.Add(s.BuildBreadcrumb());
            hud.Add(s.BuildContinueButton());
            hud.Add(s.BuildSubmenuColumn());
            hud.Add(s.BuildDetailColumn());
            hud.Add(s.BuildTargetPrompt());
            hud.Add(s.BuildIntentTooltip());
            hud.Add(s.BuildStatusBox());
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
                // BOTH RACKS NOW (AUDIT #147). It used to be `!mirrored`, i.e.
                // enemies only, because allies were never targets; the guard
                // is the CALLER's list now, so a stage side gets figure
                // targets exactly when it was handed somewhere to record them.
                UiNode hitArea = null;
                if (hitAreas != null)
                {
                    // NoChrome, NOT AsDecor and not merely a null SpriteKey.
                    // The monster is the button and this is only the area that
                    // hears the click, so it must draw nothing -- but AsDecor
                    // takes raycasting away with the drawing, and an empty
                    // SpriteKey selects the shared button face, which is what
                    // put three gold slabs over the monsters the first time
                    // this ran. NoChrome keeps the Image, keeps the raycast,
                    // and paints nothing; see UiEmitter.EmitButton.
                    hitArea = Ui.Button($"{prefix}HitArea{slot}", UiString.Runtime,
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

        // ---- empty-seat markers (PLAN_BELLWETHER_KIT 1.2/3.6, M3) ------------
        //
        // WHERE A FIGURE IN THAT SEAT WOULD STAND: the party formation's own
        // seat offset and depth scale (FightStageAnchors, the one place a slot
        // position is computed), in the stage's own centred frame -- so the
        // ring lands on the floor exactly under where the traveller will be
        // drawn once he steps there.
        //
        // A FIXED-SIZE BUTTON, NoChrome, with a floor ring and the seat's
        // name as decor children: the ring is what says "you can go here",
        // the word is what says which seat, and the button's default hover
        // rim is the gamepad focus box every other stop on the rack gets.
        private const float SeatMarkerW = 200f;
        private const float SeatMarkerH = 110f;

        // How far below the ground line the marker's bottom edge sits, so the
        // ring (the lower half) straddles the floor the way a contact shadow
        // does.
        private const float SeatMarkerSink = 30f;

        private UiNode BuildPartySeatMarkers()
        {
            int count = CombatEncounterSeats;
            var words = new[] { UiStrings.FightSeatFront, UiStrings.FightSeatMiddle, UiStrings.FightSeatRear };
            var children = new List<UiNode>();
            for (int i = 0; i < count; i++) PartySeatMarkers.Add(default);

            for (int seat = 0; seat < count; seat++)
            {
                var offset = FightStageAnchors.SlotOffset(seat, count, mirrored: true);
                float scale = FightStageAnchors.SlotScale(seat, count);

                var ring = Ui.Sprite($"PartySeat{seat}Ring", "proc:ring_outline",
                        Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 0.5f)), UiSize.Fill)
                    .Coloured(SeatMarkerRingHex)
                    .AsDecor();

                var word = Ui.Label($"PartySeat{seat}Word", words[seat],
                        new UiVec(SeatMarkerW, 40f), 22, FightHudPalette.TextPrimary,
                        Place.Pin(new UiVec(0.5f, 1f), new UiVec(0.5f, 1f), UiVec.Zero))
                    .AsDecor();

                var marker = Ui.Button($"PartySeatMarker{seat}", UiString.Runtime,
                        Place.At(offset.X, offset.Y - SeatMarkerSink * scale, new UiVec(0.5f, 0f)),
                        UiSize.Fixed(SeatMarkerW, SeatMarkerH), 1)
                    .NoChrome()
                    .Inactive()
                    .WithScale(new UiVec(scale, scale))
                    .AllowOverflow("the rear seat stands at the stage frame's edge exactly as the rear figure's slot does - the frame is a coordinate frame, not a clip region");
                marker.Children.Add(ring);
                marker.Children.Add(word);

                PartySeatMarkers[seat] = marker;
                children.Add(marker);
            }

            return Ui.Panel("PartySeatMarkerFrame", Place.At(0f, 0f),
                    UiSize.Fixed(FightStageAnchors.StageSize.X, FightStageAnchors.StageSize.Y), children.ToArray())
                .AllowOverlap("the seat markers share the stages' one centred frame, which overlaps both stages and the HUD by construction - it is a transparent coordinate frame, never a surface")
                .AllowOverflow("a seat marker stands on the floor line and straddles it, like a figure's contact shadow");
        }

        private const int CombatEncounterSeats = PrincesPalace.Domain.Party.PartySeat.Count;

        // The party's own contact-ring green, brighter and opaque: a
        // destination has to read on the floor at a glance.
        private const string SeatMarkerRingHex = "#7CF08AFF";

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

        // ---- an event fight's rounds (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md M6) ----

        // TOP CENTRE, UNDER THE BARK. The bark's log already spans the top
        // strip, so the counter hangs one gap below it, in the dead middle
        // between the two armies -- the same line the initiative tracker
        // starts on at the left, and nowhere near it: the tracker's six chips
        // end at x -448 and the counter's box starts at -RoundCounterWidth/2.
        // The width is measured, not guessed: UiStrings.FightRoundCounter's
        // sample is a label exactly MaxRoundLabelLength long plus a three-digit
        // round, and UiAudit refuses the box at any aspect it overflows.
        public const float RoundCounterWidth = 420f;
        public const float RoundCounterHeight = 48f;
        public const int RoundCounterFontSize = 30;
        public const float RoundCounterGap = 16f;

        // A SOFT DARK PLATE UNDER THE COUNTER. The dark text edge alone
        // (OverArt, the bark's answer) was not enough: the M6 captures put
        // "Toll 1" in gold straight over the pale canopy gap, where gold on
        // cream reads at barely any contrast. The plate is the scrim's own
        // shape and colour (radial_glow in the near-black violet, BuildScrim)
        // rather than a box, so it darkens what sits behind the words and
        // fades out before it reads as a panel. Sized to the words a real
        // label makes ("Toll 10" is ~110px at 30pt), not to the 420px audit
        // box, which only exists to prove the longest label fits.
        public const float RoundCounterPlateWidth = 320f;
        public const float RoundCounterPlateHeight = 104f;
        private const string RoundCounterPlateColour = "#0B0718C0";

        // The plate first: uGUI paints later siblings on top, so the label
        // lands over it. Both start hidden and FightController shows and
        // hides them together.
        private IEnumerable<UiNode> BuildRoundCounter()
        {
            float counterTop = -(BarkHeight + RoundCounterGap);
            float plateTop = counterTop + (RoundCounterPlateHeight - RoundCounterHeight) * 0.5f;

            var plate = Ui.Sprite("RoundCounterPlate", ScrimCentreKey,
                    Place.Pin(new UiVec(0.5f, 1f), new UiVec(0.5f, 1f), new UiVec(0f, plateTop)),
                    UiSize.Fixed(RoundCounterPlateWidth, RoundCounterPlateHeight))
                .Coloured(RoundCounterPlateColour)
                .AsDecor()
                .Inactive();
            RoundCounterPlate = plate;
            yield return plate;

            var counter = Ui.Label("RoundCounter", UiStrings.FightRoundCounter,
                    new UiVec(RoundCounterWidth, RoundCounterHeight), RoundCounterFontSize, FightHudPalette.HeadingGold,
                    Place.Pin(new UiVec(0.5f, 1f), new UiVec(0.5f, 1f), new UiVec(0f, counterTop)))
                .Styled(TypographyRole.FunctionalHeading)
                // The dark text edge stays too, for the plate's faded rim.
                .OverArt()
                .Inactive();
            RoundCounter = counter;
            yield return counter;
        }

        // Full-frame, keeping the art's aspect, hidden until an event fight
        // names an overlay. The controller scales it per round
        // (FightRoundPresentation.OverlayScaleFor) about the image's authored
        // pivot, held on the frame's authored anchor (PlaceOverlay) -- the
        // centre by default; at scale 1 with the defaults it is exactly the
        // canvas, which is what the audit measures.
        private UiNode BuildRoundOverlay()
        {
            var overlay = Ui.Sprite("RoundOverlay", null, Place.Stretch(), UiSize.Fill)
                .AsDecor()
                .Inactive();
            overlay.PreserveAspect = true;
            RoundOverlay = overlay;
            return overlay;
        }

        // ---- the dialogue bark (the combat log's home) ------------------------

        // Bark height, named rather than inlined -- InitiativeTracker's own Y
        // offset is pushed down by exactly this much below, so the two boxes
        // stack instead of overlapping. Change one, change the other.
        private const float BarkHeight = 130f;

        // THE LOG'S OWN BOX, in the bark's frame (x from the screen's centre).
        // Left clears the bark portrait's column; right stops one plate gap
        // short of the enemy block (PlateFirstX - PlateW / 2 is its left edge,
        // and the ENEMIES heading's).
        private const float LogLabelLeft = -820f;
        private const float LogLabelHeight = 108f;
        private const float LogLabelRight = PlateFirstX - PlateW * 0.5f - PlateGap;

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
            //
            // ENDS BEFORE THE ENEMY COLUMN, not at the screen's right edge. At
            // 1780 wide it ran to x 960, straight across the ENEMIES heading and
            // the plate stack: QA 2026-09-26 caught "...It's not very
            // effective." printed into the heading at x~1390 and a long line
            // sliding under the plates. AsDecor below kept the clicks right and
            // did nothing for the reading. The right edge is now the plate
            // block's left edge (the heading's too) less one plate gap, derived
            // from the same constants, so a wider plate moves the log with it.
            var label = Ui.Label("MessageLabel", UiString.Runtime,
                new UiVec(LogLabelRight - LogLabelLeft, LogLabelHeight), 20,
                FightHudPalette.TextPrimary, Place.At(LogLabelLeft, -8f, new UiVec(0f, 0.5f)))
                // ON THE ART, NOT A PLATE: the box went (see above) and the
                // text was left white on whatever the stage put behind it --
                // QA 2026-09-26 could barely read it over the treant's pale
                // canopy. The dark edge is what keeps it readable there, over
                // a dark trunk, and over a spell drawn across it.
                .OverArt();

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
        // GROWN AGAIN, 220 -> 260 (owner, 2026-09-10): "enemy names keep
        // truncating". They did, and not by a little -- the name box was 72px
        // at 11pt, where "Ironback Beetle" measures ~79 and
        // FightHudModel.DisplayNames appends " 2" onto a duplicate on top of
        // that, so the COMMON case was an ellipsis. The pass that put BRK
        // into the name row is what had squeezed it there.
        //
        // 40px of extra width buys 37 of content (the frame's 3.5% inset
        // takes the rest), and the name row spends all of it: BRK moves down
        // to the bar row, so the name has the whole span left of the HP value
        // -- 118px at 14pt, which fits every authored name plus its
        // duplicate suffix. Truncated() stays as the backstop for a boss name
        // nobody has authored yet.
        private const float PlateW = 260f;

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
                FightHudPalette.TextSecondary,
                Place.At(PlateFirstX - PlateW * 0.5f, EnemiesHeadingY, new UiVec(0f, 0.5f)))
                // Above the plates, so on the canopy, which is pale sky in
                // one place and near-black foliage a few pixels over. The
                // dark edge (OverArt) carries it over the sky; the colour has
                // to carry it over the leaves, and TextMuted/TextDisabled do
                // not (about 3:1 on the treant canopy's dark greens against
                // TextSecondary's 6:1 -- FightLogBandTests pins it). Heading
                // and count share it: one line of stage text, one colour.
                .OverArt();

            var hint = Ui.Label("EnemiesHint", UiStrings.StandingCount,
                new UiVec(240f, EnemiesHeadingHeight), 12,
                FightHudPalette.TextSecondary,
                Place.At(PlateBlockRight, EnemiesHeadingY, new UiVec(1f, 0.5f)))
                .OverArt();
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
                // TopY/BarY ARE LOAD-BEARING TOGETHER, and the HP box's
                // height is the third term: topY - hpHalf must equal
                // barY + barHalf + 8 (an 8px gap) AND the two outer margins
                // must match, which is two equations in two unknowns.
                // Re-solve them BOTH whenever either box's height moves.
                //
                // RE-SOLVED for the 2026-09-22 bar bump -- hp still 34 tall,
                // bar now 9 (up from 7, "if the audit allows" -- UiAudit is
                // what caught the first, height-only attempt at this: it
                // moved the bar's own half-height without re-running either
                // equation, and TheEnemyPlateTopRowAndBarAreCentredWithNo
                // BlankBand caught the resulting 1px asymmetry), plate
                // half-height 65:
                //   barY = -topY - 12.5   (equal outer margins, barHalf 4.5)
                //   topY - barY = 29.5    (the 8px gap, barHalf 4.5)
                // giving topY 8.5 (from 7.5) and barY -21 (unmoved), with
                // 39.5 of margin above and below -- down 1 from 40.5, which
                // is the whole 2px the bar grew split evenly across both
                // margins.
                const float topY = 8.5f;
                const float barY = -21f;

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
                var name = Ui.Label($"EnemyPlate{i}Name", UiString.Runtime, new UiVec(118f, 24f), 14,
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
                // 74x34 AT 13PT, scaled from 56x27 at 10pt by the same 1.3
                // the font moved by -- UiTextFitAudit measures the box
                // against the font, so growing one without the other is what
                // actually fails it. "9999/9999" is the sample it measures.
                var hp = Ui.Label($"EnemyPlate{i}Hp", UiStrings.HealthValue, new UiVec(74f, 34f), 13,
                    FightHudPalette.EnemyHpText, Place.At(rowRight, topY, new UiVec(1f, 0.5f)))
                    .TextAligned(UiTextAlign.Right);

                // THE AFTERIMAGE, UNDER the live fill in this list's order --
                // see EnemyPlateGhostFills' own header. Starts equal to the
                // fill (both anchor-stretched to Place.Stretch() at build
                // time, before the first repaint ever runs) so nothing
                // flashes a false chunk on the very first paint.
                var ghostFill = Ui.Solid($"EnemyPlate{i}GhostFill", FightHudPalette.HpDeep,
                    Place.Stretch(), UiSize.Fill);

                var fill = Ui.Solid($"EnemyPlate{i}HpFill", FightHudPalette.HpBright, Place.Stretch(), UiSize.Fill);

                // THE WARD SEGMENT, over both -- see EnemyPlateWardFills' own
                // header. Inactive at build time: SetWardFill only shows it
                // once a real ward exists, the same rule the break meter
                // below already follows.
                var wardFill = Ui.Solid($"EnemyPlate{i}WardFill", FightHudPalette.WardBright,
                        Place.Stretch(), UiSize.Fill)
                    .Inactive();

                // ONE LAYERED GROUP, the same mechanism Ui.Meter uses for its
                // own fill-plus-caption stack -- three fills sharing one
                // rect are meant to overlap, so this states that once rather
                // than UiAudit refusing each pair.
                Ui.Layered(ghostFill, fill, wardFill);

                // BRK'S ONLY REMAINING HOME, and it has moved DOWN a row
                // (2026-09-10). Phase 3 retired every other status to the
                // stage rows under each figure (see EnemyStatusLine's own
                // header) and left this node carrying nothing but the break
                // tag, at which point it was folded into the name row's
                // right end. That is what cost the name its width: at 11pt
                // in 72px the common case was an ellipsis, which is the
                // complaint this pass answers. The tag rides the HP BAR's
                // row now, right-aligned against the same rowRight, and the
                // bar gives up 28px of its right end for it -- a 7px bar
                // losing a tenth of its length is a far smaller loss of
                // information than a name the player cannot read.
                //
                // SetContent("") when the enemy is not broken (the normal
                // case) renders nothing, so the tag still needs no separate
                // active/inactive toggle and the bar simply looks short by
                // its own margin.
                var tags = Ui.Label($"EnemyPlate{i}Tags", UiString.Runtime, new UiVec(24f, 14f), 8,
                    FightHudPalette.TextSecondary,
                    Place.At(rowRight, barY, new UiVec(1f, 0.5f)))
                    .TextAligned(UiTextAlign.Right);

                // Starts where the name does and stops short of the BRK tag
                // that now shares its row -- 24px for the tag plus a 4px
                // gap. The bar used to hug rowRight itself; it cannot any
                // more without drawing under the tag, and a bar that ran
                // under a tag would read as a full bar rather than as a
                // shorter one.
                float barLeft = textLeft;
                float barRight = rowRight - 24f - 4f;
                float barCentreX = (barLeft + barRight) * 0.5f;
                float barWidth = barRight - barLeft;

                // 9 PX, UP FROM 7 (this pass, "if the audit allows" -- it
                // does: the row's own 11px of margin above/below (see
                // topY/barY's header) absorbs 2px with room left, and
                // UiAudit's four-aspect sweep is the gate that would have
                // said otherwise).
                var bar = Ui.Panel($"EnemyPlate{i}Bar",
                        Place.At(barCentreX, barY), UiSize.Fixed(barWidth, 9f), ghostFill, fill, wardFill)
                    .Coloured(FightHudPalette.Track);

                // A 12px square rotated 45 degrees, pinned just outside the
                // plate's left edge. Visible only while targeting.
                var reticle = Ui.Solid($"EnemyPlate{i}Reticle", FightHudPalette.TargetAmber,
                        new UiVec(12f, 12f), Place.At(-PlateW * 0.5f - 9f, 0f))
                    .Rotated(45f)
                    .Inactive()
                    .AllowOverflow("the reticle is deliberately OUTSIDE the plate - a marker in the margin, not a badge on the card");

                // THE BREAK METER, INSIDE the plate, one row under the HP bar
                // (QA 2026-09-26). It used to hang in the 12px gutter below
                // the frame under an AllowOverflow, from when the plate was
                // 64px tall and packed; at 130 the two rows sit centred with
                // 39.5px of margin under the bar, so the reason for leaving
                // the frame was gone and the meter read as belonging to
                // nothing -- or to the plate below. Same fill mechanism the
                // HP bar uses (SetFill), same width and x, BreakMeterGap
                // under it. It sits in the bottom margin rather than joining
                // the centred block because it is shown only for an elite or
                // boss (a BreakShield); re-centring on its presence would
                // move every ordinary plate's rows for a row they never show.
                const float breakH = 6f;
                const float BreakMeterGap = 4f;
                float breakY = barY - 4.5f - BreakMeterGap - breakH * 0.5f;
                var breakFill = Ui.Solid($"EnemyPlate{i}BreakFill", FightHudPalette.TargetAmber,
                    Place.Stretch(), UiSize.Fill);
                var breakTrack = Ui.Panel($"EnemyPlate{i}BreakTrack",
                        Place.At(barCentreX, breakY), UiSize.Fixed(barWidth, breakH), breakFill)
                    .Coloured(FightHudPalette.Track)
                    .Inactive();

                EnemyPlateIcons.Add(icon);
                EnemyPlateNames.Add(name);
                EnemyPlateHps.Add(hp);
                EnemyPlateHpFills.Add(fill);
                EnemyPlateGhostFills.Add(ghostFill);
                EnemyPlateWardFills.Add(wardFill);
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

        // ---- the three PC plates -------------------------------------------------
        //
        // ONE PLATE PER CHARACTER, ALWAYS (owner, 2026-09-10). This replaces
        // the party plate and the two roster cards outright, and it is a
        // change of MODEL rather than of layout: the column used to be "the
        // acting character, drawn big, plus whoever else is alive, drawn
        // small", so a character's card changed size, position and anatomy
        // every time the turn passed to them. It is now three identical
        // plates that never move, each one permanently a particular
        // character's, and ACTING is a highlight on their own plate.
        //
        // WHY THAT IS THE RIGHT MODEL AND NOT JUST A TIDIER ONE. Identity is
        // the art now: the owner drew three leather strips, each with its
        // character's head embossed at the right end, so a plate IS Shawn or
        // Bjorn or Odette the way a portrait is. A card that promoted the
        // acting member to a different, bigger card had to redraw that
        // identity in a second anatomy -- two ledgers, two sets of node
        // names, two paint routines, and the roster half structurally unable
        // to show what the party half could (the transform strip's own
        // removal note one revision ago is the same lesson). Three fixed
        // plates need one ledger, one paint routine and one array shape, and
        // a fourth party member costs a plate and a PNG rather than a third
        // card language.
        //
        // WHAT IS AUTHORED WHERE. The PLATE (which animal, which leather) is
        // CONTENT -- characters.json's plateArt, loaded at runtime by
        // PcPlateSprites, refused at build time if it names nothing. The
        // SHAPE of a plate (aspect, safe interior, how much of the right end
        // the head owns) is measured art and lives in PcPlateArt. This file
        // owns only the arrangement of rows on it.
        //
        // LEGIBILITY IS THE POINT OF THE PASS. The owner's screenshot at
        // 1080p had roster values at 9pt, party values at 10pt and enemy
        // names at 10pt, which is unreadable rather than merely small. Names
        // are 16pt here, every number 14pt, and both bars 20px tall with
        // their numbers drawn ON them -- which is what the extra width (452,
        // up from 380) is spent on, not on more rows.
        public const float PcPlateWidth = 452f;

        // NOT A LITERAL, and this is the one number on the column that must
        // not be: the plate is a SPRITE with a measured aspect, so a height
        // typed by hand is a claim about a PNG that a redelivery can
        // silently falsify -- the art would letterbox or stretch inside its
        // own rect and nothing would fail. 452 / 5.6111 = 80.55.
        public static float PcPlateHeight => PcPlateWidth / PcPlateArt.Aspect;

        // LEFT EDGE STAYS AT -920, which is where every card in this column
        // has started since the HUD pass before this one. The right edge
        // moves out to -468 with the extra width.
        private const float PcPlateCentreX = -920f + PcPlateWidth * 0.5f;

        private const float PcPlateGap = 4f;
        public const int PcPlateCount = 3;

        // BOTTOM-UP FROM THE SAME LINE THE VERB COLUMN AND THE SKILL PANEL
        // END ON (FightSubmenuLayout.VisibleBottomLine, -485.392), with a
        // ZERO bottom pad -- see PcPlateArt.VisiblePad for why these plates
        // have none where a kit container does.
        private static float PcPlateFirstY =>
            Ui.CentreYForVisibleBottom(FightSubmenuLayout.VisibleBottomLine, PcPlateHeight,
                PcPlateArt.VisiblePad.Bottom);

        private static float PcPlatePitchY => PcPlateHeight + PcPlateGap;

        // THE HARD CONSTRAINT, unchanged from the roster stack this replaces:
        // the stack's TOP EDGE must not rise into the party stage. The middle
        // party figure's foot ring already touches this column and the stage
        // draws OVER the HUD, so a taller stack puts feet on roster text.
        // Three plates at 80.55 plus two 4px gaps is 249.7 against the 290
        // the budget allows, and FightScreenTests pins both the budget and
        // the literal the stack actually reaches.
        public static float PcPlateStackTopY =>
            PcPlateFirstY + (PcPlateCount - 1) * PcPlatePitchY + PcPlateHeight * 0.5f;

        // ---- the plate's content box ----------------------------------------
        //
        // INSIDE THE RIM AND CLEAR OF THE HEAD. Left/top/bottom come from
        // PcPlateArt.Inset (measured: inside the lighter rim stroke and
        // outside the rounded corner arc). The RIGHT edge is not the inset at
        // all -- it is PcPlateArt.HeadZoneFrac, because the embossed head
        // owns that end of the plate and a name running under a bear's muzzle
        // is the one way this layout can look broken while every rect in it
        // is legal. FightScreenTests checks no text or badge rect enters it.
        private static float PcContentLeft =>
            -PcPlateWidth * 0.5f + PcPlateWidth * PcPlateArt.Inset.Left;

        public static float PcHeadZoneLeft => PcPlateWidth * (0.5f - PcPlateArt.HeadZoneFrac);

        private static float PcContentRight => PcHeadZoneLeft;

        private static float PcContentBottom =>
            -PcPlateHeight * 0.5f + PcPlateHeight * PcPlateArt.Inset.Bottom;

        // THE VERTICAL LEDGER, in plate-local Y (the plate is 80.55 tall, so
        // +-40.28), and the two rows are placed symmetrically about the
        // centre rather than packed against the top:
        //   38.07            content top (inset 2.21)
        //   25.5  ..   1.5   name / signature / badges   24
        //   -3.5  ..  -23.5  HP bar | pool bar           20   (gap 5)
        //   -23.5 ..  -37.85 free leather                14.35
        // The free band at the bottom is where the Second Life badge sits on
        // the bottom plate, and it is otherwise deliberate: the plate is a
        // leather STRIP with a head on it, and packing rows to both edges
        // would fight the art rather than sit on it.
        private const float PcNameRowY = 13.5f;
        private const float PcNameRowH = 24f;

        // 20, the owner's own floor for "a proper HP bar, not a red stripe",
        // and the number FightScreenTests pins as >=. Ui.Meter's sheen and
        // shade bands are FRACTIONAL (40%/25% of the bar), so 20 still shows
        // both separately.
        private const float PcBarRowY = -13.5f;
        private const float PcBarH = 20f;

        // The gutter either side of the hairline that splits the HP half from
        // the pool half.
        private const float PcBarGutter = 4f;

        // 16pt names, 14pt numbers -- the owner's sizes, stated once here.
        // Everything on this column reads one of these two.
        private const int PcNameFontSize = 16;
        private const int PcValueFontSize = 14;

        // 88 and 126 are UiTextFitAudit's own measurements of "9999/9999"
        // (76.4) and "FURY 9999/9999" (115.9) plus a margin.
        //
        // THESE USED TO BE THE WIDTH OF A CHIP as well as of a caption. The
        // owner asked for the numbers ON the bars, and drawn straight onto a
        // full HP fill in TacticalData they were barely readable -- "280/280"
        // against HpBright measured about 1.5:1 off party_formation's 4:3
        // frame, against WCAG AA's 4.5:1 -- so the first two passes put each
        // caption on a dark quad and then, when that quad read as an empty
        // track, on an outlined dark quad. Both were the same mistake in two
        // sizes: a chip on a meter is a shape the player has to be taught not
        // to read as a value, and the second capture of it still showed a
        // full HP bar as half drained.
        //
        // The treatment the captions actually wanted was a heavier MATERIAL,
        // and the note here used to say so and decline to build one, because
        // TacticalData's authored outline is switched off (width 0.07, no
        // OUTLINE_ON keyword) and Fonts/Materials/ was the owner's
        // uncommitted work. TypographyRole.OnBarCaption is that role now: a
        // NEW preset beside the six rather than an edit to any of them, so
        // nothing the owner is holding moves.
        private const float PcHpValueW = 88f;
        private const float PcPoolValueW = 126f;

        // 19, AND IT IS BOUNDED ON BOTH SIDES. UiTextFitAudit measures
        // "9999/9999" at 14pt as needing 18.2 of height and refused a 16-tall
        // box at build; the caption also has to stay INSIDE its own 20px
        // track (UiAudit's containment check, and FightScreenTests'
        // ThePcValuesAreDrawnOnTheirOwnBars). 19 is the only integer that
        // satisfies both, which is why the bar cannot shrink below 20 for a
        // second reason now.
        private const float PcValueBoxH = 19f;

        // ---- ROW 1'S WIDTH BUDGET, and every number in it is what
        // UiTextFitAudit accepted rather than what looked right.
        //
        // The row has 352.70 between the content's left edge and the head
        // zone, and it is spent 92 + 4 + 148 + 4 + 104 = 352. The first cut
        // spent 120 + 6 + 96 + 6 + 108 and the audit refused it at build:
        // "Moonlight 999/999" -- SignatureNamedValue's own sample, the
        // longest resource name a row is likely to author against a
        // three-digit pool -- wrapped onto a second line in 96px at 14pt.
        //
        // The signature is what grew and the NAME is what paid for it, which
        // is the right way round: a name box is sized for "Odette" (6
        // characters, ~58px at 16pt) with headroom, while the signature box
        // is sized for a string content can actually produce. Runtime names
        // longer than the box truncate through TMP's own overflow like every
        // other UiString.Runtime label on this screen; a sample the audit
        // measures cannot.
        private const float PcNameW = 92f;
        private const float PcSignatureW = 148f;
        private const float PcRowGapX = 4f;

        // 5 slots (4 statuses plus the "+N" overflow chip, the same
        // arrangement the enemy row uses), 20px as the owner asked, right-
        // aligned so they end where the head zone begins and grow leftwards
        // into the gap the signature leaves.
        private const float PcStatusBadgeSize = 20f;

        // 21, NOT 22: a 1px gap between badges rather than 2, which is what
        // the signature's audited width cost the row. Five slots span
        // 4 * 21 + 20 = 104.
        private const float PcStatusPitch = 21f;
        public const int PcStatusBadgesPerPlate = 5;

        // The party-wide revive charge (RunSettlement's own), which belongs to
        // no character -- it used to ride the acting card, and there is no
        // acting card any more. Parked in the free band at the bottom-left of
        // the BOTTOM plate: a fixed anchor at the foot of the column, which
        // is if anything clearer than a badge that moved with the turn.
        // THE IDENTITY LINE'S OWN ROW, in the free band the plate already
        // leaves between the bars (-23.5) and the content's bottom inset
        // (-37.85). 13px of label centred at -30.7 sits inside that with half
        // a pixel to spare at each end, and NOTHING ABOVE IT MOVES -- which is
        // the constraint this row was given rather than a happy accident. The
        // band was deliberate empty leather ("the plate is a leather STRIP
        // with a head on it"), and one quiet line of small caps is about the
        // most that can go in it without it stopping being that.
        private const float PcIdentityRowY = -30.7f;
        private const float PcIdentityRowH = 13f;
        private const int PcIdentityFontSize = 12;

        // INDENTED PAST THE REVIVE BADGE'S SLOT ON EVERY PLATE, including the
        // two that never carry one. The party-wide Second Life badge sits at
        // the bottom-left of the BOTTOM plate only, in this same band; a line
        // starting at the content's left edge would run under it on that one
        // plate and sit flush on the other two, so three plates would indent
        // differently for a reason no player could see. Reserving the slot
        // everywhere costs 18px of a 352px row and makes the column agree with
        // itself.
        private static float PcIdentityLeft =>
            PcContentLeft + PcSecondLifeSize + PcRowGapX;

        private static float PcIdentityWidth => PcContentRight - PcIdentityLeft;

        private const float PcSecondLifeSize = 14f;
        private static float PcSecondLifeY =>
            (PcBarRowY - PcBarH * 0.5f + PcContentBottom) * 0.5f;

        private static float PcBarW =>
            (PcContentRight - PcContentLeft - PcBarGutter * 2f - 1f) * 0.5f;

        private static float PcHpBarCentreX => PcContentLeft + PcBarW * 0.5f;
        private static float PcBarSplitX => PcContentLeft + PcBarW + PcBarGutter + 0.5f;
        private static float PcPoolBarCentreX => PcBarSplitX + 0.5f + PcBarGutter + PcBarW * 0.5f;

        private IEnumerable<UiNode> BuildPcPlates()
        {
            return Ui.Each(Enumerable.Range(0, PcPlateCount).ToList(), (_, i) =>
            {
                float w = PcPlateWidth;
                float h = PcPlateHeight;
                float left = PcContentLeft;

                // THE ACTING HIGHLIGHT, DECLARED FIRST so it draws BEHIND the
                // plate it haloes. One art-shaped decal, not four rim Solids:
                // the plate's corner radius is ~14% of its height (an 11px
                // arc at this size) and a rectangle of 1px Solids sticks a
                // square spur out of every corner of it. See PcPlateArt.
                // GlowKey's own note.
                //
                // Tinted at runtime with the occupant's PcTheme.Rim and shown
                // only while they are acting; baked white-and-hidden, which
                // is what an unrefreshed scene should show for a state that
                // belongs to a turn rather than to a character.
                float glowPad = h * PcPlateArt.GlowPadFrac;
                var highlight = Ui.Sprite($"PcPlate{i}Highlight", PcPlateArt.GlowKey,
                        Place.Stretch(-glowPad, -glowPad, -glowPad, -glowPad), UiSize.Fill)
                    .AsDecor()
                    .Inactive()
                    .AllowOverflow("a glow is a halo OUTSIDE the plate's silhouette -- containment is exactly what it must not have");

                // THE PLATE ITSELF. Baked with the shipped starter for this
                // slot (PcPlateArt.BakedDefaults) so a freshly built scene
                // reads as the roster that ships; RefreshPcPlate overwrites
                // it from the occupant's own plateArt on the first repaint.
                // THE IDENTITY RIM, between the acting halo and the plate:
                // the same decal at a third of the halo's pad, so a character
                // who is both acting and gold-rimmed reads as a tight metal
                // edge inside a wide bloom rather than as one thicker smear.
                // Baked white and hidden, like the halo -- an unrefreshed
                // scene must not show a rim nobody has earned.
                float rimPad = glowPad / 3f;
                var identityRim = Ui.Sprite($"PcPlate{i}Rim", PcPlateArt.GlowKey,
                        Place.Stretch(-rimPad, -rimPad, -rimPad, -rimPad), UiSize.Fill)
                    .AsDecor()
                    .Inactive()
                    .AllowOverflow("the identity rim is the plate's own glow decal at a smaller pad -- like the acting halo it sits OUTSIDE the silhouette on purpose, which is what makes it read as an edge round the plate rather than a band across it");

                var art = Ui.Sprite($"PcPlate{i}Art", PcPlateArt.BakedDefaults[i % PcPlateArt.BakedDefaults.Length],
                    Place.Stretch(), UiSize.Fill);
                art.PreserveAspect = true;
                art.AsDecor();

                // 16pt, hard left, in the occupant's own colour (PcTheme.Name
                // at runtime). The acting plate's name is the same label
                // brightened rather than a second, louder label -- one node
                // per fact.
                var name = Ui.Label($"PcPlate{i}Name", UiString.Runtime, new UiVec(PcNameW, 22f), PcNameFontSize,
                        FightHudPalette.TextPrimary, Place.At(left, PcNameRowY, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left);

                // THE SIGNATURE AS A NUMBER, not sixteen pips. The pip row was
                // this column's one bespoke widget and the owner's mock-ups
                // never had it; "Wool 2/10" says the same thing in 96px
                // instead of 280, which is what buys the badges their row.
                // Hidden outright when the occupant carries no signature,
                // which is two thirds of the party.
                var signature = Ui.Label($"PcPlate{i}Signature", UiStrings.SignatureNamedValue,
                        new UiVec(PcSignatureW, 20f), PcValueFontSize,
                        FightHudPalette.TextSecondary,
                        Place.At(left + PcNameW + PcRowGapX, PcNameRowY, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left)
                    .Styled(TypographyRole.TacticalData)
                    .Inactive();

                // ON THE BAR, and readable on any fill -- which is a
                // MATERIAL question, not a colour one, and two passes of this
                // column got that wrong before landing here.
                //
                // THE MATERIAL: TypographyRole.OnBarCaption -- TacticalData's
                // font and size band, with an outline actually switched on
                // (0.25) and the face and outline the OTHER way round from
                // every other role in the kit: near-black glyph inside a
                // near-white ring. Arithmetic, not taste. A caption on a
                // meter has to survive two unrelated backgrounds -- the
                // authored fill, and the near-black track behind it once the
                // bar drains -- and a LIGHT face cannot clear WCAG AA on
                // either fill this screen ships: pure white measures 3.32:1
                // against HpBright and 2.58:1 against MpBright. Dark reaches
                // 6.06 and 7.80, and the light ring is what carries the glyph
                // over the empty track. Measured on the party_formation
                // capture at 4.72-6.00 on the fills, 2.76 on bare track; see
                // TmpBootstrap.Typography's spec for why the two trade off
                // and where the balance point came from.
                //
                // Stated as a ROLE rather than a per-label outline so the
                // treatment lives in one place -- the enemy plates want it
                // next.
                //
                // THE LABEL COLOUR STAYS TextPrimary, and it is no longer the
                // thing that decides. TMP multiplies the vertex colour into
                // the material's face, so a near-white tint leaves a
                // near-black face where it is; the field is kept truthful
                // rather than zeroed because the roster rows share this
                // palette entry and a caption that read as untinted here and
                // tinted there would be two rules. What it must NOT become is
                // themed: FightController's ApplyPoolTheme passes this label
                // as `value: null` for exactly that reason -- the pool
                // colours the fill, the band and the rim, never the text on
                // top, because a caption tinted to match its own fill is a
                // caption that disappears into it by construction.
                var hpValue = Ui.Label($"PcPlate{i}HpValue", UiStrings.HealthValue,
                        new UiVec(PcHpValueW, PcValueBoxH), PcValueFontSize,
                        FightHudPalette.TextPrimary, Place.At(-PcBarW * 0.5f + 5f, 0f, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left)
                    .Styled(TypographyRole.OnBarCaption);

                // "+12" WHILE A SHIELD IS UP, HIDDEN AT ZERO -- owner's
                // playtest ask (2026-09-23): the ward segment (below) showed
                // no number anywhere on the card. Right-aligned against the
                // bar's own right edge rather than beside hpValue's own
                // left-aligned box: PcBarW is 171.85 at the current plate
                // width (452 * (0.5-0.1904) head zone, 0.0293 rim inset --
                // PcContentLeft/PcHeadZoneLeft's own derivation), so a
                // 40-wide box here and hpValue's 88-wide one sit ~34px apart
                // at their closest (both full 4-digit HP and a live ward) --
                // clear at every value either one can print. WardText, not
                // TextPrimary -- this sits on the SAME fill hpValue does at
                // full health (SetWardFill's own header, the "over the fill"
                // case), so it needs its own on-fill contrast rather than
                // TextPrimary's, which was only ever measured against
                // HpBright/MpBright. FightController.RefreshPartyMember sets
                // its content and visibility every repaint alongside
                // SetWardFill, off the same StatusEffects.WardPoints read.
                var wardValue = Ui.Label($"PcPlate{i}HpWardValue", UiString.Runtime,
                        new UiVec(40f, PcValueBoxH), PcValueFontSize,
                        FightHudPalette.WardText, Place.At(PcBarW * 0.5f - 5f, 0f, new UiVec(1f, 0.5f)))
                    .TextAligned(UiTextAlign.Right)
                    .Styled(TypographyRole.OnBarCaption)
                    .Inactive();

                // THE TAG IS AN ARGUMENT, not "MP" -- the meter draws whichever
                // pool its holder carries (pools.json owns the short tag and
                // the three hexes), so this reads "FURY 60/100" for Bjorn and
                // "MP 30/30" for everyone else.
                //
                // TextPrimary FOR THE SAME REASON THE HP CAPTION HAS IT, and
                // here the argument is stronger: the fill under this one is
                // whatever colour a pools.json row authored, so a caption
                // tinted to match it is a caption that disappears into it by
                // construction. FightController's ApplyPoolTheme therefore
                // passes this label as `value: null` -- the pool colours the
                // fill, the band and the rim, and never the text on top.
                var mpValue = Ui.Label($"PcPlate{i}MpValue", UiStrings.PoolNamedValue,
                        new UiVec(PcPoolValueW, PcValueBoxH), PcValueFontSize,
                        FightHudPalette.TextPrimary, Place.At(-PcBarW * 0.5f + 5f, 0f, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left)
                    .Styled(TypographyRole.OnBarCaption);

                var hp = Ui.Meter($"PcPlate{i}HpBar", $"PcPlate{i}HpFill",
                    Place.At(PcHpBarCentreX, PcBarRowY), new UiVec(PcBarW, PcBarH),
                    FightHudPalette.Track, FightHudPalette.HpRim,
                    FightHudPalette.HpBright, FightHudPalette.HpShade, hpValue, wardValue);

                // THE AFTERIMAGE AND THE WARD SEGMENT, on the party bar too --
                // one rule for both plate kinds (PcGhostFills/PcWardFills'
                // own header). Ui.Meter has no door for a layer UNDER its own
                // fill, so this reaches into the track's children directly,
                // the same way this file already reaches into a button's own
                // Children below (EnemyPlateFrames.Add(frame.Children.
                // FirstOrDefault())) -- inserted rather than appended, so
                // the ghost sits BEHIND hp.Fill instead of covering it.
                var pcGhostFill = Ui.Solid($"PcPlate{i}HpGhostFill", FightHudPalette.HpDeep,
                    Place.Stretch(), UiSize.Fill);
                var pcWardFill = Ui.Solid($"PcPlate{i}HpWardFill", FightHudPalette.WardBright,
                        Place.Stretch(), UiSize.Fill)
                    .Inactive();

                hp.Track.Children.Insert(0, pcGhostFill);
                hp.Track.Children.Insert(hp.Track.Children.IndexOf(hp.Fill) + 1, pcWardFill);

                // hpValue AND wardValue TOO -- Ui.Meter already declared
                // fill+hpValue+wardValue as one layered group internally;
                // these two new fills sit in that exact same stack and need
                // the same waiver, or the audit sees an UNDECLARED overlap
                // against the captions that Ui.Meter's own Layered call
                // never mentioned them to.
                Ui.Layered(pcGhostFill, hp.Fill, pcWardFill, hpValue, wardValue);

                var mp = Ui.Meter($"PcPlate{i}MpBar", $"PcPlate{i}MpFill",
                    Place.At(PcPoolBarCentreX, PcBarRowY), new UiVec(PcBarW, PcBarH),
                    FightHudPalette.TrackMp, FightHudPalette.MpRim,
                    FightHudPalette.MpBright, FightHudPalette.MpShade, mpValue);

                var split = Ui.Solid($"PcPlate{i}Split", FightHudPalette.Hairline,
                        new UiVec(1f, PcBarH), Place.At(PcBarSplitX, PcBarRowY))
                    .AsDecor();

                // THE ALLY RETICLE, the enemy plate's own marker mirrored to
                // this column's OUTER edge. The enemy plates put it at
                // -PlateW/2 - 9 because their column runs down the right of
                // the screen and the margin is on their left; this column
                // runs down the left, so its free margin is on the left too
                // and the marker sits at the same offset for the same reason
                // rather than at the mirrored one -- there is 40px of canvas
                // outside the plate here and nothing else in it.
                var reticle = Ui.Solid($"PcPlate{i}Reticle", FightHudPalette.TargetAmber,
                        new UiVec(12f, 12f), Place.At(-w * 0.5f - 9f, 0f))
                    .Rotated(45f)
                    .Inactive()
                    .AllowOverflow("the reticle is deliberately OUTSIDE the plate - a marker in the margin, not a badge on the card");

                // 12pt, hard left, quiet -- subordinate to the 16pt name two
                // rows above it, which is what a title is to a name.
                var identity = Ui.Label($"PcPlate{i}Identity", UiString.Runtime,
                        new UiVec(PcIdentityWidth, PcIdentityRowH), PcIdentityFontSize,
                        IdentityMetals.Line,
                        Place.At(PcIdentityLeft, PcIdentityRowY, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left)
                    .Inactive();

                PcPlateArts.Add(art);
                PcPlateRims.Add(identityRim);
                PcIdentityLines.Add(identity);
                PcPlateReticles.Add(reticle);
                PcPlateHighlights.Add(highlight);
                PcNames.Add(name);
                PcSignatures.Add(signature);
                PcHpValues.Add(hpValue);
                PcHpFills.Add(hp.Fill);
                PcGhostFills.Add(pcGhostFill);
                PcWardFills.Add(pcWardFill);
                PcWardValues.Add(wardValue);
                PcMpValues.Add(mpValue);
                PcMpFills.Add(mp.Fill);
                PcMpShades.Add(mp.Shade);
                foreach (var edge in Ui.RimEdgesOf(mp.Track)) PcMpRims.Add(edge);

                var children = new List<UiNode>
                {
                    highlight, identityRim, art, name, identity, signature,
                    hp.Track, mp.Track, split, reticle,
                };

                // RIGHT-ALIGNED, ending where the head zone begins. Laid out
                // left-to-right in index order so FightController's
                // PaintStatusRow -- which walks a flat index range and fills
                // it in order -- puts the first status furthest left and the
                // "+N" chip on the last slot.
                float lastCentre = PcContentRight - PcStatusBadgeSize * 0.5f;
                for (int k = 0; k < PcStatusBadgesPerPlate; k++)
                {
                    float x = lastCentre - (PcStatusBadgesPerPlate - 1 - k) * PcStatusPitch;
                    var badge = BuildStatusBadge($"PcStatusBadge{i}_{k}", x, PcNameRowY,
                        PcStatusBadgeSize, 9, 8);
                    PcStatusBadges.Add(badge);
                    children.Add(badge);
                }

                // ONE badge for a party-wide charge, on the bottom plate only
                // -- see PcSecondLifeY's own note.
                if (i == 0)
                {
                    var secondLife = Ui.Solid("SecondLifeBadge", FightHudPalette.TargetAmber,
                            new UiVec(PcSecondLifeSize, PcSecondLifeSize),
                            Place.At(left + PcSecondLifeSize * 0.5f, PcSecondLifeY))
                        .Inactive();
                    SecondLifeBadge = secondLife;
                    children.Add(secondLife);
                }

                // A BUTTON WHERE THIS WAS A PANEL, and nothing else about the
                // card moved: NoChrome so the button's own Image paints
                // nothing over the leather, the same 1.03 hover the enemy
                // plates take (four rows of text under a 1.05 press pop
                // swing sideways -- v1 made the same call at the same
                // number), and the children in the same order they were in.
                // Live only while an ally is being picked; RefreshPcPlates
                // owns that, exactly as RefreshEnemyPlates owns the mirror.
                var plate = Ui.Button($"PcPlate{i}", UiString.Runtime, new UiVec(w, h), 1,
                        Place.At(PcPlateCentreX, PcPlateFirstY + i * PcPlatePitchY))
                    .Hovers(1.03f)
                    .NoChrome();
                plate.Children.AddRange(children);

                PcPlates.Add(plate);
                return plate;
            });
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

        // Reads FightSubmenuLayout.VerbPitch rather than restating 62 --
        // FightSubmenuLayout.RowPitch (the submenu column's own row spacing)
        // now reads this same constant, which is the fix for the playtest
        // bug where the two columns' pitches had drifted apart. One copy,
        // not two that can disagree again.
        private const float VerbPitch = FightSubmenuLayout.VerbPitch;

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
            // next-action button, reserved for that one role, rather than a
            // combat choice - it only ever appears once a beat has resolved
            // and there is exactly one thing to do next.
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
            // by half a back row and half a gap. UNCHANGED by the flat frame
            // below: this is still "offset from the INNER box's own centre"
            // exactly as it always was, and parenting it under the frame
            // (which sits at FightSubmenuLayout.FrameCentreY, a few pixels
            // off ContainerCentreY -- see that property's own header) shifts
            // the whole block by that same small amount, automatically.
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

            // ---- the frame, now bare: NO PLATE, NO FILL, NO RIM ------------------
            //
            // WAS a flat Solid fill plus a hairline Rim (before that, a Violet
            // 3:4 Ui.Container -- see git blame for that art-inset history).
            // Owner playtest, 2026-09-23: "remove the container surrounding
            // it" -- the flat fill still read as a tall purple panel standing
            // behind buttons that are themed art in their own right and need
            // no backing plate, and it extended well past the last row/BACK
            // whenever the actor's kit was short (FrameHeight is sized off
            // BuildReservationRows' 8-row static reservation, not the real
            // count -- ResizeSubmenuContainer only shrinks it at runtime).
            //
            // SubmenuContainer SURVIVES as a plain, graphic-less Panel: it is
            // still the one thing ResizeSubmenuContainer/AnchorSubmenuRows
            // move and resize (FightController.cs), and the viewport/
            // scrollbar/BACK still ride inside it as children so the whole
            // block still hides/shows/relayouts as one unit -- only the
            // painted box that used to fill this rect is gone. A Panel with
            // no Image emits no graphic (Ui.Panel's own EmitsNoGraphic), so
            // it takes no UiAudit overlap check against its neighbours
            // whatever height it is built or resized to, and the header
            // above it reads directly against the battlefield backdrop, the
            // same way the verb column's own labels already do a few pixels
            // to its left -- no separate legibility fix needed.
            var frameSize = new UiVec(FightSubmenuLayout.FrameWidth, FightSubmenuLayout.FrameHeight);

            var frameChildren = new List<UiNode> { viewport, track, thumb, back };

            var frameNode = Ui.Panel("SubmenuContainer", Place.At(containerX, FightSubmenuLayout.FrameCentreY),
                UiSize.Fixed(frameSize), frameChildren);
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
        // 2026-09-23 PROPORTION FIX (owner playtest, "way out of
        // proportion"): 460 was sized for the three-line body-text card the
        // icon rework (this file's own DetailHFor header) already dropped --
        // the icon rows/value text this card actually draws need a fraction
        // of that width, and unlike DetailH (a pure function of row count
        // since pass 2) DetailW never shrank to match, so the box hugged its
        // content on one axis and swam in dead space on the other. 260 is
        // DetailContentLeft's own 22px pad, the widest icon+gap+value row
        // (28 + 8 + up to ~11 characters at 14pt, "10 STAMINA"-shaped), and
        // the same pad mirrored on the right -- HUGS, does not merely
        // shrink: recompute if a longer value string is ever authored.
        // Grows toward the battlefield centre (DetailX is anchored off the
        // screen's own right edge, same as always), so a collision this
        // opens is with the enemy rack, not with anything to its right --
        // UiAudit's four-aspect pass is what would catch it, and the fix is
        // to move the column, never to AllowOverlap it (see BuildDetailColumn's
        // own header).
        private const float DetailW = 260f;
        private const float DetailRightMargin = 40f;
        private const float DetailBottomMargin = 40f;
        private static float DetailX => (960f - DetailRightMargin) - DetailW * 0.5f;

        // ITS OWN HEIGHT RULE, summing what the CONTENT needs directly --
        // top pad, name+kind, the icon rows at their own pitch, bottom pad.
        //
        // 2026-09-23 ICON REWORK: the body text and its 64px minimum plus
        // 20px gap are GONE (owner's playtest ask, "remove the flavor/
        // description body from the card") -- a card that used to reserve
        // room for three lines of prose plus eight text rows now reserves
        // room for at most six compact icon rows and nothing else, which is
        // most of this box's own "dynamic, not a fixed tall rect" ask by
        // itself. STILL BUILT AT THE POOL'S FULL SIX ROWS, though (the
        // static tree's own reservation, same reason FightSubmenuLayout's
        // BuildReservationRows stays at the pool size rather than the
        // typical case) -- a true per-hover resize (this flat Solid+Rim
        // fill has no aspect-locked art in the way FightSubmenuLayout's
        // Container did, so it is SAFE to add, unlike that one) is a real
        // follow-up this pass ran out of room for; see this file's own
        // report for that open call.
        private const float DetailTopPad = 18f;
        private const float DetailBottomPad = 18f;
        private const float DetailNameBlockH = 46f;
        private const float DetailIconRowPitch = 36f;

        // The icon and its value, side by side in one row -- 28px square
        // badge (the generated Icons/Ability/*.png art is a 64px source, so
        // this reads crisp at up to ~2.3x before the importer's own mipmap
        // chain has to make anything up) plus an 8px gap and whatever width
        // the row has left for its value text.
        public const float DetailIconSize = 28f;
        public const float DetailIconValueGap = 8f;

        // THE HERO ICON (owner playtest, "the skill icon sized and anchored
        // as the card's hero, beside the title" -- the DetailIconKind.Element
        // row is the only one that identifies WHAT the card is describing
        // rather than a cost/limit on casting it, the same reason
        // FillDetailIcons already special-cases it (one type per skill, at
        // most). It is NOT a new node: RefreshDetail (FightController.Hud.cs)
        // finds whichever pool slot holds the Element icon for the current
        // hover and repositions/resizes THAT slot, so
        // FightSkillDetailElementIconTests' "DetailIcon{index}, active,
        // sprited from element_*" contract is untouched -- only where and
        // how big it paints changes. A skill with no Element row (Mend, a
        // plain heal) shows no hero at all; the title sits back at its
        // un-indented left margin (DetailNameLeftFor(false)).
        public const float DetailHeroIconSize = 36f;
        private const float DetailHeroGap = 10f;

        // A PURE LAYOUT FUNCTION (coordinator ask, 2026-09-23 pass 2 --
        // "a dynamic box around it" means resized at runtime, not just built
        // smaller once): how tall the card is for N VISIBLE icon rows,
        // clamped to the pool -- the same ContainerHeightFor(count) shape
        // FightSubmenuLayout already uses for its own runtime-resized frame.
        // FightController.Hud.cs's RefreshDetail calls this every repaint
        // with panel.Icons.Count and writes the result onto the built
        // fill/rim/column rects directly (DetailColumnFill/DetailColumnRim),
        // the same "Domain computes, Core paints" split AnchorSubmenuRows
        // already follows.
        public static int DetailVisibleIconRows(int count)
        {
            if (count < 0) return 0;
            return count > FightHudSpec.DetailIconRows ? FightHudSpec.DetailIconRows : count;
        }

        public static float DetailHFor(int count) =>
            DetailTopPad + DetailNameBlockH
            + DetailVisibleIconRows(count) * DetailIconRowPitch + DetailBottomPad;

        // THE STATIC TREE'S OWN RESERVATION -- built at the pool's full six
        // rows, same reason FightSubmenuLayout.BuildReservationRows keeps a
        // static tree at its pool size rather than the common case: it is
        // never what the player actually sees (RefreshDetail resizes the
        // real card down to however many rows the FIRST hovered panel
        // shows, before it is ever drawn), so it is pure build-time plumbing.
        private static float DetailH => DetailHFor(FightHudSpec.DetailIconRows);

        // BOTTOM-ANCHORED AT A FIXED SCREEN MARGIN -- shrinking the card for
        // a short list moves only its TOP, never this bottom edge, which is
        // the edge nearest the enemy rack/party plates this column sits
        // beside (DetailX/DetailW's own header: "a collision this opens is
        // with the enemy rack"). Growing from the CENTRE instead would push
        // this bottom edge down just as often as it pulls the top up, which
        // is exactly the "fixed tall rect" feel the owner's ask was against.
        private const float DetailFixedBottom = -(540f - DetailBottomMargin);

        public static float DetailCentreYFor(int count) => DetailFixedBottom + DetailHFor(count) * 0.5f;

        private static float DetailY => DetailCentreYFor(FightHudSpec.DetailIconRows);

        // NO BLEED CORRECTION any more -- panel_violet's 9-slice transparent
        // margin is gone with the sprite itself (see BuildDetailColumn's own
        // header); a flat fill plus a hairline Rim draws exactly on the rect
        // it is given, so the frame IS the rect on every edge.
        //
        // LOCAL (BOX-RELATIVE) COORDINATES below, all count-aware, all pure
        // functions of DetailHFor(count) -- FightController.Hud.cs's
        // RefreshDetail calls these every repaint to reposition the fill,
        // the four Rim edges, DetailName/DetailKind and each active icon row,
        // the same "Domain computes the local Y, Core writes it onto the
        // RectTransform" split FightSubmenuLayout's own Y functions already
        // follow for the submenu. The STATIC tree below is built at
        // FightHudSpec.DetailIconRows (the pool's own ceiling) by calling
        // these with that same count, so the built scene and the first
        // runtime repaint can never disagree about where anything sits.
        private static float DetailFrameTop => DetailFrameTopFor(FightHudSpec.DetailIconRows);
        private static float DetailFrameBottom => DetailFrameBottomFor(FightHudSpec.DetailIconRows);

        public static float DetailFrameTopFor(int count) => DetailHFor(count) * 0.5f;
        public static float DetailFrameBottomFor(int count) => -DetailHFor(count) * 0.5f;

        // Name/kind sit a fixed distance below the box's own (count-aware)
        // top edge -- they move DOWN, toward the fixed bottom, as the box
        // shrinks for a shorter list, the same way FightSubmenuLayout.
        // HeaderYFor tracks FrameTopFor(count) for the submenu's own title.
        public static float DetailNameYFor(int count) => DetailFrameTopFor(count) - DetailTopPad - 12f;
        public static float DetailKindYFor(int count) => DetailFrameTopFor(count) - DetailTopPad - 32f;

        // THE ROW COUNT DetailHFor/DetailIconRowYFor SHOULD SIZE AROUND --
        // the hero icon (see DetailHeroIconSize's own header) sits in the
        // header block beside the title, not in the row stack, so it must
        // not reserve a row of its own or the box grows one pitch (36px)
        // taller than its rows actually need. RefreshDetail is the only
        // caller: it works out `shown` (panel.Icons.Count) and whether one
        // of those icons is the hero, and passes THIS, never `shown`
        // directly, to every count-aware function below.
        public static int DetailListRowCountFor(int shown, bool hasHero) =>
            hasHero ? (shown > 0 ? shown - 1 : 0) : shown;

        // The content column's own left edge/width, pulled out of
        // BuildDetailColumn's local `left`/`contentW` so RefreshDetail can
        // reuse the identical numbers when it repositions the hero icon and
        // the name/kind block around it (single source, same reason
        // DetailX/DetailY are properties rather than inlined at each call
        // site).
        public static float DetailContentLeft => -DetailW * 0.5f + 22f;
        public static float DetailContentW => DetailW - 44f;

        // Name/kind make room for the hero icon by starting further right
        // and losing that same width -- ONLY when a hero is actually
        // painted (DetailNameLeftFor(false)/DetailNameWidthFor(false) are
        // the plain, un-indented values BuildDetailColumn already built the
        // static tree at, so a skill with no Element row, like a plain
        // heal, never reserves hero space it does not use).
        public static float DetailNameLeftFor(bool hasHero) =>
            hasHero ? DetailContentLeft + DetailHeroIconSize + DetailHeroGap : DetailContentLeft;
        public static float DetailNameWidthFor(bool hasHero) =>
            hasHero ? DetailContentW - DetailHeroIconSize - DetailHeroGap : DetailContentW;

        // The hero icon's own local position: pinned to the content
        // column's left edge (same X every other icon row uses) and
        // centred across the name+kind block's own vertical span, so it
        // reads as one header unit with the title rather than a row that
        // happens to be bigger.
        public static float DetailHeroIconXFor() => DetailContentLeft + DetailHeroIconSize * 0.5f;
        public static float DetailHeroIconYFor(int listRowCount) =>
            (DetailNameYFor(listRowCount) + DetailKindYFor(listRowCount)) * 0.5f;

        // Row `index`'s local Y for a card showing `count` icons, packed
        // TIGHT under the name block with no gap and no dead space above the
        // bottom pad -- DetailHFor(count) was sized to fit EXACTLY
        // DetailVisibleIconRows(count) rows at this pitch, so bottom-
        // anchoring within the CURRENT (not the static/pool) box always
        // lands the last row's own bottom exactly DetailBottomPad above this
        // count's own bottom edge.
        public static float DetailIconRowYFor(int count, int index)
        {
            int shown = DetailVisibleIconRows(count);
            float statBottom = DetailFrameBottomFor(count) + DetailBottomPad;
            return statBottom + (shown - 1 - index) * DetailIconRowPitch;
        }

        // A PERSISTENT third column rather than a hover tooltip: arrowing
        // through options lets the player compare without re-hovering, and a
        // tooltip that vanishes the moment the cursor moves cannot be compared
        // against anything.
        // A DARK, NEAR-OPAQUE PLATE, not panel_violet -- the owner's original
        // ask was legibility over ornament, and the 2026-09-23 icon rework
        // keeps the same call ("keep it dark and wide as the last commit
        // made it") even though the body text that first justified it is
        // gone. #1D1226F2 is the same near-black-plum the kit's OWN tooltip
        // already uses (CharacterDossierScreen.BuildTooltip, ReckoningScreen's
        // offer tooltip), so this is the kit's existing "read this" surface,
        // not a new one invented for this card. The hairline Rim
        // (FightHudPalette.Hairline) replaces the ornate frame's own border --
        // no darker panel sprite exists under Art/UI/Panels for this card's
        // aspect, so there is no frame art to keep (see this method's own
        // header elsewhere for the Glob that confirmed it).
        private const string DetailFillHex = "#1D1226F2";

        // A PERSISTENT third column rather than a hover tooltip: arrowing
        // through options lets the player compare without re-hovering, and a
        // tooltip that vanishes the moment the cursor moves cannot be compared
        // against anything.
        //
        // THE OUTER NODE STAYS A PLAIN (non-Decor) Panel, not Ui.Tooltip's
        // AsDecor ground -- Ui.Tooltip marks its whole subtree Decor, which
        // UiAudit's sibling-overlap check (A1) skips entirely, and this card
        // is the one place that check has to stay live: it is what would
        // catch the wider column reaching into the enemy rack, and the model
        // rule for that failure is "move it", never "exempt it" (see
        // DetailW's own comment). Only the flat fill and the Rim border are
        // marked Decor -- they are paint, not content, the same split
        // Options/Party/RunStats already draw their own bordered cards with.
        private UiNode BuildDetailColumn()
        {
            float contentW = DetailContentW;
            float left = DetailContentLeft;
            const int staticCount = FightHudSpec.DetailIconRows;

            var name = Ui.Label("DetailName", UiString.Runtime, new UiVec(contentW, 22f), 18,
                FightHudPalette.TextPrimary, Place.At(left, DetailNameYFor(staticCount), new UiVec(0f, 0.5f)));
            var kind = Ui.Label("DetailKind", UiStrings.DetailKindSkill, new UiVec(contentW, 14f), 11,
                FightHudPalette.GoldLight, Place.At(left, DetailKindYFor(staticCount), new UiVec(0f, 0.5f)));

            DetailName = name;
            DetailKind = kind;

            var fill = Ui.Solid("DetailColumnFill", DetailFillHex, new UiVec(DetailW, DetailH),
                Place.At(0f, 0f)).AsDecor();
            DetailColumnFill = fill;

            var children = new List<UiNode> { fill, name, kind };
            var rim = Ui.Rim("DetailColumn", new UiVec(DetailW, DetailH), FightHudPalette.Hairline).ToList();
            children.AddRange(rim);
            foreach (var edge in rim) DetailColumnRim.Add(edge);

            // ONE ICON POOL, NOT EIGHT TEXT KEYS (2026-09-23 icon rework).
            // FightHudModel.DetailPanel.Icons is COMPACT -- only the facts
            // that apply to whatever is hovered, in DetailIconKind's own
            // canonical order (FillDetailIcons) -- same "omit rather than
            // pad" rule the old Stats row pool followed. FightController.
            // Hud.cs's RefreshDetail sets row i's sprite (by IconKey, via
            // Resources.Load), value text AND its LOCAL Y (DetailIconRowYFor,
            // count-aware -- the pass-2 "dynamic box" rework) from panel.
            // Icons[i] for i < Count, and hides row i entirely past it -- the
            // same fixed-pool/toggle-visibility idiom FightSubmenuLayout's
            // rows and the old text-row pool this replaces both already use.
            for (int i = 0; i < FightHudSpec.DetailIconRows; i++)
            {
                float y = DetailIconRowYFor(staticCount, i);

                var icon = Ui.Sprite($"DetailIcon{i}", null, new UiVec(DetailIconSize, DetailIconSize),
                        Place.At(left + DetailIconSize * 0.5f, y, new UiVec(0.5f, 0.5f)))
                    .Inactive();

                float valueLeft = left + DetailIconSize + DetailIconValueGap;
                var value = Ui.Label($"DetailIconValue{i}", UiString.Runtime,
                        new UiVec(DetailW * 0.5f - 22f - valueLeft, DetailIconSize), 14,
                        FightHudPalette.GoldLight, Place.At(valueLeft, y, new UiVec(0f, 0.5f)))
                    .TextAligned(UiTextAlign.Left);

                DetailIconImages.Add(icon);
                DetailIconValues.Add(value);
                children.Add(icon);
                children.Add(value);
            }

            var column = Ui.Panel("DetailColumn", Place.At(DetailX, DetailY),
                UiSize.Fixed(DetailW, DetailH), children.ToArray());
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

        // ---- the status box -----------------------------------------------------
        //
        // EVERY ACTIVE STATUS ON ONE ACTOR, hung under that actor. The owner's
        // 2026-09-19 finding, in their words: "the debuff hover is still on the
        // old container and it is too small, it should bring up a clear box
        // under the hovered mob."
        //
        // WHAT IT REPLACES, and why this is a different object rather than a
        // bigger one. StatusTooltip was a 300x56 panel placed BESIDE one 36px
        // badge, showing that one badge's line. Three things follow from
        // "beside one badge", and no width fixes any of them: it answers for
        // one status when the question is "what is on this monster", it lands
        // wherever there happens to be room rather than where the player is
        // looking, and a pad cannot hover a badge at all -- so on a pad the
        // feature did not exist.
        //
        // THE SOURCE OF TRUTH IS UNCHANGED. A row's text is the same
        // FightHudModel.StatusRow.Tooltip the badge hover showed, so the name,
        // the one-line effect and the remaining duration are still written in
        // one place (Domain/Combat/Session/StatusHud.cs) and this screen only
        // lays them out beside the icon the badge already resolves.
        //
        // EIGHT ROWS is PLAN_STATUS_EFFECT_UI section 6's counted worst case
        // (a party member under Poison, Vulnerable, Shielded, Regen, Protect,
        // a speed entry, Stun and Empowered); a ninth becomes a "+N more"
        // line in the last row, the same overflow rule the badge rows use.
        public const int StatusBoxRows = 8;

        public const float StatusBoxWidth = 460f;
        public const float StatusBoxPad = 12f;
        public const float StatusBoxIconSize = 24f;
        public const float StatusBoxIconGap = 8f;
        public const float StatusBoxRowGap = 4f;
        public const int StatusBoxFontSize = 13;

        // A row is one line of text and grows to two when a long entry wraps
        // (Provoked's is the longest authored one). Beyond two it clips
        // rather than pushing the box taller than the min height eight of
        // them were measured against.
        public const float StatusBoxRowMinHeight = 26f;
        public const float StatusBoxRowMaxHeight = 40f;

        // Clear air between the actor and the box, matching
        // TooltipPlacement.DefaultGap so every floating panel on this screen
        // stands off its subject by the same distance.
        public const float StatusBoxGap = TooltipPlacement.DefaultGap;

        public const float StatusBoxTextWidth =
            StatusBoxWidth - StatusBoxPad * 2f - StatusBoxIconSize - StatusBoxIconGap;

        // THE BUILD-TIME SIZE IS THE WORST CASE, not the common one -- the
        // opposite choice from the tooltip this replaces, and for a reason
        // that changed with the object. A tooltip built tall sat as empty
        // violet under its common one-line body, so it was built short and
        // grown; this box is positioned from its own measured height every
        // time it opens (FightController.Hud's RefreshStatusBox), so its
        // declared size is never a size anyone sees. What the declared size
        // IS for is UiAudit, which solves this screen with no statuses in
        // hand: eight full rows is the largest box the runtime can produce,
        // so auditing that one audits every smaller one.
        public const float StatusBoxMaxHeight =
            StatusBoxPad * 2f + StatusBoxRows * StatusBoxRowMaxHeight + (StatusBoxRows - 1) * StatusBoxRowGap;

        // Rows stack from the TOP of the box: `usedAbove` is how much row and
        // gap has already been spent below the top pad. Shared by the build
        // (every row at its maximum height) and by the runtime (every row at
        // its measured one), so the two cannot disagree about where row i
        // sits.
        public static float StatusBoxRowCentreY(float boxHeight, float usedAbove, float rowHeight) =>
            boxHeight * 0.5f - StatusBoxPad - usedAbove - rowHeight * 0.5f;

        // WHERE THE BOX SITS, given the actor it describes. Pure, so
        // FightScreenTests walks it against this screen's real geometry at
        // every audited frame without a scene.
        //
        // UNDER THE ACTOR BY PREFERENCE, FLIPPED ABOVE WHEN IT WOULD NOT FIT.
        // "Under" is what the owner asked for and is also where a box belongs
        // when the thing above it is what named it. The flip is reachable,
        // not defensive: the rear enemy slot's badge row bottoms out around
        // y -203 and a full eight-row box needs 186 below that plus the gap,
        // which is past the floor of a 1920x1080 canvas. 16:9 is the binding
        // frame here and 4:3 is not -- every audited frame is at least
        // 1920x1080 and 4:3 adds HEIGHT (UiFrames.cs), so the shortest canvas
        // is the one that flips first.
        //
        // X FOLLOWS THE ACTOR and is only clamped to keep the box on screen,
        // which is what makes "this box belongs to that monster" readable
        // without a leader line: the box is centred under the figure.
        public static UiVec StatusBoxAt(float actorCentreX, float actorBottom, float actorTop,
                                        float boxWidth, float boxHeight,
                                        float interiorLeft, float interiorRight,
                                        float interiorBottom, float interiorTop,
                                        float gap = StatusBoxGap)
        {
            float halfW = boxWidth * 0.5f;
            float halfH = boxHeight * 0.5f;

            float x = ClampOrCentre(actorCentreX, interiorLeft + halfW, interiorRight - halfW);

            float below = actorBottom - gap - halfH;
            if (below - halfH >= interiorBottom) return new UiVec(x, below);

            float above = actorTop + gap + halfH;
            if (above + halfH <= interiorTop) return new UiVec(x, above);

            // Neither side of the actor clears the canvas -- a box taller than
            // the room above and below it. Pushed inside and overlapping the
            // figure, stated here rather than left to happen: covering the
            // monster is worse than standing off it and better than hanging
            // half off the screen.
            return new UiVec(x, ClampOrCentre(below, interiorBottom + halfH, interiorTop - halfH));
        }

        // A box wider or taller than the interior makes the two bounds cross,
        // and min/max in that order would pin it to the wrong edge. Centring
        // is the only sensible answer to "it does not fit" -- the same rule
        // TooltipPlacement's own clamp states, restated here rather than
        // reached for, because that one is private to a type this screen
        // otherwise only borrows a constant from.
        private static float ClampOrCentre(float value, float min, float max)
        {
            if (min > max) return (min + max) * 0.5f;
            if (value < min) return min;
            return value > max ? max : value;
        }

        private UiNode BuildStatusBox()
        {
            var rows = new List<UiNode>(StatusBoxRows * 2);

            float iconX = -StatusBoxWidth * 0.5f + StatusBoxPad + StatusBoxIconSize * 0.5f;
            float textX = -StatusBoxWidth * 0.5f + StatusBoxPad + StatusBoxIconSize + StatusBoxIconGap
                          + StatusBoxTextWidth * 0.5f;

            for (int i = 0; i < StatusBoxRows; i++)
            {
                float y = StatusBoxRowCentreY(StatusBoxMaxHeight,
                    i * (StatusBoxRowMaxHeight + StatusBoxRowGap), StatusBoxRowMaxHeight);

                // No sprite key: the glyph is resolved per status at runtime
                // off the row's own slug, exactly as the badges do, and the
                // Image is disabled outright on a miss so a sprite-less Image
                // never renders as a white quad.
                var icon = Ui.Sprite($"StatusBoxIcon{i}", null,
                    new UiVec(StatusBoxIconSize, StatusBoxIconSize), Place.At(iconX, y));

                // TextPrimary, which is also what the controller paints an
                // ordinary row (FightController.Hud's StatusBoxRowText): the
                // baked colour is never seen -- the box is Inactive until a
                // paint has run -- and two different answers to "what colour
                // is a row" is the restatement CODE_STANDARDS section 6 is
                // about.
                var text = Ui.Label($"StatusBoxText{i}", UiString.Runtime,
                        new UiVec(StatusBoxTextWidth, StatusBoxRowMaxHeight), StatusBoxFontSize,
                        FightHudPalette.TextPrimary, Place.At(textX, y))
                    .TextAligned(UiTextAlign.Left);

                StatusBoxIcons.Add(icon);
                StatusBoxTexts.Add(text);
                rows.Add(icon);
                rows.Add(text);
            }

            // Place.At here is a placeholder, like the tooltip's was: the box
            // is positioned from the actor every time it opens.
            var panel = Ui.Tooltip("StatusBox", PanelViolet, null, Place.At(-330f, -60f),
                UiSize.Fixed(StatusBoxWidth, StatusBoxMaxHeight), rows.ToArray());
            StatusBox = panel;
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
