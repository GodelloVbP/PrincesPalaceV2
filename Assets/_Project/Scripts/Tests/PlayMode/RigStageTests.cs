using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.U2D.Animation;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // Phase 4 checkpoint: a real fight, fielding the rat (the one enemy with
    // a rig), actually routes to the world-space rig path instead of the
    // frame-sheet Image path -- not just that RigLibrary.Resolve returns
    // non-null in isolation, but that FightController's own RefreshStage
    // wiring picks it up, hides the old Image, and puts a real rig instance
    // with real bind-pose geometry under the matching world slot.
    //
    // GRAPHICS-GATED. A live SpriteSkin instance in an active scene gets
    // rendered by the scene's own camera every frame regardless of whether
    // this test calls Camera.Render() itself -- same crash risk under
    // -nographics as RigImportIntegrityTests, self-skips the same way.
    public class RigStageTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void RestoreSpeed() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        [UnityTest]
        public IEnumerator RatEnemy_RoutesToTheRigPath_NotTheFrameSheetImage()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.RigStageTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            Assert.IsNotNull(hero, "no playable character in content -- can't build an encounter");

            var built = FightEncounterAdapter.Build(
                new System.Collections.Generic.List<string> { hero.id },
                new System.Collections.Generic.List<string> { "rat" },
                new SeededRandom(7), isBoss: false, isElite: false);
            Assert.IsNotNull(built?.Session, "'rat' could not be built into an encounter");

            built.Session.Begin();
            _fight.Bind(built.Session, EncounterClass.Normal);
            yield return null;
            yield return null;

            // RefreshStage runs every frame while busy and at least once on
            // Bind -- a couple more frames is margin, not a real wait.
            for (int i = 0; i < 5; i++) yield return null;

            var worldSlot = Named("Enemy0WorldSlot");
            Assert.IsNotNull(worldSlot, "Enemy0WorldSlot missing from the built scene");
            Assert.IsTrue(worldSlot.activeInHierarchy, "Enemy0WorldSlot should be shown -- the rat is the only enemy, it's slot 0");

            var rigRenderers = worldSlot.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            Assert.AreEqual(7, rigRenderers.Length,
                "expected the rat rig's 7 parts under its world slot -- RigLibrary didn't resolve, or RefreshRigActor didn't instantiate under the right slot");

            var rigSkins = worldSlot.GetComponentsInChildren<SpriteSkin>(includeInactive: true);
            Assert.AreEqual(7, rigSkins.Length, "expected a SpriteSkin per rig part -- this should be the real rig prefab, not a plain sprite");

            var enemySprite = Named("Enemy0Sprite");
            Assert.IsNotNull(enemySprite, "Enemy0Sprite missing from the built scene");
            Assert.IsFalse(enemySprite.activeSelf,
                "the frame-sheet Image should be hidden once a rig resolves -- RefreshCombatantSprite's graceful-degradation branch should have called image.gameObject.SetShown(false)");

            // The rig's own SpriteRenderers, not the (hidden) Image, should
            // be what's actually enabled and visible.
            Assert.IsTrue(rigRenderers.All(r => r.enabled), "every rig part's SpriteRenderer should be enabled");
        }

        // Phase 5 checkpoint: a real beat actually drives the rig's BONES,
        // not just its bind pose. A hit on the rat poses it "hurt" and
        // FlinchFrames resolves that through PlaybackFor -- if the seam
        // is wired correctly this should be a RigStancePlayback sampling
        // animations.json's hurt clip, which rotates head/body/tail away
        // from the bind pose's 0deg. Sampled mid-flight for the same
        // reason FightPlayableTests.AMultiFrameStanceActuallyAnimates is:
        // the round settles back on the bind pose (ResetToRest) before it
        // ends, so checking afterwards would pass whether or not anything
        // ever moved.
        [UnityTest]
        public IEnumerator AHitOnTheRatActuallyRotatesItsBones()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.RigStageTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var built = FightEncounterAdapter.Build(
                new System.Collections.Generic.List<string> { hero.id },
                new System.Collections.Generic.List<string> { "rat" },
                new SeededRandom(7), isBoss: false, isElite: false);
            built.Session.Begin();
            _fight.Bind(built.Session, EncounterClass.Normal);
            yield return null;
            yield return null;

            var worldSlot = Named("Enemy0WorldSlot");
            var head = worldSlot.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == "head");
            Assert.IsNotNull(head, "expected a 'head' bone Transform under the rat's world slot");

            var verb = Named("Verb0")?.GetComponent<Button>();
            var target = Named("EnemyPlate0")?.GetComponent<Button>();
            Assert.IsNotNull(verb, "no Verb0 button to click");
            Assert.IsNotNull(target, "no EnemyPlate0 button to click");
            verb.onClick.Invoke();
            target.onClick.Invoke();

            float furthestFromRest = 0f;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                float deviation = Mathf.Abs(Mathf.DeltaAngle(0f, head.localRotation.eulerAngles.z));
                if (deviation > furthestFromRest) furthestFromRest = deviation;
                yield return null;
            }

            Assert.Greater(furthestFromRest, 1f,
                "the rat's head bone never rotated more than 1deg away from bind pose during the whole round -- " +
                "either the hurt clip has no head track, or the flinch never reached RigStancePlayback at all");
        }

        // Phase C checkpoint: a real hit actually MOVES the world slot
        // itself, not just the rig's bones. Before this fix, Recoil/Punch/
        // TravelFor drove only the uGUI slot's StageActorAnimator -- which
        // still exists and still recoils, but its only remaining visible
        // children are the shadow, glow and nameplate, since the rig's own
        // Image is hidden. The rat itself stood bolt still while everything
        // around its feet flinched. This checks the actual world slot's
        // own anchoredPosition/localScale, which RigStageTests' own bone
        // test above cannot see -- a bone rotating and a slot translating
        // are two different Transforms, and this fix is specifically about
        // the second one.
        //
        // Sampled mid-flight for the same reason the bone test above is:
        // AnchorStageSlots' own guard re-homes a slot back to its mark once
        // the round settles, so checking afterwards would pass whether or
        // not the slot ever actually moved.
        [UnityTest]
        public IEnumerator AHitOnTheRatActuallyMovesItsWorldSlot()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device (-nographics). Run: tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests.RigStageTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var built = FightEncounterAdapter.Build(
                new System.Collections.Generic.List<string> { hero.id },
                new System.Collections.Generic.List<string> { "rat" },
                new SeededRandom(7), isBoss: false, isElite: false);
            built.Session.Begin();
            _fight.Bind(built.Session, EncounterClass.Normal);
            yield return null;
            yield return null;

            var rat = built.Session.Encounter.Enemies[0];
            var worldSlot = _fight.WorldSlotForTest(rat);
            Assert.IsNotNull(worldSlot, "expected a world slot for the rat");

            var animator = worldSlot.GetComponent<StageActorAnimator>();
            Assert.IsNotNull(animator, "the rat's world slot has no StageActorAnimator -- ScreenRegistry's attach loop should cover world slots now");

            var restMark = animator.Home;
            var restScale = animator.BaseScale;

            var verb = Named("Verb0")?.GetComponent<Button>();
            var target = Named("EnemyPlate0")?.GetComponent<Button>();
            verb.onClick.Invoke();
            target.onClick.Invoke();

            float furthestFromMark = 0f;
            float scaleDeviation = 0f;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                float dist = Vector2.Distance(worldSlot.anchoredPosition, restMark);
                if (dist > furthestFromMark) furthestFromMark = dist;

                float scaleDist = Vector2.Distance(
                    new Vector2(worldSlot.localScale.x, worldSlot.localScale.y),
                    new Vector2(restScale.x, restScale.y));
                if (scaleDist > scaleDeviation) scaleDeviation = scaleDist;

                yield return null;
            }

            Assert.Greater(furthestFromMark, 0.001f,
                "the rat's world slot never moved away from its own mark during the whole round -- Recoil/Punch/" +
                "TravelFor's world-space mirror never actually reached it");
        }
    }
}
