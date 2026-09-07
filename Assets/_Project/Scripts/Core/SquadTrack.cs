using PrincesPalace.Domain.Progression;

namespace PrincesPalace
{
    // How many second lives the fielded squad has earned from the reward
    // track.
    //
    // A run-scoped question asked of a per-character track has to resolve to
    // ONE number somehow, and this is THE SUM OVER THE FIELDED SQUAD, for a
    // reward that is each character's own and merely spent out of a shared
    // pot -- SecondLivesLeft's own comment argues it, and
    // docs/PLAN_REWARD_TRACKS.md §6 is where it was settled.
    //
    // NOT "highest in the squad" -- that rule belongs beside a run-scoped
    // reward the whole squad shares equally regardless of who earned it
    // (ItemOfferRoll.SquadFavor is that case: summing there would make the
    // reward scale with squad size). This class sums for the opposite
    // reason, so a "highest in the squad" rule does not belong here even if
    // a future run-scoped reward needs one again.
    public static class SquadTrack
    {
        // How many second lives this descent has left.
        //
        // THE SOURCE IS PER-CHARACTER AND THE SPEND IS SQUAD-WIDE, and
        // docs/PLAN_REWARD_TRACKS.md §6 is where that split was argued. A
        // squad of three who have each collected level 90 brings three
        // charges; a member who has collected none contributes none. Summing
        // is right here, unlike a run-scoped reward the whole squad shares
        // equally regardless of who earned it (this class's header), because
        // FightSession.TrySecondLife fires only when the party would
        // OTHERWISE BE WIPED and raises everyone who is down for one charge
        // (FightSession.Outcome.cs). Making the charge owner-only would bring
        // back exactly the outcome that design avoids: reviving one member
        // and losing anyway, having spent the charge to change nothing.
        //
        // COLLECTED, not reached: like every other reward on the track, a
        // second life is read off claimedTrackLevel, so a character who has
        // hit 90 and never pressed collect has not got it yet.
        //
        // NO MID-RUN REFRESH. The ceiling is exactly what the squad has
        // collected off the track; nothing renews a spent charge before the
        // next level-up.
        public static int SecondLivesLeft(RunSnapshot run)
        {
            if (run == null) return 0;

            var save = SaveSlotManager.CurrentSave;
            if (save == null) return 0;

            int earned = 0;
            foreach (var character in save.ActiveSquad())
            {
                if (character == null) continue;

                earned += RewardTracks.For(character)
                    .CollectedTotal(TrackReward.SecondLife, character.claimedTrackLevel);
            }

            int left = earned - run.secondLivesUsed;
            return left < 0 ? 0 : left;
        }
    }
}
