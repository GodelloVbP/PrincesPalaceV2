using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 3: the system
    // menu mid-fight -- section 3's own transition case (a Cancel that pops
    // a modal over Fight cannot also reach Fight's own branch the SAME
    // call), proven end to end rather than only at the dispatcher-fixture
    // level CancelOpensSystemMenuTests already covers. The fight is
    // reconstructed the same deterministic way JourneyFightRoundTests does
    // (see that file's own header for why: NUnit does not guarantee
    // cross-class order, so every journey segment reconstructs its own
    // precondition rather than reading a sibling class's leftover state).
    public class JourneySystemMenuMidFightTests : JourneyFixture
    {
        private FightController _fight;
        private SystemMenuController _menu;
        private Domain.Combat.Session.FightSession _session;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => TestGlobals.ResetAll();

        private IEnumerator OpenAFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the Fight scene carries no SystemMenuController");

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 1, "no enemies in content");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));
            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            _session = built.Session;
            yield return null;

            for (int i = 0; i < 120 && !_session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(_session.IsPlayerTurn, "never reached a player turn to press Cancel on");

            TakeOverInput();
            yield return null;
        }

        // Steps the tab strip with the shoulder shortcut (phase 3 item 2)
        // rather than Move -- a required action in its own right (section
        // 13's own checklist: "stick across every tab... with the shoulder
        // buttons... as well as Move"), and the more direct of the two paths
        // this segment could take. Capped rather than unbounded: a strip
        // that never reaches Options is this test's own failure to report,
        // not an infinite loop to hang the suite on.
        private IEnumerator StepTabsUntilOptions()
        {
            int optionsIndex = SystemMenuTabs.IndexOf(SystemMenuTab.Options);
            int guard = 0;
            while (_menu.SelectedIndex != optionsIndex && guard < 8)
            {
                yield return PressTabNext();
                guard++;
            }

            Assert.AreEqual(optionsIndex, _menu.SelectedIndex,
                "TabNext should reach the Options tab within one lap of the visible strip");
        }

        [UnityTest]
        public IEnumerator StartOpensTheMenu_AdjustsAStepperAndASlider_CancelClosesBackToFight()
        {
            yield return OpenAFight();

            Assert.IsFalse(_menu.IsOpen, "fixture: the menu should start closed");
            int verbBefore = _fight.FocusedVerbForTest;

            yield return PressSystemMenu();

            Assert.IsTrue(_menu.IsOpen,
                "Start should open the system menu over a fight, at any menu depth -- it is not Fight's own " +
                "step-back any more (AUDIT.md #155, moved off Cancel by the owner's 2026-09-19 call)");

            // THE OTHER HALF OF THE SAME CLAIM: the menu's context now sits
            // above Fight's, so the dispatcher's Fight branch must not run
            // underneath it -- a stick press that would otherwise walk the
            // verb column must reach the menu instead.
            yield return MoveUp();
            Assert.AreEqual(verbBefore, _fight.FocusedVerbForTest,
                "Fight's own branch ran underneath the open menu -- the verb column moved on a press the menu should have owned");
            Assert.IsTrue(_menu.IsOpen, "the menu should not have closed on a Move press");

            yield return StepTabsUntilOptions();

            var soundRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .FirstOrDefault(r => r.name == "OptionsRowsound");
            var resolutionRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .FirstOrDefault(r => r.name == "OptionsRowresolution");
            Assert.IsNotNull(soundRow, "the options pane has no sound slider row");
            Assert.IsNotNull(resolutionRow, "the options pane has no resolution stepper row");

            GameSettings.SetSoundVolume(0.5f);
            GameSettings.SetResolutionIndex(0);

            // MOVE onto each row from wherever the tab strip landed --
            // Down from the strip reaches the pane's own entry (the shoulder
            // shortcut's own contract, SystemMenuGamepadNavigationTests'
            // TabNext test), then across the pane to the two rows this
            // segment adjusts. The exact number of Down presses between rows
            // is not pinned here (that is OptionRows' own layout, covered by
            // SystemMenuGamepadNavigationTests) -- this walks Down until each
            // named row is reached, capped the same way the tab walk above is.
            yield return SelectByWalkingDown(soundRow.gameObject, 10);
            yield return MoveRight(); // one slider step right
            Assert.AreEqual(0.55f, GameSettings.SoundVolume, 0.0001f,
                "one Right press on the sound slider row should raise it by exactly OptionsController.SliderStep (0.05)");

            yield return SelectByWalkingDown(resolutionRow.gameObject, 10);
            yield return MoveRight(); // one stepper step right
            Assert.AreEqual(1, GameSettings.ResolutionIndex,
                "one Right press on the resolution stepper row should step its index up by exactly one");

            yield return PressCancel();

            Assert.IsFalse(_menu.IsOpen, "Cancel from inside Options should close the whole menu -- Options has no nested context");
            AssertFightIsTopWithNoSelection(
                "closing the menu should leave Fight on top with no EventSystem selection");
            Assert.AreEqual(verbBefore, _fight.FocusedVerbForTest,
                "Fight's own verb focus should be exactly where this segment left it -- untouched by the whole detour");
        }

        // Down repeatedly, through the dispatcher, until the named object is
        // selected or the cap is spent -- capped rather than a fixed count
        // because the exact row order is a different file's own claim to pin.
        private IEnumerator SelectByWalkingDown(GameObject target, int maxPresses)
        {
            int guard = 0;
            while (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != target && guard < maxPresses)
            {
                yield return MoveDown();
                guard++;
            }

            Assert.AreEqual(target, UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                $"Down presses from the tab strip never reached {target.name} within {maxPresses} presses");
        }
    }
}
