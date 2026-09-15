using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // Legacy Input cannot be simulated headlessly (see FightController.Input's
    // own PollGamepadNavigation comment for why -- the same reason the C/I
    // character-sheet hotkeys are tested through ToggleCharacterSheet rather
    // than through a simulated keypress). MoveFocus/ConfirmFocus/Wrap are
    // internal for exactly this reason: this suite drives the LOGIC directly,
    // the way a stick press would, and leaves the axis-polling glue itself
    // untested -- it is a few lines reading Input.GetAxisRaw, not a rule.
    public class FightGamepadNavigationTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        [Test]
        public void WrapCyclesBothDirectionsAtTheEnds()
        {
            Assert.AreEqual(0, FightController.Wrap(4, 4));
            Assert.AreEqual(3, FightController.Wrap(-1, 4));
            Assert.AreEqual(2, FightController.Wrap(2, 4));
            Assert.AreEqual(0, FightController.Wrap(0, 4));

            // A count of zero (no rows, no living enemies) never divides by
            // zero -- it lands on 0 rather than throwing, which is what lets
            // MoveFocus call this unconditionally instead of guarding first.
            Assert.AreEqual(0, FightController.Wrap(7, 0));
        }

        private IEnumerator LoadFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
        }

        [UnityTest]
        public IEnumerator MovingFocusAtRootStepsBottomUpTheWayTheColumnIsBuilt()
        {
            yield return LoadFight();

            // Root depth needs no bound session at all -- MoveFocus only
            // touches _session on the Sub/Target branches. Verified against
            // the scene straight after load, before any encounter exists.
            Assert.AreEqual(0, _fight.FocusedVerbForTest, "starts on ATTACK, index 0");

            // UP (delta -1): BuildVerbColumn puts ATTACK at the BOTTOM of the
            // column and MOVE at the top, so moving the stick toward the top
            // of the screen has to step toward the HIGHER index -- the
            // opposite of a plain top-to-bottom list.
            _fight.MoveFocus(-1);
            Assert.AreEqual(1, _fight.FocusedVerbForTest, "up moves toward the top of a bottom-up column");

            // DOWN (delta +1) from ATTACK wraps to the far end (MOVE) rather
            // than refusing to move -- a cyclic list, not a clamped one.
            _fight.MoveFocus(1);
            _fight.MoveFocus(1);
            Assert.AreEqual(3, _fight.FocusedVerbForTest, "wraps to MOVE, the top of the column");
        }

        [UnityTest]
        public IEnumerator MovingFocusAtTargetDepthCyclesTheHoveredLivingEnemy()
        {
            yield return LoadFight();

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 2, "need at least two enemies to prove target cycling");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));

            // Bind alone was never enough -- see EnemyFightableTests' own
            // header comment. FightBootstrap is what calls Begin() in real
            // play, and without it there is no current actor, so CanAct is
            // false for everything and ConfirmFocus silently no-ops -- which
            // is exactly what the first version of this test did, quietly.
            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            for (int i = 0; i < 60 && !built.Session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(built.Session.IsPlayerTurn, "never reached a player turn to test input against");

            // ATTACK (Root, focus 0) skips the submenu straight to targeting,
            // the exact click path OnVerbPressed(0) already takes.
            _fight.ConfirmFocus();
            yield return null;

            // Only the front enemy is reachable by a melee ATTACK
            // (FightSession.CanReachEnemy) -- cycling past it and
            // confirming there is a SEPARATE, already-covered claim
            // (Phase 1's OnEnemyPressed reach gate). This test's own claim
            // is narrower and does not need reach at all: that MoveFocus
            // actually moves which enemy is hovered, one step per call,
            // through the exact hover state RefreshInitiative's ghost
            // preview and the reticle's hover-brighten already read.
            _fight.MoveFocus(1);
            int first = _fight.HoveredEnemyIndexForTest;

            _fight.MoveFocus(1);
            int second = _fight.HoveredEnemyIndexForTest;

            Assert.AreNotEqual(-1, first, "the first move should hover a living enemy, not leave it unset");
            Assert.AreNotEqual(first, second, "a second move should hover a DIFFERENT enemy, not repeat the first");
        }
    }
}
