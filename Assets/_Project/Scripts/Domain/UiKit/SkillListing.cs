using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.UiKit
{
    // The words on a SKILLS row (plan C18): what a skill costs in the pool its
    // caster actually holds, how long it waits, who it aims at and from where.
    //
    // Built from the ResolvedSkill's own fields, like the fight's meta line, so
    // the row cannot promise a cost the cast then does not charge. The pool
    // names come in as arguments because they are content (pools.json and the
    // character's signatureDisplayName), and this is Domain.
    public static class SkillListing
    {
        public static UiString HeaderFor(SkillSource source)
        {
            switch (source)
            {
                case SkillSource.Innate: return UiStrings.SkillsGroupInnate;
                case SkillSource.Talent: return UiStrings.SkillsGroupTalent;
                case SkillSource.SpellBook: return UiStrings.SkillsGroupSpellBook;
                case SkillSource.RewardTrack: return UiStrings.SkillsGroupRewardTrack;
                case SkillSource.Taught: return UiStrings.SkillsGroupTaught;
                default: throw new System.ArgumentOutOfRangeException(nameof(source), source,
                    "SkillSource has no group header -- add a case above.");
            }
        }

        // "6 Fury", "3 Wool", "6 Mana + 3 Wool", "20% HP", or "Free". A skill
        // that spends a share of the pool the player chooses (poolTiers) says
        // which pool rather than a number it does not have.
        public static string CostText(ResolvedSkill skill, string primaryPoolName, string signatureName)
        {
            var parts = new List<string>();

            if (skill.ManaCost > 0)
            {
                parts.Add(UiStrings.SkillCostAmount.Format(skill.ManaCost, primaryPoolName));
            }
            else if (skill.HasPoolTiers)
            {
                parts.Add(UiStrings.SkillCostSpends.Format(primaryPoolName));
            }

            if (skill.SpendsAllResource)
            {
                parts.Add(UiStrings.SkillCostAll.Format(signatureName));
            }
            else if (skill.ResourceCost > 0)
            {
                parts.Add(UiStrings.SkillCostAmount.Format(skill.ResourceCost, signatureName));
            }

            if (skill.HealthCostPercent > 0)
            {
                parts.Add(UiStrings.SkillCostHealth.Format(skill.HealthCostPercent));
            }

            if (parts.Count == 0) return UiStrings.SkillCostFree.Format();

            string text = parts[0];
            for (int i = 1; i < parts.Count; i++) text = UiStrings.SkillCostJoin.Format(text, parts[i]);
            return text;
        }

        // "" for a skill with no wait.
        public static string CooldownText(ResolvedSkill skill)
        {
            if (skill.OncePerFight) return UiStrings.SkillOncePerFight.Format();
            return skill.CooldownTurns > 0 ? UiStrings.SkillCooldown.Format(skill.CooldownTurns) : "";
        }

        public static string TargetText(SkillTargeting targeting)
        {
            switch (targeting)
            {
                case SkillTargeting.SingleEnemy: return UiStrings.SkillTargetEnemy.Format();
                case SkillTargeting.AllEnemies: return UiStrings.SkillTargetEnemies.Format();
                case SkillTargeting.Self: return UiStrings.SkillTargetSelf.Format();
                case SkillTargeting.Party: return UiStrings.SkillTargetParty.Format();
                case SkillTargeting.SingleAlly: return UiStrings.SkillTargetAlly.Format();
                default: throw new System.ArgumentOutOfRangeException(nameof(targeting), targeting,
                    "SkillTargeting has no words -- add a case above.");
            }
        }

        // Where a single-enemy blow can land: the front rank alone for melee,
        // the named ranks (one-based, as a player counts them) for an authored
        // list. "" when it reaches anywhere, and for every other targeting,
        // where rank is not a question.
        public static string ReachText(ResolvedSkill skill)
        {
            if (skill.Targeting != SkillTargeting.SingleEnemy) return "";

            switch (skill.Reach.Kind)
            {
                case ReachKind.Melee:
                    return UiStrings.SkillReachFront.Format();

                case ReachKind.ExplicitRanks:
                    var ranks = new List<string>();
                    for (int rank = 0; rank < Reach.MaxRanks; rank++)
                    {
                        if (skill.Reach.Allows(rank)) ranks.Add((rank + 1).ToString());
                    }

                    return ranks.Count == 0 ? "" : UiStrings.SkillReachRanks.Format(string.Join(", ", ranks));

                default:
                    return "";
            }
        }

        // The whole right-hand line: cost, wait, target, reach, the empty ones
        // left out.
        public static string MetaText(ResolvedSkill skill, string primaryPoolName, string signatureName)
        {
            string text = CostText(skill, primaryPoolName, signatureName);

            foreach (string part in new[] { CooldownText(skill), TargetText(skill.Targeting), ReachText(skill) })
            {
                if (!string.IsNullOrEmpty(part)) text = UiStrings.SkillMetaJoin.Format(text, part);
            }

            return text;
        }
    }
}
