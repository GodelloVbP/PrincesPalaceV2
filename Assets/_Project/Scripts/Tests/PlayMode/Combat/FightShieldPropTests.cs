using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The planted shield is a stage prop the fight view draws in front of its
    // holder's seat: intact while more than half the placed size is left,
    // cracked at or below half, broken for a moment after a break and then
    // gone, and a Shieldwall draws that same shield at every standing ally's
    // seat, one shared pool cracking and breaking everywhere together. The
    // crack and the break land on the beat that caused them.
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

            // Planted inside a beat, as a real Plant the Shield is, so the
            // beat log has seen the shield stand before it breaks.
            session.BeatAroundForTest(hero, hero, () => session.PlantShield(hero));
            session.DrainBeats();
            _fight.RefreshShieldPropsForTest();
            Assert.AreEqual("intact", PropName(0));

            var breaking = session.BeatAroundForTest(foe, hero,
                () => session.StrikeForTest(foe, hero, 100000, DamageType.Physical));
            session.DrainBeats();
            Assert.IsFalse(hero.PlantedShield.IsPlaced, "fixture: the hit broke the shield");

            // A live repaint alone never shows a break: only the beat does.
            _fight.RefreshShieldPropsForTest();
            Assert.IsNull(PropName(0), "an idle repaint cannot tell a break from an expiry");

            _fight.PaintShieldsForTest(breaking);
            Assert.AreEqual("broken", PropName(0));

            // Cleared by its own timer, not by the next repaint.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (PropName(0) != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNull(PropName(0), "the broken shield is removed after a moment");
        }

        [UnityTest]
        public IEnumerator TheShieldCracksOnTheCrossingBlowAndBreaksOnTheBreakingBlow()
        {
            yield return OpenTheFight();
            FightBeatPlayer.BeatSpeedMultiplier = 4f;
            var session = _fight.Session;
            var hero = session.Encounter.PlayerParty[0];
            var foe = session.Encounter.Enemies[0];

            session.PlantShield(hero);
            session.DrainBeats();

            var crossing = session.BeatAroundForTest(foe, hero,
                () => hero.PlantedShield.Ward.Magnitude = hero.PlantedShield.PlacedPoints / 2);
            var breaking = session.BeatAroundForTest(foe, hero,
                () => session.StrikeForTest(foe, hero, 100000, DamageType.Physical));
            session.DrainBeats();
            Assert.IsFalse(hero.PlantedShield.IsPlaced, "fixture: the round has already resolved to a break");

            _fight.PlayBeatsForTest(new List<CombatBeat> { crossing, breaking });

            // Live state is already broken and nothing repaints it while busy,
            // so a cracked prop can only have come from the crossing beat.
            var seen = new List<string>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                string name = PropName(0);
                if (name != null && (seen.Count == 0 || seen[seen.Count - 1] != name)) seen.Add(name);
                if (name == "broken") break;
                yield return null;
            }

            CollectionAssert.AreEqual(new[] { "cracked", "broken" }, seen,
                "the crack shows on the crossing blow and the break on the breaking blow, nothing before");
        }

        [UnityTest]
        public IEnumerator AShieldwallDrawsOneShieldAtEveryStandingAllyAndCracksTogether()
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

                Assert.AreEqual("intact", PropName(i), $"{party[i].Name} has one shield, not a wall strip");
            }

            hero.PlantedShield.Ward.Magnitude = hero.PlantedShield.PlacedPoints / 2;
            _fight.RefreshShieldPropsForTest();

            for (int i = 0; i < party.Count; i++)
            {
                if (!party[i].IsAlive) continue;

                Assert.AreEqual("cracked", PropName(i), $"{party[i].Name} reads the shared pool");
            }
        }
    }
}
