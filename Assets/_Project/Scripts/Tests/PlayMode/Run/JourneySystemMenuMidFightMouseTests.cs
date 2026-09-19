using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 3: segment 3's mouse-only
    // regression.
    //
    // "OPENING THE MENU" HAS NO MOUSE EQUIVALENT -- item 3's own brief
    // names this exact case. A mouse player still has a keyboard, and escape
    // binds the SystemMenu axis as well as Cancel
    // (ProjectSettings/InputManager.asset), so this file keeps the key
    // presses for opening and closing the menu rather than inventing a HUD
    // icon that does not exist -- FightController.LeaveFight and HubController's own
    // mainMenuButton are both a DIFFERENT shortcut (straight to the Main
    // Menu, ending the run), not "open the system menu", so there is no
    // button standing in for it.
    //
    // WHAT REPLACES THE PAD'S "MoveUp proves Fight's branch is inert
    // underneath the open menu": a mouse-specific version of the same claim
    // -- clicking a Fight verb plate through the modal fires nothing, which
    // is plan section 2's own verified-safe modal-dimmer property
    // (raycastTarget stays true, so the click never reaches what is under
    // it) rather than the dispatcher's null-assert the pad test exercises.
    // Both are real regressions against the same transition; this is the one
    // a mouse can actually attempt.
    public class JourneySystemMenuMidFightMouseTests : JourneyFixture
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

            // A fresh scene's own layout can still be mid-settle the frame it

            // activates -- this suite found that gap under the full parallel

            // gate (never under a single-class or single-area slice), so every

            // mouse click aimed at a screen coordinate waits real time here

            // first, not just the two engine frames TakeOverInput's own callers

            // already pay.

            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartOpensTheMenu_AdjustsAStepperAndASlider_CancelClosesBackToFight_MouseOnly()
        {
            yield return OpenAFight();

            Assert.IsFalse(_menu.IsOpen, "fixture: the menu should start closed");
            int verbBefore = _fight.FocusedVerbForTest;
            int enemyHpBefore = _session.Encounter.Enemies[0].CurrentHealth;

            yield return PressSystemMenu(); // the mouse's own ESC key, which binds this axis too -- see this file's header

            Assert.IsTrue(_menu.IsOpen,
                "Start should open the system menu over a fight (AUDIT.md #155, moved off Cancel by the " +
                "owner's 2026-09-19 call)");

            // A click through the modal reaches nothing (plan section 2's
            // verified modal-dimmer property) -- Fight's own verb plate is
            // still there underneath, but the dimmer's raycastTarget stays
            // true and blocks it.
            yield return Click(Node("Verb0"));
            Assert.AreEqual(verbBefore, _fight.FocusedVerbForTest,
                "a mouse click on Fight's own verb plate reached it THROUGH the open menu -- the modal dimmer " +
                "should have blocked the raycast");
            Assert.AreEqual(enemyHpBefore, _session.Encounter.Enemies[0].CurrentHealth,
                "the click should not have fired an attack -- nothing under the modal should have run");
            Assert.IsTrue(_menu.IsOpen, "the menu should not have closed on a stray click either");

            yield return Click(Node("SystemTabOptions")); // the tab strip's own clickable button, not the shoulder shortcut

            var soundTrack = Node("OptionsRowsoundTrack");
            var resolutionNext = Node("OptionsRowresolutionNext");
            Assert.IsNotNull(soundTrack, "the options pane has no sound slider track");
            Assert.IsNotNull(resolutionNext, "the options pane has no resolution stepper Next button");

            GameSettings.SetSoundVolume(0.5f);
            GameSettings.SetResolutionIndex(0);

            // BarSlider.Set reads the click's own local x back into a 0..1
            // fraction of the track's width -- WorldPointAtFraction is that
            // same arithmetic run forward, so clicking at t=0.55 lands the
            // slider at exactly 0.55, the same literal the pad file pins via
            // one Right press (its own OptionsController.SliderStep, 0.05).
            var trackRect = (RectTransform)soundTrack.transform;
            yield return ClickWorldPoint(WorldPointAtFraction(trackRect, 0.55f));
            Assert.AreEqual(0.55f, GameSettings.SoundVolume, 0.0001f,
                "clicking the sound track at fraction 0.55 should set it to exactly that value");

            yield return Click(resolutionNext); // Step(key, +1), the mouse's own control for the pad's Right press
            Assert.AreEqual(1, GameSettings.ResolutionIndex,
                "clicking the resolution stepper's Next button should step its index up by exactly one");

            yield return PressCancel();

            Assert.IsFalse(_menu.IsOpen, "Cancel from inside Options should close the whole menu -- Options has no nested context");
            AssertFightIsTopWithNoSelection(
                "closing the menu should leave Fight on top with no EventSystem selection, mouse-only same as on the pad");
            Assert.AreEqual(verbBefore, _fight.FocusedVerbForTest,
                "Fight's own verb focus should be exactly where this segment left it -- untouched by the whole detour");
        }
    }
}
