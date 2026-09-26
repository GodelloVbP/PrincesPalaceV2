using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
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
        public void RestoreBeatSpeed()
        {
            // THE SCENE IS SHARED ACROSS THIS FIXTURE (SharedScene): each test
            // rebinds over it, and this stops what a rebind does not.
            FightSceneFixture.QuietForReuse(_fight);
            SharedScene.AfterTest();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

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
            yield return SharedScene.EnsureFight();

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

        // THE BLOW NO LONGER LANDS ON THE FRAME AFTER THE CLICK. Neither
        // figure in this fixture has art, so both are still drawings, and a
        // still-drawing Lunge now spends StaticSwing's wind-up
        // (anticipation plus the lunge itself) before the popup appears -- one
        // or two frames at 60x, where it used to be zero. Polled against a
        // deadline rather than counted in frames, because "how many frames"
        // is exactly the number this test has no business pinning.
        private IEnumerator WaitForThePopup()
        {
            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(player);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && !player.Popups.Any(p => p != null && !p.IsFree))
            {
                yield return null;
            }
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
            yield return WaitForThePopup();

            AssertSameColor(Hex(FightHudPalette.DamageTypeFire), ActivePopupColor(),
                "a Fire-typed attack's popup did not pop in the Fire token");
        }

        [UnityTest]
        public IEnumerator APoisonHitPopsInThePoisonColour()
        {
            yield return LoadFightWithAttackType(DamageType.Poison);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return WaitForThePopup();

            AssertSameColor(Hex(FightHudPalette.DamageTypePoison), ActivePopupColor(),
                "a Poison-typed attack's popup did not pop in the Poison token");
        }

        // ONE case from the five types this pass added, not all five --
        // this fixture pays for a full scene load per case, and every case
        // is mechanically identical (see Fire/Poison above); it only needs
        // to prove a NEW type reaches the popup the same way, not that
        // FightHudPalette's own per-type mapping is exhaustive (that is
        // DamageTypeCompletenessTests' job, cheaply, without a scene).
        [UnityTest]
        public IEnumerator AVoidHitPopsInTheVoidColour()
        {
            yield return LoadFightWithAttackType(DamageType.Void);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return WaitForThePopup();

            AssertSameColor(Hex(FightHudPalette.DamageTypeVoid), ActivePopupColor(),
                "a Void-typed attack's popup did not pop in the Void token");
        }

        // A second case worth the scene load rather than left to
        // DamageTypeCompletenessTests alone: Lightning Bolt (skills.json)
        // is the first authored content in the game to actually deal this
        // type, so this is the first time the token gets exercised by real
        // content rather than only by the synthetic fixture above.
        [UnityTest]
        public IEnumerator ALightningHitPopsInTheLightningColour()
        {
            yield return LoadFightWithAttackType(DamageType.Lightning);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return WaitForThePopup();

            AssertSameColor(Hex(FightHudPalette.DamageTypeLightning), ActivePopupColor(),
                "a Lightning-typed attack's popup did not pop in the Lightning token");
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
            yield return WaitForThePopup();

            AssertSameColor(Hex(FightHudPalette.DamageTypePhysical), ActivePopupColor(),
                "a Physical attack's popup drifted off the flat red every hit used to show");
        }

        // READABLE ON ANY SPELL. QA 2026-09-26: Winter's Rebuke's "-4" (Ice, a
        // pale face) sat on a white-blue impact burst and could barely be read.
        // Every number now carries a dark edge, and the pool still draws after
        // every spell layer -- both halves are needed: on top and unreadable is
        // what shipped.
        [UnityTest]
        public IEnumerator EveryNumberIsDrawnAboveTheSpellsWithADarkEdge()
        {
            yield return LoadFightWithAttackType(DamageType.Ice);

            var popup = _fight.GetComponentsInChildren<DamagePopup>(includeInactive: true).FirstOrDefault();
            Assert.IsNotNull(popup, "the fight scene has no damage popup");

            popup.Play(Vector2.zero, 4, false, DamageType.Ice, 5f);
            var label = popup.GetComponentInChildren<TMP_Text>(includeInactive: true);
            Assert.IsNotNull(label);

            Assert.GreaterOrEqual(label.outlineWidth, 0.2f, "the number has no edge to read against a bright spell");
            // TMP_SDF-Mobile draws no outline at any width without this keyword.
            Assert.IsTrue(label.fontMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline),
                "the outline width is set but the shader's outline branch is off, so no edge is drawn");
            Color32 edge = label.outlineColor;
            Assert.LessOrEqual((edge.r + edge.g + edge.b) / 3f, 32f, "the number's edge is not dark");
            Assert.GreaterOrEqual(edge.a, 200, "the number's edge is too faint to separate it from a bright spell");

            // Draw order: the popup pool is a later sibling than every spell pool
            // under their shared parent, so it paints over them.
            var pool = Named("DamagePopups").transform;
            foreach (var name in new[] { "SpellVfx", "SpellParticles", "SpellGroundVfx" })
            {
                var spell = Named(name);
                if (spell == null) continue;
                Assert.AreSame(pool.parent, spell.transform.parent, $"{name} and the popups no longer share a parent");
                Assert.Greater(pool.GetSiblingIndex(), spell.transform.GetSiblingIndex(),
                    $"{name} paints over the damage numbers");
            }

            popup.Reclaim();
        }
    }
}
