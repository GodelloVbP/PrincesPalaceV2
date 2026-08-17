using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Reports hover on one intent icon, by slot index.
    //
    // Added at RUNTIME by FightController rather than baked by the emitter,
    // because it carries a delegate and an index -- neither of which a scene can
    // serialise usefully, and both of which the controller already holds. The
    // scale-on-hover half is separate and IS baked (UiNode.Hovers), so the icon
    // still responds even if this never gets attached.
    public class IntentHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
