using UnityEngine;

namespace PrincesPalace
{
    // The command columns' open animation: translateX -10px -> 0, opacity
    // 0 -> 1, 140ms ease-out. Played from OnEnable rather than a driven
    // trigger -- every column this attaches to (submenu, detail, target
    // prompt) is shown purely through SetActive, so "just activated" already
    // IS "just opened". Same Update()-driven lerp style as
    // ButtonPressAnimator/SubtleHoverScale rather than a coroutine, since this
    // project has no tweening library and none of its animations need one.
    //
    // MIGRATED FROM v1. The OnEnable premise was re-checked against v2 rather
    // than assumed: FightController.Hud drives all three columns with
    // SetActive (submenuColumn, detailColumn, targetPrompt), exactly as v1's
    // Hud did, so the trigger this relies on still holds.
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class ColumnOpenAnimator : MonoBehaviour
    {
        private const float DurationSeconds = 0.14f;
        private const float SlideDistance = 10f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _restPosition;
        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _group = GetComponent<CanvasGroup>();
            _restPosition = _rect.anchoredPosition;
        }

        private void OnEnable()
        {
            // _restPosition is captured ONCE, in Awake, and never re-read here.
            // Reading it in OnEnable instead would capture the slid position on
            // the second open and walk the column 10px left every time it was
            // shown.
            _elapsed = 0f;
            _playing = true;
            _rect.anchoredPosition = _restPosition + new Vector2(-SlideDistance, 0f);
            _group.alpha = 0f;
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / DurationSeconds);
            // Quadratic ease-out: fast start, settling in gently rather than
            // arriving at a constant rate.
            float eased = 1f - (1f - t) * (1f - t);

            _rect.anchoredPosition = Vector2.LerpUnclamped(_restPosition + new Vector2(-SlideDistance, 0f), _restPosition, eased);
            _group.alpha = eased;

            if (t >= 1f)
            {
                _rect.anchoredPosition = _restPosition;
                _group.alpha = 1f;
                _playing = false;
            }
        }
    }
}
