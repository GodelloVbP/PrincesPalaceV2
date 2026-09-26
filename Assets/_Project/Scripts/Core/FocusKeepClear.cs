using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace
{
    // A RECT THE FOCUS MARKER STANDS OFF BUT NEVER TREATS AS A NEIGHBOUR --
    // today, a list's scrollbar track (FocusMarkerPlacement.Resolve's header:
    // an arrow beside a bar does not read as selecting the bar, it only
    // crowds it, so the marker steps past it instead of changing sides).
    //
    // A component rather than something FocusMarker infers, because nothing
    // it can see says "scrollbar": both bars in the project are plain decor
    // Images the emitter built, grabbable only through a behaviour their
    // controller adds at wire time (ListScroll on the fight's submenu track,
    // BarSlider on the dossier's pack track). The controller that adds that
    // behaviour adds this beside it -- the same place, the same line of
    // reasoning, and no scene change.
    //
    // A live registry (OnEnable/OnDisable) rather than a scene search: the
    // marker decides every frame, and a bar hidden because its list fits
    // leaves the registry the moment it is deactivated.
    public sealed class FocusKeepClear : MonoBehaviour
    {
        private static readonly List<FocusKeepClear> Live = new List<FocusKeepClear>();

        public static IReadOnlyList<FocusKeepClear> Active => Live;

        public RectTransform Rect => (RectTransform)transform;

        private void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
        }

        private void OnDisable() => Live.Remove(this);

        // Idempotent, so a controller that re-wires on a second Awake path
        // does not stack copies.
        public static void Mark(GameObject go)
        {
            if (go != null && go.GetComponent<FocusKeepClear>() == null) go.AddComponent<FocusKeepClear>();
        }
    }
}
