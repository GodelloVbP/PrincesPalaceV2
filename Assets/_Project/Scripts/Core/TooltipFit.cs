using TMPro;
using UnityEngine;

namespace PrincesPalace
{
    // ONE COPY of "grow the tooltip box past its build-time one-line height
    // to fit whatever body just landed in it" -- ReckoningController's own
    // FitTooltipToBody and FightController.Hud's FitStatusTooltipToBody were
    // byte-for-byte the same method against two different sets of layout
    // constants (S1's review). Both call sites already SetContent(body) on
    // their own TMP_Text immediately before calling this, which is why there
    // is no separate `body` parameter here: `text.text` already holds it.
    //
    // Not on Domain/UiKit/TooltipPlacement.cs, which answers the related but
    // separate question of WHERE a tooltip sits -- that file is engine-free
    // (docs/CODE_STANDARDS.md "Layering", `Domain/` carries no UnityEngine
    // reference at all), and this needs RectTransform/TMP_Text to measure a
    // real mesh.
    public static class TooltipFit
    {
        // GetPreferredValues rather than TMP_Text.preferredHeight: the latter
        // reads the LAST laid-out mesh, and both callers run this in the same
        // frame the body text was just set -- preferredHeight would still be
        // answering for whatever body the box showed before this hover.
        //
        // MUST RUN BEFORE the caller places the tooltip against its own rect
        // -- both original call sites already documented this, and it stays
        // true of the shared version: placement reads back the sizeDelta
        // this writes.
        public static void ToBody(RectTransform self, TMP_Text text,
            float width, float pad, float minHeight, float maxHeight)
        {
            if (self == null) return;

            float height = minHeight;

            if (text != null)
            {
                float wanted = text.GetPreferredValues(text.text, width - pad * 2f, 0f).y;
                height = Mathf.Clamp(wanted + pad * 2f, minHeight, maxHeight);

                var textRect = text.rectTransform;
                textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, height - pad * 2f);
            }

            self.sizeDelta = new Vector2(width, height);
        }
    }
}
