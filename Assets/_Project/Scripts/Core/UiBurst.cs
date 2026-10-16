using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // ONE BURST'S THREE LIGHTS, each on its own curve, which is the whole
    // difference between an impact and a thing getting bigger. Shared by the
    // reward track's claim (RewardTrackController.PaintBurst) and the
    // Reckoning's top-tier landing, so the game has one burst, not two.
    //
    // The flash is fastest and dies first; the shockwave outlives it and
    // travels furthest; the rays turn while they fan, so the burst is not
    // radially symmetric for its whole life. Nothing here shares a duration
    // with anything else on purpose. `t` is 0..1 through the burst.
    public static class UiBurst
    {
        // The core: a white-hot flash cooling to the colour as it dies. It is
        // over in the first 60% of the burst, which is what makes the
        // shockwave look like it is leaving something behind.
        public static void Core(Image core, float t, Color colour)
        {
            if (core == null) return;

            float u = Mathf.Clamp01(t / 0.6f);
            core.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 2.1f, Easing.OutCubic(u));
            core.color = WithAlpha(Color.Lerp(Color.white, colour, Easing.OutCubic(u)), (1f - u) * (1f - u));
        }

        // The shockwave: all the way out, thinning as it goes because a scaled
        // hairline does exactly that, and fading on a curve that holds its
        // brightness early and drops it late. `reach` scales how far it goes.
        public static void Ring(Image ring, float t, Color colour, float reach = 1f)
        {
            if (ring == null) return;

            ring.rectTransform.localScale = Vector3.one * (Mathf.Lerp(0.3f, 3.4f, Easing.OutCubic(t)) * reach);
            ring.color = WithAlpha(colour, 0.95f * Mathf.Pow(1f - t, 1.6f));
        }

        // The rays, turning as they fan. Twenty-two degrees over the whole
        // burst is not enough to read as a spin and is exactly enough to stop
        // the shape being the same shape twice.
        public static void Rays(Image rays, float t, Color colour)
        {
            if (rays == null) return;

            float u = Mathf.Clamp01(t / 0.8f);
            rays.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.55f, Easing.OutCubic(u));
            rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -22f * u);
            rays.color = WithAlpha(colour, 0.85f * (1f - u) * (1f - u));
        }

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
    }
}
