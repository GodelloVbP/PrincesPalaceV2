using UnityEngine;

namespace PrincesPalace
{
    // The part of a selectable the pad marker levels with, when that is not
    // the selectable's own centre: a two-line item (caption over subline) is
    // pointed at level with its caption, not between the two lines.
    // FocusMarker reads it; the marker's edge and neighbour rules still run
    // against the anchor's rect.
    public class FocusAnchor : MonoBehaviour
    {
        public RectTransform Anchor;
    }
}
