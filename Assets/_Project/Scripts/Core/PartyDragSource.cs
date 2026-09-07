using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Drag surface for one seat button or one roster card button.
    //
    // Attached at BUILD time (ScreenRegistry.WireParty's `result.Attach<
    // PartyDragSource>` per node, one array for seats and one for cards) so
    // UiWiringSweep sees the arrays the way it sees FightController's own
    // partyActorAnimators -- but WHAT a drag on a given node does is a
    // per-index closure, and a closure cannot be baked into a scene. So
    // nothing here is [SerializeField]: PartyController.Wire assigns these
    // three delegates at RUNTIME, the same split RailScroll/BarSlider draw
    // between "attached once" and "told what to do every time" (see their
    // own header comments).
    //
    // Lives on the SAME GameObject as the seat/card Button it drags, on
    // purpose: uGUI only suppresses a Button's own onClick after a drag when
    // the drag handler sits on that Button's own object (see PartyController.
    // Wire's comment on the click-vs-drag test this makes possible), so
    // splitting this onto a child node would silently bring the "a completed
    // drag also selects" bug back.
    public class PartyDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<PointerEventData> Begin;
        public Action<PointerEventData> Dragging;
        public Action<PointerEventData> End;

        public void OnBeginDrag(PointerEventData eventData) => Begin?.Invoke(eventData);

        public void OnDrag(PointerEventData eventData) => Dragging?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => End?.Invoke(eventData);
    }
}
