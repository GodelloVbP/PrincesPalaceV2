using System;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Content
{
    // ONE PLACE FOR "appliesStatus is optional, but once authored, magnitude
    // and duration are both required" -- SkillEntryResolver and
    // EnemyEntryResolver both validated a raw entry's appliesStatus/
    // statusMagnitude/statusDuration trio with a byte-for-byte-identical
    // TryResolveStatus, one hand-copied from the other. A rule change to one
    // (a new default, a loosened bound) would have left the other silently
    // on the old rule, caught by nothing -- each resolver's tests only
    // exercised its own copy.
    //
    // TAKES THE PRIMITIVE FIELDS, not a Raw*Entry: RawSkillEntry and
    // RawEnemyEntry are unrelated types with no shared interface, and adding
    // one just to share three field reads would be a bigger seam than the
    // duplication it replaces.
    internal static class StatusAuthoring
    {
        public static bool TryResolve(string appliesStatus, int statusMagnitude, int statusDuration, string label,
            out StatusEffectType? resolvedStatus, out int magnitude, out int duration, out string error)
        {
            resolvedStatus = null;
            magnitude = 0;
            duration = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(appliesStatus))
            {
                return true;
            }

            if (!Enum.TryParse<StatusEffectType>(appliesStatus.Trim(), ignoreCase: true, out var parsed))
            {
                error = $"{label}: appliesStatus '{appliesStatus}' isn't valid. Valid options: {string.Join(", ", Enum.GetNames(typeof(StatusEffectType)))}.";
                return false;
            }

            if (statusMagnitude <= 0)
            {
                error = $"{label}: appliesStatus is set to {parsed}, so statusMagnitude is required and must be positive.";
                return false;
            }

            if (statusDuration <= 0)
            {
                error = $"{label}: appliesStatus is set to {parsed}, so statusDuration is required and must be positive.";
                return false;
            }

            resolvedStatus = parsed;
            magnitude = statusMagnitude;
            duration = statusDuration;
            return true;
        }
    }
}
