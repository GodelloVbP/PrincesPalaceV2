using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Press and keep pressing. Reports progress while held, fires once at the
    // end, and forgets everything the moment the pointer leaves or lifts. Also
    // held by Submit (pad A, Enter) while this object is the selected one.
    //
    // Attached at RUNTIME by the controller, the same way HoverIndex and
    // BarSlider are: it carries two delegates, neither of which a scene can
    // serialise usefully, and both of which the controller already holds.
    //
    // UNSCALED TIME, and that is the whole reason this is a component rather
    // than four lines inside the controller that wanted it. The only screen
    // that asks for a hold is the system menu, and the system menu's entire
    // pause mechanism is Time.timeScale = 0. A hold counted on Time.deltaTime
    // there never advances: the fill sits at zero, the button never fires, and
    // nothing on screen says why. That failure is invisible in a scene that is
    // not paused, which is every other scene that might reuse this later.
    public class HoldToConfirm : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        // Overridable so a test can drive a hold in a few calls rather than
        // waiting out 1.2 seconds of real time per case.
        public float Seconds = 1.2f;

        // 0..1 while held, and 0 again on cancel or completion. Called on every
        // change rather than polled, so the fill has one writer.
        public Action<float> Progress;

        public Action Completed;

        private bool _held;
        private float _elapsed;

        public bool Holding => _held;

        public float Progress01 =>
            Seconds <= 0f ? 0f : Mathf.Clamp01(_elapsed / Seconds);

        // A pointer press takes over a Submit hold, so releasing Submit no
        // longer cancels it.
        public void OnPointerDown(PointerEventData eventData)
        {
            _submitHold = false;
            Begin();
        }

        public void OnPointerUp(PointerEventData eventData) => Cancel();

        // A pointer dragged off the button cancels it. Without this, pressing
        // and sliding away leaves the hold running under a cursor that is
        // somewhere else entirely, and it fires anyway.
        public void OnPointerExit(PointerEventData eventData) => Cancel();

        // The pane switching tabs mid-hold has to count as letting go. Nothing
        // delivers a pointer-up to a disabled object, so without this the hold
        // resumes from where it was the next time the pane is opened.
        private void OnDisable() => Cancel();

        public void Begin()
        {
            if (_held) return;
            _held = true;
            _elapsed = 0f;
            Report();
        }

        public void Cancel()
        {
            _submitHold = false;
            if (!_held && _elapsed <= 0f) return;
            _held = false;
            _elapsed = 0f;
            Report();
        }

        // Null reads the legacy Input Manager's Submit level (pad A, Enter).
        // A test cannot hold a real button down, so it swaps the level here;
        // TestGlobals.ResetAll puts it back.
        public static Func<bool> SubmitLevelOverride;

        private static bool SubmitLevel() =>
            SubmitLevelOverride != null ? SubmitLevelOverride() : Input.GetButton("Submit");

        // True while the current hold was started by Submit rather than a
        // pointer, so the poll only ever cancels its own hold.
        private bool _submitHold;
        private bool _submitWasDown;

        private void OnEnable() => _submitWasDown = SubmitLevel();

        private void Update() => Tick(Time.unscaledDeltaTime);

        // Submit level is the pad and keyboard route, not gated on
        // NavigationInputModule.LastInputWasPad: Enter on a selected control is
        // the same "activate" as A, and the module's own Submit is likewise
        // device-agnostic. Selection is the consent to hold -- a pointer hover
        // never selects, so a mouse user cannot start one with Enter by accident.
        //
        // A hold starts only on a fresh press (level false -> true) seen while
        // selected. The press that opened the dialog or moved selection here is
        // already down, so it is ignored until released; OnEnable seeds the
        // previous level for the same reason.
        public void Tick(float unscaledDelta)
        {
            bool down = SubmitLevel();
            bool selected = EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject;

            if (_submitHold)
            {
                if (!down || !selected) Cancel();
            }
            else if (!_held && down && !_submitWasDown && selected)
            {
                Begin();
                _submitHold = true;
            }
            _submitWasDown = down;

            if (_held) Advance(unscaledDelta);
        }

        // SPLIT FROM Update so it can be tested. Everything below is reachable
        // from a test; only the one line above is not -- the same split
        // SystemMenuController.HandleEscape exists for, and for the same reason:
        // a behaviour that can only be reached through the player loop is a
        // behaviour nothing in CI ever exercises.
        public void Advance(float seconds)
        {
            if (!_held || seconds <= 0f) return;

            _elapsed += seconds;
            if (_elapsed < Seconds)
            {
                Report();
                return;
            }

            // Cleared BEFORE the callback. Whatever Completed does is allowed
            // to tear this object down, and a hold left at full would otherwise
            // fire a second time if it survived.
            _held = false;
            _submitHold = false;
            _elapsed = 0f;
            Progress?.Invoke(0f);
            Completed?.Invoke();
        }

        private void Report() => Progress?.Invoke(Progress01);
    }
}
