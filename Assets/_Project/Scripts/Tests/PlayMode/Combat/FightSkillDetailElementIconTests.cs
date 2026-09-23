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
    // The skill detail card's Element icon row, hovered the way a player's
    // mouse reaches it. 2026-09-23 icon rework replaced the old
    // detailDamageType text tag with an icon-row pool painted from
    // DetailPanel.Icons (FightHudModel.FillDetailIcons) into FightController's
    // detailIconImages/detailIconValues (FightController.Hud.cs's
    // RefreshDetail) -- this is the seam proving RefreshDetail actually
    // paints an active Element row, sprited from the resolved damage type's
    // own resource, and paints none at all for a skill that deals no typed
    // damage.
    public class FightSkillDetailElementIconTests
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
        public IEnumerator HoveringAnArcaneSkillShowsAnActiveElementRow()
        {
            yield return LoadFightWithAnArcaneSkill();

            Click("Verb1");
            yield return null;
            _fight.HoverRowForTest(0);
            yield return null;

            var panel = _fight.CurrentDetailForTest();
            Assert.AreEqual("Arcane", panel.DamageType, "fixture: the model itself did not resolve Arcane");

            int index = panel.Icons.FindIndex(icon => icon.Kind == DetailIconKind.Element);
            Assert.GreaterOrEqual(index, 0, "an Arcane bolt should carry an Element icon row");

            var image = Named($"DetailIcon{index}").GetComponent<Image>();
            Assert.IsTrue(image.gameObject.activeInHierarchy, "the Element row stayed hidden for a damaging skill");

            var expectedSprite = Resources.Load<Sprite>("Icons/Ability/element_arcane");
            Assert.IsNotNull(expectedSprite, "fixture: element_arcane is not under Resources/Icons/Ability");
            Assert.AreEqual(expectedSprite, image.sprite, "the Element row is not sprited from element_arcane");
        }

        [UnityTest]
        public IEnumerator HoveringANonDamagingSkillShowsNoElementRow()
        {
            yield return LoadFightWithAnArcaneSkill();

            Click("Verb1");
            yield return null;
            _fight.HoverRowForTest(1);
            yield return null;

            var panel = _fight.CurrentDetailForTest();
            Assert.AreEqual("", panel.DamageType, "fixture: Mend should carry no damage type at all");

            bool hasElementRow = panel.Icons.Any(icon => icon.Kind == DetailIconKind.Element);
            Assert.IsFalse(hasElementRow, "a heal with no element still carries an Element icon row");
        }
    }
}
