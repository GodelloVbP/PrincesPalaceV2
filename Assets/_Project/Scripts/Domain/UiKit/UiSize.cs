namespace PrincesPalace.Domain.UiKit
{
    public enum UiSizeMode
    {
        // An authored number. The overwhelmingly common case, and the one that
        // matches how every position in this project is already written.
        Fixed,

        // Measured from children plus padding. Bottom-up data flow.
        FromChildren,

        // Take whatever the parent has spare. Top-down data flow.
        Fill,
    }

    // Per AXIS, not per node — a row of buttons that fills the panel's width
    // while standing 60px tall is ordinary, and forcing one mode onto both axes
    // is the kind of expressiveness gap that made v1's NewUiRect bypassable.
    //
    // The two non-Fixed modes flow in OPPOSITE directions, which is precisely
    // why the solver is two-phase (measure bottom-up, then arrange top-down)
    // and why Fill inside FromChildren on the same axis is a circular
    // definition the solver refuses rather than guesses at (audit A5).
    public readonly struct UiSize
    {
        public readonly UiSizeMode ModeX;
        public readonly UiSizeMode ModeY;

        // Fixed: the size on that axis. FromChildren: extra padding added to
        // the measured content. Fill: unused.
        public readonly float X;
        public readonly float Y;

        public UiSize(UiSizeMode modeX, float x, UiSizeMode modeY, float y)
        {
            ModeX = modeX;
            X = x;
            ModeY = modeY;
            Y = y;
        }

        public static UiSize Fixed(float width, float height) =>
            new UiSize(UiSizeMode.Fixed, width, UiSizeMode.Fixed, height);

        public static UiSize Fixed(UiVec size) => Fixed(size.X, size.Y);

        public static UiSize FromChildren(float padWidth = 0f, float padHeight = 0f) =>
            new UiSize(UiSizeMode.FromChildren, padWidth, UiSizeMode.FromChildren, padHeight);

        public static UiSize Fill =>
            new UiSize(UiSizeMode.Fill, 0f, UiSizeMode.Fill, 0f);

        public static UiSize FillWidth(float height) =>
            new UiSize(UiSizeMode.Fill, 0f, UiSizeMode.Fixed, height);

        public static UiSize FillHeight(float width) =>
            new UiSize(UiSizeMode.Fixed, width, UiSizeMode.Fill, 0f);

        public bool HasMode(UiSizeMode mode) => ModeX == mode || ModeY == mode;

        public override string ToString() => $"size({ModeX}:{X}, {ModeY}:{Y})";
    }
}
