namespace PrincesPalace.Domain.UiKit
{
    // The party's figure on the descent map: where it stands, and how it gets
    // to the next room.
    //
    // Pure arithmetic in Domain for the same reason MapLayout is: the walk has
    // to follow the SAME curve the trail is drawn from, or the figure crosses
    // open canopy while a path sits somewhere else on screen. Both read
    // MapLayout.PathPoint, so there is one bezier and not two.
    //
    // Knows nothing about which character it is drawing. The figure is looked
    // up from the party at runtime (CharacterDefinition.battleSpritePath ->
    // StanceAnimationLibrary), so a second character is a content entry rather
    // than a change here.
    public static class MapWalk
    {
        // Where the figure stands, relative to its room's centre.
        //
        // MEASURED, not chosen: tools/measure_clearing.py scans the painted
        // clearings out of forest_map_background.png and reports 155 x 283
        // content units. The tile standing in one is 100 x 130, so there is no
        // "beside the icon" that also fits inside the clearing -- 27 units of
        // margin each side against a figure half again as wide as it is tall.
        //
        // So the figure stands at the clearing's right EDGE and overhangs the
        // canopy, which is consistent rather than sloppy: the trails are drawn
        // over the canopy too, and a walker on a path is on a path. Standing
        // right rather than left is what puts it between the room it is in and
        // the choice it is about to make, and low enough not to cover the icon
        // that says what kind of room this is.
        public const float StandX = 66f;
        public const float StandY = -40f;

        // The figure's drawn height; its width follows from the art's own
        // aspect. Shawn's idle is 540x370, i.e. half again as wide as tall,
        // which is most of why "next to the icon" does not fit.
        public const float FigureHeight = 96f;

        // How long one column-gap of travel takes. Distance-scaled rather than
        // fixed, so a step to the room directly across is not slower per unit
        // than a step to a room two rows up.
        public const float SecondsPerColumn = 0.85f;

        // How long the camera takes to catch up after an arrival. Its own
        // number rather than a share of the walk: the pan is always exactly one
        // column-gap however long the step to that room took.
        public const float PanSeconds = 0.35f;

        // Floors and ceilings on that, so a near-vertical link inside one
        // column does not finish instantly and a long diagonal does not become
        // a walk the player waits through.
        public const float MinSeconds = 0.35f;
        public const float MaxSeconds = 1.4f;

        public static float DurationFor(UiVec from, UiVec to)
        {
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float distance = (float)System.Math.Sqrt(dx * dx + dy * dy);

            float seconds = SecondsPerColumn * distance / MapLayout.ColumnGap;
            if (seconds < MinSeconds) return MinSeconds;
            if (seconds > MaxSeconds) return MaxSeconds;
            return seconds;
        }

        // Where the figure is at time t along the link, INCLUDING the standing
        // offset -- so arriving lands it exactly where Standing() puts it and
        // there is no jump on the last frame.
        public static UiVec PositionAt(UiVec from, UiVec to, int seed, float t)
        {
            var point = MapLayout.PathPoint(from, to, seed, Clamp01(t));
            return new UiVec(point.X + StandX, point.Y + StandY + BobAt(t));
        }

        public static UiVec Standing(UiVec room) =>
            new UiVec(room.X + StandX, room.Y + StandY);

        // The whole of the walk animation, because there is no walk cycle in
        // the art -- Resources/Characters/sheep has idle, cast, hurt, defeated,
        // victory and a 6-frame attack, and nothing for locomotion.
        //
        // Two bounces per step at 5 units. Deliberately small: the figure is
        // ~96 tall, and a bob big enough to read as a gait on a walk-cycle
        // sprite reads as a hop on a static one. Replaceable by real frames
        // without touching anything else here.
        public const float BobHeight = 5f;
        public const int BobsPerStep = 2;

        // ONE BOUNCE IS PI OF PHASE, not 2 PI: the absolute value halves the
        // sine's period, so a bounce is a HALF cycle of the underlying wave and
        // the extra doubling this used to carry delivered four bounces where
        // BobsPerStep says two. Pinned by MapWalkBobTests, which counts the
        // peaks rather than trusting the constant.
        public static float BobAt(float t)
        {
            double phase = Clamp01(t) * System.Math.PI * BobsPerStep;
            return (float)(System.Math.Abs(System.Math.Sin(phase)) * BobHeight);
        }

        // The descent runs left to right, so a figure whose art faces right is
        // already facing its destination. Stated rather than assumed because
        // CharacterDefinition carries a facing and the next character's art may
        // not agree with Shawn's.
        public static bool FacesLeft(UiVec from, UiVec to) => to.X < from.X;

        private static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }
}
