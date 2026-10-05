namespace PrincesPalace.Domain.Combat.Presentation
{
    // HOW LIGHT SITS ON A BODY'S SILHOUETTE, as the numbers behind two shaders:
    // UIBurnGlow (the coal glow of a burning body) and UIRimFlash (the flash of every
    // hit). Both light the same three places in the same order of
    // strength -- the silhouette's EDGE, a faint tint over the INTERIOR so the
    // creature keeps its own colours, and for the glow the FEET -- and neither
    // paints a flat shape over the figure.
    //
    // The shaders cannot be run on the fast host, so the per-pixel arithmetic is
    // written here once, pinned with literals, and the shader files restate the
    // same constants as property defaults; BodyLightTests fails when the two
    // drift apart.
    public static class BodyLight
    {
        // ---- shared ------------------------------------------------------

        // HOW FAR IN FROM THE OUTLINE the edge band reaches, as a fraction of the
        // canvas height (the shader samples two rings of eight neighbours, at
        // this distance and at twice it). Actor canvases differ in size; a
        // fraction keeps a rat's band and a treant's proportionate to the body.
        public const float BurnRimWidth = 0.02f;
        public const float FlashRimWidth = 0.022f;

        // THE EDGE OF A PIXEL from the mean alpha of its sampled neighbours:
        // 0 deep inside the body (every neighbour opaque), 1 on the outline
        // (half or more of them clear).
        public static float EdgeOf(float meanNeighbourAlpha) =>
            Clamp01((1f - Clamp01(meanNeighbourAlpha)) * 2f);

        // ---- the burn glow ----------------------------------------------

        // The interior's share of the glow's strength: the creature shows through.
        public const float BurnInteriorShare = 0.16f;

        // How far up the canvas the feet's extra heat reaches (texcoord y, 0 is
        // the ground line): full at the ground, none from here up.
        public const float BurnFeetReach = 0.4f;

        // THE GLOW'S STRENGTH AT ONE PIXEL, 0..1, before the status' own alpha
        // (level and breathing, StageBurnGlow.AlphaAt) multiplies it. `opaque`
        // is the sprite's alpha there, `edge` EdgeOf, `y` the texture height.
        // Feet carry the heat: the edge is at full strength only at the ground
        // and at half at the crown; the interior ranges 0.35 to 1 of its share.
        public static float BurnWeight(float opaque, float edge, float y)
        {
            float feet = 1f - Smoothstep(0f, BurnFeetReach, y);
            float interior = BurnInteriorShare * (0.35f + 0.65f * feet);
            float rim = Clamp01(edge) * (0.5f + 0.5f * feet);
            return Clamp01(opaque) * Clamp01(interior + rim);
        }

        // ---- the rim flash -------------------------------------------------

        // A hit lights the outline and only tints the inside.
        public const float FlashInteriorShare = 0.22f;

        // Two frames of full strength and a short fade.
        public const int FlashHoldFrames = 2;
        public const float FlashFadeSeconds = 0.08f;

        public static float FlashWeight(float opaque, float edge) =>
            Clamp01(opaque) * (FlashInteriorShare + (1f - FlashInteriorShare) * Clamp01(edge));

        // ---- helpers -------------------------------------------------------

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static float Smoothstep(float a, float b, float x)
        {
            float t = Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
