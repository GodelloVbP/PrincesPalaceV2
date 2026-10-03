using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Where each relic chip sits in its strip, and how wide it is.
    //
    // THE TREE IS EMITTED AT THE WORST CASE, every chip at an equal share of
    // the strip, so the audit solves the case that can fail. At runtime fewer
    // chips share the same width: each takes what its name needs, capped at an
    // equal share of the room, and a name past the cap is cut by the label's
    // ellipsis. Pure, so the arithmetic is pinned without a scene.
    public static class RelicChipLayout
    {
        public readonly struct Slot
        {
            // From the strip's chip area's left edge.
            public readonly float Left;
            public readonly float Width;

            public Slot(float left, float width)
            {
                Left = left;
                Width = width;
            }
        }

        // `wanted` is each chip's natural width (icon, padding and its name).
        public static List<Slot> Lay(IReadOnlyList<float> wanted, float available, float gap, float minWidth)
        {
            var slots = new List<Slot>();
            int count = wanted?.Count ?? 0;
            if (count == 0) return slots;

            float share = (available - gap * (count - 1)) / count;
            float left = 0f;
            for (int i = 0; i < count; i++)
            {
                float width = wanted[i];
                if (width < minWidth) width = minWidth;
                if (width > share) width = share;

                slots.Add(new Slot(left, width));
                left += width + gap;
            }

            return slots;
        }

        // The equal share the tree is emitted at.
        public static float Share(int count, float available, float gap) =>
            count <= 0 ? 0f : (available - gap * (count - 1)) / count;
    }
}
