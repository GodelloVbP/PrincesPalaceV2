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

            string appliesToSkillId = (raw.appliesToSkillId ?? "").Trim();
            bool hasScoped = false;
            foreach (var effect in effects) hasScoped |= EffectCatalog.Of(effect.Kind).SkillScoped;
            if (hasScoped && appliesToSkillId.Length == 0)
            {
                error = $"{label}: a skill-scoped effect needs appliesToSkillId naming the skill it upgrades.";
                return false;
            }

            if (!hasScoped && appliesToSkillId.Length > 0)
            {
                error = $"{label}: appliesToSkillId '{appliesToSkillId}' would do nothing -- the node has no skill-scoped effect. Remove it.";
                return false;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                if (EffectCatalog.Of(effects[i].Kind).SkillScoped)
                {
                    effects[i] = new Effect(effects[i].Kind, effects[i].Magnitude,
                        effects[i].Threshold, appliesToSkillId);
                }
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
                                   || effects.Count > 0
                                   || !string.IsNullOrEmpty(grantsSkillId);
            if (!grantsSomething)
            {
                error = $"{label}: grants nothing at all, so spending an ember on it does nothing.";
                return false;
            }

            if (!ArtPathConvention.Check(label, "iconPath", raw.iconPath, out error)) return false;
            if (!DescriptionMarkup.Check(label, raw.description, out error)) return false;

            resolvedTalent = new ResolvedTalent(raw.id, raw.displayName, raw.description ?? "",
                (raw.characterId ?? "").Trim(), raw.column, raw.row,
                raw.prerequisites ?? Array.Empty<string>(),
                raw.statBonus, raw.abilityScoreBonus, raw.maxManaBonus, raw.skillManaCostReduction,
                raw.signatureCapacityBonus, raw.signaturePerTurnBonus,
                sortOrder, raw.iconPath ?? "", raw.minSpent,
                effects, grantsSkillId, appliesToSkillId);
            error = null;
            return true;
        }

        // Each authored rule is checked against its kind's EffectCatalog row
        // (which kinds a talent may hold, which read a threshold or a
        // magnitude), by the same resolver relics and affixes go through.
        private static bool TryResolveEffects(RawTalentEntry raw, string label,
            out List<Effect> effects, out string error)
        {
            effects = new List<Effect>();
            error = null;

            if (raw.effects == null || raw.effects.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < raw.effects.Length; i++)
            {
                if (!EffectEntryResolver.TryResolve(raw.effects[i], EffectHolder.Talent,
                        $"{label}: effect #{i + 1}", out var effect, out error))
                {
                    return false;
                }

                effects.Add(effect);
            }

            // Two entries of the same kind on ONE node are always a mistake:
            // a talent's kind takes the strongest copy, so the weaker one is
            // dead weight -- except for the health-gated rules, where two tiers
            // on one node is the whole point (the Black Ram's root carries both
            // its 67% and its 33% wool tiers).
            foreach (var clash in effects.GroupBy(e => e.Kind).Where(g => g.Count() > 1))
            {
                if (EffectCatalog.Of(clash.Key).UsesThreshold)
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
