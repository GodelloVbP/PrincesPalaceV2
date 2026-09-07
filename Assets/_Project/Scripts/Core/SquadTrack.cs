using PrincesPalace.Domain.Progression;

namespace PrincesPalace
{
    // What the fielded squad has earned from the reward track.
    //
    // A run-scoped question asked of a per-character track has to resolve to
    // ONE number somehow, and there are two honest ways to do it. This file
    // holds both, and which one a reward gets is a design decision each time
    // rather than a default:
    //
    // - THE HIGHEST LEVEL IN THE SQUAD (BestLevel), for anything the run
    //   itself has, for the reason ItemOfferRoll.SquadFavor gives: summing
    //   would make the reward scale with squad size, so the real decision
    //   becomes "bring more bodies" rather than "bring the character who has
    //   earned this".
    // - THE SUM OVER THE FIELDED SQUAD, for a reward that is genuinely each
    //   character's own and merely spent out of a shared pot. The second life
    //   is the only one today; SecondLivesLeft's own comment argues it, and
    //   docs/PLAN_REWARD_TRACKS.md §6 is where it was settled.
    //
    // This was "highest, never the sum" outright, which was
    // docs/archive/HANDOVER_PROGRESSION_TRACK.md 4c answered the cheap way
    // while the squad was one character. The reading it deferred -- a benefit
    // sourced from whoever earned it -- is what §6 chose once the tracks
    // became per-character and there was something to differ about.
    public static class SquadTrack
    {
        // The best level among the characters actually fielded. 1 when there is
        // no save or no squad, which is the level every character starts at and
        // therefore the answer that grants nothing.
        public static int BestLevel()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return RewardTrack.StartingLevel;

            int best = RewardTrack.StartingLevel;
            foreach (var character in save.ActiveSquad())
            {
                int level = character?.level ?? RewardTrack.StartingLevel;
                if (level > best) best = level;
            }

            return best;
        }

        // How many second lives this descent has left.
        //
        // THE SOURCE IS PER-CHARACTER AND THE SPEND IS SQUAD-WIDE, which is
        // the one place this file's "highest, never the sum" rule does not
        // apply, and docs/PLAN_REWARD_TRACKS.md §6 is where it was argued. A
        // squad of three who have each collected level 90 brings three
        // charges; a member who has collected none contributes none. The rule
        // above exists so a run-scoped reward does not scale with squad size,
        // and this one is a per-character reward the squad happens to spend
        // out of a shared pot -- because FightSession.TrySecondLife fires only
        // when the party would OTHERWISE BE WIPED and raises everyone who is
        // down for one charge (FightSession.Outcome.cs). Making the charge
        // owner-only would bring back exactly the outcome that rule exists to
        // avoid: reviving one member and losing anyway, having spent the
        // charge to change nothing.
        //
        // COLLECTED, not reached: like every other reward on the track, a
        // second life is read off claimedTrackLevel, so a character who has
        // hit 90 and never pressed collect has not got it yet.
        //
        // P1 of docs/PLAN_REWARD_TRACKS.md retired the level-100 refresh
        // (TrackReward.SecondLifeRefresh, which used to give the charge back
        // on entering every boss) along with seven other over-arching reward
        // kinds -- so the ceiling is what the squad has collected, with no way
        // to renew it mid-run.
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
