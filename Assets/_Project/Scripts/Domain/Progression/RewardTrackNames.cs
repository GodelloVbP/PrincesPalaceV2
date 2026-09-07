namespace PrincesPalace.Domain.Progression
{
    // What a track reward is called, for showing a player.
    //
    // In Domain beside the table rather than in UiStrings, the same place and
    // for the same reason as RelicRarityNames: the text is a function of an
    // enum the Domain owns, and splitting "what rewards exist" from "what they
    // are called" across two assemblies means a new reward kind compiles
    // cleanly while displaying as nothing.
    //
    // Upper case like the rest of the game's chrome, and the AMOUNT is folded
    // in rather than left to the caller to append -- "+15 MAX HEALTH" and
    // "A STAT POINT" put the number in different places, so a caller building
    // the string would have to know which reward it was holding.
    public static class RewardTrackNames
    {
        public static string Of(TrackEntry entry) => Of(entry.Reward, entry.Amount);

        public static string Of(TrackReward reward, int amount)
        {
            switch (reward)
            {
                case TrackReward.StatPoint:
                    return amount == 1 ? "A STAT POINT" : $"{amount} STAT POINTS";

                case TrackReward.MaxHealth:
                    return $"+{amount} MAX HEALTH";

                case TrackReward.Respec:
                    return "FREE RESPEC";

                case TrackReward.SecondLife:
                    return "A SECOND LIFE, ONCE PER RUN";

                default:
                    return "";
            }
        }
    }
}
