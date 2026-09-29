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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 2: Fight, one
    // round on the pad -- the fight segment 1 loads into. Reconstructed here
    // through the same deterministic bind CancelOpensSystemMenuTests/
    // FightGamepadNavigationTests already use (FightEncounterAdapter.Build
    // against a fixed hero/enemy pair and SeededRandom(3)) rather than
    // physically reading segment 1's own leftover save file: NUnit does not
    // guarantee cross-CLASS execution order, so this suite reconstructs the
    // deterministic state a prior segment would have produced instead of
    // depending on which order the runner happens to invoke fixture classes
    // in -- every existing single-screen gamepad-nav test in this project
    // already does the equivalent (it sets up its OWN scenario through the
    // orchestrator rather than reading another test's leftovers).
    //
    // Driven exactly the way NavigationInputModule.ProcessFight reads a pad
    // (docs/GAMEPAD_NAVIGATION_PLAN.md section 3's Fight branch):
    // MoveFocus/ConfirmFocus through input.GetAxisRaw/GetButtonDown, never
    // called on the controller directly. FightGamepadNavigationTests' own
    // header explains why this needs no separate "gamepad glue" test: the
    // axis-polling itself is a few lines, and this file exercises it live
    // through the real dispatcher rather than leaving it untested, which is
    // what that older, direct-handler suite (predating this mechanism) still
    // does deliberately, kept alongside to pin the model.
    public class JourneyFightRoundTests : JourneyFixture
    {
        private FightController _fight;
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

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 2, "need at least two enemies to prove the pad drives a real round");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));
            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            _session = built.Session;
            yield return null;

            for (int i = 0; i < 120 && !_session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(_session.IsPlayerTurn, "never reached a player turn to press against");

            TakeOverInput();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MoveThroughTheVerbColumn_AttackTheHoveredEnemy_LetTheRoundPlay_TheModelAdvances()
        {
            yield return OpenAFight();

            Assert.AreEqual(0, _fight.FocusedVerbForTest, "fixture: starts on ATTACK, index 0");

            // MOVE ALONG THE VERB COLUMN -- proving the pad actually reaches
            // it, not merely that ATTACK happens to be the default focus.
            // Up steps toward the column's top (BuildVerbColumn's own
            // bottom-up order, FightGamepadNavigationTests' pinned claim),
            // then Down returns to ATTACK before it is pressed.
            yield return MoveUp();
            Assert.AreEqual(1, _fight.FocusedVerbForTest, "one Up press should step off ATTACK toward the column's top");

            yield return MoveDown();
            Assert.AreEqual(0, _fight.FocusedVerbForTest, "one Down press should return to ATTACK");

            int enemyHpBefore = _session.Encounter.Enemies[0].CurrentHealth;

            yield return PressSubmit(); // ATTACK at Root skips straight to targeting

            // A fresh target pick hovers the front living enemy explicitly,
            // not -1 -- Up at Target depth opens the hovered enemy's
            // intent instead of
            // cycling (FightGamepadNavigationTests'
            // FirstPressOnAFreshTargetPickTreatsEnemyZeroAsHovered pins the
            // same rule directly), so this round cycles with DOWN instead.
            Assert.AreEqual(0, _fight.HoveredEnemyIndexForTest,
                "a fresh target pick explicitly hovers the front living enemy");

            // PICK A TARGET WITH MOVE, TWO DOWNS NOT ONE: Down steps one slot
            // NEARER, which from the front enemy (0) wraps to the deepest
            // living enemy (1) first. Enemy 1 is not reachable by a melee
            // ATTACK (FightGamepadNavigationTests' own header on
            // CanReachEnemy), so this round needs a SECOND Down to wrap back
            // to the reachable front enemy before it can confirm anything.
            yield return MoveDown();
            Assert.AreEqual(1, _fight.HoveredEnemyIndexForTest,
                "one Down at target depth should wrap from the front enemy to the deepest living one");

            yield return MoveDown();
            Assert.AreEqual(0, _fight.HoveredEnemyIndexForTest,
                "a second Down wraps back to the first living enemy, the one ATTACK can actually reach");

            yield return PressSubmit(); // confirm the attack on the hovered enemy

            // LET THE ROUND PLAY with no further input -- FightBeatPlayer's
            // own crank means this resolves within a handful of real frames,
            // whether it hands the turn back or ends the fight outright.
            yield return WaitUntil(() => _session.IsOver || (!_fight.IsBusy && _session.IsPlayerTurn),
                10f, "the attack should resolve and either hand the turn back or end the fight");

            Assert.Less(_session.Encounter.Enemies[0].CurrentHealth, enemyHpBefore,
                "one confirmed attack on the hovered enemy should have lowered its health");

            // LITERAL, pinned rather than recomputed from the damage formula
            // under test (CLAUDE.md gotcha 5's formula-pinning
            // rule) -- SeededRandom(3) against this exact hero/enemy pair.
            // Why 9: the plain swing is 8 (CombatMath.BasicAttackPowerMultiplier
            // already raises the raw figure 1.2x). The 5% party crit baseline
            // (CritRules.BaseChancePercent) is on every player-side combatant
            // and this seed's one crit draw lands under it, so the swing crits
            // at 150% before armour, and defenses then leave 9. At a 0%
            // baseline the same seed reads 8.
            Assert.AreEqual(9, enemyHpBefore - _session.Encounter.Enemies[0].CurrentHealth,
                "the exact damage this seeded attack deals");
        }
    }
}
