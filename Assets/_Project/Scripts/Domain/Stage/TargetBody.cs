using System;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Stage
{
    // WHERE A COMBATANT'S VISIBLE BODY IS ON STAGE, and how big it is -- the
    // one contract every spell layer that lands on a target positions and
    // sizes against.
    //
    // THE BODY, NOT THE SLOT. A slot is sized to the sprite's raw CANVAS, and
    // a canvas is cut for an actor's tallest pose: the golem's idle leaves 62
    // of its 461 rows empty above its head, the rat's canvas runs 616 wide
    // around a 391-wide rat that sits 38px left of centre. Aiming at the
    // slot's middle and sizing off its rect therefore placed and sized every
    // effect by padding. Owner, 2026-09-23: Court of Whispers "does not scale
    // with the mob's height/width".
    //
    // THE IDLE DRAWING'S OPAQUE BOX, measured by Core (Domain cannot read a
    // texture) and handed in here in canvas pixels, up from the canvas's
    // bottom-left -- the same direction StanceManifest's groundLine counts in.
    // Idle, for the reason the intent badge and the foot ring already read it:
    // a pose that swings an arm out must not move where a spell lands.
    //
    // ENGINE-FREE ON PURPOSE, like CastPointPlacement: FightController's
    // TransformPoint dance reduces, for an unrotated slot sharing the VFX
    // pool's frame, to `origin + local * scale`, and that arithmetic is what
    // TargetBodyTests pins with literals.
    public readonly struct StageBody
    {
        public readonly float Left;
        public readonly float Right;
        public readonly float Bottom;
        public readonly float Top;

        public StageBody(float left, float right, float bottom, float top)
        {
            Left = Math.Min(left, right);
            Right = Math.Max(left, right);
            Bottom = Math.Min(bottom, top);
            Top = Math.Max(bottom, top);
        }

        public float Width => Right - Left;
        public float Height => Top - Bottom;
        public UiVec Centre => new UiVec((Left + Right) * 0.5f, (Bottom + Top) * 0.5f);

        // THE SIDE OF THE SMALLEST SQUARE THE BODY FITS IN. A spell box is
        // square (or fitted into one by preserveAspect) and its art is drawn
        // around a middle, so what decides whether it covers a long flat rat
        // and a tall treant alike is the larger of the two extents -- width
        // for the rat, height for the treant.
        public float Extent => Math.Max(Width, Height);
    }

    public static class TargetBody
    {
        // THE BODY AN AUTHORED `size` MEANS VERBATIM: a Giant Rat alone in the
        // front rank. Every per-target layer before `fit: target` was tuned by
        // eye against that rat, so taking it as the unit keeps each of those
        // numbers meaning what its author saw.
        //
        // 391 (the rat's idle opaque width, its larger extent, measured off
        // Resources/Enemies/rat/idle.png) x 0.94 (StageLayout.NearScale, a
        // lone slot's depth) x 0.76 (FightStageAnchors.SpriteScale) = 279.3,
        // rounded. A literal rather than that expression because it is a
        // CALIBRATION -- re-slicing the rat must not silently resize every
        // other enemy's spells.
        public const float ReferenceExtent = 280f;

        // The opaque box in the SLOT's own local frame: pivot bottom-centre,
        // y = 0 on the ground line. GroundTheFigure drops the sprite by the
        // manifest ground line and it is anchor-stretched across a slot the
        // canvas's own size, so a canvas pixel (px, py) is slot-local
        // ((px - width/2) * mirror, py - groundLine). `mirror` is the sprite's
        // own facing flip (StageFacing.MirrorScaleX), which swaps which edge
        // is left.
        public static StageBody SlotLocal(float left, float bottom, float right, float top,
            float canvasWidth, float groundLine, float mirror)
        {
            float half = canvasWidth * 0.5f;
            float m = mirror < 0f ? -1f : 1f;
            return new StageBody((left - half) * m, (right - half) * m, bottom - groundLine, top - groundLine);
        }

        // Slot-local into the effect pool's frame: the slot's own origin (which
        // already carries its hover offset) plus the local box times the slot's
        // RESTING scale -- depth curve times authored stageScale, never a
        // mid-squash localScale.
        public static StageBody OnStage(StageBody local, float scale, UiVec slotOrigin)
        {
            return new StageBody(
                slotOrigin.X + local.Left * scale,
                slotOrigin.X + local.Right * scale,
                slotOrigin.Y + local.Bottom * scale,
                slotOrigin.Y + local.Top * scale);
        }

        // WHAT `fit: target` MULTIPLIES `size` BY. 1 for a body the reference
        // rat's size, and 1 -- not 0 -- for a body with no measurable extent
        // (unanchored, unreadable art), so a fitted layer degrades to its
        // authored size rather than to a zero-sized graphic.
        public static float FitFactor(StageBody body)
        {
            float extent = body.Extent;
            return extent > 0.01f ? extent / ReferenceExtent : 1f;
        }

        // ---- a tail lands ON the target; it never replaces it ---------------
        //
        // QA 2026-09-26: Winter's Rebuke's and Blackglass Spear's impact stills
        // (authored 320, i.e. 1.14 rat-bodies) and Crownfall's shatter (340,
        // 1.21) were fitted to an Elder Treant at 1.63x -- 520-555 unit
        // starbursts over a 360 x 457 body, canopy to roots gone, and the
        // damage number sitting on the burst. The authored sizes were tuned on
        // a rat, where "a bit bigger than the body" reads as a hit; the rule
        // they were reaching for is relative to the body, so it is stated
        // relative to the body.
        //
        // THREE QUARTERS OF THE BODY'S LARGER EXTENT, whatever was authored.
        // The burst then covers the struck middle and leaves the outline --
        // canopy, roots, the rat's snout and tail -- visible around it, which
        // is what reads as "hit" rather than "replaced". Applied at run time
        // to every layer that opens AT THE HIT on the struck body (FightController
        // .PlaceOne), so a spell authored tomorrow cannot re-open the defect.
        // Release-time layers are not tails: Thorn Tithe's ring is meant to
        // span the body and is hollow.
        public const float TailMaxOfBody = 0.75f;

        // THE SCALE a tail's box of side `boxSide` is drawn at on `body`: 1
        // when it already fits, and 1 for a body with no measurable extent,
        // the same degrade-to-authored posture FitFactor takes.
        public static float TailScale(float boxSide, StageBody body)
        {
            float extent = body.Extent;
            if (extent <= 0.01f || boxSide <= 0f) return 1f;
            float cap = TailMaxOfBody * extent;
            return boxSide > cap ? cap / boxSide : 1f;
        }
    }
}
