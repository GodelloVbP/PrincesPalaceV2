using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Ambience
{
    // One sine term of a flicker: how far it swings, how fast, and where in its
    // cycle it starts.
    public readonly struct FlickerTerm
    {
        public readonly float Amplitude;
        public readonly float Rate;
        public readonly float Phase;

        public FlickerTerm(float amplitude, float rate, float phase = 0f)
        {
            Amplitude = amplitude;
            Rate = rate;
            Phase = phase;
        }

        public float At(float seconds) => Amplitude * (float)Math.Sin(seconds * Rate + Phase);
    }

    // A flame's brightness over time, as DATA rather than three numbers welded
    // into a method body.
    //
    // In Domain for two reasons. It can now be TESTED -- the EditMode suite is
    // Domain-only, so while this lived in Core as a static on a MonoBehaviour it
    // was pure, seam-shaped, and unreachable by every test in the project. And
    // it can now be CHANGED -- a curve is a list of terms, so retuning a lantern
    // or giving a different light its own character is editing data, not editing
    // arithmetic and hoping.
    //
    // Engine-free: System.Math, not Mathf. That is the price of being testable
    // here and it costs nothing -- Mathf.Sin forwards to Math.Sin anyway.
    public sealed class FlickerCurve
    {
        private readonly FlickerTerm[] _terms;

        // Where the curve sits with every term at zero. 0.5 with terms summing
        // to 0.5 is what lets it reach both ends of [0,1] without spending most
        // of its life clamped at either.
        public readonly float Centre;

        public FlickerCurve(float centre, params FlickerTerm[] terms)
        {
            Centre = centre;
            _terms = terms ?? Array.Empty<FlickerTerm>();
        }

        public IReadOnlyList<FlickerTerm> Terms => _terms;

        // The sum of every term's amplitude. Exposed because "can this curve
        // actually reach 0 and 1" is a property worth asserting rather than
        // eyeballing, and it stops being obvious the moment a term is added.
        public float Swing
        {
            get
            {
                float total = 0f;
                foreach (var term in _terms) total += term.Amplitude;
                return total;
            }
        }

        public float At(float seconds)
        {
            float value = Centre;
            foreach (var term in _terms) value += term.At(seconds);
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        // The lantern flame: the hanging lanterns down the menu's colonnade, the
        // balustrade posts, the gazebo.
        //
        // Deliberately NOT one clean cosine. That is right for a magic beacon
        // under a map room and wrong for fire -- a lantern breathing on a tidy
        // 2.4s sine reads as a pulsing orb, not a flame.
        //
        // The rates are NON-HARMONIC on purpose: 2.11 / 5.39 / 11.83 share no
        // small common multiple, so the sum's true repeat is far longer than
        // anyone spends looking at a menu.
        public static readonly FlickerCurve Lantern = new FlickerCurve(0.5f,
            new FlickerTerm(0.30f, 2.11f),
            new FlickerTerm(0.13f, 5.39f, 1.7f),
            new FlickerTerm(0.07f, 11.83f, 4.1f));

        // A steadier, slower light for anything that should read as enchanted
        // rather than burning. Here as the second data point that proves the
        // shape is reusable -- a curve nobody can vary is not really data.
        public static readonly FlickerCurve Ember = new FlickerCurve(0.55f,
            new FlickerTerm(0.22f, 1.31f),
            new FlickerTerm(0.10f, 3.77f, 0.9f));

        // The heat under the Reckoning's Continue arrow: something actively
        // burning, seen close up, on a screen where nothing else is moving.
        //
        // Ember was tried there first and was the wrong tool. Its terms sum to
        // 0.32, so it only ever travels 0.24..0.85 of its own range — a
        // consumer mapping it onto a scale swing gets two thirds of what it
        // asked for, and the arrow read as a static asset in the running game
        // even though a PlayMode test could see it moving. This reaches
        // 0.08..0.98.
        //
        // A BIG SLOW TERM to be watched rather than noticed, one medium term so
        // it never resolves into a plain sine, and a little fast shimmer for
        // the impression of something combusting. The three rates share no
        // common factor, which is what stops them landing back in step and
        // turning the whole thing into one machine ticking.
        public static readonly FlickerCurve Forge = new FlickerCurve(0.52f,
            new FlickerTerm(0.30f, 1.07f),
            new FlickerTerm(0.13f, 2.63f, 1.4f),
            new FlickerTerm(0.05f, 6.91f, 3.2f));
    }
}
