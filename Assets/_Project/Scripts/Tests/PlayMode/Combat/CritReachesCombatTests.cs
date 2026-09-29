using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // The crit stats reach the stage: FightEncounterAdapter puts the party
    // baseline (5 / 150) and any bonus onto the combatant, and an enemy's
    // authored crit onto its kit. The rule itself is CritTests'; the wire
    // between content and combat is what nothing else covers.
    public class CritReachesCombatTests
    {
        private RelicDefinition _relic;
        private EnemyDefinition _enemy;

        [TearDown]
        public void Restore()
        {
            if (_relic != null) ((List<RelicDefinition>)ContentDatabase.Relics).Remove(_relic);
            if (_enemy != null) ((List<EnemyDefinition>)ContentDatabase.Enemies).Remove(_enemy);
            _relic = null;
            _enemy = null;
        }

        private static List<string> FirstCharacter() =>
            new List<string> { ContentDatabase.Characters.First().id };

        private static FightEncounterAdapter.BuiltFight Build(IReadOnlyList<string> enemies,
                                                               IReadOnlyList<string> relics = null,
                                                               IReadOnlyList<Character> characters = null) =>
            FightEncounterAdapter.Build(FirstCharacter(), enemies, new SeededRandom(11),
                relicIds: relics, partyCharacters: characters);

        private static List<string> OneEnemy() =>
            new List<string> { ContentDatabase.Enemies.First().id };

        private static void SetData(ScriptableObject asset, object value)
        {
            var field = asset.GetType().GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, asset.GetType().Name + " no longer has a private 'data' field to seed");
            field.SetValue(asset, value);
        }

        private void AddEnemy(System.Action<ResolvedEnemy> author)
        {
            var source = ContentDatabase.Enemies.First().Data;
            var copy = JsonUtility.FromJson<ResolvedEnemy>(JsonUtility.ToJson(source));
            copy.Id = "crit_test_enemy";
            author(copy);

            _enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            SetData(_enemy, copy);
            ((List<EnemyDefinition>)ContentDatabase.Enemies).Add(_enemy);
        }

        private static EnemyKit KitOfTheOnlyEnemy(FightEncounterAdapter.BuiltFight built) =>
            built.Session.SourceFor(built.Session.Encounter.Enemies.First(e => e != null));

        [Test]
        public void AnUnauthoredPartyMemberBringsTheBaseline_OnBothBuildPaths()
        {
            var fromDefinition = Build(OneEnemy()).Party[0];
            Assert.AreEqual(5, fromDefinition.CritChancePercent);
            Assert.AreEqual(150, fromDefinition.CritDamagePercent);

            var character = new Character(ContentDatabase.Characters.First().id);
            var fromSave = Build(OneEnemy(), characters: new List<Character> { character }).Party[0];
            Assert.AreEqual(5, fromSave.CritChancePercent);
            Assert.AreEqual(150, fromSave.CritDamagePercent);
        }

        [Test]
        public void ARelicCritBonusMovesTheCombatantsCritStats()
        {
            _relic = ScriptableObject.CreateInstance<RelicDefinition>();
            SetData(_relic, new ResolvedRelic
            {
                Id = "crit_test_relic",
                Modifiers = new[]
                {
                    new RelicModifier(RelicModifierType.CritChanceFlat, 10),
                    new RelicModifier(RelicModifierType.CritDamageFlat, 25),
                },
            });
            ((List<RelicDefinition>)ContentDatabase.Relics).Add(_relic);

            var state = Build(OneEnemy(), relics: new List<string> { "crit_test_relic" }).Party[0];

            Assert.AreEqual(15, state.CritChancePercent);
            Assert.AreEqual(175, state.CritDamagePercent);
        }

        [Test]
        public void AnAuthoredPlainAttackCritReachesTheEnemyKit()
        {
            AddEnemy(e =>
            {
                e.Abilities = System.Array.Empty<EnemyAbilityRef>();
                e.AttackCrits = true;
            });

            var kit = KitOfTheOnlyEnemy(Build(new List<string> { "crit_test_enemy" }));

            Assert.IsTrue(kit.Abilities[0].Crits);
        }

        [Test]
        public void AnAuthoredAbilityCritReachesTheEnemyKit_AndNotThePlainSwing()
        {
            var skill = ContentDatabase.Skills.First(s => s != null && !string.IsNullOrEmpty(s.id));
            AddEnemy(e =>
            {
                e.AttackWeight = 1f;
                e.AttackCrits = false;
                e.Abilities = new[] { new EnemyAbilityRef(skill.id, 1f, crits: true) };
            });

            var abilities = KitOfTheOnlyEnemy(Build(new List<string> { "crit_test_enemy" })).Abilities;

            Assert.IsFalse(abilities.First(a => a.IsPlainSwing).Crits, "the plain swing was not authored to crit");
            Assert.IsTrue(abilities.First(a => !a.IsPlainSwing).Crits, "the ability's authored crit was dropped");
        }
    }
}
