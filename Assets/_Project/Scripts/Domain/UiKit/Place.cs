namespace PrincesPalace.Domain.UiKit
{
    public enum PlaceKind
    {
        Flow,
        At,
        Pin,
        Stretch,
        Frac,
    }

    // WHERE a node sits. This closed set is the direct answer to why v1's
    // NewUiRect helper died at 13 of 86 adoption: it hardcoded centre anchors,
    // so stretched backgrounds, corner-pinned HUD elements and fractional
    // overhangs simply could not go through it. Roughly half the construction
    // sites in the project legitimately needed something it could not say, and
    // a helper you are entitled to bypass is a helper nobody uses.
    //
    // Every one of those shapes is expressible here, which is what makes "the
    // rect preamble exists exactly once, in the emitter" an enforceable claim
    // rather than an aspiration.
    //
    // Ordering convention throughout: left, right, bottom, top -- matching
    // UiPad, and +y UP like the rest of canvas space. Mixed edge ordering
    // between neighbouring types is its own small bug factory.
    public readonly struct Place
    {
        public readonly PlaceKind Kind;
        public readonly UiVec AnchorMin;
        public readonly UiVec AnchorMax;
        public readonly UiVec Pivot;

        // At/Pin/Frac: offset from the anchor point.
        public readonly UiVec Offset;

        // Stretch/Frac: insets from the anchored edges.
        public readonly float Left;
        public readonly float Right;
        public readonly float Bottom;
        public readonly float Top;

        private Place(PlaceKind kind, UiVec anchorMin, UiVec anchorMax, UiVec pivot,
                      UiVec offset, float left, float right, float bottom, float top)
        {
            Kind = kind;
            AnchorMin = anchorMin;
            AnchorMax = anchorMax;
            Pivot = pivot;
            Offset = offset;
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
        }

        // The default. The parent's flow container decides entirely -- there is
        // no position argument to get wrong, which is what makes the
        // "y = start - i * hardcodedPitch" loop unwritable at a construction
        // site (19 of them in v1, two byte-identical).
        public static Place Flow =>
            new Place(PlaceKind.Flow, UiVec.Centre, UiVec.Centre, UiVec.Centre, UiVec.Zero, 0f, 0f, 0f, 0f);

        // Centre-anchored absolute -- v1's universal convention, preserved so
        // ported coordinates mean the same thing they always did.
        public static Place At(float x, float y) =>
            new Place(PlaceKind.At, UiVec.Centre, UiVec.Centre, UiVec.Centre, new UiVec(x, y), 0f, 0f, 0f, 0f);

        public static Place At(float x, float y, UiVec pivot) =>
            new Place(PlaceKind.At, UiVec.Centre, UiVec.Centre, pivot, new UiVec(x, y), 0f, 0f, 0f, 0f);

        // Pinned to a corner or edge of the parent, so it keeps its relationship
        // to that edge when the canvas aspect changes. The initiative tracker
        // and any HUD corner element want this.
        public static Place Pin(UiVec anchor, UiVec pivot, UiVec offset) =>
            new Place(PlaceKind.Pin, anchor, anchor, pivot, offset, 0f, 0f, 0f, 0f);

        public static Place Pin(UiVec anchor, UiVec offset) => Pin(anchor, anchor, offset);

        // Full-bleed: backgrounds, dimmers, invisible click catchers.
        public static Place Stretch(float left = 0f, float right = 0f, float bottom = 0f, float top = 0f) =>
            new Place(PlaceKind.Stretch, UiVec.Zero, UiVec.One, UiVec.Centre, UiVec.Zero, left, right, bottom, top);

        // Fractional and deliberately overhanging anchors -- a ground shadow
        // wider than its actor, a glow bleeding past its card.
        public static Place Frac(UiVec anchorMin, UiVec anchorMax,
                                 float left = 0f, float right = 0f, float bottom = 0f, float top = 0f) =>
            new Place(PlaceKind.Frac, anchorMin, anchorMax, UiVec.Centre, UiVec.Zero, left, right, bottom, top);

        public bool IsFlow => Kind == PlaceKind.Flow;

        // True when the placement itself determines the node's size, so the
        // solver must not also apply a UiSize on that axis.
        public bool SizedByPlacement => Kind == PlaceKind.Stretch || Kind == PlaceKind.Frac;

        public override string ToString() => Kind == PlaceKind.Flow ? "Flow" : $"{Kind}{Offset}";
    }
}
