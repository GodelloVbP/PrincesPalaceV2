using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // EVERY ENEMY THE GAME SHIPS CAN ACTUALLY BE FOUGHT.
    //
    // The suite fought exactly two monsters -- whichever the synthetic fixtures
    // named -- so an enemy that only appears past floor 1 was never once put on
    // a stage by anything in CI. "After the elite, going into a fight it just
    // hangs" is what that gap looks like from the outside: floor 2 is the first
    // time several of these are reachable at all, and the first time anybody
    // finds out whether their art, their manifest entry and their skill line up.
    //
    // BOSSES INCLUDED, now -- they used to be filtered out here on the theory
    // that "only appears in the final room" meant "covered separately", and
    // nothing separately covered them. A boss whose whole kit is authored
    // skills (attackWeight 0, no plain swing at all -- the Forest Warden is
    // the first of these) is exactly the shape most likely to hang: there is
    // no legacy fallback to quietly carry a broken ability id or a missing
    // stance folder the way a plain swing always can.
    //
    // A beat that never finishes leaves the HUD locked behind IsBusy forever,
    // so the deadline IS the assertion -- there is no exception to catch.
    public class EnemyFightableTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator EveryActiveEnemyCanBeFieldedAndStruck()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var enemies = ContentDatabase.Enemies
                .Where(e => e != null)
                .Select(e => (e.id, e.data.IsBoss))
                .ToList();

            CollectionAssert.IsNotEmpty(enemies, "no active enemies in content");

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            Assert.IsNotNull(hero, "no characters in content");

            var stalled = new List<string>();

            foreach (var (enemyId, isBoss) in enemies)
            {
                var built = FightEncounterAdapter.Build(
                    new List<string> { hero.id }, new List<string> { enemyId },
                    new Domain.Rng.SeededRandom(7), isBoss: isBoss, isElite: false);

                Assert.IsNotNull(built?.Session, $"'{enemyId}' could not be built into an encounter");

                // Bind alone was never enough -- see its own header comment.
                // FightBootstrap is what calls Begin() in real play, which is
                // where an enemy that wins the opening initiative roll gets
                // its turn actually resolved. Every enemy this sweep tried
                // before happened to open slower than the hero, so the gap
                // was invisible until something here (hollow_choir, at speed
                // 14) did not.
                built.Session.Begin();
                _fight.Bind(built.Session, isBoss ? EncounterClass.Boss : EncounterClass.Normal);
                yield return null;
                yield return null;

                // One swing, driven through the button a player would press.
                var attack = _fight.GetComponentsInChildren<Button>(includeInactive: true)
                    .FirstOrDefault(b => b.name == "Verb0");
                Assert.IsNotNull(attack, "the fight has no attack verb");
                attack.onClick.Invoke();
                yield return null;

                // Targeting, if it opened one.
                var target = _fight.GetComponentsInChildren<Button>(includeInactive: true)
                    .FirstOrDefault(b => b.name == "EnemyPlate0" && b.gameObject.activeInHierarchy);
                if (target != null) target.onClick.Invoke();

                float deadline = Time.realtimeSinceStartup + 8f;
                while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

                if (_fight.IsBusy)
                {
                    stalled.Add(enemyId);

                    // Leave the clock sane for the next enemy in the sweep.
                    Time.timeScale = 1f;
                    yield return null;
                }
            }

            CollectionAssert.IsEmpty(stalled,
                "these enemies leave the fight busy forever, which is a locked HUD and no way to " +
                "act: " + string.Join(", ", stalled));
        }
    }
}
