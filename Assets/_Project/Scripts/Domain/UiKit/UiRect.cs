using System;

namespace PrincesPalace.Domain.UiKit
{
    // An axis-aligned box in canvas space: centre + size, +y UP.
    //
    // Centre-based rather than min/max because that is the convention every
    // position in this project is authored in, and because it is what a
    // RectTransform's anchoredPosition means at a centre pivot. Min/Max are
    // derived on demand for the audit's overlap and containment maths.
    public readonly struct UiRect : IEquatable<UiRect>
    {
        public readonly UiVec Centre;
        public readonly UiVec Size;

        public UiRect(UiVec centre, UiVec size)
        {
            Centre = centre;
            Size = size;
        }

        public float Left => Centre.X - Size.X / 2f;
        public float Right => Centre.X + Size.X / 2f;
        public float Bottom => Centre.Y - Size.Y / 2f;
        public float Top => Centre.Y + Size.Y / 2f;

        public float Width => Size.X;
        public float Height => Size.Y;

        // Strict: touching edges do not overlap. Two buttons stacked with zero
        // gap are legal and common; only genuine intersection is a collision.
        public bool Overlaps(UiRect other)
        {
            return Left < other.Right && Right > other.Left
                && Bottom < other.Top && Top > other.Bottom;
        }

        public UiVec OverlapExtent(UiRect other)
        {
            float w = Math.Min(Right, other.Right) - Math.Max(Left, other.Left);
            float h = Math.Min(Top, other.Top) - Math.Max(Bottom, other.Bottom);
            return new UiVec(Math.Max(0f, w), Math.Max(0f, h));
        }

        // Tolerance because a child sized exactly to its parent is legitimate
        // and float arithmetic will not always agree with itself about that.
        public bool Contains(UiRect inner, float tolerance = 0.01f)
        {
            return inner.Left >= Left - tolerance
                && inner.Right <= Right + tolerance
                && inner.Bottom >= Bottom - tolerance
                && inner.Top <= Top + tolerance;
        }

        // How far `inner` pokes out of this rect on each side, 0 where it fits.
        // Reported in the audit message so the error names the actual overflow
        // in pixels rather than just asserting that one exists.
        public (float Left, float Right, float Bottom, float Top) Overflow(UiRect inner)
        {
            return (
                Math.Max(0f, Left - inner.Left),
                Math.Max(0f, inner.Right - Right),
                Math.Max(0f, Bottom - inner.Bottom),
                Math.Max(0f, inner.Top - Top));
        }

        // The AABB of this rect after rotation about its own centre. Rotated
        // nodes are real here (talent-tree edges are rotated stripes), and an
        // unrotated overlap test would silently miss their true footprint.
        public UiRect BoundingBoxAfterRotation(float degrees)
        {
            if (Math.Abs(degrees % 360f) < 0.0001f)
            {
                return this;
            }

            double rad = degrees * Math.PI / 180.0;
            float cos = (float)Math.Abs(Math.Cos(rad));
            float sin = (float)Math.Abs(Math.Sin(rad));
            return new UiRect(Centre, new UiVec(
                Size.X * cos + Size.Y * sin,
                Size.X * sin + Size.Y * cos));
        }

        public UiRect Scaled(UiVec scale) => new UiRect(Centre, new UiVec(Size.X * scale.X, Size.Y * scale.Y));

        public bool Equals(UiRect other) => Centre.Equals(other.Centre) && Size.Equals(other.Size);
        public override bool Equals(object obj) => obj is UiRect other && Equals(other);
        public override int GetHashCode() => unchecked((Centre.GetHashCode() * 397) ^ Size.GetHashCode());
        public override string ToString() => $"[c{Centre} s{Size}]";
    }
}
