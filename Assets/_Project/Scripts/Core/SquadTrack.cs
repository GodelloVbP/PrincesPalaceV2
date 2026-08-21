using PrincesPalace.Domain.Progression;

namespace PrincesPalace
{
    // What the fielded squad has earned from the reward track.
    //
    // THE HIGHEST LEVEL IN THE SQUAD, NEVER THE SUM, and this exists because
    // that rule was being spelled out for the fourth time. The reward track is
    // per character; several of its rewards are run-scoped (how many relics a
    // descent drafts, how wide the item offer is, how many rerolls it gets,
    // whether a rest is guaranteed before a boss) and a run-scoped reward on a
    // per-character track has to resolve to ONE number somehow.
    //
    // Highest rather than total for the reason ItemOfferRoll.SquadFavor already
    // gives about Favor: summing would make every reward scale with squad size,
    // so the real decision would become "bring more bodies" rather than "bring
    // the character who has earned this". Fielding your best character is
    // supposed to be the choice.
    //
    // This is docs/HANDOVER_PROGRESSION_TRACK.md 4c answered the cheap way
    // while the squad is one character. The other reading -- a benefit that
    // applies only while its owner is fielded -- is more interesting and needs
    // a per-character notion of whose relic or whose reroll it is, which
    // nothing in the run state has.
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

        // ---- the run-scoped rewards, all asked the same way ---------------------
        //
        // GATHERED HERE rather than left one per system. These were three
        // one-liners in three files -- ItemOfferRoll knew about offer width and
        // rerolls, RelicDraftController about the draft count, and this file
        // about second lives -- and every one of them was the same sentence:
        // ask the track a question at BestLevel(). Scattered, the max-not-sum
        // rule was a comment repeated four times; gathered, it is the type's
        // header and the four accessors are one line each.

        // How many relics a descent drafts.
        public static int StartingRelics() => RewardTrack.StartingRelics(BestLevel());

        // How many items a won fight offers.
        public static int OfferWidth() => Domain.UiKit.OfferRowLayout.CardsFor(BestLevel());

        // How many item-offer rerolls a descent gets.
        public static int RerollsPerRun() => RewardTrack.RerollsPerRun(BestLevel());

        // How many second lives this descent has left. Level 90 grants one;
        // level 100 gives it back at every boss, which RunManager does by
        // clearing the spend count rather than handing out a second charge --
        // so the ceiling stays one at a time however deep a run goes.
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
