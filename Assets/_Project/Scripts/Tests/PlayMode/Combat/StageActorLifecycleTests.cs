using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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

namespace PrincesPalace.PlayModeTests
{
    // HUNT 2026-09-11, STAGE MOVERS, FAMILY A (docs/hunt/SCENARIOS.md rows
    // A2, A3, A4, A5, A6): a second call arriving while the first one's
    // coroutine is still running.
    //
    // WHY THE INTERRUPTED CASE IS ITS OWN SET OF TESTS. Every one of these
    // five classes stops its previous coroutine and starts another, and
    // StageAnimationTests already asserts what each does UNINTERRUPTED. What
    // nothing asserted is the frame after the stop: a coroutine that is
    // stopped mid-flight leaves whatever it last wrote (an offset, a stretch,
    // an alpha, a rack shoved sideways) sitting on the transform, and only
    // the code path that stops it can put that back. The failure is silent by
    // construction -- a figure a few pixels off its mark, or one that never
    // comes back to full opacity -- so it is exactly the family that survives
    // a green suite.
    //
    // DRIVEN AGAINST THE REAL SCENE'S OWN COMPONENTS, not a hand-built rig:
    // three of the five (StageDeathFade, and both halves of the animator's
    // composition) read serialized Image references that only the scene
    // binds, and StageHitFlash needs a real sprite before it will flash at
    // all.
    public class StageActorLifecycleTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast()
        {
            // AUTHORED PACE, where most Fight fixtures hurry, and the reason
            // is the whole point of this file: every test here has to catch a
            // coroutine MID-flight. StageHitFlash's whole life is 0.21s and
            // the death fade's hold is 0.35s -- at 60x both are over inside a
            // single frame, and an interrupt that cannot be timed is not an
            // interrupt. Five tests at 1x is about ten seconds of clock.
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;

            // The breath writes the same transform the lunge does, and its
            // composition is somebody else's test (StageAnimationTests'
            // ReHomingAFigureMidBreath...). Off, so a home-drift assertion
            // here is reading the lunge alone.
            FightController.BreathSpeedMultiplier = 0f;
        }

        [TearDown]
        public void Restore()
        {
            // THE SCENE IS SHARED ACROSS THIS FIXTURE (SharedScene): each test
            // rebinds over it, and this stops what a rebind does not.
            FightSceneFixture.QuietForReuse(_fight);
            SharedScene.AfterTest();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightController.BreathSpeedMultiplier = 1f;
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private IEnumerator AFightAgainst(string spritePath)
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SharedScene.EnsureFight();
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = PlayModeSparkFixture.Kit();
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, spritePath: spritePath), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // ---- A2: a lunge interrupted by a second lunge -------------------------

