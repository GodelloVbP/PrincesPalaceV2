using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // Where a silhouette overlay (hit flash, burn glow) stands relative to the
    // figure it shadows. The overlay is a stretch SIBLING of the sprite, so it
    // inherits the slot's box but not what the stage writes onto the sprite
    // itself: GroundTheFigure's drop and drift go onto the sprite's own offsets,
    // and a silhouette that does not carry them whitens a figure standing
    // somewhere else. On Shawn the hurt drawing's feet sit 41px higher on its
    // canvas than idle's, so an overlay without the offsets floated up by that
    // much. The flip has the same shape of failure on the other axis, hence
    // `scale` is handed in.
    internal static class SilhouetteLayout
    {
        internal static void Copy(Image figure, Image overlay, Vector3 scale)
        {
            overlay.preserveAspect = figure.preserveAspect;

            var rect = overlay.rectTransform;
            var from = figure.rectTransform;
            rect.localScale = scale;
            rect.offsetMin = from.offsetMin;
            rect.offsetMax = from.offsetMax;
        }
    }
}
