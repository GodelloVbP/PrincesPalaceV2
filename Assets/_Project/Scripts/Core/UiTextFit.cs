using TMPro;
using UnityEngine;

namespace PrincesPalace
{
    // DOES THIS TEXT FIT ITS LABEL'S BOX? The one measurement, shared by the
    // scene build's UiTextFitAudit (authored copy, at build time) and the
    // PlayMode checks that measure runtime-only text -- content the audit
    // skips, like a dialogue line -- in the same emitted label. In Core
    // because Tests/PlayMode cannot reference Editor (CanvasCapture's reason).
    //
    // TMP's preferred size for `sample`, wrapped at the box's width, against
    // the box. Measured with the label's own font, size, style and spacing,
    // so the caller sets up the label (font size, auto-size off, style) the
    // way the text will really draw before asking.
    public static class UiTextFit
    {
        // A pixel or two of slack: TMP's preferred values and a hand-authored
        // box will not agree exactly, and failing over sub-pixel rounding
        // would train people to widen every box "just in case".
        public const float Tolerance = 1f;

        public static Vector2 Box(TMP_Text label) => ((RectTransform)label.transform).rect.size;

        public static bool Fits(TMP_Text label, string sample, out Vector2 preferred)
        {
            var box = Box(label);
            preferred = label.GetPreferredValues(sample, box.x, 0f);
            return preferred.x <= box.x + Tolerance && preferred.y <= box.y + Tolerance;
        }
    }
}