        [UnityTest]
        public IEnumerator ASecondLungeOnTheSameFigureLeavesItOnItsMarkRatherThanDrifting()
        {
            yield return AFightAgainst("Enemies/golem");

            var slot = (RectTransform)Named("Party0Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();
            Assert.IsNotNull(animator, "the party slot has no animator");

            var home = animator.Home;
            var baseScale = animator.BaseScale;

            animator.Play(new Vector2(140f, 20f), 0.20f);
            yield return null;
            yield return null;

            Assert.IsTrue(animator.IsPlaying, "fixture: the first lunge is already over, so nothing is interrupted");
            Assert.AreNotEqual(home, slot.anchoredPosition,
                "fixture: the first lunge never moved the figure, so the interrupt proves nothing");

            // THE INTERRUPT. Play's own comment says a second hit landing
            // mid-animation restarts the move rather than stacking on it --
            // this is what "rather than stacking" has to mean at the end:
            // the home the second lunge measures from is the mark, not
            // wherever the first one had got to.
            animator.Play(new Vector2(-90f, 0f), 0.20f);

            Assert.AreEqual(home, animator.Home,
                "the interrupt moved the figure's HOME -- every later beat now measures from a drift");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (animator.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(animator.IsPlaying, "the second lunge never finished");

            // ONE handle, not two: a second PlayRoutine still running would
            // keep writing this transform after the first reported done.
            yield return null;
            yield return null;

            Assert.That(Vector2.Distance(slot.anchoredPosition, home), Is.LessThan(0.01f),
                $"the figure settled at {slot.anchoredPosition} instead of its mark {home} -- " +
                "an interrupted lunge left its travel on the transform");
            Assert.That(Vector3.Distance(slot.localScale, baseScale), Is.LessThan(0.001f),
                $"the figure settled at scale {slot.localScale} instead of {baseScale} -- " +
                "an interrupted lunge left its stretch on the transform");
        }

        // ---- A3: a lunge landing on a figure that is mid-walk -------------------

        [UnityTest]
        public IEnumerator ALungeOnAWalkingFigureStillLetsTheWalkArriveExactlyOnItsMark()
        {
            yield return AFightAgainst("Enemies/golem");

            var slot = (RectTransform)Named("Party0Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();

            var mark = animator.Home + new Vector2(220f, -40f);
            var scale = animator.BaseScale * 1.2f;

            animator.GlideTo(mark, scale, 0.6f);
            yield return null;
            yield return null;

            Assert.IsTrue(animator.IsGliding, "fixture: the glide is already over, so nothing is interrupted");

            // GlideTo's own header: a lunge, a punch or a hover already in
            // flight rides along on top and is composed rather than
            // cancelled. The other direction -- a lunge STARTING mid-walk --
            // is the one nothing pinned, and it is the live path (an enemy
            // swings in the same round a Move walks two party members past
            // each other).
            animator.Play(new Vector2(60f, 0f), 0.15f);

            Assert.IsTrue(animator.IsGliding,
                "the lunge cancelled the walk -- the figure will stop wherever it was when the blow landed");

            float deadline = Time.realtimeSinceStartup + 10f;
            while ((animator.IsGliding || animator.IsPlaying) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsFalse(animator.IsGliding, "the walk never arrived");
            Assert.IsFalse(animator.IsPlaying, "the lunge never finished");

            Assert.AreEqual(mark, animator.Home,
                "the walk did not arrive exactly on its mark, so AnchorOne will walk this figure again " +
                "on the next repaint");
            Assert.That(Vector2.Distance(slot.anchoredPosition, mark), Is.LessThan(0.01f),
                $"the figure ended at {slot.anchoredPosition} rather than on the mark it walked to");
        }

        // ---- A4: a second hit while the first flash is fading --------------------

        [UnityTest]
        public IEnumerator ASecondHitRestartsTheFlashAtFullAndItStillWearsOff()
        {
            yield return AFightAgainst("Enemies/golem");

            var flash = Named("Enemy0Slot").GetComponentInChildren<StageHitFlash>(includeInactive: true);
            Assert.IsNotNull(flash, "the enemy slot has no hit flash");

            var image = flash.GetComponent<Image>();
            Assert.IsNotNull(image, "the hit flash has no Image to write");
            Assert.IsNotNull(image.sprite,
                "the flash has no silhouette, so Flash() is a no-op and this test would pass vacuously");

            flash.Flash();
            yield return null;

            // Wait for the hold to pass and the fade to have visibly started,
            // so the second flash lands on a PARTIAL alpha.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (image.color.a > 0.85f && image.color.a > 0f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.Greater(image.color.a, 0f, "fixture: the first flash was over before it could be interrupted");
            Assert.Less(image.color.a, 0.86f, "fixture: the first flash never started fading");

            flash.Flash();
            yield return null;

            Assert.That(image.color.a, Is.GreaterThan(0.99f),
                "the second hit picked the fade up where the first left it instead of restarting at full");

            deadline = Time.realtimeSinceStartup + 5f;
            while (image.enabled && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(image.enabled,
                "the restarted flash never wore off -- two Run coroutines were writing one colour and " +
                "the loser cleared while the winner kept painting");
            Assert.AreEqual(0f, image.color.a, 0.001f, "the flash cleared without clearing its alpha");
        }

        // ---- A5: a second kick while the rack is still shaking --------------------

        [UnityTest]
        public IEnumerator ASecondKickShakesOnceAndPutsTheRackBackExactly()
        {
            yield return AFightAgainst("Enemies/golem");

            var shakes = _fight.StageShakesForTest;
            Assert.IsNotEmpty(shakes, "the stage has no shakers");

            var shake = shakes[0];
            var rack = (RectTransform)shake.transform;
            var home = rack.anchoredPosition;

            // THE AMPLITUDE CEILING is what says "one shake, not two". Kicking
            // is additive on _home, and the constant it can never exceed is
            // 30px sideways at full strength -- so a second kick composing
            // with the first (rather than replacing it) shows up as an offset
            // past that ceiling, and nothing else can produce one.
            const float MaxPixels = 30f;

            shake.Kick(1f);
            yield return null;
            yield return null;

            Assert.AreNotEqual(home, rack.anchoredPosition, "fixture: the first kick never moved the rack");

            shake.Kick(1f);

            // A full-strength kick is 0.23 x 1.3 = 0.3s, so a second one
            // starting now is certainly over inside a second -- watched the
            // whole way rather than until the rack happens to read as home,
            // which a random offset can hit at any moment.
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until)
            {
                var offset = rack.anchoredPosition - home;
                Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(MaxPixels + 0.01f),
                    "the rack was thrown further than one full-strength kick can throw it -- " +
                    "two Kicking coroutines are writing the same rack");
                yield return null;
            }

            Assert.AreEqual(home, rack.anchoredPosition,
                "the rack never came back to rest -- an interrupted kick left it parked off-mark for " +
                "the next encounter");
        }

        // ---- A6: a revival arriving mid-fade ---------------------------------------

        [UnityTest]
        public IEnumerator AFigureRevivedMidFadeEndsFullyOpaqueAndStaysThere()
        {
            yield return AFightAgainst("Enemies/golem");

            var fade = Named("Enemy0Slot").GetComponent<StageDeathFade>();
            Assert.IsNotNull(fade, "the enemy slot has no death fade");

            var sprite = Named("Enemy0Sprite").GetComponent<Image>();
            Assert.IsNotNull(sprite, "the enemy slot has no figure to fade");

            float opaque = sprite.color.a;
            Assert.Greater(opaque, 0.5f, "fixture: the figure is not visible to begin with");

            fade.PlayIfNotAlready();

            float deadline = Time.realtimeSinceStartup + 5f;
            while (sprite.color.a > opaque * 0.9f && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.Less(sprite.color.a, opaque * 0.9f, "fixture: the fade never started");
            Assert.Greater(sprite.color.a, 0f, "fixture: the fade finished before it could be interrupted");

            // THE SECOND LIFE. This is the live path for one: RefreshCombatant-
            // Sprite calls ResetToVisible the moment the slot repaints a
            // combatant that is alive again, and it arrives mid-fade by
            // construction, because the fade is the only reason there is still
            // a body on screen to revive.
            fade.ResetToVisible();

            Assert.AreEqual(opaque, sprite.color.a, 0.001f,
                "the revived figure was left at the alpha the fade happened to reach");
            Assert.IsFalse(fade.Faded, "a revived figure still reports itself as gone, so the line closes over it");

            // AND THE STOPPED COROUTINE IS NOT STILL WRITING. The assertion
            // above holds for one frame whether or not FadeRoutine was
            // actually stopped; this is the one that tells them apart.
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
                Assert.AreEqual(opaque, sprite.color.a, 0.001f,
                    $"the figure dimmed to {sprite.color.a:F3} on frame {frame} after being revived -- " +
                    "the interrupted fade is still running");
            }
        }
    }
}
