using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace
{
    // The per-save, mutable half of a character. Everything authored and
    // unchanging (display name, role, base stats) lives on the matching
    // CharacterDefinition and is resolved through ContentDatabase, so this
    // stays small and save files stay readable.
    [Serializable]
    public class Character
    {
        // Matches CharacterDefinition.id. Doubles as this character's
        // identity within a save, since the roster holds one of each.
        public string definitionId;

        // Orbs this character has awakened. Bought with EMBERS from the
        // save's own wallet (see TalentController), not with anything stored
        // here -- so this is a list of what was chosen, with no matching
        // per-character currency alongside it.
        public List<string> unlockedTalentIds = new List<string>();

        // Skills gated behind something OTHER than level — right now, only
        // an Event room's mage encounter grants one (see GameplayManager's
        // RoomType.Event case). Kept separate from unlockedTalentIds rather
        // than reusing it: a talent point is spent on a choice the player
        // made in the tree, this is handed over unconditionally by the
        // event, and conflating the two would make a respec (which only
        // ever refunds unlockedTalentIds) either strip an event-taught
        // spell it never granted or silently fail to strip a talent.
        public List<string> unlockedSkillIds = new List<string>();

        // LEGACY, migration source only. This was the whole equipment
        // system when there was a single weapon slot per character.
        // SaveData.Reconcile() moves a non-empty value into
        // equipment's Weapon1 slot and clears this, so a save written
        // before the paperdoll existed keeps its sword. Nothing should read
        // or write it — ask `equipment` (or ContentDatabase.EquippedWeapon)
        // instead. Kept as a field rather than deleted because deleting it
        // would make JsonUtility drop the value on load, silently
        // unequipping every existing save.
        public string equippedItemId = "";

        // The eight-slot paperdoll: head, necklace, torso, legs, shoes,
        // gloves and two weapon hands. THE source of truth for what this
        // character is wearing.
        //
        // Worn items are moved OUT of the player's item list and held here,
        // rather than being referenced in place — so the inventory grid
        // shows exactly what is spare, and unequipping is a real move back
        // rather than a flag flip. EquipmentService owns both halves of that
        // move so the two lists can never both hold, or both lose, the same
        // item.
        public EquipmentLoadout equipment = new EquipmentLoadout();

        // PER-RUN progression, earned from combat and reset by StartRun.
        //
        // These are save fields holding run-scoped values, which is a real
        // impurity and a deliberate one. The alternative -- moving them to
        // RunState -- means threading the run through EffectiveAbilityScores'
        // ~10 call sites and every reader of `level`, where forgetting one
        // site shows unbuffed numbers on that screen and nothing anywhere
        // says so. Keeping them here means every consumer picks them up for
        // free and a mid-run quit restores them with no snapshot work, at the
        // cost of one rule that has to hold: StartRun resets all four. There
        // is a test whose whole job is to fail if it ever stops.
        //
        // Level also gates spell tiers and level-locked skills, so those are
        // per-run too: a descent starts at tier 1 and climbs.
        public int level = 1;
        public int exp;

        // Unspent points from levelling, one per level. Spent into
        // investedAbilityScores below, which is what turns a level into a
        // build decision rather than an automatic stat bump.
        public int unspentStatPoints;

        // Where those points went. Summed by
        // ContentDatabase.EffectiveAbilityScores alongside base scores and
        // talent bonuses, so it flows into AbilityDerivation and out into
        // attack/speed/health with no separate stat pathway of its own.
        public AbilityScoreBlock investedAbilityScores;

        public Character()
        {
        }

        public Character(string definitionId)
        {
            this.definitionId = definitionId;
        }

        // Places one unspent point into `score`. Returns false, changing
        // nothing, when there is nothing to spend -- callers can drive a
        // button off this without checking first.
        //
        // ONE point at a time on purpose: the UI offers a "+" per score, and
        // a bulk-invest API would need its own partial-success story (what
        // happens when you ask for 5 and can afford 3) for no gain.
        //
        // There is deliberately no matching Refund. A placed point is placed
        // for the rest of the run, which is what makes levelling a decision
        // rather than a slider -- and the run ending is already a full reset,
        // so nobody is stuck with a build forever.
        public bool Invest(AbilityScore score)
        {
            if (unspentStatPoints <= 0)
            {
                return false;
            }

            unspentStatPoints--;
            investedAbilityScores = investedAbilityScores.With(score, investedAbilityScores[score] + 1);
            return true;
        }

        // Simple placeholder curve: level * 100. Tunable later without
        // touching callers, since they only ever ask "how much to next".
        public static int ExpToNextLevel(int level)
        {
            return level * 100;
        }

        // Adds exp and applies every level-up it earns (a big enough gain
        // can cross more than one threshold at once). Returns how many
        // levels were gained, purely so callers can show a "Level Up!" -- the
        // stat point grant itself already happened by the time this returns.
        public int AddExperience(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            exp += amount;
            int levelsGained = 0;

            while (exp >= ExpToNextLevel(level))
            {
                exp -= ExpToNextLevel(level);
                level++;
                // One point per level. This used to be a talent point, back
                // when levels were the tree's currency; the tree is bought
                // with Embers now and levels buy a build instead.
                unspentStatPoints++;
                levelsGained++;
            }

            return levelsGained;
        }
    }
}
