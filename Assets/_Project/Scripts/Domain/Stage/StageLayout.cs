using System;

namespace PrincesPalace.Domain.Stage
{
    // Fake-depth ("2.5D") placement math for the fight stage: where along an
    // imaginary receding ground plane each enemy slot sits, how big it draws
    // there, and which order the slots must be drawn in so nearer figures
    // overlap farther ones.
    //
    // Deliberately engine-free and in Domain despite being presentation
    // rather than game rules: it is pure deterministic math over floats with
    // no UnityEngine types, which means it can be unit-tested in EditMode
    // (that assembly can only reference Domain). The actual pixel anchors —
    // where "near" and "far" physically are on a 1920x1080 canvas — stay in
    // SceneBuilder, because those are genuinely a UI concern and change
    // whenever the layout does. This file only answers "how far back is slot
    // i, and what does depth do to size", which is the part worth pinning
    // with tests.
    //
    // Depth runs 0 (nearest the camera, drawn largest and lowest on screen,
    // in front of everything) to 1 (farthest, smallest, highest, behind).
    // That direction is the convention every method here assumes.
    public static class StageLayout
    {
        // A far enemy draws at 58% of a near one. Chosen to read clearly as
        // distance at a glance without shrinking the back row into
        // illegibility — the sprites are ~330-440px tall at full size, so
        // the back row still lands around 190-250px.
        public const float NearScale = 1f;
        public const float FarScale = 0.58f;

        // Depth of a slot within a row of `slotCount` slots. Slot 0 is
        // always nearest. A single-slot row sits fully forward rather than
        // splitting the difference — a lone boss should dominate the stage,
        // not hover at mid-depth.
        public static float DepthForSlot(int slotIndex, int slotCount)
        {
            if (slotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), "A stage needs at least one slot.");
            }

            if (slotIndex < 0 || slotIndex >= slotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, $"Slot index must be within 0..{slotCount - 1}.");
            }

            if (slotCount == 1)
            {
                return 0f;
            }

            return slotIndex / (float)(slotCount - 1);
        }

        public static float ScaleForDepth(float depth)
        {
            return Lerp(NearScale, FarScale, Clamp01(depth));
        }

        // Position along either axis between the scene's near and far
        // anchors. Linear rather than perspective-correct on purpose: a true
        // 1/z falloff bunches the back slots together hard, which looks
        // wrong on a stage only three figures deep. Callers pass real canvas
        // coordinates for near/far.
        public static float PositionForDepth(float nearValue, float farValue, float depth)
        {
            return Lerp(nearValue, farValue, Clamp01(depth));
        }

        // Draw order for the painter's algorithm: farthest first so nearer
        // figures paint over them. In uGUI, later siblings render on top, so
        // this is the sibling index a slot must take.
        //
        // Returns the REVERSE of the slot index (slot 0 is nearest, so it
        // must be built last). Callers that build slots in a loop should
        // create them in this order rather than reordering afterwards —
        // Transform.SetSiblingIndex after the fact is easy to get subtly
        // wrong when other children (backgrounds, labels) share the parent.
        public static int SiblingIndexForSlot(int slotIndex, int slotCount)
        {
            if (slotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), "A stage needs at least one slot.");
            }

            if (slotIndex < 0 || slotIndex >= slotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, $"Slot index must be within 0..{slotCount - 1}.");
            }

            return slotCount - 1 - slotIndex;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
