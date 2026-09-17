using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // THE NUMBERS ARE THEMEDBUTTONSTATE'S OWN, not new ones
    // (docs/GAMEPAD_NAVIGATION_PLAN.md section 7/8): a themed Button already
    // answers EventSystem focus with SelectedGlowAlpha at SelectedGlowScale --
    // a halo grown past the control's own edges rather than merely a
    // brighter ring the same size as it. A control with no ThemedButtonState
    // borrows that same ratio through a plain Image instead of inventing a
    // second one.
    //
    // PartyController.PaintSelectHalo (the first user of this shape,
    // AUDIT.md #156/party pass 2026-09-07) is not routed through this --
    // that method is private and already shipped, and re-pointing it here
    // is a no-op refactor with nothing to gain against the risk of touching
    // a controller with its own green suite for a phase-3 screen that does
    // not need Party changed. Hub is this helper's first caller; a later
    // screen with the same shape (an un-themed focusable, no
    // ThemedButtonState) should call this rather than write an eighth copy.
    public static class SelectHaloPainter
    {
        public static void Paint(Image halo, bool selected)
        {
            if (halo == null) return;

            halo.gameObject.SetShown(selected);
            if (!selected) return;

            var colour = halo.color;
            halo.color = new Color(colour.r, colour.g, colour.b, ThemedButtonState.SelectedGlowAlpha);
            halo.rectTransform.localScale = Vector3.one * ThemedButtonState.SelectedGlowScale;
        }
    }
}
