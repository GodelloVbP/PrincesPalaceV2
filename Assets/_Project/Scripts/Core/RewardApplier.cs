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
                // The track's experience nodes applied HERE, per character,
                // because that is what they are: a property of who earned it
                // rather than of what the fight paid. Two characters in the
                // same party can be owed different amounts for the same kill.
                //
                // Read from what the character had walking IN, before this
                // payout's own claim below can raise it -- a node crossed by
                // this very gain pays out from the next fight, not
                // retroactively on the one that earned it.
                int gained = isDowned ? 0 : character.ExperienceWorthOf(payout.Experience);
                if (gained > 0) character.AddExperience(gained);

                // AND THE TRACK IS **NOT** PAID HERE ANY MORE.
                //
                // This used to call character.ClaimTrackRewards() on every
                // payout, unconditionally, which kept `level` and
                // `claimedTrackLevel` in lockstep -- so the gap between reached
                // and collected only ever appeared for a character levelled by
                // a migration or a debug grant.
                //
                // COLLECTION IS SOMETHING THE PLAYER DOES NOW. The reward track
                // design handoff (section 3) makes that gap the screen's whole
                // state model: a reward is WAITING until it is claimed, the
                // waiting nodes pulse, the ribbon combs them, and a collect
                // button counts them. Paying automatically here would leave all
                // of that reachable only through the debug menu.
                //
                // WHAT THIS CHANGES FOR THE PLAYER, stated plainly because it
                // is a gameplay change and not a presentation one: stat points,
                // Favor, max health and experience-find no longer arrive at the
                // end of a fight. They arrive when the track is opened and
                // collected, from the system menu's Character & Inventory pane.
                //
                // The guarantee the old comment here defended is unchanged and
                // is what makes this safe: the WATERMARK decides what is owed,
                // not any call site, so a character who levelled before the
                // track existed or before a reward kind was implemented is
                // still owed it and can still collect it. Nothing expires.
                //
                // TO REVERT: put `character.ClaimTrackRewards();` back on the
                // line below. The reward track screen keeps working either way
                // -- it would simply have nothing to collect.

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
