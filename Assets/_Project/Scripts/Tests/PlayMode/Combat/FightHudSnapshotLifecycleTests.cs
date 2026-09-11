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

namespace PrincesPalace.PlayModeTests
{
    // HUNT 2026-09-11, FINDING F7 -- the two defects at PaintVitals, both of
    // them about a beat being a RECORD OF A MOMENT and the painter reaching
    // past the record for live state anyway.
    //
    // 1. THE DENOMINATOR. The snapshot held three CURRENT values and no
    //    maxima, so every bar was drawn as recorded-current over LIVE-max.
    //    Transformation.Enter/Exit moves MaxHealth twice per transform,
    //    mid-round, inside a round that is then replayed beat by beat -- so
    //    every beat recorded before the entry drew its recorded health over
    //    the post-transform maximum. This is clearance ledger row K9's own
    //    stated invalidator ("a Max that can move mid-fight"), and it has
    //    landed.
    //
    // 2. THE CARD. PaintVitals walked the live party list and wrote plate i
    //    for party[i]. The round has already finished resolving by the time
    //    a beat plays, so a Move in it has already traded two members' list
    //    positions -- while the cards they own are still where the last full
    //    repaint drew them. Their numbers landed on each other's portraits
    //    for the rest of the playback.
    //
    // AND AUDIT #144, the design question 4c4bddc3 did not settle. Fixing
    // (2) by addressing the plates by SLOT froze the column: it kept its
    // opening order for the whole fight, and a Move was visible only as two
    // figures trading places on the stage. The owner's call (2026-09-11) is
    // that the column FOLLOWS THE FIELD -- so a third test below pins the
    // reorder, and the two above it stay exactly as they were, because the
    // rule that replaces the slot has to keep both of them green. That rule
    // is a painted-occupancy record: RefreshPcPlates writes which member it
    // put on each card, PaintVitals reads it, and the two orders differ for
    // precisely the length of one playback.
    //
    // DRIVEN THROUGH THE REAL SCENE, a hand-built three-member session, and
    // the real buttons -- FightFlowTests' shape, extended to three party
    // members because both defects need a party that can Move and three
    // distinguishable denominators. The maximum is moved by writing
    // MaxHealth directly rather than by casting Black Ram Mode: the site
    // under test is the PAINTER, which cannot tell the two apart, and a real
    // transform would additionally need the transform skill, its art and a
    // caster who has learned it (TransformFlashTests pays that cost for the
    // silhouette question, which genuinely needs it).
    //
    // READ BY DENOMINATOR, not by the whole label. The enemies swing back in
    // the same round, so a numerator is not stable across a playback -- what
    // is stable is that Alpha's card says "/300" and Gamma's says "/200".
    public class FightHudSnapshotLifecycleTests
    {
        private FightController _fight;
        private FightBeatPlayer _beats;
        private CombatantState _alpha;
        private CombatantState _beta;
        private CombatantState _gamma;

        private const int AlphaMax = 300;
        private const int BetaMax = 250;
        private const int GammaMax = 200;

        [SetUp]
        public void PinTheClocks()
        {
            // AUTHORED PACE, deliberately, where most Fight fixtures hurry.
            // Both tests below SAMPLE playback frame by frame and refuse a
            // vacuous green, and a single-beat round at 60x is over in one
            // frame -- the speed-up would buy two seconds and cost the only
            // window either test can see. Two beats at 1x is about a second.
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightController.BreathSpeedMultiplier = 0f;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            // These fights are left standing rather than played to a close,
            // so flush whatever beat player is live before the next fixture's
            // scene load finds it mid-playback (AUDIT #106; the same
            // [UnityTearDown] TransformFlashTests and FightControllerFacing-
            // Tests already carry).
            LogAssert.ignoreFailingMessages = true;
            if (_beats != null) _beats.EndFight();
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            // TestGlobals.ResetAll(), not a hand-rolled restore: this fixture
            // pins BeatSpeedMultiplier, PlayerSpeedSource/AdoptPlayerSpeed and
            // BreathSpeedMultiplier in [SetUp], and the sanctioned restore for
            // the adopted factor is ResetAll (GlobalStateLintTests' table --
            // AdoptPlayerSpeed has no direct assignment a teardown can pin
            // back to 1 on its own).
            TestGlobals.ResetAll();
        }

        // ---- the stand-up ----------------------------------------------------

