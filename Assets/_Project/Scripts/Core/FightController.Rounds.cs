using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
    // The one exception is Bind: the round the fight opened on is shown at
    // once, toll and all, and its beat -- still queued from Begin -- is then
    // a no-op, because a round is presented once and never twice.
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
            }

            // The bed follows the presentation: a room fight's None stops a bed
            // a previous fight left running, and the same path again keeps it.
            SoundController.StartAmbience(_presentation.AmbiencePath);

            if (shows && _session != null) PresentRound(_session.Round);
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
                float scale = _presentation.OverlayScaleFor(round);
                roundOverlay.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            SoundController.PlayClip(_presentation.RoundSfxPath);
        }

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
