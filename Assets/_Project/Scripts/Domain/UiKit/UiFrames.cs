namespace PrincesPalace.Domain.UiKit
{
    // The canvas sizes every screen is audited at.
    //
    // Solving is pure arithmetic, so auditing four aspects costs four times
    // almost nothing. It is worth doing because pinned and stretched nodes MOVE
    // relative to each other as aspect changes, and a layout that is clean at
    // 16:9 can collide at 21:9 in a way no single-frame check can see.
    //
    // Flow content inside a fixed-size panel is aspect-invariant, so this only
    // ever finds canvas-level problems -- which is exactly where anchoring
    // decisions live.
    public static class UiFrames
    {
        // The frame everything is authored against: "authored == rendered".
        public static readonly UiVec Reference = new UiVec(1920f, 1080f);

        public static readonly UiVec UltraWide = new UiVec(2580f, 1080f);   // 21:9
        public static readonly UiVec FourThree = new UiVec(1920f, 1440f);   // 4:3
        public static readonly UiVec SixteenTen = new UiVec(1920f, 1200f);  // 16:10

        public static readonly UiVec[] All = { Reference, UltraWide, FourThree, SixteenTen };

        public static string Describe(UiVec frame) => $"{frame.X:0}x{frame.Y:0}";
    }
}
