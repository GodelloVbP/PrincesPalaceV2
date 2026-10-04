using System;

namespace PrincesPalace.Domain.Content
{
    // HOW A LAYER TURNS, as one model: a static tilt, a steady spin and a
    // damped pendulum add into a single angle about one pivot. A censer on its
    // chain, an hourglass flipping and a coil drawing back are the same
    // arithmetic with different numbers, so there is no per-motion switch.
    //
    // THE ANGLE IS COUNTER-CLOCKWISE in the cast's own frame and mirrors with
    // the cast, like dx: a layer that hangs from its top and authors a negative
    // pendulum amplitude swings its bottom toward the caster first on any cast.
    //
    // THE PIVOT IS A FRACTION OF THE FRAME, x from the left and y from the
    // bottom (the convention impactX/impactY use). The default is the centre,
    // so a turn that authors no pivot spins in place.
    [Serializable]
    public class SpellRotate
    {
        [ContentDoc("Static tilt, in degrees counter-clockwise (mirrors with the cast). Added to the spin and the pendulum.")]
        public float degrees;

        [ContentDoc("Steady spin, in degrees per second counter-clockwise (mirrors with the cast), from the layer's opening instant.")]
        public float spin;

        [ContentDoc("Pendulum amplitude, in degrees: the angle at the first extreme, counter-clockwise when positive (mirrors with the cast). The swing starts at rest on the vertical and moves sine-wise, so a negative amplitude swings clockwise first. 0 means no pendulum.")]
        public float amplitude;

        [ContentDoc("Seconds for one full swing (out, back through the middle, the other way, and home). Required when amplitude is set.")]
        public float period;

        [ContentDoc("Pendulum damping per second: the amplitude is multiplied by e^(-damping * seconds). 0 swings undiminished.")]
        public float damping;

        [ContentDoc("How many full periods the pendulum swings; fractions count (0.75 ends at the far extreme of the second swing). 0 means until the layer ends. After them the pendulum rests at the vertical.")]
        public float swings;

        [ContentDoc("Pivot, as a fraction of the frame from its left edge; 0.5 is the middle.")]
        public float pivotX = 0.5f;

        [ContentDoc("Pivot, as a fraction of the frame from its bottom edge; 1 is the top, where a hanging thing is hooked.")]
        public float pivotY = 0.5f;

        public bool IsAuthored => degrees != 0f || spin != 0f || amplitude != 0f;

        public SpellRotate Copy() => new SpellRotate
        {
            degrees = degrees, spin = spin, amplitude = amplitude, period = period, damping = damping,
            swings = swings, pivotX = pivotX, pivotY = pivotY,
        };

        // THE ANGLE `age` SECONDS AFTER THE LAYER OPENED, in the cast's frame.
        public float AngleAt(float age)
        {
            if (age < 0f) age = 0f;
            return degrees + spin * age + PendulumAt(age);
        }

        private float PendulumAt(float age)
        {
            if (amplitude == 0f || period <= 0f) return 0f;
            if (swings > 0f && age > swings * period) return 0f;

            double phase = 2.0 * Math.PI * age / period;
            return (float)(amplitude * Math.Sin(phase) * Math.Exp(-damping * age));
        }
    }
}
