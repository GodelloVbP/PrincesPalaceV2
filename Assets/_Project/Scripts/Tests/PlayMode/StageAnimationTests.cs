using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
    // The three things that make a blow look like a blow: the figure moves, it
    // flashes white, and its pose steps through its own frames.
    //
    // ALL THREE ARE FOUND BY GetComponent AT PLAYBACK TIME, which is a silent
    // failure mode by construction -- a missing component is a null and an early
    // return, not an error. The lunge was exactly that: StageActorAnimator
    // existed, FightBeatPlayer.Lunge called for it, and nothing ever attached
    // one, so for the entire life of the fight screen nothing moved and every
    // test passed.
    public class StageAnimationTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private IEnumerator OpenAFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);
            yield return null;
        }

        // ---- the components a playback call site reaches for -------------------

        [UnityTest]
        public IEnumerator EveryStageSlotCanActuallyBeMoved()
        {
            // The regression this file exists for.
            yield return OpenAFight();

            var slots = _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .Where(t => t.name.EndsWith("Slot") && (t.name.StartsWith("Enemy") || t.name.StartsWith("Party")))
                .ToList();

            Assert.IsNotEmpty(slots, "the stage has no slots at all - the naming convention moved");

            var unmovable = slots
                .Where(s => s.GetComponent<StageActorAnimator>() == null)
                .Select(s => s.name)
                .ToList();

            CollectionAssert.IsEmpty(unmovable,
                "these slots have no animator, so Lunge silently does nothing for them: " +
                string.Join(", ", unmovable));
        }

        [UnityTest]
        public IEnumerator EveryStageSlotCanActuallyFlash()
        {
            yield return OpenAFight();

            var slots = _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .Where(t => t.name.EndsWith("Slot") && (t.name.StartsWith("Enemy") || t.name.StartsWith("Party")))
                .ToList();

            var unflashable = slots
                .Where(s => s.GetComponentInChildren<StageHitFlash>(includeInactive: true) == null)
                .Select(s => s.name)
                .ToList();

            CollectionAssert.IsEmpty(unflashable,
                "these slots have no hit flash: " + string.Join(", ", unflashable));
        }

        // ---- and that they do something ----------------------------------------

        private IEnumerator ABoundFight()
        {
            yield return OpenAFight();

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 0, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 0, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AttackingMovesTheAttacker()
        {
            yield return ABoundFight();

            var slot = (RectTransform)Named("Party0Slot").transform;
            float rest = slot.anchoredPosition.x;

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            // Catch it mid-lunge rather than after: the animator returns the
            // figure to where it started, so waiting for playback to finish
            // would assert on the resting position either way.
            bool moved = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (!Mathf.Approximately(slot.anchoredPosition.x, rest)) moved = true;
                yield return null;
            }

            Assert.IsTrue(moved, "the attacker never left its slot - the lunge did nothing");
        }

        [UnityTest]
        public IEnumerator TheAttackerComesBackToItsSlot()
        {
            // A figure that lunges and stays there drifts across the stage over a
            // long fight.
            yield return ABoundFight();

            var slot = (RectTransform)Named("Party0Slot").transform;
            float rest = slot.anchoredPosition.x;

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            // Waited on as a condition, for the same reason as above.
            var animator = slot.GetComponent<StageActorAnimator>();
            float settle = Time.realtimeSinceStartup + 5f;
            while (!Mathf.Approximately(slot.anchoredPosition.x, animator.Home.x)
                   && Time.realtimeSinceStartup < settle) yield return null;

            Assert.AreEqual(rest, slot.anchoredPosition.x, 0.5f,
                "the figure is parked off its mark - over a long fight it drifts across the stage");
        }

        [UnityTest]
        public IEnumerator TakingAHitFlashesTheTarget()
        {
            yield return ABoundFight();

            var flash = Named("Enemy0HitFlash").GetComponent<Image>();
            Assert.IsNotNull(flash);

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            bool flashed = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (flash.enabled && flash.color.a > 0.01f) flashed = true;
                yield return null;
            }

            Assert.IsTrue(flashed, "the target never flashed - the one signal that means 'you were hit'");
        }

        [UnityTest]
        public IEnumerator TheFlashWearsOff()
        {
            yield return ABoundFight();

            var flash = Named("Enemy0HitFlash").GetComponent<Image>();

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            float clear = Time.realtimeSinceStartup + 5f;
            while (flash.enabled && flash.color.a >= 0.02f && Time.realtimeSinceStartup < clear) yield return null;

            Assert.IsTrue(!flash.enabled || flash.color.a < 0.02f,
                "a flash that never clears leaves the figure a white silhouette");
        }
    
        // ---- recoil --------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryStageSlotCanActuallyFade()
        {
            yield return OpenAFight();

            var slots = _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .Where(t => t.name.EndsWith("Slot") && (t.name.StartsWith("Enemy") || t.name.StartsWith("Party")))
                .ToList();

            var unfadable = slots
                .Where(s => s.GetComponent<StageDeathFade>() == null)
                .Select(s => s.name)
                .ToList();

            CollectionAssert.IsEmpty(unfadable,
                "these slots cannot fade, so a corpse would sit on the stage: " + string.Join(", ", unfadable));
        }

        [UnityTest]
        public IEnumerator BeingHitMovesTheTargetToo()
        {
            // The other half of a blow. Without it only the attacker moves, and
            // the hit reads as the target ignoring it.
            yield return ABoundFight();

            var slot = (RectTransform)Named("Enemy0Slot").transform;
            float rest = slot.anchoredPosition.x;

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            bool flinched = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (!Mathf.Approximately(slot.anchoredPosition.x, rest)) flinched = true;
                yield return null;
            }

            Assert.IsTrue(flinched, "the target never flinched");
        }

        [UnityTest]
        public IEnumerator AMonsterFlinchesIntoItsOwnHalf()
        {
            // Away is decided by which SIDE a figure is on, not by where the
            // blow came from -- deriving it from the attacker would send a
            // back-row monster stumbling toward the party when a status tick
            // hit it from behind.
            yield return ABoundFight();

            var slot = (RectTransform)Named("Enemy0Slot").transform;
            float rest = slot.anchoredPosition.x;

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            float furthest = rest;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (slot.anchoredPosition.x > furthest) furthest = slot.anchoredPosition.x;
                yield return null;
            }

            Assert.Greater(furthest, rest, "a monster stands on the right, so it recoils to the RIGHT");
        }

        // ---- the fade ------------------------------------------------------------

        [UnityTest]
        public IEnumerator AFreshFightUnfadesWhoeverDiedInTheLastOne()
        {
            // The slot is reused rather than rebuilt, so a figure left at zero
            // alpha would begin the next fight invisible.
            yield return ABoundFight();

            var sprite = Named("Enemy0Sprite").GetComponent<Image>();
            Named("Enemy0Slot").GetComponent<StageDeathFade>().PlayIfNotAlready();

            // Waited on as a CONDITION, not a frame count. A fixed count passes
            // alone and fails in the full suite, because how much wall-clock a
            // frame represents depends on what else is running -- which is a
            // property of the test host, not of the thing being tested.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (sprite.color.a > 0.98f && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.Less(sprite.color.a, 0.99f, "fixture: it never actually faded");

            yield return ABoundFight();

            Assert.AreEqual(1f, Named("Enemy0Sprite").GetComponent<Image>().color.a, 0.01f,
                "the next fight started with an invisible monster");
        }
}
}
