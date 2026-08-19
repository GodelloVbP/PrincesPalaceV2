using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Click or drag anywhere on a bar to set its value, 0..1.
    //
    // Attached at runtime to a track emitted by the screen tree, the same way
    // HoverIndex is -- the UiKit emits plain Images and Buttons, and behaviour
    // that needs a pointer position is not something a declarative tree can
    // state.
    //
    // There is no UnityEngine.UI.Slider anywhere in this project and this is
    // deliberately not the first: that component brings a handle, a fill rect,
    // an interactable/navigation model and a serialized value that would all
    // need emitting and wiring, to do what twenty lines do. What it would buy
    // is keyboard and gamepad focus, and neither is wired here yet -- when they
    // are, that is the moment to reconsider rather than now.
    public class BarSlider : MonoBehaviour, IPointerClickHandler, IDragHandler
    {
        public Action<float> Changed;

        public void OnPointerClick(PointerEventData eventData) => Set(eventData);

        // Drag and click share a path, so grabbing the bar and sweeping it
        // behaves like the click that started it rather than like a separate
        // gesture with its own rounding.
        public void OnDrag(PointerEventData eventData) => Set(eventData);

        private void Set(PointerEventData eventData)
        {
            var rect = transform as RectTransform;
            if (rect == null || Changed == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out var local))
            {
                return;
            }

            float width = rect.rect.width;
            if (width <= 0f) return;

            // Local x runs from -half to +half about the rect's own pivot, so
            // this maps the whole bar rather than only its right-hand side.
            float t = Mathf.Clamp01((local.x - rect.rect.xMin) / width);
            Changed(t);
        }
    }
}
