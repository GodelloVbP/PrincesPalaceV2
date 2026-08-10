using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.UiKit
{
    // Where the Divine Principality's buildings stand, and how big they read.
    //
    // The hub was five equal rectangles on a nebula: no ground, no depth, no
    // hierarchy. This is the arithmetic that makes it a PLACE -- a forecourt at
    // the edge of the void, with the gate on the terrace and the annexes
    // floating out over the drop at different distances.
    //
    // Composes StageLayout.ScaleForDepth rather than inventing a second depth
    // curve. That curve is already tested and already tuned for exactly this
    // job on the fight stage; a hub with its own falloff would be two answers to
    // one question, and they would drift.
    //
    // In Domain/UiKit/ rather than Domain/Stage/ deliberately: the Stage path
    // maps to the combat and art test areas, so a hub anchor edit there would
    // run the wrong suites and miss its own.
    public static class HubAnchors
    {
        // The terrace line -- the y a building's feet sit on when it is nearest.
        // Bottom-centre pivots throughout, the fight stage's convention, so a
        // taller building grows upward from the ground rather than about its
        // own middle.
        public const float NearY = -90f;

        // The far end of the drop. Floating buildings get smaller AND rise, so
        // the void reads as depth rather than as a wall.
        //
        // A far building's top edge is its y PLUS its full height -- bottom
        // pivots -- so the far end has to leave room for the tallest thing
        // standing on it, not just for the anchor point.
        public const float FarY = 250f;

        // How wide the composition spreads at each end. The near pair sit well
        // out toward the frame edges; the far pair pull in, which is what makes
        // the middle read as distance rather than as a gap.
        public const float NearX = 700f;
        public const float FarX = 250f;

        // The gate is not staged with the others. It stands ON the terrace at
        // the end of the path, dead centre and largest -- the descent is the
        // primary action and the composition has to say so before any label
        // does.
        public static readonly UiVec Gate = new UiVec(0f, -470f);
        public const float GateSize = 620f;

        // The caption hangs below the plinths rather than across the arch, whose
        // centre is a swirling void the eye needs to read as an opening.
        // A nameplate has to clear the SPRITE'S HALF-HEIGHT, not sit a fixed
        // distance from its centre.
        //
        // The first attempt used a flat -34, which on a 300px building is still
        // squarely on the art -- the labels stayed printed across the awning and
        // through the tree exactly as before, and only a screenshot showed it.
        // Place.At is centre-relative whatever the pivot is, so the offset has
        // to be derived from the thing's own size.
        public const float CaptionGap = 16f;

        public static float CaptionOffsetFor(float size) => -(size * 0.5f) - CaptionGap;

        public static float GateCaptionOffset => CaptionOffsetFor(GateSize);

        // One building's place in the composition: how far out over the void it
        // floats, and which side. Lateral is signed -- negative is left.
        public readonly struct Plot
        {
            public readonly float Depth;
            public readonly float Lateral;
            public readonly float BaseSize;

            public Plot(float depth, float lateral, float baseSize)
            {
                Depth = depth;
                Lateral = lateral;
                BaseSize = baseSize;
            }
        }

        // Declared near-to-far. That order is also the PAINTER'S ORDER the tree
        // declares them in, so a nearer building draws over a further one
        // without anybody sorting anything -- the same rule the fight stage
        // relies on.
        // The depths are spread wider than "looks about right" because the
        // audit refused the first pass: at a narrower spread the far building on
        // each side sat inside the near one's box, and uGUI gives the later
        // sibling the click. A distant building you can see and cannot press is
        // the exact bug the overlap check exists to catch, so these are tuned to
        // clear rather than exempted.
        public static readonly Plot Principality = new Plot(0.06f, -1f, 360f);
        public static readonly Plot CharacterSheet = new Plot(0.16f, 1f, 340f);
        public static readonly Plot Talents = new Plot(0.78f, -1f, 360f);
        public static readonly Plot Relics = new Plot(0.90f, 1f, 340f);

        public static UiVec PositionFor(Plot plot) =>
            new UiVec(
                StageLayout.PositionForDepth(NearX, FarX, plot.Depth) * plot.Lateral,
                StageLayout.PositionForDepth(NearY, FarY, plot.Depth));

        public static float ScaleFor(Plot plot) => StageLayout.ScaleForDepth(plot.Depth);

        // What the tree actually declares as the node's size. Baked into the
        // size rather than applied as a transform scale, so the audit measures
        // the box the player really sees -- a scaled-down node whose declared
        // size is full would be reported as overlapping things it does not
        // touch.
        public static float SizeFor(Plot plot) => plot.BaseSize * ScaleFor(plot);
    }
}
