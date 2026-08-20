using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

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
    public partial class FightController : MonoBehaviour
    {
        // ---- the stage ---------------------------------------------------------

        [SerializeField] internal Image backgroundImage;
        [SerializeField] internal Sprite normalBackground;
        [SerializeField] internal Sprite eliteBackground;
        [SerializeField] internal Sprite bossBackground;

        [SerializeField] internal RectTransform[] enemySlots;
        [SerializeField] internal Image[] enemySprites;
        [SerializeField] internal Image[] enemyHitFlashes;
        [SerializeField] internal TMP_Text[] enemyNameplates;
        [SerializeField] internal Image[] enemyFootShadows;

        [SerializeField] internal RectTransform[] partySlots;
        [SerializeField] internal Image[] partySprites;
        [SerializeField] internal Image[] partyHitFlashes;
        [SerializeField] internal TMP_Text[] partyNameplates;
        [SerializeField] internal Image[] partyFootShadows;

        // ---- the HUD -----------------------------------------------------------

        [SerializeField] internal Image[] initiativeIcons;
        [SerializeField] internal Image[] initiativeRings;
        [SerializeField] internal TMP_Text[] initiativeLabels;

        [SerializeField] internal Image barkPortrait;
        [SerializeField] internal TMP_Text barkLabel;

        [SerializeField] internal TMP_Text enemiesHint;
        [SerializeField] internal Button[] enemyPlates;
        [SerializeField] internal TMP_Text[] enemyPlateNames;
        [SerializeField] internal TMP_Text[] enemyPlateHps;
        [SerializeField] internal Image[] enemyPlateHpFills;
        [SerializeField] internal TMP_Text[] enemyPlateTags;
        [SerializeField] internal GameObject[] enemyPlateReticles;

        [SerializeField] internal Image partyPortrait;
        [SerializeField] internal TMP_Text partyName;
        [SerializeField] internal TMP_Text partyClass;
        [SerializeField] internal Image partyHpFill;
        [SerializeField] internal TMP_Text partyHpValue;
        [SerializeField] internal Image partyMpFill;
        [SerializeField] internal TMP_Text partyMpValue;
        [SerializeField] internal RectTransform partyMpPreview;
        [SerializeField] internal Image[] woolPips;
        [SerializeField] internal TMP_Text woolValue;

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
        [SerializeField] internal GameObject intentTooltip;
        [SerializeField] internal TMP_Text intentTooltipText;

        [SerializeField] internal GameObject submenuColumn;
        [SerializeField] internal TMP_Text submenuTitle;
        [SerializeField] internal TMP_Text submenuHint;
        [SerializeField] internal Button submenuBackButton;
        [SerializeField] internal Button[] submenuRows;
        [SerializeField] internal RectTransform[] submenuRowRects;
        [SerializeField] internal TMP_Text[] submenuNames;
        [SerializeField] internal TMP_Text[] submenuMetas;
        [SerializeField] internal TMP_Text[] submenuCosts;

        [SerializeField] internal GameObject detailColumn;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailKind;
        [SerializeField] internal TMP_Text detailBody;
        [SerializeField] internal TMP_Text[] detailStatValues;

        // The KEYS as well, so a row with nothing to say can take its label with
        // it. Declared in the tree since the panel was built and never wired, so
        // "COST / POWER / TARGET / EFFECT" sat over four blank values whenever
        // the panel had no selection to describe -- which reads as a panel that
        // failed to populate rather than one with nothing to populate.
        [SerializeField] internal TMP_Text[] detailStatKeys;

        [SerializeField] internal GameObject targetPrompt;
        [SerializeField] internal TMP_Text targetPromptLabel;

        [SerializeField] internal Image spellVfx;
        [SerializeField] internal GameObject[] damagePopups;
        [SerializeField] internal TMP_Text[] damagePopupLabels;
        [SerializeField] internal FightBeatPlayer beatPlayer;
        [SerializeField] internal SpellVfxPlayer spellVfxPlayer;

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
            RefreshStage();
        }

        // True while beats are playing. Every click checks it, in one place.
        private bool _isBusy;
        private bool _wired;

        public void Bind(FightSession session, EncounterClass encounterClass,
                         IReadOnlyList<SatchelStack> satchel = null)
        {
            _session = session;
            _satchel = satchel ?? new List<SatchelStack>();
            _menu.Reset();
            _log.Clear();

            // Clearing the log leaves the BANNER, so hide it too -- otherwise
            // every fight opens with an empty painted strip across the top of
            // the screen and only fills it once somebody speaks.
            ShowBark(false);

            ApplyBackground(encounterClass);
            WireInput();
            WirePlayback();
            ResetStagePresentation();

            // Once-per-fight voice lines are once per FIGHT, so the spent list
            // has to be cleared when a new one starts.
            _spokeVictory = false;
            CharacterVoice.ClearSpentLines();

            foreach (var line in _session.DrainImmediateMessages()) PushLogLine(line);

            // The FIRST turn's intents, which nothing else commits.
            //
            // FightSession.Begin() is where this belongs and it has NO
            // production caller -- FightEncounterAdapter constructs the session
            // and hands it straight over, so the opening sequence never runs.
            // The other commit point (AfterResolution) only fires once the
            // player has already acted, so turn one had no telegraph at all.
            // That was invisible before the icons existed, because the nameplate
            // telegraph shows nothing for a plain attack anyway.
            //
            // Called here rather than fixing Begin's wiring on purpose: Begin
            // also grants a turn start and auto-resolves enemy turns, so calling
            // it would change turn order and per-turn regen in every fight in
            // the game. That is a real bug and it is recorded in AUDIT.md rather
            // than fixed inside a UI change.
            if (!_session.IsOver && _session.IsPlayerTurn) _session.PrepareEnemyIntents();

            RefreshCommandColumn();
            RefreshUi();
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
        }

        private void WirePlayback()
        {
            if (beatPlayer == null) return;

            beatPlayer.SlotFor = SlotFor;
            beatPlayer.PaintVitals = PaintVitals;
            beatPlayer.PushLine = PushLogLine;
            beatPlayer.SetStance = PoseCombatant;
            beatPlayer.SetFrame = SetActorFrame;
            beatPlayer.AnimationFor = StanceAnimationFor;
            beatPlayer.FlashTarget = FlashCombatant;
            beatPlayer.PlayVfx = PlaySpellVfx;
            beatPlayer.FadeTheFallen = FadeTheFallen;
            beatPlayer.ImpactDelayFor = ImpactDelayFor;
            beatPlayer.StopVfx = StopSpellVfx;
        }

        // Where a combatant is standing, for the floating number. The view owns
        // this mapping; playback only asks.
        private RectTransform SlotFor(CombatantState combatant)
        {
            if (_session == null || combatant == null) return null;

            var enemies = _session.Encounter.Enemies;
            for (int i = 0; i < enemies.Count && i < enemySlots.Length; i++)
            {
                if (ReferenceEquals(enemies[i], combatant)) return enemySlots[i];
            }

            var party = _session.Encounter.PlayerParty;
            for (int i = 0; i < party.Count && i < partySlots.Length; i++)
            {
                if (ReferenceEquals(party[i], combatant)) return partySlots[i];
            }

            return null;
        }

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

            var enemies = Enemies;
            for (int i = 0; i < enemies.Count && i < enemyPlates.Length; i++)
            {
                if (!vitals.TryGetValue(enemies[i], out var recorded)) continue;

                bool standing = recorded.Health > 0;
                enemyPlates[i].gameObject.SetShown(standing);
                if (!standing) continue;

                enemyPlateHps[i].Set(UiStrings.HealthValue, recorded.Health, enemies[i].MaxHealth);
                SetFill(enemyPlateHpFills[i], recorded.Health, enemies[i].MaxHealth);
            }

            var actor = ActingCharacter();
            if (actor != null && vitals.TryGetValue(actor, out var mine))
            {
                partyHpValue.Set(UiStrings.HealthValue, mine.Health, actor.MaxHealth);
                partyMpValue.Set(UiStrings.HealthValue, mine.Mana, actor.MaxMana);
                SetFill(partyHpFill, mine.Health, actor.MaxHealth);
                SetFill(partyMpFill, mine.Mana, actor.MaxMana);
            }
        }

        private void StopSpellVfx()
        {
            if (spellVfxPlayer != null) spellVfxPlayer.StopImmediately();
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

            // CLAMPED, because a character can offer more rows than the pool
            // holds. Passing the raw count laid the column out for 17 rows and
            // pushed every rect that exists off the top of the screen.
            int shown = FightSubmenuLayout.VisibleCount(count);

            for (int i = 0; i < submenuRowRects.Length; i++)
            {
                var rect = submenuRowRects[i];
                if (rect == null) continue;

                bool visible = i < shown;
                rect.gameObject.SetActive(visible);
                if (!visible) continue;

                var position = rect.anchoredPosition;
                rect.anchoredPosition = new Vector2(position.x, FightSubmenuLayout.RowY(shown, i));
            }

            // The header rides the top of the list rather than the top of the
            // pool, so a short list keeps its own label attached to it.
            float headerY = FightSubmenuLayout.HeaderY(shown);
            MoveToY(submenuTitle, headerY);
            MoveToY(submenuHint, headerY);
        }

        private static void MoveToY(TMP_Text text, float y)
        {
            if (text == null) return;
            var rect = text.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
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
