using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The heart's boss fight as the adapter lays it out, and the stage taking
    // a reinforcement into a fallen anchor's slot (docs/PLAN_RUN_MAP.md 6).
    public class HeartAnchorStageTests
    {
        private FightController _fight;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightSceneFixture.QuietForReuse(_fight);
            SharedScene.AfterTest();
        }

        private static string BossId() => ContentDatabase.Enemies.First(e => e.Data.IsBoss).id;

        private static List<string> RollableIds(int count) =>
            ContentDatabase.Enemies
                .Where(e => e.Data.Rollable && !e.Data.IsBoss)
                .Select(e => e.id)
                .Take(count)
                .ToList();

        private static string IdOf(FightSession session, CombatantState enemy) =>
            session.SourceFor(enemy).Source.Id;

        [Test]
        public void ABossBuildWithFourAnchors_FieldsTwoInFront_QueuesTwo_AndDeclaresTheBossLeader()
        {
            var ids = RollableIds(4);
            Assert.AreEqual(4, ids.Count, "fixture: content has fewer than four rollable non-boss enemies");
            var anchors = ids.Select((id, i) => new HeartAnchors.Anchor(100 + i, i, id, 10 - i, PrizeKind.None)).ToList();

            var built = FightEncounterAdapter.Build(new List<string> { "sheep" }, new List<string> { BossId() },
                new SeededRandom(11), isBoss: true, heartAnchors: anchors);
            var session = built.Session;
            var encounter = session.Encounter;

            Assert.AreEqual(3, encounter.Enemies.Count);
            Assert.AreEqual(ids[0], IdOf(session, encounter.Enemies[0]));
            Assert.AreEqual(ids[1], IdOf(session, encounter.Enemies[1]));
            Assert.AreEqual(BossId(), IdOf(session, encounter.Enemies[2]));
            CollectionAssert.AreEqual(new[] { ids[2], ids[3] },
                encounter.Reinforcements.Select(e => IdOf(session, e)).ToList());
            Assert.AreSame(encounter.Enemies[2], encounter.Leader);

            Assert.IsTrue(session.IsEliteEnemy(encounter.Enemies[0]));
            Assert.IsTrue(session.IsEliteEnemy(encounter.Enemies[1]));
            Assert.IsFalse(session.IsEliteEnemy(encounter.Leader), "the boss is not an elite");
            Assert.IsTrue(encounter.Reinforcements.All(session.IsEliteEnemy), "a queued anchor is an elite too");
        }

        [Test]
        public void AnAnchorBringsHalfItsEliteRoomHealth()
        {
            var ids = RollableIds(1);
            var anchor = new HeartAnchors.Anchor(100, 0, ids[0], 10, PrizeKind.None);

            var heart = FightEncounterAdapter.Build(new List<string> { "sheep" }, new List<string> { BossId() },
                new SeededRandom(11), isBoss: true, heartAnchors: new[] { anchor });
            var room = FightEncounterAdapter.Build(new List<string> { "sheep" }, ids,
                new SeededRandom(11), isElite: true);

            int roomFigure = room.Session.Encounter.Enemies[0].MaxHealth;
            Assert.Greater(roomFigure, 1, "fixture: the room figure must be able to halve");
            Assert.AreEqual(HeartAnchors.HealthAtHeart(roomFigure), heart.Session.Encounter.Enemies[0].MaxHealth);
            Assert.Less(heart.Session.Encounter.Enemies[0].MaxHealth, roomFigure);
        }

        // A fixture boss fight bound to the Fight scene: one front anchor
        // (1 HP), the boss behind it, one anchor queued.
        private IEnumerator Bind(System.Action<FightSession, CombatantState, CombatantState> found)
        {
            yield return SharedScene.EnsureFight();
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);

            var hero = new CombatantState("Shawn", true, 5000, 30, 40, 10);
            var front = new CombatantState("Front", false, 1, 10, 1, 1);
            var boss = new CombatantState("Boss", false, 50000, 10, 1, 1);
            var newcomer = new CombatantState("Newcomer", false, 500, 10, 1, 1);

            EnemyKit Kit(string id, bool isBoss) => new EnemyKit(new ResolvedEnemy(id, id, new StatBlock(), 5, 3, isBoss,
                DamageType.Physical, DamageType.Physical, 0), isElite: !isBoss);

            var encounter = new CombatEncounter(new[] { hero }, new[] { front, boss });
            Assert.IsTrue(encounter.DeclareLeader(boss));
            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                new List<EnemyKit> { Kit("front", false), Kit("boss", true) }, new SeededRandom(4), isBossFight: true);
            Assert.IsTrue(session.QueueReinforcement(newcomer, Kit("newcomer", false)));
            session.Begin();

            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            _fight.Bind(session, EncounterClass.Boss);
            yield return null;
            found(session, front, newcomer);
        }

        [UnityTest]
        public IEnumerator ANewcomerTakesTheFallenSlot_AfterTheCorpseFadesOut_AtFullOpacity_UnderItsOwnName()
        {
            FightSession session = null;
            CombatantState front = null;
            CombatantState newcomer = null;
            yield return Bind((s, f, n) => { session = s; front = f; newcomer = n; });

            Assert.IsTrue(session.IsPlayerTurn, "fixture: the hero acts first");
            Assert.AreEqual("Front", Named("Enemy0Nameplate").GetComponent<TMPro.TMP_Text>().text);

            Assert.IsTrue(session.ExecuteAttack(front), "fixture: the front anchor is in reach");
            Assert.AreSame(newcomer, session.Encounter.Enemies[0], "the encounter reseated it at once");
            typeof(FightController).GetMethod("AfterResolution",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(_fight, null);

            float deadline = Time.realtimeSinceStartup + 30f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            var fade = Named("Enemy0Slot").GetComponentInChildren<StageDeathFade>(includeInactive: true);
            Assert.IsNotNull(fade);
            var plate = Named("Enemy0Nameplate").GetComponent<TMPro.TMP_Text>();
            while (plate.text != "Newcomer" && Time.realtimeSinceStartup < deadline) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(fade.Fading, "the corpse's fade is finished");
            Assert.IsFalse(fade.Faded, "the slot was given back to a standing figure");
            Assert.AreEqual(1f, Named("Enemy0Sprite").GetComponent<Image>().color.a, 0.01f);
            Assert.AreEqual("Newcomer", Named("Enemy0Nameplate").GetComponent<TMPro.TMP_Text>().text);
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;
    }
}
