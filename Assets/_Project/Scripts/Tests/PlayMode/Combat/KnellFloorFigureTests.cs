using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE KNELL'S FLOOR FIGURES AND THE CALLOUT PLATE ON THE REAL SCREEN
    // (docs/PLAN_BELLWETHER_KIT.md M5): hidden while the chains are the
    // intent, up at all three seats with the model's figures and colours
    // while the knell is committed, re-read after a free Passage, and the
    // callout on its dark plate whenever it shows. The figures themselves are
    // pinned with literals by BellwetherPresentationTests; this pins that the
    // scene paints what the model says. Headless.
    public class KnellFloorFigureTests
    {
        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => TestGlobals.ResetAll();

        private static T[] Wired<T>(FightController fight, string field) where T : class
        {
            var array = typeof(FightController)
                .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(fight) as T[];
            Assert.IsNotNull(array, $"{field} is not wired");
            Assert.AreEqual(3, array.Length, $"{field}: one per seat");
            return array;
        }

        private static IEnumerator Fight(System.Action<FightController, FightSession,
            PrincesPalace.Domain.Combat.CombatantState, PrincesPalace.Domain.Combat.CombatantState> use)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            var fight = Object.FindAnyObjectByType<FightController>();
            var (session, shawn, bell) = KnellBadgeFixture.Bind(fight);
            yield return KnellBadgeFixture.Settle(fight);
            use(fight, session, shawn, bell);
        }

        private static void AssertPainted(FightController fight, FightSession session)
        {
            var figures = FightHudModel.SeatFiguresFor(session, readable: true);
            var plates = Wired<GameObject>(fight, "partySeatFigures");
            var labels = Wired<TMP_Text>(fight, "partySeatFigureLabels");

            for (int seat = 0; seat < 3; seat++)
            {
                Assert.IsTrue(plates[seat].activeInHierarchy, $"seat {seat}: shown");
                Assert.AreEqual(figures[seat].Text, labels[seat].text, $"seat {seat}: the model's figure");
                string hex = figures[seat].Style == FightHudModel.SeatFigureStyle.Lethal ? FightHudPalette.IntentLethal
                    : figures[seat].Style == FightHudModel.SeatFigureStyle.Safe ? FightHudPalette.SeatFigureSafe
                    : FightHudPalette.IntentKnell;
                Assert.AreEqual(hex.Substring(1, 6), ColorUtility.ToHtmlStringRGB(labels[seat].color), $"seat {seat}: colour");
                Assert.AreEqual(figures[seat].IsTargetSeat ? 1.2f : 1f, labels[seat].transform.localScale.x, 0.001f,
                    $"seat {seat}: his seat is drawn larger");
            }
        }

        [UnityTest]
        public IEnumerator WhileTheChainsAreTheIntent_NoFigureShows_AndTheCalloutSitsOnItsPlate()
        {
            FightController fight = null;
            yield return Fight((f, s, shawn, bell) => fight = f);

            Assert.IsTrue(Wired<GameObject>(fight, "partySeatFigures").All(p => !p.activeInHierarchy));
            Assert.IsTrue(KnellBadgeFixture.Callout(fight).gameObject.activeInHierarchy, "\"Dark Chains! Death Knell next\"");
            var plate = KnellBadgeFixture.Callout(fight).transform.parent;
            Assert.IsTrue(plate.gameObject.activeInHierarchy, "on its plate");
            StringAssert.StartsWith("EnemyIntentCalloutPlate", plate.name);
        }

        [UnityTest]
        public IEnumerator WhileTheKnellIsCommitted_EverySeatShowsItsFigure_AndAPassageRereadsThem()
        {
            FightController fight = null;
            FightSession session = null;
            PrincesPalace.Domain.Combat.CombatantState shawn = null;
            yield return Fight((f, s, sh, bell) =>
            {
                fight = f;
                session = s;
                shawn = sh;
                KnellBadgeFixture.ToTheKnell(f, s, bell);
            });
            yield return KnellBadgeFixture.Settle(fight);

            AssertPainted(fight, session);
            var labels = Wired<TMP_Text>(fight, "partySeatFigureLabels");
            Assert.AreEqual("SAFE", labels[2].text, "the rear takes nothing");
            Assert.AreEqual(1.2f, labels[0].transform.localScale.x, 0.001f, "the chains put him at the front");

            yield return KnellBadgeFixture.PassageTo(fight, session, shawn, 2);

            AssertPainted(fight, session);
            Assert.AreEqual(1.2f, labels[2].transform.localScale.x, 0.001f, "now he stands at the rear");
            Assert.AreEqual(1f, labels[0].transform.localScale.x, 0.001f);
        }
    }
}
