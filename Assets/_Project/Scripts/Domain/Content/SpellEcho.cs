using System;

namespace PrincesPalace.Domain.Content
{
    // AFTERIMAGES OF A MOVING LAYER: `count` faint copies of the layer's own
    // drawing, each `lagSeconds` further behind than the one before, sampled
    // from the layer's own motion (turn, slide, flight) at that earlier instant.
    // A copy is only as visible as the layer has moved since, so a held layer
    // leaves none and a fast one leaves a streak: strongest where it is fastest
    // with no authored schedule to keep in step with the motion.
    //
    // Each copy costs one pooled renderer for as long as the layer lives, which
    // is what SpellPoolCapacityTests counts.
    [Serializable]
    public class SpellEcho
    {
        public const float DefaultSpread = 120f;

        [ContentDoc("How many lagged copies trail the layer; 0 means none. Each takes a pooled renderer.")]
        public int count;

        [ContentDoc("Seconds each copy trails the one before it: copy k draws the layer as it was k * lagSeconds ago.")]
        public float lagSeconds;

        [ContentDoc("Opacity of the nearest copy at full strength, 0-1; each further copy is weaker by an even step. Multiplied by the layer's own alpha.")]
        public float alpha;

        [ContentDoc("How far, in reference-frame units, a copy has to lie from the layer's current drawing to show at full strength; nearer is fainter, none at all when the layer has not moved. 0 means 120.")]
        public float spread;

        public bool IsAuthored => count > 0;

        public float SpreadOrDefault => spread > 0f ? spread : DefaultSpread;

        public SpellEcho Copy() => new SpellEcho { count = count, lagSeconds = lagSeconds, alpha = alpha, spread = spread };

        // The strength of copy `k` (1-based) when it lies `distance` from the layer.
        public float StrengthOf(int k, float distance)
        {
            if (count <= 0 || k < 1 || k > count || alpha <= 0f) return 0f;

            float step = (count - k + 1) / (float)count;
            float moved = Math.Min(1f, Math.Max(0f, distance / SpreadOrDefault));
            return alpha * step * moved;
        }
    }
}
