using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrincesPalace
{
    // A small scale "pop" on hover and press, added to every button from one
    // choke point (UiEmitter.EmitButton) rather than per-button, same reasoning
    // as the shared font and button sprite. Pure RectTransform.localScale
    // animation, no Animator/AnimationClip asset needed -- this project has no
    // tweening library, and three named states (idle/hover/press) don't need
    // one.
    //
    // Implements the pointer interfaces directly rather than wrapping Button's
    // own onClick -- EventSystem calls every matching interface on a
    // GameObject, so this composes with the existing Button component without
    // touching its click wiring at all.
    //
    // MIGRATED FROM v1, where it was attached in SceneBuilder.CreateButton.
    // It did not survive the rebuild, and neither did anything replacing it:
    // every button in v2 was silent and static, while Sound.ButtonClick's own
    // comment still read "Fired by ButtonPressAnimator on every button in the
    // game" -- the intent outlived the implementation by the whole rebuild.
    // v2's single EmitButton is a strictly better choke point than v1's
    // several CreateButton variants, so the rule is easier to hold here.
    public class ButtonPressAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        // What this button sounds like. Defaults to the shared click, so every
        // button in the game gets one for free from the same choke point that
        // already gives them all the press animation -- there is no per-button
        // wiring to forget, and EveryButtonInTheScene_HasAClickSound doubles as
        // the guarantee that no button is silent.
        //
        // Overridable for a button that should stay quiet (UiNode.Quiet()),
        // which exists for a control whose own controller plays a flourish on
        // the same click and would otherwise double it.
        public Sound clickSound = Sound.ButtonClick;

        // A CLICK, not a press. IPointerClickHandler fires on release over the
        // same button that was pressed, so dragging off before releasing
        // correctly makes no sound -- the same reason OnPointerExit clears
        // _isPressed below.
        public void OnPointerClick(PointerEventData eventData)
        {
            // EventSystem still dispatches to a non-Button handler on a
            // GameObject whose Selectable is non-interactable, so a greyed-out
            // button would otherwise click audibly while doing nothing.
            if (_button != null && !_button.interactable)
            {
                return;
            }

            SoundController.Play(clickSound);
        }

        // Public so tests can assert against the real targets instead of
        // duplicating the numbers (AUDIT.md #18's lesson about a test that
        // re-derives what it is testing).
        public const float HoverScale = 1.05f;
        public const float PressScale = 0.95f;
        public const float RestingScale = 1f;

        // Lerp factor per second, not a fixed duration -- snappy enough to read
        // as an immediate response to input rather than a delayed animation
        // catching up.
        private const float LerpSpeed = 18f;

        private RectTransform _rect;
        private Button _button;
        private Vector3 _baseScale;
        private bool _isHovering;
        private bool _isPressed;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _button = GetComponent<Button>();
            _baseScale = _rect.localScale;
        }

        private void OnDisable()
        {
            // A hidden/deactivated button (e.g. Continue swapping in for the
            // choice row) must not stay mid-animation for when it is shown
            // again -- SetActive(false) skips Update entirely, so this has to
            // reset eagerly rather than let Update ease it back down.
            _isHovering = false;
            _isPressed = false;
            if (_rect != null)
            {
                _rect.localScale = _baseScale;
            }
        }

        private void Update()
        {
            bool interactable = _button == null || _button.interactable;
            float targetMultiplier = !interactable ? RestingScale : _isPressed ? PressScale : _isHovering ? HoverScale : RestingScale;
            var target = _baseScale * targetMultiplier;
            _rect.localScale = Vector3.Lerp(_rect.localScale, target, Time.unscaledDeltaTime * LerpSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData) => _isHovering = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            // A press that drags off the button before releasing must not leave
            // _isPressed stuck true with no OnPointerUp ever coming for this
            // object.
            _isHovering = false;
            _isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => _isPressed = true;

        public void OnPointerUp(PointerEventData eventData) => _isPressed = false;
    }
}
