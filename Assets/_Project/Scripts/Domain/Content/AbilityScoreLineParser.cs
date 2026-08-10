using System.Collections.Generic;
using System.Globalization;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Reads authored ability-score REQUIREMENTS: a list of
    // "<ability score> <amount>" lines, like "strength 15" or "INT 12".
    //
    // A sibling to ScalingLineParser rather than a reuse of
    // ItemSetEntryResolver.TryParseStats — that method is private and fills
    // BOTH an AbilityScoreBlock and a StatBlock from the same vocabulary
    // (ability scores AND stats like "defense"), which is exactly right for
    // a stat BONUS but wrong for a REQUIREMENT: a requirement only ever
    // means "an ability score", so "defense 5" here is a content mistake,
    // not a stat this block silently has no field for — it must be reported,
    // not swallowed into a StatBlock nobody asked this parser to produce.
    //
    // Errors are COLLECTED rather than thrown on the first one, matching
    // every other line parser here.
    public static class AbilityScoreLineParser
    {
        public static bool TryParse(string[] lines, string label, out AbilityScoreBlock scores, List<string> errors)
        {
            scores = AbilityScoreBlock.Zero;
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
                    errors?.Add($"{label}: requirement entry '{line}' should read '<ability score> <amount>', for example 'strength 15'.");
                    ok = false;
                    continue;
                }

                if (!AbilityScores.TryParse(parts[0], out var score))
                {
                    errors?.Add($"{label}: requirement entry names '{parts[0]}', which is not an ability score (strength, dexterity, constitution, wisdom, intelligence, charisma).");
                    ok = false;
                    continue;
                }

                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                {
                    errors?.Add($"{label}: requirement entry '{line}' has '{parts[1]}' where a whole number should be.");
                    ok = false;
                    continue;
                }

                if (scores[score] != 0)
                {
                    errors?.Add($"{label}: names {AbilityScores.ShortName(score)} twice, so one of the two requirements would be silently discarded.");
                    ok = false;
                    continue;
                }

                scores = scores.With(score, amount);
            }

            return ok;
        }
    }
}
