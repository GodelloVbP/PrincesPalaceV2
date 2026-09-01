using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace
{
    // THE SAVE A BATCH PLAYS FROM, and the answer to "which profile were these
    // numbers measured on".
    //
    // docs/PLAN_BALANCE_BOT.md F5: a run's power is the profile (level, track
    // rewards, stat points, talents) plus whatever it picks up in-run, and
    // everything in the first list survives death while nothing in the second
    // does. A depth median with no profile attached to it is therefore not a
    // number at all, which is why the batch names one and this builds it.
    //
    // Deliberately built rather than loaded. A preset assembled from a file
    // somebody hand-edited would drift from the game's own idea of a save the
    // moment SaveData.Reconcile gained a step, so these go through exactly the
    // doors the game uses -- SaveData.CreateNew via SaveSystem.Load's
    // missing-file path, then Character.ClaimTrackRewards and Character.Invest.
    public static class ProfilePresets
    {
        public const string Fresh = "Fresh";
        public const string Mid = "Mid";
        public const string Late = "Late";

        // Tuning inputs, not laws -- plan §5 assumes 20 and 60 and says so.
        // Named constants rather than literals because the batch prints them
        // and the report is read against them.
        public const int MidLevel = 20;
        public const int LateLevel = 60;

        public static readonly IReadOnlyList<string> All = new[] { Fresh, Mid, Late };

        public static bool IsKnown(string profile) =>
            profile == Fresh || profile == Mid || profile == Late;

        public static int LevelFor(string profile)
        {
            switch (profile)
            {
                case Mid: return MidLevel;
                case Late: return LateLevel;
                default: return 1;
            }
        }

        // Builds the named profile onto whatever slot is current, and returns
        // it. THE CALLER OWNS THE SAVE ROOT: this is called under a throwaway
        // SaveSystem.RootOverride with SaveSlotManager.Forget() already done,
        // so CurrentSave takes SaveSystem.Load's missing-file branch and comes
        // back as SaveData.CreateNew() -- a new profile exactly as the game
        // makes one, Shawn fielded, starting stock granted. That IS the Fresh
        // preset; Mid and Late are that plus a level.
        public static SaveData Build(string profile)
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return null;

            // Belt and braces. CreateNew already reconciles, but a slot that
            // somehow held a file would have come through Migrate instead, and
            // the two paths must leave the same shape.
            save.Reconcile();

            int level = LevelFor(profile);
            if (level > 1)
            {
                // THE FIELDED SQUAD ONLY, not the whole roster. The roster
                // holds every authored character; ActiveSquad is who actually
                // walks into the fight, and levelling a bench nobody fields
                // would put a number in the report that no run ever felt.
                foreach (var character in save.ActiveSquad())
                {
                    if (character == null) continue;

                    character.level = level;

                    // NOT AUTOMATIC, and that is deliberate in the game:
                    // RewardApplier leaves the track unclaimed so the player
                    // collects it themselves. A bot that skipped this would
                    // play a level 60 character with a level 1 character's
                    // stat points, max health and exp-find -- which is not
                    // "level 60" by any reading a balance report could use.
                    character.ClaimTrackRewards();

                    SpendEveryPoint(character);
                }
            }

            SaveSlotManager.SaveCurrent();
            return save;
        }

        // ROUND-ROBIN over the six ability scores in authored order, starting
        // at Strength, until nothing is left to spend.
        //
        // Chosen because it is the one rule that is defensible without
        // knowing anything: every other rule (all into Constitution, all into
        // the class's own stat, a weighted split) is a BUILD, and a build is
        // a strategy the batch would then be measuring instead of the game.
        // An even spread is the neutral baseline the archetypes are supposed
        // to be the variable against -- and when a future phase wants to ask
        // "does a Constitution build go deeper", that is a second preset next
        // to this one, not a change to it.
        //
        // Character.Invest is the real door: it refuses when there is nothing
        // unspent, so the loop terminates on the character's own accounting
        // rather than on a count computed here that could disagree with it.
        private static void SpendEveryPoint(Character character)
        {
            int guard = 0;
            int i = 0;

            // The guard is not the terminating condition -- Invest returning
            // false is. It is here because this runs thousands of times per
            // batch inside a headless process with no way to interrupt it, and
            // an accounting bug that made Invest succeed forever would hang
            // the whole run rather than fail one.
            while (character.unspentStatPoints > 0 && guard++ < 100000)
            {
                var score = AbilityScores.All[i % AbilityScores.All.Length];
                i++;

                if (!character.Invest(score)) break;
            }
        }
    }
}
