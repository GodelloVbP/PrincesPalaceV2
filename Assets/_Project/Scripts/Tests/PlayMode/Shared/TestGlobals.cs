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
            // Null means the runner's REAL save folder, which outlives the
            // run. Safe here only because nothing saves between a teardown
            // and the next test's start, where TestSaveSandbox re-aims a
            // null root at an emptied sandbox. A test that calls this
            // MID-test puts its own root back before anything saves.
            SaveSystem.RootOverride = null;
            Navigation.Reset();

            // THE ONE GLOBAL THE LINT CANNOT SEE, and it was missing from here
            // until 2026-09-11. EscapeKey's frame stamp is written by
            // PRODUCTION during a test -- SystemMenuController.HandleEscape
            // calls Consume() -- so no test file writes it, so
            // GlobalStateLintTests has nothing to scan and would have been
            // vacuous if given a rule. Its own Reset() exists for exactly this
            // ("a stamp from a previous test would leak into the next one's
            // first frame") and was called from SystemMenuTests alone.
            //
            // What it leaks is a skipped Escape: a test whose first frame is
            // the same frame the previous test ended on reads ConsumedThisFrame
            // as true and the menu silently declines to open.
            EscapeKey.Reset();

            // THE SECOND GLOBAL THE LINT CANNOT SEE, same shape as EscapeKey
            // above: NavigationInputModule.LastInputWasPad is written by
            // PRODUCTION during a test (TrackInputDevice, every frame the
            // dispatcher runs) and by no test file, so there is nothing for
            // GlobalStateLintTests to scan. It deliberately survives a scene
            // load -- which device the player's hands are on is not a
            // property of a scene -- and that is exactly what makes it leak
            // between tests: a fixture that moved the scripted mouse hands
            // the next one a focus marker that refuses to draw.
            NavigationInputModule.ResetInputDeviceForTests();

            // THE CLOCK, which no test writes on purpose and several stop by
            // accident. The system menu pauses by setting this to zero, so any
            // fixture that opens it -- or opens the character sheet, which IS
            // it -- and then fails before closing hands every later test in the
            // process a frozen game. Nothing throws; the scaled coroutines
            // simply never finish, and it surfaces as an unrelated timeout.
            ResetEngineClock();
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

        // EVERY ENGINE TIME GLOBAL, back to what the project boots with.
        // Called from ResetAll AND, before every test, from
        // PlayModeTestProfiler.TestStarted -- before the fixture's [SetUp],
        // so a fixture that sets one of these for itself still wins.
        //
        // Why the hook as well as ResetAll: a teardown only resets what its
        // own fixture remembered, and the clock is also stopped by
        // PRODUCTION. CarriedHealthOnRespecTests opened the hub's system menu
        // (SystemMenuController.Pause, timeScale 0) and left it open; its
        // Hub scene then outlived the fixture, and a following fixture that
        // loads no scene of its own (FightBeatPhaseStanceTests,
        // FightContactCueTests) ran every beat on a stopped clock -- "the
        // beat never finished playing", 13 cases, in an order where no
        // scene load happened in between to fire the menu's OnDisable and
        // put the clock back. The single-process order only passed because
        // FightAfterTheEliteTests' scene load happened to land in the gap.
        //
        // Defaults are read off the engine on first call rather than typed
        // here, so ProjectSettings/TimeManager.asset stays their one home.
        // The first call is the hook's, before the first test of the run,
        // when nothing has had a chance to move them.
        private static bool s_clockDefaultsTaken;
        private static float s_fixedDeltaTime, s_maximumDeltaTime, s_maximumParticleDeltaTime;

        public static void ResetEngineClock()
        {
            if (!s_clockDefaultsTaken)
            {
                s_clockDefaultsTaken = true;
                s_fixedDeltaTime = UnityEngine.Time.fixedDeltaTime;
                s_maximumDeltaTime = UnityEngine.Time.maximumDeltaTime;
                s_maximumParticleDeltaTime = UnityEngine.Time.maximumParticleDeltaTime;
            }
            UnityEngine.Time.timeScale = 1f;
            // Zero here also zeroes captureFramerate; they are one setting.
            UnityEngine.Time.captureDeltaTime = 0f;
            UnityEngine.Time.fixedDeltaTime = s_fixedDeltaTime;
            UnityEngine.Time.maximumDeltaTime = s_maximumDeltaTime;
            UnityEngine.Time.maximumParticleDeltaTime = s_maximumParticleDeltaTime;
        }
    }
}
