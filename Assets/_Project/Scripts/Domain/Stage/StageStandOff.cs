using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Stage
{
    // The horizontal extent of a figure's DRAWING, measured from the centre of
    // its own sprite canvas, in that canvas's own pixels, with the stage
    // mirror already applied -- so Left is the edge on the left of the screen
    // whichever way the art was authored.
    //
    // The numbers are large and wildly off-centre on real art and that is the
    // point: the beetle's idle occupies x -130..339 of an 830-wide canvas, so
    // its slot mark sits 242 stage pixels BEHIND the front of its own body
    // once the depth scale is applied. Nothing that reasons about "how close
    // are these two figures" can use the marks alone.
    // A span carries TWO pairs of edges, and the stand-off below uses the
    // second pair. Left/Right are the outermost opaque pixels -- the tight
    // alpha box, still what "does this drawing reach that far" means.
    // MassLeft/MassRight are where the BODY is: see MassFraction.
    public readonly struct OpaqueSpan
    {
        // How much of a column has to be filled for that column to count as
        // body rather than as something sticking out of it. Per column of the
        // drawing, count the opaque rows; the tallest column is the figure at
        // its thickest, and a column carrying less than this share of that is
        // not the edge of anything worth standing off from.
        //
        // What it excludes, which is the whole reason it exists: the
        // attacker must push into the target's body, not stop at the
        // outermost pixel -- a horn, a tail, a raised weapon, a wingtip.
        //
        // 0.35 rather than anything else, and the beetle's own idle is what
        // chose it. Its body falls off a cliff at canvas x 273 (0.44 of the
        // tallest column) and its horn trails out to 338 at 0.18..0.33, so
        // every threshold at or below 0.30 keeps the horn (0.30 puts the edge
        // at 328, which is the tight edge in all but name) and 0.35 puts it at
        // 280, exactly on the body. Upward is bounded by Bjorn: his hammer
        // head peaks at 0.40 of his tallest column, so 0.40 discards the head
        // itself and his slam's reach collapses 200 -> 111 canvas pixels.
        // 0.35 is the one value that reads the horn and the hammer head
        // differently, which is what it is being asked to do.
        public const float MassFraction = 0.35f;

        public readonly float Left;
        public readonly float Right;
        public readonly float MassLeft;
        public readonly float MassRight;

        // A span that could not be measured for mass -- an unreadable texture,
        // a sprite Unity did not trim -- is its own tight box on both pairs.
        // Wrong in the safe direction, and the same direction the tight box
        // itself already degrades in: a figure stands further off, which reads
        // as staging rather than as a bug.
        public OpaqueSpan(float left, float right) : this(left, right, left, right)
        {
        }

        public OpaqueSpan(float left, float right, float massLeft, float massRight)
        {
            Left = left;
            Right = right;
            MassLeft = massLeft;
            MassRight = massRight;
        }

        // An unmeasurable figure contributes nothing rather than a guess: a
        // zero span degrades the stand-off below to "stop at the target's
        // mark", which is where the old fractions were aiming anyway.
        public static OpaqueSpan None => new OpaqueSpan(0f, 0f);

        // The span as it is DRAWN, given the sprite's mirror (-1 flips the
        // art about its own canvas centre). Both pairs flip, and they swap
        // sides doing it -- the mass edge on the art's right is on the
        // screen's left once the drawing is turned around.
        public OpaqueSpan Mirrored(float mirror)
        {
            return mirror < 0f ? new OpaqueSpan(-Right, -Left, -MassRight, -MassLeft) : this;
        }
    }

    // Where an attacker stops.
    //
    // Not a fixed fraction of the X gap between the two slot marks: a
    // fraction's residual grows with the distance travelled, which is
    // exactly backwards -- an attacker from the back slot would leave 30% of
    // a 1400px gap on the table (420px, a body and a half) where the same 30%
    // of a front-slot gap is 200, so how close you have to be to hit
    // something would depend on how far away you started. A fraction also
    // knows nothing about how far the attacker's weapon reaches: Bjorn's
    // slam draws the hammer 237px past his own canvas centre where his idle
    // reaches 155, so a distance eyeballed against an idle puts the hammer
    // inside the target.
    //
    // So the distance is derived from the two bodies instead: back the target
    // off by its own near-edge extent, back the attacker off by the forward
    // reach of the drawing it will be wearing, and leave a small authored gap
    // between them. Nothing in it refers to where the attacker started.
    //
    // And it is the body, not the outermost pixel: closing to the tight
    // alpha box means closing to whatever sticks furthest out of the
    // drawing, which on this roster is a horn, a tail, a raised weapon or a
    // wingtip. Both edges in the arithmetic below are therefore OpaqueSpan's
    // mass edges, on the attacker as well as the target. Weapons overlapping
    // the body they are hitting at the moment of impact is deliberate --
    // that is what a blow landing looks like. What must not overlap is the
    // two bodies, and closing mass edge to mass edge with a gap between them
    // is exactly the statement that they do not.
    //
    // Also Y, not X-only: a back-rank attacker swinging from its own row at
    // its own depth would land level with the target's head rather than in
    // front of it, so the stand-off lands on the target's ground line.
    // Deliberately not also re-scaling the attacker to the target's depth:
    // the animator composes scale from breath, punch and stretch, and a fourth
    // writer on it is a bigger change than this one is buying. A figure that
    // slides down the floor without growing is a smaller lie than one that
    // swings at empty air.
    public static class StageStandOff
    {
        // Daylight left between the two drawings on a swing or a walk-in.
        // Small enough to read as contact, large enough that the antialiased
        // edges of two painterly figures do not merge into one shape.
        public const float Gap = 12f;

        // A charge ends AGAINST what it hit: the recoil shoves the target back
        // on contact, so the two never actually sit inside each other for
        // longer than the hit-stop.
        public const float ChargeGap = 0f;

        // The offset, in the actor's own anchored space, that puts the actor's
        // forward edge `gap` short of the target's near edge and its feet on
        // the target's ground line.
        //
        // Marks are the two figures' HOME positions, not their live ones: a
        // flyer's hover is composed on top of the offset by the animator, so
        // aiming at a hovering target's live altitude would carry the attacker
        // into the air with it.
        //
        // Scales are the slots' own depth scales, which is what turns a
        // canvas-pixel span into a stage-pixel one.
        public static UiVec TravelTo(UiVec actorMark, OpaqueSpan actorSpan, float actorScale,
                                     UiVec targetMark, OpaqueSpan targetSpan, float targetScale,
                                     float gap)
        {
            float sign = targetMark.X >= actorMark.X ? 1f : -1f;

            // The attacker's leading edge, and the target's edge facing it --
            // where each figure's BODY ends, not where its horn or its hammer
            // does. See this class's header and OpaqueSpan.MassFraction.
            float actorForward = sign > 0f ? actorSpan.MassRight : actorSpan.MassLeft;
            float targetNear = sign > 0f ? targetSpan.MassLeft : targetSpan.MassRight;

            float standOffX = targetMark.X + targetNear * targetScale
                              - sign * gap
                              - actorForward * actorScale;

            float dx = standOffX - actorMark.X;

            // THE ONE CAP LEFT, and it replaces all three fractions: an
            // attacker never steps AWAY from its target. Reached when the two
            // drawings already overlap at rest -- a huge actor against a huge
            // target in adjacent slots -- where the honest answer is "you are
            // already there" rather than a backwards shuffle.
            if (dx * sign < 0f) dx = 0f;

            return new UiVec(dx, targetMark.Y - actorMark.Y);
        }
    }
}
