using System.Collections.Generic;

namespace PrincesPalace.Domain.Party
{
    // ONE SCALE PER SLOT KIND (seat, card), not a per-actor fit. Before this,
    // PartyController fit each occupant's idle sprite into its own fixed box
    // independently -- correct for one actor alone in a box, wrong the
    // moment two actors' canvases differ in size, because pixel size is this
    // project's only encoding of on-screen size (no per-actor scale lives in
    // content, see ContentSchema). An actor delivered on a BIGGER canvas got
    // shrunk MORE to fill the same box than an actor on a smaller one, so
    // Bjorn (bear, idle canvas 486x467) read as the smallest of the three in
    // the party pane despite being drawn a head taller than Shawn (sheep,
    // 540x370) on the fight stage, where every actor already draws at native
    // pixel size with no per-actor fit at all.
    //
    // SLOT-HEIGHT OVER MAX CANVAS HEIGHT, not over the roster's idle BBOX
    // heights: this is the number that guarantees the TALLEST actor's own
    // canvas -- padding, swung weapon, cape and all -- lands inside the slot.
    // A bbox-based scale only clears the visible pixels; the canvas around
    // them (which still occupies vertical space once the common scale is
    // applied, because every actor is scaled by the same factor) can then
    // push past the slot. Sizing off the canvas is the choice that cannot
    // clip.
    public static class PartyArtScale
    {
        // No sprite at all (an empty roster, or every entry lacking art) has
        // nothing to scale against -- 1f is the same "draw at native size"
        // answer preserveAspect gave a fixed box with nothing loaded, and it
        // means a caller never has to guard the roster's own emptiness
        // before asking.
        public const float NoRosterScale = 1f;

        public static float ScaleFor(float slotHeight, IEnumerable<float> canvasHeights)
        {
            float tallest = 0f;
            if (canvasHeights != null)
            {
                foreach (float height in canvasHeights)
                {
                    if (height > tallest) tallest = height;
                }
            }

            return tallest > 0f ? slotHeight / tallest : NoRosterScale;
        }
    }
}
