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
        private bool _isPressed;

        // THE COLOUR IS BORROWED, NOT OWNED. Map tiles, talent stars and the
        // hub's unbuilt building are tinted at runtime through this same Image
        // (it is the Button's targetGraphic), so this component only touches
        // it from a press until the release has eased back, and gives back
        // whatever colour it found -- never one captured at Awake. The first
        // cut of the dim held an Awake colour and eased toward it every frame,
        // which wiped every one of those tints within a few frames.
        //
        // `_rest` is the colour to return to; `_written` is the last colour
        // this component wrote. A mismatch at the next Update means someone
        // else painted the Image meanwhile, and that newer colour becomes the
        // one to return to.
        private bool _animating;
        private Color _rest;
        private Color _written;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _image = GetComponent<Image>();
        }

        private void OnDisable()
        {
            // A hidden/deactivated button (e.g. Continue swapping in for the
            // choice row) must not stay mid-animation for when it is shown
            // again -- SetActive(false) skips Update entirely, so this has to
            // reset eagerly rather than let Update ease it back down.
            if (_animating && _image != null && _image.color == _written) _image.color = _rest;
            _isPressed = false;
            _animating = false;
        }

        private void Update()
        {
            if (_image == null) return;

            if (_animating && _image.color != _written) _rest = _image.color;
            if (!_animating && !_isPressed) return;

            if (!_animating)
            {
                _rest = _image.color;
                _animating = true;
            }

            bool interactable = _button == null || _button.interactable;
            // Chromeless buttons keep their rest colour's alpha (often 0)
            // because PressedTint's alpha is 1 -- multiplying, not
            // overwriting, means a pressed chromeless button stays invisible
            // rather than flashing a grey plate nothing else on it has.
            var target = interactable && _isPressed ? _rest * PressedTint : _rest;
            var next = Color.Lerp(_image.color, target, Time.unscaledDeltaTime * LerpSpeed);

            // Released and home: hand the colour back exactly and stop
            // touching it, so the next repaint by its owner is left alone.
            if (!_isPressed && Near(next, target))
            {
                next = target;
                _animating = false;
            }

            _image.color = next;
            _written = next;
        }

        // Within a hundredth on every channel -- invisible, and reached in
        // well under a tenth of a second at LerpSpeed. Color's own == is a
        // 1e-5 test an exponential ease takes most of a second to satisfy,
        // which would leave the animator holding the colour long after the
        // release looked finished.
        private static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f &&
            Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;

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
