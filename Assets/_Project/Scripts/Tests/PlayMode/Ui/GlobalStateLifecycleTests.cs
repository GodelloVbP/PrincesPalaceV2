using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // THE OTHER HALF OF THE GLOBAL-STATE LADDER.
    //
    // GlobalStateLintTests catches a test that FLIPS a global and forgets the
    // teardown -- at write time, by reading the source. It cannot catch the
    // failure one level down: a global whose reset is simply MISSING from
    // TestGlobals.ResetAll, where every fixture in the suite is doing the right
    // thing and the helper they all trust quietly does not put that one back.
    //
    // Nothing else in the suite can see that. A fixture calling ResetAll is
    // exempted by the lint precisely BECAUSE it called it, so an absent entry
    // buys the offending global a permanent exemption from both rules at once.
    // That is E6's shape exactly: EscapeKey's frame stamp is written by
    // PRODUCTION during a test (SystemMenuController.HandleEscape -> Consume),
    // so no test file writes it, so the lint has nothing to scan, so the only
    // thing that could ever have noticed was a person reading ResetAll beside
    // the list of statics -- which is this file.
    //
    // scenarios E5, E6, B25, B26 (docs/hunt/SCENARIOS.md).
    public class GlobalStateLifecycleTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-globals-life-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- E6: the stamp nothing was resetting -------------------------------

        // ESCAPE IS CONSUMED BY PRODUCTION, NOT BY THE TEST, which is why this
        // one hid. A fixture that opens the system menu with Escape leaves
        // EscapeKey._consumedFrame stamped with the frame number that press
        // landed on; the next test's first frame is a DIFFERENT frame, so the
        // stamp is usually harmless -- but PlayMode tests share a player loop,
        // and a test whose first frame IS the frame the previous one ended on
        // reads ConsumedThisFrame as true and silently skips whatever Escape
        // was meant to do.
        //
        // EscapeKey.Reset() exists for exactly this, says so in its own
        // comment, and was called from SystemMenuTests alone. The fix is the
        // entry in ResetAll, not a call in every fixture.
        [Test]
        public void ResetAllClearsTheEscapeStampTheMenuLeavesBehind()
        {
            EscapeKey.Consume();
            Assert.IsTrue(EscapeKey.ConsumedThisFrame,
                "fixture: Consume did not stamp this frame, so this test proves nothing");

            TestGlobals.ResetAll();

            Assert.IsFalse(EscapeKey.ConsumedThisFrame,
                "TestGlobals.ResetAll left EscapeKey's frame stamp set, so a later test starting on " +
                "this same frame sees an Escape it never pressed as already consumed - and nothing " +
                "else in the suite can see this, because production writes the field and the source " +
                "lint only scans tests");
        }

        // ---- E5: every knob ResetAll claims to put back --------------------------

        // ONE ROW PER KNOB, each naming its own SHIPPED default rather than a
        // literal invented here -- RequirementCurve.DefaultPercent, not 100.
        // A knob whose reset is missing from ResetAll shows up as its own row
        // failing by name.
        private static readonly (string Name, Action Flip, Func<object> Read, object Shipped)[] Knobs =
        {
            ("ReckoningController.SpeedMultiplier",
                () => ReckoningController.SpeedMultiplier = 37f,
                () => ReckoningController.SpeedMultiplier, 1f),

            ("RewardTrackController.SpeedMultiplier",
                () => RewardTrackController.SpeedMultiplier = 37f,
                () => RewardTrackController.SpeedMultiplier, 1f),

            ("TalentController.MotionSpeedMultiplier",
                () => TalentController.MotionSpeedMultiplier = 37f,
                () => TalentController.MotionSpeedMultiplier, 1f),

            ("HubController.MotionSpeedMultiplier",
                () => HubController.MotionSpeedMultiplier = 37f,
                () => HubController.MotionSpeedMultiplier, 1f),

            ("FightController.BreathSpeedMultiplier",
                () => FightController.BreathSpeedMultiplier = 37f,
                () => FightController.BreathSpeedMultiplier, 1f),

            ("SpellPerformancePlayer.ClockOverride",
                () => SpellPerformancePlayer.ClockOverride = () => 0f,
                () => SpellPerformancePlayer.ClockOverride, null),

            // Written by the menu rather than by a test, usually -- a fixture
            // that opens the system menu has stopped the clock just as surely
            // as one that assigns it.
            ("Time.timeScale",
                () => Time.timeScale = 0f,
                () => Time.timeScale, 1f),

            ("RequirementCurve.Percent",
                () => RequirementCurve.Percent = 55,
                () => RequirementCurve.Percent, RequirementCurve.DefaultPercent),

            ("RequirementCurve.GearRequirementsEnabled",
                () => RequirementCurve.GearRequirementsEnabled = true,
                () => RequirementCurve.GearRequirementsEnabled, false),
        };

        [Test]
        public void EveryKnobResetAllNamesIsActuallyPutBack()
        {
            var offenders = new List<string>();

            foreach (var (name, flip, read, shipped) in Knobs)
            {
                flip();

                // The flip has to have TAKEN, or the row is vacuous and would
                // pass for a knob ResetAll never touches.
                if (Equals(read(), shipped))
                {
                    offenders.Add($"{name}: the test's own flip left it at its shipped value, so this row checks nothing");
                    continue;
                }

                TestGlobals.ResetAll();

                if (!Equals(read(), shipped))
                {
                    offenders.Add($"{name}: still {read()} after TestGlobals.ResetAll, expected {shipped ?? (object)"null"}");
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "a knob nothing resets outlives the test that set it for the whole process -- " +
                string.Join("; ", offenders));
        }

        // ---- B25: the tracker across a disable ------------------------------------

        // PLAYTIME IS AN ACCUMULATION, not a pair of timestamps, and that is
        // the whole of what a disable can get wrong. A tracker that stamped
        // Time.unscaledTime on enable and differenced it on the next tick would
        // credit the entire gap -- including a gap spent at the main menu,
        // which this deliberately does not count -- the moment it came back.
        //
        // DontDestroyOnLoad and never disabled in play, so the argument this
        // pins is the one that makes that safe rather than a path a player
        // reaches: the total is whatever was banked before the gap, and it
        // starts climbing again from there.
        [UnityTest]
        public IEnumerator PlaytimeIsNeitherLostNorDoubleCountedAcrossADisable()
        {
            yield return SceneManager.LoadSceneAsync(Navigation.Hub, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var tracker = UnityEngine.Object.FindAnyObjectByType<PlaytimeTracker>();
            Assert.IsNotNull(tracker,
                "no PlaytimeTracker in the scene -- its BeforeSceneLoad bootstrap never ran");

            // Climbing at all is the premise; without it every assertion below
            // is satisfied by a tracker that does nothing.
            float before = SaveSlotManager.CurrentSave.totalPlaySeconds;
            yield return null;
            yield return null;
            float running = SaveSlotManager.CurrentSave.totalPlaySeconds;
            Assert.Greater(running, before, "fixture: the tracker is not ticking, so nothing below is tested");

            tracker.enabled = false;
            float atTheGap = SaveSlotManager.CurrentSave.totalPlaySeconds;

            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(atTheGap, SaveSlotManager.CurrentSave.totalPlaySeconds, 0.0001f,
                "a disabled tracker kept banking seconds");

            tracker.enabled = true;
            yield return null;
            yield return null;

            float after = SaveSlotManager.CurrentSave.totalPlaySeconds;
            Assert.Greater(after, atTheGap, "the tracker never resumed after being re-enabled");

            // THE GAP ITSELF IS NOT CREDITED. Five frames were skipped; if the
            // re-enable had differenced a stamp instead of accumulating, the
            // first tick back would carry all five at once -- which is many
            // times a single frame's delta.
            Assert.Less(after - atTheGap, (running - before) * 4f,
                "re-enabling credited the whole gap at once, so the tracker is differencing a " +
                "timestamp rather than accumulating deltas");
        }

        // ---- E9: does time in the menu count as playtime? -------------------------

        // AN OWNER DECISION, WRITTEN DOWN AS A TEST RATHER THAN AS A QUESTION.
        //
        // PlaytimeTracker ticks Time.unscaledDeltaTime, so the clock the system
        // menu stops does not stop this: every second spent staring at the
        // options pane is banked as playtime. That is a defensible answer and
        // it may well be the intended one -- but nothing in the code, the docs
        // or the commit history says so, and the header that explains what this
        // tracker deliberately does NOT count (main-menu time, at length) is
        // silent about pauses.
        //
        // So this pins the CURRENT behaviour and is ignored, which is the
        // honest state: the expected value is a choice nobody has made. Delete
        // the [Ignore] if paused time counting is the answer; invert the
        // assertion and switch the tracker to Time.deltaTime if it is not.
        // Either way the decision gets recorded here instead of staying
        // implicit in a field nobody chose.
        [UnityTest]
        [Ignore("hunt 2026-09-11: pins current behaviour (paused time IS playtime) - the expected " +
                "value is an owner decision nothing in the code or docs has made")]
        public IEnumerator TimeSpentInTheSystemMenuCountsAsPlaytime()
        {
            yield return SceneManager.LoadSceneAsync(Navigation.Hub, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = UnityEngine.Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");

            menu.Open();
            Assert.AreEqual(0f, Time.timeScale, "fixture: the menu did not pause the game");

            float before = SaveSlotManager.CurrentSave.totalPlaySeconds;
            for (int i = 0; i < 5; i++) yield return null;
            float after = SaveSlotManager.CurrentSave.totalPlaySeconds;

            menu.Close();

            Assert.Greater(after, before,
                "time spent with the game paused is no longer counted as playtime - if that is the " +
                "decision, this test is the place it was written down");
        }
    }
}
