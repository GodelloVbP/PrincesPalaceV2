using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Pointer enter selects this control, so the mouse and the pad share one
    // "lit" state: a Selectable's highlighted and selected colours are the same
    // tint on a text-style menu item, and without this the item the pad left
    // selected and the item under the mouse are both lit at once.
    //
    // Attached only where a screen wants it (ScreenRegistry.LightTextItems).
    // A modal stops the raycast at its dimmer, so the items beneath it never
    // receive the enter and cannot pull focus out of the panel on top.
    public class HoverSelects : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            var events = EventSystem.current;
            if (events == null) return;

            var selectable = GetComponent<Selectable>();
            if (selectable == null || !selectable.IsInteractable()) return;

            events.SetSelectedGameObject(gameObject);
        }
    }
}
