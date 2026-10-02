using System;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Stage
{
    // THE PART OF A PROJECTILE'S LINE OF FLIGHT THAT HAS BEEN CROSSED: where it
    // is centred, how long it is, and which way it tilts. What a layer laid
    // `align: flight` along a projectile draws into, every frame.
    //
    // THE LINE IS LAUNCH TO AIM, not the box path. An oriented projectile's
    // box centre rides that line moved back by the sheet's impact point, so the
    // two points a wake has to join are the ones Core measured as the flight's
    // own (SpellLayerInstance.FlightFrom/FlightTo), and this is the arithmetic
    // over them, pinned with literals by FlightSpanTests.
    //
    // `crossed` IS THE PROJECTILE'S OWN PROGRESS (SpellPerformance.FractionOf),
    // 0 at the launch and 1 at the aim: the span grows from the launch point
    // with its tip, so the wake is always exactly as long as the path already
    // flown.
    //
    // TURNED INTO (-90, 90], like FormationSpan, so a sheet drawn left to right
    // is never upside down on a cast that flies leftward. A wake is symmetric
    // end to end, so the flip costs it nothing; a direction-sensitive drawing
    // would not be a wake.
    public readonly struct FlightSpan
    {
        public readonly UiVec Centre;
        public readonly float Length;
        public readonly float Degrees;

        private FlightSpan(UiVec centre, float length, float degrees)
        {
            Centre = centre;
            Length = length;
            Degrees = degrees;
        }

        public static FlightSpan Between(UiVec launch, UiVec aim, float crossed)
        {
            float c = crossed < 0f ? 0f : crossed > 1f ? 1f : crossed;
            float dx = aim.X - launch.X;
            float dy = aim.Y - launch.Y;
            float whole = (float)Math.Sqrt(dx * dx + dy * dy);

            double degrees = whole <= 0f ? 0.0 : Math.Atan2(dy, dx) * 180.0 / Math.PI;
            if (degrees > 90.0) degrees -= 180.0;
            else if (degrees <= -90.0) degrees += 180.0;

            var centre = new UiVec(launch.X + dx * c * 0.5f, launch.Y + dy * c * 0.5f);
            return new FlightSpan(centre, whole * c, (float)degrees);
        }
    }
}
