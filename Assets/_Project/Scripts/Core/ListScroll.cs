using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Wheel, drag and track-grab over a VERTICALLY scrolled list.
    //
    // The third of the three scroll behaviours this project has, and the split
    // between them is by GESTURE rather than by screen:
    //
    //   RailScroll  reports a horizontal DELTA -- pushing 19,000px of reward
    //               track past a 1,600px window, where the pointer's absolute
    //               position means nothing.
    //   BarSlider   reports a horizontal FRACTION -- grabbing the ascent ribbon
    //               at 60% means "show me 60% along".
    //   this        reports both, vertically, because a scrollbar needs both:
    //               the wheel and a drag over the rows are deltas, and a grab
    //               on the track is a position.
    //
    // ONE COMPONENT WITH TWO CALLBACKS rather than two components, because the
    // caller decides which it is by which delegate it sets: attached to the
    // viewport only Scrolled is wired, attached to the track only Seeked is.
    // A third copy of the same twenty lines was the alternative, and
    // docs/CODE_STANDARDS.md "Reuse" makes a second copy the moment to promote --
    // this is that promotion, made now because the two existing
    // ones are horizontal and neither could have taken a vertical caller
    // without growing an axis flag.
    //
    // No UnityEngine.UI.ScrollRect, for the reason RailScroll's header gives at
    // length: it owns the content's anchoredPosition, and the controller here
    // writes that same number when a list opens, when it is re-anchored to a
    // new count, and when the scroll is reset.
    [RequireComponent(typeof(RectTransform))]
    public class ListScroll : MonoBehaviour, IScrollHandler, IDragHandler, IPointerClickHandler
    {
        // Positive means "further DOWN the list", which is the direction scroll
        // itself runs in -- 0 is the top.
        public Action<float> Scrolled;

        // 0 at this rect's TOP edge, 1 at its bottom. Same direction, so a
        // thumb dragged to the bottom of its track shows the bottom of the list.
        public Action<float> Seeked;

        // How far one wheel notch travels. A row pitch: the list steps by whole
        // rows, so a notch never leaves a row half in the window.
        public float WheelStep = 54f;

        public void OnScroll(PointerEventData eventData)
        {
            if (Scrolled == null) return;

            float amount = eventData.scrollDelta.y;
            if (Mathf.Approximately(amount, 0f)) return;

            // Negated: a notch away from the player goes DOWN the list, which
            // is the same direction every other list in the game scrolls.
            Scrolled(-amount * WheelStep);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // A drag over the rows pushes the list under the pointer, so the
            // content follows the hand: dragging UP moves further down the
            // list. Rows are Buttons and take clicks, but a Button is not an
            // IDragHandler, so the drag bubbles here and a click still lands.
            if (Scrolled != null)
            {
                Scrolled(-eventData.delta.y);
                return;
            }

            Seek(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Scrolled != null) return;
            Seek(eventData);
        }

        private void Seek(PointerEventData eventData)
        {
            var rect = transform as RectTransform;
            if (rect == null || Seeked == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out var local))
            {
                return;
            }

            float height = rect.rect.height;
            if (height <= 0f) return;

            // Local y runs bottom-to-top, and scroll runs top-to-bottom, so
            // this is measured from yMax downward rather than from yMin up.
            Seeked(Mathf.Clamp01((rect.rect.yMax - local.y) / height));
        }
    }
}
