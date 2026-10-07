using System;

namespace PrincesPalace.Domain.Content
{
    // A SPELL'S LIGHT ON THE STRUCK BODIES' OUTLINES, for a cast that lands no
    // damage. The hit flash is armed by damage, so a status cast (Fear) leaves
    // the body unlit at its own payoff; this lets the spell author the same rim
    // in its own colour, as a short train of pulses (a shiver) rather than the
    // one pulse of a blow. Rim only: the flash shader keeps the interior at
    // BodyLight.FlashInteriorShare.
    //
    // Authored seconds, scaled by the battle-speed preset like the rest of a
    // cast. Zero pulses is "no rim" (the default), which keeps every shipped
    // spell inert.
    [Serializable]
    public class SpellBodyRim
    {
        public const int MaxPulses = 8;

        [ContentDoc("#RRGGBB colour of the rim light on every body the cast lands on. Blank means no rim.")]
        public string tint = "";

        [ContentDoc("How many pulses the rim gives at the payoff cue; 1 is a flash, 3 or more a shiver. 0 means no rim.")]
        public int pulses;

        [ContentDoc("Seconds from the start of one pulse to the start of the next.")]
        public float spacing;

        [ContentDoc("Peak opacity of each pulse, 0-1; 0 means 1.")]
        public float peak;

        public bool HasRim => pulses > 0 && !string.IsNullOrWhiteSpace(tint);

        public SpellBodyRim Copy() => new SpellBodyRim { tint = tint, pulses = pulses, spacing = spacing, peak = peak };

        public float PeakOrDefault => peak > 0f ? peak : 1f;
    }
}
