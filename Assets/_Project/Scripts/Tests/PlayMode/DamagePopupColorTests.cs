using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
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
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // A real attack, through a real scene, ends up painted with a real
    // colour -- FightHudPaletteDamageTypeTests pins the token table; this is
    // the one seam proving a beat actually carries its DamageType through
    // FightController.AfterResolution to DamagePopup.Play, rather than every
    // hit still showing the old flat red.
    public class DamagePopupColorTests
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

        // A single hero attacking with `attackType` -- see FightSession.
        // ActorAttackType and AttackTypeOf, which both read PlayerKit.
        // AttackType for a basic swing. Health is deliberately huge so the
        // fixture never one-shots the target mid-test.
        private IEnumerator LoadFightWithAttackType(DamageType attackType)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });

            var kit = PlayModeSparkFixture.Kit(attackType);
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // The colour off whichever popup the last click set running. Real
        // component read (GetComponentInChildren<TMP_Text>), not the
        // internal `label` field DamagePopup itself uses -- PlayMode has no
        // InternalsVisibleTo grant, by design (see CODE_STANDARDS.md 3/4a).
        private Color ActivePopupColor()
        {
            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(player);

            var popup = player.Popups.FirstOrDefault(p => p != null && !p.IsFree);
            Assert.IsNotNull(popup, "fixture: no popup is in flight to read a colour from");

            var label = popup.GetComponentInChildren<TMP_Text>();
            Assert.IsNotNull(label, "fixture: the popup has no label to read a colour from");
            return label.color;
        }

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : Color.magenta;

        private static void AssertSameColor(Color expected, Color actual, string because)
        {
            Assert.AreEqual(expected.r, actual.r, 0.02f, because + " (red)");
            Assert.AreEqual(expected.g, actual.g, 0.02f, because + " (green)");
            Assert.AreEqual(expected.b, actual.b, 0.02f, because + " (blue)");
        }

        [UnityTest]
        public IEnumerator AFireHitPopsInTheFireColour()
        {
            yield return LoadFightWithAttackType(DamageType.Fire);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return null;

            AssertSameColor(Hex(FightHudPalette.DamageTypeFire), ActivePopupColor(),
                "a Fire-typed attack's popup did not pop in the Fire token");
        }

        [UnityTest]
        public IEnumerator APoisonHitPopsInThePoisonColour()
        {
            yield return LoadFightWithAttackType(DamageType.Poison);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return null;

            AssertSameColor(Hex(FightHudPalette.DamageTypePoison), ActivePopupColor(),
                "a Poison-typed attack's popup did not pop in the Poison token");
        }

        // The control: a Physical hit still shows the same colour it always
        // did, so this whole feature is additive rather than a reskin of the
        // common case.
        [UnityTest]
        public IEnumerator APhysicalHitStillPopsInThePhysicalColour()
        {
            yield return LoadFightWithAttackType(DamageType.Physical);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return null;

            AssertSameColor(Hex(FightHudPalette.DamageTypePhysical), ActivePopupColor(),
                "a Physical attack's popup drifted off the flat red every hit used to show");
        }
    }
}
