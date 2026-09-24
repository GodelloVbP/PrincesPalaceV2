using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // ONE THING A MONSTER CAN DO, and a weight saying how often it does it.
    //
    // A monster used to have exactly two actions: its basic attack, and ONE
    // "skill" described by three loose scalars -- a name string, a multiplier
    // on that same basic attack, and a chance. So every ability in the game,
    // on every monster, resolved to a single line:
    //
    //     damage = Max(1, Round(damage * SkillPower));
    //
    // A boss and a rat differed by a number. Nothing could heal its own side,
    // shield an ally, hit the whole party, or apply a status that belonged to
    // the ability rather than to the monster.
    //
    // An ability is a REAL ResolvedSkill now -- the same type a player's kit
    // carries, resolved through the same content path -- so the entire
    // SkillEffect vocabulary is available to monsters and every skill written
    // for a character in future is available to them for free.
    public readonly struct EnemyAbility
    {
        // The skill itself, and null on a legacy attack.
        //
        // NEVER READ WITHOUT ASKING HasSkill FIRST. "No skill" is deliberately
        // not spelled as an empty-Id instance: an unset field is what an
        // uninitialised array slot already holds, so reading that as "this one
        // is the legacy attack" would make a wiring bug indistinguishable from
        // an authored choice. HasSkill says it outright, and both live readers
        // (FightSession.Enemies' summon gate and its cast branch) check it.
        public readonly ResolvedSkill Skill;

        // False means THE LEGACY SCALED ATTACK: a monster authored with the old
        // skillName/skillPower trio and no abilities list.
        public readonly bool HasSkill;

        // Relative likelihood, not a probability -- weights do not have to sum
        // to anything. A weight of 2 against a weight of 1 is twice as likely,
        // which is the thing an author actually wants to say and needs no
        // arithmetic to keep true when a third ability is added.
        public readonly float Weight;

        // The legacy multiplier, and only meaningful when Skill is null.
        public readonly float Power;

        // What the telegraph calls it.
        public readonly string Label;

        private EnemyAbility(ResolvedSkill skill, bool hasSkill, float weight, string label, float power)
        {
            Skill = skill;
            HasSkill = hasSkill;
            Weight = weight <= 0f ? 0f : weight;
            Power = power;
            Label = label ?? "";
        }

        public static EnemyAbility Of(ResolvedSkill skill, float weight) =>
            new EnemyAbility(skill, true, weight, skill.DisplayName, 1f);

        // The old trio, expressed as an ability so there is ONE list and one
        // draw rather than a branch for monsters authored before this existed.
        public static EnemyAbility LegacyAttack(string label, float power, float weight) =>
            new EnemyAbility(default, false, weight, label, power);

        public bool IsLegacyAttack => !HasSkill;

        // The entry every pool carries: the monster's own basic attack and
        // nothing else. Identified by its label (FightSession.IntentAttack)
        // rather than by Power == 1f: a legacy skill authored at power 1.0
        // wears the same numeric shape as the plain swing but is still an
        // authored ability with its own telegraph, and Power-based detection
        // used to swallow it into the swing's plain "Attack" intent. Same
        // rule EnemyShowcase used privately for exactly this reason before
        // the two were unified onto this one property.
        public bool IsPlainSwing => IsLegacyAttack && Label == FightSession.IntentAttack;

        // WHETHER ROOTED FORBIDS THIS ENTRY (plan 1.10). An authored ability
        // answers from its own content row; a legacy attack is a swing and is
        // physical by construction.
        //
        // IsLegacyAttack, NOT IsPlainSwing, and the widening is deliberate. A
        // legacy scaled attack is the same body doing the same thing harder --
        // there is no reading on which a root stops the swing but not the
        // shoulder-charge version of it. Nothing in enemies.json authors one
        // today (every monster either carries an abilities list or nothing at
        // all), so this changes no live fight; it stops the rule being wrong
        // the first time one is authored.
        public bool IsPhysicalMove => HasSkill
            ? CombatActions.IsPhysicalMove(Skill)
            : CombatActions.PlainAttackIsPhysicalMove;
    }

    public static class EnemyAbilityDraw
    {
        // WEIGHTED RANDOM, over a list whose weights are relative.
        //
        // ONE DRAW, whatever the list length. That matters more than it looks:
        // this is a seeded game, the intent is committed on the player's turn,
        // and a selection that consumed a variable number of draws would make
        // the whole rest of the run depend on how many abilities a monster
        // happened to have. Rejection sampling would have been simpler to write
        // and is exactly that bug.
        //
        // Returns -1 when there is nothing to pick -- an empty list, or every
        // weight at zero, which is a monster authored as able to do nothing.
        // The caller falls back to a basic attack rather than skipping a turn,
        // because a monster standing still for a content mistake reads as the
        // fight being broken.
        public static int Pick(IReadOnlyList<EnemyAbility> abilities, float roll)
        {
            if (abilities == null || abilities.Count == 0) return -1;

            float total = 0f;
            for (int i = 0; i < abilities.Count; i++)
            {
                total += Math.Max(0f, abilities[i].Weight);
            }

            if (total <= 0f) return -1;

            // Clamped rather than trusted. A roll of exactly 1 -- which a
            // generator is allowed to return -- would otherwise walk off the
            // end of the list and pick nothing from a non-empty list.
            float at = Math.Min(Math.Max(roll, 0f), 0.9999999f) * total;

            for (int i = 0; i < abilities.Count; i++)
            {
                at -= Math.Max(0f, abilities[i].Weight);
                if (at < 0f) return i;
            }

            // Only reachable through float accumulation error at the very top
            // of the range. The last entry is the honest answer there.
            return abilities.Count - 1;
        }
    }
}
