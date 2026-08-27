using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Core.Rig;
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
        // The pose each combatant is currently holding, and which frame of it.
        // Playback writes these; every repaint reads them.
        private readonly Dictionary<CombatantState, string> _stance = new Dictionary<CombatantState, string>();
        private readonly Dictionary<CombatantState, int> _actorFrame = new Dictionary<CombatantState, int>();

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

            AnchorStageSlots(enemySlots, onStage, mirrored: false, StageScaleForSlot);
            AnchorStageSlots(enemyWorldSlots, onStage, mirrored: false, StageScaleForSlot);

            for (int i = 0; i < enemySprites.Length; i++)
            {
                var enemy = i < enemies.Count && IsOnStage(enemies[i]) ? enemies[i] : null;
                enemySlots[i].gameObject.SetShown(enemy != null);
                // Deactivating the world slot also deactivates whatever rig
                // instance is parented under it -- same lifecycle as the
                // uGUI slot, no separate rig-instance cleanup needed.
                WorldSlotAt(enemyWorldSlots, i)?.gameObject.SetShown(enemy != null);
                if (enemy == null) continue;

                RefreshCombatantSprite(enemySprites[i], WorldSlotAt(enemyWorldSlots, i), enemy, StageSide.Right, StanceOf(enemy));
                RefreshNameplate(enemyNameplates[i], enemy);
            }

            RefreshIntentIcons();

            var party = _session.Encounter.PlayerParty;
            AnchorStageSlots(partySlots, party.Count, mirrored: true);
            AnchorStageSlots(partyWorldSlots, party.Count, mirrored: true);

            for (int i = 0; i < partySprites.Length; i++)
            {
                var member = i < party.Count ? party[i] : null;
                partySlots[i].gameObject.SetShown(member != null);
                WorldSlotAt(partyWorldSlots, i)?.gameObject.SetShown(member != null);
                if (member == null) continue;

                RefreshCombatantSprite(partySprites[i], WorldSlotAt(partyWorldSlots, i), member, StageSide.Left, StanceOf(member));
                RefreshNameplate(partyNameplates[i], member);
            }
        }

        // Defensive against index mismatch rather than a bare array index --
        // enemyWorldSlots/partyWorldSlots are declared to always match
        // enemySlots/partySlots 1:1 (E4's own count audit enforces it at
        // build time), but a null here should degrade to "no rig for this
        // slot" rather than throw and take the whole stage refresh down.
        private static RectTransform WorldSlotAt(RectTransform[] worldSlots, int i) =>
            worldSlots != null && i < worldSlots.Length ? worldSlots[i] : null;

        // What pose a combatant is holding. Public so a PlayMode test can assert
        // the round ended idle rather than stuck on an attack frame -- there is
        // no other way to see a stance from outside, because the visible result
        // is a sprite that may not exist for a combatant with no authored art.
        public string StanceFor(CombatantState combatant) => StanceOf(combatant);

        // Which frame of that pose. Public for the same reason StanceFor is: a
        // test sampling this mid-beat is the only way to see that an animation
        // actually stepped, since the round ends back on frame 0 either way.
        public int FrameFor(CombatantState combatant) =>
            combatant != null && _actorFrame.TryGetValue(combatant, out var frame) ? frame : 0;

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
        private static void AnchorStageSlots(RectTransform[] slots, int liveCount, bool mirrored,
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

                var animator = slots[i].GetComponent<StageActorAnimator>();

                // ONLY WHEN IT ACTUALLY MOVED, and that guard is the whole of
                // this function's correctness.
                //
                // RefreshStage is not an occasional event. It runs from the HUD
                // refresh and from SetActorFrame -- once per FRAME of every
                // attack animation. Writing the mark unconditionally therefore
                // fought the lunge tween for the rect all the way through the
                // swing, and telling the animator unconditionally CANCELLED
                // that tween outright, since Rehome stops whatever is in
                // flight. The result was an attack where nobody moved, on
                // every combatant with more than one frame of art -- and the
                // single-frame poses still moved, which is what made it look
                // like it happened at random.
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

        // One rig instance per combatant that has one, reused across
        // refreshes rather than instantiated fresh every repaint (this runs
        // every frame during an attack animation -- see AnchorStageSlots).
        // Never removed on its own: a combatant leaving the stage hides it
        // via its world slot's SetShown(false) instead (see RefreshStage),
        // so a returning combatant (a summon re-shown, a flee that comes
        // back) reuses the same instance rather than re-instantiating.
        private readonly Dictionary<CombatantState, GameObject> _rigInstances = new Dictionary<CombatantState, GameObject>();

        // Cached alongside the instance itself, computed once on first
        // instantiation rather than re-read from RigMeta every repaint.
        private readonly Dictionary<CombatantState, float> _rigScale = new Dictionary<CombatantState, float>();

        private void RefreshCombatantSprite(Image image, RectTransform worldSlot, CombatantState combatant, StageSide side, string stance)
        {
            if (image == null) return;

            var slotRect = image.transform.parent as RectTransform;

            // Graceful degradation, per-combatant: only a resolvable folder
            // gets the rig path (today, only "Enemies/rat" during the
            // pilot); everything else falls straight through to the
            // existing frame-sheet Image path completely unchanged below.
            var rigPrefab = worldSlot != null ? RigLibrary.Resolve(SpriteFolderFor(combatant)) : null;
            if (rigPrefab != null)
            {
                RefreshRigActor(rigPrefab, worldSlot, combatant, side);
                image.gameObject.SetShown(false);
                return;
            }

            var sprite = LoadStanceSprite(combatant, stance);

            if (sprite == null)
            {
                ShowFallbackPlate(image, slotRect);
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
            GroundTheFigure(image, slotRect, folder, SidewaysDrift(combatant, folder, stance, mirror));
            PlaceShadow(slotRect, folder, mirror);

            // Synced AFTER the mirror and carrying it: the flash overlay is a
            // SIBLING, so it does not inherit the sprite's flip. Without this the
            // silhouette faced the opposite way to the figure -- invisible on
            // anything unmirrored (Shawn faces right and stands left, so he
            // looked correct by luck) and obvious on every enemy.
            SyncHitFlash(image, sprite, mirror);

            // The afterimage copies this exact Image, so the animator is handed
            // it through the same door -- once bound it clones the live node
            // whenever it trails, picking up the current frame and flip for
            // free. Same-sprite rebinds are cheap and idempotent.
            slotRect?.GetComponent<StageActorAnimator>()?.BindSprite(image);

            // ACTIVATED, not merely enabled. The sprite node is built inactive
            // (it has no art until a fight exists), and enabling a Graphic whose
            // GameObject is inactive draws exactly nothing -- which is why the
            // stage showed foot glows hovering over an empty forest while every
            // headless assertion about "the sprite loaded" passed.
            image.gameObject.SetShown(true);
            image.enabled = true;
        }

        // Instantiates (or reuses) a rig actor under its world slot, sized
        // and mirrored to match what the frame-sheet Image path does for
        // everyone else. Static bind pose only for now -- no stance/frame
        // sampling yet, that's the animation-data phase this pilot lands
        // before.
        private void RefreshRigActor(GameObject prefab, RectTransform worldSlot, CombatantState combatant, StageSide side)
        {
            GameObject instance;
            bool fresh = !_rigInstances.TryGetValue(combatant, out instance) || instance == null;
            if (fresh)
            {
                instance = Object.Instantiate(prefab, worldSlot, worldPositionStays: false);
                _rigInstances[combatant] = instance;
            }
            else if (instance.transform.parent != worldSlot)
            {
                instance.transform.SetParent(worldSlot, worldPositionStays: false);
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // RigLibrary.ScaleFor undoes the rig's own bind-pose-pixel-to-
            // Unity-unit conversion (RigPrefabBuilder's PixelsPerUnit) AND
            // corrects for the bind pose's own content height disagreeing
            // with what this creature rendered at before it had a rig (see
            // RigLibrary's own comment) -- landing back at "1 unit = 1
            // canvas pixel, at the RIGHT apparent size" under a world slot
            // whose OWN scale already carries SlotScale (AnchorStageSlots),
            // so this multiplier is exactly what the world slot's local
            // space needs, nothing more. Mirror flips the same way the
            // frame-sheet Image path already does (StageFacing.MirrorScaleX),
            // just as a negative X scale here instead of on an
            // Image.rectTransform.
            //
            // RigMeta lookup only on a freshly-instantiated instance -- a
            // reused one already carries the right scale from last time,
            // and GetComponent on every repaint (this runs every frame
            // during an attack, see AnchorStageSlots) is needless work.
            if (fresh)
            {
                var meta = instance.GetComponent<RigMeta>();
                _rigScale[combatant] = RigLibrary.ScaleFor(SpriteFolderFor(combatant), meta != null ? meta.ReferenceHeightPx : 0f);
            }
            float scale = _rigScale.TryGetValue(combatant, out var s) ? s : RigLibrary.PixelsPerUnit;

            float mirror = StageFacing.MirrorScaleX(FacingOf(combatant), side);
            instance.transform.localScale = new Vector3(scale * mirror, scale, 1f);

            instance.SetActive(true);
        }

        // No authored art: fall back to the plain plate so the slot still reads
        // as a real combatant rather than vanishing or showing a white box.
        private void ShowFallbackPlate(Image image, RectTransform slotRect)
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
            SyncHitFlash(image, enemyFallbackSprite, 1f);

            // Stays off if even the fallback is missing -- an invisible slot
            // beats a solid white rectangle.
            ShowSprite(image, true);
        }

        // THE GROUND LINE IS THE SLOT'S BOTTOM, and this is what puts each
        // frame's feet on it.
        //
        // Delivered art does not agree about where the feet sit inside the
        // canvas, and the disagreement is per FRAME, not per actor. Pinning the
        // raw canvas to the ground line made a figure JUMP as it changed pose --
        // 52px for the golem going idle->attack, which is the "golem flies
        // upwards in its attack" playtest report.
        //
        // READ FROM THE MANIFEST, never measured. The runtime used to scan the
        // art's alpha and was wrong twice over: the golem's slam erupts an earth
        // spike ~50px below its own feet and Shawn's idle plants a staff ~33px
        // below his, so both were read as the floor and both figures were
        // hoisted into the air. A sheet states where its feet are, and
        // StanceManifestValidationTests fails if that stops matching the pixels
        // -- the case a runtime measurement can never report, because it just
        // quietly believes whatever it finds.
        //
        // ONE value per actor rather than per frame, and that is load-bearing:
        // the bug being designed out is a figure MOVING VERTICALLY between
        // stances, and a constant offset cannot do that by construction, where a
        // recomputed one merely usually doesn't.
        private static void GroundTheFigure(Image image, RectTransform slotRect, string folder, float drift)
        {
            if (slotRect == null) return;

            // The manifest speaks in pixels of the sprite's own canvas, and the
            // slot is sized to exactly that canvas -- so the number carries
            // across with no conversion. Scaling by the slot is still what
            // applies stage depth to it.
            float drop = StanceManifestLoader.Manifest.GroundLineFor(folder);

            // The sprite is anchor-stretched across the slot, so it is nudged
            // with offsetMin/Max rather than anchoredPosition. THE SAME VALUE
            // ON BOTH, per axis, which is what makes this a translation rather
            // than a resize -- the drift correction rides the identical
            // mechanism the ground line already uses, so the two cannot fight
            // over the rect.
            image.rectTransform.offsetMin = new Vector2(-drift, -drop);
            image.rectTransform.offsetMax = new Vector2(-drift, -drop);
        }

        // HOW FAR THIS PARTICULAR DRAWING SITS OFF THE POSE'S OWN CENTRE.
        //
        // Zero for every stance that is not marked steady, which today is
        // every stance but idle. See StanceTiming.Steady for why cancelling
        // this in a swing would nail the figure to the spot mid-lunge.
        //
        // MEASURED AGAINST FRAME 0 OF THE SAME STANCE, not against the frame's
        // geometric middle. The question being asked is "has the creature moved
        // since this pose began"; answering it against the canvas instead would
        // shove an idle sideways for any sheet whose figure is simply drawn off
        // centre, which is a property of the art rather than a wobble.
        private float SidewaysDrift(CombatantState combatant, string folder, string stance, float mirror)
        {
            if (combatant == null || string.IsNullOrWhiteSpace(folder)) return 0f;

            var animation = StanceAnimationLibrary.Resolve(folder, stance);
            if (!animation.Steady || animation.FrameCount <= 1) return 0f;

            int frame = FrameFor(combatant);
            if (frame <= 0) return 0f;

            var sprite = animation.FrameAt(frame);
            var first = animation.FrameAt(0);
            if (sprite == null || first == null) return 0f;

            float here = ContentCentreFor(folder, stance, frame, sprite);
            float home = ContentCentreFor(folder, stance, 0, first);

            // The centres are fractions of the frame's width, so the pixels come
            // back out by multiplying by it. Mirrored for the same reason
            // PlaceShadow is: a figure leaning right appears to lean left once
            // flipped, and a correction blind to that would double the error
            // instead of cancelling it.
            return (here - home) * sprite.rect.width * mirror;
        }

        // Per-FRAME, unlike ContentCentreFractionForActor, which answers for the
        // actor as a whole from its idle frame 0 and is what the badge and the
        // shadow want. Cached for the same reason that one is: the measurement
        // opens a texture's pixels, which must never happen per repaint.
        private static readonly Dictionary<string, float> FrameCentreCache = new Dictionary<string, float>();

        private static float ContentCentreFor(string folder, string stance, int frame, Sprite sprite)
        {
            string key = folder + "/" + stance + "#" + frame;
            if (FrameCentreCache.TryGetValue(key, out var cached)) return cached;

            float centre = ContentCentreFraction(sprite);
            FrameCentreCache[key] = centre;
            return centre;
        }

        // The ring stays ON the ground line. Only its X needs correcting, for art
        // whose figure is not centred in its own canvas, and that is read from
        // the IDLE frame so a pose that swings an arm out cannot drag the ring
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

        private static void SyncHitFlash(Image sprite, Sprite art, float mirror)
        {
            var slot = sprite.transform.parent;
            if (slot == null) return;

            var flash = slot.GetComponentInChildren<StageHitFlash>(includeInactive: true);
            if (flash == null) return;

            flash.SetSprite(art);
            if (flash.transform is RectTransform flashRect)
            {
                flashRect.localScale = new Vector3(mirror, 1f, 1f);
            }
        }

        // ---- what playback drives -----------------------------------------------

        // Poses a combatant and repaints. Called once per combatant per beat, so
        // the repaint is the whole stage rather than one slot -- at three actors
        // a side that is cheaper than working out which slot changed.
        private void PoseCombatant(CombatantState combatant, string stance)
        {
            if (combatant == null) return;

            _stance[combatant] = stance;

            // Frame 0 on every pose CHANGE, not on every repaint: a stance that
            // reset its own frame each time it was painted would never advance
            // past the first one.
            _actorFrame[combatant] = 0;

            RefreshStage();
        }

        // Steps a combatant to frame N of the pose it is already holding.
        //
        // Separate from PoseCombatant, which resets the frame: choosing a pose
        // and stepping through it are different events, and collapsing them
        // would re-resolve the animation on every frame -- and reset the index
        // it was trying to advance.
        private void SetActorFrame(CombatantState combatant, int frame)
        {
            if (combatant == null) return;

            _actorFrame[combatant] = frame;
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
                //
                // The RETURN of Add is the "first time we have seen this
                // corpse" signal, and it is load-bearing below: a body is in
                // the snapshot of every beat after the one that killed it, so
                // everything here is asked repeatedly.
                bool firstSight = _confirmedDefeated.Add(pair.Key);

                // THE DEATH ANIMATION, which until now never played.
                //
                // FightBeatPlayer steps frames for the beat's ACTOR only --
                // reasonably, since a target's reaction is a recoil and a
                // flash rather than an animation. But the thing that DIES is
                // the target, so a corpse held whatever frame PoseCombatant
                // reset it to, which is 0, and then faded.
                //
                // That was invisible for every kit on the roster because their
                // defeated art opens already collapsed -- frame 0 is a body on
                // the floor and the remaining frames are it settling. The
                // Ironback Beetle's does not: its six frames run from standing
                // through the flip onto its back, so it died by standing
                // perfectly still and fading out. Reported from play, not
                // caught here, which is the whole reason it is worth saying
                // out loud that a multi-frame defeated pose was decorative.
                //
                // Timed to fit what StageDeathFade already allows: 0.35s of
                // hold before the fade starts and 0.6s of fade, against a
                // six-frame collapse at ~0.11s a frame. The body finishes
                // falling roughly as it starts to go.
                if (firstSight) StartCoroutine(PlayDefeatedFrames(pair.Key));

                var slot = SlotFor(pair.Key);
                var fade = slot == null ? null : slot.GetComponent<StageDeathFade>();

                // PlayIfNotAlready, not Play: a corpse is in the snapshot of
                // every beat after the one that killed it, so this is asked
                // repeatedly and must only ever fade once.
                fade?.PlayIfNotAlready();
            }
        }

        // Walks a corpse through its own defeated frames.
        //
        // Its own coroutine rather than playback's, because it OUTLIVES the
        // beat that caused it -- the body keeps falling while the next line of
        // the log is already being written, and a beat owns its own length.
        // The pacing is shared all the same (StanceStepper): this used to walk
        // the frames flat, which animated the corpse and still made it read as
        // a slideshow.
        //
        // The abandon check is polled every frame: the fight can end, or the
        // stage reset, while a body is still going down, and writing frames for
        // a combatant whose slot the next encounter has reused is how a fresh
        // fight opens with someone else's corpse in it.
        private IEnumerator PlayDefeatedFrames(CombatantState combatant) =>
            StanceStepper.Play(
                StanceAnimationFor(combatant, FightSession.Stances.Defeated),
                frame => SetActorFrame(combatant, frame),
                abandon: () => _session == null);

        // ---- the breath between blows --------------------------------------------

        // NOBODY BREATHED. Every idle sheet in the game was a still.
        //
        // Three actors ship a six-frame idle -- the Beetle, the Treant and the
        // Forest Warden -- and the manifest authors a pace for each of them
        // (0.12s to 0.14s a frame). Nothing ever stepped those frames, so all
        // three stood on frame 0 for the whole fight. The art and the timing
        // were both already there; what was missing was anything to drive them.
        //
        // FightBeatPlayer could not be that thing, and its own comment says why
        // without realising it: "Idle is a single frame today, so this is a
        // no-op". Playback steps the beat's ACTOR, and the beat's actor is
        // never idle -- it is swinging. An idle loop is the opposite shape from
        // everything in that class: it belongs to no beat, it has to run while
        // the game sits waiting for a click, and it never ends.
        //
        // So it lives here, beside the corpse stepper, which is the other
        // animation the controller owns for the same reason: playback does not
        // drive it and it outlives the beat.
        private Coroutine _idling;

        // Where each figure is in its own breath. Cleared per combatant the
        // moment it stops being idle, which is what makes the loop restart from
        // frame 0 on the way back rather than resuming mid-inhale from before
        // the blow.
        private readonly Dictionary<CombatantState, float> _idleClock =
            new Dictionary<CombatantState, float>();

        // How far apart two figures' breaths are pushed, in frames.
        //
        // Two Ironback Beetles side by side breathing in perfect lockstep read
        // as one animation drawn twice rather than as two animals -- the same
        // failure AnchorStageSlots' own note describes for two rats overlapping
        // into "one monster with a spare tail". Offsetting by slot index is
        // free and deterministic, which matters: a random phase would make a
        // capture test's screenshot differ run to run.
        private const float IdlePhaseFrames = 1.6f;

        private void StartIdleBreathing()
        {
            // Cleared on every fight, because the keys are CombatantStates and
            // the next encounter's are different objects -- the old entries
            // would otherwise sit here for the session's life.
            _idleClock.Clear();

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
        // in `idle`, so the two can never write the same combatant's frame.
        private IEnumerator IdleBreathing()
        {
            while (true)
            {
                yield return null;

                if (_session == null) continue;

                // ONE REPAINT FOR THE WHOLE STAGE, and only when a frame
                // actually turned over. Stepping through SetActorFrame would
                // refresh once per combatant per frame for a picture that is
                // drawn once either way.
                bool moved = false;

                var enemies = _session.Encounter.Enemies;
                for (int i = 0; i < enemies.Count; i++) moved |= StepIdleFrame(enemies[i], i);

                var party = _session.Encounter.PlayerParty;
                for (int i = 0; i < party.Count; i++) moved |= StepIdleFrame(party[i], i);

                if (moved) RefreshStage();
            }
        }

        // Hands one figure's breath to the thing that wears it.
        //
        // The animator lives on the SLOT, which is what carries the depth
        // scale and the bottom pivot -- so a breath scales about the figure's
        // own ground line and cannot lift it off the floor. Same component,
        // same reasoning, as the lunge and the recoil.
        //
        // Silent when there is no slot or no animator: a combatant that is not
        // on stage (a summon still being held back by _confirmedPresent) has
        // nothing to breathe, and that is not an error worth a branch upstream.
        private void BreatheFigure(CombatantState combatant, float amount)
        {
            var slot = SlotFor(combatant);
            if (slot == null) return;

            slot.GetComponent<StageActorAnimator>()?.SetBreath(amount);
        }

        // Advances one figure's breath. True when the drawing changed.
        private bool StepIdleFrame(CombatantState combatant, int index)
        {
            if (combatant == null) return false;

            // StanceOf, not _stance, so a corpse is excluded by the same rule
            // the rest of the stage reads it by -- including the beat-confirmed
            // one, which is what stops a body twitching between the blow that
            // killed it and the pose that says so.
            if (StanceOf(combatant) != FightSession.Stances.Idle)
            {
                _idleClock.Remove(combatant);
                BreatheFigure(combatant, 0f);
                return false;
            }

            var animation = StanceAnimationFor(combatant, FightSession.Stances.Idle);

            // NO ART, NO BREATH, and the check is IsEmpty rather than
            // FrameCount because those are now different questions. A figure
            // with a single drawing is exactly the case the transform breath
            // exists for; a figure with NO drawing is a fallback plate, and a
            // UI frame that swells and settles reads as a rendering fault.
            if (animation.IsEmpty)
            {
                BreatheFigure(combatant, 0f);
                return false;
            }

            // NOT FrameHoldCurve, and not a flat step either.
            //
            // FrameHoldCurve shapes a motion around its impact frame -- a long
            // wind-up, a snap, a long settle -- which is right for a blow and
            // wrong for a loop, since an idle authors impactFrame 1 because it
            // has no impact. A flat step is what made this read as "a loop of 6
            // sprites" in the first place. LoopCycle owns both halves of the
            // answer and its header carries the measurements.
            float perFrame = FightBeatPlayer.Scaled(animation.SecondsPerFrame);
            if (perFrame <= 0f)
            {
                // Cleared rather than left alone. Nothing STOPS pushing a
                // breath, so an early return that skips the push freezes the
                // figure at whatever point of the cycle it reached.
                BreatheFigure(combatant, 0f);
                return false;
            }

            if (!_idleClock.TryGetValue(combatant, out float clock))
            {
                clock = index * perFrame * IdlePhaseFrames;
            }

            // UNSCALED, like every other clock on this stage. A fight paused
            // behind a modal should not bank up a breath and spend it all at
            // once when the panel closes.
            clock += Time.unscaledDeltaTime;
            _idleClock[combatant] = clock;

            // THE TRANSFORM HALF OF THE BREATH, and it runs for every idle
            // figure rather than only for the three with a six-frame sheet.
            // That is the point of it: the rat, the golem, the bog witch and
            // Shawn have one drawing each and stood perfectly still through
            // every fight.
            //
            // OFFSET SEPARATELY from the sheet loop, not sharing
            // IdlePhaseFrames. Two figures' DRAWINGS are separated in units of
            // their own sheet's pace; their BREATHS are separated against a
            // fixed period, so deriving one from the other would make a slow
            // sheet separate its breaths less. See BreathCurve.PhaseFor.
            BreatheFigure(combatant,
                BreathCurve.At(clock + BreathCurve.PhaseFor(index),
                               StanceManifestLoader.Manifest.BreathFor(
                                   SpriteFolderFor(combatant), animation.FrameCount)));

            if (animation.FrameCount <= 1) return false;

            int frame = LoopCycle.FrameAt(clock, animation.FrameCount, perFrame, animation.Loop,
                                          FightBeatPlayer.Scaled(animation.EndHoldSeconds));
            if (FrameFor(combatant) == frame) return false;

            // Written straight into the map rather than through SetActorFrame,
            // which would repaint the stage per combatant -- see the batching
            // note in IdleBreathing.
            _actorFrame[combatant] = frame;
            return true;
        }

        // Puts every figure back for a fresh encounter. The fade is the reason
        // this has to exist: an actor left at zero alpha would begin the next
        // fight invisible, and its slot is reused rather than rebuilt.
        private void ResetStagePresentation()
        {
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

            foreach (var slot in enemySlots.Concat(partySlots))
            {
                if (slot == null) continue;
                slot.GetComponent<StageDeathFade>()?.ResetToVisible();
                slot.GetComponent<StageActorAnimator>()?.ResetToHome();
            }

            // Rig instances are keyed by CombatantState, and a new Bind()
            // means every existing CombatantState this fight ever cached an
            // instance for is about to become unreachable -- nothing will
            // look those dictionary entries up again, but nothing was
            // destroying the GameObjects either. World slots are fixed
            // scene objects, not per-session, so a re-bind without this
            // left the PREVIOUS fight's rig actor still parented under the
            // same world slot as the new fight's, doubling its rendered
            // parts (confirmed: RigStageTests caught this at 14 renderers
            // under Enemy0WorldSlot instead of 7, the exact "two stale
            // copies" signature).
            foreach (var instance in _rigInstances.Values)
            {
                if (instance != null) Destroy(instance);
            }
            _rigInstances.Clear();
            _rigScale.Clear();

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
            // Only a beat that actually LANDED something flashes. A beat that
            // recorded no amount is a hold-back, a refusal or a status tick, and
            // flashing those would make the one signal that means "you were hit"
            // stop meaning anything.
            if (beat == null || beat.Amount <= 0) return;

            var slot = SlotFor(beat.Target);
            if (slot == null) return;

            var flash = slot.GetComponentInChildren<StageHitFlash>(includeInactive: true);
            if (flash == null) return;

            // Read off the BEAT, not off the target's health -- by the time this
            // plays, live health has already moved through the rest of the round.
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

        // The one funnel every stage sprite paints through. RefreshStage calls it
        // on every repaint, so it is also the one place a combatant's CURRENT
        // frame has to be read, not just its stance.
        private Sprite LoadStanceSprite(CombatantState combatant, string stance)
        {
            var animation = StanceAnimationFor(combatant, stance);
            if (animation.IsEmpty) return null;

            int frame = _actorFrame.TryGetValue(combatant, out var f) ? f : 0;
            return animation.FrameAt(frame);
        }

        // Falls back through requested stance -> idle. A sheet is allowed to be
        // missing a pose (only idle is really mandatory), and a monster frozen in
        // the wrong-but-present pose beats one that blinks out of existence
        // mid-fight.
        public StanceAnimation StanceAnimationFor(CombatantState combatant, string stance)
        {
            string folder = SpriteFolderFor(combatant);
            if (string.IsNullOrWhiteSpace(folder)) return StanceAnimation.Empty;

            var animation = StanceAnimationLibrary.Resolve(folder, stance);
            if (!animation.IsEmpty) return animation;

            return stance == FightSession.Stances.Idle
                ? StanceAnimation.Empty
                : StanceAnimationLibrary.Resolve(folder, FightSession.Stances.Idle);
        }

        private static float ContentCentreFractionForActor(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return 0f;
            if (ContentCentreCache.TryGetValue(folder, out var cached)) return cached;

            var idle = StanceAnimationLibrary.Resolve(folder, FightSession.Stances.Idle);
            float centre = idle.IsEmpty ? 0f : ContentCentreFraction(idle.FrameAt(0));
            ContentCentreCache[folder] = centre;
            return centre;
        }

        // How far the topmost opaque pixel of the IDLE pose sits above the frame's
        // bottom, in frame pixels. Cached per actor.
        //
        // This exists because a slot is sized to the sprite's FRAME, and a frame
        // is cut to fit the tallest pose on the sheet -- so an idle golem leaves
        // a great deal of empty canvas above its head. Pinning the intent badge
        // to the frame's top put the golem's badge 106px above it, floating in
        // open sky next to somebody else's HP plate, while the rat's and the
        // witch's looked fine. It read as a bug in the badge rather than as the
        // frame being taller than the pose.
        //
        // Measured rather than authored, and from the IDLE frame only, for the
        // same reasons ContentCentreFraction is: a pose that raises an arm must
        // not drag the badge up with it, and one more authored number per actor
        // is a number that can rot. The ground line stays authored -- that one is
        // load-bearing enough to be worth the manifest.
        private static float ContentTopForActor(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return 0f;
            if (ContentTopCache.TryGetValue(folder, out var cached)) return cached;

            var idle = StanceAnimationLibrary.Resolve(folder, FightSession.Stances.Idle);
            float top = idle.IsEmpty ? 0f : ContentTop(idle.FrameAt(0));
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
