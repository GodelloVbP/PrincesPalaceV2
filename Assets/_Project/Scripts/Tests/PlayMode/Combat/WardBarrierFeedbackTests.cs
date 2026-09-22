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
    // A Ward-protected target, through a real scene, reads as a SHIELD
    // taking the blow rather than as an ordinary hit that happened to deal
    // little or nothing -- the routing this pass's brief asked to be
    // pinned: which flash and which popup(s) a beat with Absorbed > 0
    // actually produces, in each of the two shapes that matters.
    //
    // ONE FIXTURE, TWO CASES: an enormous ward (guaranteed to eat the whole
    // swing, whatever CombatMath rolls it at) and a one-point ward
    // (guaranteed to be smaller than any real hit, so something still
    // reaches health). Sized this way rather than against a predicted
    // damage figure, so the test does not have to duplicate CombatMath's
    // own arithmetic to pick a number -- CLAUDE.md gotcha 5's reasoning,
    // applied to a boundary rather than to an exact value.
    public class WardBarrierFeedbackTests
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

        private CombatantState _foe;

        private IEnumerator LoadFightWithWardedFoe(int wardPoints)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            _foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { _foe });

            var kit = PlayModeSparkFixture.Kit(DamageType.Physical);
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9)) { DamageVarianceRange = 0f };
            session.Begin();

            StatusEffects.ApplyWard(_foe.Statuses, wardPoints, turns: 3, source: _foe);

            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // Every popup currently in flight, oldest slot first -- a partial
        // absorb pops TWO at once (PopAbsorbed then PopNumber, FightBeatPlayer.
        // ShowSingleAmount's own order), and both have to be read to prove
        // neither replaced the other.
        private List<DamagePopup> ActivePopups()
        {
            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(player);
            return player.Popups.Where(p => p != null && !p.IsFree).ToList();
        }

        private static string TextOf(DamagePopup popup) =>
            popup.GetComponentInChildren<TMP_Text>()?.text;

        private static Color ColorOf(DamagePopup popup) =>
            popup.GetComponentInChildren<TMP_Text>().color;

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : Color.magenta;

        private static void AssertSameColor(Color expected, Color actual, string because)
        {
            Assert.AreEqual(expected.r, actual.r, 0.02f, because + " (red)");
            Assert.AreEqual(expected.g, actual.g, 0.02f, because + " (green)");
            Assert.AreEqual(expected.b, actual.b, 0.02f, because + " (blue)");
        }

        // Polled against a wall-clock deadline, not counted in frames -- the
        // same reasoning DamagePopupColorTests.WaitForThePopup states: how
        // many frames a Lunge's wind-up costs before impact is not this
        // test's business to pin.
        private IEnumerator WaitUntil(System.Func<bool> condition, string timeoutMessage)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && !condition())
            {
                yield return null;
            }

            Assert.IsTrue(condition(), timeoutMessage);
        }

        [UnityTest]
        public IEnumerator AFullyWardedHit_PopsAbsorbedOnlyAndFlashesTheBarrierNotTheHit()
        {
            yield return LoadFightWithWardedFoe(wardPoints: 999999);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return WaitUntil(() => ActivePopups().Count > 0,
                "fixture: a fully-absorbed hit produced no popup at all");

            var popups = ActivePopups();
            Assert.AreEqual(1, popups.Count,
                "a fully absorbed hit should show ONE popup (Absorbed), not a health-damage one alongside it");
            StringAssert.StartsWith("ABSORBED", TextOf(popups[0]),
                "a fully absorbed hit's popup should read as absorbed, not as a landed number");
            AssertSameColor(Hex(FightHudPalette.WardText), ColorOf(popups[0]),
                "the absorbed popup did not use the ward text colour");

            var flashImage = Named("Enemy0HitFlash")?.GetComponent<Image>();
            Assert.IsNotNull(flashImage, "fixture: the enemy slot has no hit-flash image");
            yield return WaitUntil(() => flashImage.enabled,
                "a fully-absorbed hit never flashed anything at all");

            AssertSameColor(Hex(FightHudPalette.WardBright), flashImage.color,
                "a fully absorbed hit should flash the barrier tint, not the ordinary white/typed hit flash");
        }

        [UnityTest]
        public IEnumerator APartiallyWardedHit_PopsBothAbsorbedAndHealthDamage()
        {
            yield return LoadFightWithWardedFoe(wardPoints: 1);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return WaitUntil(() => ActivePopups().Count >= 2,
                "a partially-absorbed hit should show both an Absorbed popup and a health-damage popup");

            var popups = ActivePopups();
            Assert.IsTrue(popups.Any(p => (TextOf(p) ?? "").StartsWith("ABSORBED")),
                "no popup read as the absorbed amount");
            Assert.IsTrue(popups.Any(p => (TextOf(p) ?? "").StartsWith("-")),
                "no popup read as the health damage that still landed");
        }
    }
}
