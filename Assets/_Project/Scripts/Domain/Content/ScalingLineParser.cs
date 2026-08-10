using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Reads authored scaling: a list of "<ability score> <grade>" lines, like
    // "strength S" or "DEX C".
    //
    // The same shape itemsets.json already uses for stats ("dexterity 1"), on
    // purpose — an author who has written one has written the other, and a
    // flat list of strings means adding a seventh ability score or an extra
    // grade is a change to an enum rather than to a schema.
    //
    // Errors are COLLECTED rather than thrown on the first one, matching every
    // other resolver here: a content author gets every mistake in the file
    // from one rebuild instead of one per rebuild.
    public static class ScalingLineParser
    {
        public static bool TryParse(string[] lines, string label, out ScalingProfile profile, List<string> errors)
        {
            profile = ScalingProfile.None;
            bool ok = true;

            if (lines == null)
            {
                return true;
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Trim().Split(new[] { ' ', '\t', ':', '=' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    errors?.Add($"{label}: scaling entry '{line}' should read '<ability score> <grade>', for example 'strength S'.");
                    ok = false;
                    continue;
                }

                if (!AbilityScores.TryParse(parts[0], out var score))
                {
                    errors?.Add($"{label}: scaling entry names '{parts[0]}', which is not an ability score (strength, dexterity, constitution, wisdom, intelligence, charisma).");
                    ok = false;
                    continue;
                }

                if (!ScalingGrades.TryParse(parts[1], out var grade))
                {
                    errors?.Add($"{label}: scaling entry '{line}' has '{parts[1]}' where a grade (S, A, B, C, D, E, or none) should be.");
                    ok = false;
                    continue;
                }

                if (profile[score] != ScalingGrade.None)
                {
                    errors?.Add($"{label}: names {AbilityScores.ShortName(score)} twice, so one of the two grades would be silently discarded.");
                    ok = false;
                    continue;
                }

                profile = profile.With(score, grade);
            }

            return ok;
        }
    }
}
