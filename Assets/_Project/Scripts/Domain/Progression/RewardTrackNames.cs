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
    // in rather than left to the caller to append -- "+5 PRINCE'S FAVOR" and
    // "3 STARTING RELICS" put the number in different places, so a caller
    // building the string would have to know which reward it was holding.
    public static class RewardTrackNames
    {
        public static string Of(TrackEntry entry) => Of(entry.Reward, entry.Amount);

        public static string Of(TrackReward reward, int amount)
        {
            switch (reward)
            {
                case TrackReward.StatPoint:
                    return amount == 1 ? "A STAT POINT" : $"{amount} STAT POINTS";

                case TrackReward.Favor:
                    return $"+{amount} PRINCE'S FAVOR";

                case TrackReward.MaxHealth:
                    return $"+{amount} MAX HEALTH";

                case TrackReward.SignatureAtFightStart:
                    return $"+{amount} SIGNATURE AT FIGHT START";

                case TrackReward.Respec:
                    return "FREE RESPEC";

                case TrackReward.StartingRelics:
                    return $"START EVERY RUN WITH {amount} RELICS";

                case TrackReward.RestBeforeBoss:
                    return "A REST BEFORE EVERY BOSS";

                case TrackReward.OfferReroll:
                    return amount == 1 ? "AN OFFER REROLL" : $"{amount} OFFER REROLLS";

                case TrackReward.WiderOffer:
                    return $"CHOOSE FROM {amount} ITEMS";

                case TrackReward.ChosenStartingRelics:
                    return "CHOOSE YOUR STARTING RELICS";

                case TrackReward.EliteRelicDrop:
                    return "ELITES ALWAYS DROP A RELIC";

                case TrackReward.SecondLife:
                    return "A SECOND LIFE, ONCE PER RUN";

                case TrackReward.SecondLifeRefresh:
                    return "YOUR SECOND LIFE RETURNS AT EVERY BOSS";

                default:
                    return "";
            }
        }
    }
}
