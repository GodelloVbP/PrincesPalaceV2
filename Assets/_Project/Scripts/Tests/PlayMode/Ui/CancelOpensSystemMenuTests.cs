using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // AUDIT.md #155, closed: "Cancel with nothing else up opens the system
    // menu" is back on the map and in the fight, where phase 2 step B's
    // deletion of SystemMenuController's own raw Escape poll left it missing.
    //
    // Driven through the REAL dispatcher (scripted BaseInput via
    // inputOverride, `yield return null`, assert resulting state), never a
    // direct HandleCancel/OnBackPressed call standing in for a press --
    // SystemMenuGamepadNavigationTests draws the same distinction for the
    // hub's half of this, and the interesting part of the fight's half is
    // precisely which of two branches the dispatcher takes.
    public class CancelOpensSystemMenuTests
    {
        private string _root;
        private ScriptedBaseInput _input;
        private SystemMenuController _menu;
        private FightController _fight;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-cancel-menu-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // The scripted input goes on whatever module the scene just built --
        // and EventSystem.current has to be that scene's own, which a
        // previously-loaded scene's leftover EventSystem can otherwise still
        // be claiming (phase 1's own hazard note).
        private void TakeOverInput()
        {
            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "this scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "this scene carries no SystemMenuController");
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator CancelFrame()
        {
            _input.CancelDown = true;
            yield return DriveFrame();
        }

        // ---- Fight -----------------------------------------------------------------

        // A REAL BOUND SESSION, not the scene's own bootstrap placeholder:
        // Fight's NavContext is pushed by Bind (RegisterNavContext), and
        // IFightNavigationTarget refuses every verb while _session is null or
        // _isBusy is true, so a dispatcher test with no session would pass by
        // doing nothing at all. Same fixture shape as
        // FightGamepadNavigationTests' own target-depth tests.
        private IEnumerator OpenAFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 1, "no enemies in content");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));
            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            for (int i = 0; i < 120 && !built.Session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(built.Session.IsPlayerTurn, "never reached a player turn to press Cancel on");

            TakeOverInput();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FightAtRoot_Cancel_OpensTheMenu_AndFightsOwnBranchIsInertAfterwards()
        {
            yield return OpenAFight();

            Assert.IsFalse(_menu.IsOpen, "the menu should start closed");
            int verbBefore = _fight.FocusedVerbForTest;

            yield return CancelFrame();

            Assert.IsTrue(_menu.IsOpen,
                "Cancel at MenuDepth.Root -- where FightMenuState.Back() consumes nothing -- should open the " +
                "system menu, the job the deleted Escape poll used to do in this scene (AUDIT.md #155)");

            // THE OTHER HALF OF THE SAME CLAIM: the menu's context now sits
            // ABOVE Fight's, so the dispatcher's Fight branch does not run at
            // all -- a stick press that would otherwise walk the verb column
            // must reach the menu, not the fight underneath it.
            _input.Vertical = 1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(verbBefore, _fight.FocusedVerbForTest,
                "Fight's own branch ran underneath the open menu -- the verb column moved on a press the " +
                "menu should have owned");
            Assert.IsTrue(_menu.IsOpen, "the menu closed itself on a Move press");
        }

        [UnityTest]
        public IEnumerator FightInsideATargetPick_Cancel_StepsBackOneLevel_AndDoesNotOpenTheMenu()
        {
            yield return OpenAFight();

            // ATTACK (Root, focus 0) skips the submenu straight to targeting
            // -- one level down, which is what Cancel has to spend itself on.
            _fight.ConfirmFocus();
            yield return null;

            yield return CancelFrame();

            Assert.IsFalse(_menu.IsOpen,
                "Cancel inside a target pick belongs to OnBackPressed's existing step-back, not to the menu");

            // PROVEN BY WHAT THE NEXT PRESS DOES, rather than by reading a
            // private depth: if the first Cancel really stepped back to Root,
            // the second one now has nothing to consume and opens the menu.
            // If it had not, this one would step back instead.
            yield return CancelFrame();

            Assert.IsTrue(_menu.IsOpen,
                "the first Cancel did not leave the menu at Root -- a second press should have found " +
                "nothing left to back out of and opened the system menu");
        }

        // ---- Map -------------------------------------------------------------------

        private IEnumerator OpenTheMap()
        {
            RunManager.StartRun(4242);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            TakeOverInput();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapCancel_OpensTheMenu_AndCancelAgainClosesIt()
        {
            yield return OpenTheMap();

            Assert.IsFalse(_menu.IsOpen, "the menu should start closed on the map");

            yield return CancelFrame();

            Assert.IsTrue(_menu.IsOpen,
                "Cancel with nothing else up should open the system menu on the map (AUDIT.md #155)");
            Assert.IsNotNull(EventSystem.current.currentSelectedGameObject,
                "opening the menu should select its own tab bar");

            yield return CancelFrame();

            Assert.IsFalse(_menu.IsOpen, "a second Cancel should close the menu it just opened");

            // "The map's remembered node is restored": the map declares no
            // Selectable set of its own yet (its Graph group is phase 3's
            // rollout), so what its context remembers is nothing -- and the
            // claim that actually matters for correctness is the one the
            // hub's own version of this test makes: closing must not leave
            // the menu's last-selected tab sitting there as the map's
            // selection.
            Assert.IsNull(EventSystem.current.currentSelectedGameObject,
                "the menu's last-selected tab leaked through as the map's own selection after closing");
        }
    }
}
