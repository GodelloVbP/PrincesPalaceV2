using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Hover and double-click for one gear cell.
    //
    // uGUI's Button gives a click and nothing else -- no enter, no exit, no
    // click count -- so the two gestures this screen wanted both needed a
    // handler of their own. It carries only an int and a bool, which is
    // exactly what a scene can serialise.
    //
    // The controller is found by walking UP the tree rather than being handed
    // in at build time. ScreenRegistry's Wire step runs in the editor and
    // delegates do not serialise, so a callback assigned there would silently
    // not exist in the shipped scene -- the same trap SaveSlotController's
    // refresh subscription is documented as avoiding.
    public class CellPointer : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] internal int index;

        // Which grid this cell belongs to. Two flat indexes into two different
        // collections would be one collection too many to tell apart by number
        // alone.
        [SerializeField] internal bool isBagCell;

        private CharacterOverlayController _owner;
        private bool _looked;

        private CharacterOverlayController Owner()
        {
            // includeInactive, because the overlay spends most of its life
            // switched off and this resolves on the first pointer event after
            // it opens.
            if (_owner == null) _owner = GetComponentInParent<CharacterOverlayController>(true);
            return _owner;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _looked = true;
            Owner()?.HoverCell(index, isBagCell, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_looked) return;
            _looked = false;
            Owner()?.HoverCell(index, isBagCell, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // The Button's own onClick has already run and selected this cell.
            // A second click within the platform's double-click window arrives
            // here with clickCount 2, and equips or unequips whatever the first
            // click selected -- so the gesture and the action button are always
            // doing the same thing to the same item.
            if (eventData == null || eventData.clickCount < 2) return;

            Owner()?.ActivateCell(index, isBagCell);
        }

        // The cell is hidden when its page has fewer items than cells, and a
        // hidden cell never receives OnPointerExit -- which would strand the
        // compare box describing something no longer on screen.
        private void OnDisable()
        {
            if (!_looked) return;
            _looked = false;
            Owner()?.HoverCell(index, isBagCell, false);
        }
    }
}
