using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Press feedback only, added to every button from one choke point
    // (UiEmitter.EmitButton) rather than per-button, same reasoning as the
    // shared font and button sprite.
    //
    // Owner's call, 2026-09-23: "hovering makes things pop out a bit and get
    // bigger. That is fun for a website but not a game." NO HOVER EFFECT AT
    // ALL any more -- this used to lerp localScale up to HoverScale on
    // OnPointerEnter the same way it lerped down on press; that half is
    // gone, along with the Enter/Exit-driven hover bookkeeping it needed.
    // Every unthemed button's hover/focus feedback is the rim now
    // (Ui.ApplyHoverBox/Core/HoverBox.cs, built by default -- see
    // Ui.Button's own header), attached alongside THIS component, not
    // instead of it (second pass, same date): a colour dim on press and a
    // box on hover/focus answer two different gestures and do not fight
    // each other the way the old scale-pop and rim both would have.
    //
    // The press feedback ALSO used to be a localScale pop (down to
    // PressScale) and is now a plate dim instead -- the same
    // ThemedButtonState idiom (Pressed dims the plate, no transform change)
    // rather than a second "shrink" reading as a second flavour of the pop
    // the owner just asked to have removed everywhere. Colour, not
    // Animator/AnimationClip: this project has no tweening library, and one
    // named transition (idle/pressed) does not need one.
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
    public class ButtonPressAnimator : MonoBehaviour, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
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

        // Public so tests can assert against the real target instead of
        // duplicating the number (AUDIT.md #18's lesson about a test that
        // re-derives what it is testing). Same value as ThemedButtonState.
        // PressedTint -- one "how much a pressed plate dims" answer, not two.
        public static readonly Color PressedTint = new Color(0.72f, 0.72f, 0.72f, 1f);

        // Lerp factor per second, not a fixed duration -- snappy enough to read
        // as an immediate response to input rather than a delayed animation
        // catching up.
        private const float LerpSpeed = 18f;

        private Button _button;
        private Image _image;
        private Color _baseColor;
        private bool _isPressed;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _image = GetComponent<Image>();
            if (_image != null)
            {
                _baseColor = _image.color;
            }
        }

        private void OnDisable()
        {
            // A hidden/deactivated button (e.g. Continue swapping in for the
            // choice row) must not stay mid-animation for when it is shown
            // again -- SetActive(false) skips Update entirely, so this has to
            // reset eagerly rather than let Update ease it back down.
            _isPressed = false;
            if (_image != null)
            {
                _image.color = _baseColor;
            }
        }

        private void Update()
        {
            if (_image == null) return;
            bool interactable = _button == null || _button.interactable;
            // Chromeless buttons keep their base colour's alpha (often 0)
            // because PressedTint's alpha is 1 -- multiplying, not
            // overwriting, means a pressed chromeless button stays invisible
            // rather than flashing a grey plate nothing else on it has.
            var target = interactable && _isPressed ? _baseColor * PressedTint : _baseColor;
            _image.color = Color.Lerp(_image.color, target, Time.unscaledDeltaTime * LerpSpeed);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // A press that drags off the button before releasing must not leave
            // _isPressed stuck true with no OnPointerUp ever coming for this
            // object.
            _isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => _isPressed = true;

        public void OnPointerUp(PointerEventData eventData) => _isPressed = false;
    }
}
