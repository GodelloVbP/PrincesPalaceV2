namespace PrincesPalace.Domain.UiKit
{
    // Which side of the focused control the marker sits on.
    //
    // TWO VALUES, not four. The owner's brief asks for "left of list rows and
    // vertical buttons, above cards, plates, seats, orbs and fight targets",
    // and those two lists are separated by ONE property of the control: a row
    // or a button is much wider than it is tall, everything else is not. A
    // Right and a Below would be a third and fourth rule with nothing in this
    // project asking for them.
    public enum FocusEdge
    {
        Left,
        Above,
    }

    // WHERE THE ONE FOCUS MARKER GOES, for every screen.
    //
    // Pure arithmetic in canvas space (centre origin, +y UP -- UiVec's own
    // convention), so an EditMode test pins it with literal rects and no
    // scene, the same bargain UiSolver and TooltipPlacement already make.
    // Core/FocusMarker.cs is the only caller: it converts a RectTransform's
    // world rect into a UiRect at the boundary and back to an
    // anchoredPosition afterwards, and holds none of the reasoning below.
    //
    // THE EDGE IS DERIVED, NOT AUTHORED, and that is a deliberate departure
    // from "the module hands the marker an edge hint". A hint carried from
    // the source of truth would mean every screen in the project authoring a
    // value for every focusable control -- a hundred-odd new declarations,
    // each of which can be forgotten or contradict its neighbour, for a
    // question the control's own shape already answers. One rule, computed
    // from the rect, cannot drift.
    public static class FocusMarkerPlacement
    {
        // Wider than this many times its own height and the control is a row
        // or a bar, so the marker goes beside it rather than over it.
        //
        // Measured against what this project actually builds rather than
        // picked: a Fight verb plate and an Options row sit near 5:1, a
        // submenu row near 7:1; a Party seat and a Talent orb are square, a
        // Reckoning offer card and a Dossier pack cell are TALLER than wide,
        // and the Hub's gate is 620x620. Nothing in the project lives between
        // 1.4 and 3.0, so the threshold has a wide gap to sit in and a
        // slightly different control cannot flip it by accident.
        public const float WideAspect = 1.8f;

        // The gap between the control's edge and the marker's near edge.
        public const float Gap = 10f;

        // The marker's own box. "No larger than about 60 percent of a text
        // line" (the owner's brief) against this kit's ordinary 29pt body
        // (Ui.cs's own default font size) is about 17 units of ink; the box is
        // bigger than the ink because the baked arrow carries its own
        // transparent margin, and sizing the BOX to 17 would have left a 11pt
        // arrow nobody can see.
        public const float Size = 26f;

        public static FocusEdge EdgeFor(UiVec size) =>
            size.Y > 0f && size.X >= size.Y * WideAspect ? FocusEdge.Left : FocusEdge.Above;

        // Degrees about Z, for a sprite baked pointing RIGHT.
        //
        // The arrow points AT the control from the outside, so the rotation is
        // a consequence of the edge and never a separate decision: sitting on
        // the left it points right (unrotated), sitting above it points down
        // (-90, clockwise in Unity's counter-clockwise-positive convention).
        public static float RotationFor(FocusEdge edge) => edge == FocusEdge.Left ? 0f : -90f;

        // The marker's CENTRE, in the same canvas space `target` and `canvas`
        // are given in.
        //
        // Clamped so the marker's own box stays inside the canvas. A control
        // flush against the canvas edge is real -- the Hub's gate reaches the
        // bottom, the Fight verb column starts at the left margin -- and a
        // marker half off the screen is worse than one overlapping the control
        // it points at by a few units. The clamp is the LAST step, so it can
        // only ever pull the marker back inward from the edge the rule above
        // chose; it never re-picks the side.
        public static UiVec Place(UiRect target, UiRect canvas) =>
            Place(target, canvas, EdgeFor(target.Size));

        public static UiVec Place(UiRect target, UiRect canvas, FocusEdge edge)
        {
            float half = Size / 2f;

            var at = edge == FocusEdge.Left
                ? new UiVec(target.Left - Gap - half, target.Centre.Y)
                : new UiVec(target.Centre.X, target.Top + Gap + half);

            return Clamp(at, canvas);
        }

        // WHETHER A CONTROL INSIDE A CLIPPING WINDOW IS THERE TO POINT AT.
        //
        // The control's CENTRE has to lie inside the window, edges inclusive,
        // because the centre is exactly what the marker lines up with (Place
        // above: level with a row's centre, on a card's centre line). A row
        // scrolled half out still shows the part the arrow points at; a row
        // whose centre is gone would have the arrow pointing at the window's
        // frame, or at nothing.
        //
        // Hidden, not clamped to the window's edge: a marker pinned to the
        // edge would stand beside a DIFFERENT, visible row while Submit
        // presses the clipped one. The pad itself never needs this -- arrowing
        // scrolls the focused row into view (FightController.
        // ScrollSubmenuRowIntoView) -- so this only answers a wheel or a
        // scrollbar drag that moved the list out from under the focus.
        public static bool IsVisibleWithin(UiRect target, UiRect clip)
        {
            var c = target.Centre;
            return c.X >= clip.Left && c.X <= clip.Right && c.Y >= clip.Bottom && c.Y <= clip.Top;
        }

        // Keeps the marker's whole box inside `canvas`. A canvas narrower than
        // the marker itself cannot satisfy both edges; the low edge wins,
        // which is arbitrary and unreachable (the reference canvas is
        // 1920x1080 and this box is 26), and is stated rather than guarded so
        // nothing reads as a supported case.
        public static UiVec Clamp(UiVec centre, UiRect canvas)
        {
            float half = Size / 2f;

            float x = centre.X;
            if (x + half > canvas.Right) x = canvas.Right - half;
            if (x - half < canvas.Left) x = canvas.Left + half;

            float y = centre.Y;
            if (y + half > canvas.Top) y = canvas.Top - half;
            if (y - half < canvas.Bottom) y = canvas.Bottom + half;

            return new UiVec(x, y);
        }

        // THE BOB, as a pure offset along the marker's own pointing axis.
        //
        // Along the axis rather than always vertical: an arrow sitting to the
        // left of a row nudges toward and away from the row it points at, and
        // one sitting above a card does the same downward -- the motion reads
        // as the marker indicating the control rather than as the marker
        // itself floating. One cosine, the BeaconPulse shape the reuse
        // registry already names for "a signal", not the three-sine flicker
        // that is for flame.
        public const float BobAmplitude = 3f;
        public const float BobPeriod = 1.6f;

        public static UiVec BobOffset(FocusEdge edge, float unscaledTime)
        {
            double phase = 2.0 * System.Math.PI * unscaledTime / BobPeriod;
            float along = (float)System.Math.Cos(phase) * BobAmplitude;

            // +x moves a left-hand marker toward the control; -y moves an
            // above marker toward it.
            return edge == FocusEdge.Left ? new UiVec(along, 0f) : new UiVec(0f, -along);
        }
    }
}
