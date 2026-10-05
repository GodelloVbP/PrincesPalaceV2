using System;

namespace PrincesPalace.Domain.Content
{
    // THE STAGE'S NUDGE ON THE BLOW, toward the target. Beside `shake` and
    // unlike it: shake is noise that dies away, this is one push in a known
    // direction that eases back. Purely presentation: it moves the figures and
    // the spell's own layers by a few pixels and is never read by combat or
    // input. Authored seconds, so the battle-speed preset scales it with
    // everything else a cast draws.
    [Serializable]
    public class SpellKick
    {
        // The stage's width in reference-frame units, which `strength` is a fraction of.
        public const float StageWidth = 1920f;

        public const float MaxStrength = 0.1f;

        [ContentDoc("How far the stage is pushed toward the target at the peak, as a fraction of the stage's width (0.025 is 2.5%). 0 means no kick.")]
        public float strength;

        [ContentDoc("Seconds from the hit cue to the peak.")]
        public float rise;

        [ContentDoc("Seconds the stage takes to ease back from the peak.")]
        public float fall;

        public bool HasKick => strength > 0f;

        public SpellKick Copy() => new SpellKick { strength = strength, rise = rise, fall = fall };

        // The push `since` seconds after the cue, as a fraction of the stage's width.
        public float At(float since)
        {
            if (!HasKick || since < 0f) return 0f;

            if (since < rise)
            {
                float up = since / rise;
                return strength * up * up * (3f - 2f * up);
            }

            float into = since - rise;
            if (fall <= 0f || into >= fall) return 0f;

            float left = 1f - into / fall;
            return strength * left * left;
        }

        public float EndSeconds(float hitCueSeconds) => HasKick ? hitCueSeconds + rise + fall : 0f;
    }
}
