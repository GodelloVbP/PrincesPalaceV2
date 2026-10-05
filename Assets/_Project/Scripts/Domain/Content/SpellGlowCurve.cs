using System;

namespace PrincesPalace.Domain.Content
{
    // HOW A LAYER'S GLOW MOVES OVER ITS LIFE. `glow` alone is one number held
    // for the whole layer; a coal that heats as a swing builds and flares each
    // time it passes through the bottom needs the number to change, and that is
    // all this adds: a straight ramp to `to`, and a pulse laid over it that
    // crests every `period` seconds. Both are closed forms of the layer's age,
    // so the same instant gives the same glow however the clock got there.
    [Serializable]
    public class SpellGlowCurve
    {
        [ContentDoc("Glow the layer reaches when its lifetime ends, rising or falling in a straight line from `glow`; 0 means no ramp, glow holds at `glow`.")]
        public float to;

        [ContentDoc("Glow added at the crest of each pulse, on top of the ramp; 0 means no pulse.")]
        public float pulse;

        [ContentDoc("Seconds between pulse crests. Required when pulse is set.")]
        public float period;

        [ContentDoc("Seconds after the layer opens that the first pulse crests; later crests follow every period. Nothing pulses earlier than half a period before it.")]
        public float crest;

        // The shader's own knee, which lifts only the hottest highlights of a
        // drawing: right for a molten core, invisible on a metal lantern whose
        // brightest pixels sit under it.
        public const float DefaultKnee = 0.72f;

        [ContentDoc("Brightness (0-1) above which the glow lifts a pixel; lower lifts more of the drawing. 0 means the shader's own 0.72, which reaches only the hottest highlights.")]
        public float knee;

        public bool IsAuthored => to > 0f || pulse > 0f || knee > 0f;

        public float KneeOrDefault => knee > 0f ? knee : DefaultKnee;

        public SpellGlowCurve Copy() => new SpellGlowCurve { to = to, pulse = pulse, period = period, crest = crest, knee = knee };

        // The glow `age` seconds into a layer whose lifetime is `lifetime`.
        public float At(float baseGlow, float age, float lifetime)
        {
            float glow = baseGlow;

            if (to > 0f)
            {
                float through = lifetime <= 0f ? 1f : Math.Min(1f, Math.Max(0f, age / lifetime));
                glow = baseGlow + (to - baseGlow) * through;
            }

            if (pulse > 0f && period > 0f && age >= crest - period * 0.5f)
            {
                double turn = 2.0 * Math.PI * (age - crest) / period;
                glow += pulse * 0.5f * (1f + (float)Math.Cos(turn));
            }

            return glow;
        }
    }
}
