using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // AN EVENT FIGHT'S DRESSING (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.4,
    // 1.6, M6): the request-driven backdrop, the top-centre round counter,
    // the toll per round, the overlay that closes in, and the ambience bed.
    //
    // Everything here is read from ONE FightRoundPresentation handed to Bind
    // (EncounterRequest.Presentation in the game; None in every room fight and
    // in every test that binds without one), so a room fight is not a branch
    // here: None has no limit, and a presentation with no limit shows no
    // counter, no overlay and no bed.
    //
    // THE ROUND ON SCREEN IS PLAYBACK'S, not the session's. The session starts
    // a round a whole round ahead of the picture, so the counter steps when
    // the beat that carries the round plays (FightBeatPlayer.PresentRound).
    // The one exception is Bind: the round the fight opened on -- the first
    // round the opening beats carry -- is shown at once, toll and all, and
    // its beat is then a no-op, because a round is presented once and never
    // twice. Any later round Begin crossed steps when its own beat plays
    // (PresentOpeningRound).
    //
    // MISSING FILES ARE QUIET. An overlay key with no baked sprite hides the
    // layer, a backdrop key with none keeps the class backdrop, and a sound
    // path with no clip is SoundController's silent no-op.
    public partial class FightController
    {
        // Every event fight's own backdrop and round overlay, keyed by the
        // Assets-relative path content authored, baked by ScreenRegistry.Fight
        // the way WireEvent bakes an event's art. Empty until content authors
        // an event fight, and a file not there yet drops out of the bake.
        [UiOptional("no event fight authors a backdrop yet, and a missing file drops out of the bake -- the class backdrop stands in")]
        [SerializeField] internal IconEntry[] fightBackdropArt;

        [UiOptional("no event fight authors a round overlay yet, and a missing file drops out of the bake -- the layer stays hidden")]
        [SerializeField] internal IconEntry[] roundOverlayArt;

        [SerializeField] internal TMP_Text roundCounter;

        // The soft dark plate under the counter (FightScreen.BuildRoundCounter);
        // shown and hidden with it.
        [SerializeField] internal Image roundCounterPlate;
        [SerializeField] internal Image roundOverlay;

        private FightRoundPresentation _presentation = FightRoundPresentation.None;

        // The last round shown, 0 before the first. Presenting is monotonic:
        // a round at or below this one is already on screen.
        private int _presentedRound;

        // Read by tests; what the counter is dressed for this fight.
        public FightRoundPresentation Presentation => _presentation;

        // Called from Bind, after the session is set.
        private void ApplyPresentation(FightRoundPresentation presentation)
        {
            _presentation = presentation ?? FightRoundPresentation.None;
            _presentedRound = 0;

            ApplyFightBackdrop(_presentation.BackdropKey);

            bool shows = _presentation.ShowsCounter;
            if (roundCounter != null) roundCounter.gameObject.SetShown(shows);
            if (roundCounterPlate != null) roundCounterPlate.gameObject.SetShown(shows);

            bool overlay = false;
            if (roundOverlay != null)
            {
                overlay = _presentation.HasOverlay
                          && ItemIcons.Apply(roundOverlay, roundOverlayArt, _presentation.OverlayKey);
                roundOverlay.gameObject.SetShown(overlay);

                // A previous fight's last step must not carry into this one.
                roundOverlay.rectTransform.localScale = Vector3.one;
                roundOverlay.color = OverlayTint(overlay ? _presentation.OverlayTint : "");
                PlaceRoundOverlay();
            }

            // The bed follows the presentation: a room fight's None stops a bed
            // a previous fight left running, and the same path again keeps it.
            SoundController.StartAmbience(_presentation.AmbiencePath);
        }

        // THE ROUND THE FIGHT OPENED ON, shown at Bind once the opening beats
        // are drained -- the round the FIRST of them carries, not the
        // session's. Begin can cross several rounds before the party's first
        // turn (a slow party, an enemy that opens), and the session is then
        // at the last of them; showing that would jump the counter and leave
        // every queued toll a no-op, since presenting never goes backwards.
        // From the first, the opening beats step it one toll at a time.
        //
        // No beats queued, or no player to play them: the session's round,
        // stepped to in order so each toll still sounds once.
        private void PresentOpeningRound(IReadOnlyList<CombatBeat> openingBeats, bool willPlay)
        {
            if (!_presentation.ShowsCounter || _session == null) return;

            int opening = 0;
            if (openingBeats != null)
            {
                foreach (var beat in openingBeats)
                {
                    if (beat == null || beat.RoundStarted <= 0) continue;
                    opening = beat.RoundStarted;
                    break;
                }
            }

            if (opening > 0 && willPlay)
            {
                PresentRound(opening);
                return;
            }

            for (int round = Mathf.Max(1, opening); round <= _session.Round; round++) PresentRound(round);
        }

        // THE TOLL. The counter's number, the overlay's step and the round's
        // sound, once per round and never backwards.
        private void PresentRound(int round)
        {
            if (!_presentation.ShowsCounter || round <= _presentedRound) return;
            _presentedRound = round;

            if (roundCounter != null)
            {
                roundCounter.Set(UiStrings.FightRoundCounter, _presentation.CounterLabel,
                    _presentation.CounterRound(round));
            }

            if (roundOverlay != null && roundOverlay.gameObject.activeSelf)
            {
                // Placed again at every step, not only at Bind: the frame's
                // size is the canvas's, and the scaler may not have settled
                // it yet when the first round is shown.
                PlaceRoundOverlay();
                float scale = _presentation.OverlayScaleFor(round);
                roundOverlay.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            SoundController.PlayClip(_presentation.RoundSfxPath);
        }

        // THE OVERLAY'S STANDING POINT: the image's pivot held on the frame's
        // anchor (FightRoundPresentation.PlaceOverlay, the formula, pinned on
        // the fast host). The rect stays stretched over the frame with zero
        // offsets, so moving its pivot does not move it; the anchored
        // position then carries the pivot onto the anchor, and localScale
        // scales about it. Defaults put the pivot at the centre and the
        // offset at zero -- the centre-scaled full frame.
        private void PlaceRoundOverlay()
        {
            if (roundOverlay == null) return;

            var rect = roundOverlay.rectTransform;
            var sprite = roundOverlay.sprite;
            float aspect = sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 0f;
            var size = rect.rect.size;
            var place = _presentation.PlaceOverlay(size.x, size.y, aspect);
            rect.pivot = new Vector2(place.PivotX, place.PivotY);
            rect.anchoredPosition = new Vector2(place.OffsetX, place.OffsetY);
            _overlayPlacedFor = size;
        }

        // The frame size the overlay was last placed for. The pivot fraction
        // and the offset both depend on the frame's aspect (a 16:9 image sits
        // letterboxed in a 4:3 frame), so a resize between tolls -- a window
        // drag, or a capture rig switching aspect -- must re-place it, not
        // wait for the next toll with the old aspect's numbers.
        private Vector2 _overlayPlacedFor;

        // Called every frame from Update: one size compare while an overlay shows.
        private void KeepRoundOverlayPlaced()
        {
            if (roundOverlay == null || !roundOverlay.gameObject.activeInHierarchy) return;
            if (roundOverlay.rectTransform.rect.size != _overlayPlacedFor) PlaceRoundOverlay();
        }

        // Empty or unreadable is the image as painted (the content build
        // refuses an unreadable token, so that is a hand-built request).
        private static Color OverlayTint(string token) =>
            !string.IsNullOrEmpty(token) && ColorUtility.TryParseHtmlString(token, out var colour) ? colour : Color.white;

        // A key the bake holds replaces the class backdrop; any other key
        // (empty, or a file not there yet) leaves ApplyBackground's choice.
        private void ApplyFightBackdrop(string backdropKey)
        {
            if (backgroundImage == null || string.IsNullOrEmpty(backdropKey)) return;

            var sprite = ItemIcons.Find(fightBackdropArt, backdropKey);
            if (sprite != null) backgroundImage.sprite = sprite;
        }

        // The fight is over for this screen: the bed goes with it. LeaveFight
        // and the scene's teardown both come through here.
        private void EndPresentation()
        {
            SoundController.StopAmbience();
        }
    }
}
