using UnityEngine;

namespace PrincesPalace
{
    // A hold-to-confirm control's fill bar: authored at its real (non-zero)
    // width -- UiAudit refuses a zero-sized graphic -- then grown from a
    // pivot pinned to its left edge as the hold progresses, rather than
    // driven through anchors. Growing sizeDelta.x from a pinned edge is what
    // lets one number track the hold; growing from the centre would grow
    // both ways at once and paint outside the track. Shared by
    // ExitsController's abandon hold and ResetProgressController's confirm
    // hold -- both pivot-left rects with a fixed height. Not the same
    // mechanism as FightController.Hud.cs's own SetFill, which drives an HP
    // bar through anchor-stretch on purpose.
    public static class HoldFillMath
    {
        public static void SetFill(RectTransform fill, float progress, float width, float height)
        {
            if (fill == null) return;

            fill.sizeDelta = new Vector2(width * Mathf.Clamp01(progress), height);
        }
    }
}
