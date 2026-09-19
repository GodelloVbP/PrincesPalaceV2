using System;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Stage
{
    // THE LINE A RANK ACTUALLY STANDS ON: where it starts, how long it is, and
    // which way it tilts.
    //
    // WHY THIS IS A TYPE AND NOT ARITHMETIC AT THE CALL SITE. A stage formation
    // is a line drawn between two endpoints at DIFFERENT DEPTHS -- the enemy's
    // runs (300, -218) -> (660, -125), the party's (320, -218) -> (810, -64)
    // (FightStageAnchors) -- so "across the whole rank" is a diagonal on both
    // sides and has been since the depth line stopped being level. A layer
    // spanning that rank with an axis-aligned box describes a horizontal strip
    // of floor nobody is standing on: correct at the midpoint and wrong by half
    // the rank's rise at both ends. Cinderfault's fault was drawn exactly that
    // way, which is what the owner saw (2026-09-19, "the line ... should follow
    // the mobs, who stand in a diagonal line").
    //
    // ENGINE-FREE, in Domain, for the same reason CastPointPlacement is: every
    // line of it is arithmetic over two points, so it is pinnable with literal
    // numbers by an EditMode test rather than only by a running scene. Core
    // still owns MEASURING the two endpoints -- where a slot landed and how
    // wide its depth-scaled body is are things only the live stage knows.
    //
    // THE CALLER ORDERS THE ENDPOINTS LEFT TO RIGHT, and that is a contract
    // rather than a convenience: taking them in struck order would give a rank
    // swept back-to-front an angle 180 degrees from the identical rank swept
    // front-to-back, and the art -- a crack in the floor -- would render upside
    // down for one of them. Ordered by X, Degrees always lands in (-90, 90],
    // so a sheet drawn left-to-right stays the right way up whichever end the
    // cast started from.
    public readonly struct FormationSpan
    {
        // The midpoint of the padded line, in the same frame the endpoints were
        // given in.
        public readonly UiVec Centre;

        // End to end, INCLUDING both pads. This is the box's width, so the art
        // is fitted along the line rather than across the screen.
        public readonly float Length;

        // Counter-clockwise from +X, in degrees. What a rect's Z rotation takes
        // directly.
        public readonly float Degrees;

        // The unit vector along the line, left end to right end, and the unit
        // vector perpendicular to it (rotated +90). `Up` is what a sheet's own
        // ground-line correction has to move along once the box is tilted --
        // correcting on world +Y instead would slide the art off the line by
        // the tangent of the tilt.
        public readonly UiVec Along;
        public readonly UiVec Up;

        private FormationSpan(UiVec centre, float length, float degrees, UiVec along, UiVec up)
        {
            Centre = centre;
            Length = length;
            Degrees = degrees;
            Along = along;
            Up = up;
        }

        // The span between two ground points, each pushed outward by its own
        // pad.
        //
        // TWO PADS AND NOT ONE, because the two ends are at different depths
        // and therefore at different scales: a back-rank body is drawn smaller
        // than a front-rank one, so one shared margin would over-reach at the
        // back and under-reach at the front. That is the same reasoning the
        // measured-span code already gave for reading each slot's own edges
        // instead of its centre plus a constant; the pads are what carries it
        // through the rotation.
        //
        // A DEGENERATE SPAN IS LEVEL, not undefined. One struck target gives
        // first == last, and atan2(0, 0) is 0 on every platform this runs on
        // but is not a number anyone should have to look up -- so the zero case
        // is answered outright, and it answers with today's behaviour: a level
        // box of the pads' own width.
        public static FormationSpan Between(UiVec first, UiVec last, float padFirst, float padLast)
        {
            float dx = last.X - first.X;
            float dy = last.Y - first.Y;
            float run = (float)Math.Sqrt(dx * dx + dy * dy);

            var along = run > 0f ? new UiVec(dx / run, dy / run) : new UiVec(1f, 0f);
            var up = new UiVec(-along.Y, along.X);

            var a = first - along * padFirst;
            var b = last + along * padLast;

            float degrees = (float)(Math.Atan2(along.Y, along.X) * 180.0 / Math.PI);

            return new FormationSpan((a + b) * 0.5f, run + padFirst + padLast, degrees, along, up);
        }
    }
}
