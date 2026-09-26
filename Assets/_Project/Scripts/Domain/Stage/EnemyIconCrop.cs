using System;

namespace PrincesPalace.Domain.Stage
{
    // WHICH SQUARE OF AN ENEMY'S IDLE STILL THE PLATE ICON SHOWS.
    //
    // Engine-free, in canvas pixels with a BOTTOM-LEFT origin (the space
    // groundLine and castPoint's dy already count in), so the rule is pinned
    // in EditMode with literal numbers and FightController.Hud only has to
    // turn the answer into a Sprite.
    //
    // WHY THIS EXISTS (QA 2026-09-26): the first version cropped the top
    // EnemyIconHeadZoneFrac of the whole CANVAS, full width. Every idle still
    // sits on a padded shared canvas (slice_actor_sheet.py), so that band was
    // mostly empty air: the beetle, standing in the bottom 60% of an 830x413
    // canvas, got a band with nothing in it; the rat's band held one ear tip,
    // and a 616px-wide strip fitted into a 34px square came out ~34x6 --
    // the grey sliver the capture shows. Two errors, and this fixes both:
    // the crop is measured on the figure (its opaque box, or an authored
    // head), and it is SQUARE, so the icon fills its 34x34 box instead of
    // letterboxing to a strip.
    public static class EnemyIconCrop
    {
        // The crop, as a square clamped inside the canvas. Clamping can make
        // it a rectangle at a canvas edge; the icon's preserveAspect absorbs
        // that, and a head near the edge is still the head.
        //
        // authored: the manifest's head box (StanceManifest.HeadFor). When
        //   present it wins outright -- where the face is is a judgement
        //   about the drawing (RawHeadBox), and a rule cannot see it.
        // opaque: the figure's opaque bounding box on the canvas (a Tight
        //   sprite's textureRect, placed by textureRectOffset). Used only
        //   for the fallback: a square EnemyIconHeadZoneFrac of the figure's
        //   height on a side, top-aligned to the figure, centred on it
        //   across -- the head of anything that stands upright.
        public static CanvasRect HeadSquare(float canvasWidth, float canvasHeight, float groundLine,
            HeadBoxSpec? authored, CanvasRect opaque, float headZoneFrac)
        {
            float centreX, centreY, side;

            if (authored.HasValue)
            {
                centreX = canvasWidth * 0.5f + authored.Value.Dx;
                centreY = groundLine + authored.Value.Dy;
                side = authored.Value.Size;
            }
            else
            {
                side = Math.Max(1f, opaque.Height * headZoneFrac);
                centreX = opaque.X + opaque.Width * 0.5f;
                centreY = opaque.Y + opaque.Height - side * 0.5f;
            }

            float left = Clamp(centreX - side * 0.5f, 0f, canvasWidth);
            float right = Clamp(centreX + side * 0.5f, 0f, canvasWidth);
            float bottom = Clamp(centreY - side * 0.5f, 0f, canvasHeight);
            float top = Clamp(centreY + side * 0.5f, 0f, canvasHeight);

            return new CanvasRect(left, bottom, Math.Max(1f, right - left), Math.Max(1f, top - bottom));
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }

    // A rectangle in canvas pixels, bottom-left origin. Its own type rather
    // than four loose floats for the reason CastPointSpec is one.
    public readonly struct CanvasRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public CanvasRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
