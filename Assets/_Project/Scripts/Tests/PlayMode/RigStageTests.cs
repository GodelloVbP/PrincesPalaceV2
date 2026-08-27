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
    }
}
