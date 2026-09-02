using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrincesPalace
{
    // What FightController.RefreshVerbs actually distinguishes about a root
    // verb (Attack/Skill/Item/Hold Back), named after the distinction rather
    // than after "state 1/2/3": whether NOTHING about this verb is true right
    // now (Idle), whether ITS OWN branch is the one currently open -- or, with
    // nothing open yet, that it has gamepad focus (Open), or whether it is the
    // menu's own recommended default with nothing open or focused (Primary).
    // A button can be Open XOR Primary, never both -- RefreshVerbs' own
    // `highlighted ? ... : i == 0 ? ... : ...` chain is exactly that priority.
    public enum ThemedMenuState
    {
        Idle,
        Open,
        Primary,
    }

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

        // A MENU-STATE SEAM, for a button whose state is not just its own
        // pointer/selection history -- FightController.RefreshVerbs drives
        // this from what is open/focused/recommended in a menu these four
        // pointer handlers know nothing about. Idle by default so a button
        // nobody ever calls SetMenuState on (ContinueButton, TargetCancelButton
        // today) renders exactly as it always has.
        //
        // The four values below are what SetMenuState composes against --
        // Open/Primary tints and glow alphas, deliberately serialized fields
        // UiEmitter assigns (WireThemedButton) rather than consts baked into
        // this MonoBehaviour, so the numbers live with the rest of the kit's
        // authored values instead of being a second place a designer would
        // have to know to look.
        [SerializeField] internal Color OpenPlateTint = new Color(1.12f, 1.12f, 1.12f, 1f);
        [SerializeField] internal float OpenGlowAlpha = 0.5f;
        [SerializeField] internal Color PrimaryGlowColor = Color.white;
        [SerializeField] internal float PrimaryGlowAlpha = FocusGlowAlpha;

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

        private ThemedMenuState _menuState = ThemedMenuState.Idle;

        // Glow's own RGB, as Ui.ApplyTheme baked it for THIS button's theme
        // (alpha 0 at build time -- see UiNode.Theme's own comment). Captured
        // once so Open can fade the glow's alpha up in the verb's own colour
        // without this component having to know what that colour was; Primary
        // overrides it to PrimaryGlowColor instead, since a recommended-action
        // ring is gold regardless of the plate under it.
        private Color _themeGlowColor = Color.white;
        private bool _themeGlowCaptured;

        // Public for the same reason FocusGlowAlpha is: a test asserts
        // against the real state rather than reading it off a private field
        // through reflection.
        public ThemedMenuState CurrentMenuState => _menuState;

        private void Awake() => _button = GetComponent<Button>();

        private void OnEnable()
        {
            CaptureThemeGlowColor();
            Refresh();
        }

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

        // THE MENU-STATE SEAM. FightController.RefreshVerbs calls this once
        // per verb per repaint instead of writing targetGraphic.color the way
        // an unthemed button's row still does -- see that method for why the
        // two paths coexist.
        //
        // Snaps Glow's RGB immediately (Open's own theme tint, or Primary's
        // gold) rather than fading it: only alpha animates, in UpdateGlow's
        // existing fade, and the hue swap happens while alpha is at or near
        // zero in every real transition this menu produces, so there is
        // nothing for a hue fade to be seen doing.
        public void SetMenuState(ThemedMenuState state)
        {
            _menuState = state;
            CaptureThemeGlowColor();

            if (Glow != null)
            {
                var rgb = state == ThemedMenuState.Primary ? PrimaryGlowColor : _themeGlowColor;
                var current = Glow.color;
                Glow.color = new Color(rgb.r, rgb.g, rgb.b, current.a);
            }

            Refresh();
        }

        // Idempotent and safe to call before Glow is wired (BEFORE
        // WireThemedButton assigns it, at Editor build time) -- it simply
        // does nothing until Glow is non-null, and captures on the first call
        // afterward. RGB never changes once captured (only .a ever does, via
        // the fade coroutines below), so capturing late loses nothing.
        private void CaptureThemeGlowColor()
        {
            if (_themeGlowCaptured || Glow == null) return;
            _themeGlowColor = Glow.color;
            _themeGlowCaptured = true;
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

        // The menu state sets an AMBIENT floor (Open's partial glow, Primary's
        // gold ring); hover/select composes ON TOP as the stronger, temporary
        // signal a pointer or gamepad focus already meant before menu states
        // existed -- Max, not a replacement, so hovering an Open or Primary
        // verb still reads as more lit than either state alone.
        private float MenuStateGlowAlpha()
        {
            switch (_menuState)
            {
                case ThemedMenuState.Open: return OpenGlowAlpha;
                case ThemedMenuState.Primary: return PrimaryGlowAlpha;
                default: return 0f;
            }
        }

        private void UpdateGlow()
        {
            float focusAlpha = Interactable && (_isHovering || _isSelected) ? FocusGlowAlpha : 0f;
            float target = Interactable ? Mathf.Max(MenuStateGlowAlpha(), focusAlpha) : 0f;
            FadeGlowTo(target);
        }

        private Color MenuStatePlateTint() => _menuState == ThemedMenuState.Open ? OpenPlateTint : IdleTint;

        private void UpdatePlate()
        {
            var target = !Interactable ? DisabledTint : _isPressed ? PressedTint : MenuStatePlateTint();
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
            if (Plate != null) Plate.color = Interactable ? MenuStatePlateTint() : DisabledTint;
        }
    }
}
