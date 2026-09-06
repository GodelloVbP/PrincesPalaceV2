using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // Everything an achievement condition is allowed to look at.
    //
    // A flat snapshot of facts rather than a reference to the save, and that is
    // the point: Domain cannot see SaveData, and more usefully, an evaluator
    // that can only read these six numbers cannot quietly grow a dependency on
    // the whole game. Core gathers them in one place; the rules below stay
    // testable with literals.
    public readonly struct AchievementFacts
    {
        // Behind a property, not a bare readonly field. `new AchievementFacts()`
        // and `default` both skip the constructor entirely -- that is how a
        // struct works -- so a field defaulted in the constructor is still null
        // for the single most important case there is: a brand new profile that
        // has done nothing. Reading it through here is what makes "nothing is
        // earned yet" answerable rather than a NullReferenceException.
        private readonly IReadOnlyCollection<string> _defeatedBossIds;

        public IReadOnlyCollection<string> DefeatedBossIds => _defeatedBossIds ?? System.Array.Empty<string>();

        public readonly int HighestCharacterLevel;
        public readonly int RoomsCleared;
        public readonly int DeepestStep;
        public readonly long TotalDamageDealt;

        public AchievementFacts(
            IReadOnlyCollection<string> defeatedBossIds = null,
            int highestCharacterLevel = 0,
            int roomsCleared = 0,
            int deepestStep = 0,
            long totalDamageDealt = 0)
        {
            _defeatedBossIds = defeatedBossIds;
            HighestCharacterLevel = highestCharacterLevel;
            RoomsCleared = roomsCleared;
            DeepestStep = deepestStep;
            TotalDamageDealt = totalDamageDealt;
        }
    }

    // Whether an achievement has been earned.
    //
    // Pure and total: every AchievementCondition value has a case, and the
    // default is NOT satisfied. A condition someone forgets to implement
    // therefore stays permanently unearned -- which is the safe direction, and
    // AchievementTableTests fails on it anyway so it cannot ship that way.
    public static class AchievementProgress
    {
        public static bool IsEarned(ResolvedAchievement achievement, AchievementFacts facts)
        {
            switch (achievement.Condition)
            {
                case AchievementCondition.DefeatSpecificBoss:
                    return !string.IsNullOrEmpty(achievement.Parameter)
                           && Contains(facts.DefeatedBossIds, achievement.Parameter);

                case AchievementCondition.DefeatDistinctBosses:
                    return CountDistinct(facts.DefeatedBossIds) >= achievement.Threshold;

                case AchievementCondition.ReachCharacterLevel:
                    return facts.HighestCharacterLevel >= achievement.Threshold;

                case AchievementCondition.ClearRooms:
                    return facts.RoomsCleared >= achievement.Threshold;

                case AchievementCondition.ReachDepth:
                    return facts.DeepestStep >= achievement.Threshold;

                case AchievementCondition.DealTotalDamage:
                    return facts.TotalDamageDealt >= achievement.Threshold;

                // Never, and anything added to the enum without a case here.
                default:
                    return false;
            }
        }

        // A threshold condition with a threshold of zero would be earned by
        // every brand-new profile the instant it loaded. Asked separately from
        // IsEarned so the CONTENT BUILD can reject it rather than the player
        // discovering a free unlock.
        public static bool NeedsThreshold(AchievementCondition condition)
        {
            switch (condition)
            {
                case AchievementCondition.DefeatDistinctBosses:
                case AchievementCondition.ReachCharacterLevel:
                case AchievementCondition.ClearRooms:
                case AchievementCondition.ReachDepth:
                case AchievementCondition.DealTotalDamage:
                    return true;
                default:
                    return false;
            }
        }

        // Likewise: DefeatSpecificBoss without a boss id names nothing and can
        // never fire.
        public static bool NeedsParameter(AchievementCondition condition) =>
            condition == AchievementCondition.DefeatSpecificBoss;

        // THE CHECK NO SINGLE RESOLVER CAN MAKE. AchievementEntryResolver sees
        // only achievements.json, so it can refuse a blank parameter and
        // nothing more -- it has no way to know whether "forest_warden" is a
        // real enemy id, let alone one flagged isBoss. DefeatedBossIds
        // (RunLedger/EmberPayout/RunSettlement) is populated only from
        // enemies with isBoss true, so a typo'd or de-bossed parameter is
        // silently, permanently unearnable and nothing else in the pipeline
        // says so.
        //
        // Pure function of plain values rather than a walk over
        // ContentDatabase's Resources-loaded catalogue, so it stays testable
        // with literals the way the rest of this class is -- the caller
        // (ContentDatabase.ValidateContent) is the one place that has to
        // touch real content, since it is the only reader that sees the
        // whole catalogue at once.
        public static string ValidateDefeatSpecificBossParameter(
            string achievementId, string parameter, IReadOnlyDictionary<string, bool> enemyIsBossById)
        {
            if (enemyIsBossById == null || !enemyIsBossById.TryGetValue(parameter, out bool isBoss))
            {
                return $"Achievement '{achievementId}' has condition DefeatSpecificBoss naming unknown enemy id '{parameter}'.";
            }

            if (!isBoss)
            {
                return $"Achievement '{achievementId}' has condition DefeatSpecificBoss naming enemy '{parameter}', which is not a boss (isBoss is false).";
            }

            return null;
        }

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (string candidate in ids)
            {
                if (candidate == id) return true;
            }

            return false;
        }

        private static int CountDistinct(IReadOnlyCollection<string> ids)
        {
            var seen = new HashSet<string>();
            foreach (string id in ids)
            {
                if (!string.IsNullOrEmpty(id)) seen.Add(id);
            }

            return seen.Count;
        }
    }
}
