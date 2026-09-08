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
        private const int DefaultManaCost = 0;
        private const int DefaultResourceCost = 0;
        private const int DefaultPower = 0;
        private const int DefaultFlatAmount = 0;

        public static bool TryResolveAll(IReadOnlyList<RawSkillEntry> entries, out List<ResolvedSkill> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedSkill>();
            errors = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                if (TryResolveOne(entries[i], i, resolved.Count, out var single, out string error))
                {
                    resolved.Add(single);
                }
                else
                {
                    errors.Add(error);
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

        private static bool TryResolveOne(RawSkillEntry raw, int index, int sortOrder, out ResolvedSkill resolvedSkill, out string error)
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
                if (unlockLevel < 1)
                {
                    error = $"{label}: unlockLevel must be 1 or higher (got {unlockLevel}). Characters start at level 1.";
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

            // AND ONLY WHEN SOMEBODY CHOOSES IT. The whole argument above is
            // about a player weighing this action against another one; a
            // monster's abilities are drawn by weight and it has no mana or
            // wool to spend either way. See RawSkillEntry.playerSelectable.
            if (raw.playerSelectable && manaCost == 0 && resourceCost == 0 && touchesHealthOrMana)
            {
                error = $"{label}: a skill that costs neither mana nor resource is strictly better than every other action " +
                        "and would simply be spammed. Give it a cost.";
                return false;
            }

            // A heal or a mana restore has no Attack to build on, so with
            // neither a flat amount nor any scaling it restores exactly
            // nothing and the button does visibly nothing when pressed.
            bool isRestorative = effect == SkillEffect.HealSelf
                                 || effect == SkillEffect.HealParty
                                 || effect == SkillEffect.RestorePartyMana;
            if (isRestorative && flatAmount == 0 && power == 0)
            {
                error = $"{label}: a {effect} skill with no flatAmount and no power restores nothing.";
                return false;
            }

            // Scaling per point spent, on a skill that never spends any, is
            // always zero — a silent no-op the author will not notice.
            if (power > 0 && resourceCost == 0 && !raw.spendsAllResource)
            {
                error = $"{label}: power scales per point of resource spent, but this skill spends none. " +
                        "Set resourceCost, or set spendsAllResource, or use flatAmount instead.";
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

            if (raw.ignoresDefense && !(effect == SkillEffect.DamageSingle || effect == SkillEffect.DamageAll))
            {
                error = $"{label}: ignoresDefense only means anything for a damage effect, not {effect}.";
                return false;
            }

            if (!TryResolveDamageInstances(raw, label, isRestorative, out var instances, out error))
            {
                return false;
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

            var scalingAxis = ScalingAxis.Auto;
            if (!string.IsNullOrWhiteSpace(raw.scalingAxis) && !Enum.TryParse(raw.scalingAxis, ignoreCase: true, out scalingAxis))
            {
                error = $"{label}: scalingAxis '{raw.scalingAxis}' isn't valid. Valid options: {string.Join(", ", Enum.GetNames(typeof(ScalingAxis)))}.";
                return false;
            }

            if (!TryResolveTransform(raw, label, effect, out var transform, out error))
            {
                return false;
            }

            // A queue push only means anything on a skill that resolves
            // against one enemy. On a heal or an AOE there is no single
            // target to knock back, so an authored value would silently do
            // nothing — the same reading ignoresDefense already gets.
            if (raw.queuePushSlots != 0 && effect != SkillEffect.DamageSingle)
            {
                error = $"{label}: queuePushSlots only means anything on a DamageSingle skill, not {effect}.";
                return false;
            }

            if (raw.queuePushSlots < 0)
            {
                error = $"{label}: queuePushSlots is {raw.queuePushSlots} — a negative push would pull the target FORWARD in the queue, " +
                        "which no skill in this design does. Use a positive number.";
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

            if (!TryResolveElements(raw, label, instances, out var elements, out error))
            {
                return false;
            }

            resolvedSkill = new ResolvedSkill(raw.id, raw.displayName, raw.description ?? "", raw.characterId.Trim(),
                unlockLevel, effect, targeting, manaCost, resourceCost, raw.spendsAllResource,
                power, flatAmount, raw.ignoresDefense, instances,
                raw.vfx.Copy(),
                sortOrder,
                appliesStatus, statusMagnitude, statusDuration, requirements, scalingAxis,
                raw.queuePushSlots, transform, raw.playerSelectable, raw.cooldownTurns,
                raw.stance?.Trim() ?? "", raw.summonEnemyId?.Trim() ?? "", summonCap,
                ParseApproach(raw.approach), raw.shake, reach,
                raw.bookOnly, raw.bookTier, elements);
            error = null;
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

            transform = raw.transform;
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

            var resolved = new DamageInstance[raw.damageInstances.Length];
            for (int i = 0; i < raw.damageInstances.Length; i++)
            {
                var packet = raw.damageInstances[i];
                if (packet == null || !TryParseDamageType(packet.type, out var type))
                {
                    error = $"{label}: damage instance #{i + 1} has an unknown type '{packet?.type}'. " +
                            $"Valid options: {string.Join(", ", System.Enum.GetNames(typeof(DamageType)))}, Frost.";
                    return false;
                }

                if (packet.amount <= 0)
                {
                    error = $"{label}: damage instance #{i + 1} ({type}) deals {packet.amount} — " +
                            "a packet that deals nothing should be removed rather than authored.";
                    return false;
                }

                resolved[i] = new DamageInstance(type, packet.amount);
            }

            instances = resolved;
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
                case SkillEffect.Ward:
                // A summon happens to the caster's own side, not to
                // whoever the player last clicked.
                case SkillEffect.Summon: return SkillTargeting.Self;
                // Shatter picks its own targets from the wards it detonates,
                // and a Gift lands on an ally. Neither asks the player to
                // click an enemy, so neither may default to SingleEnemy or
                // the fight would stop and wait for a click that means
                // nothing.
                case SkillEffect.Shatter: return SkillTargeting.AllEnemies;
                case SkillEffect.GiftMana:
                case SkillEffect.GiftFury:
                case SkillEffect.GiftHaste: return SkillTargeting.Party;
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
