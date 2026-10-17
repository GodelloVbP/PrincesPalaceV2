using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Wheel and drag over a horizontally scrolled rail, reported as a DELTA in
    // canvas units.
    //
    // A delta rather than a value, which is the whole difference between this
    // and BarSlider next door. That one maps a pointer POSITION onto 0..1 and
    // is right for the ascent ribbon, where grabbing at 60% means "show me 60%
    // of the track". This is right for the rail itself, where the gesture is
    // pushing nineteen thousand pixels of content past a window and the
    // pointer's absolute position means nothing at all.
    //
    // Attached at runtime by the controller, the same way HoverIndex and
    // BarSlider are: it carries a delegate, which a scene cannot serialise
    // usefully.
    //
    // NO UnityEngine.UI.ScrollRect, and deliberately. That component owns the
    // content's anchoredPosition, which is the one number this screen's glides,
    // its fly-in, its arrow keys and its ribbon all write -- so adopting it
    // would mean four features asking a ScrollRect to please stop, rather than
    // one controller doing arithmetic it already does. What it would buy is
    // inertia and elastic ends, neither of which was asked for.
    [RequireComponent(typeof(RectTransform))]
    public class RailScroll : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler
    {
        // Positive means "the content should move right", i.e. show earlier
        // levels. The controller decides what that means for its own clamp.
        public Action<float> Scrolled;

        // Raised when a gesture STARTS, so the controller can cancel whatever
        // glide is in flight. A drag that fights an animation is the single
        // most common way a scrolled panel feels broken.
        public Action Grabbed;

        // The wheel's own delta is in notches, not pixels; 1.4 converts that
        // into a comparable scroll distance.
        private const float WheelGain = 1.4f;

        // A trackpad reports both axes at once and a mouse wheel reports only
        // y. LARGER AXIS WINS rather than summing them: summing makes a
        // diagonal trackpad flick scroll faster than either axis alone, which
        // reads as the rail lurching.
        public void OnScroll(PointerEventData eventData)
        {
            if (Scrolled == null) return;

            var delta = eventData.scrollDelta;
            float amount = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x : delta.y;
            if (Mathf.Approximately(amount, 0f)) return;

            Grabbed?.Invoke();

            // NOT negated: scrolling down walks forward (content slides left,
            // revealing later levels to the right); scrolling up walks back.
            Scrolled(amount * WheelGain * Mathf.Abs(NodePitchStep()));
        }

        public void OnBeginDrag(PointerEventData eventData) => Grabbed?.Invoke();

        // 1:1 against the canvas, so the node under the cursor stays under
        // the cursor. eventData.delta is in screen pixels and the canvas
        // scales with the window (CanvasScaler.ScaleWithScreenSize), so it is
        // divided down to canvas units first.
        public void OnDrag(PointerEventData eventData)
        {
            if (Scrolled == null) return;
            if (Mathf.Approximately(eventData.delta.x, 0f)) return;

            Scrolled(CanvasUnits(this, eventData.delta.x));
        }

        // Screen pixels to canvas units under the root canvas's current scale.
        internal static float CanvasUnits(Component under, float screenPixels)
        {
            var canvas = under.GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.rootCanvas.scaleFactor > 0f ? canvas.rootCanvas.scaleFactor : 1f;
            return screenPixels / scale;
        }

        // One wheel notch moves a third of a pitch, so a scroll is a sweep
        // across nodes rather than a jump between them -- the arrow keys are
        // what step node to node, and having both do the same thing would waste
        // one of them.
        private static float NodePitchStep() => Domain.UiKit.RewardTrackLayout.NodePitch / 3f;
    }
}
