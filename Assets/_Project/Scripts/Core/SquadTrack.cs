using PrincesPalace.Domain.Progression;

namespace PrincesPalace
{
    // What the fielded squad has earned from the reward track.
    //
    // THE HIGHEST LEVEL IN THE SQUAD, NEVER THE SUM, and this exists because
    // that rule was being spelled out for the fourth time. The reward track is
    // per character; a run-scoped reward on a per-character track (how many
    // second lives a descent has left) has to resolve to ONE number somehow.
    //
    // Highest rather than total for the reason ItemOfferRoll.SquadFavor already
    // gives about Favor: summing would make every reward scale with squad size,
    // so the real decision would become "bring more bodies" rather than "bring
    // the character who has earned this". Fielding your best character is
    // supposed to be the choice.
    //
    // This is docs/archive/HANDOVER_PROGRESSION_TRACK.md 4c answered the cheap way
    // while the squad is one character. The other reading -- a benefit that
    // applies only while its owner is fielded -- is more interesting and needs
    // a per-character notion of whose second life it is, which nothing in the
    // run state has.
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

        // Whether the squad has earned a capability at all.
        public static bool HasUnlocked(TrackReward reward) =>
            RewardTrack.HasUnlocked(reward, BestLevel());

        // How many second lives this descent has left. Level 90 grants one.
        //
        // P1 of docs/PLAN_REWARD_TRACKS.md retired the level-100 refresh
        // (TrackReward.SecondLifeRefresh, which used to give the charge back
        // on entering every boss) along with seven other over-arching reward
        // kinds -- so the ceiling is one per descent with no way to renew it
        // mid-run, and level 100 is a MaxHealth node on the interim table now.
        //
        // No HasUnlocked guard: UnlockedAmount with a fallback of 0 already
        // answers 0 below level 90, and the guard was a second BestLevel() scan
        // to learn what the next line was about to work out anyway.
        public static int SecondLivesLeft(RunSnapshot run)
        {
            if (run == null) return 0;

            int left = RewardTrack.UnlockedAmount(TrackReward.SecondLife, BestLevel(), 0) - run.secondLivesUsed;
            return left < 0 ? 0 : left;
        }
    }
}
