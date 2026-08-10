namespace PrincesPalace.Domain.UiKit
{
    // Inner padding on a container: the band between its own rect and the area
    // its children are laid out in.
    //
    // A container property, never a per-child one. In v1 the equivalent was
    // spelled out at each construction site as an adjusted start coordinate,
    // which is exactly how "the count came from content but the offsets stayed
    // hardcoded" bugs were written.
    public readonly struct UiPad
    {
        public readonly float Left;
        public readonly float Right;
        public readonly float Bottom;
        public readonly float Top;

        public UiPad(float left, float right, float bottom, float top)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
        }

        public static UiPad Zero => new UiPad(0f, 0f, 0f, 0f);
        public static UiPad All(float p) => new UiPad(p, p, p, p);
        public static UiPad Axis(float horizontal, float vertical) => new UiPad(horizontal, horizontal, vertical, vertical);

        public float Horizontal => Left + Right;
        public float Vertical => Bottom + Top;

        public override string ToString() => $"pad(l{Left} r{Right} b{Bottom} t{Top})";
    }
}
