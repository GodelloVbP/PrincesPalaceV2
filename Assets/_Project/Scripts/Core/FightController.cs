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

        // The two stage racks' shakers. Both kick together: a blow shakes the
        // stage, not the half of it the victim happens to stand on -- one side
        // jolting while the other holds still reads as a bug in the layout.
        [SerializeField] internal StageShake[] stageShakes;
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
        [SerializeField] internal Image[] enemyPlateIcons;
        [SerializeField] internal TMP_Text[] enemyPlateNames;

        // A click target over each enemy FIGURE, live only while a mark is
        // being chosen -- see FightScreen.BuildStage for why it is a separate
        // node rather than the sprite.
        [SerializeField] internal Button[] enemyHitAreas;

        // The way out of targeting. It lives on the target prompt because the
        // list BACK used to live in folds as soon as a skill is picked.
        [SerializeField] internal Button targetCancelButton;
        [SerializeField] internal TMP_Text[] enemyPlateHps;
        [SerializeField] internal Image[] enemyPlateHpFills;
        [SerializeField] internal TMP_Text[] enemyPlateTags;
        [SerializeField] internal GameObject[] enemyPlateReticles;
        [SerializeField] internal GameObject[] enemyPlateBreakTracks;
        [SerializeField] internal Image[] enemyPlateBreakFills;
        [SerializeField] internal GameObject secondLifeBadge;
        [SerializeField] internal GameObject transformStrip;
        [SerializeField] internal TMP_Text transformStripText;
        [SerializeField] internal GameObject[] rosterPlates;
        [SerializeField] internal TMP_Text[] rosterNames;
        [SerializeField] internal TMP_Text[] rosterHpValues;
        [SerializeField] internal Image[] rosterHpFills;
        [SerializeField] internal GameObject lowHpVignette;

        [SerializeField] internal Image partyPortrait;
        [SerializeField] internal TMP_Text partyName;
        [SerializeField] internal TMP_Text partyClass;
        [SerializeField] internal Image partyHpFill;
        [SerializeField] internal TMP_Text partyHpValue;
        [SerializeField] internal Image partyMpFill;
        [SerializeField] internal TMP_Text partyMpValue;

        // Same "caption is a synthesised child with no NodeRef" story as
        // enemyIntentIcons below -- each badge's Image and TMP glyph are both
        // resolved at Start.
        [SerializeField] internal GameObject[] partyBuffIcons;
        [SerializeField] internal GameObject partyBuffTooltip;
        [SerializeField] internal TMP_Text partyBuffTooltipText;
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

        // The scrolling frame around those rows.
        [SerializeField] internal RectTransform submenuViewport;
        [SerializeField] internal RectTransform submenuContent;
        [SerializeField] internal RectTransform submenuScrollTrack;
        [SerializeField] internal RectTransform submenuScrollThumb;

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

        // The skill's element, riding the POWER row -- see FightScreen.
        // BuildDetailColumn's own comment for why it shares that line rather
        // than getting a sixth row.
        [SerializeField] internal TMP_Text detailDamageType;

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

        // THE SHARED GROUND LAYER, and there is exactly one of it. A fault
        // opening under the whole enemy formation is one drawing however many
        // enemies stand on it -- see FightScreen.BuildSpellGroundVfx for why it
        // is a separate node at a different depth rather than a fourth member
        // of the pool above.
        [SerializeField] internal Image spellGroundVfx;
        [SerializeField] internal SpellVfxPlayer spellGroundVfxPlayer;

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
            RefreshStage();
        }

        // True while beats are playing. Every click checks it, in one place.
        private bool _isBusy;
        private bool _wired;

        public void Bind(FightSession session, EncounterClass encounterClass,
                         IReadOnlyList<SatchelStack> satchel = null)
        {
            _session = session;
            _encounterClass = encounterClass;
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

            // The first turn's intents are NOT committed here any more.
            //
            // This used to read "FightSession.Begin() is where this belongs and
            // it has NO production caller", and called PrepareEnemyIntents
            // itself as the narrow fix -- deliberately narrow, because Begin
            // also grants a turn start and auto-resolves enemy turns, and that
            // was judged too large a change to make inside a UI one.
            //
            // The judgement was sound and the missing caller was the whole bug:
            // with nothing resolving an enemy that won initiative, a fight that
            // opened on a monster's turn never gave the player one. FightBootstrap
            // calls Begin now and Begin telegraphs turn one, so committing them
            // again here would be a second home for the same concern -- and a
            // Bind that quietly repairs half an opening is what let the missing
            // Begin go unnoticed for as long as it did.
            RefreshCommandColumn();
            RefreshUi();
            AnnounceBossIfAny();
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
            WirePartyBuffIcons();
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
        }

        private void WirePlayback()
        {
            if (beatPlayer == null) return;

            beatPlayer.SlotFor = SlotFor;
            beatPlayer.PaintVitals = PaintVitals;
            beatPlayer.PushLine = PushLogLine;
            beatPlayer.SetStance = PoseCombatant;
            beatPlayer.FlashTarget = FlashCombatant;
            beatPlayer.PlayVfx = PlaySpellVfx;
            beatPlayer.PlayContactFx = PlayContactFx;
            beatPlayer.FadeTheFallen = FadeTheFallen;
            beatPlayer.ImpactDelayFor = ImpactDelayFor;
            beatPlayer.StopVfx = StopSpellVfx;
            beatPlayer.ShakeStage = ShakeStage;
        }

        // Kicks both stage racks. Playback decides how hard; the view decides
        // what "the stage" means, which is exactly the split every other
        // delegate on beatPlayer already draws.
        private void ShakeStage(float strength)
        {
            if (stageShakes == null) return;

            foreach (var shake in stageShakes) shake?.Kick(strength);
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
            if (spellVfxPlayers != null)
            {
                foreach (var player in spellVfxPlayers)
                {
                    if (player != null) player.StopImmediately();
                }
            }

            // The ground layer clears with them. It is a separate node with a
            // separate lifetime, so an abandoned fight would otherwise leave a
            // fault frozen mid-rupture behind an empty stage -- the exact case
            // FightBeatPlayer.Flush calls this for.
            if (spellGroundVfxPlayer != null) spellGroundVfxPlayer.StopImmediately();
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
            ApplySubmenuScroll();

            // The header sits on the container now rather than on the top row,
            // so it no longer moves with the count -- but it is still written
            // here, because the container's height is what it is derived from
            // and that is a layout number rather than a constant.
            float headerY = FightSubmenuLayout.HeaderY(shown);
            MoveToY(submenuTitle, headerY);
            MoveToY(submenuHint, headerY);
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

            float windowHalf = FightSubmenuLayout.ViewportHeight * 0.5f;
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
