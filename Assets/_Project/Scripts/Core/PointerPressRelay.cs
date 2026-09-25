using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // A LEFT PRESS ANYWHERE ON A RECT, reported as a delegate. Added at
    // runtime, the bargain ListScroll and HoverIndex make: it carries a
    // delegate, and a scene cannot serialise one usefully.
    //
    // On pointer DOWN, deliberately: a press is counted by the frame it
    // started on (docs/PLAN_DIALOGUE_STAGE.md contract 17), and uGUI only
    // clicks a Button whose own rect took the down -- so a press that began
    // here can never finish as a click on a row that appeared under it.
    [RequireComponent(typeof(RectTransform))]
    public class PointerPressRelay : MonoBehaviour, IPointerDownHandler
    {
        public Action Pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Pressed?.Invoke();
        }
    }
}
