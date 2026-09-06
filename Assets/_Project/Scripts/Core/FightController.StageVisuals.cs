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
        private static readonly Dictionary<string, float> ContentTopCache = new Dictionary<string, float>();

        // ---- the whole stage --------------------------------------------------

        private void RefreshStage()
        {
            if (_session == null) return;

            var enemies = _session.Encounter.Enemies;

            // THE SHOWN COUNT, not the roster's. Spreading the formation for a
            // monster nobody can see yet would shuffle the survivors sideways
            // to make room for it, which gives the arrival away just as loudly
            // as drawing it early did.
            //
            // A hidden newcomer is always the LAST entry -- a summon appends to
            // the encounter -- so counting them is enough and no re-packing of
            // the slot indices is needed. Everything else on this stage maps a
            // combatant to a slot by its index in this same list (SlotFor, the
            // plates, the intent icons), and re-packing would have to move all
            // of them together.
            int onStage = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (IsOnStage(enemies[i])) onStage++;
            }

            AnchorStageSlots(enemySlots, enemyActorAnimators, onStage, mirrored: false, StageScaleForSlot);

            for (int i = 0; i < enemySprites.Length; i++)
            {
                var enemy = i < enemies.Count && IsOnStage(enemies[i]) ? enemies[i] : null;
                enemySlots[i].gameObject.SetShown(enemy != null);
                if (enemy == null) continue;

                RefreshCombatantSprite(enemySprites[i], enemy, StageSide.Right, StanceOf(enemy),
                    enemyActorAnimators[i], enemyHitFlashes[i]);
                RefreshNameplate(enemyNameplates[i], enemy);
            }

            RefreshIntentIcons();

            var party = _session.Encounter.PlayerParty;
            AnchorStageSlots(partySlots, partyActorAnimators, party.Count, mirrored: true);

            for (int i = 0; i < partySprites.Length; i++)
            {
                var member = i < party.Count ? party[i] : null;
                partySlots[i].gameObject.SetShown(member != null);
                if (member == null) continue;

                RefreshCombatantSprite(partySprites[i], member, StageSide.Left, StanceOf(member),
                    partyActorAnimators[i], partyHitFlashes[i]);
                RefreshNameplate(partyNameplates[i], member);
            }
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
                bool defeated = _isBusy ? _confirmedDefeated.Contains(combatant) : !combatant.IsAlive;
                if (defeated) return FightSession.Stances.Defeated;
            }

            return _stance.TryGetValue(combatant, out var stance) ? stance : FightSession.Stances.Idle;
        }

        // Spreads however many actors are ACTUALLY on this side across the whole
        // depth range, instead of filling the first N of three fixed slots.
        //
        // The bug this fixes, measured rather than eyeballed: two Giant Rats sat
        // in slots 0 and 1 of a three-slot formation, 132px apart, while slot 2
        // -- the widest position, 265px out -- stood empty. The rat sheet is
        // 675px wide, so the back one was 78% hidden behind the front one and
        // read as one monster with a spare tail. Spreading the pair to the two
        // ENDS of the same range takes that to 48% with no change to the anchors
        // themselves, which are load-bearing for panel clearance and were
        // derived against the tallest actor (see FightStageAnchors).
        //
        // Runtime rather than build-time for the same reason the submenu rows
        // are: how many monsters a room fields is not known until it is entered.
        // It goes through the SAME FightStageAnchors functions the builder used,
        // so the two can never disagree about what "slot 1 of 2" means.
        //
        // Applies to the PARTY too. A squad of two had exactly the same problem
        // and nobody had noticed, because two sheep overlapping reads as two
        // sheep standing close together rather than as a layout fault.
        private static void AnchorStageSlots(RectTransform[] slots, StageActorAnimator[] animators,
                                            int liveCount, bool mirrored,
                                            System.Func<int, float> presence = null)
        {
            if (slots == null || liveCount <= 0) return;

            int shown = liveCount > slots.Length ? slots.Length : liveCount;
            for (int i = 0; i < shown; i++)
            {
                if (slots[i] == null) continue;

                var offset = FightStageAnchors.SlotOffset(i, shown, mirrored);
                var mark = new Vector2(offset.X, offset.Y);

                // DEPTH FIRST, THEN THE CREATURE. The stage's own scale answers
                // "how far away is this slot"; the multiplier answers "how big
                // is the thing standing in it", and those are two different
                // questions that were previously being given one answer. See
                // RawEnemyEntry.stageScale.
                //
                // Multiplied rather than substituted, so a boss in the back row
                // is still smaller than the same boss in front and the
                // perspective the whole stage rests on survives.
                float scale = FightStageAnchors.SlotScale(i, shown) * (presence?.Invoke(i) ?? 1f);
                var baseScale = new Vector3(scale, scale, 1f);

                // HANDED IN, NOT LOOKED UP -- the array ScreenRegistry
                // populated one-to-one with `slots`, so an index into one is
                // an index into the other and there is nothing here left to
                // GetComponent for.
                var animator = animators != null && i < animators.Length ? animators[i] : null;

                // ONLY WHEN IT ACTUALLY MOVED, and that guard is the whole of
                // this function's correctness.
                //
                // RefreshStage is not an occasional event -- the HUD refresh
                // and the idle breath both reach it. Writing the mark
                // unconditionally therefore fought the lunge tween for the
                // rect all the way through the swing, and telling the animator
                // unconditionally CANCELLED that tween outright, since Rehome
                // stops whatever is in flight. The result was an attack where
                // nobody moved.
                //
                // Compared against the ANIMATOR'S mark, never the live rect:
                // mid-lunge the rect is somewhere between here and the target
                // by design, so reading it back would see a difference every
                // frame and re-home forever. The animator's home is the only
                // thing that still knows where the figure belongs. Without an
                // animator there is nothing to move it, so the rect IS the mark.
                var currentMark = animator != null ? animator.Home : slots[i].anchoredPosition;
                var currentScale = animator != null ? animator.BaseScale : slots[i].localScale;

                if ((currentMark - mark).sqrMagnitude < 0.0001f &&
                    (currentScale - baseScale).sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                // THE ANIMATOR HAS TO BE TOLD, and telling it is now the whole
                // of the write. It holds the mark a figure returns to after a
                // lunge and the scale its stretch multiplies onto, and it
                // captured both in Awake -- which was correct only while this
                // function did not exist. Without this every swing after a
                // re-spread ended by snapping the figure back to where its slot
                // used to be, at the size it used to be.
                //
                // ASSIGNING THE RECT HERE AS WELL USED TO BE PART OF IT, and it
                // is not any more. This wrote the two values and then called a
                // Rehome that read them straight back, which worked only
                // because nothing else wrote localScale between the two lines.
                // The idle breath writes it every frame, so that arrangement
                // would have folded a breath into the base scale and
                // multiplied it again on the next one. Handing the values over
                // leaves one writer, which cannot be got out of order.
                if (animator != null)
                {
                    animator.Rehome(mark, baseScale);
                }
                else
                {
                    slots[i].anchoredPosition = mark;
                    slots[i].localScale = baseScale;
                }
            }
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

                _intentGlyphs[i] = icon.GetComponentInChildren<TMPro.TMP_Text>(includeInactive: true);
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

        private void OnHoverIndex(int index, bool entered)
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

            string folder = SpriteFolderFor(combatant);
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
            // ScreenRegistry populated for AnchorStageSlots.
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

        // Fades out whoever this beat left on the floor.
        //
        // Driven off the beat's SNAPSHOT rather than off live health: by the
        // time a beat plays, later actions in the same round have already moved
        // the real numbers, so a combatant who dies on beat 3 would otherwise
        // start fading on beat 1.
        private void FadeTheFallen(CombatBeat beat)
        {
            if (beat?.Snapshot == null || _session == null) return;

            foreach (var pair in beat.Snapshot)
            {
                if (pair.Value.Health > 0) continue;

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
                DeathFadeFor(pair.Key)?.PlayIfNotAlready();
            }
        }

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

            if (_idling != null || !isActiveAndEnabled) return;

            _idling = StartCoroutine(IdleBreathing());
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

            // UNSCALED BY BeatSpeedMultiplier, like every other clock on this
            // stage -- a fight paused behind a modal should not bank up a
            // breath and spend it all at once when the panel closes. Scaled
            // by BreathSpeedMultiplier instead, which is 1 outside a test and
            // therefore changes nothing about that rule in play.
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
            ContentTopCache.Clear();

            _confirmedDefeated.Clear();

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

            foreach (var fade in enemyDeathFades.Concat(partyDeathFades)) fade?.ResetToVisible();
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

            // Only a beat that actually LANDED something flashes. A beat that
            // recorded no amount is a hold-back, a refusal or a status tick, and
            // flashing those would make the one signal that means "you were hit"
            // stop meaning anything.
            if (beat.Amount <= 0) return;

            FlashOne(beat, beat.Target);
        }

        // Read off the BEAT, not off the target's health -- by the time this
        // plays, live health has already moved through the rest of the round.
        private void FlashOne(CombatBeat beat, CombatantState target)
        {
            var flash = HitFlashFor(target);
            if (flash == null) return;

            if (beat.IsHealing) flash.FlashHeal();
            else flash.Flash();
        }

        // ---- resolving art from content ----------------------------------------

        private string SpriteFolderFor(CombatantState combatant)
        {
            var enemy = _session?.SourceFor(combatant);
            if (enemy != null) return enemy.Source.SpritePath;

            // Player art comes off the adapter's parallel array rather than the
            // kit: the kit is what COMBAT needs and art is not that. v1 kept both
            // in one dictionary, which is a large part of why its combat logic
            // could not leave the controller.
            return PortraitFolderFor(combatant);
        }

        // How big whoever is standing in enemy slot `index` should be drawn.
        //
        // BY SLOT INDEX, because that is the only thing AnchorStageSlots knows
        // -- it walks slots, not combatants, and is shared with the party rack
        // where the question does not arise. Reading the roster back out here
        // keeps that function's signature honest about what it operates on.
        private float StageScaleForSlot(int index)
        {
            var enemies = _session?.Encounter.Enemies;
            if (enemies == null || index < 0 || index >= enemies.Count) return 1f;

            return _session.SourceFor(enemies[index])?.Source.StageScale ?? 1f;
        }

        private SpriteFacing FacingOf(CombatantState combatant)
        {
            var enemy = _session?.SourceFor(combatant);
            if (enemy != null) return enemy.Source.Facing;

            // Only reached for a combatant with no content behind it at all,
            // which also means no art to mirror -- the value is inert.
            return SpriteFacing.Right;
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

        private static float ContentCentreFractionForActor(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return 0f;
            if (ContentCentreCache.TryGetValue(folder, out var cached)) return cached;

            var idle = StanceAnimationLibrary.Resolve(folder, FightSession.Stances.Idle);
            float centre = ContentCentreFraction(idle);
            ContentCentreCache[folder] = centre;
            return centre;
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
        // same reasons ContentCentreFraction is: a pose that raises an arm must
        // not drag the badge up with it, and one more authored number per actor
        // is a number that can rot. The ground line stays authored -- that one is
        // load-bearing enough to be worth the manifest.
        private static float ContentTopForActor(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return 0f;
            if (ContentTopCache.TryGetValue(folder, out var cached)) return cached;

            var idle = StanceAnimationLibrary.Resolve(folder, FightSession.Stances.Idle);
            float top = ContentTop(idle);
            ContentTopCache[folder] = top;
            return top;
        }

        private static float ContentTop(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable) return 0f;

            var rect = sprite.textureRect;
            var pixels = sprite.texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);

            // GetPixels is bottom-up, so walking down from the last row finds the
            // visual TOP on the first opaque hit.
            for (int y = (int)rect.height - 1; y >= 0; y--)
            {
                for (int x = 0; x < (int)rect.width; x++)
                {
                    if (pixels[y * (int)rect.width + x].a > 0.02f) return y + 1;
                }
            }

            return 0f;
        }

        // How far the opaque pixels' horizontal midpoint sits from the canvas's,
        // as a fraction of width. 0 means the figure is dead centre.
        private static float ContentCentreFraction(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable) return 0f;

            var rect = sprite.textureRect;
            int left = int.MaxValue;
            int right = int.MinValue;

            var pixels = sprite.texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            for (int y = 0; y < (int)rect.height; y++)
            {
                for (int x = 0; x < (int)rect.width; x++)
                {
                    if (pixels[y * (int)rect.width + x].a <= 0.02f) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }

            if (left > right) return 0f;

            float midpoint = (left + right) * 0.5f;
            return midpoint / rect.width - 0.5f;
        }
    }
}
