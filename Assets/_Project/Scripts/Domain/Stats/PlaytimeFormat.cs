namespace PrincesPalace.Domain.Stats
{
    // How a lifetime playtime total reads on a save slot card.
    //
    // Domain, not Core, even though nothing else here touches saves --
    // formatting a number into words is exactly the kind of arithmetic an
    // EditMode test should own rather than a PlayMode one, and the boundary
    // hours/minutes cross is precisely where a hand-checked string is most
    // likely to be one digit wrong.
    public static class PlaytimeFormat
    {
        // "3h 12m" once an hour has passed, "41m" before that, "<1m" for a
        // slot that has barely been opened. Never seconds -- a save slot is
        // read from across the room, not audited to the second, and "0m" for
        // a slot played for 45 seconds reads as broken where "<1m" reads as
        // exactly what it is.
        public static string Describe(float totalSeconds)
        {
            if (totalSeconds < 0f) totalSeconds = 0f;

            int totalMinutes = (int)(totalSeconds / 60f);
            if (totalMinutes < 1) return "<1m";

            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
        }
    }
}
