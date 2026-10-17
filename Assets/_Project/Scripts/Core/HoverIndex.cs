using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Reports hover on one indexed thing: an enemy's intent icon, an offer
    // card on the Reckoning, anything with a row of siblings and a tooltip.
    //
    // ONE component rather than one per screen: the fight's intent icons and
    // the reward screen's offer cards share it.
    //
    // Added at RUNTIME by the controller rather than baked by the emitter,
    // because it carries a delegate and an index -- neither of which a scene can
    // serialise usefully, and both of which the controller already holds.
    public class HoverIndex : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public int Index;
        public Action<int, bool> Changed;

        public void OnPointerEnter(PointerEventData eventData) => Changed?.Invoke(Index, true);

        public void OnPointerExit(PointerEventData eventData) => Changed?.Invoke(Index, false);

        // A tooltip anchored to an object that gets deactivated mid-hover would
        // otherwise stay open forever, because no exit event is ever delivered
        // to a disabled object. The dead monster's icon vanishing is exactly
        // that case.
        private void OnDisable() => Changed?.Invoke(Index, false);
    }
}
