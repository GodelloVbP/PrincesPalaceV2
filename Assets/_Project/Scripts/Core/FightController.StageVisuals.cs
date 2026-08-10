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

        // ---- the whole stage --------------------------------------------------

        private void RefreshStage()
        {
            if (_session == null) return;

            var enemies = _session.Encounter.Enemies;
            for (int i = 0; i < enemySprites.Length; i++)
            {
                var enemy = i < enemies.Count ? enemies[i] : null;
                SetActive(enemySlots[i].gameObject, enemy != null);
                if (enemy == null) continue;

                RefreshCombatantSprite(enemySprites[i], enemy, StageSide.Right, StanceOf(enemy));
                RefreshNameplate(enemyNameplates[i], enemy);
            }

            var party = _session.Encounter.PlayerParty;
            for (int i = 0; i < partySprites.Length; i++)
            {
                var member = i < party.Count ? party[i] : null;
                SetActive(partySlots[i].gameObject, member != null);
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

        private void RefreshNameplate(TMPro.TMP_Text plate, CombatantState combatant)
        {
            if (plate == null) return;
            plate.SetContent(combatant.Name);
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
            SetActive(image.gameObject, true);
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
