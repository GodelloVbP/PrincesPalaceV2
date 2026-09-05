using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Content
{
    // One achievement, exactly as typed into achievements.json.
    [Serializable]
    public class RawAchievementEntry
    {
        [ContentDoc("Stable identifier; written into save data the moment this is earned, so never rename it.")]
        public string id;
        [ContentDoc("The name shown for this achievement.")]
        public string displayName;
        [ContentDoc("Flavor text shown to the player.")]
        public string description = "";

        // Parsed against AchievementCondition by name (case-insensitive).
        [ContentDoc("Which AchievementCondition this is earned by, matched case-insensitively.")]
        public string condition = "";

        // What the condition compares against. Meaning depends on the
        // condition -- a level, a room count, a depth.
        [ContentDoc("What the condition compares against; meaning depends on the condition, and a counting condition rejects 0.")]
        public int threshold;

        // The condition's subject, when it has one: a boss id, today.
        [ContentDoc("The condition's subject when it has one; today only DefeatSpecificBoss uses it, naming a boss enemy id.")]
        public string parameter = "";
    }

    [Serializable]
    public class RawAchievementFile
    {
        public RawAchievementEntry[] achievements = Array.Empty<RawAchievementEntry>();
    }

    // One validated achievement -- and the shape AchievementDefinition now
    // STORES rather than restates. It carried a ToResolved() of its own, which
    // was the half-precedent the whole collapse generalised.
    //
    // [Serializable] class with public fields; System.Serializable is BCL, so
    // Domain stays engine-free.
    [Serializable]
    public sealed class ResolvedAchievement
    {
        // Written into save files the moment this is earned. NEVER rename
        // after a save exists.
        public string Id = "";
        public string DisplayName = "";
        public string Description = "";

        // How it is earned. Every value has a case in AchievementProgress.
        public AchievementCondition Condition;

        // What the condition compares against -- a level, a room count, a
        // depth. Meaning depends on the condition.
        public int Threshold;

        // The condition's subject when it has one. Today only
        // DefeatSpecificBoss uses it, naming a boss enemy id.
        public string Parameter = "";
        public int SortOrder;

        // For the serializer only.
        public ResolvedAchievement()
        {
        }

        public ResolvedAchievement(string id, string displayName, string description,
            AchievementCondition condition, int threshold, string parameter, int sortOrder)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Condition = condition;
            Threshold = threshold;
            Parameter = parameter ?? "";
            SortOrder = sortOrder;
        }
    }

    // Validates achievements.json.
    //
    // The rules here exist because an achievement that can never be earned
    // fails in the quietest possible way: whatever it gates simply never
    // appears, and nothing anywhere says why. Every check below turns one of
    // those into a build failure.
    public static class AchievementEntryResolver
    {
        public static bool TryResolveAll(IReadOnlyList<RawAchievementEntry> entries,
            out List<ResolvedAchievement> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedAchievement>();
            errors = new List<string>();
            if (entries == null) entries = new List<RawAchievementEntry>();

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

            foreach (string duplicate in resolved.GroupBy(a => a.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                // These end up in save data. Two accomplishments sharing an id
                // would be indistinguishable forever.
                errors.Add($"Duplicate achievement id '{duplicate}' -- every id must be unique.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawAchievementEntry raw, int index, int sortOrder,
            out ResolvedAchievement achievement, out string error)
        {
            achievement = default;
            string label = string.IsNullOrEmpty(raw.id) ? $"achievements.json entry #{index + 1}" : $"achievement '{raw.id}'";

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

            if (!Enum.TryParse<AchievementCondition>(raw.condition, ignoreCase: true, out var condition)
                || condition == AchievementCondition.Never)
            {
                error = $"{label}: condition '{raw.condition}' is not a known AchievementCondition " +
                        $"({string.Join(", ", Enum.GetNames(typeof(AchievementCondition)).Where(n => n != nameof(AchievementCondition.Never)))}).";
                return false;
            }

            // A counting condition with a threshold of zero is earned by every
            // brand new profile the moment it loads.
            if (AchievementProgress.NeedsThreshold(condition) && raw.threshold <= 0)
            {
                error = $"{label}: {condition} needs a threshold above 0, or it is earned before the player does anything.";
                return false;
            }

            // A boss achievement with no boss named can never fire.
            if (AchievementProgress.NeedsParameter(condition) && string.IsNullOrWhiteSpace(raw.parameter))
            {
                error = $"{label}: {condition} needs a `parameter` naming the boss id it is about.";
                return false;
            }

            achievement = new ResolvedAchievement(raw.id, raw.displayName, raw.description ?? "",
                condition, raw.threshold, raw.parameter, sortOrder);
            error = null;
            return true;
        }
    }
}
