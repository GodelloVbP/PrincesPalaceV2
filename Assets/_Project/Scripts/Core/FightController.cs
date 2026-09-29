using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The battle screen's view.
    //
    // What is NOT here is the point: no damage funnel, no enemy AI, no turn
    // riders, no skill dispatch, no talent rules, no reward assembly. All of
    // that resolved into Domain's FightSession, which is why v1's 7,073-line
    // ten-partial controller does not need to be reproduced here. This holds
    // references, paints them from session queries, and forwards clicks.
    //
    // Bound by direct field assignment from ScreenRegistry.Fight(), so a
    // mistyped name is a compile error rather than a silently null field --
    // v1 wired 330 of these through reflection over strings.
    // IFightNavigationTarget is implemented explicitly, in FightController.Input.cs
    // -- MoveFocus/ConfirmFocus/OnBackPressed themselves stay public/private
    // exactly as before, called directly by every existing test; see that
    // file for why the interface exists at all.
    public partial class FightController : MonoBehaviour, IFightNavigationTarget
    {
        // ---- the stage ---------------------------------------------------------

        [SerializeField] internal Image backgroundImage;
        [SerializeField] internal Sprite normalBackground;
        [SerializeField] internal Sprite eliteBackground;
        [SerializeField] internal Sprite bossBackground;

        [SerializeField] internal RectTransform[] enemySlots;
        [SerializeField] internal Image[] enemySprites;
        [SerializeField] internal StageHitFlash[] enemyHitFlashes;
        [SerializeField] internal TMP_Text[] enemyNameplates;
        [SerializeField] internal Image[] enemyFootShadows;

        // The per-slot lunge/recoil animator and death fade -- the same shape
        // stageShakes and spellVfxPlayers below already use: attached and
        // stored here at scene-build time (ScreenRegistry.Fight) rather than
        // resolved with GetComponent at the point of use. SlotFor's own
        // comment explains why the view owns this mapping; AnimatorFor and
        // DeathFadeFor below are its exact twins for these two components.
        [SerializeField] internal StageActorAnimator[] enemyActorAnimators;
        [SerializeField] internal StageDeathFade[] enemyDeathFades;

        [SerializeField] internal RectTransform[] partySlots;

        // The two stage racks' shakers. Both kick together: a blow shakes the
        // stage, not the half of it the victim happens to stand on -- one side
        // jolting while the other holds still reads as a bug in the layout.
        [SerializeField] internal StageShake[] stageShakes;
        [SerializeField] internal Image[] partySprites;

        // The planted shield / Shieldwall prop, one per party slot, a child of the
        // slot (FightController.ShieldProps.cs).
        [SerializeField] internal Image[] partyShieldProps;
        [SerializeField] internal StageHitFlash[] partyHitFlashes;
        [SerializeField] internal TMP_Text[] partyNameplates;
        [SerializeField] internal StageActorAnimator[] partyActorAnimators;
        [SerializeField] internal StageDeathFade[] partyDeathFades;
        [SerializeField] internal Image[] partyFootShadows;

        // ---- the HUD -----------------------------------------------------------

        [SerializeField] internal Image[] initiativeIcons;
        [SerializeField] internal Image[] initiativeRings;
        [SerializeField] internal TMP_Text[] initiativeLabels;

        [SerializeField] internal Image barkPortrait;
        [SerializeField] internal TMP_Text barkLabel;

        [SerializeField] internal TMP_Text enemiesHint;
        [SerializeField] internal Button[] enemyPlates;

        // The Crimson TwoByOne container frame's own Art Image.
        // RefreshEnemyPlates tints THIS for
        // the elite/boss dress and the out-of-reach dim -- enemyPlates is
        // NoChrome now (a transparent, always-alpha-0 click target, see
        // FightScreen.BuildEnemyPlates), so its own targetGraphic is no
        // longer the visible plate.
        [SerializeField] internal Image[] enemyPlateFrames;
        [SerializeField] internal Image[] enemyPlateIcons;
        [SerializeField] internal TMP_Text[] enemyPlateNames;

        // A click target over each enemy FIGURE, live only while a mark is
        // being chosen -- see FightScreen.BuildStage for why it is a separate
        // node rather than the sprite.
        [SerializeField] internal Button[] enemyHitAreas;

        // The same over each PARTY figure, live only while an ALLY is being
        // picked. INDEXED BY STAGE SLOT, which on this side is
        // not the party list's order -- a slot belongs to one character for
        // the whole fight and the list reorders on every Move, so the handler
        // resolves a slot to its occupant rather than indexing the party by
        // it. See FightScreen.PartyHitAreas.
        [SerializeField] internal Button[] partyHitAreas;

        // One per party SEAT (0 = front), live only for an EMPTY seat while a
        // seat pick is open (Palace Passage onto any seat, PLAN_BELLWETHER_KIT
        // 1.2/3.6). See FightScreen.PartySeatMarkers.
        [SerializeField] internal Button[] partySeatMarkers;

        // The way out of targeting. It lives on the target prompt rather
        // than the list, because the list folds away as soon as a skill is
        // picked.
        [SerializeField] internal Button targetCancelButton;
        [SerializeField] internal TMP_Text[] enemyPlateHps;
        [SerializeField] internal Image[] enemyPlateHpFills;
        [SerializeField] internal TMP_Text[] enemyPlateTags;
        [SerializeField] internal GameObject[] enemyPlateReticles;
        [SerializeField] internal GameObject[] enemyPlateBreakTracks;
        [SerializeField] internal Image[] enemyPlateBreakFills;
        [SerializeField] internal GameObject secondLifeBadge;

        // ---- the HUD column: three PC plates --------------------
        //
        // ONE ARRAY SHAPE FOR THE WHOLE COLUMN. Every one of these is
        // PcPlateCount long and indexed by PLATE, so the paint routine is one
        // loop over three identical surfaces rather than a party path and a
        // roster path that had to be kept in step by hand. What replaced:
        // partyName/partyHpFill/partyHpValue/partyMpFill/partyMpValue/
        // partyMpTag/partyMpShade/partyMpRims/partyCardRims/partyBuffIcons/
        // woolPips/woolValue and the whole roster* family.
        //
        // Bound by FIELD NAME off FightScreen's NodeRef lists of the same
        // name -- UiAutoBind matches an Image[]/TMP_Text[]/GameObject[] to a
        // list called the same thing with no ScreenRegistry code, and
        // UiWiringSweep refuses the build outright if one comes back null or
        // empty.
        // BUTTONS SINCE AUDIT #147, where these were GameObjects: a
        // single-ally cast is confirmed by clicking the squadmate, on the
        // plate that already IS that squadmate. Interactable ONLY while an
        // ally pick is open -- outside one a live plate would be a button
        // that silently does nothing, which is the state the enemy plates'
        // own `blocked` handling exists to avoid.
        [SerializeField] internal Button[] pcPlates;

        // The ally-pick marker, one per plate. Shown only during an ally
        // pick and greyed on a plate this cast will not accept -- the mirror
        // of enemyPlateReticles, painted by the same rule.
        [SerializeField] internal GameObject[] pcPlateReticles;

        // The leather itself. THE identity surface: swapped at runtime to the
        // occupant's own characters.json plateArt through PcPlateSprites,
        // because which character sits in which slot is decided per
        // encounter and cannot be baked.
        [SerializeField] internal Image[] pcPlateArts;

        // The acting halo -- shown on exactly one plate, tinted with that
        // occupant's PcTheme.Rim. The plate's corner is rounded, so this is
        // an art-shaped decal rather than four rim Images; see
        // Domain/UiKit/PcPlateArt.GlowKey for why.
        [SerializeField] internal Image[] pcPlateHighlights;

        // THE IDENTITY RIM, worn for the rest of a career rather than for a
        // turn: the same glow decal at a third of the halo's pad, tinted by
        // whichever PlateRim node the character has collected. A second decal
        // rather than a state on the halo above, because a rim earned at
        // level 32 and "it is your go" are different facts and a surface that
        // said both would say neither.
        [SerializeField] internal Image[] pcPlateRims;

        [SerializeField] internal TMP_Text[] pcNames;

        // The line under the bars: the chosen title, then VICTOR, then
        // MASTER. Hidden outright for a character with nothing collected,
        // which is every character below level 31.
        [SerializeField] internal TMP_Text[] pcIdentityLines;

        [SerializeField] internal TMP_Text[] pcSignatures;
        [SerializeField] internal TMP_Text[] pcHpValues;
        [SerializeField] internal Image[] pcHpFills;
        [SerializeField] internal TMP_Text[] pcMpValues;
        [SerializeField] internal Image[] pcMpFills;

        // THE SECOND METER'S OWN SURFACES (plan P7). Ui.Meter bakes its
        // colours into the scene, so a meter that draws whatever pool its
        // holder carries has to be repainted at runtime -- the fill is not
        // enough, the band under it and the four rim edges carry the colour
        // too. pcMpRims is CARD-MAJOR: plate i's edges are i*RimsPerCard..+3.
        [SerializeField] internal Image[] pcMpShades;
        [SerializeField] internal Image[] pcMpRims;

        // Five per plate, CARD-MAJOR.
        [SerializeField] internal GameObject[] pcStatusBadges;

        private const int RimsPerCard = 4;

        [SerializeField] internal GameObject lowHpVignette;

        // The enemy row (3 stage slots x 5 badges, slot-major) FightScreen.
        // BuildEnemyStatusRows declared -- see FightController.Hud.cs's
        // RefreshEnemyStatusRows for how the flat index maps back to a
        // stage slot. The PC column's own badges are pcStatusBadges above.
        [SerializeField] internal GameObject[] enemyStatusBadges;

        // One backing strip per enemy stage slot, hidden outright when that
        // slot's row has nothing to show (PLAN_STATUS_EFFECT_UI.md section
        // 7) -- the roster and party surfaces sit on their own painted
        // plates already and were never given a strip of their own.
        [SerializeField] internal GameObject[] enemyStatusStrips;

        // THE ONE STATUS BOX, hung under whichever actor is being inspected
        // and listing every status on it, one row per status --
        // FightScreen.BuildStatusBox for the shape and for what it replaced
        // (StatusTooltip/StatusTooltipText, the per-badge tooltip, removed
        // outright: it answered for one badge, landed beside that badge, and
        // a pad could not reach it at all).
        [SerializeField] internal GameObject statusBox;
        [SerializeField] internal Image[] statusBoxIcons;
        [SerializeField] internal TMP_Text[] statusBoxTexts;

        // ---- status badges: consts and runtime state (S9's review) ---------------
        //
        // Moved here from FightController.Hud.cs, which built and paints
        // this system but is a PART file -- docs/CODE_STANDARDS.md "Partial classes"
        // reserves consts and runtime state for the root file of a
        // MonoBehaviour partial class, methods only for its parts, because
        // the scene/inspector GUID binding is anchored to this file alone.
        // See FightController.Hud.cs for how every one of these is used.

        // Mirrors FightScreen's own EnemyStatusBadgesPerRow/
        // PcStatusBadgesPerPlate (section 1's measured table) -- Package C
        // codes against the node NAMES that registry produces, not against
        // its private layout constants, so these are a second statement of
        // the same "5 slots per row" fact rather than a shared one.
        private const int EnemyStatusBadgesPerRow = 5;
        private const int PcStatusBadgesPerPlate = 5;
        private const int EnemyStatusBadgeCount = 15; // 3 stage slots x 5

        // The column is three identical plates of five, so there is ONE flat
        // range covering the whole party rather than two ranges with
        // different widths that PaintStatusRow would have to be told about
        // separately. The overflow chip is still the last slot of each
        // plate's five.
        private const int PcStatusBadgeCount = PcPlateCount * PcStatusBadgesPerPlate;
        private const int PcPlateCount = 3;

        private StatusBadgeParts[] _statusBadgeParts;

        // WHICH STATUS EACH PHYSICAL BADGE SLOT IS SHOWING, as of the last
        // paint, or null for a slot showing nothing (and for the "+N" chip,
        // which stands for several). It was the badge's whole tooltip TEXT
        // until the status box replaced the per-badge tooltip; the box builds
        // its own text from the holder's rows, so what a badge still has to
        // carry is only which row of that list it is -- see _inspectedCode.
        private readonly string[] _statusBadgeCode = new string[TotalStatusBadges];

        // Which CODES were actually painted for each HOLDER last refresh --
        // keyed by the CombatantState itself, not by the physical row's
        // baseIndex (C4's review). The party plate and roster rows change
        // OCCUPANT every turn (whoever is acting swaps out of the roster and
        // into the plate), so a row-keyed set called a status "new" every
        // time its holder changed seats even though nothing about the status
        // itself changed -- a long-standing Regen popped its appearance
        // animation on every single turn transition. Keying by holder instead
        // means "new" only ever means what section 6/D7 actually asks for: a
        // status newly applied to THIS combatant, never a seat
        // change (S10's review deletes "genuinely" from this line). Cleared
        // in ResetStagePresentation, once per Bind -- a
        // holder from a PREVIOUS fight can never come back to be compared
        // against, so a fresh fight starts every combatant's set empty, same
        // as a fresh WireAllStatusBadges always did for a baseIndex.
        private readonly Dictionary<CombatantState, HashSet<string>> _statusRowActiveCodes =
            new Dictionary<CombatantState, HashSet<string>>();

        // C4/S5: the one scratch buffer PaintStatusRow fills a holder's
        // CURRENT codes into before comparing/committing, so no call
        // allocates a fresh HashSet<string> just to throw it away next
        // refresh -- see PaintStatusRow's own comment for how it's reused.
        private readonly HashSet<string> _statusRowScratchCodes = new HashSet<string>();

        // One appearance-pop coroutine per physical badge SLOT (flat index),
        // since the pop animates the slot's own RectTransform and two
        // overlapping tweens on the same node would fight over its scale.
        // A flat array, sized like its siblings (_statusBadgeParts,
        // _statusBadgeCode, _statusBadgeHolder) rather than a
        // Dictionary<int, Coroutine>
        // (S7's review) -- flat is a dense 0..TotalStatusBadges-1 range by
        // construction, so a Dictionary was paying hashing/boxing for what
        // is really just indexed storage every other per-badge array here
        // already uses.
        private readonly Coroutine[] _statusBadgePopRoutines = new Coroutine[TotalStatusBadges];

        // WHO EACH PHYSICAL BADGE SLOT BELONGS TO, as of the last paint --
        // the badge-hover path's answer to "which actor is being asked
        // about". Written only by PaintStatusRow, which is already handed
        // the holder for the appearance-pop's sake, so this is a second
        // reader of a fact the paint already had rather than a second way of
        // working it out.
        private readonly CombatantState[] _statusBadgeHolder = new CombatantState[TotalStatusBadges];

        // THE ACTOR THE POINTER IS ASKING ABOUT, or null. Set by every mouse
        // surface that names one combatant -- a status badge, an enemy
        // plate, an intent icon over a monster's head, a PC plate -- and
        // beaten by nothing: the pad's own focus is consulted only when this
        // is null (RefreshStatusBox). One field rather than one hover index
        // per surface, because the box's question is about an ACTOR and
        // every surface that can answer it already holds one.
        private CombatantState _inspectedActor;

        // The status code whose row the box draws emphasised, or null. Only
        // a badge hover sets one: the badge names a single status inside a
        // list the box shows whole, and without this the box would lose
        // which one was pointed at.
        private string _inspectedCode;

        // WHICH SURFACE OPENED THE BOX, so that only that surface can close
        // it again. Comparing ACTORS is not enough: walking the pointer along
        // one badge row is four surfaces naming the same monster, and Unity
        // delivers the arriving surface's enter before the departing one's
        // exit often enough that OnEnemyUnhovered already carries a comment
        // about it -- an actor-keyed guard would let the badge being LEFT
        // close the box the badge being ENTERED just opened.
        private enum InspectSource { None, StatusBadge, Enemy, AllyPlate }

        private InspectSource _inspectedFrom = InspectSource.None;
        private int _inspectedFromIndex = -1;

        // The Canvas statusBox sits under, resolved once by
        // WireAllStatusBadges rather than per hover (S8's review).
        private Canvas _statusBoxCanvas;

        // What each status-box row MEASURED to this paint. A row is one line
        // of text until its own line does not fit, so where row i sits
        // depends on every row above it and on the box's final height --
        // which is not known until they have all been measured. One reused
        // buffer rather than a fresh array per paint, the same reasoning
        // _statusRowScratchCodes carries.
        private readonly float[] _statusBoxRowHeights = new float[FightScreen.StatusBoxRows];

        // BUILD-TIME positions, captured once and never overwritten --
        // RefreshEnemyStatusRows re-derives each refresh's position as
        // home + delta, never by reading back wherever the row is CURRENTLY
        // sitting (which may already be shifted from a previous refresh at
        // a different enemy count). Same "compare against the recorded
        // home, not the live rect" rule AnchorOne's own comment gives
        // for why StageActorAnimator keeps a Home instead of reading the
        // rect back. Null-safe throughout: a screen with no status rows at
        // all (an older build, a test double) leaves these null and
        // RefreshEnemyStatusRows simply skips repositioning.
        private Vector2[] _enemyStatusStripHome;
        private Vector2[] _enemyStatusBadgeHome;

        [SerializeField] internal Button[] verbButtons;
        [SerializeField] internal TMP_Text[] verbLabels;
        [SerializeField] internal GameObject[] verbCarets;
        [SerializeField] internal TMP_Text breadcrumb;
        [SerializeField] internal Button continueButton;

        // The character sheet, mounted in this scene so it is reachable
        // mid-fight. Wired with lockedForFight set, so it reads and never
        // edits.
        [SerializeField] internal GameObject characterSheetPanel;

        // One per enemy stage slot. The TMP inside each is resolved at Start
        // rather than wired: the emitter synthesises a button's caption as a
        // child it has no NodeRef for, and inventing one here would be a second
        // way to name the same object (AUDIT.md #39's exact shape).
        [SerializeField] internal GameObject[] enemyIntentIcons;

        // Same slot index: the badge's expected damage and its telegraph
        // callout (FightScreen.EnemyIntentValues/Callouts).
        [SerializeField] internal TMP_Text[] enemyIntentValues;
        [SerializeField] internal TMP_Text[] enemyIntentCallouts;

        // The callout's backing plate, same slot index (FightScreen.
        // EnemyIntentCalloutPlates, M5).
        [SerializeField] internal GameObject[] enemyIntentCalloutPlates;

        // The knell's floor figures, one per party SEAT (FightScreen.
        // PartySeatFigures, PLAN_BELLWETHER_KIT M5): the plate and its text.
        [SerializeField] internal GameObject[] partySeatFigures;
        [SerializeField] internal TMP_Text[] partySeatFigureLabels;
        [SerializeField] internal GameObject intentTooltip;
        [SerializeField] internal TMP_Text intentTooltipText;

        [SerializeField] internal GameObject submenuColumn;
        [SerializeField] internal TMP_Text submenuTitle;
        [SerializeField] internal TMP_Text submenuHint;
        [SerializeField] internal Button submenuBackButton;
        [SerializeField] internal Button[] submenuRows;
        [SerializeField] internal RectTransform[] submenuRowRects;

        // The scrolling frame around those rows.
        [SerializeField] internal RectTransform submenuViewport;
        [SerializeField] internal RectTransform submenuContent;
        [SerializeField] internal RectTransform submenuScrollTrack;
        [SerializeField] internal RectTransform submenuScrollThumb;

        // The themed art frame around the whole box -- "SubmenuContainer" in
        // FightScreen.BuildSubmenuFrame. Wired so it can be moved at runtime
        // as the container grows to fit the row count (AnchorSubmenuRows'
        // own header).
        [SerializeField] internal RectTransform submenuContainer;

        // How far down the open list the player has scrolled, in pixels, and
        // how many rows that list holds. Held together because neither means
        // anything without the other -- the range is a function of the count.
        private float _submenuScroll;
        private int _submenuCount;

        // WHICH LIST THAT SCROLL BELONGS TO, so that a repaint can tell itself
        // apart from a new list.
        //
        // AnchorSubmenuRows is called from RefreshSubmenu, which runs on every
        // RefreshUi -- and RefreshUi runs on every hover, every plate, every
        // beat. It reset the scroll unconditionally, so the list snapped to the
        // top on all of them, and the wheel was the worst case rather than an
        // exception: scrolling slides the rows UNDER a stationary pointer, so
        // the notch itself changes which row is hovered and springs the list
        // straight back. The bar looked dead.
        //
        // A count and a branch, not a bool, because "the same list" has to mean
        // the same entries: ITEM's twelve potions and SKILL's twelve spells are
        // different lists that happen to be the same length. Cleared outright
        // when the column closes (ForgetSubmenuScroll), which is what keeps a
        // freshly opened menu starting at its first row.
        private int _submenuScrolledCount = -1;
        private MenuBranch _submenuScrolledBranch = MenuBranch.None;
        [SerializeField] internal TMP_Text[] submenuNames;

        // The SPELL submenu's per-row book art (FightScreen.BuildSubmenuColumn's
        // 36x36 `CharacterSkill{i}Mark`) -- auto-bound off the "SubmenuMarks"
        // NodeRef list the same way submenuNames above binds "SubmenuNames"
        // (UiAutoBind keys on the field's own identifier; no wiring line
        // needed in ScreenRegistry for this one). RefreshSubmenu paints it.
        [SerializeField] internal Image[] submenuMarks;

        // Skill book/glyph art, keyed by skill id -- baked in Fight()'s Wire
        // step the same way ShopController.skillArt is (see its own header),
        // not paired with any UI array the same length (a lookup table
        // filtered to whichever skills authored an iconPath), which is why
        // ScreenRegistry's own "NO CountBindings" comment sits beside the
        // assignment rather than here.
        [SerializeField] internal IconEntry[] skillArt;

        [SerializeField] internal GameObject detailColumn;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailKind;

        // THE DYNAMIC BOX ITSELF -- the
        // fill and the four Rim edges (Top/Bottom/Left/Right, Ui.Rim's own
        // yield order) RefreshDetail resizes/repositions every repaint via
        // FightScreen.DetailHFor/DetailFrameTopFor/DetailFrameBottomFor, so
        // the card is a pure function of the CURRENT panel's icon count, not
        // a static six-row reservation drawn small.
        [SerializeField] internal Image detailColumnFill;
        [SerializeField] internal Image[] detailColumnRim;

        // ICON ROWS, NOT TEXT -- see FightScreen.BuildDetailColumn's own header.
        // detailIconImages[i] is a plain Image with no sprite assigned at
        // build time (FightScreen.BuildDetailColumn's Ui.Sprite(..., null,
        // ...)) -- RefreshDetail assigns one at runtime by IconKey, off the
        // same Resources.Load + IconCache<string> mechanism
        // StatusRowSprites already uses.
        [SerializeField] internal Image[] detailIconImages;
        [SerializeField] internal TMP_Text[] detailIconValues;

        [SerializeField] internal GameObject targetPrompt;
        [SerializeField] internal TMP_Text targetPromptLabel;

        [SerializeField] internal Image[] spellVfx;
        [SerializeField] internal GameObject[] damagePopups;
        [SerializeField] internal TMP_Text[] damagePopupLabels;
        [SerializeField] internal FightBeatPlayer beatPlayer;
        // One per stage slot. Index 0 is the primary -- it draws the beat's
        // own Target and is what the measurement helpers read -- and the rest
        // only ever run for an effect that lands on more than one thing.
        [SerializeField] internal SpellVfxPlayer[] spellVfxPlayers;

        // THE SHARED GROUND BAND. A fault opening under the whole enemy
        // formation is ONE drawing however many enemies stand on it -- see
        // FightScreen.BuildSpellGroundVfx for why it is a separate node at a
        // different depth rather than more members of the pool above.
        //
        // A BAND OF TWO RATHER THAN A POOL OF ONE, and the second member is
        // forced by ownership: a Cinderfault opening while the previous one's
        // fault is still cooling must get its own member, because a cast taking
        // over a live one is the restart per-cast ownership exists to remove.
        // See FightHudSpec.SpellGroundRenderers for why two is also the
        // ceiling.
        [SerializeField] internal Image[] spellGroundVfx;
        [SerializeField] internal SpellVfxPlayer[] spellGroundVfxPlayers;

        // Every pooled drop's Image, bound from the particle pool node.
        [SerializeField] internal Image[] spellParticles;

        // WHO OWNS A SPELL WHILE IT IS DRAWING. Lives on the effects pool node,
        // so it ticks from its own Update and this controller gains no
        // responsibility for a clock it has no opinion about.
        [SerializeField] internal SpellPerformancePlayer performancePlayer;

        // The plate every combatant with no authored battle art falls back to.
        // Assigned at build time from the same sprite the enemy plates use, so a
        // slot with nothing behind it reads as a real combatant rather than
        // vanishing or -- the shipped v1 bug class -- rendering as a solid white
        // quad.
        [SerializeField] internal Sprite enemyFallbackSprite;

        // ---- state -------------------------------------------------------------

        // The session is handed in by whoever starts the fight. Null until then,
        // and every paint below is a no-op meanwhile -- which is what lets the
        // scene be built, screenshotted and audited with no run in progress.
        private FightSession _session;

        // The menu is the VIEW's state and lives here, not on the session -- a
        // session that also tracked what was highlighted could disagree with the
        // screen about whose turn it was. It is still a plain Domain object, so
        // its five edges are tested without a scene.
        private readonly FightMenuState _menu = new FightMenuState();

        // The satchel is the RUN's, resolved by whoever starts the fight. The
        // inventory lives in RunState and has no business being reachable from
        // combat.
        private IReadOnlyList<SatchelStack> _satchel = new List<SatchelStack>();

        // Bind's own parameter, kept rather than spent on ApplyBackground
        // alone. An elite room fields a squad drawn entirely from the elite
        // pool (EncounterRoll.EliteEnemyCount) -- eliteness is a property of
        // the ENCOUNTER, not of any one monster in it, so every living enemy
        // plate reads it the same way the background already does. A boss
        // room fields exactly the one declared boss, so Boss dressing reads
        // the front slot only -- see RefreshEnemyPlates' own use of this.
        private EncounterClass _encounterClass = EncounterClass.Normal;

        // NO INPUT LOCK. A real 0.6s WaitForSeconds gate was drafted and cut
        // before it shipped: EnemyFightableTests.cs binds with
        // EncounterClass.Boss and presses ATTACK after two frames, for every
        // boss in the roster, with no speed-multiplier seam available to
        // compress a hard real-time wait the way FightBeatPlayer's own waits
        // already can. A lock that only some fixtures know how to skip past
        // is a lock that silently breaks the ones that don't -- so the
        // announcement stays a beat the bark speaks, not a gate the player
        // waits on.
        private void AnnounceBossIfAny()
        {
            if (_encounterClass != EncounterClass.Boss) return;

            // A BOSS ROOM FIELDS EXACTLY THE ONE DECLARED BOSS -- see
            // _encounterClass's own comment -- so the front slot is always
            // it.
            string bossName = _session?.Encounter.FrontEnemy?.Name;
            if (!string.IsNullOrEmpty(bossName)) PushLogLine(bossName);
        }

        // Handed a new satchel after one is spent. Set only in Bind until now,
        // so a potion used mid-fight left the column showing the count it had
        // when the fight started.
        internal void RefreshSatchel(IReadOnlyList<SatchelStack> satchel)
        {
            _satchel = satchel ?? new List<SatchelStack>();
            RefreshUi();
        }

        // Where each party member's battle art lives, keyed by the combatant the
        // encounter is running.
        //
        // A PARALLEL MAP, deliberately not part of PlayerKit. The kit is what
        // combat needs, and a sprite folder is not that -- v1 kept art lookups
        // and combat data in the one `_playerOwners` dictionary, which is a
        // large part of why its combat logic could not leave the controller.
        private readonly Dictionary<CombatantState, string> _partyArt =
            new Dictionary<CombatantState, string>();

        private string PortraitFolderFor(CombatantState combatant) =>
            combatant != null && _partyArt.TryGetValue(combatant, out var folder) ? folder : null;

        // Handed in alongside the session by whoever starts the fight. Separate
        // from Bind so a caller with no art (a test, the design preview) simply
        // does not call it.
        public void BindPartyArt(IReadOnlyList<CombatantState> party, IReadOnlyList<string> folders)
        {
            _partyArt.Clear();
            if (party == null || folders == null) return;

            for (int i = 0; i < party.Count && i < folders.Count; i++)
            {
                _partyArt[party[i]] = folders[i];
            }

            // Repaints itself rather than trusting the caller to.
            //
            // Bind already painted the stage once, BEFORE this map existed, so
            // every party member resolved to the fallback plate and stayed there.
            // Making the order matter would be a rule nobody can see; repainting
            // here makes the two calls commutative.
            //
            // THE WHOLE SCREEN, not only the stage: the turn-order ribbon
            // also reads this map (RefreshInitiative -> StanceSpriteFor ->
            // SpriteFolderFor), so a stage-only repaint would leave every
            // party chip on its letter fallback ("S", "O", "B") while the
            // enemy chips beside it draw, until the player's first press
            // repaints the HUD. RefreshUi includes RefreshStage, so the next
            // reader of party art is covered without being listed here.
            if (_session != null) RefreshUi();
            else RefreshStage();
        }

        // True while beats are playing. Every click checks it, in one place.
        private bool _isBusy;
        private bool _wired;

        // This controller's own entry on NavigationInputModule's stack --
        // null until RegisterNavContext runs (FightController.Input.cs),
        // which is what makes that registration idempotent.
        private NavContext _navContext;

        // The overarching menu Cancel opens from the ROOT of the fight menu
        // (OnBackPressed, FightController.Input.cs) -- assigned by
        // ScreenRegistry.WireFight from the same WireSystemMenu call that
        // builds it, the way the hub and the map hold their copies.
        [SerializeField] internal SystemMenuController systemMenu;

        // `presentation` is the fight's dressing beyond its class -- an event
        // fight's backdrop, round counter, overlay and ambience
        // (FightController.Rounds). Null is a room fight's None.
        public void Bind(FightSession session, EncounterClass encounterClass,
                         IReadOnlyList<SatchelStack> satchel = null,
                         FightRoundPresentation presentation = null)
        {
            _session = session;
            _encounterClass = encounterClass;
            _satchel = satchel ?? new List<SatchelStack>();
            _menu.Reset();
            _log.Clear();

            // RESET, NOT TRUSTED. A Bind that finds this already true would
            // hold the new fight's own input closed for no reason it caused --
            // stale only if some earlier playback on this same controller was
            // superseded rather than finished (Play's Supersede stops the
            // coroutine without ever calling its onFinished, which is the one
            // place this flag normally clears). Every real fight binds a fresh
            // FightController and never hits this; a test that binds a second
            // session onto one already-bootstrapped controller can.
            _isBusy = false;

            // Clearing the log leaves the BANNER, so hide it too -- otherwise
            // every fight opens with an empty painted strip across the top of
            // the screen and only fills it once somebody speaks.
            ShowBark(false);

            ApplyBackground(encounterClass);
            WireInput();
            WirePlayback();
            ResetStagePresentation();

            // After the class backdrop, which a request-driven one replaces.
            // The opening round is shown below, once the opening beats are
            // drained (PresentOpeningRound).
            ApplyPresentation(presentation);

            // The session existing IS what "Fight is active" meant to the
            // old PollGamepadNavigation guard (_session != null) -- so this
            // is where the dispatcher learns Fight is on the stack.
            RegisterNavContext();

            // Once-per-fight voice lines are once per FIGHT, so the spent list
            // has to be cleared when a new one starts.
            _spokeVictory = false;
            CharacterVoice.ClearSpentLines();

            foreach (var line in _session.DrainImmediateMessages()) PushLogLine(line);

            // The first turn's intents are NOT committed here any more.
            //
            // FightSession.Begin() is where this belongs: with nothing
            // resolving an enemy that won initiative, a fight that opened on
            // a monster's turn would never give the player one.
            // FightBootstrap calls Begin, and Begin telegraphs turn one, so
            // committing them again here would be a second home for the same
            // concern -- and a Bind that quietly repairs half an opening
            // would let a missing Begin call go unnoticed.
            //
            // But Begin() ALSO resolves a faster enemy's opening swing
            // (RelicsOnCombatBegin/GrantTurnStart/AutoResolveEnemyTurns, all
            // ahead of the player's own first turn), and that swing recorded a
            // beat into the session the same way any other action does. Nothing
            // upstream of here ever drained it, so it sat queued until the
            // player's own first click, then played bundled underneath the
            // player's own action -- a monster's opening hit that was invisible
            // until the player had already moved. Draining it HERE, before the
            // static/dynamic repaint below opens the screen to input, is what
            // makes it visible on its own, in order, before the player acts.
            RefreshCommandColumn();

            var openingBeats = _session.DrainBeats();
            foreach (var beat in openingBeats)
            {
                if (beat == null) continue;
                beat.PaintActorDamageType(_session.ActorAttackType(beat.Actor));
            }

            // The counter opens on the round the first of these beats
            // carries, so the rest step it as they play.
            PresentOpeningRound(openingBeats, willPlay: beatPlayer != null && openingBeats.Count > 0);

            if (beatPlayer != null && openingBeats.Count > 0)
            {
                // HELD CLOSED, the same way AfterResolution holds it closed for
                // a player's own action: _isBusy blocks CanAct, so nothing can
                // act on the enemy's swing while it is still animating.
                // OnPlaybackFinished is the one place that flag is cleared and
                // is exactly what a player's own beat sequence completes
                // through -- reusing it here means the opening swing is
                // finished, watchdog-safe, exactly the way every other beat
                // sequence is.
                _isBusy = true;
                beatPlayer.Play(openingBeats, () =>
                {
                    OnPlaybackFinished();
                    AnnounceBossIfAny();
                });
            }
            else
            {
                // No beats to play (the player won initiative, or no player
                // attached -- a headless test, a preview): behave exactly as
                // before.
                foreach (var beat in openingBeats)
                {
                    foreach (var line in beat.Messages) PushLogLine(line);
                }

                RefreshUi();
                AnnounceBossIfAny();
            }
        }

        private void Start()
        {
            // Painted once so the screen is legible before any fight exists:
            // the static half of the HUD (verb captions, breadcrumb, submenu
            // chrome) comes straight from the manifest and needs no session.
            RefreshCommandColumn();

            // Once, here rather than per refresh: attaching a hover handler
            // every frame would stack them.
            WireIntentIcons();
            WireAllStatusBadges();
            WireSubmenuScroll();
        }

        // The skill list's two scroll surfaces.
        //
        // ADDED AT RUNTIME, the same bargain HoverIndex and RailScroll make:
        // both carry a delegate, and a scene cannot serialise one usefully.
        private void WireSubmenuScroll()
        {
            if (submenuViewport != null)
            {
                // The viewport draws nothing, so it has no Graphic and would
                // never see a wheel event. A fully transparent Image raycasts
                // against its RECT, which is exactly what a scroll surface
                // wants -- the same mechanism the reward track's rail uses.
                var catcher = submenuViewport.gameObject.GetComponent<Image>()
                              ?? submenuViewport.gameObject.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f);
                catcher.raycastTarget = true;

                var wheel = submenuViewport.gameObject.GetComponent<ListScroll>()
                            ?? submenuViewport.gameObject.AddComponent<ListScroll>();
                wheel.WheelStep = FightSubmenuLayout.RowPitch;
                wheel.Scrolled = ScrollSubmenuBy;
            }

            if (submenuScrollTrack == null) return;

            // The track is decoration and the emitter cleared its raycast
            // target with everything else in the subtree; a bar you cannot
            // grab is a picture of a bar.
            var trackImage = submenuScrollTrack.GetComponent<Image>();
            if (trackImage != null) trackImage.raycastTarget = true;

            var seek = submenuScrollTrack.gameObject.GetComponent<ListScroll>()
                       ?? submenuScrollTrack.gameObject.AddComponent<ListScroll>();
            seek.Seeked = SeekSubmenuTo;

            // The focus marker steps past the bar instead of crowding it
            // (FocusKeepClear's header).
            FocusKeepClear.Mark(submenuScrollTrack.gameObject);
        }

        private void WirePlayback()
        {
            if (beatPlayer == null) return;

            beatPlayer.SlotFor = SlotFor;
            beatPlayer.AnimatorFor = AnimatorFor;
            beatPlayer.PaintVitals = PaintVitals;
            beatPlayer.PaintShields = PaintShields;
            beatPlayer.PaintFormation = PaintFormation;
            beatPlayer.PaintTurnOrder = PaintTurnOrder;
            beatPlayer.FormationIsMoving = FormationIsMoving;
            beatPlayer.PushLine = PushLogLine;
            beatPlayer.SetStance = PoseCombatant;
            beatPlayer.WearForm = WearForm;
            beatPlayer.ResyncForms = ResyncForms;
            beatPlayer.FlashTarget = FlashCombatant;
            // A lambda rather than the method group, because the cast handle
            // PlaySpellVfx returns is for the tests that ask about ownership --
            // the beat player has nothing to do with it and is deliberately not
            // given a way to hold one.
            beatPlayer.PlayVfx = beat => PlaySpellVfx(beat);
            beatPlayer.PlayContactFx = PlayContactFx;
            beatPlayer.PlayFormHitFx = PlayFormHitFx;
            beatPlayer.FadeTheFallen = FadeTheFallen;
            beatPlayer.ImpactDelayFor = ImpactDelayFor;
            beatPlayer.StopVfx = StopSpellVfx;
            beatPlayer.ShakeStage = ShakeStage;
            beatPlayer.PresentRound = PresentRound;
        }

        // Kicks both stage racks. Playback decides how hard; the view decides
        // what "the stage" means, which is exactly the split every other
        // delegate on beatPlayer already draws.
        private void ShakeStage(float strength)
        {
            if (stageShakes == null) return;

            foreach (var shake in stageShakes) shake?.Kick(strength);
        }

        // WHICH SLOT A COMBATANT OWNS, FOR THE WHOLE FIGHT.
        //
        // Scanning Encounter.PlayerParty for the combatant and returning the
        // handle at the same INDEX would be correct only while the party
        // list never moves, and Move is the command that moves it: trading
        // two members' list positions would trade their sprites, their
        // nameplates, their hit flashes, their death fades and their
        // animators, so an already-queued enemy swing could land its flash
        // and its number on the figure that had stepped out of the way.
        //
        // A slot belongs to the combatant that started in it and keeps it
        // until the fight ends. Position is the thing that moves -- see
        // RefreshStage, which marks each slot from its occupant's RANK in the
        // beat being played. Identity and position were one fact by accident,
        // and separating them is the whole of this change.
        private readonly Dictionary<CombatantState, int> _slotOf =
            new Dictionary<CombatantState, int>();

        // Seeded once per fight from the opening lists, which is the moment
        // "where everyone started" is true by definition.
        private void BindSlots()
        {
            _slotOf.Clear();
            if (_session == null) return;

            var party = _session.Encounter.PlayerParty;
            for (int i = 0; i < party.Count; i++) _slotOf[party[i]] = i;

            var enemies = _session.Encounter.Enemies;
            for (int i = 0; i < enemies.Count; i++) _slotOf[enemies[i]] = i;
        }

        // A combatant's own slot index, registering a newcomer on the way.
        //
        // ONLY THE ENEMY SIDE CAN GROW, and only by appending (a summon;
        // CombatEncounter.TryAddEnemy refuses past StageSlotsPerSide), so an
        // unregistered combatant's list index IS its slot -- nothing has
        // reordered the list it is being read out of. The party is registered
        // whole at Bind and can never reach this path, which matters: reading
        // a party member's index back out of the live list is exactly the bug
        // above.
        private int SlotIndexOf(CombatantState combatant)
        {
            if (_session == null || combatant == null) return -1;
            if (_slotOf.TryGetValue(combatant, out int known)) return known;

            var side = combatant.IsPlayerSide ? _session.Encounter.PlayerParty : _session.Encounter.Enemies;
            for (int i = 0; i < side.Count; i++)
            {
                if (!ReferenceEquals(side[i], combatant)) continue;

                _slotOf[combatant] = i;
                return i;
            }

            return -1;
        }

        // The one lookup the four below are. They differed only in which pair
        // of arrays they walked, and each carried its own copy of the scan
        // that the slot map replaces.
        private T HandleFor<T>(CombatantState combatant, T[] enemyHandles, T[] partyHandles)
            where T : class
        {
            int slot = SlotIndexOf(combatant);
            if (slot < 0) return null;

            var handles = combatant.IsPlayerSide ? partyHandles : enemyHandles;
            return handles != null && slot < handles.Length ? handles[slot] : null;
        }

        // Where a combatant is standing, for the floating number. The view owns
        // this mapping; playback only asks.
        private RectTransform SlotFor(CombatantState combatant) =>
            HandleFor(combatant, enemySlots, partySlots);

        // SlotFor's exact twin for the animator that lives on that same slot,
        // so playback (FightBeatPlayer.TravelFor/RecoilOne/Punch) does not
        // have to walk SlotFor and then GetComponent<StageActorAnimator> on
        // what comes back;
        // this returns the component directly from the array ScreenRegistry
        // populated, so nothing downstream of the controller ever calls
        // GetComponent to find one.
        private StageActorAnimator AnimatorFor(CombatantState combatant) =>
            HandleFor(combatant, enemyActorAnimators, partyActorAnimators);

        // SlotFor's twin for the hit-flash silhouette, so FlashOne avoids
        // the SlotFor(target).GetComponentInChildren shape this file avoids
        // elsewhere too.
        private StageHitFlash HitFlashFor(CombatantState combatant) =>
            HandleFor(combatant, enemyHitFlashes, partyHitFlashes);

        // SlotFor's twin for the death fade, so FadeTheFallen avoids
        // SlotFor(target).GetComponent.
        private StageDeathFade DeathFadeFor(CombatantState combatant) =>
            HandleFor(combatant, enemyDeathFades, partyDeathFades);

        // And for the sprite, which nothing outside RefreshStage asks for --
        // it is here so every per-combatant handle is resolved one way.
        private Image SpriteFor(CombatantState combatant) =>
            HandleFor(combatant, enemySprites, partySprites);

        // Repaints the HUD from a RECORDED set of vitals rather than from live
        // state. This is the reason the session records any at all: by the time a
        // beat plays, every later action in the same round has already moved the
        // real numbers, so painting live state would show the end of the round on
        // the first blow.
        //
        // Called twice per beat -- with PreSnapshot when the beat opens and with
        // Snapshot at the impact frame. Which set is which is playback's
        // business; this only paints what it is handed.
        private void PaintVitals(IReadOnlyDictionary<CombatantState, Vitals> vitals)
        {
            if (vitals == null) return;

            // A SNAPSHOT IS ALSO A GUEST LIST. Anyone it mentions existed at
            // the moment it was taken, which is the only signal the stage has
            // for when a summoned monster is allowed to be seen -- see
            // _confirmedPresent. Done here rather than in a fourth delegate
            // because this is already the one call that receives a moment.
            ConfirmPresent(vitals);
            RefreshStage();

            var enemies = Enemies;
            for (int i = 0; i < enemies.Count && i < enemyPlates.Length; i++)
            {
                if (!vitals.TryGetValue(enemies[i], out var recorded)) continue;

                bool standing = recorded.Health > 0;
                enemyPlates[i].gameObject.SetShown(standing);
                if (!standing) continue;

                int enemyMax = RecordedMax(recorded.MaxHealth, enemies[i].MaxHealth);
                enemyPlateHps[i].Set(UiStrings.HealthValue, recorded.Health, enemyMax);
                SetFill(enemyPlateHpFills[i], recorded.Health, enemyMax);
            }

            // EVERY PARTY MEMBER IN THE SNAPSHOT, not just the acting one:
            // three identical plates means a heal landing on an ally moves
            // that ally's own bar on the frame the blow lands, which is what
            // a snapshot is for.
            //
            // A member the snapshot does not mention is left alone rather
            // than cleared: a snapshot is a record of a moment, and silence
            // in it means "unchanged", not "gone".
            //
            // ONTO THE CARD THAT IS CURRENTLY SHOWING THE MEMBER -- the
            // painted-occupancy record, walked plate-first.
            //
            // NEITHER OF THE TWO OBVIOUS INDEXES IS THE RIGHT ONE. The live
            // party list is wrong because the round has already finished
            // resolving by the time a beat plays, so a Move in it has traded
            // two members' list positions while their cards are still where
            // RefreshPcPlates last drew them -- addressing by that list would
            // land the two moved members' numbers on each other's portraits
            // for the rest of the playback. _slotOf is wrong because a slot
            // never moves, so addressing by it would force the column to sit
            // in its opening order for the whole fight, when the column is
            // meant to follow the field.
            //
            // What is right is the third thing: whom RefreshPcPlates last
            // PUT on each card. That answer is correct during the playback
            // (the cards have not moved yet) and correct after it (the
            // repaint at OnPlaybackFinished moves the cards and rewrites the
            // record in the same pass). A null record means no repaint has
            // run at all, so no card is showing anybody and there is nothing
            // to paint onto -- Bind repaints before any beat can play.
            //
            // Length-bounded by the record itself rather than by pcPlates:
            // its only writer sizes it FROM pcPlates, and every array this
            // loop then writes is bounds-checked by Has().
            var occupants = _plateOccupants;
            for (int plate = 0; occupants != null && plate < occupants.Length; plate++)
            {
                var member = occupants[plate];
                if (member == null || !vitals.TryGetValue(member, out var recordedPc)) continue;

                int maxHealth = RecordedMax(recordedPc.MaxHealth, member.MaxHealth);
                int maxPrimary = RecordedMax(recordedPc.MaxPrimary, member.MaxMana);

                if (Has(pcHpValues, plate))
                {
                    pcHpValues[plate].Set(UiStrings.HealthValue, recordedPc.Health, maxHealth);
                }
                if (Has(pcMpValues, plate))
                {
                    pcMpValues[plate].Set(UiStrings.PoolNamedValue,
                        member.PrimaryPool?.ShortTag ?? "", recordedPc.Primary, maxPrimary);
                }
                if (Has(pcHpFills, plate)) SetFill(pcHpFills[plate], recordedPc.Health, maxHealth);
                if (Has(pcMpFills, plate)) SetFill(pcMpFills[plate], recordedPc.Primary, maxPrimary);
            }
        }

        // A RECORDED MAXIMUM, OR LIVE IF THE MOMENT DID NOT RECORD ONE.
        //
        // Vitals gained its two maxima in the same change as the call sites
        // above; the three-argument constructor that does not set them is
        // still what the EditMode beat fixtures build, and a zero denominator
        // is the one value that cannot be painted. Graceful degradation to
        // live state is the house style, and live state is what every one of
        // these call sites read before the maxima existed at all.
        private static int RecordedMax(int recorded, int live) => recorded > 0 ? recorded : live;

        // EVERY LIVING CAST, ABANDONED. Wired to the beat player's EndFight
        // rather than to its Flush: a tail from the previous round is supposed
        // to survive the start of the next one, and stopping every visual when
        // a new playback begins is precisely the beat-end cleanup the brief
        // asks to remove. What this is still for is the abandoned fight -- a
        // scene change mid-round must not leave a fault frozen mid-rupture
        // behind an empty stage.
        private void StopSpellVfx()
        {
            if (performancePlayer != null) performancePlayer.CancelAll();
        }

        // Three backdrops on ONE Image, swapped by encounter class -- v1's
        // behaviour, preserved deliberately over three stacked variants: only
        // one is ever visible, and three would be three chances for the wrong
        // one to be left active.
        public void ApplyBackground(EncounterClass encounterClass)
        {
            if (backgroundImage == null) return;

            var sprite = encounterClass == EncounterClass.Boss ? bossBackground
                : encounterClass == EncounterClass.Elite ? eliteBackground
                : normalBackground;

            // Falls back rather than blanking: an Image with no sprite renders
            // as a solid white quad, not as nothing.
            backgroundImage.sprite = sprite != null ? sprite : normalBackground;
        }

        // The part of the HUD that is the same in every fight.
        public void RefreshCommandColumn()
        {
            breadcrumb.Set(UiStrings.CommandTitle);
            submenuTitle.Set(UiStrings.SubmenuSkillsTitle);
            submenuHint.Set(UiStrings.SubmenuHint);
        }

        // Re-anchors the open submenu to however many rows it is actually
        // showing, through the SAME function that placed them at build time.
        //
        // This is the one runtime layout call on the screen, and it exists
        // because a character's skill count is not known until a fight starts.
        // v1 did the same thing from a hand-mirrored copy of the constants,
        // which is how its design preview ended up drawing rows at 8-slot
        // positions with a gap above BACK.
        public void AnchorSubmenuRows(int count)
        {
            if (submenuRowRects == null) return;

            // CLAMPED TO THE POOL, which is sixteen rows rather than the nine
            // that fit. A count above the pool is content growth, not an error
            // -- but a row past it has no rect and cannot be reached at all,
            // which is why the pool carries four spare.
            int shown = FightSubmenuLayout.VisibleCount(count);
            _submenuCount = shown;

            // NOTHING IS RE-ANCHORED ANY MORE. Every row sits at its pool
            // position forever and the rect around them moves instead -- one
            // write where there were sixteen, and the only construction that
            // can also scroll. Rows are switched on or off and nothing else.
            for (int i = 0; i < submenuRowRects.Length; i++)
            {
                var rect = submenuRowRects[i];
                if (rect == null) continue;

                bool visible = i < shown;
                if (rect.gameObject.activeSelf != visible) rect.gameObject.SetActive(visible);
            }

            // Opening at the TOP of the list: row 0 is the first skill, and a
            // menu that opens halfway down its own contents is a menu the
            // player has to scroll before they can read it.
            //
            // ONLY ON OPENING IT, though, which is the fix. This ran on every
            // repaint, and a repaint is what a hover costs -- so the player
            // scrolled, the rows moved under the pointer, the new hover
            // repainted, and the list was back at the top before the notch had
            // finished. Same branch and same count means the same list still
            // open in front of the same player, and where they left it is
            // theirs to keep.
            bool sameList = shown == _submenuScrolledCount && _menu.Branch == _submenuScrolledBranch;
            _submenuScrolledCount = shown;
            _submenuScrolledBranch = _menu.Branch;

            if (!sameList) _submenuScroll = 0f;

            ResizeSubmenuContainer(shown);
            ApplySubmenuScroll();

            // The header sits on the container now rather than on the top row,
            // so it no longer moves with the count -- but it is still written
            // here, because the container's height is what it is derived from
            // and that is a layout number rather than a constant.
            //
            // HeaderYFor, not HeaderY -- HeaderY answers the STATIC tree's own
            // (oversized, BuildReservationRows) placeholder position; this
            // wants the REAL frame's top edge for `shown` rows.
            float headerY = FightSubmenuLayout.HeaderYFor(shown);
            MoveToY(submenuTitle, headerY);
            MoveToY(submenuHint, headerY);
        }

        // THE CONTAINER GROWS TO FIT UP TO FightSubmenuLayout.RowsInView
        // ROWS, THEN CAPS -- a three-skill actor gets a three-row frame, a
        // twelve-skill one gets the same five-row cap, with the existing
        // scrollbar reaching the rest.
        //
        // HEIGHT AND Y ONLY, NEVER WIDTH OR X. Every one of these is
        // BOTTOM-anchored (VisibleBottomLine for the frame, RowsBottom for
        // the viewport, ContainerBottom for the back row -- all fixed
        // constants, independent of row count), so shrinking only ever
        // removes space off the TOP of the list; BACK and the verb column's
        // own flush edge never move. Width stays at FightSubmenuLayout.
        // FrameWidth's own STATIC figure always -- a row is RowWidth wide
        // regardless of how many of them there are, so following the
        // frame's 3:4 aspect down to a short list's own smaller height would
        // narrow it past what a row needs and clip it (FightSubmenuLayout.
        // BuildReservationRows' own header is where that was tried and
        // failed the audit). Holding width fixed is also what keeps
        // ContainerX/SubmenuX and every offset solved from them at build
        // time correct at any count, so nothing besides these rects has to
        // move.
        //
        // THE TRACK MOVES WITH THE VIEWPORT too, not only the thumb --
        // FightSubmenuLayout.ThumbHeight/ThumbCentreY already read
        // RowsInView's own (capped) window rather than the STATIC one, on
        // the reasoning that the bar only ever shows once the real viewport
        // has been resized to that cap; the track's own rect has to agree
        // with the same window or it runs alongside a thumb that no longer
        // matches its length.
        private void ResizeSubmenuContainer(int count)
        {
            if (submenuContainer != null)
            {
                submenuContainer.sizeDelta = new Vector2(
                    submenuContainer.sizeDelta.x, FightSubmenuLayout.FrameHeightFor(count));
                MoveToY(submenuContainer, FightSubmenuLayout.FrameCentreYFor(count));
            }

            if (submenuViewport != null)
            {
                submenuViewport.sizeDelta = new Vector2(
                    submenuViewport.sizeDelta.x, FightSubmenuLayout.ViewportHeightFor(count));
                MoveToY(submenuViewport, FightSubmenuLayout.ViewportOffsetInContainerFor(count));
            }

            if (submenuScrollTrack != null)
            {
                submenuScrollTrack.sizeDelta = new Vector2(
                    submenuScrollTrack.sizeDelta.x, FightSubmenuLayout.ViewportHeightFor(count));
                MoveToY(submenuScrollTrack, FightSubmenuLayout.ViewportOffsetInContainerFor(count));
            }

            if (submenuBackButton != null)
            {
                MoveToY((RectTransform)submenuBackButton.transform,
                    FightSubmenuLayout.BackRowY - FightSubmenuLayout.ContainerCentreYFor(count));
            }
        }

        // Slides the list and repaints the bar. The one place either is written.
        private void ApplySubmenuScroll()
        {
            float range = FightSubmenuLayout.ScrollRange(_submenuCount);

            if (_submenuScroll < 0f) _submenuScroll = 0f;
            if (_submenuScroll > range) _submenuScroll = range;

            if (submenuContent != null)
            {
                submenuContent.anchoredPosition = new Vector2(
                    submenuContent.anchoredPosition.x,
                    FightSubmenuLayout.ContentY(_submenuCount, _submenuScroll));
            }

            // NO BAR WHEN THERE IS NOTHING TO SCROLL, which is the common case:
            // a character with nine skills or fewer sees a plain framed list.
            // A track drawn against a list that fits is a control that cannot
            // do anything, and this screen already had eight of those.
            bool scrolls = range > 0f;

            if (submenuScrollTrack != null
                && submenuScrollTrack.gameObject.activeSelf != scrolls)
            {
                submenuScrollTrack.gameObject.SetActive(scrolls);
            }

            if (submenuScrollThumb == null) return;

            if (submenuScrollThumb.gameObject.activeSelf != scrolls)
            {
                submenuScrollThumb.gameObject.SetActive(scrolls);
            }
            if (!scrolls) return;

            submenuScrollThumb.sizeDelta = new Vector2(
                submenuScrollThumb.sizeDelta.x, FightSubmenuLayout.ThumbHeight(_submenuCount));

            submenuScrollThumb.anchoredPosition = new Vector2(
                submenuScrollThumb.anchoredPosition.x,
                FightSubmenuLayout.ThumbCentreY(_submenuCount, _submenuScroll));
        }

        // The two gestures, both landing in the same place. The wheel and a drag
        // over the rows arrive as pixels; a grab on the track arrives as a
        // fraction of the whole list.
        private void ScrollSubmenuBy(float pixels)
        {
            _submenuScroll += pixels;
            ApplySubmenuScroll();
        }

        private void SeekSubmenuTo(float fromTop)
        {
            _submenuScroll = FightSubmenuLayout.ScrollAt(_submenuCount, fromTop);
            ApplySubmenuScroll();
        }

        // The column closed, so the scroll it was holding stops being anybody's.
        //
        // Called from the one place that knows -- RefreshSubmenu's closed path.
        // Without it, backing out of a twelve-row list and opening the same
        // twelve-row list again would land the player wherever they were last
        // time, which is a menu that remembers something no player asked it to.
        private void ForgetSubmenuScroll()
        {
            _submenuScrolledCount = -1;
            _submenuScrolledBranch = MenuBranch.None;
        }

        // Bring a row the player ARROWED onto back inside the window.
        //
        // Only the keyboard and the stick need this: a row reached with the
        // mouse is under the pointer and therefore already on screen, and a
        // list that scrolled itself on hover would be unusable. Arrowing has no
        // such guarantee -- past the eighth row the highlight simply walked
        // off the top of the viewport and the player was navigating a list they
        // could not see.
        //
        // Written as a correction to the CURRENT position rather than as a
        // target scroll: how far the row overhangs the window is the amount to
        // move, and a row already inside it moves nothing.
        private void ScrollSubmenuRowIntoView(int index)
        {
            if (index < 0 || index >= _submenuCount) return;
            if (FightSubmenuLayout.ScrollRange(_submenuCount) <= 0f) return;

            float y = FightSubmenuLayout.ContentY(_submenuCount, _submenuScroll)
                      + FightSubmenuLayout.RowYInContent(index);

            // RowsInView's own window, not the static ViewportHeight -- this
            // only ever runs once ScrollRange has already confirmed the list
            // is long enough to scroll, which is exactly the regime the real
            // (resized) viewport sits at RowsInView's cap in.
            float windowHalf = FightSubmenuLayout.ColumnHeight(FightSubmenuLayout.RowsInView) * 0.5f;
            float rowHalf = FightSubmenuLayout.RowHeight * 0.5f;

            // Scrolling further down the list moves the content UP, so a row
            // hanging off the top is corrected by scrolling BACK -- see
            // FightSubmenuLayout.ContentOffsetY for why the two run opposite
            // ways round.
            float overTop = y + rowHalf - windowHalf;
            float underBottom = -windowHalf - (y - rowHalf);

            if (overTop > 0f) _submenuScroll -= overTop;
            else if (underBottom > 0f) _submenuScroll += underBottom;
            else return;

            ApplySubmenuScroll();
        }

        private static void MoveToY(TMP_Text text, float y)
        {
            if (text == null) return;
            var rect = text.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        private static void MoveToY(RectTransform rect, float y)
        {
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        // Both axes at once -- FightController.Hud.cs's RefreshDetail needs
        // this for the skill detail card's hero icon and its name/kind
        // block, which move sideways (not just up and down like every other
        // count-resized row) depending on whether the current hover has a
        // hero icon to make room for.
        private static void MoveTo(RectTransform rect, float x, float y)
        {
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(x, y);
        }

        // ---- queries the view asks of the session --------------------------------

        public bool HasSession => _session != null;

        // True while beats are playing. Public so a PlayMode test can wait for
        // playback to finish rather than guessing at a frame count -- the same
        // reason FightBeatPlayer.BeatSpeedMultiplier is public.
        public bool IsBusy => _isBusy;

        // The session and the fallback plate, for the one test that opens the
        // scene the way a player does and has to check what came out of content.
        public FightSession Session => _session;

        public Sprite FallbackSprite => enemyFallbackSprite;

        public IReadOnlyList<CombatantState> Enemies =>
            _session != null ? _session.Encounter.Enemies : System.Array.Empty<CombatantState>();
    }
}
