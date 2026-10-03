using System;

namespace PrincesPalace.Domain.Content
{
    // A LAYER'S SLIDE ACROSS ITS OWN LIFE: where it starts and where it ends,
    // as fractions of its own drawn box, eased out. A layer is otherwise placed
    // once (dx, dy), so a figure that leans in, or a mist that parts, had no
    // way to move without a travel path to a target.
    //
    // In box fractions rather than pixels so one authored lean is the same
    // lean on a rat and on a treant: `fit: target` already scales the box.
    // X mirrors with the cast's facing, like dx.
    [Serializable]
    public class SpellDrift
    {
        [ContentDoc("Horizontal offset at the layer's opening instant, in widths of its own drawn box (mirrors with the cast). Added to dx.")]
        public float fromX;

        [ContentDoc("Horizontal offset when the layer's lifetime ends, in widths of its own drawn box. The slide between the two eases out.")]
        public float toX;

        [ContentDoc("Vertical offset at the layer's opening instant, in heights of its own drawn box (positive is up).")]
        public float fromY;

        [ContentDoc("Vertical offset when the layer's lifetime ends, in heights of its own drawn box.")]
        public float toY;

        [ContentDoc("Optional waypoint, for a layer that goes out and comes back: where the layer is (box widths, X mirrors with the cast) at viaAt. Unused unless viaAt is above 0.")]
        public float viaX;

        [ContentDoc("The waypoint's height, in heights of the layer's own box.")]
        public float viaY;

        [ContentDoc("Fraction of the layer's lifetime, 0-1 exclusive, at which it passes through viaX/viaY; 0 means no waypoint. Each leg eases out.")]
        public float viaAt;

        public bool IsAuthored => fromX != 0f || toX != 0f || fromY != 0f || toY != 0f || viaAt > 0f;

        public SpellDrift Copy() => new SpellDrift
        {
            fromX = fromX, toX = toX, fromY = fromY, toY = toY, viaX = viaX, viaY = viaY, viaAt = viaAt,
        };

        // The offset `age` seconds into a `lifetime`-long layer, in box
        // fractions. Quadratic ease-out: the layer covers most of the distance
        // early and settles, the lean-in of someone arriving.
        private static (float x, float y) Leg(float ax, float ay, float bx, float by, float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            return (ax + (bx - ax) * eased, ay + (by - ay) * eased);
        }

        public (float x, float y) At(float age, float lifetime)
        {
            float t = lifetime <= 0f ? 1f : Math.Min(1f, Math.Max(0f, age / lifetime));
            if (viaAt > 0f && viaAt < 1f)
            {
                if (t < viaAt) return Leg(fromX, fromY, viaX, viaY, t / viaAt);
                return Leg(viaX, viaY, toX, toY, (t - viaAt) / (1f - viaAt));
            }

            return Leg(fromX, fromY, toX, toY, t);
        }
    }
}
