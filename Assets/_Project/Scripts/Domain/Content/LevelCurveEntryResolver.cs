using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Content
{
    // Validates the human-edited level_curve.json. Every field is required
    // (see RawLevelCurveEntry), so this is pure validation, not defaulting --
    // the same stance SpellTierEntryResolver takes, with the same
    // collected-not-first-only error reporting so a hand-edited file says
    // everything wrong with it in one pass.
    //
    // THREE RULES, AND THEY ARE THE PLAN'S, NOT THIS FILE'S. Progression v2
    // (docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md, contract 2 in §1
    // and the table in §3) says a level must cost no more than one deep run's
    // pay and no less than two typical room-0 fights, and that the table is
    // an authored ladder rather than a formula. A ladder with a hole in it,
    // or one that gets CHEAPER as it climbs, is not a ladder -- and neither
    // failure is visible by reading the file, which is why they are checked
    // here rather than trusted.
    //
    // WHY THE CAP IS READ FROM RewardTrack RATHER THAN TYPED HERE: the track
    // screen, the reward content and this table all have to agree about where
    // the ladder ends, and three copies of "40" is three chances to move two
    // of them. RewardTrack.MaxLevel owns it; this refuses a table that does
    // not match.
    public static class LevelCurveEntryResolver
    {
        // CONTRACT 2, as two numbers.
        //
        // 58 is two typical room-0 fights (29 each, xp_model.md Part A) --
        // the floor below which reaching a level is not an event, it is a
        // rounding error on one fight. 10,153 is a full deep run's pay at 25
        // permille (Part B) -- the ceiling above which a single level costs
        // more than the longest run the game has, which is how the geometric
        // curve priced everything past level 35.
        //
        // Both are inclusive, and both are deliberately wide: they are a
        // refusal to author something absurd, not a balance opinion. The
        // balance opinion is the table itself.
        public const int MinCost = 58;
        public const int MaxCost = 10153;

        public static bool TryResolveAll(IReadOnlyList<RawLevelCurveEntry> entries,
            out List<ResolvedLevelCost> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedLevelCost>();
            errors = new List<string>();

            var rows = entries ?? new List<RawLevelCurveEntry>();

            for (int i = 0; i < rows.Count; i++)
            {
                var raw = rows[i];
                string label = $"level_curve.json entry #{i + 1}" + (raw != null && raw.level > 0 ? $" (level {raw.level})" : "");

                if (raw == null)
                {
                    errors.Add($"{label}: the row is missing.");
                    continue;
                }

                if (raw.cost < MinCost || raw.cost > MaxCost)
                {
                    errors.Add($"{label}: cost {raw.cost} is outside {MinCost}-{MaxCost}. " +
                               "A level must cost at least two room-0 fights and at most one deep run's pay.");
                    continue;
                }

                resolved.Add(new ResolvedLevelCost(raw.level, raw.cost));
            }

            // THE TABLE'S OWN SHAPE, asked of the rows that survived the
            // per-row pass. Exactly levels 2..MaxLevel, in order, once each:
            // a gap silently makes a level free, a duplicate silently makes
            // one cost twice, and both look like ordinary rows on the way
            // past.
            var byLevel = resolved.OrderBy(r => r.Level).ToList();
            var expected = Enumerable.Range(LevelCurve.FirstPaidLevel,
                RewardTrack.MaxLevel - LevelCurve.FirstPaidLevel + 1).ToList();

            var actual = byLevel.Select(r => r.Level).ToList();
            if (!actual.SequenceEqual(expected))
            {
                var missing = expected.Except(actual).ToList();
                var extra = actual.Where(l => !expected.Contains(l)).Distinct().ToList();
                var duplicated = actual.GroupBy(l => l).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

                string detail = string.Join("; ", new[]
                {
                    missing.Count > 0 ? "missing " + string.Join(", ", missing) : null,
                    extra.Count > 0 ? "not on the track: " + string.Join(", ", extra) : null,
                    duplicated.Count > 0 ? "duplicated: " + string.Join(", ", duplicated) : null,
                }.Where(s => s != null));

                errors.Add($"level_curve.json must hold exactly levels {LevelCurve.FirstPaidLevel}-{RewardTrack.MaxLevel}, " +
                           $"one row each ({(detail.Length == 0 ? "wrong order" : detail)}).");
            }

            // MONOTONIC NON-DECREASING. Flat is fine -- the authored table
            // has 18 and 17 both at 4,000 and ten 10,000s in a row -- but a
            // level that costs LESS than the one before it makes every "about
            // N fights to go" reading go backwards, and makes the track speed
            // up where it is meant to slow down.
            for (int i = 1; i < byLevel.Count; i++)
            {
                if (byLevel[i].Cost < byLevel[i - 1].Cost)
                {
                    errors.Add($"level_curve.json level {byLevel[i].Level} costs {byLevel[i].Cost}, " +
                               $"less than level {byLevel[i - 1].Level}'s {byLevel[i - 1].Cost}. " +
                               "Costs may repeat but must never fall.");
                }
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            resolved = byLevel;
            return true;
        }

        // The resolved table as the flat list LevelCurve reads: index 0 is
        // the cost to enter FirstPaidLevel. Sorted by level on the way out of
        // TryResolveAll, so this is a projection and not a second ordering
        // decision.
        public static IReadOnlyList<int> CostsOf(IReadOnlyList<ResolvedLevelCost> rows) =>
            rows == null ? new int[0] : rows.OrderBy(r => r.Level).Select(r => r.Cost).ToList();
    }
}
