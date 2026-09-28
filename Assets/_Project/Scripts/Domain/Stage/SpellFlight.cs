using System;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Stage
{
    // Where a called strike amasses, and how a projectile lies on its flight.
    //
    // Two halves of one problem: a strike like Winter's Rebuke needs to form
    // and amass in the air around the target rather than spawning under it,
    // and fire itself diagonally at the mob rather than sitting at the feet
    // rotated along the rank (align: span), which is the floor's diagonal,
    // not the strike's.
    //
    // Engine-free for the reason FormationSpan and CastPointPlacement are:
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
        // Halfway and not "over the target", because a strike called down
        // vertically onto the target reads as a lightning bolt; from halfway
        // up and over, the line to the target is the intended diagonal, and
        // its angle falls out of the stage rather than being authored.
        public static UiVec SkyPoint(UiVec casterPoint, float casterTop, StageBody target)
        {
            var aim = target.Centre;
            float x = (casterPoint.X + aim.X) * 0.5f;
            float y = Math.Min(Math.Max(casterTop, target.Top) + SkyLift, SkyCeiling);
            return new UiVec(x, y);
        }

        // ---- nothing a spell draws may cross the combat log ----------------
        //
        // Against a tall target (an Elder Treant, say) the air point meets
        // SkyCeiling and a large drawing sized to the target's body can
        // reach past the log or off the top of the screen. Lowering the
        // ceiling alone cannot fix it -- the treant's head is at 240, so an
        // air point any lower is beside the head rather than above it, and a
        // 490-unit spear launched from it still reaches past the top of the
        // screen. What has to give is the size of the drawing near the
        // ceiling.
        //
        // Shrunk about its anchor, not moved. Every placed layer has a point
        // that must not move -- the air point a spear leaves from, the body an
        // impact lands on -- and its box sits at a fixed offset from that point
        // that scales with the box (the impact-point correction is a fraction
        // of the art). So the top of the drawing is `anchor + s * reach`, and
        // the largest s that keeps it under the log's lower edge is one
        // division. The spell still amasses at the same air point and still
        // flies the same line; it is only drawn smaller when, and only as much
        // as, the stage has no room above it. Against a rat nothing changes.

        // THE SMALLEST THIS WILL EVER DRAW A LAYER. An anchor already inside
        // the band cannot be fixed by shrinking; a quarter-size drawing there
        // is a visible degradation rather than a vanished spell.
        public const float MinFitScale = 0.25f;

        // HOW MUCH TO SCALE a drawing whose top would be `anchorY +
        // reachAboveAnchor` at full size, so that its top stays at or under
        // `ceiling`. 1 when it already fits (or reaches down, not up).
        public static float ScaleUnder(float anchorY, float reachAboveAnchor, float ceiling)
        {
            if (reachAboveAnchor <= 0f || anchorY + reachAboveAnchor <= ceiling) return 1f;

            float room = ceiling - anchorY;
            if (room <= 0f) return MinFitScale;
            return Math.Max(MinFitScale, room / reachAboveAnchor);
        }

        // HALF THE HEIGHT OF A BOX TURNED BY `degrees`: the vertical half-extent
        // of its axis-aligned bounds. A spear drawn corner to corner in its box
        // reaches the box's corner, so the rotated box's bounds -- not its
        // unrotated half-height -- are where its tail ends.
        public static float RotatedHalfHeight(UiVec size, float degrees)
        {
            double r = degrees * Math.PI / 180.0;
            return (float)(Math.Abs(size.X * 0.5 * Math.Sin(r)) + Math.Abs(size.Y * 0.5 * Math.Cos(r)));
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
