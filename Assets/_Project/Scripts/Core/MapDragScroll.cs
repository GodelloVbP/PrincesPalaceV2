using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // The map viewport's drag-to-pan. A press is only a drag once it travels
    // EventSystem.pixelDragThreshold, so a click on a room never pans, and a
    // drag that began on a room cancels that room's click (the press target is
    // not the drag target).
    public class MapDragScroll : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        // Receives how far the content should move UP, in canvas units.
        public Action<float> Dragged;

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.rootCanvas.scaleFactor > 0f ? canvas.rootCanvas.scaleFactor : 1f;
            Dragged?.Invoke(eventData.delta.y / scale);
        }
    }
}
