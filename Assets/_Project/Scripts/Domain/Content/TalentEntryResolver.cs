using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Content
{
    // Validates talents.json. Same collected-not-first-only reporting as the
    // other resolvers.
    //
    // Two of the rules here exist to protect SCENE GEOMETRY, which is
    // unusual for a Domain resolver and deliberate.
    // Talent Tree v2 (2026-08-02) replaced the scrolled 21-row ladder with a
    // FIXED, non-scrolling per-path skeleton (root -> 3x3 grid ->
    // convergence -> 3-way branch -> capstone, 21 slots) shared identically
    // by every path/character — Domain/Talents/TalentSkeleton.cs bakes the
    // skeleton's shape once; talents.json only supplies which talent occupies which
    // (column=path, row=slot) address. MaxColumn is now exactly 3 paths and
    // MaxRow exactly the skeleton's 21 slots, both hard ceilings rather than
    // generous headroom, since the shape is no longer content-driven. Those
    // limits used to be undocumented traps that produced a silently
    // overlapping tree at runtime; asserting them here turns them into a
    // content-build failure instead. That is AUDIT.md's "content validation
    // pass" and its "layout assertion" meeting in one place.
    public static class TalentEntryResolver
    {
        // DERIVED FROM THE SKELETON, not restated as literals.
        //
        // These are exactly the skeleton's own bounds, and the comment here
        // used to say so while writing 2 and 20 by hand anyway -- so widening
        // the tree meant editing this file too, and forgetting would have had
        // the resolver silently REJECT valid content with a message insisting
        // the row was out of range.
        //
        // Both are inclusive maxima against exclusive counts, which is the one
        // thing worth stating rather than leaving to the reader.
        private static int MaxColumn => Talents.TalentPage.PathCount - 1;
        private static int MaxRow => Talents.TalentSkeleton.SlotCount - 1;

        public static bool TryResolveAll(IReadOnlyList<RawTalentEntry> entries, out List<ResolvedTalent> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedTalent>();
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

            foreach (string duplicateId in resolved.GroupBy(t => t.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate talent id '{duplicateId}' — every id must be unique.");
            }

            var byId = resolved.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());

            // Cell collisions are checked PER VIEWER, not globally. Two
            // characters' private trees may share a cell — only one of them is
            // ever on screen — but a shared node and a character's own node in
            // the same cell would both be visible to that character at once,
            // and the grid can only render one.
            foreach (var owner in resolved.Select(t => t.CharacterId).Where(c => !string.IsNullOrEmpty(c)).Distinct())
            {
                var visible = resolved.Where(t => t.CharacterId == owner || t.IsShared).ToList();
                foreach (var clash in visible.GroupBy(t => new { t.Column, t.Row }).Where(g => g.Count() > 1))
                {
                    errors.Add($"'{owner}' sees {clash.Count()} talents at cell ({clash.Key.Column},{clash.Key.Row}): " +
                               $"{string.Join(", ", clash.Select(t => t.Id))}. One cell renders one node.");
                }
            }

            foreach (var talent in resolved)
            {
                foreach (string prerequisiteId in talent.Prerequisites)
                {
                    if (!byId.TryGetValue(prerequisiteId, out var prerequisite))
                    {
                        errors.Add($"Talent '{talent.Id}' requires unknown talent '{prerequisiteId}'.");
                        continue;
                    }

                    // A prerequisite the owner cannot see could never be
                    // unlocked, so the gated node would be permanently dead.
                    if (!prerequisite.IsShared && prerequisite.CharacterId != talent.CharacterId)
                    {
                        errors.Add($"Talent '{talent.Id}' requires '{prerequisiteId}', which belongs to " +
                                   $"'{prerequisite.CharacterId}' and can never be unlocked by '{talent.CharacterId}'.");
                    }

                    // Strictly lower row makes cycles impossible by
                    // construction, and means the tree always reads bottom-up.
                    if (prerequisite.Row >= talent.Row)
                    {
                        errors.Add($"Talent '{talent.Id}' (row {talent.Row}) requires '{prerequisiteId}' on row " +
                                   $"{prerequisite.Row} — a prerequisite must sit on a strictly lower row.");
                    }
                }
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            resolved = resolved.OrderBy(t => t.Row).ThenBy(t => t.Column).ToList();
            return true;
        }

        private static bool TryResolveOne(RawTalentEntry raw, int index, int sortOrder, out ResolvedTalent resolvedTalent, out string error)
        {
            resolvedTalent = default;
            string label = string.IsNullOrEmpty(raw.id) ? $"talents.json entry #{index + 1}" : $"talent '{raw.id}'";

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

            if (raw.column < 0 || raw.column > MaxColumn)
            {
                error = $"{label}: column {raw.column} is outside 0-{MaxColumn}. " +
                        "There are only 3 paths in the built skeleton (SceneBuilder.Talents.cs' TalentColumnX).";
                return false;
            }

            if (raw.row < 0 || raw.row > MaxRow)
            {
                error = $"{label}: row (slot) {raw.row} is outside 0-{MaxRow}. " +
                        "The fixed per-path skeleton only has 21 slots (SceneBuilder.Talents.cs' TalentSlot table).";
                return false;
            }

            if (raw.minSpent < 0)
            {
                error = $"{label}: minSpent {raw.minSpent} cannot be negative.";
                return false;
            }

            if (!TryResolveEffects(raw, label, out var effects, out error))
            {
                return false;
            }

            string grantsSkillId = (raw.grantsSkillId ?? "").Trim();

            // A talent that grants nothing costs an ember and does nothing —
            // the same rule Equipment and Weapons already get. Now with two
            // more ways to grant something, both of which have to be listed
            // here or a pure-effects node would be rejected as empty.
            bool grantsSomething = !raw.statBonus.Equals(Stats.StatBlock.Zero)
                                   || !raw.abilityScoreBonus.Equals(Stats.AbilityScoreBlock.Zero)
                                   || raw.maxManaBonus != 0
                                   || raw.skillManaCostReduction != 0
                                   || raw.signatureCapacityBonus != 0
                                   || raw.signaturePerTurnBonus != 0
                                   || !string.IsNullOrWhiteSpace(raw.grantsStartingItemId)
                                   || effects.Count > 0
                                   || !string.IsNullOrEmpty(grantsSkillId);
            if (!grantsSomething)
            {
                error = $"{label}: grants nothing at all, so spending an ember on it does nothing.";
                return false;
            }

            if (!ArtPathConvention.Check(label, "iconPath", raw.iconPath, out error)) return false;

            resolvedTalent = new ResolvedTalent(raw.id, raw.displayName, raw.description ?? "",
                (raw.characterId ?? "").Trim(), raw.column, raw.row,
                raw.prerequisites ?? Array.Empty<string>(),
                raw.statBonus, raw.abilityScoreBonus, raw.maxManaBonus, raw.skillManaCostReduction,
                raw.signatureCapacityBonus, raw.signaturePerTurnBonus,
                (raw.grantsStartingItemId ?? "").Trim(), sortOrder, raw.iconPath ?? "", raw.minSpent,
                effects, grantsSkillId);
            error = null;
            return true;
        }

        // Which TalentEffectType members read `threshold` at all. Everything
        // else must leave it at 0, so an author who puts a threshold on a
        // rule that cannot use one is told rather than left with a number
        // that quietly does nothing.
        //
        // Two of these read it as something other than percent-of-max-health,
        // and both say so in their own comments: TransformExtendOnKill (a cap
        // in turns) and WardAlsoAppliesRegen (a duration in turns). Those are
        // the only two exceptions, and they are the reason The Flock's spread
        // was split into a rule plus a flag rather than becoming a third —
        // see WardSpreadsToWholeParty. The 1-100 range check below is loose
        // enough to hold for all three readings, which is the one thing
        // keeping this from needing a per-member unit.
        private static readonly HashSet<TalentEffectType> UsesThreshold = new HashSet<TalentEffectType>
        {
            TalentEffectType.WoolPerTurnBelowHealth,
            TalentEffectType.ExecuteDamageBonusPercent,
            TalentEffectType.TransformExtendOnKill,
            TalentEffectType.TransformHoldsBelowHealth,
            TalentEffectType.DefenseBonusPercentBelowHealth,
            TalentEffectType.DamageCapPercentBelowHealth,
            TalentEffectType.TransformPermanentBelowHealth,
            TalentEffectType.WardAlsoAppliesRegen,
            TalentEffectType.CritChanceBelowTargetHealth,
        };

        // The four rules that are a FLAG: present or absent, with no number
        // attached. Authoring a magnitude on one of these is the same class
        // of mistake as authoring a threshold on a rule with no health gate.
        private static readonly HashSet<TalentEffectType> IgnoresMagnitude = new HashSet<TalentEffectType>
        {
            TalentEffectType.ProvokeHitsEveryEnemy,
            TalentEffectType.CheatDeathOncePerFight,
            TalentEffectType.HeadbuttCancelsIntent,
            TalentEffectType.TransformPushesEveryEnemy,
            TalentEffectType.TransformHoldsBelowHealth,
            TalentEffectType.TransformPermanentBelowHealth,
            TalentEffectType.WardIsFreeAction,
            TalentEffectType.WardSpreadsToWholeParty,
            TalentEffectType.GiftAppliesImmediateTurn,
            TalentEffectType.ShatterAppliesVulnerable,
            TalentEffectType.WardsNeverExpire,
            TalentEffectType.FuryEngineEinherjar,
            TalentEffectType.FirstIdleTurnFree,
            TalentEffectType.KillFillsMomentum,
            TalentEffectType.TwinRampage,
        };

        private static bool TryResolveEffects(RawTalentEntry raw, string label,
            out List<TalentEffect> effects, out string error)
        {
            effects = new List<TalentEffect>();
            error = null;

            if (raw.effects == null || raw.effects.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < raw.effects.Length; i++)
            {
                var authored = raw.effects[i];
                string where = $"{label}: effect #{i + 1}";

                if (authored == null || string.IsNullOrWhiteSpace(authored.type))
                {
                    error = $"{where} has no type.";
                    return false;
                }

                if (!Enum.TryParse<TalentEffectType>(authored.type.Trim(), ignoreCase: true, out var type)
                    || type == TalentEffectType.None)
                {
                    error = $"{where}: '{authored.type}' isn't a talent effect. Valid options: " +
                            $"{string.Join(", ", Enum.GetNames(typeof(TalentEffectType)).Where(n => n != nameof(TalentEffectType.None)))}.";
                    return false;
                }

                bool wantsThreshold = UsesThreshold.Contains(type);
                if (!wantsThreshold && authored.threshold != 0)
                {
                    error = $"{where} ({type}) has no health gate, so threshold {authored.threshold} would do nothing. Remove it.";
                    return false;
                }

                if (wantsThreshold && (authored.threshold <= 0 || authored.threshold > 100))
                {
                    error = $"{where} ({type}) needs a threshold between 1 and 100 (percent of max health); got {authored.threshold}.";
                    return false;
                }

                bool wantsMagnitude = !IgnoresMagnitude.Contains(type);
                if (wantsMagnitude && authored.magnitude <= 0)
                {
                    error = $"{where} ({type}) needs a positive magnitude; got {authored.magnitude}.";
                    return false;
                }

                if (!wantsMagnitude && authored.magnitude != 0)
                {
                    error = $"{where} ({type}) is a flag — it reads no magnitude, so {authored.magnitude} would do nothing. Remove it.";
                    return false;
                }

                effects.Add(new TalentEffect(type, authored.magnitude, authored.threshold));
            }

            // Two entries of the same type on ONE node are always a mistake:
            // TalentEffectSet takes the strongest of a type, so the weaker
            // one is dead weight — except for the health-gated rules, where
            // two tiers on one node is the whole point (the Black Ram's root
            // carries both its 67% and its 33% wool tiers).
            foreach (var clash in effects.GroupBy(e => e.Type).Where(g => g.Count() > 1))
            {
                if (UsesThreshold.Contains(clash.Key))
                {
                    continue;
                }

                error = $"{label}: {clash.Key} is listed {clash.Count()} times. Only the strongest would ever apply — " +
                        "merge them into one entry.";
                return false;
            }

            return true;
        }
    }
}