        // Searched from THIS fight's own root, never globally -- these
        // fixtures load the same scene repeatedly and a global lookup can
        // hand back the previous load's plate, still holding the previous
        // load's numbers (FightFlowTests.Named's own note).
        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the Fight scene");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private string TextOf(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the Fight scene");
            return go.GetComponent<TMP_Text>().text;
        }

        private IEnumerator LoadThreeMemberFight()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _beats = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(_beats, "the Fight scene has no FightBeatPlayer");

            // RE-PINNED AFTER THE LOAD, not only before it: FightBootstrap.
            // Start installs the production settings-backed source during
            // this same load, so a pin from before it is overwritten a moment
            // later (FightSceneFixture's own note).
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightBeatPlayer.AdoptPlayerSpeed();

            // FightBootstrap's own placeholder fight is mid-beat and holds
            // the controller busy, so a click would be swallowed.
            _beats.Flush();
            yield return null;
            yield return null;

            // DISTINCT MAXIMA, and fast enough to act before the monsters do
            // -- Alpha at the front, so Alpha is the one with an ally behind
            // to trade places with.
            _alpha = new CombatantState("Alpha", true, AlphaMax, 30, 12, 40);
            _beta = new CombatantState("Beta", true, BetaMax, 30, 10, 30);
            _gamma = new CombatantState("Gamma", true, GammaMax, 30, 10, 20);

            // Deliberately far more health than one swing can take off, for
            // FightFlowTests' reason: a fixture that dies changes which code
            // path is under test without saying so.
            var foes = new[]
            {
                new CombatantState("Front", false, 5000, 10, 6, 4),
                new CombatantState("Back", false, 5000, 10, 6, 3),
            };

            var encounter = new CombatEncounter(new[] { _alpha, _beta, _gamma }, foes);

            var kits = new List<PlayerKit>
            {
                PlayModeSparkFixture.Kit(), PlayModeSparkFixture.Kit(), PlayModeSparkFixture.Kit(),
            };

            var enemyKits = foes.Select(f => new EnemyKit(
                new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name, new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false)).ToList();

            var session = new FightSession(encounter, kits, enemyKits, new SeededRandom(20260911));
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // ---- reading one plate ------------------------------------------------

        // The denominator on a plate's HP caption, or -1 when the caption is
        // not a "{0}/{1}" at all. UiStrings.HealthValue's format is the
        // contract being parsed, and it is one place.
        private int HpMaxOnPlate(int plate)
        {
            string label = TextOf($"PcPlate{plate}HpValue");
            int slash = label.IndexOf('/');
            if (slash < 0) return -1;

            return int.TryParse(label.Substring(slash + 1), out int max) ? max : -1;
        }

        private string NameOnPlate(int plate) => TextOf($"PcPlate{plate}Name");

        private static int MaxFor(string name)
        {
            switch (name)
            {
                case "Alpha": return AlphaMax;
                case "Beta": return BetaMax;
                case "Gamma": return GammaMax;
                default: return -1;
            }
        }

        // THE INVARIANT, asked of every plate that has an occupant: the
        // numbers on a card belong to the character whose portrait and name
        // are on that card. It is deliberately phrased against the NAME
        // rather than against a plate index, so it holds whether or not the
        // column reorders -- the defect is the two disagreeing, not either
        // one's order.
        private void AssertEveryCardCarriesItsOwnNumbers(string when)
        {
            for (int plate = 0; plate < 3; plate++)
            {
                if (!Named($"PcPlate{plate}").activeSelf) continue;

                string name = NameOnPlate(plate);
                int expected = MaxFor(name);
                if (expected < 0) continue;

                Assert.AreEqual(expected, HpMaxOnPlate(plate),
                    $"{when}: plate {plate} says it belongs to {name}, but its health bar is drawn " +
                    "against somebody else's maximum -- the numbers landed on the wrong card");
            }
        }

        // ---- F7, second half: the card ----------------------------------------

        [UnityTest]
        public IEnumerator AMovesPlaybackDoesNotLandTwoMembersNumbersOnEachOthersCards()
        {
            yield return LoadThreeMemberFight();

            Assert.AreEqual("Alpha", NameOnPlate(0), "fixture: Alpha opens at the front");
            AssertEveryCardCarriesItsOwnNumbers("before the move");

            // MOVE (verb 3), then BACK (row 1 -- FightHudModel.MoveRows' own
            // fixed order, FORWARD then BACK).
            Click("Verb3");
            Click("CharacterSkill1");

            Assert.IsTrue(_fight.IsBusy,
                "fixture: the move should still be playing right after the click, or this proves nothing");

            int samples = 0;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                AssertEveryCardCarriesItsOwnNumbers("during the move's playback");
                samples++;
                yield return null;
            }

            Assert.IsFalse(_fight.IsBusy, "playback never finished");
            Assert.Greater(samples, 1, "fixture: playback was never actually sampled mid-flight");

            AssertEveryCardCarriesItsOwnNumbers("after the move settled");
        }

        // ---- #144: the column follows the field --------------------------------

        // THE INVERSE OF WHAT 4c4bddc3 SHIPPED. That commit's slot-indexing
        // meant Alpha's card stayed at plate 0 for the whole fight no matter
        // where Alpha stood; the owner's call is that the card follows the
        // rank, so after a BACK the front card is the ally who took the
        // front.
        //
        // READ OFF THE NAME, and the name only, because the three fixture
        // kits are identical (PlayModeSparkFixture.Kit): plate art and plate
        // theme genuinely do move with the member -- RefreshPcPlate paints
        // every one of them off the same `member` -- but this fixture cannot
        // tell them apart, so it does not claim to. The meters it CAN tell
        // apart, and AssertEveryCardCarriesItsOwnNumbers below is the check
        // that they moved with the name rather than staying behind.
        [UnityTest]
        public IEnumerator AMoveReordersTheColumnToFollowTheField()
        {
            yield return LoadThreeMemberFight();

            Assert.AreEqual("Alpha", NameOnPlate(0), "fixture: Alpha opens at the front");
            Assert.AreEqual("Beta", NameOnPlate(1), "fixture: Beta opens one rank behind Alpha");

            // The same two clicks as the test above -- MOVE (verb 3), then
            // BACK (row 1). Alpha and Beta trade places.
            Click("Verb3");
            Click("CharacterSkill1");

            float deadline = Time.realtimeSinceStartup + 15f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(_fight.IsBusy, "playback never finished");

            Assert.AreEqual("Beta", NameOnPlate(0),
                "Alpha moved BACK and Beta took the front rank, but the front card still shows " +
                "Alpha: the column is holding its opening order instead of following the field");
            Assert.AreEqual("Alpha", NameOnPlate(1),
                "Beta came forward, so Alpha's card belongs where Beta's was");

            // THE WHOLE CARD MOVED, not just the caption: each plate's
            // denominator has to be its new occupant's maximum.
            AssertEveryCardCarriesItsOwnNumbers("after the column followed the move");
        }

        // ---- F7, first half: the denominator -----------------------------------

        [UnityTest]
        public IEnumerator ABeatPaintsTheMaximumItRecordedRatherThanTheOneTheFightHasMovedOnTo()
        {
            yield return LoadThreeMemberFight();

            Click("Verb0");
            Click("EnemyPlate0");

            Assert.IsTrue(_fight.IsBusy,
                "fixture: the swing should still be playing right after the click, or this proves nothing");

            // THE TRANSFORM, stripped to the one thing the painter can see:
            // a maximum that moves after the round was recorded and before
            // its beats have finished playing. Gamma is nobody's target, so
            // the numerator on that card cannot move for any other reason.
            _gamma.MaxHealth = GammaMax + 500;

            int samples = 0;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                Assert.AreEqual(GammaMax, HpMaxOnPlate(2),
                    "a beat recorded before the maximum moved was replayed against the NEW maximum: " +
                    "the recorded health is drawn over a denominator from after the moment being painted");
                samples++;
                yield return null;
            }

            Assert.IsFalse(_fight.IsBusy, "playback never finished");
            Assert.Greater(samples, 1, "fixture: playback was never actually sampled mid-flight");

            // AND LIVE STATE IS CORRECT AGAIN THE MOMENT PLAYBACK IS OVER,
            // which is the other half of the rule: the full repaint at
            // OnPlaybackFinished is showing the present, not a record, so it
            // reads the maximum the fight actually has now.
            Assert.AreEqual(GammaMax + 500, HpMaxOnPlate(2),
                "the settled HUD is live state, and live state really did gain 500 maximum health");
        }
    }
}
