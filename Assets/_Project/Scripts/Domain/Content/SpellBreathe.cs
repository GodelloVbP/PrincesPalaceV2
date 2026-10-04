using System;
using PrincesPalace.Domain.Ambience;

namespace PrincesPalace.Domain.Content
{
    // A HELD LAYER THAT NEVER HOLDS STILL: alpha and scale ride the Ember
    // flicker curve (FlickerCurve.Ember), the same curve the menu's embers use.
    //
    // THE CURVE IS NORMALISED to -1..1 about its own centre before it is used,
    // because Ember sits at 0.55 and only travels 0.24..0.85: applied raw, a
    // layer authoring 0.1 of scale would swell asymmetrically and its mean
    // size would drift off the drawing's.
    [Serializable]
    public class SpellBreathe
    {
        [ContentDoc("Alpha flutter: the fraction of the layer's alpha lost at the curve's low point (0.35 dips to 65%). It never brightens past the authored opacity. 0 means none.")]
        public float alpha;

        [ContentDoc("Scale flutter: the layer swells and shrinks by up to this fraction either side of its size (0.08 is plus or minus 8%). 0 means none.")]
        public float scale;

        [ContentDoc("Curve speed multiplier: 1 is the Ember curve's own pace (a slow swell of several seconds); 3-5 reads as a flame.")]
        public float rate = 1f;

        public bool IsAuthored => alpha > 0f || scale > 0f;

        public SpellBreathe Copy() => new SpellBreathe { alpha = alpha, scale = scale, rate = rate };

        // The curve at `age` seconds, -1 at its low point and 1 at its high.
        // `phase` (seconds on the curve) keeps two breathing layers off each other's beat.
        public float WaveAt(float age, float phase)
        {
            var curve = FlickerCurve.Ember;
            float value = curve.At(age * rate + phase);
            return (value - curve.Centre) / curve.Swing;
        }

        public float AlphaMultiplierAt(float age, float phase) =>
            alpha <= 0f ? 1f : 1f - alpha * (1f - WaveAt(age, phase)) * 0.5f;

        public float ScaleMultiplierAt(float age, float phase) =>
            scale <= 0f ? 1f : 1f + scale * WaveAt(age, phase);
    }
}
