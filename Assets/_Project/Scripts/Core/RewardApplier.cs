using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // Turns a settled payout into changes on the save.
    //
    // Separate from RunManager because the two halves of a payout live in
    // different places and have different lifetimes: GOLD belongs to the run and
    // is lost with it, EXPERIENCE belongs to the characters and survives. v1
    // applied both inside the fight controller, which is a large part of why
    // ending a fight touched a save file, a run and a UI in one method.
    public static class RewardApplier
    {
        // Applies a fight's experience to whoever was fielded, and reports what
        // happened to each of them.
        //
        // The report is the whole return value rather than a side effect,
        // because the rewards SCREEN needs before-and-after for its bars -- and
        // "before" stops existing the moment AddExperience mutates in place.
        public static CombatReward Apply(VictoryRewards.Payout payout, IReadOnlyList<string> fieldedIds)
        {
            var reward = new CombatReward { GoldGained = payout.Gold };

            var save = SaveSlotManager.CurrentSave;
            if (save == null) return reward;

            // SQUAD SLOT ORDER, not roster order and not the order the fight
            // happened to hold them in. ActiveSquad is ordered by the player's
            // own selection and is stable, which makes row i squad slot i by
            // construction rather than by luck -- v1 iterated a Dictionary here
            // and relied on insertion order holding.
            var squad = save.ActiveSquad();
            var fielded = new HashSet<string>(fieldedIds ?? new List<string>());

            for (int slot = 0; slot < squad.Count; slot++)
            {
                var character = squad[slot];

                // FIELDED is decided by whether the fight actually had them. A
                // squad member at 0 HP is left out of the encounter, and used to
                // vanish from the reward entirely -- so a party of three came
                // back as two rows with nothing saying why. They get a row now,
                // downed, with nothing gained.
                bool isDowned = !fielded.Contains(character.definitionId);

                // Read BEFORE the award: AddExperience mutates in place, and the
                // bar needs somewhere to start from.
                int levelBefore = character.level;
                int expBefore = character.exp;

                // Experience is NOT split across the party -- every fielded
                // character receives the full amount. A pre-existing design
                // decision, made visible here rather than changed.
                int gained = isDowned ? 0 : payout.Experience;
                if (gained > 0) character.AddExperience(gained);

                reward.Characters.Add(new CharacterReward(
                    character.definitionId,
                    DisplayNameFor(character.definitionId),
                    levelBefore, expBefore,
                    character.level, character.exp,
                    Character.ExpToNextLevel(character.level),
                    gained,
                    slot,
                    isDowned,
                    // The START level's requirement, captured before
                    // AddExperience moved the level underneath it.
                    Character.ExpToNextLevel(levelBefore)));
            }

            SaveSlotManager.SaveCurrent();
            return reward;
        }

        private static string DisplayNameFor(string definitionId)
        {
            var definition = Content.ContentDatabase.Characters.FirstOrDefault(c => c.id == definitionId);
            return definition == null ? definitionId : definition.displayName;
        }
    }
}
