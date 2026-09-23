using System;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Stage
{
    // WHERE A CALLED STRIKE AMASSES, AND HOW A PROJECTILE LIES ON ITS FLIGHT.
    //
    // Two halves of one owner report (2026-09-23): Winter's Rebuke "spawns
    // somewhere under the mob" and should "form and amass in the air around
    // the middle, then fire itself diagonally at the mob". The vocabulary had
    // no point that was not ON a body, and no way to turn a painted spear onto
    // the line it flies along -- it was held at the feet as a still, rotated
    // along the RANK (align: span), which is the floor's diagonal, not the
    // strike's.
    //
    // ENGINE-FREE for the reason FormationSpan and CastPointPlacement are:
    // Core measures the points (slot transforms, sprite alpha) and this is the
    // arithmetic over them, pinned with literals by SpellFlightTests.
    public static class SpellFlight
    {
        // HOW FAR ABOVE THE TALLER OF THE TWO BODIES the air point sits. Enough
        // that a spear painted 314 square and drawn around 250 units clears
        // both heads with its own half-height to spare at a rat's scale.
        public const float SkyLift = 110f;

        // THE HIGHEST IT MAY GO, in the effect pool's frame (y = 0 at screen
        // centre, 540 at the top edge). The bark and the initiative strip
        // occupy the top ~230 units at the left and the enemy plate stack
        // hangs to 108 at x >= 520; a point at x ~ 0 capped at 240 stays in
        // open air between them. A tall target (an Elder Treant's head is at
        // ~240 in the front rank) meets the cap rather than pushing the strike
        // off the top of the stage.
        public const float SkyCeiling = 240f;

        // THE AIR POINT FOR ONE CASTER-TARGET PAIR: horizontally halfway from
        // where the cast leaves the caster to the middle of the target's
        // visible body, lifted SkyLift above whichever of the two stands
        // taller, never above SkyCeiling.
        //
        // HALFWAY and not "over the target", because a strike called down
        // vertically onto the target reads as a lightning bolt; from halfway
        // up and over, the line to the target is the diagonal the owner asked
        // for, and its angle falls out of the stage rather than being authored.
        public static UiVec SkyPoint(UiVec casterPoint, float casterTop, StageBody target)
        {
            var aim = target.Centre;
            float x = (casterPoint.X + aim.X) * 0.5f;
            float y = Math.Min(Math.Max(casterTop, target.Top) + SkyLift, SkyCeiling);
            return new UiVec(x, y);
        }

        // THE ANGLE OF A LINE, counter-clockwise from +x, in degrees.
        public static float DegreesOf(UiVec from, UiVec to)
        {
            return (float)(Math.Atan2(to.Y - from.Y, to.X - from.X) * 180.0 / Math.PI);
        }

        // HOW FAR TO TURN THE BOX so a drawing painted pointing `artDegrees`
        // points along `pathDegrees` once it is drawn.
        //
        // AFTER THE MIRROR. SpellVfxPlayer mirrors on localScale and turns on
        // localRotation of the same RectTransform, so the mirror happens first
        // and a sheet painted pointing up-right at a points up-LEFT at
        // 180 - a once mirrored. Turning from the painted angle rather than the
        // mirrored one would be off by 180 - 2a on every leftward cast --
        // invisible on the party, whose casts all go right.
        //
        // Normalised to (-180, 180] so the renderer is never handed a
        // needless whole turn.
        public static float Turn(float pathDegrees, float artDegrees, float drawFacing)
        {
            float painted = drawFacing < 0f ? 180f - artDegrees : artDegrees;
            return Normalise(pathDegrees - painted);
        }

        // WHERE THE SHEET'S OWN IMPACT POINT SITS RELATIVE TO THE BOX'S CENTRE,
        // once the box is mirrored and turned -- so a caller can put the
        // IMPACT on the line of flight rather than the box's middle. `art` is
        // the rendered size (after preserveAspect); impactX from the left,
        // impactY from the bottom, as everywhere else.
        public static UiVec ImpactOffset(float impactX, float impactY, UiVec art, float drawFacing,
            float degrees)
        {
            float m = drawFacing < 0f ? -1f : 1f;
            float lx = (impactX - 0.5f) * art.X * m;
            float ly = (impactY - 0.5f) * art.Y;

            double r = degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(r);
            float s = (float)Math.Sin(r);
            return new UiVec(lx * c - ly * s, lx * s + ly * c);
        }

        private static float Normalise(float degrees)
        {
            float d = degrees % 360f;
            if (d > 180f) d -= 360f;
            if (d <= -180f) d += 360f;
            return d;
        }
    }
}
