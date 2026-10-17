namespace PrincesPalace.Domain.UiKit
{
    // Inner padding on a container: the band between its own rect and the area
    // its children are laid out in.
    //
    // A container property, never a per-child one. Spelled out at each
    // construction site as an adjusted start coordinate, it is how "the count
    // came from content but the offsets stayed hardcoded" bugs get written.
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

        public float Horizontal => Left + Right;
        public float Vertical => Bottom + Top;

        public override string ToString() => $"pad(l{Left} r{Right} b{Bottom} t{Top})";
    }
}
