using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace
{
    // Putting the actors on the stage: which sprite, which way round, and how
    // far off the floor.
    //
    // Ported from v1's FightController.StageVisuals.cs. The two hard-won rules
    // travel with it, because both were playtest bugs that read as art problems:
    // the ground line comes from the MANIFEST rather than from measuring alpha,
    // and the shadow's X is measured from the IDLE frame only.
    public partial class FightController
    {
        // The pose each combatant is currently holding. Playback writes it;
        // every repaint reads it.
        private readonly Dictionary<CombatantState, string> _stance = new Dictionary<CombatantState, string>();

        // Who has actually been SHOWN dying, as of the beats painted so far --
        // not who Domain already knows is dead. FightSession resolves a whole
        // round synchronously before any beat reaches the view, so by the time
        // beat 1 starts playing, CombatantState.IsAlive already reflects
        // whatever beat 4 or 5 does to that combatant. StanceOf used to read
        // IsAlive directly, which put a character in the Defeated pose from the
        // very first frame of the round if anything later in that same round
        // killed them -- "before the turn is done, with all phases, Shawn
        // already downs." FadeTheFallen already solved this exact class of bug
        // for the death fade by keying off beat.Snapshot instead of live
        // health; this is the same fix applied to the pose.
        private readonly HashSet<CombatantState> _confirmedDefeated = new HashSet<CombatantState>();

        // WHOSE CORPSE HAS EXPLICITLY REPORTED ITSELF GONE -- the one true
        // source HoldsRank reads, in place of asking a StageDeathFade's own
        // Faded flag live.
        //
        // THE RACE THIS REPLACES (StageFormationTests.TheSurvivorClosesUp
        // OnlyOnceTheCorpseHasFinishedFading, hunt 2026-09-22): HoldsRank
        // used to read fade.Faded directly, and RefreshStage is reached
        // from every ordinary beat's PaintVitals as well as from a fade's
        // own Finished callback. Both are coroutines on different
        // MonoBehaviours, and Unity gives no ordering guarantee between two
        // different components' coroutines resuming on the same frame -- so
        // an ordinary beat's repaint could, on the exact frame a fade
        // crossed its own finish line, observe Faded already true and close
        // ranks a frame ahead of the fade's own notification. A HitStrength
        // pass that shortened a nearby beat's dwell made that coincidence
        // land often enough to be caught; the coincidence was always
        // possible.
        //
        // Written ONLY by OnCorpseFadeFinished (the fade's own explicit
        // notification) and cleared only by a revival, so no OTHER caller
        // can ever flip a corpse's rank-holding status by asking the wrong
        // question at the wrong moment.
        private readonly HashSet<CombatantState> _corpseGone = new HashSet<CombatantState>();

        // WHO HAS ACTUALLY ARRIVED, as against who the model already contains.
        //
        // The exact mirror of _confirmedDefeated, and it exists for the mirror
        // bug. A round resolves in full before a single beat plays, so by the
        // time playback opens its FIRST beat the Warden's Roar has already run
        // in the model and the rat it called is already in Encounter.Enemies.
        // The stage draws from that list, so the rat walked on at the top of
        // the round and the roar that summoned it happened several beats later
        // -- reported from play as "the rat spawns before the roar".
        //
        // Filled from the beat snapshots by PaintVitals, which is the only
        // thing that sees a moment rather than the present: a combatant in a
        // played beat's snapshot existed at that beat, and one that is not did
        // not exist yet.
        private readonly HashSet<CombatantState> _confirmedPresent = new HashSet<CombatantState>();

        // Same busy/idle split StanceOf draws, and for the same reason: while a
        // round is playing only what a beat has confirmed counts, and the rest
        // of the time there is no in-flight animation to get ahead of.
        private bool IsOnStage(CombatantState combatant) =>
            combatant != null && (!_isBusy || _confirmedPresent.Contains(combatant));

        // Everyone a beat's snapshot mentions has, by definition, arrived.
        internal void ConfirmPresent(IReadOnlyDictionary<CombatantState, Vitals> vitals)
        {
            if (vitals == null) return;
            foreach (var pair in vitals) _confirmedPresent.Add(pair.Key);
        }

        // Horizontal centring is MEASURED rather than authored, unlike the
        // ground line -- see RefreshCombatantSprite. Cached because it opens a
        // sprite's pixels, which is far too expensive to do per repaint.
        private static readonly Dictionary<string, float> ContentCentreCache = new Dictionary<string, float>();

        // ---- the whole stage --------------------------------------------------

        // WHICH MOMENT THE STAGE IS DRAWING: the formation a beat recorded, or
        // null for "whatever is live".
        //
        // The same split StanceOf and FadeTheFallen already draw, applied to
        // position. A round resolves in one pass before a single beat plays,
        // so Encounter.PlayerParty read during playback is the order the round
        // FINISHED on -- and with Move in the game that is a different order
        // from the one most of its beats happened in. Set at the top of each
        // beat by playback and cleared when playback ends, at which point live
        // state is the moment being shown and is correct again.
        private BeatFormation _playingFormation;

        internal void PaintFormation(BeatFormation formation)
        {
            _playingFormation = formation;
            RefreshStage();
        }

        // Whether anybody is still walking to a new mark. Playback holds the
        // next beat on this: an enemy that swings while the party is halfway
        // through a swap aims at neither of them (FightBeatPlayer.TravelFor
        // reads the slot's live anchoredPosition), and the blow lands on a
        // figure still sliding out from under it.
        private bool FormationIsMoving() =>
            AnyGliding(enemyActorAnimators) || AnyGliding(partyActorAnimators);

        private static bool AnyGliding(StageActorAnimator[] animators)
        {
            if (animators == null) return false;

            foreach (var animator in animators)
            {
                if (animator != null && animator.IsGliding) return true;
            }

            return false;
        }

        // The order a side stood in for the moment being drawn. The
        // recorded-or-live rule, including what an empty recording falls back
        // to and why, lives in CombatBeat.QueueToShow.
        private IReadOnlyList<CombatantState> OrderOf(bool playerSide)
        {
            return CombatBeat.QueueToShow(
                _playingFormation?.SideOf(playerSide),
                playerSide ? _session.Encounter.PlayerParty : _session.Encounter.Enemies);
        }

        // The party's seat list for the same moment, from the SAME source
        // OrderOf picked (a recorded formation with a party, else live), so
        // the order and the seats can never come from two different beats.
        private IReadOnlyList<CombatantState> PartyFieldToShow()
        {
            if (_playingFormation != null && _playingFormation.Party.Count > 0) return _playingFormation.PartyField;

            return _session.Encounter.PartyField;
        }

        // WHETHER THIS FIGURE IS STILL ON ITS FEET, as far as the stage knows.
        //
        // Busy-aware, and that is the whole of it: while a round plays only
        // what a painted beat has confirmed counts, because live IsAlive
        // already reflects a kill from later in the same round. Idle, there is
        // no in-flight animation to get ahead of and live state is the answer.
        // Extracted because three separate readers now ask it -- StanceOf, the
        // rank rule below, and the revival check.
        private bool IsStanding(CombatantState combatant)
        {
            if (combatant == null) return false;

            return _isBusy ? !_confirmedDefeated.Contains(combatant) : combatant.IsAlive;
        }

        // WHETHER THIS FIGURE STILL TAKES UP A PLACE IN THE LINE.
        //
        // The living do. So does a body that has not finished fading, and that
        // clause is the one doing the work: ranks compress behind a corpse the
        // instant it dies, so a stage that took its ranks straight from the
        // living would slide the survivors forward on the very frame the blow
        // landed -- through a figure still standing there at full opacity,
        // before its death fade has so much as started. Holding the rank until
        // StageDeathFade says the body is gone is what makes the kill read as
        // "it falls, it fades, THEN the line closes up".
        private bool HoldsRank(CombatantState combatant)
        {
            if (!IsOnStage(combatant)) return false;
            if (IsStanding(combatant)) return true;

            // _corpseGone, not fade.Faded read live -- see its own header
            // for the race asking the fade directly used to open.
            return !_corpseGone.Contains(combatant);
        }

        // Reused across repaints rather than allocated per call: RefreshStage
        // is reached from every pose change of every beat, and these are three
        // entries each.
        private readonly List<CombatantState> _ranked = new List<CombatantState>();
        private CombatantState[] _occupants = new CombatantState[0];

        private void RefreshStage()
        {
            if (_session == null) return;

            var enemies = OrderOf(playerSide: false);

            DrawSide(enemies, enemySlots, enemySprites, enemyActorAnimators, enemyHitFlashes,
                     enemyNameplates, _enemySlotPlaced, StageSide.Right, mirrored: false, StageScaleFor,
                     _enemyMarks);

            RefreshIntentIcons();

            // NOT WHILE BUSY (C1's review). This method is reached from
            // PaintVitals on every beat of a round, and CombatBeat carries no
            // status snapshot -- painting the status row off LIVE state here
            // would show a status a LATER beat in the same round already
            // applied, several beats before the one actually landing it has
            // played. RefreshUi (called once at OnPlaybackFinished, and on
            // every input event -- both always with _isBusy already false) is
            // the only path allowed to repaint this row; while busy it simply
            // keeps showing whatever it last painted, exactly like the
            // plates/verbs/submenu already do for the same reason.
            if (!_isBusy) RefreshEnemyStatusRows();

            DrawSide(OrderOf(playerSide: true), partySlots, partySprites, partyActorAnimators,
                     partyHitFlashes, partyNameplates, _partySlotPlaced, StageSide.Left,
                     mirrored: true, null, field: PartyFieldToShow());
        }

        // ONE SIDE: where each figure stands, and what it is wearing.
        //
        // THE TWO HALVES ARE INDEXED DIFFERENTLY AND THAT IS THE POINT.
        // Position is walked by RANK -- rank 0 gets the near mark whoever is
        // standing in it -- while the drawing, the nameplate, the flash and
        // the fade are walked by SLOT, which belongs to one combatant for the
        // whole fight. A Move changes the first and must never touch the
        // second; before the slot map existed they were the same loop, so it
        // changed both and the enemy's already-queued swing flashed the figure
        // that had stepped out of the way.
        private void DrawSide(IReadOnlyList<CombatantState> order,
                              RectTransform[] slots, Image[] sprites, StageActorAnimator[] animators,
                              StageHitFlash[] flashes, TMPro.TMP_Text[] nameplates, bool[] placed,
                              StageSide side, bool mirrored, System.Func<CombatantState, float> presence,
                              Vector2[] marks = null, IReadOnlyList<CombatantState> field = null)
        {
            if (slots == null) return;

            // ---- where they stand, by rank or by seat ------------------------
            //
            // TWO RULES FOR TWO SIDES (PLAN_BELLWETHER_KIT 1.1/R1). The enemy
            // side has no seats: rank r of the n still holding a place, spread
            // over the whole line, as it always was. The PARTY side stands in
            // seats of three -- `field` is its seat list -- so a duo's second
            // member is drawn in the middle rather than at the far end, and a
            // lone Shawn who stepped back is drawn in the seat he stepped to.
            // A full party with no empty seat draws exactly as rank r of 3.
            // HoldsRank is the "holds a place" rule for both, so a corpse
            // keeps its seat (and the line waits) until its fade has finished.
            _ranked.Clear();
            for (int i = 0; i < order.Count; i++)
            {
                if (HoldsRank(order[i])) _ranked.Add(order[i]);
            }

            int count = _ranked.Count > slots.Length ? slots.Length : _ranked.Count;
            if (field != null) count = System.Math.Min(CombatEncounter.SeatsPerSide, slots.Length);

            for (int held = 0; held < _ranked.Count; held++)
            {
                int rank = field != null ? FieldSeating.SeatIn(field, _ranked[held], HoldsRank) : held;
                if (rank < 0 || rank >= count) continue;

                int slot = SlotIndexOf(_ranked[held]);
                if (slot < 0 || slot >= slots.Length || slots[slot] == null) continue;

                var offset = FightStageAnchors.SlotOffset(rank, count, mirrored);
                float scale = FightStageAnchors.SlotScale(rank, count)
                              * (presence?.Invoke(_ranked[held]) ?? 1f);

                var mark = new Vector2(offset.X, offset.Y);

                // RECORDED, so the enemy status strip can follow the figure
                // rather than re-deriving where it thinks the figure went.
                // That row is baked at build time against the fixed
                // three-slot geometry and has to be nudged by the difference
                // (RefreshEnemyStatusRows); it used to compute that
                // difference from the slot INDEX, which was the same number
                // as the rank until this file stopped treating them as one.
                if (marks != null && slot < marks.Length) marks[slot] = mark;

                AnchorOne(slots[slot],
                          animators != null && slot < animators.Length ? animators[slot] : null,
                          mark, new Vector3(scale, scale, 1f), placed, slot);

                // THE PAINTER'S ORDER, now that a figure can change rank.
                // FightScreen.BuildStage declares the slots far-to-near so
                // slot 0 draws last, which was the whole answer while a slot's
                // rank never changed. A Move swaps two ranks without moving
                // either figure's slot, so the nearer one has to be re-parented
                // to the end or the back rank draws over the front one.
                // Compared before assigning: SetSiblingIndex dirties the
                // hierarchy, and this runs on every repaint.
                int sibling = StageLayout.SiblingIndexForSlot(rank, count);
                if (slots[slot].GetSiblingIndex() != sibling) slots[slot].SetSiblingIndex(sibling);
            }

            // ---- what they are wearing, by slot -----------------------------
            if (_occupants.Length < slots.Length) _occupants = new CombatantState[slots.Length];
            for (int slot = 0; slot < slots.Length; slot++) _occupants[slot] = null;

            for (int i = 0; i < order.Count; i++)
            {
                if (!IsOnStage(order[i])) continue;

                int slot = SlotIndexOf(order[i]);
                if (slot >= 0 && slot < slots.Length) _occupants[slot] = order[i];
            }

            for (int slot = 0; slot < slots.Length; slot++)
            {
                if (slots[slot] == null) continue;

                var combatant = _occupants[slot];
                slots[slot].gameObject.SetShown(combatant != null);
                if (combatant == null) continue;

                RaiseIfStandingAgain(combatant);

                if (sprites != null && slot < sprites.Length)
                {
                    RefreshCombatantSprite(sprites[slot], combatant, side, StanceOf(combatant),
                        animators != null && slot < animators.Length ? animators[slot] : null,
                        flashes != null && slot < flashes.Length ? flashes[slot] : null);
                }

                if (nameplates != null && slot < nameplates.Length)
                {
                    RefreshNameplate(nameplates[slot], combatant);
                }
            }
        }

        // BACK ON ITS FEET AFTER HAVING BEEN SHOWN DOWN -- Second Life, which
        // raises the WHOLE party at the moment the fight would have been lost
        // (FightSession.Outcome.TrySecondLife).
        //
        // The beat-driven half of this lives in FadeTheFallen, which sees the
        // revival in a snapshot. This is the other half, for a raise that
        // lands after the last beat was committed: playback is over, live
        // state is the moment being shown, and a party left faded out would
        // fight the rest of the encounter invisible. Idle only, for exactly
        // the reason StanceOf is busy-aware -- mid-round, live health has
        // already run past the beat being drawn.
        private void RaiseIfStandingAgain(CombatantState combatant)
        {
            if (_isBusy || !combatant.IsAlive) return;
            if (!_confirmedDefeated.Remove(combatant)) return;

            // See the identical line in FadeTheFallen's own revival branch.
            _corpseGone.Remove(combatant);

            DeathFadeFor(combatant)?.ResetToVisible();
        }

        // What pose a combatant is holding. Public so a PlayMode test can assert
        // the round ended idle rather than stuck on an attack frame -- there is
        // no other way to see a stance from outside, because the visible result
        // is a sprite that may not exist for a combatant with no authored art.
        public string StanceFor(CombatantState combatant) => StanceOf(combatant);

        private string StanceOf(CombatantState combatant)
        {
            if (combatant != null)
            {
                // WHILE A ROUND IS PLAYING, only what has actually been
                // confirmed by a painted beat counts -- that is the whole
                // fix. The rest of the time (idle between actions, or a test
                // that pokes CurrentHealth directly with no beat involved at
                // all) there is no in-flight animation to get ahead of, so
                // falling back to live IsAlive is correct and is what lets a
                // combatant killed outside the beat pipeline still show
                // defeated once refreshed.
                if (!IsStanding(combatant)) return FightSession.Stances.Defeated;
            }

            return _stance.TryGetValue(combatant, out var stance) ? stance : FightSession.Stances.Idle;
        }

        // ONE SLOT PUT ON ONE MARK, at the size that mark implies.
        //
        // Spreads however many actors are ACTUALLY on this side across the
        // whole depth range, instead of filling the first N of three fixed
        // slots. The bug that fixes, measured rather than eyeballed: two Giant
        // Rats sat in slots 0 and 1 of a three-slot formation, 132px apart,
        // while slot 2 -- the widest position, 265px out -- stood empty. The
        // rat sheet is 675px wide, so the back one was 78% hidden behind the
        // front one and read as one monster with a spare tail. Spreading the
        // pair to the two ENDS of the same range takes that to 48% with no
        // change to the anchors themselves, which are load-bearing for panel
        // clearance and were derived against the tallest actor (see
        // FightStageAnchors).
        //
        // Runtime rather than build-time for the same reason the submenu rows
        // are: how many monsters a room fields is not known until it is
        // entered. It goes through the SAME FightStageAnchors functions the
        // builder used, so the two can never disagree about what "slot 1 of 2"
        // means.
        //
        // WAS A LOOP OVER SLOTS, and is now a call per RANK, which is the
        // whole of A3's positional half: the caller decides which combatant
        // holds which rank for the beat being drawn and this puts that
        // combatant's own slot on the mark that rank implies. While rank and
        // slot were the same number the difference did not exist.
        private static void AnchorOne(RectTransform slot, StageActorAnimator animator,
                                      Vector2 mark, Vector3 baseScale, bool[] placed, int index)
        {
            if (slot == null) return;

            // ONLY WHEN IT ACTUALLY MOVED, and that guard is the whole of this
            // function's correctness.
            //
            // RefreshStage is not an occasional event -- the HUD refresh and
            // every pose change reach it. Writing the mark unconditionally
            // therefore fought the lunge tween for the rect all the way
            // through the swing, and telling the animator unconditionally
            // CANCELLED that tween outright, since Rehome stops whatever is in
            // flight. The result was an attack where nobody moved.
            //
            // Compared against the ANIMATOR'S mark, never the live rect:
            // mid-lunge the rect is somewhere between here and the target by
            // design, so reading it back would see a difference every frame and
            // re-home forever. Against its GOAL rather than its current mark,
            // because a walk in flight is between the two -- see
            // StageActorAnimator.Mark. Without an animator there is nothing to
            // move it, so the rect IS the mark.
            var currentMark = animator != null ? animator.Mark : slot.anchoredPosition;
            var currentScale = animator != null ? animator.GoalScale : slot.localScale;

            bool arrived = (currentMark - mark).sqrMagnitude < 0.0001f
                           && (currentScale - baseScale).sqrMagnitude < 0.0001f;

            // FIRST PLACEMENT SNAPS, EVERY LATER ONE WALKS. Setting a stage up
            // is not a movement anybody is meant to watch -- figures sliding in
            // from wherever the previous encounter left the slot would open
            // every fight with the party wandering into position -- where a
            // Move, or a line closing up over a corpse, is exactly the
            // movement the player is meant to read.
            bool first = placed == null || index < 0 || index >= placed.Length || !placed[index];
            if (placed != null && index >= 0 && index < placed.Length) placed[index] = true;

            if (arrived) return;

            // THE ANIMATOR HAS TO BE TOLD, and telling it is the whole of the
            // write. It holds the mark a figure returns to after a lunge and
            // the scale its stretch multiplies onto, and it captured both in
            // Awake -- which was correct only while a slot's position was fixed
            // at build time. Without this every swing after a re-spread ended
            // by snapping the figure back to where its slot used to be, at the
            // size it used to be.
            //
            // ASSIGNING THE RECT HERE AS WELL USED TO BE PART OF IT, and it is
            // not any more. That wrote the two values and then called a Rehome
            // that read them straight back, which worked only because nothing
            // else wrote localScale between the two lines. The idle breath
            // writes it every frame, so that arrangement would have folded a
            // breath into the base scale and multiplied it again on the next
            // one. Handing the values over leaves one writer, which cannot be
            // got out of order.
            if (animator == null)
            {
                slot.anchoredPosition = mark;
                slot.localScale = baseScale;
                return;
            }

            if (first) animator.Rehome(mark, baseScale);
            else animator.GlideTo(mark, baseScale,
                                  FightBeatPlayer.Scaled(StageActorAnimator.GlideSeconds));
        }

        // THE SAME NAME THE PLATE IN THE CORNER USES, ordinal and all. Three
        // rats labelled "Giant Rat" under their feet and "Giant Rat 2" in the
        // list is a screen disagreeing with itself about which one is which,
        // which is worse than not numbering them at all.
        private void RefreshNameplate(TMPro.TMP_Text plate, CombatantState combatant)
        {
            if (plate == null) return;

            plate.SetContent(DisplayNameOf(combatant));
        }

        // Over the side the combatant is on. Numbering across both armies would
        // make Shawn "Shawn" and a summoned Shawn "Shawn 2" -- correct, and not
        // a case that exists -- while costing every lookup a second list walk.
        private string DisplayNameOf(CombatantState combatant)
        {
            if (_session == null || combatant == null) return combatant?.Name ?? "";

            var side = combatant.IsPlayerSide
                ? _session.Encounter.PlayerParty
                : _session.Encounter.Enemies;

            return FightHudModel.DisplayNameOf(side, combatant);
        }

        // ---- enemy intent icons -------------------------------------------------

        private TMPro.TMP_Text[] _intentGlyphs;
        private Image[] _intentImages;

        // Resources.Load every frame would be wasteful and, worse, hides a
        // missing file behind a per-frame retry. Resolved once per kind,
        // through the same IconCache<TKind> StatusSprites in
        // FightController.Hud.cs now uses -- see that type's own header for
        // the one deliberate behavioural difference between the two icon
        // systems (this one disables its Image on a miss; that one restores
        // a captured default sprite instead).
        private static readonly IconCache<EnemyIntentKind> IntentSprites = new IconCache<EnemyIntentKind>();

        private static Sprite IntentSpriteFor(EnemyIntentKind kind) =>
            IntentSprites.Resolve(kind, Domain.Combat.Session.EnemyIntentIcons.ResourceFor);

        // Called once, from Start. The caption of a button is a child the
        // emitter synthesises and holds no NodeRef for, so it is found here
        // rather than wired -- see the field's own comment.
        private void WireIntentIcons()
        {
            if (enemyIntentIcons == null) return;

            _intentGlyphs = new TMPro.TMP_Text[enemyIntentIcons.Length];
            _intentImages = new Image[enemyIntentIcons.Length];
            for (int i = 0; i < enemyIntentIcons.Length; i++)
            {
                var icon = enemyIntentIcons[i];
                if (icon == null) continue;

                // BY NAME: the badge now also holds its number and callout
                // (enemyIntentValues/Callouts), so "the first TMP under it" is
                // no longer the caption. The emitter names that "<name>Label".
                _intentGlyphs[i] = icon.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: true)
                    .FirstOrDefault(t => t.name == icon.name + "Label");
                _intentImages[i] = icon.GetComponent<Image>();

                var hover = icon.GetComponent<HoverIndex>();
                if (hover == null) hover = icon.AddComponent<HoverIndex>();
                hover.Index = i;
                hover.Changed = OnHoverIndex;
            }

            intentTooltip.SetShown(false);
        }

        // Shown ONLY while the player's turn is the one on screen, which is the
        // same rule TelegraphSuffix documents and for the same reason: once a
        // round is resolving, the committed intent already belongs to the NEXT
        // turn, so drawing it during playback telegraphs the wrong turn.
        private void RefreshIntentIcons()
        {
            if (enemyIntentIcons == null || _session == null) return;

            // Bind() runs before Start() when the adapter builds the fight, so
            // the first paint arrived with the glyph refs still unresolved and
            // every icon switched on carrying no character at all. Resolving on
            // demand makes the two call orders commutative, which is the same
            // fix RefreshStage's own comment describes for the party art map.
            if (_intentGlyphs == null) WireIntentIcons();

            var enemies = _session.Encounter.Enemies;
            bool readable = _session.IsPlayerTurn && !_isBusy && !_session.IsOver;

            RefreshSeatFigures(readable);

            for (int i = 0; i < enemyIntentIcons.Length; i++)
            {
                var enemy = i < enemies.Count ? enemies[i] : null;
                var intent = enemy != null && enemy.IsAlive && readable
                    ? _session.IntentDetailFor(enemy)
                    : null;

                enemyIntentIcons[i].SetShown(intent.HasValue);
                if (!intent.HasValue) continue;

                var kind = intent.Value.Kind;
                var art = IntentSpriteFor(kind);

                PlaceIntentBadge(enemyIntentIcons[i], enemy);

                if (_intentImages != null && _intentImages[i] != null)
                {
                    _intentImages[i].color = Hex(Domain.Combat.Session.EnemyIntentIcons.TintFor(kind));
                    // Nothing to draw is worse than a plain plate: an Image with
                    // no sprite paints a filled RECTANGLE, which is the white
                    // quad this project has hunted eleven times. IconCache<TKind>.
                    // Apply's `disableOnMiss: true` is what enforces that here.
                    IconCache<EnemyIntentKind>.Apply(_intentImages[i], art, disableOnMiss: true);
                }

                // The three-letter word only when the art did not load, so the
                // badge degrades to something readable instead of to nothing.
                if (_intentGlyphs != null && _intentGlyphs[i] != null)
                {
                    _intentGlyphs[i].SetContent(art == null
                        ? Domain.Combat.Session.EnemyIntentIcons.For(kind)
                        : "");
                }

                PaintIntentReading(i, enemy, intent.Value);
            }
        }

        // THE NUMBER, THE LETHAL STYLE AND THE CALLOUT (PLAN_BELLWETHER_KIT
        // 3.8), all read off the LIVE intent, so a Move or a free Palace
        // Passage re-reads them on the next refresh without the monster
        // choosing again. Lethal tints the icon and its number alike.
        private void PaintIntentReading(int i, CombatantState enemy, EnemyIntent intent)
        {
            bool lethal = intent.IsLethal;
            if (lethal && _intentImages != null && _intentImages[i] != null)
            {
                _intentImages[i].color = Hex(Domain.UiKit.FightHudPalette.IntentLethal);
            }

            var value = enemyIntentValues != null && i < enemyIntentValues.Length ? enemyIntentValues[i] : null;
            if (value != null)
            {
                bool shows = !intent.Heals && intent.ExpectedDamage > 0;
                value.gameObject.SetActive(shows);
                if (shows)
                {
                    value.SetContent(intent.ExpectedDamage.ToString());
                    value.color = Hex(lethal ? Domain.UiKit.FightHudPalette.IntentLethal : Domain.UiKit.FightHudPalette.IntentNumber);
                }
            }

            var callout = enemyIntentCallouts != null && i < enemyIntentCallouts.Length ? enemyIntentCallouts[i] : null;
            var plate = enemyIntentCalloutPlates != null && i < enemyIntentCalloutPlates.Length
                ? enemyIntentCalloutPlates[i]
                : null;
            if (callout != null)
            {
                bool telegraphs = intent.Then != null || intent.DamageBySeat != null;
                string line = telegraphs ? _session.TelegraphLine(enemy) : "";
                callout.gameObject.SetActive(line.Length > 0);
                if (plate != null) plate.SetShown(line.Length > 0);
                if (line.Length > 0)
                {
                    callout.SetContent(line);
                    callout.color = Hex(lethal ? Domain.UiKit.FightHudPalette.IntentLethal : Domain.UiKit.FightHudPalette.IntentNumber);
                    FitCalloutPlate(plate, callout);
                }
            }
        }

        // THE PLATE HUGS ITS TEXT: authored at its widest (FightScreen's
        // CalloutPlateW), narrowed to the line's own width plus a margin so a
        // short "Dark Chains! Death Knell next" is not a long empty bar.
        private static void FitCalloutPlate(GameObject plate, TMPro.TMP_Text callout)
        {
            if (plate == null || callout == null) return;

            var rect = (RectTransform)plate.transform;
            var labelRect = (RectTransform)callout.transform;
            float widest = labelRect.rect.width + 16f;
            float wanted = Mathf.Min(widest, callout.GetPreferredValues(callout.text).x + 36f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(80f, wanted));
        }

        // THE KNELL'S FLOOR FIGURES (PLAN_BELLWETHER_KIT M5): what a
        // seat-sized intent deals at each seat, on the floor under it --
        // lethal red, survivable lilac, SAFE green -- on the badge's own
        // readability rule, so they come and go with it. The seat the target
        // stands in now is drawn a size larger.
        private void RefreshSeatFigures(bool readable)
        {
            if (partySeatFigures == null) return;

            var figures = Domain.Combat.Session.FightHudModel.SeatFiguresFor(_session, readable);
            for (int seat = 0; seat < partySeatFigures.Length; seat++)
            {
                var node = partySeatFigures[seat];
                if (node == null) continue;

                var figure = seat < figures.Length ? figures[seat] : default;
                node.SetShown(figure.Shown);
                if (!figure.Shown) continue;

                var label = partySeatFigureLabels != null && seat < partySeatFigureLabels.Length
                    ? partySeatFigureLabels[seat]
                    : null;
                if (label != null)
                {
                    label.SetContent(figure.Text);
                    label.color = Hex(SeatFigureHex(figure.Style));
                    label.transform.localScale = Vector3.one * (figure.IsTargetSeat ? 1.2f : 1f);
                }
            }
        }

        private static string SeatFigureHex(Domain.Combat.Session.FightHudModel.SeatFigureStyle style)
        {
            switch (style)
            {
                case Domain.Combat.Session.FightHudModel.SeatFigureStyle.Lethal:
                    return Domain.UiKit.FightHudPalette.IntentLethal;
                case Domain.Combat.Session.FightHudModel.SeatFigureStyle.Safe:
                    return Domain.UiKit.FightHudPalette.SeatFigureSafe;
                default:
                    return Domain.UiKit.FightHudPalette.IntentKnell;
            }
        }

        // Sits the badge just above the monster's ACTUAL head.
        //
        // The build pins it to the slot's top edge, which is the sprite FRAME's
        // top -- fine for the rat, whose frame hugs its pose, and badly wrong for
        // the golem, whose frame is cut for a taller pose and left its badge
        // floating in open sky. Measured content beats declared canvas, the same
        // conclusion the foot shadow reached for X.
        //
        // Slot-local y = 0 IS the ground line: the slot's pivot is bottom-centre
        // and GroundTheFigure nudges the sprite down by the authored drop, so the
        // two coincide by construction.
        private void PlaceIntentBadge(GameObject badge, CombatantState enemy)
        {
            if (badge == null) return;

            string folder = SpriteFolderFor(enemy);
            if (string.IsNullOrWhiteSpace(folder)) return;

            float top = ContentTopForActor(folder);
            if (top <= 0f) return;

            float drop = StanceManifestLoader.Manifest.GroundLineFor(folder);
            var rect = (RectTransform)badge.transform;

            // Anchored to the slot's own centre so this y is measured from the
            // ground line, not from whichever edge the build happened to pin to.
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            // The badge's BOTTOM edge clears the head, not its centre -- half the
            // icon is added on top of the gap. Measured headroom differs wildly
            // between actors (17px on the rat's frame, 123 on the golem's), so
            // getting this half-height wrong is invisible on a tight frame and
            // parks the badge on a golem's shoulder.
            rect.anchoredPosition = new Vector2(0f,
                top - drop + FightStageAnchors.IntentIconOffset + FightStageAnchors.IntentIconSize * 0.5f);
        }

        // THE ICON OVER A MONSTER'S HEAD IS HOW A POINTER REACHES THE MONSTER
        // (2026-09-19). The figure's own hit area is live ONLY while a target
        // is being picked -- RefreshEnemyPlates raises it for that window and
        // takes it down again, deliberately, so an idle stage takes no clicks
        // -- which left the badge row under its feet as the only always-live
        // node on an enemy, and those badges are 36px. The intent icon is
        // always up during the player's turn and stands ON the figure, so it
        // answers "what is on this thing" as well as "what is it about to
        // do": the status box opens for the same enemy this tooltip
        // describes.
        //
        // AND THE TWO CANNOT LAND ON EACH OTHER, which is worth stating
        // because it is an argument rather than a distance. The intent
        // tooltip is pinned upper-left at a fixed spot
        // (FightScreen.BuildIntentTooltip) while the box hangs under a figure
        // on the stage, so for an enemy they are nowhere near each other. The
        // one geometry that WOULD cross it is a full-height box flipped above
        // a near PARTY figure -- and that needs the box to be describing a
        // party member while this tooltip describes an enemy, which takes two
        // pointers: the mouse is what opens this, and opening it names its
        // own enemy to the box (above), which outranks the pad's focus.
        // Unreachable, so there is no stacking rule to write.
        private void OnHoverIndex(int index, bool entered)
        {
            InspectEnemyAt(index, entered);

            ShowIntentTooltip(index, entered);
        }

        private void ShowIntentTooltip(int index, bool entered)
        {

            if (intentTooltip == null) return;

            if (!entered)
            {
                intentTooltip.SetShown(false);
                return;
            }

            var enemies = _session?.Encounter.Enemies;
            var enemy = enemies != null && index < enemies.Count ? enemies[index] : null;
            var intent = enemy == null ? null : _session.IntentDetailFor(enemy);
            if (!intent.HasValue)
            {
                intentTooltip.SetShown(false);
                return;
            }

            if (intentTooltipText != null)
            {
                intentTooltipText.SetContent(
                    FightHudModel.IntentTooltip(DisplayNameOf(enemy), intent.Value));
            }

            intentTooltip.SetShown(true);
        }

        // ---- one combatant ----------------------------------------------------

        private void RefreshCombatantSprite(Image image, CombatantState combatant, StageSide side, string stance,
                                            StageActorAnimator animator, StageHitFlash hitFlash)
        {
            if (image == null) return;

            var slotRect = image.transform.parent as RectTransform;
            string folder = SpriteFolderFor(combatant);

            // BEFORE THE FALLBACK BRANCH BELOW, which returns early: an
            // animator left holding the PREVIOUS occupant's folder would
            // measure a stand-off against art this figure is not wearing.
            // Handing over the folder of a combatant with no art at all is
            // harmless -- SpanForStance resolves nothing and falls through to
            // whatever Image is bound, which is the plate.
            animator?.BindArt(folder);

            var sprite = StanceSpriteFor(combatant, stance);

            if (sprite == null)
            {
                ShowFallbackPlate(image, slotRect, hitFlash);
                return;
            }

            image.sprite = sprite;
            image.preserveAspect = true;
            if (slotRect != null) slotRect.sizeDelta = sprite.rect.size;

            // Mirror only when the art's authored facing disagrees with the side
            // it stands on, so a combatant always looks ACROSS the stage at the
            // opposition rather than off the edge of it.
            float mirror = StageFacing.MirrorScaleX(FacingOf(combatant), side);
            image.rectTransform.localScale = new Vector3(mirror, 1f, 1f);

            GroundTheFigure(image, slotRect, folder);
            PlaceShadow(slotRect, folder, mirror);

            // Synced AFTER the mirror and carrying it: the flash overlay is a
            // SIBLING, so it does not inherit the sprite's flip. Without this the
            // silhouette faced the opposite way to the figure -- invisible on
            // anything unmirrored (Shawn faces right and stands left, so he
            // looked correct by luck) and obvious on every enemy.
            SyncHitFlash(image, sprite, mirror, hitFlash);

            // The afterimage copies this exact Image, so the animator is handed
            // it through the same door -- once bound it clones the live node
            // whenever it trails, picking up the current drawing and flip for
            // free. Same-sprite rebinds are cheap and idempotent. HANDED IN
            // rather than GetComponent'd off slotRect -- the same array
            // ScreenRegistry populated for AnchorOne.
            animator?.BindSprite(image);

            // ACTIVATED, not merely enabled. The sprite node is built inactive
            // (it has no art until a fight exists), and enabling a Graphic whose
            // GameObject is inactive draws exactly nothing -- which is why the
            // stage showed foot glows hovering over an empty forest while every
            // headless assertion about "the sprite loaded" passed.
            image.gameObject.SetShown(true);
            image.enabled = true;
        }

        // No authored art: fall back to the plain plate so the slot still reads
        // as a real combatant rather than vanishing or showing a white box.
        private void ShowFallbackPlate(Image image, RectTransform slotRect, StageHitFlash hitFlash)
        {
            image.sprite = enemyFallbackSprite;
            image.preserveAspect = false;
            if (slotRect != null) slotRect.sizeDelta = new Vector2(320f, 120f);

            // The plate has no feet to ground, and this branch returns early --
            // so the grounding nudge never runs to undo itself. Cleared here, or
            // a slot that fell back AFTER showing real art would keep the last
            // pose's drop.
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;

            // Never mirrored: it is a UI frame, not a character, and flipping it
            // would just reverse its bevel lighting for no gain.
            image.rectTransform.localScale = Vector3.one;
            SyncHitFlash(image, enemyFallbackSprite, 1f, hitFlash);

            // Stays off if even the fallback is missing -- an invisible slot
            // beats a solid white rectangle.
            ShowSprite(image, true);
        }

        // THE GROUND LINE IS THE SLOT'S BOTTOM, and this is what puts each
        // drawing's feet on it.
        //
        // Delivered art does not put the feet on the canvas bottom. Pinning the
        // raw canvas to the ground line made a figure JUMP as it changed pose --
        // 52px for the golem going idle->attack, which is the "golem flies
        // upwards in its attack" playtest report.
        //
        // READ FROM THE MANIFEST, never measured. The runtime used to scan the
        // art's alpha and was wrong twice over: the golem's slam erupts an earth
        // spike ~50px below its own feet and Shawn's idle plants a staff ~33px
        // below his, so both were read as the floor and both figures were
        // hoisted into the air. A kit states where its feet are, which is the
        // one thing a runtime measurement can never report -- it just quietly
        // believes whatever it finds.
        //
        // ONE value per actor rather than per stance, and that is load-bearing:
        // the bug being designed out is a figure MOVING VERTICALLY between
        // poses, and a constant offset cannot do that by construction, where a
        // recomputed one merely usually doesn't. It is only meaningful because
        // every one of an actor's drawings sits on one shared canvas --
        // EnemyStanceCaptureTests.EveryStanceOfAnActorSharesOneCanvas is what
        // keeps that true.
        private static void GroundTheFigure(Image image, RectTransform slotRect, string folder)
        {
            if (slotRect == null) return;

            // The manifest speaks in pixels of the sprite's own canvas, and the
            // slot is sized to exactly that canvas -- so the number carries
            // across with no conversion. Scaling by the slot is still what
            // applies stage depth to it.
            float drop = StanceManifestLoader.Manifest.GroundLineFor(folder);

            // The sprite is anchor-stretched across the slot, so it is nudged
            // with offsetMin/Max rather than anchoredPosition. THE SAME VALUE
            // ON BOTH, which is what makes this a translation rather than a
            // resize.
            image.rectTransform.offsetMin = new Vector2(0f, -drop);
            image.rectTransform.offsetMax = new Vector2(0f, -drop);
        }

        // The ring stays ON the ground line. Only its X needs correcting, for art
        // whose figure is not centred in its own canvas, and that is read from
        // the IDLE drawing so a pose that swings an arm out cannot drag the ring
        // sideways with it.
        //
        // STILL MEASURED, unlike the ground line. Horizontal centring has never
        // been implicated in a bug -- an arm's width either side of centre moves
        // a ring by a few pixels, where a mistaken FLOOR moves an entire creature
        // off the stage -- so it keeps the cheaper arrangement rather than four
        // more authored numbers.
        private static void PlaceShadow(RectTransform slotRect, string folder, float mirror)
        {
            if (slotRect == null || slotRect.childCount == 0) return;
            if (!(slotRect.GetChild(0) is RectTransform shadowRect)) return;

            float centre = ContentCentreFractionForActor(folder);
            shadowRect.anchoredPosition = new Vector2(centre * slotRect.sizeDelta.x * mirror, 0f);
        }

        // TEST SEAM (C3, PartyFormationCaptureTests/StageFormationTests):
        // where the ring OUGHT to sit on the X axis for a combatant's
        // CURRENTLY SHOWN sprite, converted into the same "stage pixels"
        // space those fixtures already read StanceSpriteFor's rendered
        // bounds in (RectTransformUtility.CalculateRelativeRectTransformBounds
        // against the root canvas). Reuses the exact folder lookup and
        // fraction PlaceShadow itself consults (SpriteFolderFor,
        // ContentCentreFractionForActor) so a test cannot silently check
        // against a different actor's manifest entry than production reads.
        // `mirrorSign` is read off the sprite's own rectTransform.localScale.x
        // by the caller -- RefreshCombatantSprite writes exactly that value,
        // so this needs no second copy of StageFacing's mirroring rule.
        public float ExpectedRingCentreXForTest(CombatantState combatant, Rect spriteRectStagePixels, float mirrorSign)
        {
            string folder = SpriteFolderFor(combatant);
            float fraction = ContentCentreFractionForActor(folder);
            float centreX = spriteRectStagePixels.x + spriteRectStagePixels.width * 0.5f;
            return centreX + fraction * spriteRectStagePixels.width * mirrorSign;
        }

        // HANDED IN rather than found with GetComponentInChildren off the
        // slot -- the array ScreenRegistry populated one-to-one with the
        // slots is the same lookup, done once at build time instead of once
        // per repaint.
        private static void SyncHitFlash(Image sprite, Sprite art, float mirror, StageHitFlash flash)
        {
            if (flash == null) return;

            flash.SetSprite(art);
            if (flash.transform is RectTransform flashRect)
            {
                flashRect.localScale = new Vector3(mirror, 1f, 1f);

                // AND THE GROUND OFFSET, exactly as the dissolve layer below
                // copies it. The flash is a stretch sibling of the sprite, so
                // it inherits the slot's box for free -- but GroundTheFigure
                // writes the figure's drop and drift onto the SPRITE's own
                // offsets, and a silhouette that does not carry them whitens a
                // figure standing somewhere else. On Shawn the hurt drawing's
                // feet sit 41px higher on its canvas than idle's, so the flash
                // floated up by that much: head and hands went white, boots
                // and cloak-hem stayed in colour, and the flash read as a
                // different pose laid over the one he was in. Same failure the
                // mirror line above already fixed on the other axis.
                flashRect.offsetMin = sprite.rectTransform.offsetMin;
                flashRect.offsetMax = sprite.rectTransform.offsetMax;
            }
            flash.image.preserveAspect = sprite.preserveAspect;
        }

        // ---- what playback drives -----------------------------------------------

        // Poses a combatant and repaints. Called once per combatant per beat, so
        // the repaint is the whole stage rather than one slot -- at three actors
        // a side that is cheaper than working out which slot changed.
        private void PoseCombatant(CombatantState combatant, string stance)
        {
            if (combatant == null) return;

            _stance[combatant] = stance;
            RefreshStage();
        }

        // BECOMING SOMETHING ELSE, on the frame the beat says so.
        //
        // Three things at once, and they are one event rather than three: the
        // figure starts resolving its stances out of the new actor's folder,
        // the repaint re-lays it out against that actor's own canvas and
        // ground line (the ram is not Shawn's 540x370 and its feet are not
        // where his are), and a white silhouette of the RESULT flashes over
        // the swap so the change reads as a change rather than as a sprite
        // popping. "He should flash and then become the black ram."
        //
        // THE FLASH IS THE ORDINARY IMPACT ONE, not a new tempo. Flash() is
        // already white and untinted -- FlashHeal is the one that carries a
        // colour -- and the hold-and-fade it uses is long enough to cover the
        // swap. It goes through StageHitFlash directly rather than through
        // FlashCombatant, which is guarded on the beat having landed damage
        // and would also want a damage popup; a transformation lands nothing
        // and pops no number.
        //
        // NO ART, NO CHANGE. A form folder that is not on disk leaves the
        // figure in its own skin and says so once, which is the house posture
        // everywhere else -- a Black Ram that has not been drawn yet should
        // look like Shawn with better numbers, not like a nameplate.
        internal void WearForm(CombatantState combatant, string folder)
        {
            if (combatant == null) return;

            string current = SpriteFolderFor(combatant);
            bool clearing = string.IsNullOrWhiteSpace(folder);

            if (!clearing && StanceAnimationLibrary.Resolve(folder, FightSession.Stances.Idle) == null)
            {
                Debug.LogWarning($"[stage] {combatant.Name} transforms into '{folder}', which has no idle " +
                                 "drawing under Resources -- keeping their own art.");
                return;
            }

            if (clearing) _form.Remove(combatant);
            else _form[combatant] = folder;

            // Nothing actually changed -- a transform that only moves numbers,
            // or a re-entry into the form already worn. The stage has nothing
            // to redraw and nothing to punctuate.
            if (SpriteFolderFor(combatant) == current) return;

            RefreshStage();
            HitFlashFor(combatant)?.Flash();
        }

        // BACK TO WHOEVER THEY ACTUALLY ARE, once the round has finished
        // playing. The twin of PaintFormation(null) and PaintTurnOrder(null),
        // called from the same two places and for the same reason: playback is
        // over, so live state is the moment on screen.
        //
        // It is also the whole of how a transformation ENDS on the stage.
        // TickTransform expires it at the holder's turn start, which is not an
        // action, opens no beat and therefore has nothing to record the change
        // on -- so the revert lands here, at the end of the round the timer ran
        // out in. Quietly, with no flash: he was the ram for a while and now
        // he is not, and punctuating that would claim something happened on a
        // beat where nothing did.
        internal void ResyncForms()
        {
            if (_session == null) return;

            bool changed = false;

            foreach (var combatant in _session.Encounter.PlayerParty.Concat(_session.Encounter.Enemies))
            {
                string live = combatant?.Transformation?.SpritePath;
                bool worn = _form.ContainsKey(combatant);

                if (string.IsNullOrWhiteSpace(live))
                {
                    changed |= worn && _form.Remove(combatant);
                    continue;
                }

                // Same missing-art rule as WearForm's: a form nothing can draw
                // is not worn, here or there.
                if (StanceAnimationLibrary.Resolve(live, FightSession.Stances.Idle) == null) continue;

                if (worn && _form[combatant] == live) continue;

                _form[combatant] = live;
                changed = true;
            }

            if (changed) RefreshStage();
        }

        // Fades out whoever this beat left on the floor.
        //
        // Driven off the beat's SNAPSHOT rather than off live health: by the
        // time a beat plays, later actions in the same round have already moved
        // the real numbers, so a combatant who dies on beat 3 would otherwise
        // start fading on beat 1.
        private void FadeTheFallen(CombatBeat beat)
        {
            if (beat?.Snapshot == null || _session == null) return;

            bool raised = false;

            foreach (var pair in beat.Snapshot)
            {
                if (pair.Value.Health > 0)
                {
                    // AND THE FALLEN WHO ARE NOT FALLEN ANY MORE. Second Life
                    // raises the WHOLE party at the moment the fight would
                    // otherwise be lost (FightSession.Outcome.TrySecondLife),
                    // so a snapshot showing health where the last one showed a
                    // body IS the revival, and this is the only place that
                    // reads snapshots in order. Without it the raised party
                    // fought the rest of the encounter as invisible corpses:
                    // _confirmedDefeated never emptied and the fade never
                    // came back.
                    if (!_confirmedDefeated.Remove(pair.Key)) continue;

                    // The rank-holding half of the same revival -- a raised
                    // combatant must be able to hold its rank on a LATER
                    // death, and _corpseGone from a previous life would
                    // otherwise still say "gone" for a body that is standing
                    // again.
                    _corpseGone.Remove(pair.Key);

                    DeathFadeFor(pair.Key)?.ResetToVisible();
                    raised = true;
                    continue;
                }

                // The pose's own confirmation, alongside the fade's. Same
                // beat-scoped moment, same reason: this is the first point
                // "actually dead" is allowed to become visible.
                // The RETURN of Add is the "first time we have seen this
                // corpse" signal: a body is in the snapshot of every beat after
                // the one that killed it, so everything here is asked
                // repeatedly.
                _confirmedDefeated.Add(pair.Key);

                // PlayIfNotAlready, not Play: a corpse is in the snapshot of
                // every beat after the one that killed it, so this is asked
                // repeatedly and must only ever fade once.
                //
                // Finished IS RE-BOUND HERE, EVERY TIME, rather than once at
                // fight start: the fight-start binding (see the reset loop
                // below) cannot close over WHICH combatant will die in a
                // given slot, because it does not know yet -- a slot is
                // reused across the fight (revival, the next encounter) and
                // its occupant at death time is the one fact this call site
                // actually has. PlayIfNotAlready's own no-op-after-first
                // guard means a repeated rebind of an ALREADY-fading corpse
                // is harmless: the coroutine it would otherwise restart
                // never starts a second time.
                var fade = DeathFadeFor(pair.Key);
                if (fade != null)
                {
                    var corpse = pair.Key;
                    fade.Finished = () => OnCorpseFadeFinished(corpse);
                    fade.PlayIfNotAlready();
                }
            }

            // The raised are back in the line, so the line has to be re-laid
            // around them. Nothing else repaints between here and the next
            // beat, and a revival is the one case where every figure on a side
            // changes rank at once.
            if (raised) RefreshStage();
        }

        // A BODY HAS FINISHED FADING, so the survivors close up.
        //
        // The whole of the compaction rule's timing, and it is deliberately
        // the fade that decides rather than the beat: the kill, the fall and
        // the fade are nearly a second apart, and sliding the line forward at
        // either of the first two would walk a figure through a body still on
        // screen. See HoldsRank and _corpseGone's own headers for why THIS
        // is the one and only writer of _corpseGone -- an ordinary beat's
        // repaint must never be able to reach the same conclusion on its
        // own by reading the fade's state instead of being told.
        private void OnCorpseFadeFinished(CombatantState corpse)
        {
            if (_session == null) return;

            _corpseGone.Add(corpse);
            RefreshStage();
        }

        private static bool[] NewPlacedFlags(RectTransform[] slots) =>
            new bool[slots == null ? 0 : slots.Length];

        // Whether each slot has been put on a mark at least once this fight --
        // the difference between standing a stage up and moving somebody.
        private bool[] _enemySlotPlaced = new bool[0];
        private bool[] _partySlotPlaced = new bool[0];

        // Where the last repaint actually put each enemy figure. Read by the
        // status strip, which hangs off the same position but is not a child
        // of the slot -- see DrawSide.
        private Vector2[] _enemyMarks = new Vector2[0];

        // ---- the breath between blows --------------------------------------------

        // NOBODY BREATHES BY THEMSELVES. Every stance in the game is a single
        // drawing, so a stage between blows is six figures standing perfectly
        // still -- which reads as a paused game rather than as a fight waiting
        // on the player. The whole of the motion is a transform swell; see
        // Domain/Stage/BreathCurve for why that beat sheets of drawn frames.
        //
        // IT LIVES HERE RATHER THAN IN PLAYBACK because it is the opposite
        // shape from everything in that class: it belongs to no beat, it has
        // to run while the game sits waiting for a click, and it never ends.
        private Coroutine _idling;

        // THE TEST SEAM FOR THE BREATH SPECIFICALLY, separate from
        // FightBeatPlayer.BeatSpeedMultiplier on purpose. BreatheIdle's own
        // comment explains why the breath clock is UNSCALED by that
        // multiplier -- a paused fight must not bank up a breath -- so
        // speeding the fight's beats up for a test does nothing to how long
        // the ~2.8s breath cycle (BreathCurve) takes in real time. Four
        // PlayMode tests (StageAnimationTests: FlatArtStillBreathesEven
        // ThoughItsDrawingCannot, ABreathOnlyEverMakesTheFigureTallerThanIts
        // Mark, ReHomingAFigureMidBreathDoesNotFoldTheBreathIntoItsSize,
        // APunchStillLandsOnAFigureThatIsBreathing) were each waiting out a
        // real ~3s deadline for exactly that reason. Set in [UnitySetUp],
        // reset in [UnityTearDown], same shape as BeatSpeedMultiplier.
        public static float BreathSpeedMultiplier = 1f;

        // Where each figure is in its own breath. Cleared per combatant the
        // moment it stops being idle, so the breath restarts from rest on the
        // way back rather than resuming mid-inhale from before the blow.
        private readonly Dictionary<CombatantState, float> _idleClock =
            new Dictionary<CombatantState, float>();

        private void StartIdleBreathing()
        {
            // Cleared on every fight, because the keys are CombatantStates and
            // the next encounter's are different objects -- the old entries
            // would otherwise sit here for the session's life.
            _idleClock.Clear();
            _hoverClock.Clear();

            ResumeIdleBreathing();
        }

        // THE HANDLE IS NOT THE COROUTINE (hunt 2026-09-11, scenario B1).
        //
        // Unity stops every coroutine on a MonoBehaviour the moment it is
        // disabled, and it does not resume them on the way back -- but the
        // Coroutine object this held stayed non-null through all of it. So
        // the guard below read "already breathing" about a coroutine that had
        // been dead since the disable, and the stage stood perfectly still
        // for the rest of the fight. The pair of Unity messages underneath is
        // the other half: Bind was the only caller, so even with the handle
        // nulled nothing would have asked for the breath back.
        //
        // Split from StartIdleBreathing rather than folded into it because
        // the two clocks must be cleared per FIGHT and not per enable -- a
        // re-enable rejoins the fight it left, and clearing them there would
        // be a correct-looking line that quietly restarts every figure's
        // breath from rest on a click that has nothing to do with the fight.
        private void ResumeIdleBreathing()
        {
            if (_idling != null || !isActiveAndEnabled) return;

            _idling = StartCoroutine(IdleBreathing());
        }

        // Fires before Start and before any Bind on the first enable, which
        // costs nothing: IdleBreathing's loop skips every frame while there
        // is no session, and Bind still clears the clocks when one arrives.
        private void OnEnable()
        {
            ResumeIdleBreathing();
        }

        private void OnDisable()
        {
            // Unity has already stopped it; what is left to do is drop the
            // handle, so the guard above can tell "running" from "was".
            if (_idling == null) return;

            StopCoroutine(_idling);
            _idling = null;
        }

        // ONE COROUTINE FOR THE WHOLE STAGE, not one per figure.
        //
        // Per-figure handles would need starting and stopping on every pose
        // change, on every death, on every reset, and on a combatant that
        // joins mid-fight (the Warden's Roar summons rats). Reading the stance
        // each frame instead means the rule is stated once and cannot fall out
        // of step: whatever is idle, breathes.
        //
        // That rule is also what keeps this off playback's toes. A figure being
        // driven by a beat is in `attack`, `cast`, `hurt` or `defeated`, never
        // in `idle`, so the two can never write one figure's transform at once.
        private IEnumerator IdleBreathing()
        {
            while (true)
            {
                yield return null;

                if (_session == null) continue;

                var enemies = _session.Encounter.Enemies;
                for (int i = 0; i < enemies.Count; i++) BreatheIdle(enemies[i], i);

                var party = _session.Encounter.PlayerParty;
                for (int i = 0; i < party.Count; i++) BreatheIdle(party[i], i);
            }
        }

        // Hands one figure's breath to the thing that wears it.
        //
        // The animator lives on the SLOT, which is what carries the depth
        // scale and the bottom pivot -- so a breath scales about the figure's
        // own ground line and cannot lift it off the floor. Same component,
        // same reasoning, as the lunge and the recoil.
        //
        // NO REPAINT. The breath is a transform write, so nothing about the
        // picture RefreshStage paints has changed; calling it per figure per
        // frame is what the old sprite-swapping loop had to do and this does
        // not.
        //
        // Silent when there is no slot or no animator: a combatant that is not
        // on stage (a summon still being held back by _confirmedPresent) has
        // nothing to breathe, and that is not an error worth a branch upstream.
        private void BreatheFigure(CombatantState combatant, float amount)
        {
            AnimatorFor(combatant)?.SetBreath(amount);
        }

        // A flyer's own clock, separate from the breath's because it does not
        // stop when the breath does: the breath is idle-only, the hover is
        // every stance but defeated.
        private readonly Dictionary<CombatantState, float> _hoverClock =
            new Dictionary<CombatantState, float>();

        // Advances one idle figure's breath, and one flyer's hover.
        private void BreatheIdle(CombatantState combatant, int index)
        {
            if (combatant == null) return;

            // BEFORE THE IDLE GATE BELOW, because a flyer rides its hover in
            // every stance but defeated -- Odette is airborne while she
            // casts, while she is hit and while she lunges (the animator
            // composes the two, see StageActorAnimator.WritePosition). Only
            // the fallen touch the floor, and they do so at once: the drop is
            // the knock-down, not a glide.
            HoverIdle(combatant, index);

            // StanceOf, not _stance, so a corpse is excluded by the same rule
            // the rest of the stage reads it by -- including the beat-confirmed
            // one, which is what stops a body twitching between the blow that
            // killed it and the pose that says so.
            if (StanceOf(combatant) != FightSession.Stances.Idle)
            {
                _idleClock.Remove(combatant);
                BreatheFigure(combatant, 0f);
                return;
            }

            // NO ART, NO BREATH. A figure with no drawing is a fallback plate,
            // and a UI frame that swells and settles reads as a rendering
            // fault rather than as a creature.
            if (StanceSpriteFor(combatant, FightSession.Stances.Idle) == null)
            {
                BreatheFigure(combatant, 0f);
                return;
            }

            if (!_idleClock.TryGetValue(combatant, out float clock))
            {
                clock = BreathCurve.PhaseFor(index);
            }

            // UNSCALED BY BeatSpeedMultiplier AND BY Time.timeScale, on purpose
            // and unlike the glide, punch and kick, which run on
            // Time.deltaTime since 1822a059 so the pause stops them. An idle
            // breath and a hover are ambient, not part of the action, so the
            // battle-speed preset leaves them alone (PLAN_BATTLE_SPEED
            // contract 9); and a fight paused behind a modal should not bank
            // up a breath and spend it all at once when the panel closes.
            // Scaled by BreathSpeedMultiplier instead, which is 1 outside a
            // test and therefore changes nothing about that rule in play.
            clock += Time.unscaledDeltaTime * BreathSpeedMultiplier;
            _idleClock[combatant] = clock;

            BreatheFigure(combatant,
                BreathCurve.At(clock,
                               StanceManifestLoader.Manifest.BreathFor(SpriteFolderFor(combatant))));
        }

        private void HoverIdle(CombatantState combatant, int index)
        {
            var spec = StanceManifestLoader.Manifest.HoverFor(SpriteFolderFor(combatant));
            if (!spec.IsAirborne) return;

            if (StanceOf(combatant) == FightSession.Stances.Defeated)
            {
                _hoverClock.Remove(combatant);
                HoverFigure(combatant, 0f);
                return;
            }

            if (!_hoverClock.TryGetValue(combatant, out float clock))
            {
                clock = BreathCurve.PhaseFor(index);
            }

            // Same clock rules as the breath: unscaled by BeatSpeedMultiplier,
            // collapsed by BreathSpeedMultiplier in a test.
            clock += Time.unscaledDeltaTime * BreathSpeedMultiplier;
            _hoverClock[combatant] = clock;

            HoverFigure(combatant, HoverCurve.At(clock, spec));
        }

        private void HoverFigure(CombatantState combatant, float pixels)
        {
            AnimatorFor(combatant)?.SetHover(pixels);
        }

        // Puts every figure back for a fresh encounter. The fade is the reason
        // this has to exist: an actor left at zero alpha would begin the next
        // fight invisible, and its slot is reused rather than rebuilt.
        private void ResetStagePresentation()
        {
            // Both of these are `static readonly Dictionary`s keyed by folder
            // -- they cache a PIXEL MEASUREMENT taken the
            // first time each key is asked for, and nothing ever invalidated
            // them. Resources.Load happily picks up a re-sliced/re-ordered
            // sprite the moment its .meta reimports, but these dictionaries
            // do not know that happened -- a script recompile clears them
            // (new static instances on domain reload), but an ASSET-ONLY
            // edit (repainting or re-slicing an enemy's stances, exactly
            // what iterating on stage art actually is) does not
            // recompile anything, so a long-lived Editor session can carry
            // a stale measurement for hours after the art it was taken from
            // is gone. Symptom: a frame-drift fix that is provably correct
            // on disk (measured directly off the delivered PNGs) still
            // wobbles on stage, because PlaceShadow and the intent badge
            // are both still positioning off the OLD numbers. Clearing once
            // per fight costs a few pixel-scans (a handful of enemies, one
            // drawing each) against never risking this again.
            ContentCentreCache.Clear();
            ClearOpaqueBoxCache();
            ClearVfxPaddingCache();

            _confirmedDefeated.Clear();

            // AND NOBODY IS WEARING ANYBODY ELSE'S SKIN. A transformation
            // belongs to the encounter it was cast in; carrying one across
            // would open the next fight with a combatant drawn as a form
            // whose Transformation object no longer exists.
            _form.Clear();

            // AND NOBODY IS STILL POSED FROM THE LAST FIGHT. Keyed by
            // CombatantState like _form and _confirmedDefeated, so the same
            // rule applies: the next encounter's instances are different
            // objects, and an uncleared entry here would just leak for the
            // session's life rather than affect anything visible this fight.
            _stance.Clear();

            // WHO OWNS WHICH SLOT, for the whole of this fight -- see the map's
            // own header. Seeded here rather than in Bind because this is the
            // one method that means "a new encounter is standing up", and the
            // two facts it establishes (who is where, and nobody has moved yet)
            // are the same fact.
            BindSlots();

            // Nobody has been put on a mark yet, so the first anchor of each
            // slot snaps rather than walking -- see AnchorOne.
            _enemySlotPlaced = NewPlacedFlags(enemySlots);
            _partySlotPlaced = NewPlacedFlags(partySlots);
            _enemyMarks = new Vector2[enemySlots == null ? 0 : enemySlots.Length];

            // AND THE BEAT BEING DRAWN IS "none": a formation left over from
            // the last encounter names combatants that no longer exist, and
            // OrderOf would draw it. Its twin for the turn queue goes with it,
            // for the same reason and against the same failure -- a tracker
            // opening a fresh fight with the last fight's dead monsters in it.
            _playingFormation = null;
            _playingTurnOrder = null;

            // C4: no holder from a fight that just ended can ever be
            // compared against again -- a fresh Bind means every combatant's
            // remembered status-code set starts empty, exactly as a fresh
            // WireAllStatusBadges always left the (then row-keyed) dictionary
            // for a new fight anyway.
            _statusRowActiveCodes.Clear();

            // AND NOBODY IS BEING INSPECTED. The same reasoning one line up,
            // against a worse failure: a held reference to a combatant from
            // the fight that just ended would keep the status box open over
            // the new encounter, describing a monster that is not there.
            _inspectedActor = null;
            _inspectedCode = null;
            _inspectedFrom = InspectSource.None;
            _inspectedFromIndex = -1;
            if (statusBox != null) statusBox.SetShown(false);

            // SEEDED WITH WHOEVER IS ALREADY HERE. The reveal rule only ever
            // has to hold back a monster that arrives DURING a round; the
            // opening roster is on stage before the first beat exists, and
            // waiting for a snapshot to say so would blank the stage for the
            // frame between the player acting and playback starting.
            _confirmedPresent.Clear();
            if (_session != null)
            {
                foreach (var enemy in _session.Encounter.Enemies) _confirmedPresent.Add(enemy);
                foreach (var member in _session.Encounter.PlayerParty) _confirmedPresent.Add(member);
            }

            StartIdleBreathing();

            // _corpseGone TOO -- a fresh fight reuses these same slot
            // components, and a combatant object from the LAST encounter
            // (a fixture reusing CombatantState instances, or simply the
            // same reference by coincidence) must not open this one already
            // marked gone.
            _corpseGone.Clear();

            foreach (var fade in enemyDeathFades.Concat(partyDeathFades))
            {
                if (fade == null) continue;

                fade.ResetToVisible();

                // NOT BOUND HERE ANY MORE. Finished used to be assigned once
                // per slot, right here, with no idea which combatant would
                // eventually die in it -- FadeTheFallen now rebinds it at
                // the moment a specific combatant's fade actually starts
                // (its own comment says why), which is the only place that
                // knows who. Left unbound, a fade that somehow ran with no
                // rebind (should not happen -- PlayIfNotAlready has exactly
                // one call site, and it always rebinds first) simply
                // finishes in silence rather than throwing on a null
                // corpse.
            }

            foreach (var animator in enemyActorAnimators.Concat(partyActorAnimators)) animator?.ResetToHome();

            // The racks too, for the same reason the figures are: a kick
            // interrupted by a fight ending would leave the whole stage parked
            // a few pixels off for the next encounter, and nothing else writes
            // that position.
            if (stageShakes != null)
            {
                foreach (var shake in stageShakes) shake?.ResetToHome();
            }
        }

        private void FlashCombatant(CombatBeat beat)
        {
            if (beat == null) return;

            // EVERY ENEMY A SWEEP LANDED ON, each judged on its OWN amount. The
            // beat-wide Amount holds the largest single hit, so testing it once
            // would flash an enemy that dodged the same cast the two beside it
            // took -- see BeatTargetResult.
            if (beat.HasPerTargetResults)
            {
                foreach (var result in beat.Results)
                {
                    if (result.Amount > 0) FlashOne(beat, result.Target);
                }

                return;
            }

            // Only a beat that actually LANDED something flashes -- OR that
            // a shield ate, which is what Absorbed on its own (Amount <= 0)
            // now also has to arm this: a Ward status's full absorb leaves
            // Amount at its unwritten 0 (CombatBeat.Absorbed's own header),
            // and a shield taking a whole blow is exactly the kind of "you
            // were hit" this gate exists to let through, not filter out. A
            // beat that recorded neither is a hold-back, a refusal or a
            // status tick, and flashing those would make the signal mean
            // nothing.
            if (beat.Amount <= 0 && beat.Absorbed <= 0) return;

            FlashOne(beat, beat.Target);
        }

        // Read off the BEAT, not off the target's health -- by the time this
        // plays, live health has already moved through the rest of the round.
        private void FlashOne(CombatBeat beat, CombatantState target)
        {
            var flash = HitFlashFor(target);
            if (flash == null) return;

            // A SHIELD TOOK SOME OR ALL OF IT -- the barrier reaction,
            // distinct from the ordinary hit flash (FightHudPalette.
            // WardBright rather than white/typed/heal). What actually
            // reached health is Amount less Absorbed, CLAMPED: a
            // Ward-status full absorb can leave Amount at 0 while Absorbed
            // is positive (see CombatBeat.Absorbed's own header), which
            // would read as negative health damage without the clamp.
            if (beat.Absorbed > 0)
            {
                int healthDamage = beat.Amount - beat.Absorbed;
                if (healthDamage < 0) healthDamage = 0;

                if (healthDamage <= 0)
                {
                    // FULLY ABSORBED: the barrier is the whole story. No
                    // hit flash underneath it, matching HitStrength.Tier.
                    // None's "no slide either" on the recoil side.
                    flash.FlashBarrier();
                    return;
                }

                // PARTIALLY ABSORBED: the blow still landed for something,
                // so it still opens with its own flash -- the barrier
                // trails it rather than replacing it.
                flash.FlashPartiallyWarded(beat.DamageType);
                return;
            }

            // AND THE ELEMENT OFF THE BEAT, for the same reason the amount is
            // read off it: a poison tick's beat declares Poison (see
            // CombatBeat.DeclareDamageType), and everything else carries
            // whatever AfterResolution painted from the actor's own attack
            // type -- which is Physical, and still flashes white, for every
            // ordinary swing.
            if (beat.IsHealing) flash.FlashHeal();
            else flash.Flash(beat.DamageType);
        }

        // ---- resolving art from content ----------------------------------------

        // WHICH FORM EACH COMBATANT IS CURRENTLY DRAWN AS -- a transformation's
        // own stance folder, absent for everyone wearing their own art, which
        // is everyone nearly all of the time.
        //
        // Written by playback at the impact instant of the beat that recorded
        // the change (WearForm) and rewritten wholesale from live state when
        // playback ends (ResyncForms). Never read from Transformation directly
        // during a round: the whole round has already resolved by then, so the
        // ram would be on screen from the first frame of the beat that
        // summons it. See CombatBeat.Forms.
        private readonly Dictionary<CombatantState, string> _form =
            new Dictionary<CombatantState, string>();

        // THE ONE SEAM THAT ANSWERS "whose art is this combatant drawn from".
        //
        // Every other question the stage asks -- which sprite for this stance,
        // where its feet are, how tall its canvas is, how far it breathes,
        // whether it hovers, where its intent badge and its foot ring go --
        // is asked of a FOLDER and reaches that folder through here. So a
        // transformation swaps this one answer and the whole figure changes
        // with it: the ram's own canvas, its own ground line, its own
        // manifest entry, and a hit-flash silhouette re-synced to whatever
        // drawing came out. Rewiring each of those readers instead would be
        // eleven call sites agreeing by hand.
        private string SpriteFolderFor(CombatantState combatant)
        {
            if (combatant != null && _form.TryGetValue(combatant, out var form)
                && !string.IsNullOrWhiteSpace(form))
            {
                return form;
            }

            var enemy = _session?.SourceFor(combatant);
            if (enemy != null) return enemy.Source.SpritePath;

            // Player art comes off the adapter's parallel array rather than the
            // kit: the kit is what COMBAT needs and art is not that. v1 kept both
            // in one dictionary, which is a large part of why its combat logic
            // could not leave the controller.
            return PortraitFolderFor(combatant);
        }

        // How big this particular monster should be drawn, on top of whatever
        // depth its rank implies.
        //
        // BY COMBATANT, where it used to be by slot index -- the caller walked
        // slots and read the roster back out at the same index, which is the
        // identity assumption A3 removed. Only enemies author one
        // (RawEnemyEntry.stageScale); the party rack passes no presence
        // function at all.
        private float StageScaleFor(CombatantState combatant) =>
            _session?.SourceFor(combatant)?.Source.StageScale ?? 1f;

        private SpriteFacing FacingOf(CombatantState combatant)
        {
            var enemy = _session?.SourceFor(combatant);
            if (enemy != null) return enemy.Source.Facing;

            // The party side: SourceFor is enemy-only (see FightSession.
            // Enemies.cs's own note on SourceFor(player) always being null),
            // so a party combatant's facing comes off its PlayerKit instead
            // -- ResolvedCharacter.BattleSpriteFacing, carried through
            // FightEncounterAdapter.KitFor exactly the way PlateArt already
            // is. This used to fall through to a hardcoded Right with a
            // comment claiming the fallback was unreachable for a
            // content-backed combatant; that was false for the whole party
            // (every one of them reaches here on every repaint) and only
            // read as correct because all three fielded characters happen to
            // be authored Right. KitFor's own facing default (also Right)
            // is what a kit with no content behind it -- or none at all --
            // now falls through to.
            return _session?.KitFor(combatant)?.Facing ?? SpriteFacing.Right;
        }

        // The one drawing this combatant is showing, falling back through
        // requested stance -> idle. A kit is allowed to be missing a pose (only
        // idle is really mandatory), and a monster frozen in the
        // wrong-but-present pose beats one that blinks out of existence
        // mid-fight.
        //
        // Public so a PlayMode test can assert a stance actually resolved: the
        // visible result of a miss is the fallback plate, which looks like a
        // layout choice rather than like missing art.
        public Sprite StanceSpriteFor(CombatantState combatant, string stance)
        {
            string folder = SpriteFolderFor(combatant);
            if (string.IsNullOrWhiteSpace(folder)) return null;

            var sprite = StanceAnimationLibrary.Resolve(folder, stance);
            if (sprite != null) return sprite;

            return stance == FightSession.Stances.Idle
                ? null
                : StanceAnimationLibrary.Resolve(folder, FightSession.Stances.Idle);
        }

        // C3 ROOT CAUSE, confirmed with a throwaway diagnostic PlayMode test
        // (Resources.Load("Characters/sheep/idle") + a raw GetPixels dump,
        // run against the real imported asset): `sprite.textureRect` is NOT
        // `sprite.rect`. Every stance PNG imports with Sprite Mesh Type
        // Tight, which crops the texture DATA Unity actually stores down to
        // the opaque bounding box -- for Shawn's idle, `rect` reports the
        // full authored 540x370 canvas but `textureRect` is (65, 6, 280,
        // 354), a sub-window well inside it. The measurement functions below
        // (both the old whole-silhouette one and this file's first C3 pass)
        // called `GetPixels(textureRect.x, textureRect.y, ...)` -- which
        // correctly reads the trimmed pixel data -- and then divided by
        // THAT SAME trimmed rect's own width to get a fraction, discarding
        // its `.x`/`.y` OFFSET within the untrimmed canvas entirely. A tight
        // crop's content fills its own crop edge-to-edge BY CONSTRUCTION, so
        // that fraction is always close to 0 no matter how far off-centre
        // the art actually sits on the canvas PlaceShadow positions the ring
        // against (`slotRect.sizeDelta = sprite.rect.size`, the UNTRIMMED
        // width) -- which is exactly the "the offset reads as barely
        // applied" symptom the capture showed. Confirmed numerically: the
        // old whole-silhouette scan against the trimmed rect measured
        // Shawn's fraction at ~0 (the shipped `footShadowAnchoredPosition.x
        // == -0.87` in tools/screenshots/runtime/party_formation/a3 and
        // /c3_after/slots.json, both captured before this fix); converting
        // the same scan's pixel coordinates back into canvas space before
        // dividing gives -0.121 (whole silhouette) / -0.133 (foot band) --
        // which is what an offline scan of the same idle.png on disk, with
        // no Unity import involved at all, independently gives too.
        //
        // THE FIX, IN ONE SENTENCE: every pixel coordinate GetPixels hands
        // back is LOCAL TO THE TRIMMED CROP, and has to be translated back
        // into canvas space (+ textureRect.x / .y) before it is compared
        // against a canvas-space quantity (the ground line) or divided by
        // the canvas's own width (sprite.rect.width, not textureRect.width).
        //
        // Was the whole silhouette's centroid (every opaque pixel, top to
        // bottom); now it is the FOOT BAND's alone -- the 24 texture rows
        // directly above the authored ground line. Even set aside from the
        // trimming bug, the whole-silhouette centroid was always the wrong
        // SPAN to average: a prop or a limb that never touches the ring's
        // own ground line (Shawn's staff, a flyer's trailing wingtip) drags
        // the measured centre away from where the FEET actually are.
        private const int FootBandHeight = 24;

        // The alpha floor both readers below compare against, scaled once to
        // the 0..255 byte range GetPixels32's Color32 buffer works in rather
        // than re-deriving it at each comparison site.
        private const float AlphaFloor = 0.02f;
        private const byte AlphaFloorByte = (byte)(AlphaFloor * 255f);

        private static float ContentCentreFractionForActor(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return 0f;
            if (ContentCentreCache.TryGetValue(folder, out var cached)) return cached;

            var idle = StanceAnimationLibrary.Resolve(folder, FightSession.Stances.Idle);
            float groundLine = StanceManifestLoader.Manifest.GroundLineFor(folder);
            float centre = FootBandCentreFraction(idle, groundLine);
            ContentCentreCache[folder] = centre;
            return centre;
        }

        // How far the FOOT BAND's opaque pixels' horizontal midpoint sits
        // from the canvas's, as a fraction of the FULL (untrimmed) canvas
        // width -- `sprite.rect`, the same one `slotRect.sizeDelta` reads,
        // never `sprite.textureRect`, which a Tight sprite mesh crops down
        // to the opaque bounding box (see this method's own header above
        // for the measured numbers that found this). The band is the
        // `FootBandHeight` rows starting at the ground line and reaching
        // up; the ground line is authored in canvas-bottom-relative pixels,
        // which is `textureRectOffset.y` rows above where GetPixels32's own
        // bottom-up row 0 sits, so that offset has to be subtracted before
        // the ground line means anything as an index into the pixels
        // GetPixels32 actually returns.
        private static float FootBandCentreFraction(Sprite sprite, float groundLine)
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable) return 0f;

            var textureRect = sprite.textureRect;
            int cropX = (int)textureRect.x;
            int cropY = (int)textureRect.y;
            int cropWidth = (int)textureRect.width;
            int cropHeight = (int)textureRect.height;
            if (cropWidth <= 0 || cropHeight <= 0) return 0f;

            float canvasWidth = sprite.rect.width;
            if (canvasWidth <= 0f) return 0f;

            // textureRect is where the crop sits in the TEXTURE (what the
            // pixel indexing below needs); textureRectOffset is where it sits
            // in the sprite's own CANVAS (what the ground line and the
            // returned fraction are measured in). They agree only while the
            // sprite is the whole texture -- a sub-rect sprite would read the
            // wrong band with textureRect alone. Same split OpaqueBox makes.
            var canvasOffset = sprite.textureRectOffset;
            int bandBottom = Mathf.Clamp(Mathf.RoundToInt(groundLine - canvasOffset.y), 0, Mathf.Max(cropHeight - 1, 0));
            int bandTop = Mathf.Clamp(bandBottom + FootBandHeight, bandBottom, cropHeight);

            // GetPixels32, NOT GetPixels -- a byte-per-channel buffer for an
            // alpha-only read. UNLIKE GetPixels, GetPixels32 has no
            // cropped-rect overload (Unity ships only
            // GetPixels32(int miplevel)), so this reads the WHOLE texture and
            // indexes at the crop's own texture-space offset (+cropX,
            // +cropY) rather than a crop-local one.
            var texture = sprite.texture;
            var pixels = texture.GetPixels32();
            int textureWidth = texture.width;
            int left = int.MaxValue;
            int right = int.MinValue;

            for (int y = bandBottom; y < bandTop; y++)
            {
                int rowStart = (cropY + y) * textureWidth + cropX;
                for (int x = 0; x < cropWidth; x++)
                {
                    if (pixels[rowStart + x].a <= AlphaFloorByte) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }

            if (left > right) return 0f;

            // BACK INTO CANVAS SPACE (+canvasOffset.x) before dividing by the canvas's
            // own width -- see this method's own header for why dividing by
            // the trimmed crop's width instead is the bug this replaces.
            float midpointCanvasX = canvasOffset.x + (left + right) * 0.5f;
            return midpointCanvasX / canvasWidth - 0.5f;
        }

        // How far the topmost opaque pixel of the IDLE pose sits above the
        // canvas's bottom, in canvas pixels. Cached per actor.
        //
        // This exists because a slot is sized to the sprite's CANVAS, and an
        // actor's canvas is cut to fit its tallest pose -- so an idle golem
        // leaves a great deal of empty space above its head. Pinning the intent
        // badge to the canvas top put the golem's badge 106px above it,
        // floating in open sky next to somebody else's HP plate, while the
        // rat's and the witch's looked fine. It read as a bug in the badge
        // rather than as the canvas being taller than the pose.
        //
        // Measured rather than authored, and from the IDLE pose only, for the
        // same reasons FootBandCentreFraction is: a pose that raises an arm must
        // not drag the badge up with it, and one more authored number per actor
        // is a number that can rot. The ground line stays authored -- that one is
        // load-bearing enough to be worth the manifest.
        //
        // CANVAS SPACE, via the one opaque-box scan the spell layer already
        // reads (OpaqueBoxForActor, FightController.SpellVfx). This used to be
        // its own scan that returned the first opaque row counted from the
        // Tight crop's bottom -- crop space -- while PlaceIntentBadge
        // subtracts the authored ground line, which is canvas space. Every
        // trimmed idle put the badge low by exactly its crop's Y offset, the
        // bug FootBandCentreFraction had in X (.claude/rules/ui.md "Sprites").
        // One scan with one crop-to-canvas mapping, rather than a second
        // copy of both.
        private static float ContentTopForActor(string folder)
        {
            var box = OpaqueBoxForActor(folder);
            return box.HasValue ? box.Value.Box.yMax : 0f;
        }

        // Public for the EditMode pin, for the reason every *ForTest seam on
        // this class is: InternalsVisibleTo names the Editor assembly only.
        public static float ContentTopForActorForTest(string folder) => ContentTopForActor(folder);

    }
}
