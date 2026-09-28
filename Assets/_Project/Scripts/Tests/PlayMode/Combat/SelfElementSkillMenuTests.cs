using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // A Self/Party skill that also authors elements[] must offer the
    // element choice from the menu: OnRowPressed's HasElementChoice check
    // runs before the Self/Party instant-resolve, so the row press opens
    // the element list rather than casting on the spot with whatever
    // element ResolvedSkill would default to. FightAction.LegalActions
    // enumerates every element for the bot regardless, so a player and the
    // bot must agree about what the same skill can do.
    //
    // No such skill exists in real content today (every element-choice skill
    // in the catalogue targets SingleEnemy), so this is a fixture -- but the
    // menu code cannot tell a fixture skill from an authored one, which is
    // exactly why the regression belongs at this level rather than a content
    // test that would need one added first.
    public class SelfElementSkillMenuTests
    {
        private FightController _fight;
        private CombatantState _hero;
        private const int ManaCost = 5;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        private IEnumerator LoadFightWithASelfElementSkill()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { _hero }, new[] { foe });

            // ONE SKILL, Self-targeted, authoring two elements -- the exact
            // shape the finding names: nothing a Target-depth click could
            // change, and a choice the menu has to ask about before it can
            // resolve.
            var selfElementSkill = new ResolvedSkill(
                "fixture_self_infusion", "Self Infusion", "Charges the caster with a chosen element.",
                "shawn", unlockLevel: 1, effect: SkillEffect.HealSelf, targeting: SkillTargeting.Self,
                manaCost: ManaCost, resourceCost: 0, spendsAllResource: false, power: 5, flatAmount: 0,
                ignoresDefense: false, damageInstances: null, presentation: SpellPresentation.None,
                sortOrder: 0,
                elements: new[]
                {
                    new ElementChoice(DamageType.Fire),
                    new ElementChoice(DamageType.Ice),
                });

            var kit = new PlayerKit("shawn", CharacterRole.Tank,
                new List<ResolvedSkill> { selfElementSkill }, null, DamageType.Physical, level: 4);
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RowPressOnASelfElementSkillEntersElementDepthRatherThanCastingOnTheSpot()
        {
            yield return LoadFightWithASelfElementSkill();

            int manaBeforeSkillRow = _hero.CurrentMana;

            Click("Verb1"); // opens the Skill branch
            yield return null;
            Click("CharacterSkill0"); // the skill row -- Self, but it asks first
            yield return null;

            Assert.AreEqual(manaBeforeSkillRow, _hero.CurrentMana,
                "the skill row press spent mana on its own -- it instant-resolved before asking which element");
            Assert.IsFalse(_fight.IsBusy,
                "the skill row press already resolved the cast -- it never gave the element list a chance to show");
        }

        [UnityTest]
        public IEnumerator PressingTheElementRowResolvesTheCastImmediatelyWithNoTargetStep()
        {
            yield return LoadFightWithASelfElementSkill();

            Click("Verb1");
            yield return null;
            Click("CharacterSkill0"); // skill row: Self + elements[] -> Element depth
            yield return null;

            int manaBeforeElementRow = _hero.CurrentMana;

            // THE SAME ROW POOL, a different list bound into it -- the same
            // button that was the skill row a moment ago is now element row 0
            // (FightScreen's own comment: "one row pool serves both... CurrentRows()
            // rebinds the same nodes at runtime").
            Click("CharacterSkill0"); // element row 0: Fire
            yield return null;

            Assert.AreEqual(manaBeforeElementRow - ManaCost, _hero.CurrentMana,
                "the element press did not resolve the cast -- a Self skill should need no further click");
            Assert.IsTrue(_fight.IsBusy,
                "AfterResolution never ran, so the cast did not actually go through");
        }
    }
}
