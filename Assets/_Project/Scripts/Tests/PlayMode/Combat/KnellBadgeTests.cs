using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE KNELL BADGE ON SCREEN (docs/PLAN_BELLWETHER_KIT.md 1.5, 3.8; M4).
    // A lone Shawn -- the real one, art and Palace Passage -- against a
    // fixture bell whose schedule opens with a Pull and a seat-sized knell
    // (fixture table [110, 35, 0], ignoring defence, as M5 will author it).
    // Headless-safe: every assertion is about what the wired labels and the
    // badge image carry, not pixels. KnellBadgeCaptureTests takes the picture.
    public class KnellBadgeTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => TestGlobals.ResetAll();

        [UnityTest]
        public IEnumerator TheKnellBadgeRereadsItsNumberAfterAFreePassage()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);

            var (session, shawn, bell) = KnellBadgeFixture.Bind(_fight);
            yield return KnellBadgeFixture.Settle(_fight);

            // THE CHAINS: Pull kind, no number, the callout names what follows.
            Assert.AreEqual(EnemyIntentKind.Pull, session.IntentDetailFor(bell).Value.Kind);
            Assert.IsFalse(KnellBadgeFixture.Value(_fight).gameObject.activeInHierarchy, "a pull deals nothing");
            Assert.AreEqual("Dark Chains! Death Knell next", KnellBadgeFixture.Callout(_fight).text);

            // THE KNELL, at the front where the chains left him: lethal.
            KnellBadgeFixture.ToTheKnell(_fight, session, bell);
            yield return KnellBadgeFixture.Settle(_fight);

            var front = session.IntentDetailFor(bell).Value;
            Assert.AreEqual(EnemyIntentKind.Knell, front.Kind);
            Assert.AreEqual(0, session.Encounter.SeatOf(shawn));
            Assert.IsTrue(front.IsLethal, "110% of his max health, defence ignored");

            var value = KnellBadgeFixture.Value(_fight);
            Assert.IsTrue(value.gameObject.activeInHierarchy);
            Assert.AreEqual(front.ExpectedDamage.ToString(), value.text);
            Assert.AreEqual(Hex(FightHudPalette.IntentLethal), value.color);
            Assert.AreEqual(Hex(FightHudPalette.IntentLethal), KnellBadgeFixture.Badge(_fight).color,
                "the lethal style tints the icon too");
            Assert.AreEqual("Death Knell! Step back", KnellBadgeFixture.Callout(_fight).text);

            // A FREE PASSAGE to the middle, through the controller's own
            // post-action path: the same badge, a smaller number, not lethal.
            int abilityIndex = front.AbilityIndex;
            yield return KnellBadgeFixture.PassageTo(_fight, session, shawn, 1);

            var middle = session.IntentDetailFor(bell).Value;
            Assert.AreEqual(abilityIndex, middle.AbilityIndex, "the bell did not choose again");
            Assert.Less(middle.ExpectedDamage, front.ExpectedDamage);
            Assert.Greater(middle.ExpectedDamage, 0);
            Assert.IsFalse(middle.IsLethal);
            Assert.AreEqual(middle.ExpectedDamage.ToString(), value.text, "the badge re-read the seat");
            Assert.AreEqual(Hex(FightHudPalette.IntentNumber), value.color);
            Assert.AreEqual(Hex(EnemyIntentIcons.TintFor(EnemyIntentKind.Knell)), KnellBadgeFixture.Badge(_fight).color);
            Assert.AreEqual("Death Knell! Step back", KnellBadgeFixture.Callout(_fight).text);
        }

        private static Color Hex(string hex)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out var color), hex);
            return color;
        }
    }
}
