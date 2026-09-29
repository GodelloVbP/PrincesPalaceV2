using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
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
    // A crit reads differently from a hit: the popup carries a "!" and a gold
    // colour no element uses, on a single-target beat and on each target of a
    // sweep, and an authored enemy crit shows on its intent badge.
    //
    // Synthetic beats played through FightBeatPlayer.Play, the same recipe
    // BattleSpeedPopupLifetimeTests uses: what is bypassed is FightSession's
    // own resolution of who crit, which the Domain CritTests already pin.
    public class CritPresentationTests
    {
        private FightController _fight;
        private FightBeatPlayer _beats;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            TestGlobals.ResetAll();
        }

        private IEnumerator LoadAndBind(int enemies, bool authoredCrit = false)
        {
            yield return FightSceneFixture.LoadFight();

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            _beats = _fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true);

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = new List<CombatantState>();
            var kits = new List<EnemyKit>();
            for (int i = 0; i < enemies; i++)
            {
                foes.Add(new CombatantState("Front" + i, false, 5000, 10, 8, 4));
                kits.Add(new EnemyKit(
                    new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                        DamageType.Physical, DamageType.Physical, 0) { AttackCrits = authoredCrit }, false));
            }

            var encounter = new CombatEncounter(new[] { hero }, foes.ToArray());
            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                kits, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private CombatantState Hero => _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
        private List<CombatantState> Foes => _fight.SessionForTest.Encounter.Enemies.Where(e => e != null).ToList();

        private List<TMP_Text> ActiveLabels() =>
            _beats.Popups.Where(p => p != null && !p.IsFree)
                  .Select(p => p.GetComponentInChildren<TMP_Text>()).ToList();

        private IEnumerator WaitForPopups(int count)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && ActiveLabels().Count < count) yield return null;
            Assert.GreaterOrEqual(ActiveLabels().Count, count, "fixture: the popups never appeared");
        }

        // The gold is pinned as a literal, not read from DamagePopup: PlayMode
        // has no InternalsVisibleTo grant, and a test reading the production
        // constant would agree with any value it held.
        private static void AssertGold(Color actual)
        {
            Assert.AreEqual(1f, actual.r, 0.02f);
            Assert.AreEqual(0.82f, actual.g, 0.02f);
            Assert.AreEqual(0.25f, actual.b, 0.02f);
        }

        [UnityTest]
        public IEnumerator ASingleTargetCritPopsGoldWithABang()
        {
            yield return LoadAndBind(1);

            _beats.Play(new List<CombatBeat>
            {
                new CombatBeat { Actor = Hero, Target = Foes[0], Amount = 36, Crit = true, DamageType = DamageType.Fire },
            }, () => { });
            yield return WaitForPopups(1);

            var label = ActiveLabels()[0];
            Assert.AreEqual("-36!", label.text);
            AssertGold(label.color);
        }

        [UnityTest]
        public IEnumerator APlainHitStaysUnmarked()
        {
            yield return LoadAndBind(1);

            _beats.Play(new List<CombatBeat>
            {
                new CombatBeat { Actor = Hero, Target = Foes[0], Amount = 24, DamageType = DamageType.Fire },
            }, () => { });
            yield return WaitForPopups(1);

            Assert.AreEqual("-24", ActiveLabels()[0].text);
        }

        [UnityTest]
        public IEnumerator ASweepMarksOnlyTheTargetsThatCrit()
        {
            yield return LoadAndBind(2);
            var foes = Foes;

            _beats.Play(new List<CombatBeat>
            {
                new CombatBeat
                {
                    Actor = Hero, Target = foes[0], Amount = 30, DamageType = DamageType.Fire,
                    Results = new List<BeatTargetResult>
                    {
                        new BeatTargetResult(foes[0], 30, missed: false, crit: true),
                        new BeatTargetResult(foes[1], 20, missed: false, crit: false),
                    },
                },
            }, () => { });
            yield return WaitForPopups(2);

            var texts = ActiveLabels().Select(l => l.text).OrderBy(t => t).ToList();
            CollectionAssert.AreEqual(new[] { "-20", "-30!" }, texts);
        }

        [UnityTest]
        public IEnumerator AnAuthoredEnemyCritShowsOnItsIntentBadge()
        {
            yield return LoadAndBind(1, authoredCrit: true);

            var value = _fight.GetComponentsInChildren<TMP_Text>(includeInactive: true)
                              .FirstOrDefault(t => t.name == "EnemyIntentValue0");
            Assert.IsNotNull(value, "the fight has no intent number for the front enemy");
            Assert.IsTrue(value.gameObject.activeInHierarchy, "the intent number is hidden");
            StringAssert.EndsWith("!", value.text);
        }
    }
}
