using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrincesPalace
{
    // The flat state controller a THEMED button gets instead of
    // ButtonPressAnimator/SubtleHoverScale - see UiEmitter.EmitButton's
    // Themed() branch for why the two are mutually exclusive with this one.
    //
    // NO TRANSFORM SCALING, deliberately. The press animators exist because a
    // plain Image has nothing else to give feedback WITH; a themed button has
    // its own Glow and Plate, so idle/focus/pressed/disabled are drawn states
    // on those two Images instead - Focus fades the glow ring in, Pressed dims
    // the plate, Disabled greys it, matching the mockup's four states
    // (final_dynamic_ui_mockup.png) rather than reproducing a different
    // button's animation language on top of art that already carries one.
    //
    // NO PERMANENT Update() either. Each transition is a short coroutine that
    // starts on the event that caused it and stops itself when the fade is
    // done - four states changing occasionally do not need a per-frame poll
    // the way a continuous hover-pop does.
    //
    // Glow/Plate are assigned by UiEmitter once Visuals' children exist
    // (WireThemedButton), the same internal + [SerializeField] shape every
    // other builder-wired controller reference in this project uses - see
    // CODE_STANDARDS.md 4a. This component itself creates no GameObjects
    // (UiKitLintTests' OnlyTheEmitterMayCreateGameObjects would refuse it).
    public class ThemedButtonState : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] internal Image Glow;
        [SerializeField] internal Image Plate;

        // Same default and the same override reason as ButtonPressAnimator:
        // UiNode.Quiet() flips this to Sound.None for a button whose own
        // controller plays a flourish on the same click.
        public Sound clickSound = Sound.ButtonClick;

        // Public for the same reason ButtonPressAnimator's own scale constants
        // are: a test asserts against the real target instead of duplicating
        // the number (AUDIT.md #18).
        public const float FocusGlowAlpha = 0.85f;
        private const float FadeSeconds = 0.12f;

        private static readonly Color PressedTint = new Color(0.72f, 0.72f, 0.72f, 1f);
        private static readonly Color DisabledTint = new Color(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color IdleTint = Color.white;

        private Button _button;
        private Coroutine _glowFade;
        private Coroutine _plateFade;
        private bool _isHovering;
        private bool _isSelected;
        private bool _isPressed;

        private void Awake() => _button = GetComponent<Button>();

        private void OnEnable() => Refresh();

        private void OnDisable()
        {
            // A hidden/deactivated button must not stay mid-fade for when it
            // is shown again - SetActive(false) stops every coroutine on this
            // object anyway, but the interaction flags driving them have to
            // reset too, or the next OnEnable's Refresh() reads stale ones.
            _isHovering = false;
            _isSelected = false;
            _isPressed = false;
            ApplyImmediate();
        }

        // THE REFRESH SEAM. Nothing here polls Button.interactable every
        // frame - whatever code path flips it calls this afterward instead.
        // Nothing in this project currently toggles a themed button's
        // interactable flag, so nothing calls this outside OnEnable/OnDisable
        // yet; it exists so the day something does, it has a cheap single
        // call rather than a reason to add a permanent Update().
        public void Refresh()
        {
            UpdateGlow();
            UpdatePlate();
        }

        private bool Interactable => _button == null || _button.interactable;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            UpdateGlow();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // A press that drags off the button before releasing must not
            // leave _isPressed stuck true with no OnPointerUp ever coming for
            // this object - same reasoning ButtonPressAnimator's own
            // OnPointerExit carries.
            _isHovering = false;
            _isPressed = false;
            UpdateGlow();
            UpdatePlate();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            UpdatePlate();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            UpdatePlate();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected = true;
            UpdateGlow();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            UpdateGlow();
        }

        // A CLICK, not a press - fires on release over the same button that
        // was pressed, matching ButtonPressAnimator's own reasoning.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable) return;
            SoundController.Play(clickSound);
        }

        private void UpdateGlow()
        {
            float target = Interactable && (_isHovering || _isSelected) ? FocusGlowAlpha : 0f;
            FadeGlowTo(target);
        }

        private void UpdatePlate()
        {
            var target = !Interactable ? DisabledTint : _isPressed ? PressedTint : IdleTint;
            FadePlateTo(target);
        }

        private void FadeGlowTo(float alpha)
        {
            if (Glow == null) return;
            if (_glowFade != null) StopCoroutine(_glowFade);
            if (!gameObject.activeInHierarchy) { SetAlphaImmediate(Glow, alpha); return; }
            _glowFade = StartCoroutine(FadeAlpha(Glow, alpha));
        }

        private void FadePlateTo(Color target)
        {
            if (Plate == null) return;
            if (_plateFade != null) StopCoroutine(_plateFade);
            if (!gameObject.activeInHierarchy) { Plate.color = target; return; }
            _plateFade = StartCoroutine(FadeColor(Plate, target));
        }

        private static void SetAlphaImmediate(Image image, float alpha)
        {
            var c = image.color;
            c.a = alpha;
            image.color = c;
        }

        private IEnumerator FadeAlpha(Image image, float target)
        {
            float start = image.color.a;
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                SetAlphaImmediate(image, Mathf.Lerp(start, target, Mathf.Clamp01(t / FadeSeconds)));
                yield return null;
            }
            SetAlphaImmediate(image, target);
        }

        private IEnumerator FadeColor(Image image, Color target)
        {
            Color start = image.color;
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                image.color = Color.Lerp(start, target, Mathf.Clamp01(t / FadeSeconds));
                yield return null;
            }
            image.color = target;
        }

        private void ApplyImmediate()
        {
            if (Glow != null) SetAlphaImmediate(Glow, 0f);
            if (Plate != null) Plate.color = Interactable ? IdleTint : DisabledTint;
        }
    }
}
