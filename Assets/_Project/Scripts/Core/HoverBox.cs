using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // What every unthemed button gets, at the one seam every Ui.Button() call
    // already goes through (Ui.ApplyHoverBox builds the HoverRim child by
    // default -- see that method's own header; UiEmitter.WireHoverBox wires
    // this component to it): a solid box around the control on hover/focus,
    // so changing what this one seam builds changes every button's hover
    // without editing any of their files.
    //
    // NO SCALING, NO FADE. The rim is built once at its final size
    // (Ui.ApplyHoverBox picks the pad/thickness) and this component only
    // flips its GameObject active. ThemedButtonState fades its Glow because a
    // themed button's focus ring is meant to breathe; a 2px hairline rim
    // fading in over a couple of frames reads as flicker at that thickness,
    // not as a transition, so this snaps.
    //
    // Shown on EITHER a real pointer hover OR gamepad/keyboard SELECTION
    // (ISelectHandler) -- SubtleHoverScale never answered to gamepad focus at
    // all, which is half of why it had to go, not just the pop.
    public class HoverBox : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        // The HoverRim child UiEmitter wires (WireHoverBox) once Ui.
        // ApplyHoverBox's declared child has actually been emitted. Built
        // StartInactive, so Refresh() below is the only thing that ever
        // turns it on -- public so UiWiringSweep's E3 (every [SerializeField]
        // reference non-null) covers it the same way it covers Glow/Plate.
        [SerializeField] public GameObject Rim;

        private bool _isHovering;
        private bool _isSelected;

        private void OnEnable() => Refresh();

        private void OnDisable()
        {
            // Mirrors SubtleHoverScale's own OnDisable reasoning: a button
            // hidden mid-hover (a row swapping out under the cursor, a
            // submenu closing under a held gamepad selection) must not come
            // back still showing a rim for a pointer or a focus that is no
            // longer there. SetActive(false) already skips Update for a
            // scale-driven component; for this one there is no Update to
            // skip, only a flag pair to clear before the rim is asked to
            // hide itself.
            _isHovering = false;
            _isSelected = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected = true;
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            Refresh();
        }

        private void Refresh()
        {
            if (Rim != null) Rim.SetActive(_isHovering || _isSelected);
        }
    }
}
