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
    // A fight, driven through the real scene by clicking the real buttons.
    //
    // Deliberately SMALL. The rules themselves are covered by ~180 EditMode
    // tests that need no scene at all, so what is left for PlayMode is only what
    // genuinely needs one: that the wiring reaches the session, that a beat's
    // snapshot is what gets painted, and that the popup pool comes back.
    //
    // That inversion is the point of the whole decomposition. v1's
    // FightControllerTests was 2,117 lines of scene-loading assertions about
    // damage numbers.
    public class FightFlowTests
    {
        private FightController _fight;
        private CombatantState _front;

        [SetUp]
        public void PlayBeatsFast()
        {
            // Twelve beats at 0.75s each would be nine real seconds per test.
            // The multiplier changes nothing about ORDER, which is all these
            // assert.
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void RestoreBeatSpeed()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        // Searched from THIS fight's own root, not globally.
        //
        // Resources.FindObjectsOfTypeAll also returns objects from a scene that
        // is unloading, and these tests load the same scene repeatedly -- so a
        // global lookup can hand back last test's plate, still holding last
        // test's numbers. That is not hypothetical: it is what made an attack
        // that demonstrably reduced the enemy's health still read 400/400.
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

        private string TextOf(string name) => Named(name).GetComponent<TMP_Text>().text;

        private bool Active(string name) => Named(name).activeSelf;

        // Deliberately far more health than one swing can take off.
        //
        // The first draft used 400 and the hero one-shot it, so the plate had
        // correctly DEACTIVATED and the assertion was reading a dead enemy's
        // stale label. A fixture that dies changes which code path is under
        // test without saying so.
        private IEnumerator LoadFight(int foeHealth = 5000, int heroMana = 30)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, heroMana, 40, 10);
            hero.Signature = new SignatureResource("wool", "Wool", 16, 0, 2, 0);

            var foes = new[]
            {
                new CombatantState("Front", false, foeHealth, 10, 8, 4),
                new CombatantState("Back", false, foeHealth, 10, 8, 3),
            };

            _front = foes[0];
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);

            var enemyKits = foes.Select(f => new EnemyKit(
                new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name, new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false)).ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { kit }, enemyKits, new SeededRandom(9));
            session.AppendMessage("The forest closes in.");
            session.Begin();

            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // ---- the wiring reaches the session ------------------------------------

        [UnityTest]
        public IEnumerator TheOpeningLineShowsBeforeAnyBeatExists()
        {
            // AppendMessage's immediate path, which only ever runs in this one
            // window: before a single beat has been recorded.
            yield return LoadFight();

            StringAssert.Contains("The forest closes in.", TextOf("MessageLabel"));
        }

        [UnityTest]
        public IEnumerator ThePlatesReadTheRealEnemies()
        {
            yield return LoadFight();

            Assert.IsTrue(Active("EnemyPlate0"));
            Assert.IsTrue(Active("EnemyPlate1"));
            Assert.IsFalse(Active("EnemyPlate2"), "only two monsters are in this room");
            Assert.AreEqual("Front", TextOf("EnemyPlate0Name"));
            StringAssert.Contains("5000", TextOf("EnemyPlate0Hp"));
        }

        [UnityTest]
        public IEnumerator PressingAttackOpensTargetingWithoutASubmenu()
        {
            yield return LoadFight();

            Click("Verb0");

            Assert.IsTrue(Active("TargetPrompt"));
            Assert.IsFalse(Active("SubmenuColumn"), "ATTACK has no list to show");
            Assert.IsTrue(Active("DetailColumn"), "but it still describes the swing");
            StringAssert.Contains("T A R G E T", TextOf("Breadcrumb"));
        }

        [UnityTest]
        public IEnumerator PressingSkillOpensTheColumnWithTheBasicSpellInIt()
        {
            yield return LoadFight();

            Click("Verb1");

            Assert.IsTrue(Active("SubmenuColumn"));
            Assert.IsTrue(Active("CharacterSkill0"), "one row: the basic spell");
            Assert.IsFalse(Active("CharacterSkill1"), "and nothing beyond it");
            Assert.AreEqual("Spark", TextOf("CharacterSkill0Name"));
        }

        [UnityTest]
        public IEnumerator OpeningSkillPutsItInOpenStateAndAttackInPrimary()
        {
            // FightController.RefreshVerbs' own three-way distinction, now
            // painted through ThemedButtonState.SetMenuState instead of a
            // flat targetGraphic.color write -- see FightController.Hud.cs.
            yield return LoadFight();

            var attackState = Named("Verb0").GetComponent<ThemedButtonState>();
            var skillState = Named("Verb1").GetComponent<ThemedButtonState>();
            Assert.IsNotNull(attackState, "Verb0 should be a themed button (ThemedPlate(Crimson))");
            Assert.IsNotNull(skillState, "Verb1 should be a themed button (ThemedPlate(Violet))");

            // Resting: nothing is OPEN (Branch == None, ActiveVerbIndex -1),
            // but gamepad focus defaults to index 0 -- the same verb the
            // menu's own recommendation happens to be -- so `highlighted`
            // is already true for ATTACK before anything is ever pressed.
            // Open, not Primary, is the state that actually paints at rest;
            // Primary only shows once focus or an open branch moves
            // elsewhere while index 0 stays neither.
            Assert.AreEqual(ThemedMenuState.Open, attackState.CurrentMenuState);
            Assert.AreEqual(ThemedMenuState.Idle, skillState.CurrentMenuState);

            Click("Verb1");

            // SKILL's own branch open (ActiveVerbIndex 1) makes ATTACK
            // neither the open verb NOR the default gamepad focus (the
            // `active < 0 && ...` clause only ever applies while nothing is
            // open) -- so it falls all the way through to i == 0, its
            // Primary/recommended-default colouring, exactly as it read
            // VerbIdlePrimary before this migration.
            Assert.AreEqual(ThemedMenuState.Primary, attackState.CurrentMenuState);
            Assert.AreEqual(ThemedMenuState.Open, skillState.CurrentMenuState);
        }

        [UnityTest]
        public IEnumerator OpenVerbGlowsWithAHaloLargerThanItsPlate()
        {
            // Item 1 of the 2026-09-03 handoff: Selected (SetMenuState Open,
            // here Verb0 at rest per the test above it) grows the glow rect
            // past the plate's own edges instead of only brightening in
            // place - a halo, not a same-size ring.
            yield return LoadFight();

            var attackState = Named("Verb0").GetComponent<ThemedButtonState>();
            Assert.AreEqual(ThemedMenuState.Open, attackState.CurrentMenuState);

            // The fade/scale coroutines are event-driven and unscaled-time
            // based (~150ms) - give them a couple of frames' worth of real
            // time to settle before asserting the end state.
            yield return new WaitForSecondsRealtime(0.25f);

            var plateRect = attackState.Plate.rectTransform;
            var glowRect = attackState.GlowRect;
            Assert.Greater(glowRect.lossyScale.x, 1f, "Open state should scale the glow rect past 1.0");
            Assert.AreEqual(ThemedButtonState.SelectedGlowScale, glowRect.localScale.x, 0.01f);

            float plateWidth = plateRect.rect.width * plateRect.lossyScale.x;
            float glowWidth = glowRect.rect.width * glowRect.lossyScale.x;
            Assert.Greater(glowWidth, plateWidth, "the glow should read as a halo around the plate, not the same footprint");
        }

        [UnityTest]
        public IEnumerator TheOnlyRowLandsJustAboveBack_WhateverTheCount()
        {
            // v1's design preview drew rows at 8-slot positions and left a gap
            // above BACK.
            //
            // MEASURED IN WORLD SPACE, not against the row's own
            // anchoredPosition, and the difference is the scrolling container:
            // a row's local y is now its fixed slot in a sixteen-row pool, and
            // what puts the LIST where it belongs is the rect around it. Asking
            // the row where it thinks it is would only ever get the pool answer
            // back, which is 405 whatever the count and means nothing on its
            // own.
            //
            // World space is also the more honest question. The claim is about
            // where the row lands ON SCREEN relative to BACK, and that is true
            // or false regardless of how many rects are nested between them.
            yield return LoadFight();

            Click("Verb1");

            var row = (RectTransform)Named("CharacterSkill0").transform;
            var back = (RectTransform)Named("SubmenuBack").transform;

            float rowBottom = row.position.y - row.rect.height * 0.5f * row.lossyScale.y;
            float backTop = back.position.y + back.rect.height * 0.5f * back.lossyScale.y;

            Assert.Greater(rowBottom, backTop, "the only row stacks above BACK, never through it");
            Assert.Less(rowBottom - backTop, 8f * row.lossyScale.y,
                "and immediately above it, with no dead space -- which is what bottom-anchoring buys");
        }

        [UnityTest]
        public IEnumerator AnAttackReachesTheSessionAndMovesTheEnemy()
        {
            yield return LoadFight();

            Click("Verb0");
            Click("EnemyPlate0");

            // Playback is running; wait it out at 60x.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(_fight.IsBusy, "playback never finished");
            Assert.Less(_front.CurrentHealth, _front.MaxHealth, "the session never resolved the swing");
            StringAssert.Contains("attacks", TextOf("MessageLabel"));
            Assert.IsTrue(Active("EnemyPlate0"), "the fixture is meant to SURVIVE the swing");
            StringAssert.DoesNotContain("5000/5000", TextOf("EnemyPlate0Hp"), "the front rank took the hit");
        }

        [UnityTest]
        public IEnumerator TheMenuClosesItselfOnEveryResolution()
        {
            // Which is what makes the branch trustworthy as the sole authority
            // for whether the column is up.
            yield return LoadFight();

            Click("Verb0");
            Click("EnemyPlate0");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(Active("TargetPrompt"));
            Assert.IsFalse(Active("DetailColumn"));
            Assert.AreEqual("C O M M A N D", TextOf("Breadcrumb"));
        }

        [UnityTest]
        public IEnumerator TheDetailCardHidesTheInstantAnActionCommits_NotAfterTheAnimationFinishes()
        {
            // The test above only checks AFTER waiting for IsBusy to clear --
            // which is exactly the gap the real bug lived in. RefreshUi() used
            // to run once, at OnPlaybackFinished, once the WHOLE beat sequence
            // was done playing -- so for the entire duration of the swing/cast
            // animation the skill-detail card (and the target prompt) stayed
            // on screen showing the choice the player already committed to,
            // and only vanished at the very end alongside the log line. This
            // checks the moment the click resolves, while the animation is
            // still running, which no other test in this file does.
            yield return LoadFight();

            Click("Verb0");
            Click("EnemyPlate0");

            Assert.IsTrue(_fight.IsBusy,
                "fixture: the beat should still be playing right after the click, or this proves nothing");
            Assert.IsFalse(Active("TargetPrompt"), "the target prompt must not survive into its own animation");
            Assert.IsFalse(Active("DetailColumn"), "the skill card must not survive into its own animation");

            // ATTACK/SKILL/ITEM/HOLD BACK too, on the same report: the verb
            // column used to stay up for the whole animation right alongside
            // the card and the prompt, describing choices the player was not
            // currently making.
            for (int i = 0; i < 4; i++)
            {
                Assert.IsFalse(Active($"Verb{i}"), $"Verb{i} must not survive into the animation it triggered");
            }
        }

        [UnityTest]
        public IEnumerator BackingOutOfTargetingCostsNoTurn()
        {
            yield return LoadFight();
            string before = TextOf("EnemyPlate0Hp");

            Click("Verb1");
            Click("CharacterSkill0");
            Assert.IsTrue(Active("TargetPrompt"));

            Click("SubmenuBack");

            Assert.IsFalse(Active("TargetPrompt"));
            Assert.IsTrue(Active("SubmenuColumn"), "back from a target lands on the list it came from");
            Assert.AreEqual(before, TextOf("EnemyPlate0Hp"), "and nothing was spent");
        }

        [UnityTest]
        public IEnumerator ARearEnemyCannotBeMeleedThroughTheFrontRank()
        {
            // The front-rank rule is the session's, enforced at a click.
            yield return LoadFight();

            Click("Verb0");
            Click("EnemyPlate1");

            Assert.IsTrue(Active("TargetPrompt"), "the refusal does not spend the turn");
            StringAssert.Contains("out of reach", TextOf("MessageLabel"));
        }

        [UnityTest]
        public IEnumerator AnEmptySatchelRefusesRatherThanOpeningAnEmptyColumn()
        {
            yield return LoadFight();

            Click("Verb2");

            Assert.IsFalse(Active("SubmenuColumn"));
            StringAssert.Contains("No items", TextOf("MessageLabel"));
        }

        // ---- the popup pool comes back -------------------------------------------

        [UnityTest]
        public IEnumerator FlushReclaimsEveryInFlightPopup()
        {
            // THE LEAK. v1's Flush left popups running and its Clear had zero
            // call sites, so an abandoned fight leaked one popup per in-flight
            // number out of a pool of six that is never refilled -- and the next
            // fight showed no numbers at all, with nothing saying why.
            yield return LoadFight();

            var player = Object.FindAnyObjectByType<FightBeatPlayer>();
            Assert.IsNotNull(player);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return null;

            Assert.IsTrue(player.Popups.Any(p => !p.IsFree), "fixture: at least one popup is in flight");

            player.Flush();

            Assert.IsTrue(player.Popups.All(p => p.IsFree), "every popup must come back");
            Assert.IsFalse(player.IsPlaying);
        }

        [UnityTest]
        public IEnumerator ThePoolSurvivesRepeatedFights()
        {
            // The consequence of the fix, stated as the symptom it prevents.
            yield return LoadFight();
            var player = Object.FindAnyObjectByType<FightBeatPlayer>();

            for (int round = 0; round < 4; round++)
            {
                Click("Verb0");
                Click("EnemyPlate0");
                yield return null;
                player.Flush();
            }

            Assert.AreEqual(player.Popups.Count, player.Popups.Count(p => p.IsFree),
                "after four abandoned rounds the pool is still whole");
        }

        [UnityTest]
        public IEnumerator DisablingThePlayerReclaimsToo()
        {
            // A scene change mid-round is the abandoned-fight case, and it must
            // not be the caller's job to remember.
            yield return LoadFight();
            var player = Object.FindAnyObjectByType<FightBeatPlayer>();

            Click("Verb0");
            Click("EnemyPlate0");
            yield return null;

            player.gameObject.SetActive(false);

            Assert.IsTrue(player.Popups.All(p => p.IsFree));
        }
    }
}
