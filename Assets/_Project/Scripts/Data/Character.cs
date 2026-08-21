using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Progression;
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

        // META progression: earned from combat and KEPT ACROSS RUNS.
        //
        // This comment used to say the opposite -- "PER-RUN progression,
        // earned from combat and reset by StartRun", closing on "one rule that
        // has to hold: StartRun resets all four. There is a test whose whole
        // job is to fail if it ever stops." None of that was true.
        // RunManager.StartRun replaces activeRun and never touches
        // save.roster; nothing anywhere assigns level = 1 or exp = 0 outside
        // this field initialiser; and there was no such test. RewardApplier's
        // own header states the real rule and always has -- "GOLD belongs to
        // the run and is lost with it, EXPERIENCE belongs to the characters
        // and survives".
        //
        // Worth knowing WHY the wrong version was dangerous rather than merely
        // wrong: it read as a load-bearing invariant with a named guard, so
        // anyone planning against it would price levels as a within-run curve
        // and design rewards that cannot exist. See AUDIT.md #49.
        //
        // The consequence, stated plainly because it is a balance fact and not
        // an implementation detail: level gates spell tiers
        // (ContentDatabase.GetSpellTierForLevel) and level-locked skills, so a
        // returning character starts their next descent at whatever tier they
        // had reached, not at tier 1.
        //
        // Living on Character rather than RunState is still the deliberate
        // choice it was, and now a cheaper one: every consumer picks these up
        // for free, and there is no snapshot work to get wrong at either end
        // of a run.
        public int level = 1;
        public int exp;

        // THIS CHARACTER'S embers, not the profile's.
        //
        // They were a single wallet figure, which meant investing in whoever
        // you actually play starved everyone else -- and worse, the reverse:
        // a character you have never fielded could be kindled to the top of
        // their tree out of a pool someone else earned. Progression is supposed
        // to reflect who you played.
        //
        // Purely additive, so CurrentVersion does not move: an older save has
        // no such field, JsonUtility leaves it at zero, and SaveData.Migrate
        // moves whatever was in the shared wallet onto the roster.
        public int embers;

        // Unspent points from levelling, one per level. Spent into
        // investedAbilityScores below, which is what turns a level into a
        // build decision rather than an automatic stat bump.
        public int unspentStatPoints;

        // Where those points went. Summed by
        // ContentDatabase.EffectiveAbilityScores alongside base scores and
        // talent bonuses, so it flows into AbilityDerivation and out into
        // attack/speed/health with no separate stat pathway of its own.
        public AbilityScoreBlock investedAbilityScores;

        // Prince's Favor earned from the reward track, ADDED TO the authored
        // CharacterDefinition.princesFavor rather than replacing it.
        //
        // The reason this field has to exist: Favor was authored-only. It is a
        // field on CharacterDefinition, which is a ScriptableObject built by
        // ContentBuilder and immutable at runtime, and the reward track grants
        // Favor at 21 of its 100 nodes -- none of which had anywhere to write.
        //
        // PER CHARACTER, which is the whole point rather than an
        // implementation detail. ItemOfferRoll.SquadFavor takes the fielded
        // party's HIGHEST Favor and never the sum, precisely so that Favor is
        // a reason to field a particular character. Earning it per character
        // keeps that true: a levelled character becomes the one you bring for
        // loot. A profile-wide pool would make it a reason to field nobody in
        // particular.
        //
        // Purely additive, so SaveData.CurrentVersion does not move: an older
        // save has no such field, JsonUtility leaves it at zero, and zero is
        // exactly "has earned none yet".
        public int earnedFavor;

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
        // There is deliberately no matching Refund, and the reason given for
        // that has to change: it was "a placed point is placed for the rest of
        // the run ... and the run ending is already a full reset, so nobody is
        // stuck with a build forever". The second half was never true. Nothing
        // resets these (see `level` above), so a placed point is placed
        // FOREVER, and the escape hatch this argument leaned on does not
        // exist.
        //
        // Left without a Refund anyway, for the half of the argument that
        // survives: a point you can take back is a slider, and levelling
        // should be a decision. The way out is a deliberate, earned respec
        // rather than an always-available undo.
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

        // What the next level costs. The curve itself is
        // Domain.Progression.LevelCurve; this stays as the name every caller
        // already asks, which is what the old comment here promised -- "tunable
        // later without touching callers, since they only ever ask how much to
        // next". Tuned, and no caller moved.
        public static int ExpToNextLevel(int level)
        {
            return LevelCurve.ExpToNextLevel(level);
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
