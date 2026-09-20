using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Content
{
    // Validates skills.json. Same collected-not-first-only error reporting as
    // EnemyEntryResolver and SpellTierEntryResolver, for the same reason: a
    // hand-edited file should say everything wrong with it in one pass rather
    // than making the author fix and rebuild once per typo.
    //
    // The interesting rules are the cross-field ones. A skill that costs
    // nothing and does nothing is an authoring mistake worth naming, and a
    // heal that scales off a resource it never spends would silently always
    // scale by zero.
    public static class SkillEntryResolver
    {
        private const int DefaultUnlockLevel = 1;

        // THE ONLY TWO unlockLevel VALUES a non-book skill may author -- see
        // the check in TryResolveOne for what each one means and why there
        // is nothing in between. Public so ContentDatabase.Validation can
        // mirror the rule against the LOADED catalogue by reading the same
        // two numbers rather than retyping them.
        public const int StartingKitUnlockLevel = 1;
        public const int GrantedElsewhereUnlockLevel = 999;
        private const int DefaultManaCost = 0;
        private const int DefaultResourceCost = 0;
        private const int DefaultPower = 0;
        private const int DefaultFlatAmount = 0;

        // The catalogue-blind overload, for the fixtures and for anything that
        // only wants skills.json checked against itself. Nobody refuses books.
        public static bool TryResolveAll(IReadOnlyList<RawSkillEntry> entries, out List<ResolvedSkill> resolved, out List<string> errors) =>
            TryResolveAll(entries, null, null, out resolved, out errors);

        // THE ONE CROSS-CATALOGUE FACT THIS RESOLVER IS HANDED (plan P6,
        // gate 5): the owner ids whose primary pool refuses spell books,
        // computed by ContentBuilder from the pool and character catalogues it
        // has already built (pools, then characters, then skills -- see its
        // own ordering note). A bookOnly skill authored against one of them
        // can never be learned by anybody, because the only route to it is a
        // book its owner cannot hold; it is dead content that still costs a
        // shop slot and a drop roll.
        //
        // AN EXTRA PARAMETER RATHER THAN A REQUIRED ONE, unlike
        // CharacterEntryResolver's poolIds. That one guards a field every
        // character row authors, so a caller forgetting it would skip a live
        // check; this one has a single production caller and twenty-eight
        // fixtures whose skills name owners no character catalogue contains at
        // all. Null or empty means "nothing refuses books", which is the
        // shipped catalogue's own answer today.
        public static bool TryResolveAll(IReadOnlyList<RawSkillEntry> entries,
                                         IReadOnlyCollection<string> bookRefusingOwnerIds,
                                         out List<ResolvedSkill> resolved, out List<string> errors) =>
            TryResolveAll(entries, bookRefusingOwnerIds, null, out resolved, out errors);

        // THE SECOND CROSS-CATALOGUE FACT, threaded the same way and for the
        // same reason: the owner ids whose primary pool authors
        // `startRule: Zero`, i.e. who open every fight holding nothing. It is
        // what the free-skill refusal below needs to tell an authoring slip
        // from a design (see that rule's own comment).
        //
        // Null or empty means "everybody opens with something to spend",
        // which was the whole catalogue's answer until Bjorn's `fury` row.
        public static bool TryResolveAll(IReadOnlyList<RawSkillEntry> entries,
                                         IReadOnlyCollection<string> bookRefusingOwnerIds,
                                         IReadOnlyCollection<string> zeroStartPoolOwnerIds,
                                         out List<ResolvedSkill> resolved, out List<string> errors) =>
            TryResolveAll(entries, bookRefusingOwnerIds, zeroStartPoolOwnerIds, null, out resolved, out errors);

        // THE THIRD CROSS-CATALOGUE FACT, threaded the same way as the two
        // above and for the same reason: the owner ids a poolTiers skill may
        // be authored against, i.e. every id PoolOwnership.PrimaryPoolOwners
        // recognises as a real character (see that method's own header for
        // what "recognises" excludes). Null or empty means "nothing refuses
        // poolTiers on cross-catalogue grounds", same convention as the other
        // two -- a fixture resolving skills.json alone still gets every
        // WITHIN-FILE poolTiers rule (TryResolvePoolTiers), just not this one.
        public static bool TryResolveAll(IReadOnlyList<RawSkillEntry> entries,
                                         IReadOnlyCollection<string> bookRefusingOwnerIds,
                                         IReadOnlyCollection<string> zeroStartPoolOwnerIds,
                                         IReadOnlyCollection<string> primaryPoolOwnerIds,
                                         out List<ResolvedSkill> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedSkill>();
            errors = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                if (TryResolveOne(entries[i], i, resolved.Count, zeroStartPoolOwnerIds, out var single, out string error))
                {
                    resolved.Add(single);
                }
                else
                {
                    errors.Add(error);
                }
            }

            if (bookRefusingOwnerIds != null && bookRefusingOwnerIds.Count > 0)
            {
                foreach (var skill in resolved.Where(s => s.BookOnly && bookRefusingOwnerIds.Contains(s.CharacterId)))
                {
                    errors.Add($"skill '{skill.Id}': bookOnly is set and it belongs to '{skill.CharacterId}', " +
                               "whose primary pool refuses spell books — the only way to reach a book-only skill " +
                               "is to learn the book, and that character can never hold one. Give it an owner who " +
                               "can carry books, or drop bookOnly and author an unlockLevel.");
                }
            }

            if (primaryPoolOwnerIds != null && primaryPoolOwnerIds.Count > 0)
            {
                foreach (var skill in resolved.Where(s => s.HasPoolTiers && !primaryPoolOwnerIds.Contains(s.CharacterId)))
                {
                    errors.Add($"skill '{skill.Id}': poolTiers is authored but '{skill.CharacterId}' is not a " +
                               "character with a primary pool to spend from — poolTiers spends the OWNER'S primary " +
                               "pool (Fury, for Bjorn), not a signature resource, and a monster or unknown owner " +
                               "has none.");
                }
            }

            foreach (string duplicateId in resolved.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate skill id '{duplicateId}' — every id must be unique.");
            }

            // Several skills MAY share an unlock level. An earlier version of
            // this resolver rejected that as "almost always a typo", which was
            // wrong: a character starting with more than one spell is an
            // ordinary design, and the rule blocked it outright the first time
            // anyone tried. Duplicate IDS are still caught above, which is the
            // mistake actually worth refusing.

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            resolved = resolved.OrderBy(s => s.CharacterId).ThenBy(s => s.UnlockLevel).ToList();
            return true;
        }

        private static bool TryResolveOne(RawSkillEntry raw, int index, int sortOrder,
                                          IReadOnlyCollection<string> zeroStartPoolOwnerIds,
                                          out ResolvedSkill resolvedSkill, out string error)
        {
            resolvedSkill = default;
            string label = string.IsNullOrEmpty(raw.id) ? $"skills.json entry #{index + 1}" : $"skill '{raw.id}'";

            if (string.IsNullOrWhiteSpace(raw.id))
            {
                error = $"{label}: id is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.displayName))
            {
                error = $"{label}: displayName is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.characterId))
            {
                error = $"{label}: characterId is required — a skill with no owner would be offered to the whole roster.";
                return false;
            }

            // PHASE 3: a placeholder skill is a stand-in, not an ability --
            // it may author no effect field at all, and must say why it is
            // undesigned. Checked before any of those fields are resolved,
            // against the RAW entry rather than the resolved defaults below
            // (effect="" silently resolves to DamageSingle a few lines down,
            // which would make "no effect fields authored" unreadable off
            // the resolved shape).
            if (raw.placeholder)
            {
                if (string.IsNullOrWhiteSpace(raw.placeholderNote))
                {
                    error = $"{label}: placeholder is set but placeholderNote is empty — say why this skill exists undesigned.";
                    return false;
                }

                bool authorsAnEffectField = !string.IsNullOrWhiteSpace(raw.effect)
                    || raw.manaCost >= 0 || raw.resourceCost >= 0 || raw.spendsAllResource
                    || raw.power >= 0 || raw.flatAmount >= 0 || raw.ignoresDefense
                    || (raw.damageInstances != null && raw.damageInstances.Length > 0)
                    || !string.IsNullOrWhiteSpace(raw.appliesStatus) || raw.statusMagnitude >= 0 || raw.statusDuration >= 0
                    || (raw.elements != null && raw.elements.Length > 0)
                    || (raw.poolTiers != null && raw.poolTiers.Length > 0)
                    // PHASE 4's four. Listed here rather than left out
                    // because the whole value of this check is that the list
                    // is complete: a field missing from it is an effect a
                    // stand-in can quietly author.
                    || raw.resourceSpendCap != 0 || raw.spendsAllPrimary
                    || raw.percentOfMaxHealthPerPoint != 0 || raw.freeAction
                    // AND THE SHIELD MODEL'S TWO, for the same reason.
                    || raw.percentOfCasterMaxHealth != 0 || raw.wardTurns != 0
                    // AND MILESTONE B'S SIX, same reason again: a stand-in
                    // may not quietly author a health cost, a board-state
                    // requirement, a consumed status, or a detonation split.
                    || raw.healthCostPercent != 0 || !string.IsNullOrWhiteSpace(raw.requiresStatus)
                    || !string.IsNullOrWhiteSpace(raw.consumesStatus)
                    || (raw.damageInstancesIfConsumed != null && raw.damageInstancesIfConsumed.Length > 0)
                    || raw.detonationPercent != 0
                    || (raw.detonationSplit != null && raw.detonationSplit.Length > 0)
                    // AND MILESTONE C'S ONE, same reason once more: a
                    // stand-in may not quietly author a turn-order advance.
                    || raw.advanceSlots != 0;

                if (authorsAnEffectField)
                {
                    error = $"{label}: placeholder skills may not author any effect field — this is a stand-in, not an implemented ability.";
                    return false;
                }
            }

            var effect = SkillEffect.DamageSingle;
            if (!string.IsNullOrWhiteSpace(raw.effect) && !Enum.TryParse(raw.effect, ignoreCase: true, out effect))
            {
                error = $"{label}: effect '{raw.effect}' isn't valid. Valid options: {string.Join(", ", Enum.GetNames(typeof(SkillEffect)))}.";
                return false;
            }

            var targeting = DefaultTargetingFor(effect);
            if (!string.IsNullOrWhiteSpace(raw.targeting) && !Enum.TryParse(raw.targeting, ignoreCase: true, out targeting))
            {
                error = $"{label}: targeting '{raw.targeting}' isn't valid. Valid options: {string.Join(", ", Enum.GetNames(typeof(SkillTargeting)))}.";
                return false;
            }

            // BOOK-ONLY SKILLS DO NOT DEFAULT TO unlockLevel 1 (docs/
            // PLAN_SHOP.md §1a point 1). Simply omitting unlockLevel would
            // resolve to DefaultUnlockLevel below and hand a book-only spell
            // to a level-1 character by the ordinary level route -- the
            // exact opposite of the intent, silently. int.MaxValue is what
            // keeps it off the ladder, and AvailableSkillsFor.OrderBy(level)
            // reads that as "sorts last", which is the right place for a
            // learned spell to sit behind an authored kit.
            int unlockLevel;
            if (raw.bookOnly)
            {
                if (raw.unlockLevel >= 0)
                {
                    error = $"{label}: bookOnly and unlockLevel cannot both be authored — a book-only skill " +
                            "is reached by being learned, never by levelling. Remove unlockLevel.";
                    return false;
                }

                unlockLevel = int.MaxValue;
            }
            else
            {
                unlockLevel = raw.unlockLevel >= 0 ? raw.unlockLevel : DefaultUnlockLevel;

                // ONE WAY TO LEARN A SKILL, AND unlockLevel IS NO LONGER IT
                // (docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §1,
                // contract 6: "One way to learn a skill: the track. The
                // unlockLevel ladder is removed").
                //
                // Two values are left and they are not two halves of a
                // ladder, they are two different statements:
                //
                //   1   -- the starting kit. This character HAS this from the
                //          moment they exist, and every monster's whole
                //          ability list is this too.
                //   999 -- unreachable on purpose. Something else hands this
                //          over: a track's UnlockSkill node, a talent's
                //          grantsSkillId. The number is a marker, not a
                //          level, which is why it is not MaxLevel + 1 -- it
                //          must stay unreachable however far the cap moves.
                //
                // Anything between them is the ladder, and a ladder plus a
                // track is two systems handing out the same skill at two
                // different moments with no rule about which wins. Refused
                // with the skill named, rather than resolved and left to be
                // discovered when a level-6 character gets Battering Ram the
                // track had not paid out yet.
                if (unlockLevel != StartingKitUnlockLevel && unlockLevel != GrantedElsewhereUnlockLevel)
                {
                    error = $"{label}: unlockLevel is {unlockLevel}. A skill is either part of the starting kit " +
                            $"({StartingKitUnlockLevel}) or handed over by a reward track or a talent " +
                            $"({GrantedElsewhereUnlockLevel}) -- the levelling ladder in between is gone. " +
                            "Put the skill on the character's track instead.";
                    return false;
                }
            }

            if (raw.bookTier < 0)
            {
                error = $"{label}: bookTier cannot be negative (got {raw.bookTier}). 0 means this skill is not " +
                        "book-eligible at all.";
                return false;
            }

            int manaCost = raw.manaCost >= 0 ? raw.manaCost : DefaultManaCost;
            int resourceCost = raw.resourceCost >= 0 ? raw.resourceCost : DefaultResourceCost;
            int power = raw.power >= 0 ? raw.power : DefaultPower;
            int flatAmount = raw.flatAmount >= 0 ? raw.flatAmount : DefaultFlatAmount;

            // A free skill that DOES something to a health bar is strictly
            // better than every other action and would simply be spammed.
            //
            // A free skill that only rearranges the board is not, and the
            // rule used to catch it anyway. Provoke's whole cost is the turn:
            // it deals no damage, heals nobody, and hands the enemy team a
            // free swing at you in exchange for choosing who swings. Charging
            // wool for that on top would make the Black Ram's own income
            // engine cost wool to switch on, which is the opposite of what
            // the strand is for (handoff §6.2).
            bool touchesHealthOrMana = effect != SkillEffect.Provoke;

            // AND ONLY WHEN THE OWNER HAS SOMETHING TO SPEND ON TURN ONE. A
            // pool authored `startRule: Zero` opens every fight at nothing, so
            // a character holding one who owns no free action cannot act at
            // all on the turn the fight starts -- not "acts badly", cannot
            // act. Charging for the opening move of a resource that is earned
            // by moving is a circular price, and the rule above cannot see it
            // because it only ever looked at the skill.
            //
            // THIS IS NOT A HOLE WAITING FOR BJORN'S REAL PRICES. Fury costs
            // are unauthored today (his slam and brace are manaCost 0 in
            // skills.json, pending the balance pass that authors them beside
            // the gain numbers), and it would be fair to read this carve-out
            // as interim scaffolding for that. It is not: even once every
            // Fury cost is authored, SOMETHING in his kit has to be free or
            // he opens every fight passing, so a Zero-start pool's first turn
            // is a design fact rather than a placeholder. The carve-out stays.
            //
            // AND ONLY WHEN SOMEBODY CHOOSES IT. The whole argument above is
            // about a player weighing this action against another one; a
            // monster's abilities are drawn by weight and it has no mana or
            // wool to spend either way. See RawSkillEntry.playerSelectable.
            //
            // AND NEVER FOR A PLACEHOLDER -- PlayerSelectable is FORCED false
            // for one regardless of what raw.playerSelectable says (see the
            // constructor call below), so the argument above already applies;
            // checking raw.playerSelectable here would still catch a
            // placeholder authored with the field left at its default true,
            // which is exactly what "no effect fields authored" is supposed
            // to leave alone.
            bool ownerOpensEmpty = zeroStartPoolOwnerIds != null
                                   && zeroStartPoolOwnerIds.Contains(raw.characterId);

            if (raw.playerSelectable && !raw.placeholder && manaCost == 0 && resourceCost == 0 && touchesHealthOrMana && !ownerOpensEmpty)
            {
                error = $"{label}: a skill that costs neither mana nor resource is strictly better than every other action " +
                        "and would simply be spammed. Give it a cost.";
                return false;
            }

            var scalingAxis = ScalingAxis.Auto;
            if (!string.IsNullOrWhiteSpace(raw.scalingAxis) && !Enum.TryParse(raw.scalingAxis, ignoreCase: true, out scalingAxis))
            {
                error = $"{label}: scalingAxis '{raw.scalingAxis}' isn't valid. Valid options: {string.Join(", ", Enum.GetNames(typeof(ScalingAxis)))}.";
                return false;
            }

            // A heal or a mana restore has no Attack to build on, so with
            // neither a flat amount nor any scaling it restores exactly
            // nothing and the button does visibly nothing when pressed.
            //
            // HealSingle joins the three, with two more ways to be worth
            // casting: an authored scaling axis (Mend's "20 plus her spell
            // attack" would still be worth casting at flatAmount 0) and a
            // per-point percentage of max health (Second Wind's whole
            // figure).
            bool isRestorative = effect == SkillEffect.HealSelf
                                 || effect == SkillEffect.HealParty
                                 || effect == SkillEffect.HealSingle
                                 || effect == SkillEffect.RestorePartyMana;
            bool ridesAnAuthoredAxis = scalingAxis == ScalingAxis.Weapon || scalingAxis == ScalingAxis.Spell;
            if (isRestorative && flatAmount == 0 && power == 0 && raw.percentOfMaxHealthPerPoint == 0
                && !ridesAnAuthoredAxis)
            {
                error = $"{label}: a {effect} skill with no flatAmount, no power, no percentOfMaxHealthPerPoint " +
                        "and no authored scalingAxis restores nothing.";
                return false;
            }

            // Scaling per point spent, on a skill that never spends any, is
            // always zero — a silent no-op the author will not notice.
            // WHAT power AND percentOfMaxHealthPerPoint ACTUALLY SCALE OFF:
            // the pool the cast EMPTIES, which is the signature resource
            // ordinarily and the primary pool only for a spendsAllPrimary
            // skill (FightSession.PointsSpent). A plain manaCost does NOT
            // count -- an ordinary mana cost is a price, not a hoard, and
            // nothing feeds it into the per-point term.
            bool spendsSomethingPerPoint = resourceCost > 0 || raw.spendsAllResource || raw.spendsAllPrimary;
            if (power > 0 && !spendsSomethingPerPoint)
            {
                error = $"{label}: power scales per point of resource spent, but this skill spends none. " +
                        "Set resourceCost, or set spendsAllResource, or use flatAmount instead.";
                return false;
            }

            // ---- phase 4's four authored fields --------------------------
            //
            // Each refusal is the same shape as the two above: a field that
            // would resolve cleanly and then be read by nothing.

            if (raw.resourceSpendCap != 0 && !raw.spendsAllResource)
            {
                error = $"{label}: resourceSpendCap caps how much a spendsAllResource cast takes, and this skill " +
                        "does not spend all. Drop the cap, or set spendsAllResource.";
                return false;
            }

            if (raw.resourceSpendCap < 0)
            {
                error = $"{label}: resourceSpendCap cannot be negative (got {raw.resourceSpendCap}). 0 means no cap.";
                return false;
            }

            // The cap is a CEILING and resourceCost is the FLOOR, so a cap
            // under the cost is a skill that can never pay for itself.
            if (raw.resourceSpendCap > 0 && raw.resourceSpendCap < resourceCost)
            {
                error = $"{label}: resourceSpendCap {raw.resourceSpendCap} is below resourceCost {resourceCost}, " +
                        "so the cast could never spend what it costs.";
                return false;
            }

            // manaCost is the MINIMUM a spendsAllPrimary cast needs, the same
            // way resourceCost is for spendsAllResource. At 0 there is no
            // minimum, which makes "spends all" indistinguishable from "spend
            // whatever happens to be there, including nothing".
            if (raw.spendsAllPrimary && manaCost <= 0)
            {
                error = $"{label}: spendsAllPrimary needs a manaCost as its minimum -- a capstone that fires on an " +
                        "empty pool for nothing is the case this flag exists to prevent.";
                return false;
            }

            if (raw.percentOfMaxHealthPerPoint < 0)
            {
                error = $"{label}: percentOfMaxHealthPerPoint cannot be negative (got {raw.percentOfMaxHealthPerPoint}).";
                return false;
            }

            if (raw.percentOfMaxHealthPerPoint > 0 && !spendsSomethingPerPoint)
            {
                error = $"{label}: percentOfMaxHealthPerPoint is paid per point of the pool spent, but this skill " +
                        "spends none.";
                return false;
            }

            bool healsSomebody = effect == SkillEffect.HealSelf || effect == SkillEffect.HealParty
                                 || effect == SkillEffect.HealSingle;
            if (raw.percentOfMaxHealthPerPoint > 0 && !healsSomebody)
            {
                error = $"{label}: percentOfMaxHealthPerPoint is only read by a heal, not by {effect}.";
                return false;
            }

            // ---- the shield model's two authored fields ------------------
            //
            // Same shape of refusal again: a field that would resolve
            // cleanly and then be read by nothing.

            if (raw.percentOfCasterMaxHealth < 0)
            {
                error = $"{label}: percentOfCasterMaxHealth cannot be negative (got {raw.percentOfCasterMaxHealth}).";
                return false;
            }

            if (raw.percentOfCasterMaxHealth > 0 && effect != SkillEffect.Ward)
            {
                error = $"{label}: percentOfCasterMaxHealth is only read by a Ward, not by {effect}. " +
                        "A heal that wants a share of the caster's bar uses percentOfMaxHealthPerPoint.";
                return false;
            }

            if (raw.wardTurns < 0)
            {
                error = $"{label}: wardTurns cannot be negative (got {raw.wardTurns}). 0 means the default of one.";
                return false;
            }

            if (raw.wardTurns != 0 && effect != SkillEffect.Ward)
            {
                error = $"{label}: wardTurns is how long a Ward stands, and this skill is a {effect}.";
                return false;
            }

            // A WARD WITH NO SIZE IS A CAST THAT DOES NOTHING, and it is the
            // exact failure the old model could hide: every ward's strength
            // used to be able to come from the caster's WardReductionPercent
            // talent instead of from the row, so a row authoring no number at
            // all was legal. The talent is a MULTIPLIER now
            // (SkillResolution.Amount's Ward case), and a multiplier on
            // nothing is nothing.
            if (effect == SkillEffect.Ward && flatAmount == 0 && power == 0
                && raw.percentOfCasterMaxHealth == 0 && !ridesAnAuthoredAxis)
            {
                error = $"{label}: a Ward with no flatAmount, no power, no percentOfCasterMaxHealth and no " +
                        "authored scalingAxis puts up a shield of nothing.";
                return false;
            }

            // A free action is a choice the PLAYER gets to make twice in one
            // turn. A monster's abilities are drawn one per turn by a
            // weighted pool that has no concept of acting again, so the flag
            // would resolve and then be read by nothing.
            if (raw.freeAction && !raw.playerSelectable)
            {
                error = $"{label}: freeAction only means anything for a skill a player presses -- a monster's " +
                        "turn is one drawn ability, not a sequence it can extend.";
                return false;
            }

            // A party buff with nothing to apply is a cast that visibly does
            // nothing — the same "authored field set is incomplete" rule
            // appliesStatus/statusMagnitude already follow, from the other
            // side.
            if (effect == SkillEffect.BuffParty && string.IsNullOrWhiteSpace(raw.appliesStatus))
            {
                error = $"{label}: a BuffParty skill needs an appliesStatus — the status IS the whole effect.";
                return false;
            }

            if (raw.ignoresDefense && !SkillEffects.IsDamagePipeline(effect))
            {
                error = $"{label}: ignoresDefense only means anything for a damage effect, not {effect}.";
                return false;
            }

            // ---- milestone B: the health cost -----------------------------
            //
            // A PAYMENT, not damage (plan 1.2), so it is validated on its own
            // terms rather than folded into the damage checks above. 100 or
            // more could never leave 1 HP for a caster whose own cost equals
            // or exceeds their bar, which is not a cast this game can pay for
            // under any board state -- refused as an authoring mistake rather
            // than left to refuse itself silently at every single cast.
            if (raw.healthCostPercent < 0 || raw.healthCostPercent >= 100)
            {
                error = $"{label}: healthCostPercent is {raw.healthCostPercent} — it must be 0 (no cost) or a " +
                        "positive percent under 100, since 100 or more could never leave the caster 1 HP.";
                return false;
            }

            if (!TryResolveDamageInstances(raw, label, isRestorative, out var instances, out error))
            {
                return false;
            }

            // ---- milestone B: requires/consumes status and the Reclaim split
            //
            // Three related but independent authoring facts, checked here
            // (after damageInstances has resolved, so the checks below can
            // read `instances` rather than the raw array a second time).

            if (!TryParseOptionalStatus(raw.requiresStatus, label, "requiresStatus", out var requiresStatus, out error))
            {
                return false;
            }

            if (requiresStatus.HasValue && targeting != SkillTargeting.SingleEnemy)
            {
                error = $"{label}: requiresStatus only means anything on a SingleEnemy skill, not {targeting} — " +
                        "there is no single target to ask the question about.";
                return false;
            }

            if (!TryParseOptionalStatus(raw.consumesStatus, label, "consumesStatus", out var consumesStatus, out error))
            {
                return false;
            }

            bool authorsConsumedPackets = raw.damageInstancesIfConsumed != null && raw.damageInstancesIfConsumed.Length > 0;
            if (consumesStatus.HasValue != authorsConsumedPackets)
            {
                error = $"{label}: consumesStatus and damageInstancesIfConsumed must be authored together — " +
                        "one names what is spent, the other what a landed hit deals once it is.";
                return false;
            }

            DamageInstance[] instancesIfConsumed = Array.Empty<DamageInstance>();
            if (consumesStatus.HasValue)
            {
                if (effect != SkillEffect.DamageSingle || instances.Length == 0)
                {
                    error = $"{label}: consumesStatus only means anything on a DamageSingle skill that also " +
                            "authors an ordinary damageInstances to fall back to when the target does not carry it.";
                    return false;
                }

                if (!TryResolveDamagePackets(raw.damageInstancesIfConsumed, label, "damageInstancesIfConsumed",
                        out instancesIfConsumed, out error))
                {
                    return false;
                }
            }

            bool authorsDetonationSplit = raw.detonationSplit != null && raw.detonationSplit.Length > 0;
            if ((raw.detonationPercent > 0) != authorsDetonationSplit)
            {
                error = $"{label}: detonationPercent and detonationSplit must be authored together — " +
                        "one is the markup, the other is what the marked-up total is divided across.";
                return false;
            }

            DamageType[] detonationSplit = Array.Empty<DamageType>();
            if (raw.detonationPercent > 0)
            {
                if (effect != SkillEffect.Reclaim)
                {
                    error = $"{label}: detonationPercent/detonationSplit only mean anything on a Reclaim skill, " +
                            $"not {effect} — every other damage effect deals a packet the ordinary way.";
                    return false;
                }

                var splitTypes = new DamageType[raw.detonationSplit.Length];
                for (int i = 0; i < raw.detonationSplit.Length; i++)
                {
                    if (!TryParseDamageType(raw.detonationSplit[i], out splitTypes[i]))
                    {
                        error = $"{label}: detonationSplit entry #{i + 1} has an unknown type " +
                                $"'{raw.detonationSplit[i]}'. Valid options: " +
                                $"{string.Join(", ", System.Enum.GetNames(typeof(DamageType)))}, Frost.";
                        return false;
                    }
                }

                detonationSplit = splitTypes;
            }

            if (effect == SkillEffect.Reclaim)
            {
                if (raw.detonationPercent <= 0)
                {
                    error = $"{label}: a Reclaim skill needs a positive detonationPercent — its damage IS the " +
                            "consumed total marked up, and there is no sensible default for that.";
                    return false;
                }

                if (!requiresStatus.HasValue)
                {
                    error = $"{label}: a Reclaim skill needs a requiresStatus — casting it against a target " +
                            "carrying nothing would detonate nothing, and that is a refusal, not a resolution.";
                    return false;
                }

                if (instances.Length > 0)
                {
                    error = $"{label}: a Reclaim skill authors no damageInstances of its own — its packets are " +
                            "built at resolution from the consumed total, not from the row.";
                    return false;
                }

                if (raw.power > 0 || raw.flatAmount > 0)
                {
                    error = $"{label}: a Reclaim skill's damage is the consumed total marked up once — power and " +
                            "flatAmount are never read by it.";
                    return false;
                }
            }

            if (!TryResolveStatus(raw, label, out var appliesStatus, out int statusMagnitude, out int statusDuration, out error))
            {
                return false;
            }

            if (!TryResolveSummon(raw, label, effect, out int summonCap, out error))
            {
                return false;
            }

            var requirementErrors = new List<string>();
            if (!AbilityScoreLineParser.TryParse(raw.requires, label, out var requirements, requirementErrors))
            {
                error = string.Join(" ", requirementErrors);
                return false;
            }

            // Same "this field has no meaning on that effect" rule
            // ignoresDefense, queuePushSlots, meleeReach, reachSlots,
            // transform and summonEnemyId already follow (the seventh
            // instance). damageInstances replaces the Attack/power formula
            // entirely — see TryResolveDamageInstances's own refusal above —
            // so there is no formula left for an authored axis to scale.
            if (instances.Length > 0 && !string.IsNullOrWhiteSpace(raw.scalingAxis))
            {
                error = $"{label}: scalingAxis has no meaning on a skill with damageInstances — " +
                        "damageInstances replace the Attack-scaled formula entirely.";
                return false;
            }

            if (!TryResolveTransform(raw, label, effect, out var transform, out error))
            {
                return false;
            }

            // A queue push only means anything on a skill that deals damage
            // to somebody: on a heal there is nobody to knock back, so an
            // authored value would silently do nothing — the same reading
            // ignoresDefense already gets.
            //
            // DamageAll JOINED DamageSingle HERE IN MILESTONE C. It was
            // refused until Gale Scythe (plan 2.8), and the refusal was
            // honest at the time: the AOE branch applied status but not the
            // push, so an authored value on a sweep really would have done
            // nothing. It does something now (ResolveDamageAll's batch
            // delay), so the content rule follows the code rather than the
            // other way round. Asked through SkillEffects.IsDamagePipeline
            // so a third damage effect cannot quietly disagree with this
            // list.
            if (raw.queuePushSlots != 0 && !SkillEffects.IsDamagePipeline(effect))
            {
                error = $"{label}: queuePushSlots only means anything on a damage skill, not {effect}.";
                return false;
            }

            if (raw.queuePushSlots < 0)
            {
                error = $"{label}: queuePushSlots is {raw.queuePushSlots} — a negative push would pull the target FORWARD in the queue, " +
                        "which no skill in this design does. Use advanceSlots on a Hasten skill instead.";
                return false;
            }

            // THE MIRROR OF THE TWO ABOVE, for the advance (plan 1.9, 2.7).
            // A Hasten row must author a positive advanceSlots -- an advance
            // of zero places is a cast that visibly does nothing -- and
            // nothing else may author one at all, because no other
            // resolution reads it.
            if (raw.advanceSlots != 0 && effect != SkillEffect.Hasten)
            {
                error = $"{label}: advanceSlots only means anything on a Hasten skill, not {effect}.";
                return false;
            }

            if (effect == SkillEffect.Hasten && raw.advanceSlots <= 0)
            {
                error = $"{label}: a Hasten skill needs a positive advanceSlots — moving an ally zero places " +
                        "earlier is a cast that spends mana and changes nothing.";
                return false;
            }

            // AN AFFLICT IS ITS STATUS AND NOTHING ELSE (plan 2.10, milestone
            // D), so a row that authors none is a cast that spends mana, plays
            // a beat and changes nothing -- the same shape the Hasten refusal
            // just above catches. Same reasoning as "a skill must cost
            // something": the author believes they wrote a spell.
            //
            // THE DURATION IS NOT CHECKED HERE. StatusEffects.Apply floors
            // turns at 1 (StatusEffect.cs), so an unauthored duration is a
            // one-turn affliction rather than a no-op, and refusing it would
            // be inventing a content rule the engine does not have.
            if (effect == SkillEffect.Afflict && !appliesStatus.HasValue)
            {
                error = $"{label}: an Afflict skill needs appliesStatus — the status IS the spell, " +
                        "and a row without one casts, pays and does nothing.";
                return false;
            }

            // Same "this field has no meaning on that effect" rule
            // ignoresDefense and queuePushSlots already follow. A heal or an
            // AOE has no single front-ranked target for the rule to ask
            // about; a SingleEnemy-typed effect other than DamageSingle
            // (there isn't one today, but the check reads on the targeting
            // rather than the effect on purpose) is still a click on one
            // enemy and the rule still means something.
            if (raw.meleeReach && targeting != SkillTargeting.SingleEnemy)
            {
                error = $"{label}: meleeReach only means anything on a SingleEnemy skill, not {targeting}.";
                return false;
            }

            // A DAMAGE ROW MUST SAY WHETHER IT IS A PHYSICAL MOVE (plan D7).
            //
            // THE ONE FIELD WHOSE DEFAULT IS REFUSED RATHER THAN TAKEN, and
            // deliberately so: every other bool here means something harmless
            // when omitted, while an unstated physicalMove silently classifies
            // a new sword-swing as a cast, and the only symptom is a shackled
            // monster quietly swinging anyway. There is nothing to derive it
            // from -- not the element, not the approach (boulder_slam, shear
            // and battering_ram author none and are all physical) -- so the
            // author is made to answer.
            //
            // DAMAGE ROWS ONLY, because they are where the question is live.
            // A ward, a heal, a shout or a summon is not a move, and making
            // forty rows restate that would make the required answer noise
            // rather than a decision. A non-damage row may still author
            // physicalMove (nothing here refuses it) -- the classification is
            // about whether the actor moves to act, not about damage.
            //
            // physicalMoveOmitted is stamped by whoever parsed the FILE; an
            // entry built in code reads as "stated" and is never accused. See
            // RawSkillEntry.physicalMoveOmitted.
            if (raw.physicalMoveOmitted && SkillEffects.IsDamagePipeline(effect))
            {
                error = $"{label}: a {effect} row must state physicalMove explicitly — true for a swing, " +
                        "charge, lunge or thrown body blow, false for a cast. It cannot be derived from the " +
                        "damage element (a flaming sword strike is physical; a rock thrown by magic need not be), " +
                        "and Rooted refuses exactly the rows that say true.";
                return false;
            }

            if (!TryResolveReach(raw, label, targeting, out var reach, out error)) return false;

            // Both Resources-relative. A wrong convention here costs the skill
            // its animation and its sound with no error anywhere — the hit just
            // lands silently.
            // A COOLDOWN OF 1 IS NOT A COOLDOWN. The number counts from the
            // turn it was cast on, so 2 means "turn one, then turn three" and 1
            // means "turn one, then turn two" -- which is every turn, which is
            // what a skill with no cooldown already does.
            //
            // Refused rather than silently rounded up or ignored: an author who
            // types 1 believes they have made the skill wait, and the shortest
            // wait that exists is 2. Same reasoning as a relic modifier of zero.
            if (raw.cooldownTurns == 1)
            {
                error = $"{label}: a cooldownTurns of 1 means 'usable again next turn', which is " +
                        "no cooldown at all. Use 0 for none, or 2 for the shortest real wait " +
                        "(cast on turn one, back on turn three).";
                return false;
            }

            if (raw.cooldownTurns < 0)
            {
                error = $"{label}: cooldownTurns {raw.cooldownTurns} cannot be negative.";
                return false;
            }

            if (!SpellPresentationPaths.Check(label, raw.vfx, out error)) return false;

            if (!SpellLayerRules.TryCheck(label, raw.vfx, out error)) return false;

            if (!ArtPathConvention.Check(label, "iconPath", raw.iconPath, out error)) return false;

            if (!TryResolveElements(raw, label, instances, out var elements, out error))
            {
                return false;
            }

            if (!TryResolvePoolTiers(raw, label, effect, out var poolTiers, out error))
            {
                return false;
            }

            resolvedSkill = new ResolvedSkill(raw.id, raw.displayName, raw.description ?? "", raw.characterId.Trim(),
                unlockLevel, effect, targeting, manaCost, resourceCost, raw.spendsAllResource,
                power, flatAmount, raw.ignoresDefense, instances,
                raw.vfx.Copy(),
                sortOrder,
                appliesStatus, statusMagnitude, statusDuration, requirements, scalingAxis,
                raw.queuePushSlots, transform,
                // FORCED, not merely defaulted: a placeholder is never
                // player-selectable and never drawn by a monster's weighted
                // pool regardless of what raw.playerSelectable said, since
                // that field's own default is true and a bool has no -1
                // sentinel to distinguish "authored true" from "said
                // nothing".
                raw.placeholder ? false : raw.playerSelectable,
                raw.cooldownTurns,
                raw.stance?.Trim() ?? "", raw.summonEnemyId?.Trim() ?? "", summonCap,
                ParseApproach(raw.approach), raw.shake, reach,
                raw.bookOnly, raw.bookTier, elements,
                raw.approachStance?.Trim() ?? "", raw.windupStance?.Trim() ?? "",
                poolTiers,
                raw.placeholder, raw.placeholderNote ?? "",
                raw.resourceSpendCap, raw.spendsAllPrimary, raw.percentOfMaxHealthPerPoint, raw.freeAction,
                raw.percentOfCasterMaxHealth, raw.wardTurns, raw.iconPath ?? "",
                raw.healthCostPercent, requiresStatus, consumesStatus, instancesIfConsumed,
                raw.detonationPercent, detonationSplit,
                raw.advanceSlots, raw.physicalMove);
            error = null;
            return true;
        }

        // BJORN'S SLAM'S OWN RULES, all of them local to this one skill --
        // the cross-catalogue "does the owner even have a primary pool" rule
        // lives in TryResolveAll above, next to bookOnly's identical shape.
        //
        // SCOPED TO DamageSingle, same "this field has no meaning on that
        // effect" rule ignoresDefense/queuePushSlots/meleeReach already
        // follow. The rule is still "a path that actually reads PoolTiers",
        // and it now covers TWO: DamageSingle and DamageAll, both with no
        // fixed packets.
        //
        // WIDENED THE DAY A SECOND SKILL NEEDED IT, which is what this
        // comment said it would be. Bjorn's Rampage is a DamageAll with
        // Slam's own tiers (PLAN_PROGRESSION_V2.md §5), so ResolveDamageAll
        // now takes the fired tier and applies the same multiplier through
        // the same PoolTierResolution call ResolveDamageSingle uses --
        // one rule, two resolution paths, rather than a second tier
        // mechanic for sweeps.
        //
        // A FIXED-PACKET SKILL IS STILL REFUSED. ResolveDamageInstances
        // computes its own per-packet figure and never sees a tier, so
        // authoring one there would still spend a real pool for a
        // multiplier nobody applies.
        private static bool TryResolvePoolTiers(RawSkillEntry raw, string label, SkillEffect effect,
            out ResolvedPoolTier[] poolTiers, out string error)
        {
            poolTiers = Array.Empty<ResolvedPoolTier>();
            error = null;

            var authored = raw.poolTiers;
            if (authored == null || authored.Length == 0) return true;

            bool tieredEffect = SkillEffects.IsDamagePipeline(effect);
            if (!tieredEffect || (raw.damageInstances != null && raw.damageInstances.Length > 0))
            {
                error = $"{label}: poolTiers only means anything on a DamageSingle or DamageAll skill with no fixed " +
                        "damageInstances -- nothing multiplies a packet spell's or an AOE's damage by a tier today.";
                return false;
            }

            var resolved = new ResolvedPoolTier[authored.Length];
            float previousSpend = 0f;

            for (int i = 0; i < authored.Length; i++)
            {
                var tier = authored[i];
                if (tier == null)
                {
                    error = $"{label}: poolTiers entry #{i + 1} is missing.";
                    return false;
                }

                if (tier.spend <= 0f || tier.spend > 1f)
                {
                    error = $"{label}: poolTiers entry #{i + 1} spends {tier.spend} of the primary pool's " +
                            "capacity -- must be greater than 0 and at most 1.";
                    return false;
                }

                if (tier.spend <= previousSpend)
                {
                    error = $"{label}: poolTiers entry #{i + 1} spends {tier.spend}, which is not greater than " +
                            $"entry #{i}'s {previousSpend} -- tiers must be authored in strictly ascending order " +
                            "so the highest affordable one is always the last in the list.";
                    return false;
                }

                if (tier.damageMultiplier < 1f)
                {
                    error = $"{label}: poolTiers entry #{i + 1} has damageMultiplier {tier.damageMultiplier} -- " +
                            "must be 1 or higher. A tier that does not even match the base cast is not a tier.";
                    return false;
                }

                resolved[i] = new ResolvedPoolTier(tier.spend, tier.damageMultiplier, tier.shake, tier.hitStopSeconds);
                previousSpend = tier.spend;
            }

            poolTiers = resolved;
            return true;
        }

        // THE FIVE RULES A CHOICE OF ELEMENT HAS TO SATISFY, all of them about
        // the same thing: the JSON must read as what actually happens when the
        // first element is picked.
        //
        // Nothing here is orb-specific. A skill says it offers a choice by
        // listing more than one element beside packets to retype; every
        // consumer downstream (the menu depth, the bot's legal list, the cast
        // gate) reads ResolvedSkill.HasElementChoice and never an id.
        private static bool TryResolveElements(RawSkillEntry raw, string label, DamageInstance[] instances,
            out ElementChoice[] elements, out string error)
        {
            elements = System.Array.Empty<ElementChoice>();
            error = null;

            var authored = raw.elements;
            if (authored == null || authored.Length == 0) return true;

            // A CHOICE OF ONE IS NOT A CHOICE. It would put a menu depth in
            // front of the player with a single row on it, and the same spell
            // authored as a typed packet plays identically with one fewer
            // click.
            if (authored.Length == 1)
            {
                error = $"{label}: elements lists one element, which is not a choice — " +
                        "author the packet as that type instead, or list a second element.";
                return false;
            }

            // RETYPING IS THE WHOLE MECHANIC, so there has to be something to
            // retype. An Attack-scaled skill (power/flatAmount) rides the
            // caster's own attackType at cast time and has no authored packet
            // a choice could touch -- see RawSkillEntry.elements.
            if (instances == null || instances.Length == 0)
            {
                error = $"{label}: elements needs damageInstances to retype — an Attack-scaled skill " +
                        "rides the caster's own attackType and has no packet a choice could change.";
                return false;
            }

            var resolved = new ElementChoice[authored.Length];
            var seen = new List<DamageType>();

            for (int i = 0; i < authored.Length; i++)
            {
                var choice = authored[i];
                if (choice == null || !TryParseDamageType(choice.type, out var type))
                {
                    error = $"{label}: element #{i + 1} has an unknown type '{choice?.type}'. " +
                            $"Valid options: {string.Join(", ", System.Enum.GetNames(typeof(DamageType)))}, Frost.";
                    return false;
                }

                if (seen.Contains(type))
                {
                    error = $"{label}: elements lists {type} twice — the menu would show the same row " +
                            "twice and the second one could never be told from the first.";
                    return false;
                }

                seen.Add(type);

                if (!SpellPresentationPaths.Check($"{label} element #{i + 1}", choice.vfx, out error)) return false;

                if (!SpellLayerRules.TryCheck($"{label} element #{i + 1}", choice.vfx, out error)) return false;

                resolved[i] = new ElementChoice(type, choice.vfx);
            }

            // THE AUTHORED PACKETS MUST BE ONE OF THE OFFERED ELEMENTS. The
            // file is read by a human as "this is what the spell does"; a
            // packet typed as something the list does not offer would be a
            // spell that never once deals what its own damageInstances say.
            foreach (var instance in instances)
            {
                if (seen.Contains(instance.type)) continue;

                error = $"{label}: a damage packet is typed {instance.type}, which elements does not offer " +
                        $"({string.Join(", ", seen)}). Type the packets as one of the choices, so the file " +
                        "reads as what happens when that element is picked.";
                return false;
            }

            elements = resolved;
            return true;
        }

        // WHERE THE TWO REACH CONVENTIONS MEET, and the only place they do.
        //
        // meleeReach and reachSlots are two ways of saying the same KIND of
        // thing and are refused together rather than merged: "the front-rank
        // rule applies" is a rule with a relic that lifts it (Monkey King's
        // Scepter), while "positions 2 and 3" is an authored list nothing
        // lifts. A skill that said both would have to pick one at read time,
        // and whichever it picked would surprise whoever wrote the other.
        private static bool TryResolveReach(RawSkillEntry raw, string label, SkillTargeting targeting,
            out Reach reach, out string error)
        {
            reach = raw.meleeReach ? Reach.Melee : Reach.Any;
            error = null;

            var slots = raw.reachSlots;
            if (slots == null || slots.Length == 0) return true;

            if (raw.meleeReach)
            {
                error = $"{label}: reachSlots and meleeReach both say where this skill can be aimed. " +
                        "Use meleeReach for the front-rank rule (which Monkey King's Scepter lifts), " +
                        "or reachSlots for an authored restriction (which nothing lifts) — not both.";
                return false;
            }

            // Same "this field has no meaning on that targeting" rule
            // meleeReach just above already follows: a group cast or a self
            // buff has no single position to be aimed at.
            if (targeting != SkillTargeting.SingleEnemy)
            {
                error = $"{label}: reachSlots only means anything on a SingleEnemy skill, not {targeting}.";
                return false;
            }

            foreach (int slot in slots)
            {
                if (slot >= 1 && slot <= Reach.MaxRanks) continue;

                error = $"{label}: reachSlots names position {slot}, and positions are 1-{Reach.MaxRanks} " +
                        $"counted from the front (a side never fields more than {Reach.MaxRanks}). " +
                        "A 0 reads as an off-by-one against the 1-based convention; anything higher is a " +
                        "restriction nothing on the field could ever satisfy.";
                return false;
            }

            reach = Reach.FromContent(slots);
            return true;
        }

        // summonEnemyId and summonCap are required together, same
        // both-fields-or-neither rule appliesStatus/statusMagnitude follow —
        // and only mean anything on a Summon effect, same rule transform
        // follows against every effect but its own.
        private static bool TryResolveSummon(RawSkillEntry raw, string label, SkillEffect effect,
            out int summonCap, out string error)
        {
            summonCap = 0;
            error = null;

            bool authored = !string.IsNullOrWhiteSpace(raw.summonEnemyId) || raw.summonCap >= 0;

            if (effect != SkillEffect.Summon)
            {
                if (authored)
                {
                    error = $"{label}: summonEnemyId/summonCap have no meaning on a {effect} skill.";
                    return false;
                }

                return true;
            }

            if (string.IsNullOrWhiteSpace(raw.summonEnemyId))
            {
                error = $"{label}: a Summon skill needs a summonEnemyId — without one it would resolve and call in nothing.";
                return false;
            }

            if (raw.summonCap <= 0)
            {
                error = $"{label}: summonEnemyId is set to '{raw.summonEnemyId}', so summonCap is required and must be positive.";
                return false;
            }

            summonCap = raw.summonCap;
            return true;
        }

        // A Transform skill needs its grant block, and nothing else may carry
        // one. Same both-ways rule appliesStatus/statusMagnitude already
        // follow: an incomplete field set is refused rather than half-applied,
        // and a field set on the wrong effect is refused rather than ignored.
        private static bool TryResolveTransform(RawSkillEntry raw, string label, SkillEffect effect,
            out TransformGrant transform, out string error)
        {
            transform = null;
            error = null;

            bool authored = raw.transform != null && raw.transform.IsAuthored;

            if (effect != SkillEffect.Transform)
            {
                if (authored)
                {
                    error = $"{label}: a transform block has no meaning on a {effect} skill.";
                    return false;
                }

                return true;
            }

            if (!authored)
            {
                error = $"{label}: a Transform skill needs a transform block with a positive `turns` — " +
                        "without one it would cost its resource and change nothing.";
                return false;
            }

            if (raw.transform.attackPercent == 0 && raw.transform.speedPercent == 0
                && raw.transform.temporaryHealthPercent == 0)
            {
                error = $"{label}: this transform grants no attack, no speed and no temporary health — " +
                        "it would run its duration out and do nothing.";
                return false;
            }

            // THE FORM'S OWN HIT CUE, checked by exactly the two functions the
            // skill's own `vfx` block is checked by. A presentation that only
            // ever plays while a transform is running is no less able to name
            // a folder that does not exist or an anchor that does not parse,
            // and a second copy of those rules here would be the drift the
            // shared checkers exist to prevent.
            var hit = raw.transform.hit;
            if (hit != null && hit.vfx != null)
            {
                if (!SpellPresentationPaths.Check($"{label} transform.hit", hit.vfx, out error)) return false;
                if (!SpellLayerRules.TryCheck($"{label} transform.hit", hit.vfx, out error)) return false;
            }

            transform = raw.transform;
            if (transform.hit == null) transform.hit = new TransformHitCue();

            if (string.IsNullOrWhiteSpace(transform.displayName))
            {
                transform.displayName = raw.displayName;
            }

            return true;
        }

        // appliesStatus is entirely optional, but once authored, magnitude
        // and duration are both required — same "mixing field sets is
        // rejected rather than silently ignored" spirit as every other
        // cross-field rule in this file. Stun's magnitude is unused by
        // StatusEffects itself but still has to be a real positive number
        // here, so an author who leaves it blank gets a clear error rather
        // than a status that silently never lands. The rule itself lives in
        // StatusAuthoring.TryResolve, shared with EnemyEntryResolver's own
        // appliesStatus field — this is just the RawSkillEntry-shaped call.
        private static bool TryResolveStatus(RawSkillEntry raw, string label,
            out StatusEffectType? appliesStatus, out int magnitude, out int duration, out string error) =>
            StatusAuthoring.TryResolve(raw.appliesStatus, raw.statusMagnitude, raw.statusDuration, label,
                out appliesStatus, out magnitude, out duration, out error);


        // Long enough to read as a spell rather than a flicker, short enough
        // that a player casting it every turn is not waiting on it.
        private const float DefaultVfxSeconds = 0.6f;

        // Both authored spells peak on their third frame - the bolt is fully
        // struck, the flare fully open - and both are six frames long, so a
        // third of the way in is where a hit reads as landing. A spell drawn
        // to a different rhythm says so in its own entry.
        private const int DefaultVfxImpactFrame = 3;

        private static bool TryResolveDamageInstances(RawSkillEntry raw, string label, bool isRestorative,
            out DamageInstance[] instances, out string error)
        {
            instances = System.Array.Empty<DamageInstance>();
            error = null;

            if (raw.damageInstances == null || raw.damageInstances.Length == 0)
            {
                return true;
            }

            // Typed packets are damage. On a heal they would silently do
            // nothing, which is worse than being told.
            if (isRestorative)
            {
                error = $"{label}: damageInstances have no meaning on a {raw.effect} skill.";
                return false;
            }

            // The two ways of specifying damage are mutually exclusive on
            // purpose. A skill that had both would leave "does power scale on
            // top of the fixed amount, or replace it" as a question the
            // author has to guess the answer to.
            if (raw.power > 0 || raw.flatAmount > 0)
            {
                error = $"{label}: damageInstances replace the Attack-scaled formula entirely — " +
                        "remove power and flatAmount, or remove damageInstances.";
                return false;
            }

            return TryResolveDamagePackets(raw.damageInstances, label, "damageInstances", out instances, out error);
        }

        // THE SHARED LOOP behind damageInstances and damageInstancesIfConsumed
        // (milestone B) -- one place that parses a type, refuses a
        // non-positive amount and reports which field and which index, rather
        // than two copies free to drift about what "a packet that deals
        // nothing" means.
        private static bool TryResolveDamagePackets(RawDamageInstance[] raw, string label, string fieldName,
            out DamageInstance[] instances, out string error)
        {
            instances = System.Array.Empty<DamageInstance>();
            error = null;

            if (raw == null || raw.Length == 0)
            {
                return true;
            }

            var resolved = new DamageInstance[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                var packet = raw[i];
                if (packet == null || !TryParseDamageType(packet.type, out var type))
                {
                    error = $"{label}: {fieldName} #{i + 1} has an unknown type '{packet?.type}'. " +
                            $"Valid options: {string.Join(", ", System.Enum.GetNames(typeof(DamageType)))}, Frost.";
                    return false;
                }

                if (packet.amount <= 0)
                {
                    error = $"{label}: {fieldName} #{i + 1} ({type}) deals {packet.amount} — " +
                            "a packet that deals nothing should be removed rather than authored.";
                    return false;
                }

                resolved[i] = new DamageInstance(type, packet.amount);
            }

            instances = resolved;
            return true;
        }

        // "requiresStatus"/"consumesStatus" (milestone B): a StatusEffectType
        // name, optional, matched case-insensitively -- the same shape
        // StatusAuthoring.TryResolve's own appliesStatus parse uses, without
        // the magnitude/duration pair that field alone needs.
        private static bool TryParseOptionalStatus(string text, string label, string fieldName,
            out StatusEffectType? parsed, out string error)
        {
            parsed = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (!System.Enum.TryParse<StatusEffectType>(text.Trim(), ignoreCase: true, out var status))
            {
                error = $"{label}: {fieldName} '{text}' isn't valid. Valid options: " +
                        $"{string.Join(", ", System.Enum.GetNames(typeof(StatusEffectType)))}.";
                return false;
            }

            parsed = status;
            return true;
        }

        // "Frost" is accepted as Ice. The enum says Ice and the game says
        // frost; making an author remember which word won is a trap for no
        // benefit.
        private static bool TryParseDamageType(string text, out DamageType type)
        {
            type = DamageType.Physical;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (string.Equals(trimmed, "Frost", System.StringComparison.OrdinalIgnoreCase))
            {
                type = DamageType.Ice;
                return true;
            }

            return System.Enum.TryParse(trimmed, ignoreCase: true, out type)
                   && System.Array.IndexOf((DamageType[])System.Enum.GetValues(typeof(DamageType)), type) >= 0;
        }

        // So the common case never has to write `targeting` at all — an
        // author who states the effect has already said who it hits.
        // PUBLIC so a caller building a ResolvedSkill by hand (every EditMode
        // fixture that does) lands on the same targeting the resolver would
        // have given it. A fixture that guessed SingleEnemy for a HealSelf was
        // building content skills.json could never produce, and it went
        // unnoticed until targeting started carrying a rule.
        public static SkillTargeting DefaultTargetingFor(SkillEffect effect)
        {
            switch (effect)
            {
                case SkillEffect.DamageAll: return SkillTargeting.AllEnemies;
                case SkillEffect.HealSelf: return SkillTargeting.Self;
                case SkillEffect.HealParty:
                case SkillEffect.RestorePartyMana:
                case SkillEffect.BuffParty: return SkillTargeting.Party;
                // A transform happens to the caster; Provoke falls through to
                // SingleEnemy, and widens to the whole enemy side at CAST
                // time when the caster's tree says so (see SkillEffect.Provoke)
                // rather than being authored twice.
                case SkillEffect.Transform:
                // A summon happens to the caster's own side, not to
                // whoever the player last clicked.
                case SkillEffect.Summon: return SkillTargeting.Self;
                // Shatter picks its own targets from the wards it detonates,
                // so it asks the player to confirm rather than to aim.
                case SkillEffect.Shatter: return SkillTargeting.AllEnemies;
                // A WARD AND A GIFT BOTH LAND ON ONE ALLY THE PLAYER PICKS
                // (AUDIT #147, owner 2026-09-15). The ward was Self and the
                // gifts were Party, and neither was true: the ward always
                // went to the caster and the gifts always went to whoever
                // the engine chose, which is what the owner rejected. The
                // Target depth they now enter is the party side's, so the
                // fight stops for a click that means something.
                // HealSingle is the case AllyTargeting's own header named in
                // advance -- "a heal aimed at one squadmate is the obvious
                // one, and 'anybody on my side, myself included' is the right
                // default for it".
                case SkillEffect.HealSingle:
                case SkillEffect.Ward:
                case SkillEffect.GiftMana:
                case SkillEffect.GiftFury:
                // MILESTONE C'S TWO JOIN THE ALLY RACK. Borrowed Moment aims
                // at one squadmate and Palace Passage at two, and the SIDE is
                // the only thing SkillTargeting says -- how many picks the
                // side is asked for is the effect's own answer
                // (SkillEffects.PicksRequired), never an authored field, so
                // both rows read SingleAlly and only one of them stops twice.
                case SkillEffect.Hasten:
                case SkillEffect.SwapAllies:
                case SkillEffect.GiftHaste: return SkillTargeting.SingleAlly;
                default: return SkillTargeting.SingleEnemy;
            }
        }
        // A cast that names no approach holds -- the rooted default a spell has
        // always taken. The string map itself lives on StageApproaches so the
        // enemy plain-attack path cannot disagree with it.
        private static StageApproach ParseApproach(string approach) =>
            StageApproaches.Parse(approach, StageApproach.Hold);

    }
}
