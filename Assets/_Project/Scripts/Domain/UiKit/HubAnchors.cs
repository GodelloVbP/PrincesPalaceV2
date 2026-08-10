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
        // A nameplate has to clear THE ART, which is not the same as clearing
        // the box.
        //
        // Two wrong answers came first. A flat -34 from centre did not move the
        // labels off the buildings at all. Half the box did move them -- and
        // landed one under the book, one across the stall and one above the
        // shrine, because every sprite carries a different amount of transparent
        // padding and the visible art sits at a different height inside each.
        //
        // MEASURED, not guessed: these are the lowest opaque row of each sheet
        // as a fraction of its height, read off the actual PNGs. Same convention
        // as the ambience positions and the stance manifest's ground lines --
        // the art is the authority on where the art is.
        public const float CaptionGap = 14f;

        public const float TalentsContentBottom = 0.9444f;
        public const float PrincipalityContentBottom = 0.9083f;
        public const float CharacterSheetContentBottom = 0.9441f;
        public const float RelicsContentBottom = 0.9162f;
        public const float GateContentBottom = 0.8848f;

        // Node space is centre-origin with +y up, so the art's bottom edge sits
        // at size * (0.5 - contentBottom) -- a negative number for any sheet
        // whose content reaches past the middle, which is all of them.
        public static float CaptionOffsetFor(float size, float contentBottom) =>
            size * (0.5f - contentBottom) - CaptionGap;

        public static float GateCaptionOffset => CaptionOffsetFor(GateSize, GateContentBottom);

        // One building's place in the composition: how far out over the void it
        // floats, and which side. Lateral is signed -- negative is left.
        public readonly struct Plot
        {
            public readonly float Depth;
            public readonly float Lateral;
            public readonly float BaseSize;

            // Where this building's art actually ends inside its sheet. Carried
            // on the plot so a nameplate cannot be handed another building's
            // padding.
            public readonly float ContentBottom;

            public Plot(float depth, float lateral, float baseSize, float contentBottom)
            {
                Depth = depth;
                Lateral = lateral;
                BaseSize = baseSize;
                ContentBottom = contentBottom;
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
        public static readonly Plot Principality = new Plot(0.06f, -1f, 360f, PrincipalityContentBottom);
        public static readonly Plot CharacterSheet = new Plot(0.16f, 1f, 340f, CharacterSheetContentBottom);
        public static readonly Plot Talents = new Plot(0.78f, -1f, 360f, TalentsContentBottom);
        public static readonly Plot Relics = new Plot(0.90f, 1f, 340f, RelicsContentBottom);

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
