using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // The subtler hover pop the battle UI wants for WIDE rows -- submenu rows
    // (1.02) and enemy plates (1.03) -- where ButtonPressAnimator's 105/95
    // would visibly shift the row's own text sideways. Hover only, no
    // press-shrink: these rows are read and clicked, not "pressed" the way a
    // root verb is.
    //
    // MIGRATED FROM v1 alongside ButtonPressAnimator, and it has to come with
    // it rather than after it: the two are a pair, and the reason the fight's
    // plates and rows look wrong under the press animator is exactly why this
    // exists. A node asks for it with UiNode.Hovers(scale), and UiEmitter then
    // gives that node this INSTEAD of ButtonPressAnimator -- never both, which
    // would fight over the same localScale every frame.
    public class SubtleHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float HoverScale = 1.02f;
        private const float RestingScale = 1f;
        private const float LerpSpeed = 18f;

        private RectTransform _rect;
        private Vector3 _baseScale;
        private bool _isHovering;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _baseScale = _rect.localScale;
        }

        private void OnDisable()
        {
            _isHovering = false;
            if (_rect != null)
            {
                _rect.localScale = _baseScale;
            }
        }

        private void Update()
        {
            var target = _baseScale * (_isHovering ? HoverScale : RestingScale);
            _rect.localScale = Vector3.Lerp(_rect.localScale, target, Time.unscaledDeltaTime * LerpSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData) => _isHovering = true;

        public void OnPointerExit(PointerEventData eventData) => _isHovering = false;
    }
}
