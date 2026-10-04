using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // A POSITION CAST MOVES THE TRACKER ON ITS CUE: the beat opens on the queue
    // before the move (CombatBeat.PreTurnOrder) and paints the new one with the
    // sound and the log line, so the order does not change before the cast is
    // seen. Any other beat paints its one queue from the start and never again.
    public class PositionCastTrackerTests
    {
        private FightController _fight;
        private FightBeatPlayer _beats;

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            TestGlobals.ResetAll();
        }

        private IEnumerator LoadAndBind()
        {
            yield return FightSceneFixture.LoadFight();

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            _beats = _fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true);

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                new List<EnemyKit> { enemyKit }, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private IEnumerator Play(CombatBeat beat, List<string> painted)
        {
            _beats.WireTurnOrderForTest(order =>
                painted.Add(order == null ? "live" : string.Join(",", order.Select(c => c.Name))));

            bool finished = false;
            _beats.Play(new List<CombatBeat> { beat }, () => finished = true);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(finished, "the beat never finished");
        }

        [UnityTest]
        public IEnumerator ABeatWithAnOldOrderPaintsItFirstAndTheNewOneOnTheImpact()
        {
            yield return LoadAndBind();
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First();
            var foe = _fight.SessionForTest.Encounter.Enemies.First();

            var painted = new List<string>();
            yield return Play(new CombatBeat
            {
                Actor = hero, Target = hero, DamageType = DamageType.Physical,
                PreTurnOrder = new List<CombatantState> { hero, foe },
                TurnOrder = new List<CombatantState> { hero, hero },
            }, painted);

            CollectionAssert.AreEqual(new[] { "Shawn,Front", "Shawn,Shawn", "live" }, painted);
        }

        [UnityTest]
        public IEnumerator ABeatWithoutOneNeverRepaintsTheOrder()
        {
            yield return LoadAndBind();
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First();
            var foe = _fight.SessionForTest.Encounter.Enemies.First();

            var painted = new List<string>();
            yield return Play(new CombatBeat
            {
                Actor = hero, Target = foe, Amount = 3, DamageType = DamageType.Physical,
                TurnOrder = new List<CombatantState> { hero, foe },
            }, painted);

            CollectionAssert.AreEqual(new[] { "Shawn,Front", "live" }, painted);
        }
    }
}
