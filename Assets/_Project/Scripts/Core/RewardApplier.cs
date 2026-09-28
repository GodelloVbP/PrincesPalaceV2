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
        //
        // `participants`, when given, is who this fight was FOR: squad members
        // outside it get no row and no experience. An event fight with a
        // party override (the Bell's Shawn alone) passes its override, so the
        // members who sat it out are untouched rather than paid as "downed"
        // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.4: benched XP untouched).
        // Null is the whole squad, every room fight's rule.
        public static CombatReward Apply(VictoryRewards.Payout payout, IReadOnlyList<string> fieldedIds,
            IReadOnlyCollection<string> participants = null)
        {
            var reward = ApplyUnsaved(payout, fieldedIds, participants);
            SaveSlotManager.SaveCurrent();
            return reward;
        }

        // Apply's rule without its write, for a caller that persists once
        // for several changes -- an event's exp effect
        // (RunOrchestrator.ChooseEventOption). Same loop, same split, same
        // downed-half rule; only who writes differs.
        internal static CombatReward ApplyUnsaved(VictoryRewards.Payout payout, IReadOnlyList<string> fieldedIds,
            IReadOnlyCollection<string> participants = null)
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
                if (participants != null && !participants.Contains(character.definitionId)) continue;

                // FIELDED is decided by whether the fight actually had them. A
                // squad member at 0 HP is left out of the encounter but still
                // gets a row, downed, with nothing gained, so a party of three
                // never comes back as two rows with nothing saying why.
                bool isDowned = !fielded.Contains(character.definitionId);

                // Read BEFORE the award: AddExperience mutates in place, and the
                // bar needs somewhere to start from.
                int levelBefore = character.level;
                int expBefore = character.exp;

                // Experience is NOT split across the party -- every fielded
                // character receives the full amount, and a downed one half of
                // it rounded up (progression v2 contract 4). Nothing on the
                // reward track scales this figure per character.
                //
                // ASKED OF VictoryRewards RATHER THAN COMPUTED HERE. The rule
                // was inlined on this line and stated a second time in
                // VictoryRewards.ExperienceFor, which nothing in production
                // called -- two copies of one rule, one of them live and the
                // other only ever asserted against. They agreed until the day
                // the rule changed, which is today. The Domain seam is the one
                // that survives, so the arithmetic is pinnable without a save
                // and there is one place to change it next time.
                int gained = VictoryRewards.ExperienceFor(payout, isDowned);
                if (gained > 0) character.AddExperience(gained);

                // AND THE TRACK IS NOT PAID HERE.
                //
                // COLLECTION IS SOMETHING THE PLAYER DOES. A reward is
                // WAITING until it is claimed, the waiting nodes pulse, the
                // ribbon combs them, and a collect button counts them.
                // Paying automatically here would leave all of that
                // reachable only through the debug menu.
                //
                // WHAT THIS CHANGES FOR THE PLAYER, stated plainly because it
                // is a gameplay change and not a presentation one: stat points,
                // Favor, max health and experience-find no longer arrive at the
                // end of a fight. They arrive when the track is opened and
                // collected, from the system menu's Character & Inventory pane.
                //
                // WHAT MAKES THIS SAFE: the WATERMARK decides what is owed,
                // not any call site, so a character who levelled before the
                // track existed or before a reward kind was implemented is
                // still owed it and can still collect it. Nothing expires.
                //
                // Manual collection is the design, not a state to be undone.

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

            return reward;
        }

        private static string DisplayNameFor(string definitionId)
        {
            var definition = Content.ContentDatabase.Characters.FirstOrDefault(c => c.id == definitionId);
            return definition == null ? definitionId : definition.Data.DisplayName;
        }
    }
}
