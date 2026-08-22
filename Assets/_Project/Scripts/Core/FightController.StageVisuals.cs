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
        // The pose each combatant is currently holding, and which frame of it.
        // Playback writes these; every repaint reads them.
        private readonly Dictionary<CombatantState, string> _stance = new Dictionary<CombatantState, string>();
        private readonly Dictionary<CombatantState, int> _actorFrame = new Dictionary<CombatantState, int>();

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
            AnchorStageSlots(enemySlots, enemies.Count, mirrored: false);

            for (int i = 0; i < enemySprites.Length; i++)
            {
                var enemy = i < enemies.Count ? enemies[i] : null;
                enemySlots[i].gameObject.SetShown(enemy != null);
                if (enemy == null) continue;

                RefreshCombatantSprite(enemySprites[i], enemy, StageSide.Right, StanceOf(enemy));
                RefreshNameplate(enemyNameplates[i], enemy);
            }

            RefreshIntentIcons();

            var party = _session.Encounter.PlayerParty;
            AnchorStageSlots(partySlots, party.Count, mirrored: true);

            for (int i = 0; i < partySprites.Length; i++)
            {
                var member = i < party.Count ? party[i] : null;
                partySlots[i].gameObject.SetShown(member != null);
                if (member == null) continue;

                RefreshCombatantSprite(partySprites[i], member, StageSide.Left, StanceOf(member));
                RefreshNameplate(partyNameplates[i], member);
            }
        }

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
            if (combatant != null && !combatant.IsAlive) return FightSession.Stances.Defeated;
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
        private static void AnchorStageSlots(RectTransform[] slots, int liveCount, bool mirrored)
        {
            if (slots == null || liveCount <= 0) return;

            int shown = liveCount > slots.Length ? slots.Length : liveCount;
            for (int i = 0; i < shown; i++)
            {
                if (slots[i] == null) continue;

                var offset = FightStageAnchors.SlotOffset(i, shown, mirrored);
                var mark = new Vector2(offset.X, offset.Y);

                float scale = FightStageAnchors.SlotScale(i, shown);
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

                slots[i].anchoredPosition = mark;
                slots[i].localScale = baseScale;

                // AND THE ANIMATOR HAS TO BE TOLD. It holds the mark a figure
                // returns to after a lunge and the scale its stretch multiplies
                // onto, and it captured both in Awake -- which was correct only
                // while these two lines did not exist. Without this every swing
                // after a re-spread ended by snapping the figure back to where
                // its slot used to be, at the size it used to be.
                //
                // Told rather than polled: this function is the authority on
                // where a slot lives, so it is the one thing that always knows
                // the answer has changed.
                if (animator != null) animator.Rehome();
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
        // missing file behind a per-frame retry. Resolved once per kind, and a
        // null stays null so the text fallback takes over permanently rather
        // than flickering.
        private static readonly Dictionary<EnemyIntentKind, Sprite> IntentSprites =
            new Dictionary<EnemyIntentKind, Sprite>();

        private static Sprite IntentSpriteFor(EnemyIntentKind kind)
        {
            if (IntentSprites.TryGetValue(kind, out var cached)) return cached;

            var loaded = Resources.Load<Sprite>(Domain.Combat.Session.EnemyIntentIcons.ResourceFor(kind));
            IntentSprites[kind] = loaded;
            return loaded;
        }

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
                    _intentImages[i].sprite = art;
                    _intentImages[i].color = Hex(Domain.Combat.Session.EnemyIntentIcons.TintFor(kind));
                    // Nothing to draw is worse than a plain plate: an Image with
                    // no sprite paints a filled RECTANGLE, which is the white
                    // quad this project has hunted eleven times.
                    _intentImages[i].enabled = art != null;
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

        private void RefreshCombatantSprite(Image image, CombatantState combatant, StageSide side, string stance)
        {
            if (image == null) return;

            var slotRect = image.transform.parent as RectTransform;
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
            GroundTheFigure(image, slotRect, folder);
            PlaceShadow(slotRect, folder, mirror);

            // Synced AFTER the mirror and carrying it: the flash overlay is a
            // SIBLING, so it does not inherit the sprite's flip. Without this the
            // silhouette faced the opposite way to the figure -- invisible on
            // anything unmirrored (Shawn faces right and stands left, so he
            // looked correct by luck) and obvious on every enemy.
            SyncHitFlash(image, sprite, mirror);

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
        private static void GroundTheFigure(Image image, RectTransform slotRect, string folder)
        {
            if (slotRect == null) return;

            // The manifest speaks in pixels of the sprite's own canvas, and the
            // slot is sized to exactly that canvas -- so the number carries
            // across with no conversion. Scaling by the slot is still what
            // applies stage depth to it.
            float drop = StanceManifestLoader.Manifest.GroundLineFor(folder);

            // The sprite is anchor-stretched across the slot, so it is nudged
            // with offsetMin/Max rather than anchoredPosition.
            image.rectTransform.offsetMin = new Vector2(0f, -drop);
            image.rectTransform.offsetMax = new Vector2(0f, -drop);
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

                var slot = SlotFor(pair.Key);
                var fade = slot == null ? null : slot.GetComponent<StageDeathFade>();

                // PlayIfNotAlready, not Play: a corpse is in the snapshot of
                // every beat after the one that killed it, so this is asked
                // repeatedly and must only ever fade once.
                fade?.PlayIfNotAlready();
            }
        }

        // Puts every figure back for a fresh encounter. The fade is the reason
        // this has to exist: an actor left at zero alpha would begin the next
        // fight invisible, and its slot is reused rather than rebuilt.
        private void ResetStagePresentation()
        {
            foreach (var slot in enemySlots.Concat(partySlots))
            {
                if (slot == null) continue;
                slot.GetComponent<StageDeathFade>()?.ResetToVisible();
                slot.GetComponent<StageActorAnimator>()?.ResetToHome();
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
