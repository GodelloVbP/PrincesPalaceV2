using PrincesPalace.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Every static this project can leak between tests, in one place.
    //
    // There is no DI container here and there is not meant to be one: the game
    // is single-player, and static services are the right call for it. The cost
    // is that a static is the one place the compiler stops helping, and a test
    // process runs 383 PlayMode tests in a row through the same statics. A test
    // that sets BeatSpeedMultiplier = 60f and forgets its teardown makes every
    // later test in that process run at sixty times speed, and the failure
    // surfaces somewhere else entirely -- AUDIT.md's Bloodlust investigation
    // burned four rounds on a suspected variant of exactly that.
    //
    // WHY THIS IS A HELPER AND NOT AN AUTOMATIC HOOK. The obvious design is an
    // assembly-level NUnit action that resets before every test. It was not
    // used, deliberately: NUnit's ordering between an assembly-level action and
    // a fixture's own [SetUp] decides whether this runs before or AFTER the
    // fixture prepares itself, and the second order is destructive here. Most
    // of these fixtures set SaveSystem.RootOverride to a throwaway directory in
    // [SetUp]; a reset landing after that would null it and point the whole
    // test at the player's real persistentDataPath. So the enforcement is a
    // source lint instead (GlobalStateLintTests) -- it catches the missing
    // teardown when it is WRITTEN, which is cheaper than catching it when it
    // fires, and it cannot reorder anything.
    //
    // ADDING A GLOBAL: put its reset here AND its pair in the lint's table.
    // The lint's own vacuity guard fails if that table stops matching anything.
    internal static class TestGlobals
    {
        // ONE entry point rather than a caches/knobs split. The split existed
        // for about ten minutes and only ever produced a method nobody called:
        // a caller in [TearDown] wants "leave nothing behind", not a choice
        // about which half. Anything needing finer control calls the seams
        // directly, which is what they are for.
        //
        // Cheap on purpose. Every Reset here nulls a field; the reload it
        // implies is lazy, so a test that never touches content never pays for
        // ContentDatabase.Reset.
        public static void ResetAll()
        {
            // Caches -- rebuildable, so clearing costs only the next read.
            ContentDatabase.Reset();
            AudioLevels.Reset();
            StanceManifestLoader.Reset();
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            // Knobs and seams. Every value is the SHIPPED default read from
            // where it is declared -- RequirementCurve.DefaultPercent rather
            // than 100 -- so this file does not become a second home for a
            // number that already has one.
            SaveSystem.RootOverride = null;
            Navigation.Reset();

            // THE CLOCK, which no test writes on purpose and several stop by
            // accident. The system menu pauses by setting this to zero, so any
            // fixture that opens it -- or opens the character sheet, which IS
            // it -- and then fails before closing hands every later test in the
            // process a frozen game. Nothing throws; the scaled coroutines
            // simply never finish, and it surfaces as an unrelated timeout.
            UnityEngine.Time.timeScale = 1f;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            // BOTH HALVES of the player-facing product (docs/
            // PLAN_BATTLE_SPEED.md revision 3 point 1): the source back to
            // its shipped default, then adopted immediately rather than left
            // for the next beat to catch up -- a test reading
            // PlayerSpeedMultiplier right after ResetAll must see 1, not
            // whatever the LAST fight in this process happened to adopt.
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightBeatPlayer.AdoptPlayerSpeed();
            ReckoningController.SpeedMultiplier = 1f;
            RewardTrackController.SpeedMultiplier = 1f;
            TalentController.MotionSpeedMultiplier = 1f;
            HubController.MotionSpeedMultiplier = 1f;
            FightController.BreathSpeedMultiplier = 1f;
            SpellPerformancePlayer.ClockOverride = null;
            RequirementCurve.Percent = RequirementCurve.DefaultPercent;
            RequirementCurve.GearRequirementsEnabled = false;
        }
    }
}
