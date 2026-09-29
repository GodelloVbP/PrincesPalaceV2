using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The planted shield is a stage prop the fight view draws in front of its
    // holder's seat: intact while more than half the placed size is left,
    // cracked at or below half, broken for a moment after a break and then
    // gone, and a Shieldwall draws the wall on every standing ally instead.
    public class FightShieldPropTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        private IEnumerator OpenTheFight()
        {
            yield return SharedScene.Ensure("Fight");
            SharedScene.MarkDirty("plants a shield on a party member");

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsTrue(_fight.HasSession, "the Fight scene has no fight to plant a shield in");
        }

        private string PropName(int slot) => _fight.ShieldPropSpriteForTest(slot)?.name;

        [UnityTest]
        public IEnumerator PlantingDrawsTheShieldAtTheSeatAndDamageCracksIt()
        {
            yield return OpenTheFight();
            var session = _fight.Session;
            var hero = session.Encounter.PlayerParty[0];

            _fight.RefreshShieldPropsForTest();
            Assert.IsNull(PropName(0), "nothing is planted yet");

            Assert.IsTrue(session.PlantShield(hero));
            _fight.RefreshShieldPropsForTest();
            Assert.AreEqual("intact", PropName(0));

            // The prop is a child of the seat, standing on its ground line: its
            // bottom-centre pivot sits at or just below the slot's bottom edge.
            var prop = _fight.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .First(i => i.name == "Party0ShieldProp");
            Assert.AreEqual("Party0Slot", prop.transform.parent.name);
            Assert.LessOrEqual(prop.rectTransform.anchoredPosition.y, 0f, "the tip is on the ground line, not above it");
            Assert.Greater(prop.rectTransform.sizeDelta.y, 0f);

            // Half of the placed size left is already cracked.
            hero.PlantedShield.Ward.Magnitude = hero.PlantedShield.PlacedPoints / 2;
            _fight.RefreshShieldPropsForTest();
            Assert.AreEqual("cracked", PropName(0));

            hero.PlantedShield.Ward.Magnitude = hero.PlantedShield.PlacedPoints / 2 + 1;
            _fight.RefreshShieldPropsForTest();
            Assert.AreEqual("intact", PropName(0), "over half is intact");
        }

        [UnityTest]
        public IEnumerator ABreakShowsTheBrokenShieldThenRemovesIt()
        {
            yield return OpenTheFight();
            var session = _fight.Session;
            var hero = session.Encounter.PlayerParty[0];
            var foe = session.Encounter.Enemies[0];

            session.PlantShield(hero);
            _fight.RefreshShieldPropsForTest();
            Assert.AreEqual("intact", PropName(0));

            session.StrikeForTest(foe, hero, 100000, DamageType.Physical);
            Assert.IsFalse(hero.PlantedShield.IsPlaced, "fixture: the hit broke the shield");

            _fight.RefreshShieldPropsForTest();
            Assert.AreEqual("broken", PropName(0));

            // Cleared by its own timer, not by the next repaint.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (PropName(0) != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNull(PropName(0), "the broken shield is removed after a moment");
        }

        [UnityTest]
        public IEnumerator AShieldwallDrawsTheWallOnEveryStandingAlly()
        {
            yield return OpenTheFight();
            var session = _fight.Session;
            var party = session.Encounter.PlayerParty;
            var hero = party[0];

            hero.PlantedShield.CoversParty = true;
            Assert.IsTrue(session.PlantShield(hero));
            _fight.RefreshShieldPropsForTest();

            for (int i = 0; i < party.Count; i++)
            {
                if (!party[i].IsAlive) continue;

                int slot = i;
                Assert.AreEqual("wall", PropName(slot), $"{party[i].Name} stands behind the wall");
            }
        }
    }
}
