using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // A pointer click on a control, reported even when its Button is not
    // interactable. Button.onClick never fires for such a control, so a screen
    // that wants to say WHY a press went nowhere (the map's refused rooms)
    // listens here. Sibling of HoverIndex and SelectIndex.
    public class ClickIndex : MonoBehaviour, IPointerClickHandler
    {
        public int Index;
        public Action<int> Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(Index);
    }
}
