namespace PrincesPalace.Domain.UiKit
{
    // Which side of the focused control the marker sits on.
    //
    // Left and Above are the two the SHAPE picks (EdgeFor(UiVec)): the
    // owner's brief asks for "left of list rows and vertical buttons, above
    // cards, plates, seats, orbs and fight targets", and those two lists are
    // separated by ONE property of the control -- a row or a button is much
    // wider than it is tall, everything else is not.
    //
    // Right and Below exist for one reason only: the shape's edge can be
    // occupied. A marker drawn over a NEIGHBOURING control reads as
    // selecting it. EdgeFor(target, canvas, obstacles) moves it to the
    // opposite side, then to the other axis. No screen authors an edge.
    public enum FocusEdge
    {
        Left,
        Above,
        Right,
        Below,
    }

    // Where the marker stands: the edge (which sets its rotation and bob
    // direction) and its centre, which is not always Place(edge) -- a
    // keep-clear rect on that edge steps it outward (FocusMarkerPlacement.
    // Resolve).
    public readonly struct FocusSpot
    {
        public readonly FocusEdge Edge;
        public readonly UiVec Centre;

        public FocusSpot(FocusEdge edge, UiVec centre)
        {
            Edge = edge;
            Centre = centre;
        }
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
        public static float RotationFor(FocusEdge edge)
        {
            switch (edge)
            {
                case FocusEdge.Left: return 0f;
                case FocusEdge.Right: return 180f;
                case FocusEdge.Below: return 90f;
                default: return -90f;
            }
        }

        // THE EDGE, WITH THE NEIGHBOURS CONSULTED.
        //
        // The shape's edge (EdgeFor(UiVec)) unless the marker there would
        // overlap an obstacle -- another visible control or visible text,
        // which the caller collects (Core/FocusMarker.cs) -- then the
        // opposite edge, then the two on the other axis, the first that is
        // clear. The marker's box is tested WITH its bob, since the bob is
        // what swings it into a neighbour 8px away. When every edge is
        // occupied the shape's edge stands: something has to be drawn, and
        // the shape's edge is the one the player has learned to expect.
        public static FocusEdge EdgeFor(UiRect target, UiRect canvas, System.Collections.Generic.IReadOnlyList<UiRect> obstacles) =>
            Resolve(target, canvas, obstacles, null).Edge;

        // THE EDGE AND THE SPOT, with the two kinds of neighbour a marker can
        // meet told apart.
        //
        // OCCUPANTS (controls, text) take an edge: a marker drawn over one
        // reads as selecting it, so that side is out and the next is tried.
        //
        // KEEP-CLEAR rects (a list's scrollbar) never take an edge. Nobody
        // reads an arrow beside a scrollbar as selecting the scrollbar; the
        // defect is only that the arrow crowds it. So
        // the marker on that edge steps OUTWARD past it and stands off it by
        // the same Gap it stands off the control -- the bar reads as part of
        // the list the arrow points into. Turning a scrollbar into an
        // occupant instead would have taken the right edge too, and with the
        // verb column on the left and rows above and below, every edge would
        // be taken and the arrow would drop back onto ITEM.
        //
        // Occupants are tested at the STEPPED spot, so stepping past a bar
        // onto a control still counts as that edge being taken.
        public static FocusSpot Resolve(UiRect target, UiRect canvas,
            System.Collections.Generic.IReadOnlyList<UiRect> occupants,
            System.Collections.Generic.IReadOnlyList<UiRect> keepClear)
        {
            var preferred = EdgeFor(target.Size);
            var preferredSpot = new FocusSpot(preferred, PlaceClear(target, canvas, preferred, keepClear));
            if (occupants == null || occupants.Count == 0) return preferredSpot;

            foreach (var edge in Order(preferred))
            {
                var spot = edge == preferred
                    ? preferredSpot
                    : new FocusSpot(edge, PlaceClear(target, canvas, edge, keepClear));
                if (IsClear(MarkerBoxAt(spot.Centre), occupants)) return spot;
            }

            return preferredSpot;
        }

        // Opposite side first -- still level with the control, still reads
        // as the same kind of pointer -- then the other axis.
        private static FocusEdge[] Order(FocusEdge preferred) => preferred == FocusEdge.Left
            ? new[] { FocusEdge.Left, FocusEdge.Right, FocusEdge.Above, FocusEdge.Below }
            : new[] { FocusEdge.Above, FocusEdge.Below, FocusEdge.Left, FocusEdge.Right };

        // Everything the marker can cover at that edge: its box, grown by the
        // bob's reach on every side.
        public static UiRect MarkerBox(UiRect target, UiRect canvas, FocusEdge edge) =>
            MarkerBoxAt(Place(target, canvas, edge));

        public static UiRect MarkerBoxAt(UiVec centre)
        {
            float reach = Size + 2f * BobAmplitude;
            return new UiRect(centre, new UiVec(reach, reach));
        }

        private static bool IsClear(UiRect marker, System.Collections.Generic.IReadOnlyList<UiRect> obstacles)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                if (marker.Overlaps(obstacles[i])) return false;
            }
            return true;
        }

        // Place(target, canvas, edge), then stepped outward along the edge's
        // normal past every keep-clear rect its bobbing box touches, to Gap
        // beyond that rect's far side. Outward only, so a bar can push the
        // marker away from the control but never onto it; repeated so a
        // second rect the first step lands on is stepped past too (one pass
        // per rect is the most that can ever be needed). Clamped last, like
        // Place itself.
        public static UiVec PlaceClear(UiRect target, UiRect canvas, FocusEdge edge,
            System.Collections.Generic.IReadOnlyList<UiRect> keepClear)
        {
            var at = Place(target, canvas, edge);
            if (keepClear == null || keepClear.Count == 0) return at;

            float half = Size / 2f;
            for (int pass = 0; pass < keepClear.Count; pass++)
            {
                bool moved = false;
                for (int i = 0; i < keepClear.Count; i++)
                {
                    var bar = keepClear[i];
                    if (!MarkerBoxAt(at).Overlaps(bar)) continue;

                    switch (edge)
                    {
                        case FocusEdge.Left: at = new UiVec(System.Math.Min(at.X, bar.Left - Gap - half), at.Y); break;
                        case FocusEdge.Right: at = new UiVec(System.Math.Max(at.X, bar.Right + Gap + half), at.Y); break;
                        case FocusEdge.Below: at = new UiVec(at.X, System.Math.Min(at.Y, bar.Bottom - Gap - half)); break;
                        default: at = new UiVec(at.X, System.Math.Max(at.Y, bar.Top + Gap + half)); break;
                    }
                    moved = true;
                }

                if (!moved) break;
            }

            return Clamp(at, canvas);
        }

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

            UiVec at;
            switch (edge)
            {
                case FocusEdge.Left: at = new UiVec(target.Left - Gap - half, target.Centre.Y); break;
                case FocusEdge.Right: at = new UiVec(target.Right + Gap + half, target.Centre.Y); break;
                case FocusEdge.Below: at = new UiVec(target.Centre.X, target.Bottom - Gap - half); break;
                default: at = new UiVec(target.Centre.X, target.Top + Gap + half); break;
            }

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

            // Positive `along` always moves the marker TOWARD the control:
            // +x from the left, -x from the right, -y from above, +y from below.
            switch (edge)
            {
                case FocusEdge.Left: return new UiVec(along, 0f);
                case FocusEdge.Right: return new UiVec(-along, 0f);
                case FocusEdge.Below: return new UiVec(0f, along);
                default: return new UiVec(0f, -along);
            }
        }
    }
}
