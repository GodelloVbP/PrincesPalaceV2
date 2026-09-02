using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PrincesPalace;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The skill detail card's element tag, hovered the way a player's mouse
    // reaches it -- FightScreenTests.TheDamageTypeTagSitsBetweenThePowerKey
    // AndItsValue already pins the built node's geometry; this is the seam
    // proving RefreshDetail actually fills it in and hides it again when the
    // skill deals no typed damage at all.
    public class FightSkillDetailDamageTypeTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayBeatsFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void RestoreBeatSpeed() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        // One character with two authored skills: a fixed-packet Arcane bolt
        // and a plain heal that deals no typed damage at all -- the pair the
        // "hidden for a non-damaging skill" half of the requirement needs.
        private IEnumerator LoadFightWithAnArcaneSkill()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });

            var arcaneBolt = new ResolvedSkill(
                "arcane_bolt", "Arcane Bolt", "A bolt of raw arcane force.", "shawn",
                unlockLevel: 1, effect: SkillEffect.DamageSingle, targeting: SkillTargeting.SingleEnemy,
                manaCost: 4, resourceCost: 0, spendsAllResource: false, power: 0, flatAmount: 0,
                ignoresDefense: false, damageInstances: new[] { new DamageInstance(DamageType.Arcane, 20) },
                presentation: SpellPresentation.None, sortOrder: 0);

            var heal = new ResolvedSkill(
                "mend", "Mend", "Restores health, no element at all.", "shawn",
                unlockLevel: 1, effect: SkillEffect.HealSelf, targeting: SkillTargeting.Self,
                manaCost: 3, resourceCost: 0, spendsAllResource: false, power: 5, flatAmount: 0,
                ignoresDefense: false, damageInstances: null,
                presentation: SpellPresentation.None, sortOrder: 1);

            var kit = new PlayerKit("shawn", CharacterRole.Tank,
                new List<ResolvedSkill> { arcaneBolt, heal }, null, DamageType.Physical, level: 4);
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
        public IEnumerator HoveringAnArcaneSkillShowsARCANE()
        {
            yield return LoadFightWithAnArcaneSkill();

            Click("Verb1");
            yield return null;
            _fight.HoverRowForTest(0);
            yield return null;

            var panel = _fight.CurrentDetailForTest();
            Assert.AreEqual("Arcane", panel.DamageType, "fixture: the model itself did not resolve Arcane");

            var label = Named("DetailDamageType").GetComponent<TMP_Text>();
            Assert.IsTrue(label.gameObject.activeInHierarchy, "the element tag stayed hidden for a damaging skill");
            Assert.AreEqual("ARCANE", label.text);

            bool ok = ColorUtility.TryParseHtmlString(FightHudPalette.DamageTypeArcane, out var expected);
            Assert.IsTrue(ok, "fixture: the palette token itself does not parse");
            Assert.AreEqual(expected.r, label.color.r, 0.02f);
            Assert.AreEqual(expected.g, label.color.g, 0.02f);
            Assert.AreEqual(expected.b, label.color.b, 0.02f);
        }

        [UnityTest]
        public IEnumerator HoveringANonDamagingSkillHidesTheTag()
        {
            yield return LoadFightWithAnArcaneSkill();

            Click("Verb1");
            yield return null;
            _fight.HoverRowForTest(1);
            yield return null;

            var panel = _fight.CurrentDetailForTest();
            Assert.AreEqual("", panel.DamageType, "fixture: Mend should carry no damage type at all");

            var label = Named("DetailDamageType").GetComponent<TMP_Text>();
            Assert.IsFalse(label.gameObject.activeInHierarchy,
                "a heal with no element still shows a (presumably stale) element tag");
        }
    }
}
