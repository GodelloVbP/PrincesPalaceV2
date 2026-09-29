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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 3: segment 2's mouse-only
    // regression. Same deterministic fight bind as JourneyFightRoundTests
    // (this file's own header on why a journey reconstructs rather than reads
    // a sibling class's leftover state), driven with the mouse instead of the
    // pad: a click on the verb plate is the whole "select ATTACK", and a
    // click on the enemy plate is the whole "hover then confirm" the pad
    // needs two presses for -- FightController.Input.cs's own "FOUR
    // SURFACES, ONE CONFIRM" comment on OnEnemyPressed is exactly why one
    // click suffices where the pad file needs a Move then a Submit.
    public class JourneyFightRoundMouseTests : JourneyFixture
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
            Assert.GreaterOrEqual(enemyIds.Count, 2, "need at least two enemies to prove the mouse drives a real round");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));
            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            _session = built.Session;
            yield return null;

            for (int i = 0; i < 120 && !_session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(_session.IsPlayerTurn, "never reached a player turn to click against");

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
        public IEnumerator ClickAttack_ClickTheEnemyPlate_LetTheRoundPlay_TheModelAdvances_MouseOnly()
        {
            yield return OpenAFight();

            Assert.AreEqual(0, _fight.FocusedVerbForTest, "fixture: starts on ATTACK, index 0");

            int enemyHpBefore = _session.Encounter.Enemies[0].CurrentHealth;

            yield return Click(Node("Verb0")); // ATTACK at Root skips straight to targeting
            Assert.AreEqual(0, _fight.HoveredEnemyIndexForTest,
                "a fresh target pick explicitly hovers the front living enemy (owner's 2026-09-19 call), not -1");

            // ONE CLICK, NOT A HOVER-THEN-CONFIRM PAIR: OnEnemyPressed is the
            // same "FOUR SURFACES, ONE CONFIRM" handler the pad's Submit
            // reaches after its own MoveDown -- a mouse click on the plate
            // both names the target AND confirms the attack.
            yield return Click(Node("EnemyPlate0"));

            yield return WaitUntil(() => _session.IsOver || (!_fight.IsBusy && _session.IsPlayerTurn),
                10f, "the attack should resolve and either hand the turn back or end the fight");

            Assert.Less(_session.Encounter.Enemies[0].CurrentHealth, enemyHpBefore,
                "one confirmed attack on the clicked enemy should have lowered its health");

            // LITERAL, pinned the same as the pad file: the input mode does
            // not change what SeededRandom(3) rolls against this exact
            // hero/enemy pair. Why 9: the plain swing is 8
            // (CombatMath.BasicAttackPowerMultiplier is already in it), and
            // the 5% party crit baseline (CritRules.BaseChancePercent) lands
            // on this seed's one crit draw, applied before armour; at a 0%
            // baseline the same seed reads 8.
            Assert.AreEqual(9, enemyHpBefore - _session.Encounter.Enemies[0].CurrentHealth,
                "the exact damage this seeded attack deals, mouse-only same as on the pad");
        }
    }
}
